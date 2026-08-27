using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Tests.Metadata;
using Fovium.Viewer;

namespace Fovium.Tests.Histogram;

public sealed class HistogramCoordinatorTests
{
    [Fact]
    public void HiddenOverlayDoesNoWorkAcrossPresentedImageChanges()
    {
        using var source = new TestPresentedImageSource();
        var reader = new ControllableReader();
        using var coordinator = new HistogramCoordinator(source, reader);

        source.Set(CreateImage(1));
        source.Set(CreateImage(2));
        source.Set(CreateImage(3));

        Assert.False(coordinator.IsVisible);
        Assert.Null(coordinator.CurrentState);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task ToggleShowsLoadingImmediatelyAndReopenUsesCache()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(7));
        var reader = new ControllableReader();
        using var coordinator = new HistogramCoordinator(source, reader);

        coordinator.SetVisible(true);

        Assert.True(coordinator.IsVisible);
        Assert.True(coordinator.CurrentState!.IsLoading);
        Assert.Null(coordinator.CurrentState.Data);
        Assert.Equal(1, reader.CallCount);

        var published = NextStateChange(coordinator);
        reader.CompleteSynchronously(7, CreateResult(7));
        await published;
        Assert.False(coordinator.CurrentState!.IsLoading);
        Assert.Equal(1, coordinator.CurrentState.Data!.Red[7]);

        coordinator.SetVisible(false);
        coordinator.SetVisible(true);

