using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class PerceptualColorClassifierTests
{
    [Theory]
    [InlineData("#000000", PerceptualHueFamily.Neutral, PerceptualLightnessClass.VeryDark)]
    [InlineData("#08090A", PerceptualHueFamily.Neutral, PerceptualLightnessClass.VeryDark)]
    [InlineData("#FFFFFF", PerceptualHueFamily.Neutral, PerceptualLightnessClass.VeryLight)]
    [InlineData("#808080", PerceptualHueFamily.Neutral, PerceptualLightnessClass.Medium)]
    [InlineData("#807870", PerceptualHueFamily.WarmGray, PerceptualLightnessClass.Medium)]
    [InlineData("#807878", PerceptualHueFamily.WarmGray, PerceptualLightnessClass.Medium)]
    [InlineData("#707988", PerceptualHueFamily.CoolGray, PerceptualLightnessClass.Medium)]
    [InlineData("#FF0000", PerceptualHueFamily.Red, PerceptualLightnessClass.Medium)]
    [InlineData("#690F24", PerceptualHueFamily.Burgundy, PerceptualLightnessClass.Dark)]
    [InlineData("#FF7F50", PerceptualHueFamily.Coral, PerceptualLightnessClass.Light)]
    [InlineData("#FF8000", PerceptualHueFamily.Orange, PerceptualLightnessClass.Light)]
    [InlineData("#FFD000", PerceptualHueFamily.Yellow, PerceptualLightnessClass.Light)]
    [InlineData("#ADFF2F", PerceptualHueFamily.YellowGreen, PerceptualLightnessClass.VeryLight)]
    [InlineData("#00FF00", PerceptualHueFamily.Green, PerceptualLightnessClass.Light)]
    [InlineData("#667A20", PerceptualHueFamily.Olive, PerceptualLightnessClass.Medium)]
    [InlineData("#00BFA5", PerceptualHueFamily.Turquoise, PerceptualLightnessClass.Light)]
    [InlineData("#00D4FF", PerceptualHueFamily.Cyan, PerceptualLightnessClass.Light)]
    [InlineData("#0080FF", PerceptualHueFamily.Blue, PerceptualLightnessClass.Medium)]
    [InlineData("#7030A0", PerceptualHueFamily.Violet, PerceptualLightnessClass.Medium)]
    [InlineData("#8A2BE2", PerceptualHueFamily.BlueViolet, PerceptualLightnessClass.Medium)]
    [InlineData("#FF00FF", PerceptualHueFamily.Magenta, PerceptualLightnessClass.Medium)]
    [InlineData("#FF69B4", PerceptualHueFamily.Pink, PerceptualLightnessClass.Light)]
    [InlineData("#FFC0CB", PerceptualHueFamily.Pink, PerceptualLightnessClass.Light)]
    [InlineData("#8B4513", PerceptualHueFamily.Brown, PerceptualLightnessClass.Medium)]
    [InlineData("#1D3A2B", PerceptualHueFamily.Green, PerceptualLightnessClass.Dark)]
    [InlineData("#F5D6C6", PerceptualHueFamily.Orange, PerceptualLightnessClass.VeryLight)]
    public void RepresentativeReferenceSrgbColorsHaveExpectedPerceptualFamilies(
        string hex,
        object expectedHue,
        object expectedLightness)
    {
        var description = Describe(hex);

        Assert.Equal(expectedHue, description.HueFamily);
        Assert.Equal(expectedLightness, description.LightnessClass);
        Assert.NotNull(description.Oklch);
    }

    [Theory]
    [InlineData("#694044", PerceptualHueFamily.Burgundy, PerceptualChromaClass.Muted)]
    [InlineData("#C86351", PerceptualHueFamily.Coral, PerceptualChromaClass.Moderate)]
    [InlineData("#717485", PerceptualHueFamily.CoolGray, PerceptualChromaClass.Muted)]
    [InlineData("#5D440E", PerceptualHueFamily.Brown, PerceptualChromaClass.Moderate)]
    [InlineData("#79AC07", PerceptualHueFamily.YellowGreen, PerceptualChromaClass.Saturated)]
    [InlineData("#393D58", PerceptualHueFamily.CoolGray, PerceptualChromaClass.Muted)]
    [InlineData("#B49101", PerceptualHueFamily.Olive, PerceptualChromaClass.Moderate)]
    public void OwnerSmokeColorsRemainGeneralTaxonomySanityChecks(
        string hex,
        object expectedHue,
        object expectedChroma)
    {
        var description = Describe(hex);

        Assert.Equal(expectedHue, description.HueFamily);
        Assert.Equal(expectedChroma, description.ChromaClass);
    }

    [Fact]
    public void OklchConversionMatchesAcceptedOklabReferenceForPureRed()
    {
        var actual = OklchColor.FromSrgb(255, 0, 0);

        Assert.Equal(0.627955, actual.L, 6);
        Assert.Equal(0.257683, actual.C, 6);
        Assert.Equal(29.234, actual.HueDegrees, 3);
    }

    [Fact]
    public void LightnessAndChromaThresholdsAreMonotonicAndBounded()
    {
        var lightness = new[] { 0.10, 0.30, 0.60, 0.80, 0.95 }
            .Select(PerceptualColorClassifier.ClassifyLightness)
            .ToArray();
        var chroma = new[] { 0.01, 0.04, 0.10, 0.18, 0.28 }
            .Select(PerceptualColorClassifier.ClassifyChroma)
            .ToArray();

        Assert.Equal(Enum.GetValues<PerceptualLightnessClass>(), lightness);
        Assert.Equal(Enum.GetValues<PerceptualChromaClass>(), chroma);
    }

    [Theory]
    [InlineData(0.249999, PerceptualLightnessClass.VeryDark)]
    [InlineData(0.25, PerceptualLightnessClass.Dark)]
    [InlineData(0.449999, PerceptualLightnessClass.Dark)]
    [InlineData(0.45, PerceptualLightnessClass.Medium)]
    [InlineData(0.719999, PerceptualLightnessClass.Medium)]
    [InlineData(0.72, PerceptualLightnessClass.Light)]
    [InlineData(0.879999, PerceptualLightnessClass.Light)]
    [InlineData(0.88, PerceptualLightnessClass.VeryLight)]
    public void LightnessThresholdBoundariesAreDeterministic(
        double lightness,
        object expected)
    {
        Assert.Equal(expected, PerceptualColorClassifier.ClassifyLightness(lightness));
    }

    [Theory]
    [InlineData(0.024999, PerceptualChromaClass.Neutral)]
    [InlineData(0.025, PerceptualChromaClass.Muted)]
    [InlineData(0.069999, PerceptualChromaClass.Muted)]
    [InlineData(0.07, PerceptualChromaClass.Moderate)]
    [InlineData(0.139999, PerceptualChromaClass.Moderate)]
    [InlineData(0.14, PerceptualChromaClass.Saturated)]
    [InlineData(0.239999, PerceptualChromaClass.Saturated)]
    [InlineData(0.24, PerceptualChromaClass.Vivid)]
    public void ChromaThresholdBoundariesAreDeterministic(double chroma, object expected)
    {
        Assert.Equal(expected, PerceptualColorClassifier.ClassifyChroma(chroma));
    }

    [Fact]
    public void SaturatedColorIsNeverClassifiedAsGrayAndTrueNeutralIsNeverChromatic()
    {
        var saturated = Describe("#00FF00");
        var neutral = Describe("#808080");

        Assert.Equal(PerceptualHueFamily.Green, saturated.HueFamily);
        Assert.Equal(PerceptualChromaClass.Vivid, saturated.ChromaClass);
        Assert.Equal(PerceptualHueFamily.Neutral, neutral.HueFamily);
        Assert.Equal(PerceptualChromaClass.Neutral, neutral.ChromaClass);
    }

    [Fact]
    public void FullyTransparentSampleHasNoInventedPerceptualProperties()
    {
        var sample = new ColorSample(120, 80, 40, 0, "transparent", null, ColorSampleAccuracy.Exact);

        var description = PerceptualColorClassifier.Describe(sample);

        Assert.True(description.IsTransparent);
        Assert.Null(description.Oklch);
        Assert.Null(description.HueFamily);
        Assert.Null(description.LightnessClass);
        Assert.Null(description.ChromaClass);
    }

    private static PerceptualColorDescription Describe(string hex)
    {
        var sample = new ColorSample(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16),
            byte.MaxValue,
            $"rgb-{hex[1..].ToLowerInvariant()}",
            "Creative",
            ColorSampleAccuracy.Exact);
        return PerceptualColorClassifier.Describe(sample);
    }
}