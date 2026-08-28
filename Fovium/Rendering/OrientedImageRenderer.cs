using Fovium.Imaging;
using SkiaSharp;

namespace Fovium.Rendering;

internal static class OrientedImageRenderer
{
    public static void Draw(
        SKCanvas canvas,
        SKImage source,
        PixelSize encodedSize,
        ExifOrientation orientation,
        PixelSize targetSize,
        SKColor? clearColor = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(source);
        if (!encodedSize.IsValid || !targetSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSize));
        }

        canvas.Clear(clearColor ?? SKColors.Transparent);
        var affine = OrientationAffine.Create(encodedSize, orientation);
        var orientedSize = OrientationTransform.GetOrientedSize(encodedSize, orientation);
        var scaleX = (double)targetSize.Width / orientedSize.Width;
        var scaleY = (double)targetSize.Height / orientedSize.Height;
        var matrix = new SKMatrix(
            (float)(affine.A * scaleX),
            (float)(affine.B * scaleX),
            (float)(affine.C * scaleX),
            (float)(affine.D * scaleY),
            (float)(affine.E * scaleY),
            (float)(affine.F * scaleY),
            0,
            0,
            1);
        canvas.Concat(in matrix);
        using var paint = new SKPaint { IsAntialias = false };
        canvas.DrawImage(
            source,
            0,
            0,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
            paint);
        canvas.Flush();
    }
}
