using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorSampleTests
{
    [Fact]
    public void OpaqueSampleUsesUppercaseSixDigitHexAndRgbDecimals()
    {
        var sample = Create(0x8d, 0x74, 0x65, 255);

        Assert.Equal("#8D7465", sample.Hex);
        Assert.Equal("RGB 141, 116, 101", sample.Components);
        Assert.False(sample.IsTransparent);
    }

    [Fact]
    public void PartialAlphaUsesRgbaHexAndDecimals()
    {
        var sample = Create(141, 116, 101, 128);

        Assert.Equal("#8D746580", sample.Hex);
        Assert.Equal("RGBA 141, 116, 101, 128", sample.Components);
    }

    [Fact]
    public void TransparentSampleUsesTruthfulDecodedRepresentation()
    {
        var sample = new ColorSample(0, 0, 0, 0, "transparent", null, ColorSampleAccuracy.Exact);

        Assert.True(sample.IsTransparent);
        Assert.Equal("#00000000", sample.Hex);
        Assert.Null(sample.CanonicalName);
    }

    private static ColorSample Create(byte red, byte green, byte blue, byte alpha) =>
        new(red, green, blue, alpha, "warm-taupe", "Warm Taupe", ColorSampleAccuracy.Exact);
}
