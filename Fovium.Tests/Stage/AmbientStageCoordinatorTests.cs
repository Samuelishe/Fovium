using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class AmbientStageCoordinatorTests
{
    [Theory]
    [InlineData((int)StageMode.Black)]
    [InlineData((int)StageMode.Neutral)]
    public async Task SolidModesDoNotScheduleAmbientPreparation(int modeValue)
    {
        var mode = (StageMode)modeValue;
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new RecordingPreparer();
        await using var coordinator = new AmbientStageCoordinator(repository, preparer, mode);

        coordinator.SelectImage("photo", image.Identity);
        await coordinator.WaitForIdleAsync();

        Assert.Equal(0, preparer.CallCount);
        Assert.False(image.HasAmbient);
        Assert.Equal(0, coordinator.GetMetrics().ScheduledWorkCount);
        Assert.Equal(0, coordinator.GetMetrics().PreparedCount);
    }

    [Fact]
    public async Task AmbientAndAmbientMatteReuseOnePreparedAsset()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new RecordingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageMode.Ambient);
        coordinator.SelectImage("photo", image.Identity);
        await coordinator.WaitForIdleAsync();

        using var ambient = coordinator.AcquirePresentation();
        coordinator.SetMode(StageMode.AmbientMatte);
        using var matte = coordinator.AcquirePresentation();

        Assert.Equal(1, preparer.CallCount);
        Assert.NotNull(ambient.Ambient);
        Assert.NotNull(matte.Ambient);
        Assert.Same(ambient.Ambient!.Image, matte.Ambient!.Image);
        Assert.Equal(StageMode.AmbientMatte, matte.Mode);
    }

    [Fact]
    public async Task StalePreparationCannotPublishAndIsDisposed()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        var preparer = new BlockingPreparer();
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            preparer,
            StageMode.Ambient);
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
    public async Task PreparationFailureKeepsAmbientModeOnBlackFallback()
    {
        using var repository = new TestRepository();
        var image = StageTestImages.CreateDecoded();
        repository.Add("photo", image, protect: true);
        await using var coordinator = new AmbientStageCoordinator(
            repository,
            new ThrowingPreparer(),
            StageMode.Ambient);

        coordinator.SelectImage("photo", image.Identity);
        await coordinator.WaitForIdleAsync();
        using var presentation = coordinator.AcquirePresentation();

        Assert.Equal(StageMode.Ambient, presentation.Mode);
        Assert.Null(presentation.Ambient);
        Assert.False(image.HasAmbient);
        Assert.Equal(1, coordinator.GetMetrics().PreparationFailureCount);
        Assert.NotNull(coordinator.LastDiagnostic);
    }

    [Fact]
    public async Task AdjacentAmbientIsPreparedOnlyAfterPhotoPreloadCompletes()
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
            StageMode.Ambient);

        coordinator.SelectImage("current", current.Identity);
        Assert.Equal(0, preparer.CallCount);
        repository.CompletePreload();
        await coordinator.WaitForIdleAsync();

        Assert.Equal(2, preparer.CallCount);
        Assert.True(current.HasAmbient);
        Assert.True(adjacent.HasAmbient);
    }

    private sealed class RecordingPreparer : IAmbientStagePreparer
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public PreparedAmbient Prepare(DecodedImage image, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            return StageTestImages.CreateAmbient();
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

        public PreparedAmbient Prepare(DecodedImage image, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            _completion.Wait();
            _prepared = StageTestImages.CreateAmbient();
            return _prepared;
        }

        public void Complete() => _completion.Set();
    }

    private sealed class ThrowingPreparer : IAmbientStagePreparer
    {
        public PreparedAmbient Prepare(DecodedImage image, CancellationToken cancellationToken) =>
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
