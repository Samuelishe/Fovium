using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.ColorPicking;

internal sealed class PhotoColorSampler
{
    private readonly Lazy<ColorNameMatcher> _matcher;

    public PhotoColorSampler(Func<ColorNameMatcher>? matcherFactory = null)
    {
        _matcher = new Lazy<ColorNameMatcher>(
            matcherFactory ?? (() => new ColorNameMatcher(ColorNameCatalog.LoadEmbedded())),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ColorSample Sample(DecodedImage image, PixelPoint orientedPixel)
    {
        ArgumentNullException.ThrowIfNull(image);
        var descriptor = image.Descriptor;
        var encodedPixel = OrientationTransform.OrientedToEncodedPixel(
            descriptor.EncodedSize,
            descriptor.Orientation,
            orientedPixel);
        using var pixels = image.AcquirePixelLease();
        if (pixels.ColorType != SKColorType.Bgra8888 ||
            pixels.AlphaType != SKAlphaType.Premul ||
            encodedPixel.X < 0 || encodedPixel.X >= pixels.Width ||
            encodedPixel.Y < 0 || encodedPixel.Y >= pixels.Height)
        {
            throw new InvalidOperationException("The presented image has no supported sampleable pixel layout.");
        }

        var raw = ReadRawPremultiplied(pixels, encodedPixel);
        var accuracy = descriptor.ColorState == SourceColorState.EmbeddedProfileUnpreserved
            ? ColorSampleAccuracy.Approximate
            : ColorSampleAccuracy.Exact;
        var color = raw.Alpha == 0
            ? new SampledRgba(0, 0, 0, 0)
            : descriptor.ColorState == SourceColorState.NormalizedNonSrgb
                ? ReadNormalizedNonSrgb(pixels, encodedPixel, raw, ref accuracy)
                : Unpremultiply(raw);

        if (color.Alpha == 0)
        {
            return new ColorSample(
                0,
                0,
                0,
                0,
                "transparent",
                null,
                accuracy);
        }

        var match = _matcher.Value.FindNearest(color.Red, color.Green, color.Blue);
        return new ColorSample(
            color.Red,
            color.Green,
            color.Blue,
            color.Alpha,
            match.StableId,
            match.CanonicalName,
            accuracy);
    }

    private static SampledRgba ReadRawPremultiplied(
        DecodedImage.PixelLease pixels,
        PixelPoint encodedPixel)
    {
        var offset = checked((encodedPixel.Y * pixels.RowBytes) + (encodedPixel.X * 4));
        var bytes = pixels.PixelBytes;
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidOperationException("The sample pixel lies outside the decoded raster.");
        }

        return new SampledRgba(bytes[offset + 2], bytes[offset + 1], bytes[offset], bytes[offset + 3]);
    }

    private static SampledRgba ReadNormalizedNonSrgb(
        DecodedImage.PixelLease pixels,
        PixelPoint encodedPixel,
        SampledRgba fallback,
        ref ColorSampleAccuracy accuracy)
    {
        Span<byte> converted = stackalloc byte[4];
        if (!pixels.TryReadSrgbUnpremultiplied(encodedPixel.X, encodedPixel.Y, converted))
        {
            accuracy = ColorSampleAccuracy.Approximate;
            return Unpremultiply(fallback);
        }

        return new SampledRgba(converted[2], converted[1], converted[0], converted[3]);
    }

    internal static SampledRgba Unpremultiply(SampledRgba color)
    {
        if (color.Alpha == 0)
        {
            return new SampledRgba(0, 0, 0, 0);
        }

        if (color.Alpha == byte.MaxValue)
        {
            return color;
        }

        return new SampledRgba(
            UnpremultiplyChannel(color.Red, color.Alpha),
            UnpremultiplyChannel(color.Green, color.Alpha),
            UnpremultiplyChannel(color.Blue, color.Alpha),
            color.Alpha);
    }

    private static byte UnpremultiplyChannel(byte channel, byte alpha) =>
        (byte)Math.Min(byte.MaxValue, ((channel * byte.MaxValue) + (alpha / 2)) / alpha);
}

internal readonly record struct SampledRgba(byte Red, byte Green, byte Blue, byte Alpha);
