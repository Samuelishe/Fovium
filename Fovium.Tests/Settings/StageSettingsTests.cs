using Fovium.Stage;

namespace Fovium.Tests.Settings;

public sealed class StageSettingsTests
{
    [Fact]
    public void DefaultsAreCentralizedAndWithinUserRanges()
    {
        var defaults = StageSettings.Default;

        Assert.Equal(StageBackgroundMode.Black, defaults.BackgroundMode);
        Assert.False(defaults.MatteEnabled);
        Assert.Equal("#202020", defaults.CustomBackgroundColor.ToHex());
        Assert.Equal("#202020", defaults.MatteColor.ToHex());
        Assert.Equal(MatteStyle.Solid, defaults.MatteStyle);
        Assert.Equal(24, defaults.MatteWidthPhysicalPixels);
        Assert.Equal(0.65, defaults.AmbientBrightness);
        Assert.Equal(0.85, defaults.AmbientSaturation);
        Assert.Equal(18, defaults.AmbientBlur);
        Assert.InRange(defaults.AmbientBrightness, 0.30, 1.00);
        Assert.InRange(defaults.AmbientSaturation, 0.00, 1.25);
        Assert.InRange(defaults.AmbientBlur, 8, 32);
    }

    [Theory]
    [InlineData(-1, 2, 2, 0.30, 1.25, 8)]
    [InlineData(2, -1, 0, 1.00, 0.00, 8)]
    public void OutOfRangeAmbientValuesAreClamped(
        double brightness,
        double saturation,
        double blur,
        double expectedBrightness,
        double expectedSaturation,
        double expectedBlur)
    {
        var normalized = (StageSettings.Default with
        {
            AmbientBrightness = brightness,
            AmbientSaturation = saturation,
            AmbientBlur = blur,
        }).Normalize();

        Assert.Equal(expectedBrightness, normalized.AmbientBrightness);
        Assert.Equal(expectedSaturation, normalized.AmbientSaturation);
        Assert.Equal(expectedBlur, normalized.AmbientBlur);
    }

    [Fact]
    public void NonFiniteAmbientValuesUseDefaults()
    {
        var normalized = (StageSettings.Default with
        {
            AmbientBrightness = double.NaN,
            AmbientSaturation = double.PositiveInfinity,
            AmbientBlur = double.NegativeInfinity,
        }).Normalize();

        Assert.Equal(StageDefaults.AmbientBrightness, normalized.AmbientBrightness);
        Assert.Equal(StageDefaults.AmbientSaturation, normalized.AmbientSaturation);
        Assert.Equal(StageDefaults.AmbientBlurSigmaPixels, normalized.AmbientBlur);
    }

    [Fact]
    public void UnknownBackgroundValueNormalizesToBlack()
    {
        var normalized = (StageSettings.Default with
        {
            BackgroundMode = (StageBackgroundMode)999,
        }).Normalize();

        Assert.Equal(StageBackgroundMode.Black, normalized.BackgroundMode);
    }

    [Fact]
    public void InvalidMatteStyleAndNonFiniteWidthUseDefaults()
    {
        var normalized = (StageSettings.Default with
        {
            MatteStyle = (MatteStyle)999,
            MatteWidthPhysicalPixels = double.NaN,
        }).Normalize();

        Assert.Equal(MatteStyle.Solid, normalized.MatteStyle);
        Assert.Equal(24, normalized.MatteWidthPhysicalPixels);
    }

    [Theory]
    [InlineData(-100, 4)]
    [InlineData(0, 4)]
    [InlineData(3.99, 4)]
    [InlineData(4, 4)]
    [InlineData(192, 192)]
    [InlineData(192.01, 192)]
    [InlineData(1000, 192)]
    public void MatteWidthIsClampedToPhysicalPixelRange(double value, double expected)
    {
        var normalized = (StageSettings.Default with { MatteWidthPhysicalPixels = value }).Normalize();

        Assert.Equal(expected, normalized.MatteWidthPhysicalPixels);
    }

    [Theory]
    [InlineData("#000000", 0, 0, 0)]
    [InlineData("#123ABC", 0x12, 0x3A, 0xBC)]
    [InlineData("#ffffff", 255, 255, 255)]
    public void ColorRoundTripUsesCanonicalOpaqueHex(string value, int red, int green, int blue)
    {
        Assert.True(StageColor.TryParse(value, out var color));
        Assert.Equal((byte)red, color.Red);
        Assert.Equal((byte)green, color.Green);
        Assert.Equal((byte)blue, color.Blue);
        Assert.Equal(value.ToUpperInvariant(), color.ToHex());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    [InlineData("#11223344")]
    public void InvalidColorsAreRejected(string? value) =>
        Assert.False(StageColor.TryParse(value, out _));
}
