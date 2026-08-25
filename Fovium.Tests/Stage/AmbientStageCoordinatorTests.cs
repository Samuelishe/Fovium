using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Stage;

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

        coordinator.SelectImage("photo", image.Identity);
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
        coordinator.SelectImage("photo", image.Identity);
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
        coordinator.SelectImage("photo", image.Identity);
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
        coordinator.SelectImage("photo", image.Identity);
        await coordinator.WaitForIdleAsync();

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
    }

    [Fact]
    public async Task OldBlurMayBridgeCurrentImageButNeverPublishesForNewImage()
    {
        using var repository = new TestRepository(delayPreload: true);
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
        coordinator.SelectImage("first", first.Identity);

        coordinator.SetStage(initial with { AmbientBlur = 24 });
        using var transitional = coordinator.AcquirePresentation();
        coordinator.SelectImage("second", second.Identity);
        using var newImage = coordinator.AcquirePresentation();

        Assert.Equal(18, transitional.Ambient?.Blur);
        Assert.Null(newImage.Ambient);
        repository.CompletePreload();
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
        coordinator.SelectImage("photo", image.Identity);
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
    public async Task PreparationFailureKeepsAmbientOnBlackFallback()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            new ThrowingPreparer(),
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient });

        coordinator.SelectImage("photo", image.Identity);
        await coordinator.WaitForIdleAsync();
        using var presentation = coordinator.AcquirePresentation();

        Assert.Equal(StageBackgroundMode.Ambient, presentation.Stage.BackgroundMode);
        Assert.Null(presentation.Ambient);
        Assert.False(image.HasAmbient);
        Assert.Equal(1, coordinator.GetMetrics().PreparationFailureCount);
        Assert.NotNull(coordinator.LastDiagnostic);
    }

    [Fact]
    public async Task AdjacentAmbientUsesCurrentBlurAfterPhotoPreloadCompletes()
    {
        using var repository = new TestRepository(delayPreload: true);
        var current = StageTestImages.CreateDecoded("current.png");
        var adjacent = StageTestImages.CreateDecoded("next.png");
        repository.Add("current", current, protect: true);
        repository.Add("next", adjacent, protect: false);
        repository.AdjacentPaths.Add("next");
        var preparer = new RecordingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Ambient,
                AmbientBlur = 26,
            });

        coordinator.SelectImage("current", current.Identity);
        Assert.Equal(0, preparer.CallCount);
        repository.CompletePreload();
        await coordinator.WaitForIdleAsync();

        Assert.Equal(2, preparer.CallCount);
        Assert.All(preparer.Blurs, blur => Assert.Equal(26, blur));
        Assert.True(current.HasAmbientForBlur(26));
        Assert.True(adjacent.HasAmbientForBlur(26));
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

    private sealed class TestRepository : IAmbientImageRepository, IDisposable
    {
        private readonly ByteBudgetCache<string, DecodedImage> _cache = new(1_000_000, StringComparer.Ordinal);
        private readonly TaskCompletionSource _preload = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TestRepository(bool delayPreload = false)
        {
            if (!delayPreload)
            {
                _preload.TrySetResult();
            }
        }

        public List<string> AdjacentPaths { get; } = [];

        public void Add(string path, DecodedImage image, bool protect) =>
            Assert.True(_cache.Add(path, image, protect));

        public bool TryAcquire(string path, out SharedResourceLease<DecodedImage>? image) =>
            _cache.TryAcquire(path, out image);

        public bool RefreshRetainedCost(string path, DecodedImage image) =>
            _cache.RefreshCost(path, image);

        public Task WaitForAdjacentPreloadAsync(CancellationToken cancellationToken) =>
            _preload.Task.WaitAsync(cancellationToken);

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

        public void CompletePreload() => _preload.TrySetResult();

        public void Dispose() => _cache.Dispose();
    }
}
