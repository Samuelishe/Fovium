using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Fovium.Diagnostics;
using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Rendering;

// Avalonia marks this direct-Skia lease API unstable. All production access to
// that API is intentionally confined to this replaceable adapter.
internal sealed class SkiaPhotoDrawOperation : ICustomDrawOperation
{
    private DecodedImage.RenderLease? _imageLease;
    private DecodedImage.AmbientLease? _ambientLease;
    private ManagedPhotoSourceLease? _managedSource;
    private readonly PixelSize _encodedSize;
    private readonly ExifOrientation _orientation;
    private readonly RectD _destination;
    private readonly bool _exactPixelSampling;
    private readonly StageSettings _stage;
    private readonly double _renderScaling;
    private readonly long _imageIdentity;
    private readonly long? _ambientIdentity;
    private readonly AmbientRenderFrameDiagnostics _frameDiagnostics;
    private readonly InteractionRenderDiagnostics _interactionDiagnostics;
    private readonly bool _suppressLegacyPhoto;
    private readonly ManagedPhotoPresentationCoordinator? _managedCoordinator;

    public SkiaPhotoDrawOperation(
        Rect bounds,
        DecodedImage.RenderLease imageLease,
        PixelSize encodedSize,
        ExifOrientation orientation,
        RectD destination,
        bool exactPixelSampling,
        StageSettings stage,
        double renderScaling,
        DecodedImage.AmbientLease? ambientLease,
        long imageIdentity,
        long? ambientIdentity,
        AmbientRenderFrameDiagnostics frameDiagnostics,
        InteractionRenderDiagnostics interactionDiagnostics,
        ManagedPhotoSourceLease? managedSource = null,
        bool suppressLegacyPhoto = false,
        ManagedPhotoPresentationCoordinator? managedCoordinator = null)
    {
        Bounds = bounds;
        _imageLease = imageLease;
        _encodedSize = encodedSize;
        _orientation = orientation;
        _destination = destination;
        _exactPixelSampling = exactPixelSampling;
        _stage = stage;
        _renderScaling = renderScaling;
        _ambientLease = ambientLease;
        _imageIdentity = imageIdentity;
        _ambientIdentity = ambientIdentity;
        _frameDiagnostics = frameDiagnostics;
        _interactionDiagnostics = interactionDiagnostics;
        _managedSource = managedSource;
        _suppressLegacyPhoto = suppressLegacyPhoto;
        _managedCoordinator = managedCoordinator;
    }

    public Rect Bounds { get; }

    public bool HitTest(Point point) => Bounds.Contains(point);

    public void Render(ImmediateDrawingContext context)
    {
        _interactionDiagnostics.RecordPhotoPresentationRender();
        _interactionDiagnostics.RecordPhotoSkiaDraw();
        _frameDiagnostics.RecordViewportRender();
        _frameDiagnostics.RecordCustomDrawEntered();
        var imageLease = _imageLease;
        if (imageLease is null)
        {
            return;
        }

        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            _frameDiagnostics.RecordSkiaLeaseUnavailable();
            return;
        }

        using var canvasLease = feature.Lease();
        _frameDiagnostics.RecordSkiaLeaseAcquired();
        var canvas = canvasLease.SkCanvas;
        var ambientLease = _ambientLease;
        SkiaStageRenderer.Draw(
            canvas,
            new RectD(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height),
            _destination,
            _renderScaling,
            _stage,
            ambientLease?.Image,
            ambientLease?.Size,
            _imageIdentity,
            _ambientIdentity,
            _frameDiagnostics);
        var managedSource = _managedSource;
        if (_suppressLegacyPhoto && managedSource is null)
        {
            return;
        }

        DrawPhoto(
            canvas,
            managedSource?.Source.Image ?? imageLease.Image,
            _encodedSize,
            _orientation,
            _destination,
            _exactPixelSampling);
        if (managedSource is not null)
        {
            _managedCoordinator?.RecordManagedSourceFrame();
        }

    }

    public bool Equals(ICustomDrawOperation? other) => false;

    internal static void DrawPhoto(
        SKCanvas canvas,
        SKImage image,
        PixelSize encodedSize,
        ExifOrientation orientation,
        RectD destination,
        bool exactPixelSampling)
    {
        var affine = OrientationAffine.Create(encodedSize, orientation);
        var orientedSize = OrientationTransform.GetOrientedSize(encodedSize, orientation);
        var scaleX = destination.Width / orientedSize.Width;
        var scaleY = destination.Height / orientedSize.Height;
        var matrix = new SKMatrix(
            (float)(affine.A * scaleX),
            (float)(affine.B * scaleX),
            (float)(destination.X + affine.C * scaleX),
            (float)(affine.D * scaleY),
            (float)(affine.E * scaleY),
            (float)(destination.Y + affine.F * scaleY),
            0,
            0,
            1);

        canvas.Save();
        try
        {
            canvas.Concat(in matrix);
            using var paint = new SKPaint { IsAntialias = false };
            var sampling = exactPixelSampling
                ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
                : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            canvas.DrawImage(image, 0, 0, sampling, paint);
        }
        finally
        {
            canvas.Restore();
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _imageLease, null)?.Dispose();
        Interlocked.Exchange(ref _ambientLease, null)?.Dispose();
        Interlocked.Exchange(ref _managedSource, null)?.Dispose();
    }

}
