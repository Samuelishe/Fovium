using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorManagement;

public sealed class ManagedPhotoPresentationCoordinatorTests
{
    [Fact]
    public async Task ActiveWorkBecomesStaleAndOnlyLatestPendingPresentationPublishes()
    {
        using var image = CreateImage();
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        using var first = CreateRequest(image, 1);
        coordinator.Request(first with { Source = first.Source.Acquire() });
        await renderer.WaitUntilStartedAsync(1);

        using var second = CreateRequest(image, 2);
        using var third = CreateRequest(image, 3);
        coordinator.Request(second with { Source = second.Source.Acquire() });
        coordinator.Request(third with { Source = third.Source.Acquire() });
        renderer.Complete(1);
        await renderer.WaitUntilStartedAsync(3);
        renderer.Complete(3);
        await renderer.WaitUntilCompletedAsync(3);

        Assert.False(coordinator.TryAcquire(first.Key, out _));
        Assert.False(coordinator.TryAcquire(second.Key, out _));
        Assert.True(coordinator.TryAcquire(third.Key, out var current));
        using (current)
        {
            Assert.Equal(3, current!.Value.Key.ImageIdentity);
        }

        var metrics = coordinator.Metrics;
        Assert.Equal(3, metrics.Requests);
        Assert.Equal(1, metrics.CoalescedRequests);
        Assert.Equal(1, metrics.Completed);
        Assert.Equal(1, metrics.StaleResults);
        Assert.Equal([1L, 3L], renderer.Started);
    }

    [Fact]
    public async Task DisposingDuringSynchronousTransformPreventsLatePublicationAndReleasesRenderer()
    {
        using var image = CreateImage();
        var renderer = new ControllableRenderer();
        var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        using var request = CreateRequest(image, 9);
        coordinator.Request(request with { Source = request.Source.Acquire() });
        await renderer.WaitUntilStartedAsync(9);

        coordinator.Dispose();
        renderer.Complete(9);
        await renderer.WaitUntilDisposedAsync();

        Assert.False(coordinator.TryAcquire(request.Key, out _));
        Assert.Equal(0, coordinator.Metrics.Completed);
        Assert.Equal(1, coordinator.Metrics.StaleResults);
    }

    [Theory]
    [InlineData(255, 200, 100, 50, 200, 100, 50)]
    [InlineData(128, 200, 100, 50, 100, 50, 25)]
    [InlineData(1, 255, 128, 1, 1, 1, 0)]
    [InlineData(0, 255, 255, 255, 0, 0, 0)]
    public void PremultiplicationPreservesAlphaExactlyOnceAndCanonicalizesTransparentPixels(
        byte alpha,
        byte blue,
        byte green,
        byte red,
        byte expectedBlue,
        byte expectedGreen,
        byte expectedRed)
    {
        Span<byte> output = stackalloc byte[4];

        SkiaLittleCmsPhotoRenderer.Premultiply([blue, green, red, alpha], output);

        Assert.Equal([expectedBlue, expectedGreen, expectedRed, alpha], output.ToArray());
    }

    private static ManagedPhotoRenderRequest CreateRequest(DecodedImage image, long identity)
    {
        var geometry = new ManagedPhotoGeometry(
            new RectD(0, 0, 1, 1),
            new RectD(0, 0, 1, 1),
            1,
            true);
        return new ManagedPhotoRenderRequest(
            new ManagedPhotoKey(
                identity,
                new DisplayProfileIdentity("ABCDEF", false),
                new PixelSize(1, 1),
                ExifOrientation.Normal,
                geometry),
            image.Descriptor,
            image.AcquireRenderLease(),
            DisplayIccProfileAdmissionTests.CreateProfileHeader());
    }

    private static DecodedImage CreateImage()
    {
        var info = new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        bitmap.GetPixelSpan().Fill(255);
        var image = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(1, 1);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.AssumedSrgb,
                false,
                "Bgra8888/Premul",
                bitmap.ByteCount,
                bitmap.ByteCount,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            image);
    }

    private sealed class ControllableRenderer : IManagedPhotoRenderer
    {
        private readonly object _sync = new();
        private readonly Dictionary<long, TaskCompletionSource> _started = [];
        private readonly Dictionary<long, TaskCompletionSource> _completion = [];
        private readonly Dictionary<long, TaskCompletionSource> _completed = [];
        private readonly TaskCompletionSource _disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<long> Started { get; } = [];

        public ManagedPhotoSurface Render(ManagedPhotoRenderRequest request)
        {
            var identity = request.Key.ImageIdentity;
            Task completion;
            lock (_sync)
            {
                Started.Add(identity);
                Get(_started, identity).TrySetResult();
                completion = Get(_completion, identity).Task;
            }

            completion.GetAwaiter().GetResult();
            var bitmap = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul));
            var image = SKImage.FromBitmap(bitmap);
            var result = new ManagedPhotoSurface(
                request.Key,
                request.Key.Geometry.VisiblePhotoBounds,
                bitmap,
                image,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);
            lock (_sync)
            {
                Get(_completed, identity).TrySetResult();
            }

            return result;
        }

        public Task WaitUntilStartedAsync(long identity) =>
            GetSynchronized(_started, identity).Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitUntilCompletedAsync(long identity) =>
            GetSynchronized(_completed, identity).Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitUntilDisposedAsync() => _disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Complete(long identity) => GetSynchronized(_completion, identity).TrySetResult();

        public void Dispose()
        {
            lock (_sync)
            {
                foreach (var completion in _completion.Values)
                {
                    completion.TrySetResult();
                }
            }

            _disposed.TrySetResult();
        }

        private TaskCompletionSource GetSynchronized(
            Dictionary<long, TaskCompletionSource> values,
            long identity)
        {
            lock (_sync)
            {
                return Get(values, identity);
            }
        }

        private static TaskCompletionSource Get(
            Dictionary<long, TaskCompletionSource> values,
            long identity)
        {
            if (!values.TryGetValue(identity, out var completion))
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                values.Add(identity, completion);
            }

            return completion;
        }
    }
}
