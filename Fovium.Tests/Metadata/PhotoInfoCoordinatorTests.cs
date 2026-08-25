using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Metadata;
using Fovium.Rendering;
using Fovium.Viewer;

namespace Fovium.Tests.Metadata;

public sealed class PhotoInfoCoordinatorTests
{
    [Fact]
    public void HiddenPanelNeverStartsMetadataReadsWhilePresentedImagesChange()
    {
        using var source = new TestPresentedImageSource();
        var reader = new ControllableReader();
        using var coordinator = new PhotoInfoCoordinator(source, reader);

        source.Set(CreateImage(1, "A.jpg"));
        source.Set(CreateImage(2, "B.jpg"));
        source.Set(CreateImage(3, "C.jpg"));

        Assert.Equal(0, reader.CallCount);
        Assert.False(coordinator.IsVisible);
        Assert.Null(coordinator.CurrentState);
    }

    [Fact]
    public async Task TogglePublishesBaseImmediatelyAndReopenUsesBoundedCache()
    {
        using var source = new TestPresentedImageSource();
        var sourcePath = Path.Combine(Path.GetTempPath(), "private", "portrait.jpg");
        source.Set(CreateImage(7, sourcePath, new PixelSize(6000, 4000)));
        var reader = new ControllableReader();
        using var coordinator = new PhotoInfoCoordinator(source, reader);

        coordinator.SetVisible(true);

        var initial = Assert.IsType<PhotoInfoState>(coordinator.CurrentState);
        Assert.Equal(sourcePath, initial.Base.SourcePath);
        Assert.Equal("portrait.jpg", Path.GetFileName(initial.Base.SourcePath));
        Assert.Equal(new PixelSize(6000, 4000), initial.Base.OrientedSize);
        Assert.Equal(1, initial.Base.EncodedBytes);
        Assert.True(initial.IsMetadataLoading);
        Assert.Equal(1, reader.CallCount);

        var published = NextStateChange(coordinator);
        reader.Complete(7, PhotoMetadataSummary.Empty with { CameraModel = "CAMERA-7" });
        await published;
        Assert.Equal("CAMERA-7", coordinator.CurrentState!.Metadata.CameraModel);
        Assert.False(coordinator.CurrentState.IsMetadataLoading);

        coordinator.SetVisible(false);
        coordinator.SetVisible(true);

        Assert.Equal(1, reader.CallCount);
        Assert.Equal("CAMERA-7", coordinator.CurrentState!.Metadata.CameraModel);
        Assert.Equal(1, coordinator.Metrics.CacheHits);
    }

