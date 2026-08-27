using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorManagement;

public sealed class ManagedPhotoPresentationContinuityTests
{
    private static readonly DisplayProfileIdentity Destination = new("destination", false);

    [Fact]
    public async Task SameSourceAndDestinationGeometryChurnKeepsProxyVisibleUntilLatestExactPublishes()
    {
        using var image = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        var scheduler = new ManualRefinementScheduler();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer, scheduler);
        var g0 = CreateKey(image, Destination, new RectD(2, 2, 6, 6));
        var g1 = CreateKey(image, Destination, new RectD(1, 1, 8, 8));
        var g2 = CreateKey(image, Destination, new RectD(0, 0, 10, 10));
        var g3 = CreateKey(image, Destination, new RectD(2, 2, 5, 5));

        coordinator.Request(CreateRequest(image, g0));
        await renderer.WaitUntilStartedAsync(g0);
        var g0Published = NextPresentationChange(coordinator);
        renderer.Complete(g0);
        await g0Published.WaitAsync(TimeSpan.FromSeconds(5));
        AssertPresentation(
            coordinator,
            g0,
            g0,
            ManagedPhotoPresentationQuality.Exact,
            ManagedPhotoPendingReason.None,
            expectedUnderResolution: false);

        AssertPresentation(
            coordinator,
            g1,
            g0,
            ManagedPhotoPresentationQuality.Proxy,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            expectedUnderResolution: true);
        coordinator.Request(
            CreateRequest(image, g1),
            deferGeometryRefinement: true,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            qualityRefinement: true);

        AssertPresentation(
            coordinator,
            g2,
            g0,
            ManagedPhotoPresentationQuality.Proxy,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            expectedUnderResolution: true);
        coordinator.Request(
            CreateRequest(image, g2),
            deferGeometryRefinement: true,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            qualityRefinement: true);
        AssertPresentation(
            coordinator,
            g3,
            g0,
            ManagedPhotoPresentationQuality.Proxy,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            expectedUnderResolution: false);
        coordinator.Request(
            CreateRequest(image, g3),
            deferGeometryRefinement: true,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            qualityRefinement: false);

        Assert.Equal([g0], renderer.Started);
        Assert.Equal(3, scheduler.ScheduleCount);
        Assert.Equal(1, coordinator.Metrics.Pending);
        Assert.Equal(0, coordinator.Metrics.Active);
        scheduler.Fire();
        await renderer.WaitUntilStartedAsync(g3);
        Assert.False(coordinator.TryAcquire(g1, out _));
        Assert.False(coordinator.TryAcquire(g2, out _));
        AssertPresentation(
            coordinator,
            g3,
            g0,
            ManagedPhotoPresentationQuality.Proxy,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            expectedUnderResolution: false);

        var g3Published = NextPresentationChange(coordinator);
        renderer.Complete(g3);
        await g3Published.WaitAsync(TimeSpan.FromSeconds(5));
        AssertPresentation(
            coordinator,
            g3,
            g3,
            ManagedPhotoPresentationQuality.Exact,
            ManagedPhotoPendingReason.None,
            expectedUnderResolution: false);

