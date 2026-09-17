using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class PerceptualColorClassifierTests
{
    [Theory]
    [InlineData("#000000", PerceptualColorRole.NearBlack, PerceptualHueFamily.Neutral,
        PerceptualLightnessClass.VeryDark)]
    [InlineData("#08090A", PerceptualColorRole.NearBlack, PerceptualHueFamily.Neutral,
        PerceptualLightnessClass.VeryDark)]
    [InlineData("#FFFFFF", PerceptualColorRole.NearWhite, PerceptualHueFamily.Neutral,
        PerceptualLightnessClass.VeryLight)]
    [InlineData("#808080", PerceptualColorRole.Neutral, PerceptualHueFamily.Neutral, PerceptualLightnessClass.Medium)]
    [InlineData("#807870", PerceptualColorRole.NearNeutral, PerceptualHueFamily.Greige,
        PerceptualLightnessClass.Medium)]
    [InlineData("#807878", PerceptualColorRole.NearNeutral, PerceptualHueFamily.RoseGray,
        PerceptualLightnessClass.Medium)]
    [InlineData("#707988", PerceptualColorRole.TintedNeutral, PerceptualHueFamily.BlueGray,
        PerceptualLightnessClass.Medium)]
    [InlineData("#FF0000", PerceptualColorRole.Chromatic, PerceptualHueFamily.Red, PerceptualLightnessClass.Medium)]
    [InlineData("#690F24", PerceptualColorRole.Chromatic, PerceptualHueFamily.Burgundy, PerceptualLightnessClass.Dark)]
    [InlineData("#FF7F50", PerceptualColorRole.Chromatic, PerceptualHueFamily.Coral, PerceptualLightnessClass.Light)]
    [InlineData("#FF8000", PerceptualColorRole.Chromatic, PerceptualHueFamily.Orange, PerceptualLightnessClass.Light)]
    [InlineData("#FFD000", PerceptualColorRole.Chromatic, PerceptualHueFamily.Yellow, PerceptualLightnessClass.Light)]
    [InlineData("#ADFF2F", PerceptualColorRole.Chromatic, PerceptualHueFamily.YellowGreen,
        PerceptualLightnessClass.VeryLight)]
    [InlineData("#00FF00", PerceptualColorRole.Chromatic, PerceptualHueFamily.Green, PerceptualLightnessClass.Light)]
    [InlineData("#667A20", PerceptualColorRole.Chromatic, PerceptualHueFamily.OliveGreen,
        PerceptualLightnessClass.Medium)]
    [InlineData("#00BFA5", PerceptualColorRole.Chromatic, PerceptualHueFamily.Turquoise,
        PerceptualLightnessClass.Light)]
    [InlineData("#00D4FF", PerceptualColorRole.Chromatic, PerceptualHueFamily.Cyan, PerceptualLightnessClass.Light)]
    [InlineData("#0080FF", PerceptualColorRole.Chromatic, PerceptualHueFamily.Blue, PerceptualLightnessClass.Medium)]
    [InlineData("#7030A0", PerceptualColorRole.Chromatic, PerceptualHueFamily.BlueViolet,
        PerceptualLightnessClass.Medium)]
    [InlineData("#8A2BE2", PerceptualColorRole.Chromatic, PerceptualHueFamily.BlueViolet,
        PerceptualLightnessClass.Medium)]
    [InlineData("#FF00FF", PerceptualColorRole.Chromatic, PerceptualHueFamily.Magenta, PerceptualLightnessClass.Medium)]
    [InlineData("#FF69B4", PerceptualColorRole.Chromatic, PerceptualHueFamily.Pink, PerceptualLightnessClass.Light)]
    [InlineData("#FFC0CB", PerceptualColorRole.Chromatic, PerceptualHueFamily.Rose, PerceptualLightnessClass.Light)]
    [InlineData("#8B4513", PerceptualColorRole.Chromatic, PerceptualHueFamily.Brown, PerceptualLightnessClass.Medium)]
    [InlineData("#1D3A2B", PerceptualColorRole.Chromatic, PerceptualHueFamily.Green, PerceptualLightnessClass.Dark)]
    [InlineData("#F5D6C6", PerceptualColorRole.Chromatic, PerceptualHueFamily.Peach,
        PerceptualLightnessClass.VeryLight)]
    public void RepresentativeReferenceSrgbColorsHaveExpectedPerceptualFamilies(
        string hex,
        object expectedRole,
        object expectedHue,
        object expectedLightness)
    {
        var description = Describe(hex);

        Assert.Equal(expectedRole, description.Role);
        Assert.Equal(expectedHue, description.HueFamily);
        Assert.Equal(expectedLightness, description.LightnessClass);
        Assert.NotNull(description.Oklch);
    }

    [Theory]
    [InlineData("#694044", PerceptualHueFamily.Burgundy, PerceptualChromaClass.Muted)]
    [InlineData("#C86351", PerceptualHueFamily.Coral, PerceptualChromaClass.Moderate)]
    [InlineData("#717485", PerceptualHueFamily.BlueGray, PerceptualChromaClass.Muted)]
    [InlineData("#5D440E", PerceptualHueFamily.Brown, PerceptualChromaClass.Moderate)]
    [InlineData("#79AC07", PerceptualHueFamily.YellowGreen, PerceptualChromaClass.Saturated)]
    [InlineData("#393D58", PerceptualHueFamily.BlueGray, PerceptualChromaClass.Muted)]
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

    [Theory]
    [InlineData("#344F67", PerceptualColorRole.TintedNeutral, PerceptualHueFamily.BlueGray, PerceptualUndertone.Blue)]
    [InlineData("#ADC8E5", PerceptualColorRole.Chromatic, PerceptualHueFamily.Blue, PerceptualUndertone.None)]
    [InlineData("#CDD9F3", PerceptualColorRole.TintedNeutral, PerceptualHueFamily.BlueGray, PerceptualUndertone.Blue)]
    [InlineData("#190B0B", PerceptualColorRole.NearBlack, PerceptualHueFamily.Red, PerceptualUndertone.Red)]
    [InlineData("#020100", PerceptualColorRole.NearBlack, PerceptualHueFamily.Neutral, PerceptualUndertone.None)]
    [InlineData("#0A0A02", PerceptualColorRole.NearBlack, PerceptualHueFamily.Olive, PerceptualUndertone.Olive)]
    [InlineData("#010300", PerceptualColorRole.NearBlack, PerceptualHueFamily.Green, PerceptualUndertone.Green)]
    [InlineData("#07060B", PerceptualColorRole.NearBlack, PerceptualHueFamily.Neutral, PerceptualUndertone.None)]
    [InlineData("#80A53E", PerceptualColorRole.Chromatic, PerceptualHueFamily.OliveGreen, PerceptualUndertone.None)]
    [InlineData("#456800", PerceptualColorRole.Chromatic, PerceptualHueFamily.OliveGreen, PerceptualUndertone.None)]
    [InlineData("#B38093", PerceptualColorRole.Chromatic, PerceptualHueFamily.Rose, PerceptualUndertone.None)]
    [InlineData("#FDC8F6", PerceptualColorRole.Chromatic, PerceptualHueFamily.PinkLilac, PerceptualUndertone.None)]
    [InlineData("#A4256C", PerceptualColorRole.Chromatic, PerceptualHueFamily.Crimson, PerceptualUndertone.None)]
    [InlineData("#828FC4", PerceptualColorRole.Chromatic, PerceptualHueFamily.BlueViolet, PerceptualUndertone.None)]
    [InlineData("#59A3A6", PerceptualColorRole.Chromatic, PerceptualHueFamily.TurquoiseCyan, PerceptualUndertone.None)]
    [InlineData("#F8E2CD", PerceptualColorRole.NearWhite, PerceptualHueFamily.Cream, PerceptualUndertone.Olive)]
    [InlineData("#9BA29A", PerceptualColorRole.NearNeutral, PerceptualHueFamily.GreenGray, PerceptualUndertone.Green)]
    public void EmpiricalOwnerVectorsExerciseRolesUndertonesAndTransitionFamilies(
        string hex,
        object expectedRole,
        object expectedHue,
        object expectedUndertone)
    {
        var description = Describe(hex);

        Assert.Equal(expectedRole, description.Role);
        Assert.Equal(expectedHue, description.HueFamily);
        Assert.Equal(expectedUndertone, description.Undertone);
    }

    [Theory]
    [InlineData("#C2992D", PerceptualColorRole.Chromatic, PerceptualHueFamily.Mustard)]
    [InlineData("#B09E8A", PerceptualColorRole.TintedNeutral, PerceptualHueFamily.Greige)]
    [InlineData("#D2C1AD", PerceptualColorRole.TintedNeutral, PerceptualHueFamily.Greige)]
    [InlineData("#DBC5AE", PerceptualColorRole.Chromatic, PerceptualHueFamily.Beige)]
    [InlineData("#D4B99E", PerceptualColorRole.Chromatic, PerceptualHueFamily.Beige)]
    [InlineData("#C1A98F", PerceptualColorRole.Chromatic, PerceptualHueFamily.Beige)]
    [InlineData("#DDBB96", PerceptualColorRole.Chromatic, PerceptualHueFamily.Sand)]
    [InlineData("#FFD5A8", PerceptualColorRole.Chromatic, PerceptualHueFamily.Apricot)]
    [InlineData("#FEB08C", PerceptualColorRole.Chromatic, PerceptualHueFamily.Peach)]
    [InlineData("#EF9D77", PerceptualColorRole.Chromatic, PerceptualHueFamily.Peach)]
    [InlineData("#C6C0CA", PerceptualColorRole.NearNeutral, PerceptualHueFamily.LilacGray)]
    [InlineData("#737189", PerceptualColorRole.TintedNeutral, PerceptualHueFamily.VioletGray)]
    [InlineData("#6E6B72", PerceptualColorRole.NearNeutral, PerceptualHueFamily.LilacGray)]
    [InlineData("#FFFDE8", PerceptualColorRole.NearWhite, PerceptualHueFamily.Cream)]
    public void WarmNeutralAndPurpleNeutralOwnerVectorsUseDedicatedFamilies(
        string hex,
        object expectedRole,
        object expectedHue)
    {
        var description = Describe(hex);

        Assert.Equal(expectedRole, description.Role);
        Assert.Equal(expectedHue, description.HueFamily);
    }

    [Theory]
    [InlineData(0.014999, PerceptualHueFamily.OliveGray)]
    [InlineData(0.015, PerceptualHueFamily.Greige)]
    [InlineData(0.037999, PerceptualHueFamily.Greige)]
    [InlineData(0.038, PerceptualHueFamily.Beige)]
    [InlineData(0.054999, PerceptualHueFamily.Beige)]
    [InlineData(0.055, PerceptualHueFamily.Sand)]
    [InlineData(0.089999, PerceptualHueFamily.Sand)]
    [InlineData(0.090, PerceptualHueFamily.Ochre)]
    public void WarmNeutralChromaBandsMeetWithoutGapsOrOscillation(
        double chroma,
        object expected)
    {
        Assert.Equal(
            expected,
            PerceptualColorClassifier.ClassifyHue(new OklchColor(0.70, chroma, 70)));
    }

    [Theory]
    [InlineData(0.759999, 0.11, 46, PerceptualHueFamily.Terracotta)]
    [InlineData(0.760, 0.11, 46, PerceptualHueFamily.Peach)]
    [InlineData(0.860, 0.064999, 69, PerceptualHueFamily.Sand)]
    [InlineData(0.860, 0.065, 69, PerceptualHueFamily.Apricot)]
    [InlineData(0.70, 0.11, 81.999, PerceptualHueFamily.Ochre)]
    [InlineData(0.70, 0.11, 82, PerceptualHueFamily.Mustard)]
    [InlineData(0.899999, 0.03, 100, PerceptualHueFamily.OliveGray)]
    [InlineData(0.90, 0.03, 100, PerceptualHueFamily.Cream)]
    public void EarthAndWarmWhiteBoundariesAreDeterministic(
        double lightness,
        double chroma,
        double hue,
        object expected)
    {
        Assert.Equal(expected, PerceptualColorClassifier.ClassifyHue(new OklchColor(lightness, chroma, hue)));
    }

    [Theory]
    [InlineData(284.999, PerceptualHueFamily.BlueGray)]
    [InlineData(285, PerceptualHueFamily.VioletGray)]
    [InlineData(299.999, PerceptualHueFamily.VioletGray)]
    [InlineData(300, PerceptualHueFamily.LilacGray)]
    [InlineData(324.999, PerceptualHueFamily.LilacGray)]
    [InlineData(325, PerceptualHueFamily.RoseGray)]
    public void PurpleNeutralTransitionsAreDeterministic(double hue, object expected)
    {
        Assert.Equal(expected, PerceptualColorClassifier.ClassifyHue(new OklchColor(0.60, 0.03, hue)));
    }

    [Theory]
    [InlineData("#FF634A", PerceptualHueFamily.Coral)]
    [InlineData("#AC2D7A", PerceptualHueFamily.Crimson)]
    [InlineData("#341D6D", PerceptualHueFamily.BlueViolet)]
    [InlineData("#6AEBF1", PerceptualHueFamily.TurquoiseCyan)]
    [InlineData("#505E23", PerceptualHueFamily.OliveGreen)]
    [InlineData("#1A2529", PerceptualHueFamily.BlueGray)]
    [InlineData("#D3F5FF", PerceptualHueFamily.Blue)]
    [InlineData("#E2D9DC", PerceptualHueFamily.RoseGray)]
    public void AcceptedColorRegionsRemainStable(string hex, object expected)
    {
        Assert.Equal(expected, Describe(hex).HueFamily);
    }

    [Fact]
    public void NeutralBandsNarrowTowardLightnessExtremes()
    {
        var midGrayLimit = PerceptualColorClassifier.TintedNeutralLimit(0.50, 250);
        var darkLimit = PerceptualColorClassifier.TintedNeutralLimit(0.20, 250);
        var lightLimit = PerceptualColorClassifier.TintedNeutralLimit(0.80, 250);

        Assert.True(midGrayLimit > darkLimit);
        Assert.Equal(darkLimit, lightLimit, 12);
        Assert.Equal(PerceptualColorRole.Chromatic, PerceptualColorClassifier.ClassifyRole(
            new OklchColor(0.20, darkLimit, 250)));
        Assert.Equal(PerceptualColorRole.TintedNeutral, PerceptualColorClassifier.ClassifyRole(
            new OklchColor(0.50, darkLimit, 250)));
    }

    [Theory]
    [InlineData(0.199999, 0.054999, 250, PerceptualColorRole.NearBlack)]
    [InlineData(0.20, 0.054999, 250, PerceptualColorRole.Chromatic)]
    [InlineData(0.899999, 0.069999, 50, PerceptualColorRole.Chromatic)]
    [InlineData(0.90, 0.069999, 50, PerceptualColorRole.NearWhite)]
    [InlineData(0.50, 0.007999, 120, PerceptualColorRole.Neutral)]
    [InlineData(0.50, 0.008, 120, PerceptualColorRole.NearNeutral)]
    public void PerceptualRoleBoundariesAreDeterministic(
        double lightness,
        double chroma,
        double hue,
        object expected)
    {
        Assert.Equal(expected, PerceptualColorClassifier.ClassifyRole(new OklchColor(lightness, chroma, hue)));
    }

    [Theory]
    [InlineData(37.999, PerceptualHueFamily.Red)]
    [InlineData(38, PerceptualHueFamily.RedOrange)]
    [InlineData(47.999, PerceptualHueFamily.RedOrange)]
    [InlineData(48, PerceptualHueFamily.Orange)]
    [InlineData(189.999, PerceptualHueFamily.Turquoise)]
    [InlineData(190, PerceptualHueFamily.TurquoiseCyan)]
    [InlineData(204.999, PerceptualHueFamily.TurquoiseCyan)]
    [InlineData(205, PerceptualHueFamily.Cyan)]
    [InlineData(229.999, PerceptualHueFamily.Cyan)]
    [InlineData(230, PerceptualHueFamily.CyanBlue)]
    [InlineData(244.999, PerceptualHueFamily.CyanBlue)]
    [InlineData(245, PerceptualHueFamily.Blue)]
    [InlineData(269.999, PerceptualHueFamily.Blue)]
    [InlineData(270, PerceptualHueFamily.BlueViolet)]
    [InlineData(306.999, PerceptualHueFamily.BlueViolet)]
    [InlineData(307, PerceptualHueFamily.Violet)]
    [InlineData(321.999, PerceptualHueFamily.Violet)]
    [InlineData(322, PerceptualHueFamily.Magenta)]
    [InlineData(339.999, PerceptualHueFamily.Magenta)]
    [InlineData(340, PerceptualHueFamily.RedMagenta)]
    [InlineData(354.999, PerceptualHueFamily.RedMagenta)]
    [InlineData(355, PerceptualHueFamily.Red)]
    public void CompoundHueTransitionsAreDeterministic(double hue, object expected)
    {
        Assert.Equal(expected, PerceptualColorClassifier.ClassifyHue(new OklchColor(0.65, 0.24, hue)));
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
        Assert.Null(description.Role);
        Assert.Null(description.Undertone);
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