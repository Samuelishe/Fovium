using Fovium.ColorPicking;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorPicking;

public sealed class PhotoColorSamplerTests
{
    [Fact]
    public void OpaquePixelReadsBgraChannelsAsExactRgb()
    {
        using var image = CreateImage(10, 20, 30, 255, SourceColorState.AssumedSrgb);

        var sample = CreateSampler(10, 20, 30).Sample(image, new PixelPoint(0, 0));

        Assert.Equal((byte)10, sample.Red);
        Assert.Equal((byte)20, sample.Green);
        Assert.Equal((byte)30, sample.Blue);
        Assert.Equal((byte)255, sample.Alpha);
        Assert.Equal(ColorSampleAccuracy.Exact, sample.Accuracy);
    }

    [Fact]
    public void HalfAlphaUnpremultipliesExactlyOnceBeforeMatching()
    {
        using var image = CreateImage(50, 25, 10, 128, SourceColorState.NormalizedSrgb);

        var sample = CreateSampler(100, 50, 20).Sample(image, new PixelPoint(0, 0));

        Assert.Equal((byte)100, sample.Red);
        Assert.Equal((byte)50, sample.Green);
        Assert.Equal((byte)20, sample.Blue);
        Assert.Equal((byte)128, sample.Alpha);
        Assert.Equal("expected", sample.ColorNameStableId);
    }

    [Fact]
    public void AlphaOneUsesCheckedRoundedUnpremultiplication()
    {
        using var image = CreateImage(1, 0, 1, 1, SourceColorState.AssumedSrgb);

        var sample = CreateSampler(255, 0, 255).Sample(image, new PixelPoint(0, 0));

        Assert.Equal((byte)255, sample.Red);
        Assert.Equal((byte)0, sample.Green);
        Assert.Equal((byte)255, sample.Blue);
        Assert.Equal((byte)1, sample.Alpha);
    }

    [Fact]
    public void TransparentPixelBypassesCatalogInitializationAndNaming()
    {
        var factoryCalls = 0;
        using var image = CreateImage(99, 88, 77, 0, SourceColorState.NormalizedNonSrgb);
        var sampler = new PhotoColorSampler(() =>
        {
            factoryCalls++;
            return CreateMatcher(1, 2, 3);
        });

        var sample = sampler.Sample(image, new PixelPoint(0, 0));

        Assert.Equal(0, factoryCalls);
        Assert.True(sample.IsTransparent);
        Assert.Equal("transparent", sample.ColorNameStableId);
        Assert.Null(sample.CanonicalName);
        Assert.Equal("#00000000", sample.Hex);
    }

    [Theory]
    [InlineData((int)SourceColorState.AssumedSrgb)]
    [InlineData((int)SourceColorState.NormalizedSrgb)]
    [InlineData((int)SourceColorState.NormalizedSrgbFromNclx)]
    public void DirectSrgbStatesRemainExact(int stateValue)
    {
        using var image = CreateImage(11, 22, 33, 255, (SourceColorState)stateValue);

        var sample = CreateSampler(11, 22, 33).Sample(image, new PixelPoint(0, 0));

        Assert.Equal((byte)11, sample.Red);
        Assert.Equal((byte)22, sample.Green);
        Assert.Equal((byte)33, sample.Blue);
        Assert.Equal(ColorSampleAccuracy.Exact, sample.Accuracy);
    }

    [Fact]
    public void UnpreservedProfileKeepsAvailableRgbButMarksApproximate()
    {
        using var image = CreateImage(11, 22, 33, 255, SourceColorState.EmbeddedProfileUnpreserved);

        var sample = CreateSampler(11, 22, 33).Sample(image, new PixelPoint(0, 0));

        Assert.Equal((byte)11, sample.Red);
        Assert.Equal((byte)22, sample.Green);
        Assert.Equal((byte)33, sample.Blue);
        Assert.Equal(ColorSampleAccuracy.Approximate, sample.Accuracy);
    }

    [Fact]
    public void NormalizedNonSrgbUsesSinglePixelSkiaConversionToReferenceSrgb()
    {
        using var linearSrgb = SKColorSpace.CreateSrgbLinear();
        using var image = CreateImage(
            128,
            0,
            0,
            255,
            SourceColorState.NormalizedNonSrgb,
            linearSrgb);

        var sample = CreateSampler(188, 0, 0).Sample(image, new PixelPoint(0, 0));

        Assert.InRange(sample.Red, (byte)187, (byte)189);
        Assert.Equal((byte)0, sample.Green);
        Assert.Equal((byte)0, sample.Blue);
        Assert.Equal(ColorSampleAccuracy.Exact, sample.Accuracy);
    }

    [Fact]
    public void SamplingDisposedImageFailsWithoutCatalogInitialization()
    {
        var factoryCalls = 0;
        var image = CreateImage(1, 2, 3, 255, SourceColorState.AssumedSrgb);
        image.Dispose();
        var sampler = new PhotoColorSampler(() =>
        {
            factoryCalls++;
            return CreateMatcher(1, 2, 3);
        });

        Assert.Throws<ObjectDisposedException>(() => sampler.Sample(image, new PixelPoint(0, 0)));
        Assert.Equal(0, factoryCalls);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(4, 40)]
    [InlineData(5, 10)]
    [InlineData(6, 40)]
    [InlineData(7, 60)]
    [InlineData(8, 30)]
    public void AllExifOrientationsSampleTheVisibleTopLeftPixelExactlyOnce(
        int orientationValue,
        byte expectedRed)
    {
        using var image = CreateOrientedImage((ExifOrientation)orientationValue);

        var sample = CreateSampler(expectedRed, 0, 0).Sample(image, new PixelPoint(0, 0));

        Assert.Equal(expectedRed, sample.Red);
        Assert.Equal((byte)0, sample.Green);
        Assert.Equal((byte)0, sample.Blue);
    }

    private static PhotoColorSampler CreateSampler(byte red, byte green, byte blue) =>
        new(() => CreateMatcher(red, green, blue));

    private static ColorNameMatcher CreateMatcher(byte red, byte green, byte blue) =>
        new(ColorNameCatalog.CreateForTests(
            [new ColorNameEntry("expected", red, green, blue, "Expected")]));

    private static DecodedImage CreateImage(
        byte red,
        byte green,
        byte blue,
        byte alpha,
        SourceColorState colorState,
        SKColorSpace? colorSpace = null)
    {
        var info = new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
        var bitmap = new SKBitmap(info);
        var bytes = bitmap.GetPixelSpan();
        bytes[0] = blue;
        bytes[1] = green;
        bytes[2] = red;
        bytes[3] = alpha;
        var image = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(1, 1);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                colorState,
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

    private static DecodedImage CreateOrientedImage(ExifOrientation orientation)
    {
        var size = new PixelSize(3, 2);
        var bitmap = new SKBitmap(new SKImageInfo(3, 2, SKColorType.Bgra8888, SKAlphaType.Premul));
        var bytes = bitmap.GetPixelSpan();
        var redValues = new byte[] { 10, 20, 30, 40, 50, 60 };
        for (var index = 0; index < redValues.Length; index++)
        {
            bytes[(index * 4) + 2] = redValues[index];
            bytes[(index * 4) + 3] = 255;
        }

        var image = SKImage.FromBitmap(bitmap);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Jpeg,
                size,
                OrientationTransform.GetOrientedSize(size, orientation),
                orientation,
                1,
                SourceColorState.AssumedSrgb,
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
}