    [Fact]
    public async Task LatestPresentedImageWinsAndOldMetadataIsClearedImmediately()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(1, "A.jpg"));
        var reader = new ControllableReader();
        using var coordinator = new PhotoInfoCoordinator(source, reader);
        coordinator.SetVisible(true);
        source.Set(CreateImage(2, "B.jpg"));
        Assert.Equal("B.jpg", coordinator.CurrentState!.Base.SourcePath);
        Assert.False(coordinator.CurrentState.Metadata.HasUsefulMetadata);
        source.Set(CreateImage(3, "C.jpg"));
        Assert.Equal("C.jpg", coordinator.CurrentState!.Base.SourcePath);
        Assert.False(coordinator.CurrentState.Metadata.HasUsefulMetadata);

        var currentPublished = NextStateChange(coordinator);
        reader.Complete(3, PhotoMetadataSummary.Empty with { CameraModel = "CAMERA-C" });
        await currentPublished;
        reader.Complete(1, PhotoMetadataSummary.Empty with { CameraModel = "STALE-A" });
        reader.Complete(2, PhotoMetadataSummary.Empty with { CameraModel = "STALE-B" });
        await YieldUntilAsync(() => coordinator.Metrics.StaleResults == 2);

        Assert.Equal("C.jpg", coordinator.CurrentState!.Base.SourcePath);
        Assert.Equal("CAMERA-C", coordinator.CurrentState.Metadata.CameraModel);
        Assert.Equal(3, reader.CallCount);
        Assert.Equal(2, coordinator.Metrics.StaleResults);
    }

    [Fact]
    public async Task NewSequenceDropsMetadataCache()
    {
        using var source = new TestPresentedImageSource();
        source.Set(CreateImage(1, "A.jpg"));
        var reader = new ControllableReader();
        using var coordinator = new PhotoInfoCoordinator(source, reader);
        coordinator.SetVisible(true);
        var published = NextStateChange(coordinator);
        reader.Complete(1, PhotoMetadataSummary.Empty);
        await published;
        coordinator.SetVisible(false);
        coordinator.SetVisible(true);
        Assert.Equal(1, reader.CallCount);

        coordinator.BeginNewSequence();
        source.Set(CreateImage(1, "A.jpg"));

        Assert.Equal(2, reader.CallCount);
        Assert.True(coordinator.CurrentState!.IsMetadataLoading);
    }

    [Fact]
    public void CacheEvictsLeastRecentlyUsedEntryAndCachesNoMetadataResult()
    {
        var cache = new PhotoMetadataCache(capacity: 2);
        var empty = PhotoMetadataReadResult.FromSummary(PhotoMetadataSummary.Empty);
        cache.Add(1, empty);
        cache.Add(2, empty);
        Assert.True(cache.TryGet(1, out var cached));
        Assert.Equal(PhotoMetadataReadStatus.NoMetadata, cached!.Status);

        cache.Add(3, empty);

        Assert.True(cache.TryGet(1, out _));
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(3, out _));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public async Task MetadataReadUsesRetainedEncodedBytesWithoutTouchingSourcePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fovium-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var sourcePath = Path.Combine(directory, "source.jpg");
        byte[] sentinel = [0x10, 0x20, 0x30, 0x40];
        await File.WriteAllBytesAsync(sourcePath, sentinel);
        try
        {
            using var source = new TestPresentedImageSource();
            source.Set(MetadataTestImages.CreateDecoded(
                MetadataTestImages.CreateJpegWithExif(),
                sourcePath));
            using var coordinator = new PhotoInfoCoordinator(
                source,
                new MetadataExtractorPhotoMetadataReader());
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.StateChanged += (_, _) =>
            {
                if (coordinator.CurrentState is { IsMetadataLoading: false })
                {
                    completed.TrySetResult();
                }
            };

            coordinator.SetVisible(true);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("TESTMAKE", coordinator.CurrentState!.Metadata.CameraMake);
            Assert.Equal(sentinel, await File.ReadAllBytesAsync(sourcePath));
            Assert.Equal([sourcePath], Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DecodedImage CreateImage(byte marker, string path, PixelSize? size = null) =>
        MetadataTestImages.CreateDecoded([marker], path, size);

    private static Task NextStateChange(PhotoInfoCoordinator coordinator)
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

    private sealed class ControllableReader : IPhotoMetadataReader
    {
        private readonly Dictionary<byte, TaskCompletionSource<PhotoMetadataReadResult>> _pending = [];

        public int CallCount { get; private set; }

        public Task<PhotoMetadataReadResult> ReadAsync(
            ReadOnlyMemory<byte> encodedSource,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var marker = encodedSource.Span[0];
            var completion = new TaskCompletionSource<PhotoMetadataReadResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(marker, completion);
            return completion.Task;
        }

        public void Complete(byte marker, PhotoMetadataSummary summary)
        {
            var completion = _pending[marker];
            _pending.Remove(marker);
            completion.SetResult(PhotoMetadataReadResult.FromSummary(summary));
        }
    }

    private sealed class TestPresentedImageSource : IPresentedImageSource, IDisposable
    {
        private SharedResource<DecodedImage>? _current;
        private string? _identity;

        public event EventHandler? PresentedImageChanged;

        public void Set(DecodedImage image)
        {
            var previous = _current;
            _current = new SharedResource<DecodedImage>(image);
            _identity = image.Descriptor.SourcePath;
            PresentedImageChanged?.Invoke(this, EventArgs.Empty);
            previous?.ReleaseOwner();
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
            _current?.ReleaseOwner();
            _current = null;
        }
    }
}
