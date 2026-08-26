using Fovium.ColorManagementProbe;
using Fovium.ColorPicking;
using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorManagementProbe;

public sealed class ColorDomainIndependenceTests
{
    [Fact]
    public async Task DestinationTransformsDoNotRedefinePickerOrSourceDomainHistogram()
    {
        var sourcePixel = new ProbePixel(196, 83, 41, 255);
        using var image = CreateImage(sourcePixel);
        var sampler = new PhotoColorSampler(() => new ColorNameMatcher(
            ColorNameCatalog.CreateForTests(
                [new ColorNameEntry("source", 196, 83, 41, "Source")])));
        var histogramReader = new SkiaDecodedHistogramReader();

        var pickerBefore = sampler.Sample(image, new PixelPoint(0, 0));
        var histogramBefore = Assert.IsType<HistogramData>(
            (await histogramReader.ReadAsync(image, CancellationToken.None)).Data);

        using var sourceSpace = SKColorSpace.CreateSrgb();
        using var displayP3 = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.DisplayP3);
        using var adobeRgb = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.AdobeRgb);
        var displayP3Pixel = SkiaColorTransformProbe.TransformPixel(sourcePixel, sourceSpace, displayP3);
        var adobeRgbPixel = SkiaColorTransformProbe.TransformPixel(sourcePixel, sourceSpace, adobeRgb);

        var pickerAfter = sampler.Sample(image, new PixelPoint(0, 0));
        var histogramAfter = Assert.IsType<HistogramData>(
            (await histogramReader.ReadAsync(image, CancellationToken.None)).Data);

        Assert.NotEqual(displayP3Pixel, adobeRgbPixel);
        Assert.Equal(pickerBefore, pickerAfter);
        Assert.Equal("#C45329", pickerAfter.Hex);
        Assert.Equal(histogramBefore.Red, histogramAfter.Red);
        Assert.Equal(histogramBefore.Green, histogramAfter.Green);
        Assert.Equal(histogramBefore.Blue, histogramAfter.Blue);
        Assert.Equal(2, histogramAfter.SampleCount);
        Assert.Equal(1, histogramAfter.Red[196]);
        Assert.Equal(1, histogramAfter.Green[83]);
        Assert.Equal(1, histogramAfter.Blue[41]);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.EncodedSource);
        using var pixels = image.AcquirePixelLease();
        Assert.Equal(new byte[] { 41, 83, 196, 255 }, pixels.PixelBytes[..4].ToArray());
    }

    private static DecodedImage CreateImage(ProbePixel source)
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(
            2,
            1,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            colorSpace);
        var bitmap = new SKBitmap(info);
        var bytes = bitmap.GetPixelSpan();
        SetPixel(bytes, 0, source);
        SetPixel(bytes, 1, new ProbePixel(10, 20, 30, 255));
        var image = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(2, 1);
        return new DecodedImage(
            [1, 2, 3],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.NormalizedSrgb,
                false,
                "Bgra8888/Premul",
                bitmap.ByteCount,
                bitmap.ByteCount,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            image);
    }

    private static void SetPixel(Span<byte> bytes, int x, ProbePixel pixel)
    {
        var offset = x * 4;
        bytes[offset] = pixel.Blue;
        bytes[offset + 1] = pixel.Green;
        bytes[offset + 2] = pixel.Red;
        bytes[offset + 3] = pixel.Alpha;
    }
}
