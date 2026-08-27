using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorManagement;

public sealed class ManagedPhotoPresentationCoverageTests
{
    private static readonly DisplayProfileIdentity Destination = new("destination", false);

    [Fact]
    public async Task PartialCenterDetailCannotSatisfyZoomedOutFullPhotoPresentation()
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var detailKey = CreateKey(image, Destination, Center100Geometry());
        var fitKey = CreateKey(image, Destination, FitGeometry());

        await PublishDetailAsync(coordinator, renderer, image, detailKey);

        Assert.False(coordinator.TryAcquirePresentation(fitKey, out var presentation, out var reason));
        Assert.Null(presentation);
        Assert.Equal(ManagedPhotoPendingReason.CoverageRefinementPending, reason);
        Assert.Equal(1, coordinator.Metrics.PartialCoverageRejected);
        Assert.Equal(1, coordinator.Metrics.CoverageMisses);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    [Fact]
    public async Task FullSourceBaseCoversG1ThroughG4WithoutBlankFallback()
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var detailKey = CreateKey(image, Destination, Center100Geometry());

        await PublishBaseAndDetailAsync(coordinator, renderer, image, detailKey);

        AssertVisible(coordinator, CreateKey(image, Destination, FitGeometry()), ManagedPhotoSurfaceRole.Base);
        AssertVisible(coordinator, detailKey, ManagedPhotoSurfaceRole.Detail);
        AssertVisible(coordinator, CreateKey(image, Destination, Panned100Geometry()), ManagedPhotoSurfaceRole.Base);
        AssertVisible(coordinator, CreateKey(image, Destination, ResizedFitGeometry()), ManagedPhotoSurfaceRole.Base);

