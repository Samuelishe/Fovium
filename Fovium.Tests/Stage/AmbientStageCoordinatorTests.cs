using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Rendering;
using Fovium.Stage;
using Fovium.Viewer;

namespace Fovium.Tests.Stage;

public sealed class AmbientStageCoordinatorTests
{
    [Theory]
    [InlineData((int)StageBackgroundMode.Black, false)]
    [InlineData((int)StageBackgroundMode.Black, true)]
    [InlineData((int)StageBackgroundMode.Neutral, false)]
    [InlineData((int)StageBackgroundMode.Neutral, true)]
    [InlineData((int)StageBackgroundMode.Custom, false)]
    [InlineData((int)StageBackgroundMode.Custom, true)]
    public async Task SolidBackgroundsNeverScheduleAmbientPreparation(int modeValue, bool matteEnabled)
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new RecordingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with
            {
                BackgroundMode = (StageBackgroundMode)modeValue,
                MatteEnabled = matteEnabled,
            });

        SelectAndStart(coordinator, "photo", image.Identity);
        repository.RaiseAdjacentImageAvailable();
        await coordinator.WaitForIdleAsync();

        Assert.Equal(0, preparer.CallCount);
        Assert.False(image.HasAmbient);
        Assert.Equal(0, coordinator.GetMetrics().ScheduledWorkCount);
        Assert.Equal(0, coordinator.GetMetrics().PreparedCount);
    }

    [Fact]
    public async Task MattePresentationChangesReusePreparedAmbientWithoutSchedulingWork()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new RecordingPreparer();
        var initial = StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient };
        await using var coordinator = new AmbientStageCoordinator(repository, preparer, initial);
        SelectAndStart(coordinator, "photo", image.Identity);
        await coordinator.WaitForIdleAsync();
        using var before = coordinator.AcquirePresentation();

        coordinator.SetStage(initial with
        {
            MatteEnabled = true,
            MatteColor = new StageColor(0x60, 0x50, 0x40),
            MatteStyle = MatteStyle.Soft,
            MatteWidthPhysicalPixels = 128,
        });
        await coordinator.WaitForIdleAsync();
        using var after = coordinator.AcquirePresentation();

        Assert.Equal(1, preparer.CallCount);
        Assert.NotNull(before.Ambient);
        Assert.NotNull(after.Ambient);
        Assert.Same(before.Ambient!.Image, after.Ambient!.Image);
        Assert.True(after.Stage.MatteEnabled);
        Assert.Equal(new StageColor(0x60, 0x50, 0x40), after.Stage.MatteColor);
        Assert.Equal(MatteStyle.Soft, after.Stage.MatteStyle);
        Assert.Equal(128, after.Stage.MatteWidthPhysicalPixels);
        Assert.Equal(1, coordinator.GetMetrics().ScheduledWorkCount);
    }

    [Fact]
    public async Task BrightnessAndSaturationChangesDoNotRegeneratePreparedAmbient()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new RecordingPreparer();
        var initial = StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient };
        await using var coordinator = new AmbientStageCoordinator(repository, preparer, initial);
        SelectAndStart(coordinator, "photo", image.Identity);
        await coordinator.WaitForIdleAsync();
        using var before = coordinator.AcquirePresentation();

        coordinator.SetStage(initial with { AmbientBrightness = 0.9, AmbientSaturation = 1.2 });
        await coordinator.WaitForIdleAsync();
        using var after = coordinator.AcquirePresentation();

        Assert.Equal(1, preparer.CallCount);
        Assert.Equal(1, coordinator.GetMetrics().ScheduledWorkCount);
        Assert.Same(before.Ambient!.Image, after.Ambient!.Image);
        Assert.Equal(0.9, after.Stage.AmbientBrightness);
        Assert.Equal(1.2, after.Stage.AmbientSaturation);
    }

    [Fact]
    public async Task BlurChangeRepreparesAndPublishesOnlyLatestValue()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new RecordingPreparer();
        var initial = StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient };
        await using var coordinator = new AmbientStageCoordinator(repository, preparer, initial);
        SelectAndStart(coordinator, "photo", image.Identity);
        await coordinator.WaitForIdleAsync();
        var navigationMetrics = coordinator.GetMetrics();

        coordinator.SetStage(initial with { AmbientBlur = 20 });
        coordinator.SetStage(initial with { AmbientBlur = 22 });
        coordinator.SetStage(initial with { AmbientBlur = 24 });
        await coordinator.WaitForIdleAsync();
        using var presentation = coordinator.AcquirePresentation();

        Assert.Equal(2, preparer.CallCount);
        Assert.Equal(new[] { 18d, 24d }, preparer.Blurs);
        Assert.Equal(24, presentation.Ambient?.Blur);
        Assert.Equal(24, presentation.Stage.AmbientBlur);
        Assert.Equal(2, coordinator.GetMetrics().PreparedCount);
        Assert.Equal(1, coordinator.GetMetrics().CurrentAmbientPrepareCount);
        Assert.Equal(
            navigationMetrics.LastPhotoToAmbientPresentationGap,
            coordinator.GetMetrics().LastPhotoToAmbientPresentationGap);
    }

    [Fact]
    public async Task OldBlurMayBridgeCurrentImageButNeverPublishesForNewImage()
    {
        using var repository = new TestRepository();
        var first = StageTestImages.CreateDecoded("first.png");
        var second = StageTestImages.CreateDecoded("second.png");
        Assert.True(first.TryAttachAmbient(StageTestImages.CreateAmbient(blur: 18)));
        Assert.True(second.TryAttachAmbient(StageTestImages.CreateAmbient(blur: 18)));
        repository.Add("first", first, protect: true);
        repository.Add("second", second, protect: false);
        var initial = StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient };
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            new RecordingPreparer(),
            initial);
        SelectAndStart(coordinator, "first", first.Identity);

        coordinator.SetStage(initial with { AmbientBlur = 24 });
        using var transitional = coordinator.AcquirePresentation();
        SelectAndStart(coordinator, "second", second.Identity);
        using var newImage = coordinator.AcquirePresentation();

        Assert.Equal(18, transitional.Ambient?.Blur);
        Assert.Null(newImage.Ambient);
        await coordinator.WaitForIdleAsync();
        using var prepared = coordinator.AcquirePresentation();
        Assert.Equal(24, prepared.Ambient?.Blur);
    }

    [Fact]
    public async Task StaleBlurPreparationCannotPublishAndIsDisposed()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new BlockingPreparer();
        var stage = StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient };
        await using var coordinator = new AmbientStageCoordinator(repository, preparer, stage);
        SelectAndStart(coordinator, "photo", image.Identity);
        await preparer.Started.Task;

        coordinator.ClearImage();
        preparer.Complete();
        await coordinator.WaitForIdleAsync();
        using var presentation = coordinator.AcquirePresentation();

        Assert.False(image.HasAmbient);
        Assert.Null(presentation.Ambient);
        Assert.Equal(1, coordinator.GetMetrics().StaleDisposalCount);
        Assert.True(preparer.PreparedWasDisposed);
    }

    [Fact]
    public async Task ObsoleteCurrentAmbientCannotPublishOverLatestSelection()
    {
        using var repository = new TestRepository();
        var first = StageTestImages.CreateDecoded("first.png");
        var latest = StageTestImages.CreateDecoded("latest.png");
        repository.Add("first", first, protect: true);
        repository.Add("latest", latest, protect: false);
        var preparer = new ObsoleteFirstPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });

        SelectAndStart(coordinator, "first", first.Identity);
        await preparer.FirstStarted.Task;
        SelectAndStart(coordinator, "latest", latest.Identity);
        await preparer.LatestCompleted.Task;
        preparer.CompleteFirst();
        await coordinator.WaitForIdleAsync();
        using var presentation = coordinator.AcquirePresentation();

        Assert.Equal(latest.Identity, presentation.ImageIdentity);
        Assert.NotNull(presentation.Ambient);
        Assert.True(latest.HasAmbientForBlur(18));
        Assert.False(first.HasAmbient);
        Assert.True(preparer.ObsoletePreparedWasDisposed);
        Assert.Equal(1, coordinator.GetMetrics().StaleDisposalCount);
        Assert.Equal(1, coordinator.GetMetrics().CurrentAmbientPrepareCount);
    }

    [Fact]
    public async Task PreparationFailureKeepsAmbientOnBlackFallback()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            new ThrowingPreparer(),
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });

        SelectAndStart(coordinator, "photo", image.Identity);
        await coordinator.WaitForIdleAsync();
        using var presentation = coordinator.AcquirePresentation();

        Assert.Equal(StageBackgroundMode.Ambient, presentation.Stage.BackgroundMode);
        Assert.Null(presentation.Ambient);
        Assert.False(image.HasAmbient);
        Assert.Equal(1, coordinator.GetMetrics().PreparationFailureCount);
        Assert.NotNull(coordinator.LastDiagnostic);
    }

    [Fact]
    public async Task CurrentAmbientPreparationDoesNotDependOnAdjacentAvailability()
    {
        using var repository = new TestRepository();
        var current = StageTestImages.CreateDecoded("current.png");
        repository.Add("current", current, protect: true);
        var preparer = new BlockingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });

        SelectAndStart(coordinator, "current", current.Identity);
        await preparer.Started.Task;

        try
        {
            Assert.False(current.HasAmbient);
        }
        finally
        {
            preparer.Complete();
        }

        await coordinator.WaitForIdleAsync();
        Assert.True(current.HasAmbientForBlur(18));
    }

    [Fact]
    public async Task CurrentAmbientPublishesWithoutAnyAdjacentProgressSignal()
    {
        using var repository = new TestRepository();
        var current = StageTestImages.CreateDecoded("current.png");
        repository.Add("current", current, protect: true);
        var preparer = new RecordingPreparer();
        var ambientPublished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowPublicationToContinue = new ManualResetEventSlim(false);
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });
        coordinator.PresentationChanged += (_, _) =>
        {
            using var presentation = coordinator.AcquirePresentation();
            if (presentation.Ambient is not null)
            {
                ambientPublished.TrySetResult();
                allowPublicationToContinue.Wait();
            }
        };

        SelectAndStart(coordinator, "current", current.Identity);
        await ambientPublished.Task;

        try
        {
            var metrics = coordinator.GetMetrics();
            Assert.Equal(1, metrics.CurrentAmbientPrepareCount);
            Assert.Equal(0, metrics.CurrentAmbientCacheHitCount);
            Assert.Equal(0, metrics.AdjacentAmbientPreparedCount);
            Assert.NotNull(metrics.LastPhotoToAmbientPresentationGap);
            Assert.False(metrics.LastCurrentAmbientWasCacheHit);
            Assert.Equal(
                TimeSpan.FromMilliseconds(1),
                metrics.LastCurrentAmbientPreparationDuration);
        }
        finally
        {
            allowPublicationToContinue.Set();
        }

        await coordinator.WaitForIdleAsync();
    }

    [Fact]
    public async Task ReadyNeighborAmbientPreparesProgressivelyWithoutWaitingForAnotherNeighbor()
    {
        using var repository = new TestRepository();
        var current = StageTestImages.CreateDecoded("current.png");
        var next = StageTestImages.CreateDecoded("next.png");
        repository.Add("current", current, protect: true);
        var preparer = new RecordingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });

        SelectAndStart(coordinator, "current", current.Identity);
        await coordinator.WaitForIdleAsync();
        Assert.Equal(1, preparer.CallCount);

        repository.Add("next", next, protect: false);
        repository.AdjacentPaths.Add("next");
        repository.RaiseAdjacentImageAvailable();
        await coordinator.WaitForIdleAsync();

        Assert.True(next.HasAmbientForBlur(18));
        Assert.Equal(2, preparer.CallCount);
        Assert.Equal(1, coordinator.GetMetrics().AdjacentAmbientPreparedCount);
    }

    [Fact]
    public async Task CachedMatchingAmbientAndPhotoInstallAsOneViewportPresentation()
    {
        using var repository = new TestRepository();
        var target = StageTestImages.CreateDecoded("target.png");
        Assert.True(target.TryAttachAmbient(StageTestImages.CreateAmbient()));
        repository.Add("target", target, protect: true);
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            new RecordingPreparer(),
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });
        Assert.True(repository.TryAcquire("target", out var targetLease));
        var viewport = new PhotoViewportControl();
        var publicationCount = 0;
        coordinator.PresentationChanged += (_, _) => publicationCount++;

        using (var initial = coordinator.BeginImageSelection("target", target.Identity))
        {
            Assert.NotNull(initial.Ambient);
            Assert.Equal(0, publicationCount);
            viewport.SetPresentation(targetLease!, ViewTransfer.Fit, "target", initial);
        }

        var state = viewport.CaptureAmbientPresentationState();
        Assert.Equal(target.Identity, state.ImageIdentity);
        Assert.Equal(target.Identity, state.AmbientIdentity);
        Assert.True(state.HasMatchingAmbient);
        Assert.Equal(0, viewport.GetAmbientRenderFrameMetrics().BlackFallbackRenderedFrameCount);

        coordinator.StartCurrentImageWork();
        await coordinator.WaitForIdleAsync();
        Assert.Equal(0, publicationCount);
        viewport.ClearImage();
    }

    [Fact]
    public async Task PreparedNeighborAmbientIsExposedImmediatelyWhenSelectedWithoutRepreparation()
    {
        using var repository = new TestRepository();
        var first = StageTestImages.CreateDecoded("first.png");
        var target = StageTestImages.CreateDecoded("target.png");
        repository.Add("first", first, protect: true);
        repository.Add("target", target, protect: false);
        repository.AdjacentPaths.Add("target");
        var preparer = new RecordingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });

        SelectAndStart(coordinator, "first", first.Identity);
        await coordinator.WaitForIdleAsync();
        Assert.Equal(2, preparer.CallCount);
        Assert.True(target.HasAmbientForBlur(18));

        repository.AdjacentPaths.Clear();
        repository.AdjacentPaths.Add("first");
        SelectAndStart(coordinator, "target", target.Identity);
        using var presentation = coordinator.AcquirePresentation();
        var metrics = coordinator.GetMetrics();

        Assert.Equal(target.Identity, presentation.ImageIdentity);
        Assert.NotNull(presentation.Ambient);
        Assert.Equal(18, presentation.Ambient!.Blur);
        Assert.Equal(2, preparer.CallCount);
        Assert.Equal(1, metrics.CurrentAmbientCacheHitCount);
        Assert.Equal(1, metrics.CurrentAmbientPrepareCount);
        Assert.Equal(1, metrics.AdjacentAmbientPreparedCount);
        Assert.NotNull(metrics.LastPhotoToAmbientPresentationGap);
        Assert.True(metrics.LastCurrentAmbientWasCacheHit);

        await coordinator.WaitForIdleAsync();
        Assert.Equal(2, preparer.CallCount);
    }

    [Fact]
    public async Task AdjacentAmbientUsesCurrentBlurWhenNeighborBecomesAvailable()
    {
        using var repository = new TestRepository();
        var current = StageTestImages.CreateDecoded("current.png");
        var adjacent = StageTestImages.CreateDecoded("next.png");
        repository.Add("current", current, protect: true);
        var preparer = new RecordingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Ambient,
                AmbientBlur = 26,
            });

        SelectAndStart(coordinator, "current", current.Identity);
        await coordinator.WaitForIdleAsync();
        Assert.Equal(1, preparer.CallCount);
        Assert.True(current.HasAmbientForBlur(26));
        Assert.False(adjacent.HasAmbientForBlur(26));
        repository.Add("next", adjacent, protect: false);
        repository.AdjacentPaths.Add("next");
        repository.RaiseAdjacentImageAvailable();
        await coordinator.WaitForIdleAsync();

        Assert.Equal(2, preparer.CallCount);
        Assert.All(preparer.Blurs, blur => Assert.Equal(26, blur));
        Assert.True(current.HasAmbientForBlur(26));
        Assert.True(adjacent.HasAmbientForBlur(26));
    }

    private static void SelectAndStart(
        AmbientStageCoordinator coordinator,
        string path,
        long identity)
    {
        using var initial = coordinator.BeginImageSelection(path, identity);
        coordinator.StartCurrentImageWork();
    }

    private sealed class RecordingPreparer : IAmbientStagePreparer
    {
        private readonly object _sync = new();
        private readonly List<double> _blurs = [];

        public int CallCount
        {
            get
            {
                lock (_sync)
                {
                    return _blurs.Count;
                }
            }
        }

        public double[] Blurs
        {
            get
            {
                lock (_sync)
                {
                    return _blurs.ToArray();
                }
            }
        }

        public PreparedAmbient Prepare(
            DecodedImage image,
            double blur,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _blurs.Add(blur);
            }

            return StageTestImages.CreateAmbient(blur: blur);
        }
    }

    private sealed class BlockingPreparer : IAmbientStagePreparer
    {
        private readonly ManualResetEventSlim _completion = new(false);
        private PreparedAmbient? _prepared;

        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool PreparedWasDisposed
        {
            get
            {
                try
                {
                    _ = _prepared?.Image.Width;
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
            }
        }

        public PreparedAmbient Prepare(
            DecodedImage image,
            double blur,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            _completion.Wait();
            _prepared = StageTestImages.CreateAmbient(blur: blur);
            return _prepared;
        }

        public void Complete() => _completion.Set();
    }

    private sealed class ThrowingPreparer : IAmbientStagePreparer
    {
        public PreparedAmbient Prepare(
            DecodedImage image,
            double blur,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic native preparation failure.");
    }

    private sealed class ObsoleteFirstPreparer : IAmbientStagePreparer
    {
        private readonly ManualResetEventSlim _firstCompletion = new(false);
        private int _callCount;
        private PreparedAmbient? _obsoletePrepared;

        public TaskCompletionSource FirstStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource LatestCompleted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ObsoletePreparedWasDisposed
        {
            get
            {
                try
                {
                    _ = _obsoletePrepared?.Image.Width;
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }
            }
        }

        public PreparedAmbient Prepare(
            DecodedImage image,
            double blur,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                FirstStarted.TrySetResult();
                _firstCompletion.Wait();
                _obsoletePrepared = StageTestImages.CreateAmbient(blur: blur);
                return _obsoletePrepared;
            }

            var prepared = StageTestImages.CreateAmbient(blur: blur);
            LatestCompleted.TrySetResult();
            return prepared;
        }

        public void CompleteFirst() => _firstCompletion.Set();
    }

    private sealed class TestRepository : IAmbientImageRepository, IDisposable
    {
        private readonly ByteBudgetCache<string, DecodedImage> _cache = new(1_000_000, StringComparer.Ordinal);

        public event EventHandler? AdjacentImageAvailable;

        public List<string> AdjacentPaths { get; } = [];

        public void Add(string path, DecodedImage image, bool protect) =>
            Assert.True(_cache.Add(path, image, protect));

        public bool TryAcquire(string path, out SharedResourceLease<DecodedImage>? image) =>
            _cache.TryAcquire(path, out image);

        public bool RefreshRetainedCost(string path, DecodedImage image) =>
            _cache.RefreshCost(path, image);

        public IReadOnlyList<CachedResourceLease<DecodedImage>> AcquireAdjacent()
        {
            List<CachedResourceLease<DecodedImage>> leases = [];
            foreach (var path in AdjacentPaths)
            {
                if (_cache.TryAcquire(path, out var lease))
                {
                    leases.Add(new CachedResourceLease<DecodedImage>(path, lease!));
                }
            }

            return leases;
        }

        public void RaiseAdjacentImageAvailable() =>
            AdjacentImageAvailable?.Invoke(this, EventArgs.Empty);

        public void Dispose() => _cache.Dispose();
    }
}
