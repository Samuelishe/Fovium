using Avalonia;
using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Rendering;
using Fovium.Slideshow;
using Fovium.Stage;
using Fovium.Tests.ColorManagement;
using Fovium.Tests.Stage;
using Fovium.Viewer;
using SkiaSharp;
using RenderPixelSize = Fovium.Rendering.PixelSize;

namespace Fovium.Tests.Slideshow;

public sealed class SlideshowManagedPreparationTests
{
    [Fact]
    public async Task PreparedNextIsConsumedByAtomicPublicationWithoutAnotherConversion()
    {
        using var first = new ImageResource("A.png", new RenderPixelSize(120, 200));
        using var next = new ImageResource("B.png", new RenderPixelSize(300, 120));
        var renderer = new RecordingRenderer();
        var viewport = CreateViewport(renderer, Profile(1));
        var publications = new List<string?>();
        viewport.PresentedImageChanged += (_, _) => publications.Add(viewport.PresentedImageIdentity);

        try
        {
            await PresentAsync(viewport, first);
            using var nextLease = next.Acquire();

            var prepared = await viewport.PrepareSlideshowNextAsync(
                nextLease,
                CancellationToken.None);
            using var stage = CreateStagePresentation(next);
            viewport.SetPresentation(next.Acquire(), ViewTransfer.Fit, next.Path, stage);

            Assert.Equal(SlideshowPreparationStatus.Ready, prepared.Status);
            Assert.Equal(checked(300L * 120 * 4), prepared.RetainedManagedBytes);
            Assert.Equal("B.png", viewport.PresentedImageIdentity);
            Assert.Equal(["A.png", "B.png"], publications);
            Assert.Equal(2, renderer.Keys.Count);
            Assert.Equal(0, viewport.MonitorColorMetrics!.Value.MatteWithoutPhotoFrames);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    [Fact]
    public async Task PreparedNextForOldDestinationCannotPublishAfterDestinationChange()
    {
        using var first = new ImageResource("A.png", new RenderPixelSize(120, 200));
        using var next = new ImageResource("B.png", new RenderPixelSize(300, 120));
        var renderer = new RecordingRenderer();
        var d1 = Profile(1);
        var d2 = Profile(2);
        var viewport = CreateViewport(renderer, d1);

        try
        {
            await PresentAsync(viewport, first);
            using (var nextLease = next.Acquire())
            {
                var prepared = await viewport.PrepareSlideshowNextAsync(
                    nextLease,
                    CancellationToken.None);
                Assert.Equal(SlideshowPreparationStatus.Ready, prepared.Status);
            }

            viewport.SetDisplayProfile(d2);
            await viewport.WaitForManagedPhotoIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            viewport.ProcessManagedPresentationAvailability();
            using (var stage = CreateStagePresentation(next))
            {
                viewport.SetPresentation(next.Acquire(), ViewTransfer.Fit, next.Path, stage);
            }

            Assert.Equal("A.png", viewport.PresentedImageIdentity);
            await viewport.WaitForManagedPhotoIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
            viewport.ProcessManagedPresentationAvailability();

            var d1B = renderer.Keys.Single(key =>
                key.ImageIdentity == next.Image.Identity &&
                key.DestinationIdentity == d1.Profile!.Identity);
            var d2B = renderer.Keys.Single(key =>
                key.ImageIdentity == next.Image.Identity &&
                key.DestinationIdentity == d2.Profile!.Identity);
            Assert.NotEqual(d1B.DestinationIdentity, d2B.DestinationIdentity);
            Assert.Equal("B.png", viewport.PresentedImageIdentity);
            Assert.True(viewport.CaptureAtomicPresentationState().HasManagedSource);
            Assert.Equal(d2.Profile!.Identity, d2B.DestinationIdentity);
        }
        finally
        {
            Shutdown(viewport);
        }
    }

    private static PhotoViewportControl CreateViewport(
        RecordingRenderer renderer,
        DisplayProfileResolution profile)
    {
        var viewport = new PhotoViewportControl { Width = 1920, Height = 1080 };
        viewport.Measure(new Size(1920, 1080));
        viewport.Arrange(new Rect(0, 0, 1920, 1080));
        viewport.ConfigureMonitorColorManagement(
            renderer,
            enabled: true,
            engineAvailable: true,
            platformSupported: true);
        viewport.SetDisplayProfile(profile);
        viewport.SetPhotoPresentationViewEnabled(true);
        return viewport;
    }

    private static async Task PresentAsync(PhotoViewportControl viewport, ImageResource image)
    {
        using var stage = CreateStagePresentation(image);
        viewport.SetPresentation(image.Acquire(), ViewTransfer.Fit, image.Path, stage);
        await viewport.WaitForManagedPhotoIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        viewport.ProcessManagedPresentationAvailability();
    }

    private static StagePresentation CreateStagePresentation(ImageResource image) => new(
        StageSettings.Default with
        {
            MatteEnabled = true,
            MatteStyle = MatteStyle.Solid,
            MatteWidthPhysicalPixels = 32,
        },
        image.Image.Identity,
        null);

    private static DisplayProfileResolution Profile(byte marker)
    {
        var bytes = DisplayIccProfileAdmissionTests.CreateProfileHeader();
        bytes[100] = marker;
        return new DisplayProfileResolution(
            MonitorColorState.Managed,
            new DisplayProfile(
                bytes,
                DisplayProfileIdentity.FromBytes(bytes, false),
                $"D{marker}",
                false,
                $"monitor-{marker}",
                marker),
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

        public ImageResource(string path, RenderPixelSize size)
        {
            Path = path;
            Image = StageTestImages.CreateDecoded(path, size);
            _resource = new SharedResource<DecodedImage>(Image);
        }

        public string Path { get; }

        public DecodedImage Image { get; }

        public SharedResourceLease<DecodedImage> Acquire() => _resource.Acquire();

        public void Dispose() => _resource.ReleaseOwner();
    }

    private sealed class RecordingRenderer : IManagedPhotoRenderer
    {
        private readonly object _sync = new();
        private readonly List<ManagedPhotoKey> _keys = [];

        public IReadOnlyList<ManagedPhotoKey> Keys
        {
            get
            {
                lock (_sync)
                {
                    return _keys.ToArray();
                }
            }
        }

        public ManagedPhotoSource Render(ManagedPhotoRenderRequest request)
        {
            lock (_sync)
            {
                _keys.Add(request.Key);
            }

            var bitmap = new SKBitmap(new SKImageInfo(
                request.Key.EncodedSize.Width,
                request.Key.EncodedSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul));
            return new ManagedPhotoSource(
                request.Key,
                bitmap,
                SKImage.FromBitmap(bitmap),
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(2),
                TimeSpan.FromMilliseconds(1));
        }

        public void Dispose()
        {
        }
    }
}
