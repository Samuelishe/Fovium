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
        await renderer.WaitUntilStartedAsync(first.Key);

        using var second = CreateRequest(image, 2);
        using var third = CreateRequest(image, 3);
        coordinator.Request(second with { Source = second.Source.Acquire() });
        coordinator.Request(third with { Source = third.Source.Acquire() });
        renderer.Complete(first.Key);
        await renderer.WaitUntilStartedAsync(third.Key);
        renderer.Complete(third.Key);
        await renderer.WaitUntilCompletedAsync(third.Key);

        Assert.False(coordinator.TryAcquire(first.Key, out _));
        Assert.False(coordinator.TryAcquire(second.Key, out _));
        Assert.True(coordinator.TryAcquire(third.Key, out var current));
        using (current)
        {
            Assert.Equal(3, current!.Source.Key.ImageIdentity);
        }

        var metrics = coordinator.Metrics;
        Assert.Equal(3, metrics.Requests);
        Assert.Equal(1, metrics.CoalescedRequests);
        Assert.Equal(1, metrics.Completed);
        Assert.Equal(1, metrics.StaleResults);
        Assert.Equal([1L, 3L], renderer.Started.Select(key => key.ImageIdentity));
    }

    [Fact]
    public async Task DestinationChangeMakesActiveTransformStaleAndPublishesOnlyLatestProfile()
    {
        using var image = CreateImage();
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        using var first = CreateRequest(image, image.Identity, "D1");
        using var second = CreateRequest(image, image.Identity, "D2");
        coordinator.Request(first with { Source = first.Source.Acquire() });
        await renderer.WaitUntilStartedAsync(first.Key);

        coordinator.Request(second with { Source = second.Source.Acquire() });
        renderer.Complete(first.Key);
        await renderer.WaitUntilStartedAsync(second.Key);
        renderer.Complete(second.Key);
        await renderer.WaitUntilCompletedAsync(second.Key);

        Assert.False(coordinator.TryAcquire(first.Key, out _));
        Assert.True(coordinator.TryAcquire(second.Key, out var current));
        current?.Dispose();
        Assert.Equal(1, coordinator.Metrics.StaleResults);
        Assert.Equal(1, coordinator.Metrics.DestinationChanges);
    }

    [Fact]
    public async Task DisposingDuringSynchronousTransformPreventsLatePublicationAndReleasesRenderer()
    {
        using var image = CreateImage();
        var renderer = new ControllableRenderer();
        var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        using var request = CreateRequest(image, 9);
        coordinator.Request(request with { Source = request.Source.Acquire() });
        await renderer.WaitUntilStartedAsync(request.Key);

        coordinator.Dispose();
        renderer.Complete(request.Key);
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

    private static ManagedPhotoRenderRequest CreateRequest(
        DecodedImage image,
        long identity,
        string destinationIdentity = "ABCDEF")
    {
        return new ManagedPhotoRenderRequest(
            new ManagedPhotoKey(
                identity,
                new DisplayProfileIdentity(destinationIdentity, false),
                new PixelSize(1, 1),
                ExifOrientation.Normal),
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
        private readonly Dictionary<ManagedPhotoKey, TaskCompletionSource> _started = [];
        private readonly Dictionary<ManagedPhotoKey, TaskCompletionSource> _completion = [];
        private readonly Dictionary<ManagedPhotoKey, TaskCompletionSource> _completed = [];
        private readonly TaskCompletionSource _disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ManagedPhotoKey> Started { get; } = [];

        public ManagedPhotoSource Render(ManagedPhotoRenderRequest request)
        {
            var key = request.Key;
            Task completion;
            lock (_sync)
            {
                Started.Add(key);
                Get(_started, key).TrySetResult();
                completion = Get(_completion, key).Task;
            }

            completion.GetAwaiter().GetResult();
            var bitmap = new SKBitmap(new SKImageInfo(
                request.Key.EncodedSize.Width,
                request.Key.EncodedSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul));
            var image = SKImage.FromBitmap(bitmap);
            var result = new ManagedPhotoSource(
                request.Key,
                bitmap,
                image,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);
            lock (_sync)
            {
                Get(_completed, key).TrySetResult();
            }

            return result;
        }

        public Task WaitUntilStartedAsync(ManagedPhotoKey key) =>
            GetSynchronized(_started, key).Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitUntilCompletedAsync(ManagedPhotoKey key) =>
            GetSynchronized(_completed, key).Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitUntilDisposedAsync() => _disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Complete(ManagedPhotoKey key) => GetSynchronized(_completion, key).TrySetResult();

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
            Dictionary<ManagedPhotoKey, TaskCompletionSource> values,
            ManagedPhotoKey key)
        {
            lock (_sync)
            {
                return Get(values, key);
            }
        }

        private static TaskCompletionSource Get(
            Dictionary<ManagedPhotoKey, TaskCompletionSource> values,
            ManagedPhotoKey key)
        {
            if (!values.TryGetValue(key, out var completion))
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                values.Add(key, completion);
            }

            return completion;
        }
    }
}
