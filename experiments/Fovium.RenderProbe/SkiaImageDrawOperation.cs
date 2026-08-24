using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Fovium.RenderProbe;

// All use of Avalonia's explicitly unstable direct-Skia lease is confined to this adapter.
internal sealed class SkiaImageDrawOperation : ICustomDrawOperation
{
    private readonly SKImage _image;
    private readonly ImageSize _encodedSize;
    private readonly ExifOrientation _orientation;
    private readonly RectD _destination;
    private readonly SamplingMode _sampling;

    public SkiaImageDrawOperation(
        Rect bounds,
        SKImage image,
        ImageSize encodedSize,
        ExifOrientation orientation,
        RectD destination,
        SamplingMode sampling)
    {
        Bounds = bounds;
        _image = image;
        _encodedSize = encodedSize;
        _orientation = orientation;
        _destination = destination;
        _sampling = sampling;
    }

    public Rect Bounds { get; }

    public bool HitTest(Point point) => Bounds.Contains(point);

    public void Render(ImmediateDrawingContext context)
    {
        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            return;
        }

        using var lease = feature.Lease();
        var canvas = lease.SkCanvas;
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
            canvas.DrawImage(_image, 0, 0, ToSkiaSampling(_sampling), paint);
        }
        finally
        {
            canvas.Restore();
        }
    }

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
        // The control owns the image; the operation owns no native resource after Render returns.
    }

    private static SKSamplingOptions ToSkiaSampling(SamplingMode sampling) =>
        sampling switch
        {
            SamplingMode.Nearest => new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
            SamplingMode.Linear => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            SamplingMode.LinearMipmap => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
            SamplingMode.Mitchell => new SKSamplingOptions(SKCubicResampler.Mitchell),
            SamplingMode.CatmullRom => new SKSamplingOptions(SKCubicResampler.CatmullRom),
            _ => throw new ArgumentOutOfRangeException(nameof(sampling)),
        };
}
