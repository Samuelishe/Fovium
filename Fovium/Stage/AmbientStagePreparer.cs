using System.Diagnostics;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Stage;

internal interface IAmbientStagePreparer
{
    PreparedAmbient Prepare(DecodedImage image, double blur, CancellationToken cancellationToken);
}

internal sealed class AmbientStagePreparer : IAmbientStagePreparer
{
    public PreparedAmbient Prepare(
        DecodedImage image,
        double blur,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!double.IsFinite(blur) ||
            blur < StageDefaults.AmbientBlurMinimum ||
            blur > StageDefaults.AmbientBlurMaximum)
        {
            throw new ArgumentOutOfRangeException(nameof(blur));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var targetSize = CalculateTargetSize(
            image.Descriptor.OrientedSize,
            StageDefaults.AmbientLongEdgePixels);

        using var sourceLease = image.AcquireRenderLease();
        using var colorSpace = SKColorSpace.CreateSrgb();
        var imageInfo = new SKImageInfo(
            targetSize.Width,
            targetSize.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            colorSpace);
        using var orientedSurface = SKSurface.Create(imageInfo)
            ?? throw new InvalidOperationException("Skia could not allocate the oriented Ambient surface.");
        DrawOrientedSource(
            orientedSurface.Canvas,
            sourceLease.Image,
            image.Descriptor.EncodedSize,
            image.Descriptor.Orientation,
            targetSize);
        cancellationToken.ThrowIfCancellationRequested();

        using var orientedImage = orientedSurface.Snapshot();
        using var outputSurface = SKSurface.Create(imageInfo)
            ?? throw new InvalidOperationException("Skia could not allocate the filtered Ambient surface.");
        outputSurface.Canvas.Clear(SKColors.Black);
        using var paint = new SKPaint
        {
            IsAntialias = false,
            ImageFilter = SKImageFilter.CreateBlur(
                (float)blur,
                (float)blur,
                SKShaderTileMode.Clamp),
        };
        var bounds = new SKRect(0, 0, targetSize.Width, targetSize.Height);
        outputSurface.Canvas.DrawImage(
            orientedImage,
            bounds,
            bounds,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
            paint);
        outputSurface.Canvas.Flush();
        cancellationToken.ThrowIfCancellationRequested();
        return new PreparedAmbient(outputSurface.Snapshot(), targetSize, blur, stopwatch.Elapsed);
    }

    public static PixelSize CalculateTargetSize(PixelSize orientedSize, int longEdgePixels)
    {
        if (!orientedSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(orientedSize));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(longEdgePixels);
        var scale = Math.Min(1d, (double)longEdgePixels / Math.Max(orientedSize.Width, orientedSize.Height));
        return new PixelSize(
            Math.Max(1, (int)Math.Round(orientedSize.Width * scale)),
            Math.Max(1, (int)Math.Round(orientedSize.Height * scale)));
    }

    private static void DrawOrientedSource(
        SKCanvas canvas,
        SKImage source,
        PixelSize encodedSize,
        ExifOrientation orientation,
        PixelSize targetSize)
    {
        canvas.Clear(SKColors.Black);
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
