using Avalonia;
using Fovium.ColorManagement;
using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Metadata;
using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Stage;
using Fovium.Tests.Stage;
using Fovium.Viewer;
using SkiaSharp;
using RenderPixelSize = Fovium.Rendering.PixelSize;

namespace Fovium.Tests.ColorManagement;

public sealed class AtomicManagedPhotoTransitionTests
{
    [Theory]
    [InlineData((int)MatteStyle.Solid)]
    [InlineData((int)MatteStyle.Rounded)]
    [InlineData((int)MatteStyle.Soft)]
    [InlineData((int)MatteStyle.Angular)]
    public async Task PortraitToLandscapeRetainsCompletePublishedFrameUntilAtomicCommit(int matteStyle)
    {
        using var portrait = new ImageResource("portrait.png", new RenderPixelSize(120, 200));
        using var landscape = new ImageResource("landscape.png", new RenderPixelSize(300, 120));
        var renderer = new ControllableRenderer();
        var (viewport, overlay) = CreateViewport(renderer);
        var publications = 0;
        viewport.PresentedImageChanged += (_, _) => publications++;

        try
        {
            await PresentAsync(viewport, renderer, portrait, CreateStage(MatteStyle.Solid));
            var before = viewport.CaptureAtomicPresentationState();

            Request(viewport, landscape, CreateStage((MatteStyle)matteStyle));
            await renderer.WaitUntilStartedAsync(landscape.Image.Identity);
            var pending = viewport.CaptureAtomicPresentationState();

            Assert.Equal(portrait.Image.Identity, pending.PresentedNumericIdentity);
            Assert.Equal("portrait.png", pending.PresentedIdentity);
            Assert.Equal(before.PresentedOrientedSize, pending.PresentedOrientedSize);
            Assert.Equal(before.PresentedDestination, pending.PresentedDestination);
            Assert.Equal(before.PresentedStage, pending.PresentedStage);
            Assert.Equal(landscape.Image.Identity, pending.PendingNumericIdentity);
            Assert.Equal("landscape.png", pending.PendingIdentity);
            Assert.Equal("portrait.png", overlay.CurrentImageIdentity);
            Assert.True(viewport.TryAcquirePresentedImage(out var retained));
            using (retained)
            {
                Assert.Equal(portrait.Image.Identity, retained!.Image.Identity);
                Assert.Equal("portrait.png", retained.PresentationIdentity);
            }

            Assert.Equal(1, publications);
            await CompleteAsync(viewport, renderer, landscape.Image.Identity);
            var committed = viewport.CaptureAtomicPresentationState();

            Assert.Equal(landscape.Image.Identity, committed.PresentedNumericIdentity);
            Assert.Equal("landscape.png", committed.PresentedIdentity);
            Assert.Equal(landscape.Image.Descriptor.OrientedSize, committed.PresentedOrientedSize);
            Assert.Equal((MatteStyle)matteStyle, committed.PresentedStage!.MatteStyle);
            Assert.Null(committed.PendingNumericIdentity);
            Assert.True(committed.PhotoPresentationVisible);
            Assert.True(committed.HasManagedSource);
            Assert.Equal("landscape.png", overlay.CurrentImageIdentity);
            Assert.Equal(2, publications);
            Assert.Equal(0, viewport.MonitorColorMetrics!.Value.MatteWithoutPhotoFrames);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task LandscapeToPortraitRetainsMatchingMatteAndAmbientUntilAtomicCommit()
    {
        using var landscape = new ImageResource("landscape.png", new RenderPixelSize(300, 120), ambient: true);
        using var portrait = new ImageResource("portrait.png", new RenderPixelSize(120, 200), ambient: true);
        var renderer = new ControllableRenderer();
        var (viewport, _) = CreateViewport(renderer);

        try
        {
            await PresentAsync(viewport, renderer, landscape, CreateAmbientStage(MatteStyle.Angular));
            var before = viewport.CaptureAtomicPresentationState();

            Request(viewport, portrait, CreateAmbientStage(MatteStyle.Rounded));
            await renderer.WaitUntilStartedAsync(portrait.Image.Identity);
            using (var updatedPending = CreateStagePresentation(
                       portrait,
                       CreateAmbientStage(MatteStyle.Soft)))
            {
                viewport.SetStage(updatedPending);
            }

            var pending = viewport.CaptureAtomicPresentationState();
            Assert.Equal(landscape.Image.Identity, pending.PresentedNumericIdentity);
            Assert.Equal(landscape.Image.Identity, pending.PresentedAmbientIdentity);
            Assert.Equal(before.PresentedStage, pending.PresentedStage);
            Assert.Equal(MatteStyle.Soft, pending.PendingStage!.MatteStyle);

            await CompleteAsync(viewport, renderer, portrait.Image.Identity);
            var committed = viewport.CaptureAtomicPresentationState();
            Assert.Equal(portrait.Image.Identity, committed.PresentedNumericIdentity);
            Assert.Equal(portrait.Image.Identity, committed.PresentedAmbientIdentity);
            Assert.Equal(MatteStyle.Soft, committed.PresentedStage!.MatteStyle);
            Assert.Equal(0, viewport.MonitorColorMetrics!.Value.MatteWithoutPhotoFrames);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task FirstManagedImageKeepsPhotoAndMatteAbsentUntilReady()
    {
        using var image = new ImageResource("first.png", new RenderPixelSize(120, 200));
        var renderer = new ControllableRenderer();
        var (viewport, overlay) = CreateViewport(renderer);
        var publications = 0;
        viewport.PresentedImageChanged += (_, _) => publications++;

        try
        {
            Request(viewport, image, CreateStage(MatteStyle.Soft));
            await renderer.WaitUntilStartedAsync(image.Image.Identity);
            var pending = viewport.CaptureAtomicPresentationState();

            Assert.Null(pending.PresentedNumericIdentity);
            Assert.Null(pending.PresentedStage);
            Assert.False(pending.PhotoPresentationVisible);
            Assert.Equal(image.Image.Identity, pending.PendingNumericIdentity);
            Assert.Null(overlay.CurrentImageIdentity);
            Assert.False(viewport.TryAcquirePresentedImage(out _));
            Assert.Equal(0, publications);

            await CompleteAsync(viewport, renderer, image.Image.Identity);
            var committed = viewport.CaptureAtomicPresentationState();
            Assert.Equal(image.Image.Identity, committed.PresentedNumericIdentity);
            Assert.True(committed.PresentedStage!.MatteEnabled);
            Assert.True(committed.PhotoPresentationVisible);
            Assert.Equal(1, publications);
            Assert.Equal(0, viewport.MonitorColorMetrics!.Value.MatteWithoutPhotoFrames);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task FirstImageWaitsForInitialDisplayProfileDecisionWithoutEmptyMatte()
    {
        using var image = new ImageResource("first.png", new RenderPixelSize(120, 200));
        var renderer = new ControllableRenderer();
        var (viewport, _) = CreateViewport(renderer, resolveDisplayProfile: false);

        try
        {
            Request(viewport, image, CreateStage(MatteStyle.Solid));
            var unresolved = viewport.CaptureAtomicPresentationState();
            Assert.Null(unresolved.PresentedNumericIdentity);
            Assert.Null(unresolved.PresentedStage);
            Assert.Equal(image.Image.Identity, unresolved.PendingNumericIdentity);
            Assert.Empty(renderer.Started);

            viewport.SetDisplayProfile(CreateResolution());
            await renderer.WaitUntilStartedAsync(image.Image.Identity);
            await CompleteAsync(viewport, renderer, image.Image.Identity);

            Assert.Equal(image.Image.Identity, viewport.CaptureAtomicPresentationState().PresentedNumericIdentity);
            Assert.Equal(0, viewport.MonitorColorMetrics!.Value.MatteWithoutPhotoFrames);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public void InitialUnavailableProfileDecisionPublishesFirstLegacyPhotoAndMatteTogether()
    {
        using var image = new ImageResource("first.png", new RenderPixelSize(120, 200));
        var renderer = new ControllableRenderer();
        var (viewport, _) = CreateViewport(renderer, resolveDisplayProfile: false);

        try
        {
            Request(viewport, image, CreateStage(MatteStyle.Solid));
            Assert.Null(viewport.CaptureAtomicPresentationState().PresentedNumericIdentity);

            viewport.SetDisplayProfile(new DisplayProfileResolution(
                MonitorColorState.DestinationUnavailable,
                null,
                "No display profile is assigned."));

            var committed = viewport.CaptureAtomicPresentationState();
            Assert.Equal(image.Image.Identity, committed.PresentedNumericIdentity);
            Assert.True(committed.PresentedStage!.MatteEnabled);
            Assert.True(committed.PhotoPresentationVisible);
            Assert.False(committed.HasManagedSource);
            Assert.Empty(renderer.Started);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task RapidAtoBtoCPublishesOnlyLatestReadyTarget()
    {
        using var first = new ImageResource("A.png", new RenderPixelSize(120, 200));
        using var second = new ImageResource("B.png", new RenderPixelSize(300, 120));
        using var latest = new ImageResource("C.png", new RenderPixelSize(140, 220));
        var renderer = new ControllableRenderer();
        var (viewport, _) = CreateViewport(renderer);
        var published = new List<string?>();
        viewport.PresentedImageChanged += (_, _) => published.Add(viewport.PresentedImageIdentity);

        try
        {
            await PresentAsync(viewport, renderer, first, CreateStage(MatteStyle.Solid));
            Request(viewport, second, CreateStage(MatteStyle.Rounded));
            await renderer.WaitUntilStartedAsync(second.Image.Identity);
            Request(viewport, latest, CreateStage(MatteStyle.Angular));

            renderer.Complete(second.Image.Identity);
            await renderer.WaitUntilStartedAsync(latest.Image.Identity);
            Assert.Equal("A.png", viewport.PresentedImageIdentity);
            Assert.DoesNotContain("B.png", published);

            await CompleteAsync(viewport, renderer, latest.Image.Identity);
            Assert.Equal("C.png", viewport.PresentedImageIdentity);
            Assert.Equal(["A.png", "C.png"], published);
            Assert.Equal(1, viewport.MonitorColorMetrics!.Value.StaleResults);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task ReverseNavigationRetainsCurrentPresentationWithoutRetransform()
    {
        using var first = new ImageResource("A.png", new RenderPixelSize(120, 200));
        using var second = new ImageResource("B.png", new RenderPixelSize(300, 120));
        var renderer = new ControllableRenderer();
        var (viewport, _) = CreateViewport(renderer);
        var publications = 0;
        viewport.PresentedImageChanged += (_, _) => publications++;

        try
        {
            await PresentAsync(viewport, renderer, first, CreateStage(MatteStyle.Solid));
            Request(viewport, second, CreateStage(MatteStyle.Rounded));
            await renderer.WaitUntilStartedAsync(second.Image.Identity);
            Request(viewport, first, CreateStage(MatteStyle.Angular));

            Assert.Equal("A.png", viewport.PresentedImageIdentity);
            Assert.Equal(MatteStyle.Angular, viewport.CaptureAtomicPresentationState().PresentedStage!.MatteStyle);
            Assert.Equal(1, publications);
            Assert.Equal(2, renderer.Started.Count);

            renderer.Complete(second.Image.Identity);
            await viewport.WaitForManagedPhotoIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            viewport.ProcessManagedPresentationAvailability();
            Assert.Equal("A.png", viewport.PresentedImageIdentity);
            Assert.Equal(1, publications);
            Assert.Equal(1, viewport.MonitorColorMetrics!.Value.StaleResults);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task RecoverableManagedFailurePublishesLegacyTargetAtomically()
    {
        using var first = new ImageResource("A.png", new RenderPixelSize(120, 200));
        using var failed = new ImageResource("B.png", new RenderPixelSize(300, 120));
        var renderer = new ControllableRenderer();
        var (viewport, _) = CreateViewport(renderer);

        try
        {
            await PresentAsync(viewport, renderer, first, CreateStage(MatteStyle.Solid));
            renderer.Fail(failed.Image.Identity);
            Request(viewport, failed, CreateStage(MatteStyle.Soft));
            await renderer.WaitUntilStartedAsync(failed.Image.Identity);
            Assert.Equal("A.png", viewport.PresentedImageIdentity);

            renderer.Complete(failed.Image.Identity);
            await viewport.WaitForManagedPhotoIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            viewport.ProcessManagedPresentationFailure();

            var committed = viewport.CaptureAtomicPresentationState();
            Assert.Equal("B.png", committed.PresentedIdentity);
            Assert.Equal(MatteStyle.Soft, committed.PresentedStage!.MatteStyle);
            Assert.True(committed.PhotoPresentationVisible);
            Assert.False(committed.HasManagedSource);
            Assert.Equal(MonitorColorState.InvalidDestinationProfile, viewport.MonitorColorState);
            Assert.Equal(0, viewport.MonitorColorMetrics!.Value.MatteWithoutPhotoFrames);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task PublishedManagedSourceSurvivesGeometryChangesWithoutCmmWork()
    {
        using var image = new ImageResource("A.png", new RenderPixelSize(600, 400));
        var renderer = new ControllableRenderer();
        var (viewport, _) = CreateViewport(renderer);

        try
        {
            await PresentAsync(viewport, renderer, image, CreateStage(MatteStyle.Solid));
            var requests = viewport.MonitorColorMetrics!.Value.Requests;

            for (var index = 0; index < 50; index++)
            {
                viewport.ZoomByStepsAtCenter(index % 2 == 0 ? 1 : -1);
            }

            viewport.Fit();
            viewport.SetPhotographic100AtCenter();
            viewport.Fit();

            Assert.Equal(requests, viewport.MonitorColorMetrics!.Value.Requests);
            Assert.Single(renderer.Started);
            Assert.True(viewport.CaptureAtomicPresentationState().HasManagedSource);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task PresentedConsumersRemainOnVisibleSourceUntilAtomicCommit()
    {
        using var first = new ImageResource("A.png", new RenderPixelSize(120, 200));
        using var second = new ImageResource("B.png", new RenderPixelSize(300, 120));
        var renderer = new ControllableRenderer();
        var (viewport, overlay) = CreateViewport(renderer);
        var histogramReader = new ImmediateHistogramReader();
        var metadataReader = new ImmediateMetadataReader();

        try
        {
            await PresentAsync(viewport, renderer, first, CreateStage(MatteStyle.Solid));
            using var histogram = new HistogramCoordinator(viewport, histogramReader);
            using var photoInfo = new PhotoInfoCoordinator(viewport, metadataReader);
            histogram.SetVisible(true);
            photoInfo.SetVisible(true);

            Assert.Equal("A.png", histogram.CurrentState!.PresentationIdentity);
            Assert.Equal("A.png", photoInfo.CurrentState!.Base.SourcePath);
            Request(viewport, second, CreateStage(MatteStyle.Rounded));
            await renderer.WaitUntilStartedAsync(second.Image.Identity);

            Assert.Equal("A.png", viewport.PresentedImageIdentity);
            Assert.Equal("A.png", overlay.CurrentImageIdentity);
            Assert.Equal("A.png", histogram.CurrentState!.PresentationIdentity);
            Assert.Equal("A.png", photoInfo.CurrentState!.Base.SourcePath);
            Assert.Equal(1, histogramReader.CallCount);
            Assert.Equal(1, metadataReader.CallCount);

            await CompleteAsync(viewport, renderer, second.Image.Identity);
            Assert.Equal("B.png", viewport.PresentedImageIdentity);
            Assert.Equal("B.png", overlay.CurrentImageIdentity);
            Assert.Equal("B.png", histogram.CurrentState!.PresentationIdentity);
            Assert.Equal("B.png", photoInfo.CurrentState!.Base.SourcePath);
            Assert.Equal(2, histogramReader.CallCount);
            Assert.Equal(2, metadataReader.CallCount);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    private static (PhotoViewportControl Viewport, PresentationOverlaySession Overlay) CreateViewport(
        ControllableRenderer renderer,
        bool resolveDisplayProfile = true)
    {
        var viewport = new PhotoViewportControl
        {
            Width = 1920,
            Height = 1080,
        };
        viewport.Measure(new Size(1920, 1080));
        viewport.Arrange(new Rect(0, 0, 1920, 1080));
        var overlay = new PresentationOverlaySession(PresentationSettings.Default);
        viewport.ConfigurePresentation(overlay);
        viewport.ConfigureMonitorColorManagement(
            renderer,
            enabled: true,
            engineAvailable: true,
            platformSupported: true);
        if (resolveDisplayProfile)
        {
            viewport.SetDisplayProfile(CreateResolution());
        }

        return (viewport, overlay);
    }

    private static async Task PresentAsync(
        PhotoViewportControl viewport,
        ControllableRenderer renderer,
        ImageResource image,
        StageSettings stage)
    {
        Request(viewport, image, stage);
        await renderer.WaitUntilStartedAsync(image.Image.Identity);
        await CompleteAsync(viewport, renderer, image.Image.Identity);
    }

    private static void Request(
        PhotoViewportControl viewport,
        ImageResource image,
        StageSettings stage)
    {
        using var presentation = CreateStagePresentation(image, stage);
        viewport.SetPresentation(image.Acquire(), ViewTransfer.Fit, image.Path, presentation);
    }

    private static async Task CompleteAsync(
        PhotoViewportControl viewport,
        ControllableRenderer renderer,
        long identity)
    {
        renderer.Complete(identity);
        await viewport.WaitForManagedPhotoIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        viewport.ProcessManagedPresentationAvailability();
    }

    private static StagePresentation CreateStagePresentation(ImageResource image, StageSettings stage) =>
        new(stage, image.Image.Identity, image.Image.TryAcquireAmbient());

    private static StageSettings CreateStage(MatteStyle style) => StageSettings.Default with
    {
        BackgroundMode = StageBackgroundMode.Black,
        MatteEnabled = true,
        MatteColor = new StageColor(255, 220, 0),
        MatteStyle = style,
        MatteWidthPhysicalPixels = 48,
    };

    private static StageSettings CreateAmbientStage(MatteStyle style) => CreateStage(style) with
    {
        BackgroundMode = StageBackgroundMode.Ambient,
    };

    private static DisplayProfileResolution CreateResolution()
    {
        var bytes = DisplayIccProfileAdmissionTests.CreateProfileHeader();
        return new DisplayProfileResolution(
            MonitorColorState.Managed,
            new DisplayProfile(
                bytes,
                DisplayProfileIdentity.FromBytes(bytes, false),
                "Synthetic",
                false,
                "monitor",
                1),
            "managed",
            false,
            8);
    }

    private static void Shutdown(PhotoViewportControl viewport)
    {
        viewport.ClearImage();
        viewport.ShutdownMonitorColorManagement();
    }

    private sealed class ImageResource : IDisposable
    {
        private readonly SharedResource<DecodedImage> _resource;

        public ImageResource(string path, RenderPixelSize size, bool ambient = false)
        {
            Path = path;
            Image = StageTestImages.CreateDecoded(path, size);
            if (ambient)
            {
                Assert.True(Image.TryAttachAmbient(StageTestImages.CreateAmbient()));
            }

            _resource = new SharedResource<DecodedImage>(Image);
        }

        public string Path { get; }

        public DecodedImage Image { get; }

        public SharedResourceLease<DecodedImage> Acquire() => _resource.Acquire();

        public void Dispose() => _resource.ReleaseOwner();
    }

    private sealed class ControllableRenderer : IManagedPhotoRenderer
    {
        private readonly object _sync = new();
        private readonly Dictionary<long, TaskCompletionSource> _started = [];
        private readonly Dictionary<long, TaskCompletionSource> _completion = [];
        private readonly HashSet<long> _failures = [];

        public List<long> Started { get; } = [];

        public ManagedPhotoSource Render(ManagedPhotoRenderRequest request)
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
            lock (_sync)
            {
                if (_failures.Contains(identity))
                {
                    throw new InvalidDataException("Controlled transform failure.");
                }
            }

            var bitmap = new SKBitmap(new SKImageInfo(
                request.Key.EncodedSize.Width,
                request.Key.EncodedSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul));
            var image = SKImage.FromBitmap(bitmap);
            return new ManagedPhotoSource(
                request.Key,
                bitmap,
                image,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero);
        }

        public Task WaitUntilStartedAsync(long identity) =>
            GetSynchronized(_started, identity).Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Complete(long identity) => GetSynchronized(_completion, identity).TrySetResult();

        public void Fail(long identity)
        {
            lock (_sync)
            {
                _failures.Add(identity);
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

    private sealed class ImmediateHistogramReader : IImageHistogramReader
    {
        public int CallCount { get; private set; }

        public Task<HistogramReadResult> ReadAsync(DecodedImage image, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(HistogramReadResult.Unsupported);
        }
    }

    private sealed class ImmediateMetadataReader : IPhotoMetadataReader
    {
        public int CallCount { get; private set; }

        public Task<PhotoMetadataReadResult> ReadAsync(
            ReadOnlyMemory<byte> encodedSource,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(PhotoMetadataReadResult.FromSummary(PhotoMetadataSummary.Empty));
        }
    }
}
