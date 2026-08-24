using Fovium.Imaging;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.Stage;

internal static class StageTestImages
{
    public static DecodedImage CreateDecoded(
        string path = "photo.png",
        PixelSize? encodedSize = null,
        ExifOrientation orientation = ExifOrientation.Normal,
        long retainedBytes = 256)
    {
        var encoded = encodedSize ?? new PixelSize(12, 8);
        var bitmap = new SKBitmap(new SKImageInfo(
            encoded.Width,
            encoded.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        bitmap.Erase(new SKColor(220, 80, 30, 128));
        var image = SKImage.FromBitmap(bitmap);
        var descriptor = new ImageDescriptor(
            path,
            "Png",
            encoded,
            OrientationTransform.GetOrientedSize(encoded, orientation),
            orientation,
            1,
            SourceColorState.AssumedSrgb,
            false,
            "Bgra8888/Premul",
            retainedBytes,
            retainedBytes,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero);
        return new DecodedImage([1, 2, 3], descriptor, bitmap, image);
    }

    public static PreparedAmbient CreateAmbient(int width = 16, int height = 8)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        bitmap.Erase(SKColors.DarkSlateBlue);
        return new PreparedAmbient(
            SKImage.FromBitmap(bitmap),
            new PixelSize(width, height),
            TimeSpan.FromMilliseconds(1));
    }
}