        var metrics = coordinator.Metrics;
        Assert.True(metrics.BaseRasterBytes > 0);
        Assert.True(metrics.DetailRasterBytes > 0);
        Assert.Equal(metrics.BaseRasterBytes + metrics.DetailRasterBytes, metrics.MaximumCombinedRasterBytes);
        Assert.True(
            metrics.MaximumCombinedRasterBytes <=
            ManagedPhotoBaseCoveragePlanner.MaximumBaseRasterBytes +
            ManagedPhotoCoveragePlanner.MaximumOverscanRasterBytes);
        Assert.True(metrics.BaseFrames >= 3);
        Assert.True(metrics.BaseFallbackFrames >= 3);
        Assert.Equal(4, metrics.CoverageHits);
        Assert.Equal(0, metrics.ManagedIncompletePhotoFrames);
    }

    [Fact]
    public async Task RapidZoomOutCoalescesToLatestExactWhileBaseCoversEveryGeometry()
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var center = CreateKey(image, Destination, Center100Geometry());
        var active = CreateKey(image, Destination, ZoomOutGeometry(-2100, -1400, 5400, 3600));
        var generations = new[]
        {
            CreateKey(image, Destination, ZoomOutGeometry(-1800, -1200, 4800, 3200)),
            CreateKey(image, Destination, ZoomOutGeometry(-1500, -1000, 4200, 2800)),
            CreateKey(image, Destination, ZoomOutGeometry(-1200, -800, 3600, 2400)),
            CreateKey(image, Destination, ZoomOutGeometry(-900, -600, 3000, 2000)),
        };

        await PublishBaseAndDetailAsync(coordinator, renderer, image, center);
        coordinator.Request(
            CreateRequest(image, active),
            deferGeometryRefinement: false,
            ManagedPhotoPendingReason.CoverageRefinementPending,
            qualityRefinement: false,
            ensureFullSourceBase: true);
        await renderer.WaitUntilStartedAsync(active, ManagedPhotoSurfaceRole.Detail);

        for (var index = 0; index < generations.Length; index++)
        {
            var generation = generations[index];
            coordinator.Request(
                CreateRequest(image, generation),
                deferGeometryRefinement: false,
                ManagedPhotoPendingReason.CoverageRefinementPending,
                qualityRefinement: false,
                ensureFullSourceBase: true);
            AssertVisible(
                coordinator,
                generation,
                index == 0 ? ManagedPhotoSurfaceRole.Detail : ManagedPhotoSurfaceRole.Base);
        }

        renderer.Complete(active, ManagedPhotoSurfaceRole.Detail);
        var latest = generations[^1];
        await renderer.WaitUntilStartedAsync(latest, ManagedPhotoSurfaceRole.Detail);
        Assert.All(generations[..^1], key =>
            Assert.False(renderer.WasStarted(key, ManagedPhotoSurfaceRole.Detail)));
        AssertVisible(coordinator, latest, ManagedPhotoSurfaceRole.Base);

        var published = NextPresentationChange(coordinator);
        renderer.Complete(latest, ManagedPhotoSurfaceRole.Detail);
        await published.WaitAsync(TimeSpan.FromSeconds(5));

        AssertVisible(
            coordinator,
            latest,
            ManagedPhotoSurfaceRole.Detail,
            ManagedPhotoPresentationQuality.Exact);
        Assert.Equal(1, coordinator.Metrics.StaleResults);
        Assert.True(coordinator.Metrics.CoalescedRequests >= 3);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    [Fact]
    public async Task ExactDetailRemainsAuthoritativeAtSettledPhotographic100()
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var at100 = CreateKey(image, Destination, Center100Geometry());

        await PublishBaseAndDetailAsync(coordinator, renderer, image, at100);

        AssertVisible(
            coordinator,
            at100,
            ManagedPhotoSurfaceRole.Detail,
            ManagedPhotoPresentationQuality.Exact);
        Assert.True(coordinator.Metrics.BaseRasterBytes > 0);
        Assert.True(coordinator.Metrics.DetailRasterBytes > 0);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    [Fact]
    public async Task OpenAtPhotographic100ThenZoomOutUsesBaseUntilLatestExactPublishes()
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var at100 = CreateKey(image, Destination, Center100Geometry());
        var fit = CreateKey(image, Destination, FitGeometry());

        coordinator.Request(
            CreateRequest(image, at100),
            deferGeometryRefinement: false,
            ManagedPhotoPendingReason.NoPresentationYet,
            qualityRefinement: false,
            ensureFullSourceBase: true);
        await renderer.WaitUntilStartedAsync(at100, ManagedPhotoSurfaceRole.Base);
        var basePublished = NextPresentationChange(coordinator);
        renderer.Complete(at100, ManagedPhotoSurfaceRole.Base);
        await basePublished.WaitAsync(TimeSpan.FromSeconds(5));
        await renderer.WaitUntilStartedAsync(at100, ManagedPhotoSurfaceRole.Detail);

        coordinator.Request(
            CreateRequest(image, fit),
            deferGeometryRefinement: false,
            ManagedPhotoPendingReason.CoverageRefinementPending,
            qualityRefinement: false,
            ensureFullSourceBase: true);
        AssertVisible(coordinator, fit, ManagedPhotoSurfaceRole.Base);
        var changes = 0;
        coordinator.PresentationChanged += (_, _) => changes++;

        renderer.Complete(at100, ManagedPhotoSurfaceRole.Detail);
        await renderer.WaitUntilStartedAsync(fit, ManagedPhotoSurfaceRole.Detail);
        Assert.Equal(0, changes);
        AssertVisible(coordinator, fit, ManagedPhotoSurfaceRole.Base);

        var fitPublished = NextPresentationChange(coordinator);
        renderer.Complete(fit, ManagedPhotoSurfaceRole.Detail);
        await fitPublished.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, changes);
        AssertVisible(coordinator, fit, ManagedPhotoSurfaceRole.Detail, ManagedPhotoPresentationQuality.Exact);
        Assert.Equal(1, coordinator.Metrics.StaleResults);
        Assert.True(coordinator.Metrics.CoverageRefinementRequests >= 1);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    [Fact]
    public async Task FitToDeepZoomToFitRetainsFullSourceBase()
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var fit = CreateKey(image, Destination, FitGeometry());
        var deep = CreateKey(image, Destination, Center100Geometry());

        await PublishBaseOnlyAsync(coordinator, renderer, image, fit);
        await PublishDetailAsync(coordinator, renderer, image, deep);

        var presentation = AssertVisible(coordinator, fit, ManagedPhotoSurfaceRole.Base);
        Assert.Equal(ManagedPhotoPresentationQuality.Exact, presentation);
        Assert.True(coordinator.Metrics.BaseRasterBytes > 0);
        Assert.True(coordinator.Metrics.DetailRasterBytes > 0);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    [Fact]
    public async Task PanBeyondDetailCoverageFallsBackToFullSourceBase()
    {
        await AssertTransitionUsesBaseAsync(Panned100Geometry());
    }

    [Fact]
    public async Task ResizeBeyondDetailCoverageFallsBackToFullSourceBase()
    {
        await AssertTransitionUsesBaseAsync(ResizedFitGeometry());
    }

    [Fact]
    public async Task PeekOutsideDetailCoverageUsesBaseAndFitRestoreRemainsCovered()
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var center = CreateKey(image, Destination, Center100Geometry());

        await PublishBaseAndDetailAsync(coordinator, renderer, image, center);

        AssertVisible(
            coordinator,
            CreateKey(image, Destination, PeekRightEdgeGeometry()),
            ManagedPhotoSurfaceRole.Base);
        AssertVisible(
            coordinator,
            CreateKey(image, Destination, FitGeometry()),
            ManagedPhotoSurfaceRole.Base);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    [Fact]
    public async Task SourceChangeRejectsPreviousBaseAndLateDetailCannotRepopulateCandidates()
    {
        using var oldImage = CreateImage();
        using var newImage = CreateImage();
        await AssertIdentityRaceAsync(
            oldImage,
            newImage,
            Destination,
            Destination,
            ManagedPhotoPendingReason.SourceChanged);
    }

    [Fact]
    public async Task DestinationChangeRejectsPreviousBaseAndLateDetailCannotRepopulateCandidates()
    {
        using var image = CreateImage();
        await AssertIdentityRaceAsync(
            image,
            image,
            Destination,
            new DisplayProfileIdentity("new-destination", false),
            ManagedPhotoPendingReason.DestinationChanged);
    }

    private static async Task AssertTransitionUsesBaseAsync(ManagedPhotoGeometry targetGeometry)
    {
        using var image = CreateImage();
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var center = CreateKey(image, Destination, Center100Geometry());

        await PublishBaseAndDetailAsync(coordinator, renderer, image, center);

        AssertVisible(
            coordinator,
            CreateKey(image, Destination, targetGeometry),
            ManagedPhotoSurfaceRole.Base);
        Assert.True(coordinator.Metrics.PartialCoverageRejected >= 1);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    private static async Task AssertIdentityRaceAsync(
        DecodedImage oldImage,
        DecodedImage newImage,
        DisplayProfileIdentity oldDestination,
        DisplayProfileIdentity newDestination,
        ManagedPhotoPendingReason expectedReason)
    {
        var renderer = new ControlledRoleRenderer();
        using var coordinator = new ManagedPhotoPresentationCoordinator(renderer);
        var oldCenter = CreateKey(oldImage, oldDestination, Center100Geometry());
        var oldPan = CreateKey(oldImage, oldDestination, Panned100Geometry());
        var latest = CreateKey(newImage, newDestination, FitGeometry());

        await PublishBaseAndDetailAsync(coordinator, renderer, oldImage, oldCenter);
        coordinator.Request(CreateRequest(oldImage, oldPan));
        await renderer.WaitUntilStartedAsync(oldPan, ManagedPhotoSurfaceRole.Detail);
        coordinator.Request(
            CreateRequest(newImage, latest),
            deferGeometryRefinement: false,
            expectedReason,
            qualityRefinement: false,
            ensureFullSourceBase: true);

        AssertUnavailable(coordinator, latest, expectedReason);
        var changes = 0;
        coordinator.PresentationChanged += (_, _) => changes++;
        renderer.Complete(oldPan, ManagedPhotoSurfaceRole.Detail);
        await renderer.WaitUntilStartedAsync(latest, ManagedPhotoSurfaceRole.Base);
        Assert.Equal(0, changes);
        AssertUnavailable(coordinator, latest, expectedReason);

        var latestPublished = NextPresentationChange(coordinator);
        renderer.Complete(latest, ManagedPhotoSurfaceRole.Base);
        await latestPublished.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, changes);
        AssertVisible(coordinator, latest, ManagedPhotoSurfaceRole.Base, ManagedPhotoPresentationQuality.Exact);
        Assert.Equal(1, coordinator.Metrics.StaleResults);
        Assert.Equal(0, coordinator.Metrics.ManagedIncompletePhotoFrames);
    }

    private static ManagedPhotoPresentationQuality AssertVisible(
        ManagedPhotoPresentationCoordinator coordinator,
        ManagedPhotoKey key,
        ManagedPhotoSurfaceRole expectedRole,
        ManagedPhotoPresentationQuality? expectedQuality = null)
    {
        Assert.True(coordinator.TryAcquirePresentation(key, out var presentation, out var reason));
        using (presentation)
        {
            Assert.NotNull(presentation);
            Assert.Equal(expectedRole, presentation.Surface.Role);
            Assert.True(presentation.CoversVisiblePhoto);
            Assert.True(ManagedPhotoCoveragePlanner.Contains(
                presentation.Surface.OrientedSourceCoverage,
                ManagedPhotoCoveragePlanner.VisibleSourceRect(key.Geometry, key.EncodedSize)));
            Assert.Equal(
                ManagedPhotoCoveragePlanner.MapSourceToDestination(
                    presentation.Surface.OrientedSourceCoverage,
                    key.Geometry.PhotoDestination,
                    key.EncodedSize),
                presentation.TargetDestination);
            if (expectedQuality is { } quality)
            {
                Assert.Equal(quality, presentation.Quality);
            }

            var publication = ManagedPhotoPublicationPolicy.Resolve(true, reason);
            Assert.False(publication.SuppressLegacyPhoto);
            Assert.True(publication.PhotoPresentationVisible);
            Assert.False(publication.GeometryOnlyBlackFallback);
            coordinator.RecordFrame(presentation.Quality, presentation.CoversVisiblePhoto);
            return presentation.Quality;
        }
    }

    private static void AssertUnavailable(
        ManagedPhotoPresentationCoordinator coordinator,
        ManagedPhotoKey key,
        ManagedPhotoPendingReason expectedReason)
    {
        Assert.False(coordinator.TryAcquirePresentation(key, out var presentation, out var reason));
        Assert.Null(presentation);
        Assert.Equal(expectedReason, reason);
    }

    private static async Task PublishDetailAsync(
        ManagedPhotoPresentationCoordinator coordinator,
        ControlledRoleRenderer renderer,
        DecodedImage image,
        ManagedPhotoKey key)
    {
        coordinator.Request(CreateRequest(image, key));
        await renderer.WaitUntilStartedAsync(key, ManagedPhotoSurfaceRole.Detail);
        var published = NextPresentationChange(coordinator);
        renderer.Complete(key, ManagedPhotoSurfaceRole.Detail);
        await published.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task PublishBaseOnlyAsync(
        ManagedPhotoPresentationCoordinator coordinator,
        ControlledRoleRenderer renderer,
        DecodedImage image,
        ManagedPhotoKey key)
    {
        coordinator.Request(
            CreateRequest(image, key),
            deferGeometryRefinement: false,
            ManagedPhotoPendingReason.NoPresentationYet,
            qualityRefinement: false,
            ensureFullSourceBase: true);
        await renderer.WaitUntilStartedAsync(key, ManagedPhotoSurfaceRole.Base);
        var published = NextPresentationChange(coordinator);
        renderer.Complete(key, ManagedPhotoSurfaceRole.Base);
        await published.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task PublishBaseAndDetailAsync(
        ManagedPhotoPresentationCoordinator coordinator,
        ControlledRoleRenderer renderer,
        DecodedImage image,
        ManagedPhotoKey key)
    {
        coordinator.Request(
            CreateRequest(image, key),
            deferGeometryRefinement: false,
            ManagedPhotoPendingReason.NoPresentationYet,
            qualityRefinement: false,
            ensureFullSourceBase: true);
        await renderer.WaitUntilStartedAsync(key, ManagedPhotoSurfaceRole.Base);
        var basePublished = NextPresentationChange(coordinator);
        renderer.Complete(key, ManagedPhotoSurfaceRole.Base);
        await basePublished.WaitAsync(TimeSpan.FromSeconds(5));
        await renderer.WaitUntilStartedAsync(key, ManagedPhotoSurfaceRole.Detail);
        var detailPublished = NextPresentationChange(coordinator);
        renderer.Complete(key, ManagedPhotoSurfaceRole.Detail);
        await detailPublished.WaitAsync(TimeSpan.FromSeconds(5));
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

    private static ManagedPhotoRenderRequest CreateRequest(DecodedImage image, ManagedPhotoKey key) => new(
        key,
        image.Descriptor,
        image.AcquireRenderLease(),
        DisplayIccProfileAdmissionTests.CreateProfileHeader());

    private static ManagedPhotoKey CreateKey(
        DecodedImage image,
        DisplayProfileIdentity destination,
        ManagedPhotoGeometry geometry) => new(
        image.Identity,
        destination,
        image.Descriptor.EncodedSize,
        image.Descriptor.Orientation,
        geometry);

    private static ManagedPhotoGeometry FitGeometry() => new(
        new RectD(0, 0, 1200, 800),
        new RectD(0, 0, 1200, 800),
        1,
        false);

    private static ManagedPhotoGeometry Center100Geometry() => new(
        new RectD(0, 0, 1200, 800),
        new RectD(-2400, -1600, 6000, 4000),
        1,
        true);

    private static ManagedPhotoGeometry Panned100Geometry() => new(
        new RectD(0, 0, 1200, 800),
        new RectD(-3600, -1600, 6000, 4000),
        1,
        true);

    private static ManagedPhotoGeometry ResizedFitGeometry() => new(
        new RectD(0, 0, 1600, 900),
        new RectD(125, 0, 1350, 900),
        1,
        false);

    private static ManagedPhotoGeometry PeekRightEdgeGeometry() => new(
        new RectD(0, 0, 1200, 800),
        new RectD(-4400, -1600, 6000, 4000),
        1,
        true);

    private static ManagedPhotoGeometry ZoomOutGeometry(
        double x,
        double y,
        double width,
        double height) => new(
        new RectD(0, 0, 1200, 800),
        new RectD(x, y, width, height),
        1,
        false);

    private static DecodedImage CreateImage()
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        var bitmap = new SKBitmap(new SKImageInfo(
            1,
            1,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            colorSpace));
        bitmap.Erase(new SKColor(30, 60, 90, 255));
        var skImage = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(6000, 4000);
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
                4,
                4,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            skImage);
    }

    private readonly record struct WorkId(ManagedPhotoKey Key, ManagedPhotoSurfaceRole Role);

    private sealed class ControlledRoleRenderer : IManagedPhotoRenderer
    {
        private readonly object _sync = new();
        private readonly Dictionary<WorkId, TaskCompletionSource> _started = [];
        private readonly Dictionary<WorkId, TaskCompletionSource> _completion = [];

        public ManagedPhotoSurface Render(ManagedPhotoRenderRequest request)
        {
            var id = new WorkId(request.Key, request.Role);
            Task completion;
            lock (_sync)
            {
                Get(_started, id).TrySetResult();
                completion = Get(_completion, id).Task;
            }

            completion.GetAwaiter().GetResult();
            var coverage = request.Role == ManagedPhotoSurfaceRole.Base
                ? ManagedPhotoBaseCoveragePlanner.Create(request.Key.Geometry, request.Descriptor.OrientedSize)
                : ManagedPhotoCoveragePlanner.Create(request.Key.Geometry, request.Descriptor.OrientedSize);
            var bitmap = new SKBitmap(new SKImageInfo(
                coverage.RasterPixelSize.Width,
                coverage.RasterPixelSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul));
            bitmap.Erase(new SKColor(30, 60, 90, 255));
            var image = SKImage.FromBitmap(bitmap);
            return new ManagedPhotoSurface(
                request.Key,
                coverage,
                bitmap,
                image,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                request.Role);
        }

        public Task WaitUntilStartedAsync(ManagedPhotoKey key, ManagedPhotoSurfaceRole role) =>
            GetSynchronized(_started, new WorkId(key, role)).Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Complete(ManagedPhotoKey key, ManagedPhotoSurfaceRole role) =>
            GetSynchronized(_completion, new WorkId(key, role)).TrySetResult();

        public bool WasStarted(ManagedPhotoKey key, ManagedPhotoSurfaceRole role)
        {
            lock (_sync)
            {
                return _started.TryGetValue(new WorkId(key, role), out var started) &&
                    started.Task.IsCompletedSuccessfully;
            }
        }

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
            Dictionary<WorkId, TaskCompletionSource> values,
            WorkId id)
        {
            lock (_sync)
            {
                return Get(values, id);
            }
        }

        private static TaskCompletionSource Get(
            Dictionary<WorkId, TaskCompletionSource> values,
            WorkId id)
        {
            if (!values.TryGetValue(id, out var completion))
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                values.Add(id, completion);
            }

            return completion;
        }
    }
}
