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
        OrientedImageRenderer.Draw(
            orientedSurface.Canvas,
            sourceLease.Image,
            image.Descriptor.EncodedSize,
            image.Descriptor.Orientation,
            targetSize,
            SKColors.Black);
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
        => BoundedImageSize.Calculate(orientedSize, longEdgePixels);

}