        var metrics = coordinator.Metrics;
        Assert.Equal(4, metrics.Requests);
        Assert.Equal(2, metrics.CoalescedRequests);
        Assert.Equal(2, metrics.Completed);
        Assert.Equal(0, metrics.StaleResults);
        Assert.Equal(2, metrics.QualityRefinementRequests);
        Assert.Equal([g0, g3], renderer.Started);
    }

    [Fact]
    public async Task SourceOrDestinationChangeCannotUsePreviousManagedSurfaceAsProxy()
    {
        using var firstImage = CreateImage(20, 20);
        using var secondImage = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var geometry = CreateGeometry(new RectD(0, 0, 10, 10));
        var current = CreateKey(firstImage, Destination, geometry.PhotoDestination);

        coordinator.Request(CreateRequest(firstImage, current));
        await renderer.WaitUntilStartedAsync(current);
        var published = NextPresentationChange(coordinator);
        renderer.Complete(current);
        await published.WaitAsync(TimeSpan.FromSeconds(5));

        var differentSource = CreateKey(secondImage, Destination, geometry.PhotoDestination);
        var differentDestination = CreateKey(
            firstImage,
            new DisplayProfileIdentity("other-destination", false),
            geometry.PhotoDestination);

        AssertUnavailable(
            coordinator,
            differentSource,
            ManagedPhotoPendingReason.SourceChanged);
        AssertUnavailable(
            coordinator,
            differentDestination,
            ManagedPhotoPendingReason.DestinationChanged);
        AssertPresentation(
            coordinator,
            current,
            current,
            ManagedPhotoPresentationQuality.Exact,
            ManagedPhotoPendingReason.None,
            expectedUnderResolution: false);
    }

    [Fact]
    public async Task ProxyReuseRequiresCoverageOfTheLatestVisibleSourceRegion()
    {
        using var image = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var current = CreateKey(image, Destination, new RectD(-5, 0, 20, 10));
        var covered = CreateKey(image, Destination, new RectD(-6, 0, 24, 12));
        var partiallyCovered = CreateKey(image, Destination, new RectD(-2, 0, 20, 10));
        var disjoint = CreateKey(image, Destination, new RectD(-100, 0, 101, 10));

        coordinator.Request(CreateRequest(image, current));
        await renderer.WaitUntilStartedAsync(current);
        var published = NextPresentationChange(coordinator);
        renderer.Complete(current);
        await published.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(coordinator.TryAcquirePresentation(covered, out var proxy, out var coveredReason));
        using (proxy)
        {
            Assert.NotNull(proxy);
            Assert.Equal(ManagedPhotoPresentationQuality.Proxy, proxy.Quality);
            Assert.Equal(ManagedPhotoPendingReason.GeometryRefinementPending, coveredReason);
            Assert.True(proxy.CoversVisiblePhoto);
            Assert.Equal(
                ManagedPhotoCoveragePlanner.MapSourceToDestination(
                    proxy.Surface.OrientedSourceCoverage,
                    covered.Geometry.PhotoDestination,
                    covered.EncodedSize),
                proxy.TargetDestination);
        }

        Assert.True(coordinator.TryAcquirePresentation(
            partiallyCovered,
            out var partialProxy,
            out var partialReason));
        using (partialProxy)
        {
            Assert.NotNull(partialProxy);
            Assert.Equal(ManagedPhotoPresentationQuality.Proxy, partialProxy.Quality);
            Assert.Equal(ManagedPhotoPendingReason.GeometryRefinementPending, partialReason);
            Assert.False(partialProxy.CoversVisiblePhoto);
        }

        AssertUnavailable(
            coordinator,
            disjoint,
            ManagedPhotoPendingReason.GeometryRefinementPending);
    }

    [Fact]
    public async Task DestinationChangeRejectsOldProxyAndLateOldDestinationCannotPublish()
    {
        using var image = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var baseline = CreateKey(image, Destination, new RectD(2, 2, 6, 6));
        var oldDestinationWork = CreateKey(image, Destination, new RectD(0, 0, 10, 10));
        var newDestination = new DisplayProfileIdentity("new-destination", false);
        var latest = CreateKey(image, newDestination, new RectD(1, 1, 8, 8));

        await PublishAsync(coordinator, renderer, image, baseline);
        var presentationChanges = 0;
        coordinator.PresentationChanged += (_, _) => presentationChanges++;

        coordinator.Request(CreateRequest(image, oldDestinationWork));
        await renderer.WaitUntilStartedAsync(oldDestinationWork);
        coordinator.Request(CreateRequest(image, latest));

        AssertUnavailable(coordinator, latest, ManagedPhotoPendingReason.DestinationChanged);
        renderer.Complete(oldDestinationWork);
        await renderer.WaitUntilStartedAsync(latest);
        Assert.Equal(0, presentationChanges);
        Assert.False(coordinator.TryAcquire(oldDestinationWork, out _));
        AssertUnavailable(coordinator, latest, ManagedPhotoPendingReason.DestinationChanged);

        var latestPublished = NextPresentationChange(coordinator);
        renderer.Complete(latest);
        await latestPublished.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, presentationChanges);
        AssertPresentation(
            coordinator,
            latest,
            latest,
            ManagedPhotoPresentationQuality.Exact,
            ManagedPhotoPendingReason.None,
            expectedUnderResolution: false);
        Assert.Equal(1, coordinator.Metrics.StaleResults);
        Assert.Equal([baseline, oldDestinationWork, latest], renderer.Started);
    }

    [Fact]
    public async Task SourceChangeRejectsOldProxyAndLateOldSourceCannotPublish()
    {
        using var firstImage = CreateImage(20, 20);
        using var latestImage = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var baseline = CreateKey(firstImage, Destination, new RectD(2, 2, 6, 6));
        var oldSourceWork = CreateKey(firstImage, Destination, new RectD(0, 0, 10, 10));
        var latest = CreateKey(latestImage, Destination, new RectD(1, 1, 8, 8));

        await PublishAsync(coordinator, renderer, firstImage, baseline);
        var presentationChanges = 0;
        coordinator.PresentationChanged += (_, _) => presentationChanges++;

        coordinator.Request(CreateRequest(firstImage, oldSourceWork));
        await renderer.WaitUntilStartedAsync(oldSourceWork);
        coordinator.Request(CreateRequest(latestImage, latest));

        AssertUnavailable(coordinator, latest, ManagedPhotoPendingReason.SourceChanged);
        renderer.Complete(oldSourceWork);
        await renderer.WaitUntilStartedAsync(latest);
        Assert.Equal(0, presentationChanges);
        Assert.False(coordinator.TryAcquire(oldSourceWork, out _));
        AssertUnavailable(coordinator, latest, ManagedPhotoPendingReason.SourceChanged);

        var latestPublished = NextPresentationChange(coordinator);
        renderer.Complete(latest);
        await latestPublished.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, presentationChanges);
        AssertPresentation(
            coordinator,
            latest,
            latest,
            ManagedPhotoPresentationQuality.Exact,
            ManagedPhotoPendingReason.None,
            expectedUnderResolution: false);
        Assert.Equal(1, coordinator.Metrics.StaleResults);
        Assert.Equal([baseline, oldSourceWork, latest], renderer.Started);
    }

    [Fact]
    public async Task UnderResolvedProxyRemainsAvailableUntilQualityRefinementPublishesExact()
    {
        using var image = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var lowResolution = CreateKey(image, Destination, new RectD(3, 3, 4, 4));
        var refinement = CreateKey(image, Destination, new RectD(0, 0, 10, 10));

        await PublishAsync(coordinator, renderer, image, lowResolution);
        AssertPresentation(
            coordinator,
            refinement,
            lowResolution,
            ManagedPhotoPresentationQuality.Proxy,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            expectedUnderResolution: true);

        coordinator.Request(
            CreateRequest(image, refinement),
            deferGeometryRefinement: false,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            qualityRefinement: true);
        await renderer.WaitUntilStartedAsync(refinement);

        Assert.Equal(1, coordinator.Metrics.QualityRefinementRequests);
        Assert.Equal(
            ManagedPhotoPendingReason.GeometryRefinementPending,
            coordinator.Metrics.LastPendingReason);
        AssertPresentation(
            coordinator,
            refinement,
            lowResolution,
            ManagedPhotoPresentationQuality.Proxy,
            ManagedPhotoPendingReason.GeometryRefinementPending,
            expectedUnderResolution: true);

        var refinementPublished = NextPresentationChange(coordinator);
        renderer.Complete(refinement);
        await refinementPublished.WaitAsync(TimeSpan.FromSeconds(5));

        AssertPresentation(
            coordinator,
            refinement,
            refinement,
            ManagedPhotoPresentationQuality.Exact,
            ManagedPhotoPendingReason.None,
            expectedUnderResolution: false);
        Assert.Equal(2, coordinator.Metrics.Completed);
    }

    [Fact]
    public async Task FrameMetricsDistinguishExactProxyAndGeometryOnlyBlackFallback()
    {
        using var image = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var exactKey = CreateKey(image, Destination, new RectD(2, 2, 6, 6));
        var proxyKey = CreateKey(image, Destination, new RectD(1, 1, 8, 8));

        await PublishAsync(coordinator, renderer, image, exactKey);
        Assert.True(coordinator.TryAcquirePresentation(exactKey, out var exact, out _));
        using (exact)
        {
            Assert.NotNull(exact);
            coordinator.RecordFrame(exact.Quality);
        }

        Assert.True(coordinator.TryAcquirePresentation(proxyKey, out var proxy, out _));
        using (proxy)
        {
            Assert.NotNull(proxy);
            coordinator.RecordFrame(proxy.Quality);
        }

        var accepted = coordinator.Metrics;
        Assert.Equal(1, accepted.ExactFrames);
        Assert.Equal(1, accepted.ProxyFrames);
        Assert.Equal(0, accepted.GeometryOnlyBlackFallbackFrames);

        coordinator.RecordGeometryOnlyBlackFallback();

        var fallback = coordinator.Metrics;
        Assert.Equal(1, fallback.ExactFrames);
        Assert.Equal(1, fallback.ProxyFrames);
        Assert.Equal(1, fallback.GeometryOnlyBlackFallbackFrames);
    }

    [Fact]
    public async Task ProxyTargetMapsOrientedSourcePointsToTheLatestViewportGeometry()
    {
        using var image = CreateImage(20, 20);
        var renderer = new ControllableRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var current = CreateKey(image, Destination, new RectD(-5, 0, 20, 10));
        var requested = CreateKey(image, Destination, new RectD(-6, 0, 24, 12));

        await PublishAsync(coordinator, renderer, image, current);
        Assert.True(coordinator.TryAcquirePresentation(requested, out var proxy, out _));
        using (proxy)
        {
            Assert.NotNull(proxy);
            var source = new PointD(10, 10);
            var coverage = proxy.Surface.OrientedSourceCoverage;
            var mappedThroughProxy = new PointD(
                proxy.TargetDestination.X +
                    (source.X - coverage.X) / coverage.Width * proxy.TargetDestination.Width,
                proxy.TargetDestination.Y +
                    (source.Y - coverage.Y) / coverage.Height * proxy.TargetDestination.Height);
            var mappedThroughLatestGeometry = new PointD(
                requested.Geometry.PhotoDestination.X +
                    source.X / requested.EncodedSize.Width * requested.Geometry.PhotoDestination.Width,
                requested.Geometry.PhotoDestination.Y +
                    source.Y / requested.EncodedSize.Height * requested.Geometry.PhotoDestination.Height);

            Assert.Equal(mappedThroughLatestGeometry.X, mappedThroughProxy.X, 8);
            Assert.Equal(mappedThroughLatestGeometry.Y, mappedThroughProxy.Y, 8);
        }
    }

    private static void AssertPresentation(
        ManagedPhotoPresentationCoordinator coordinator,
        ManagedPhotoKey requested,
        ManagedPhotoKey expectedSurface,
        ManagedPhotoPresentationQuality expectedQuality,
        ManagedPhotoPendingReason expectedReason,
        bool expectedUnderResolution)
    {
        Assert.True(coordinator.TryAcquirePresentation(requested, out var presentation, out var reason));
        using (presentation)
        {
            Assert.NotNull(presentation);
            Assert.Equal(expectedQuality, presentation.Quality);
            Assert.Equal(expectedSurface, presentation.Surface.Key);
            Assert.Equal(
                ManagedPhotoCoveragePlanner.MapSourceToDestination(
                    presentation.Surface.OrientedSourceCoverage,
                    requested.Geometry.PhotoDestination,
                    requested.EncodedSize),
                presentation.TargetDestination);
            Assert.Equal(expectedReason, reason);
            Assert.True(presentation.CoversVisiblePhoto);
            Assert.Equal(expectedUnderResolution, presentation.UnderResolved);
        }
    }

    private static void AssertUnavailable(
        ManagedPhotoPresentationCoordinator coordinator,
        ManagedPhotoKey requested,
        ManagedPhotoPendingReason expectedReason)
    {
        Assert.False(coordinator.TryAcquirePresentation(requested, out var presentation, out var reason));
        Assert.Null(presentation);
        Assert.Equal(expectedReason, reason);
    }

    private static Task NextPresentationChange(ManagedPhotoPresentationCoordinator coordinator)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            coordinator.PresentationChanged -= handler;
            completion.TrySetResult();
        };
        coordinator.PresentationChanged += handler;
        return completion.Task;
    }

    private static async Task PublishAsync(
        ManagedPhotoPresentationCoordinator coordinator,
        ControllableRenderer renderer,
        DecodedImage image,
        ManagedPhotoKey key)
    {
        coordinator.Request(CreateRequest(image, key));
        await renderer.WaitUntilStartedAsync(key);
        var published = NextPresentationChange(coordinator);
        renderer.Complete(key);
        await published.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static ManagedPhotoGeometry CreateGeometry(RectD photoDestination) => new(
        new RectD(0, 0, 10, 10),
        photoDestination,
        1,
        false);

    private static ManagedPhotoKey CreateKey(
        DecodedImage image,
        DisplayProfileIdentity destination,
        RectD photoDestination) => new(
        image.Identity,
        destination,
        image.Descriptor.EncodedSize,
        image.Descriptor.Orientation,
        CreateGeometry(photoDestination));

    private static ManagedPhotoRenderRequest CreateRequest(DecodedImage image, ManagedPhotoKey key) => new(
        key,
        image.Descriptor,
        image.AcquireRenderLease(),
        DisplayIccProfileAdmissionTests.CreateProfileHeader());

    private static DecodedImage CreateImage(int width, int height)
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
        var bitmap = new SKBitmap(info);
        bitmap.Erase(new SKColor(30, 60, 90, 255));
        var skImage = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(width, height);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.NormalizedSrgb,
                false,
                "Bgra8888/Premul",
                bitmap.ByteCount,
                bitmap.ByteCount,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            skImage);
    }

    private sealed class ControllableRenderer : IManagedPhotoRenderer
    {
        private readonly object _sync = new();
        private readonly Dictionary<ManagedPhotoKey, TaskCompletionSource> _started = [];
        private readonly Dictionary<ManagedPhotoKey, TaskCompletionSource> _completion = [];

        public List<ManagedPhotoKey> Started { get; } = [];

        public ManagedPhotoSurface Render(ManagedPhotoRenderRequest request)
        {
            Task completion;
            lock (_sync)
            {
                Started.Add(request.Key);
                Get(_started, request.Key).TrySetResult();
                completion = Get(_completion, request.Key).Task;
            }

            completion.GetAwaiter().GetResult();
            var coverage = ManagedPhotoCoveragePlanner.Create(
                request.Key.Geometry,
                request.Descriptor.OrientedSize);
            var width = coverage.RasterPixelSize.Width;
            var height = coverage.RasterPixelSize.Height;
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            bitmap.Erase(new SKColor(30, 60, 90, 255));
            var image = SKImage.FromBitmap(bitmap);
            return new ManagedPhotoSurface(
                request.Key,
                coverage,
                bitmap,
                image,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);
        }

        public Task WaitUntilStartedAsync(ManagedPhotoKey key) =>
            GetSynchronized(_started, key).Task.WaitAsync(TimeSpan.FromSeconds(5));

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

    private sealed class ManualRefinementScheduler : IManagedPhotoRefinementScheduler
    {
        private Action? _pending;

        public int ScheduleCount { get; private set; }

        public void Schedule(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            _pending = action;
            ScheduleCount++;
        }

        public void Cancel() => _pending = null;

        public void Fire()
        {
            var action = _pending ?? throw new InvalidOperationException("No refinement is scheduled.");
            _pending = null;
            action();
        }

        public void Dispose() => _pending = null;
    }
}