        Assert.Equal(1, reader.CallCount);
        Assert.Equal(1, coordinator.CurrentState!.Data!.Red[7]);
        Assert.Equal(1, coordinator.Metrics.CacheHits);
    }

    [Fact]
    public async Task LatestPresentedImageWinsAndOldHistogramClearsImmediately()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(1));
        var reader = new ControllableReader();
        using var coordinator = new HistogramCoordinator(source, reader);
        coordinator.SetVisible(true);

        Assert.Equal(1, reader.CallCount);
        Assert.Equal("image-1", coordinator.CurrentState!.PresentationIdentity);
        Assert.True(coordinator.CurrentState.IsLoading);
        Assert.Null(coordinator.CurrentState.Data);

        source.Set(CreateImage(2));

        Assert.Equal(2, reader.CallCount);
        Assert.Equal("image-2", coordinator.CurrentState!.PresentationIdentity);
        Assert.True(coordinator.CurrentState!.IsLoading);
        Assert.Null(coordinator.CurrentState.Data);

        source.Set(CreateImage(3));

        Assert.Equal(3, reader.CallCount);
        Assert.Equal("image-3", coordinator.CurrentState!.PresentationIdentity);
        Assert.True(coordinator.CurrentState.IsLoading);
        Assert.Null(coordinator.CurrentState.Data);

        var currentPublished = NextStateChange(coordinator);
        reader.CompleteSynchronously(3, CreateResult(3));
        await currentPublished;

        Assert.False(coordinator.CurrentState!.IsLoading);
        Assert.Equal("image-3", coordinator.CurrentState.PresentationIdentity);
        Assert.Equal(1, coordinator.CurrentState.Data!.Red[3]);

        reader.CompleteSynchronously(1, CreateResult(1));
        Assert.Equal(1, coordinator.Metrics.StaleResults);
        reader.CompleteSynchronously(2, CreateResult(2));
        Assert.Equal(2, coordinator.Metrics.StaleResults);

        Assert.Equal("image-3", coordinator.CurrentState!.PresentationIdentity);
        Assert.Equal(1, coordinator.CurrentState.Data!.Red[3]);
        Assert.Equal(3, reader.CallCount);
    }

    [Fact]
    public async Task BlinkLikeSwapUsesComparisonIdentityAndRestoresCachedCanonicalResult()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(2));
        var reader = new ControllableReader();
        using var coordinator = new HistogramCoordinator(source, reader);
        coordinator.SetVisible(true);
        var canonicalPublished = NextStateChange(coordinator);
        reader.CompleteSynchronously(2, CreateResult(2));
        await canonicalPublished;

        source.Set(CreateImage(1));
        Assert.Equal("image-1", coordinator.CurrentState!.PresentationIdentity);
        Assert.Null(coordinator.CurrentState.Data);
        var comparisonPublished = NextStateChange(coordinator);
        reader.CompleteSynchronously(1, CreateResult(1));
        await comparisonPublished;

        source.Present("image-2");

        Assert.Equal("image-2", coordinator.CurrentState!.PresentationIdentity);
        Assert.Equal(1, coordinator.CurrentState.Data!.Red[2]);
        Assert.Equal(2, reader.CallCount);
        Assert.Equal(1, coordinator.Metrics.CacheHits);
    }

    [Fact]
    public async Task PeekLikeSamePresentationDoesNotRaiseSourceEventOrReadAgain()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(4));
        var reader = new ControllableReader();
        using var coordinator = new HistogramCoordinator(source, reader);
        coordinator.SetVisible(true);
        var published = NextStateChange(coordinator);
        reader.CompleteSynchronously(4, CreateResult(4));
        await published;

        source.SimulateViewportOnlyChange();

        Assert.Equal(1, reader.CallCount);
        Assert.Equal(1, coordinator.CurrentState!.Data!.Red[4]);
    }

    [Fact]
    public async Task HidingCancelsAnUnnecessaryActiveCalculation()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(8));
        var reader = new CancellationReader();
        using var coordinator = new HistogramCoordinator(source, reader);
        coordinator.SetVisible(true);

        coordinator.SetVisible(false);
        await reader.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await YieldUntilAsync(() => coordinator.Metrics.Canceled == 1);

        Assert.False(coordinator.IsVisible);
        Assert.Null(coordinator.CurrentState);
    }

    [Fact]
    public async Task NewSequenceDropsCachedHistograms()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(5));
        var reader = new ControllableReader();
        using var coordinator = new HistogramCoordinator(source, reader);
        coordinator.SetVisible(true);
        var published = NextStateChange(coordinator);
        reader.CompleteSynchronously(5, CreateResult(5));
        await published;
        coordinator.SetVisible(false);
        coordinator.SetVisible(true);
        Assert.Equal(1, reader.CallCount);

        coordinator.BeginNewSequence();
        source.Set(CreateImage(5));

        Assert.Equal(2, reader.CallCount);
        Assert.True(coordinator.CurrentState!.IsLoading);
    }

    [Fact]
    public async Task RecoverableReaderFailurePublishesQuietNoDataState()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(9));
        var reader = new ControllableReader();
        using var coordinator = new HistogramCoordinator(source, reader);
        coordinator.SetVisible(true);
        var published = NextStateChange(coordinator);

        reader.CompleteSynchronously(9, HistogramReadResult.Failed);
        await published;

        Assert.True(coordinator.IsVisible);
        Assert.False(coordinator.CurrentState!.IsLoading);
        Assert.Null(coordinator.CurrentState.Data);
        Assert.Equal(1, coordinator.Metrics.Failures);
    }

    private static HistogramReadResult CreateResult(byte marker)
    {
        var red = new long[256];
        red[marker] = 1;
        var green = new long[256];
        var blue = new long[256];
        return HistogramReadResult.Success(
            new HistogramData(red, green, blue, 1, 1, 1, false));
    }

    private static DecodedImage CreateImage(byte marker) =>
        MetadataTestImages.CreateDecoded([marker], $"image-{marker}");

    private static Task NextStateChange(HistogramCoordinator coordinator)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            coordinator.StateChanged -= handler;
            completion.TrySetResult();
        };
        coordinator.StateChanged += handler;
        return completion.Task;
    }

    private static async Task YieldUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 1000 && !condition(); attempt++)
        {
            await Task.Yield();
        }

        Assert.True(condition());
    }

    private sealed class ControllableReader : IImageHistogramReader
    {
        private readonly Dictionary<byte, TaskCompletionSource<HistogramReadResult>> _pending = [];

        public int CallCount { get; private set; }

        public Task<HistogramReadResult> ReadAsync(DecodedImage image, CancellationToken cancellationToken)
        {
            CallCount++;
            var marker = image.EncodedSource[0];
            var completion = new TaskCompletionSource<HistogramReadResult>();
            _pending.Add(marker, completion);
            return completion.Task;
        }

        public void CompleteSynchronously(byte marker, HistogramReadResult result)
        {
            var completion = _pending[marker];
            _pending.Remove(marker);

            // Test-owned inline completion makes this return only after the
            // coordinator has processed the controlled reader result.
            completion.SetResult(result);
        }
    }

    private sealed class CancellationReader : IImageHistogramReader
    {
        public TaskCompletionSource Canceled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<HistogramReadResult> ReadAsync(
            DecodedImage image,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return HistogramReadResult.Failed;
            }
            catch (OperationCanceledException)
            {
                Canceled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class TestPresentedImageSource : IPresentedImageSource, IDisposable
    {
        private readonly Dictionary<string, SharedResource<DecodedImage>> _images = [];
        private SharedResource<DecodedImage>? _current;
        private string? _identity;

        public event EventHandler? PresentedImageChanged;

        public void Set(DecodedImage image)
        {
            var identity = image.Descriptor.SourcePath;
            var resource = new SharedResource<DecodedImage>(image);
            if (_images.Remove(identity, out var previous))
            {
                previous.ReleaseOwner();
            }

            _images.Add(identity, resource);
            _current = resource;
            _identity = identity;
            PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Present(string identity)
        {
            _current = _images[identity];
            _identity = identity;
            PresentedImageChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SimulateViewportOnlyChange()
        {
        }

        public bool TryAcquirePresentedImage(out PresentedImageLease? image)
        {
            if (_current is null || _identity is null)
            {
                image = null;
                return false;
            }

            image = new PresentedImageLease(_current.Acquire(), _identity);
            return true;
        }

        public void Dispose()
        {
            foreach (var image in _images.Values)
            {
                image.ReleaseOwner();
            }
        }
    }
}
