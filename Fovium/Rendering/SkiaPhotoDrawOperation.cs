using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Fovium.Diagnostics;
using Fovium.Imaging;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Rendering;

// Avalonia marks this direct-Skia lease API unstable. All production access to
// that API is intentionally confined to this replaceable adapter.
internal sealed class SkiaPhotoDrawOperation : ICustomDrawOperation
{
    private DecodedImage.RenderLease? _imageLease;
    private DecodedImage.AmbientLease? _ambientLease;
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
        InteractionRenderDiagnostics interactionDiagnostics)
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
        var affine = OrientationAffine.Create(_encodedSize, _orientation);
        var orientedSize = OrientationTransform.GetOrientedSize(_encodedSize, _orientation);
        var scaleX = _destination.Width / orientedSize.Width;
        var scaleY = _destination.Height / orientedSize.Height;
        var matrix = new SKMatrix(
            (float)(affine.A * scaleX),
            (float)(affine.B * scaleX),
            (float)(_destination.X + affine.C * scaleX),
            (float)(affine.D * scaleY),
            (float)(affine.E * scaleY),
            (float)(_destination.Y + affine.F * scaleY),
            0,
            0,
            1);

        canvas.Save();
        try
        {
            canvas.Concat(in matrix);
            using var paint = new SKPaint { IsAntialias = false };
            var sampling = _exactPixelSampling
                ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
                : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            canvas.DrawImage(imageLease.Image, 0, 0, sampling, paint);
        }
        finally
        {
            canvas.Restore();
        }

    }

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
        Interlocked.Exchange(ref _imageLease, null)?.Dispose();
        Interlocked.Exchange(ref _ambientLease, null)?.Dispose();
    }

}
