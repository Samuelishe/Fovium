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
        Assert.Equal(MatteColorSource.Custom, defaults.MatteColorSource);
        Assert.Equal(PhotoSeparationMode.None, defaults.PhotoSeparation);
        Assert.Equal(MatteStyle.Solid, defaults.MatteStyle);
        Assert.Equal(24, defaults.MatteWidthPhysicalPixels);
        var ambient = defaults.BackgroundAdjustments.Ambient;
        Assert.Equal(0.65, ambient.Brightness);
        Assert.Equal(0.85, ambient.Saturation);
        Assert.Equal(18, ambient.Blur);
        Assert.InRange(ambient.Brightness, 0.30, 1.00);
        Assert.InRange(ambient.Saturation, 0.00, 1.25);
        Assert.InRange(ambient.Blur, 8, 32);
        Assert.True(defaults.BackgroundAdjustments.Average.IsIdentity);
        Assert.True(defaults.BackgroundAdjustments.Dominant.IsIdentity);
        Assert.True(defaults.BackgroundAdjustments.ColorWash.IsIdentity);
        Assert.True(defaults.BackgroundAdjustments.ColorGradient.IsIdentity);
        Assert.True(defaults.BackgroundAdjustments.SoftGlow.IsIdentity);
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
            BackgroundAdjustments = StageSettings.Default.BackgroundAdjustments with
            {
                Ambient = StageSettings.Default.BackgroundAdjustments.Ambient with
                {
                    Brightness = brightness,
                    Saturation = saturation,
                    Blur = blur,
                },
            },
        }).Normalize();

        Assert.Equal(expectedBrightness, normalized.BackgroundAdjustments.Ambient.Brightness);
        Assert.Equal(expectedSaturation, normalized.BackgroundAdjustments.Ambient.Saturation);
        Assert.Equal(expectedBlur, normalized.BackgroundAdjustments.Ambient.Blur);
    }

    [Fact]
    public void NonFiniteAmbientValuesUseDefaults()
    {
        var normalized = (StageSettings.Default with
        {
            BackgroundAdjustments = StageSettings.Default.BackgroundAdjustments with
            {
                Ambient = StageSettings.Default.BackgroundAdjustments.Ambient with
                {
                    Brightness = double.NaN,
                    Saturation = double.PositiveInfinity,
                    Blur = double.NegativeInfinity,
                },
            },
        }).Normalize();

        Assert.Equal(
            StageDefaults.AmbientBrightness,
            normalized.BackgroundAdjustments.Ambient.Brightness);
        Assert.Equal(
            StageDefaults.AmbientSaturation,
            normalized.BackgroundAdjustments.Ambient.Saturation);
        Assert.Equal(
            StageDefaults.AmbientBlurSigmaPixels,
            normalized.BackgroundAdjustments.Ambient.Blur);
    }

    [Theory]
    [InlineData(-1, 3, 0.5, 2.0)]
    [InlineData(2, -1, 1.5, 0.0)]
    [InlineData(double.NaN, double.PositiveInfinity, 1.0, 1.0)]
    public void DerivedAdjustmentValuesNormalizeToPresentationBounds(
        double brightness,
        double saturation,
        double expectedBrightness,
        double expectedSaturation)
    {
        var normalized = (StageSettings.Default with
        {
            BackgroundAdjustments = StageSettings.Default.BackgroundAdjustments with
            {
                Average = new StageColorAdjustment
                {
                    Brightness = brightness,
                    Saturation = saturation,
                },
            },
        }).Normalize();

        Assert.Equal(expectedBrightness, normalized.BackgroundAdjustments.Average.Brightness);
        Assert.Equal(expectedSaturation, normalized.BackgroundAdjustments.Average.Saturation);
    }

    [Fact]
    public void EachGeneratedModeRemembersItsOwnAdjustment()
    {
        var adjustments = StageBackgroundAdjustments.Default
            .With(StageBackgroundMode.Average, new StageColorAdjustment
            {
                Brightness = 0.8,
                Saturation = 1.1,
            })
            .With(StageBackgroundMode.ColorGradient, new StageColorAdjustment
            {
                Brightness = 1.2,
                Saturation = 0.7,
            })
            .With(StageBackgroundMode.Ambient, new StageColorAdjustment
            {
                Brightness = 0.9,
                Saturation = 1.2,
            });

        Assert.Equal(new StageColorAdjustment { Brightness = 0.8, Saturation = 1.1 }, adjustments.Average);
        Assert.Equal(
            new StageColorAdjustment { Brightness = 1.2, Saturation = 0.7 },
            adjustments.ColorGradient);
        Assert.True(adjustments.Dominant.IsIdentity);
        Assert.True(adjustments.ColorWash.IsIdentity);
        Assert.True(adjustments.SoftGlow.IsIdentity);
        Assert.Equal(0.9, adjustments.Ambient.Brightness);
        Assert.Equal(1.2, adjustments.Ambient.Saturation);
        Assert.Equal(StageDefaults.AmbientBlurSigmaPixels, adjustments.Ambient.Blur);
    }

    [Theory]
    [InlineData((int)StageBackgroundMode.Average)]
    [InlineData((int)StageBackgroundMode.Dominant)]
    [InlineData((int)StageBackgroundMode.ColorWash)]
    [InlineData((int)StageBackgroundMode.ColorGradient)]
    [InlineData((int)StageBackgroundMode.SoftGlow)]
    public void UpdatingOnePhotoDerivedModeLeavesEveryOtherModeAtIdentity(int modeValue)
    {
        var mode = (StageBackgroundMode)modeValue;
        var expected = new StageColorAdjustment { Brightness = 0.8, Saturation = 1.2 };
        var updated = StageBackgroundAdjustments.Default.With(mode, expected);
        var generatedModes = new[]
        {
            StageBackgroundMode.Average,
            StageBackgroundMode.Dominant,
            StageBackgroundMode.ColorWash,
            StageBackgroundMode.ColorGradient,
            StageBackgroundMode.SoftGlow
        };

        foreach (var candidate in generatedModes)
        {
            Assert.Equal(candidate == mode ? expected : StageColorAdjustment.Identity, updated.For(candidate));
        }
    }

    [Theory]
    [InlineData((int)StageBackgroundMode.Black)]
    [InlineData((int)StageBackgroundMode.Neutral)]
    [InlineData((int)StageBackgroundMode.Custom)]
    public void ModesWithoutPresentationTuningResolveIdentity(int modeValue)
    {
        var adjustment = StageBackgroundAdjustments.Default.For((StageBackgroundMode)modeValue);

        Assert.True(adjustment.IsIdentity);
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

    [Fact]
    public void InvalidDerivedStylingEnumsUseSafeManualDefaults()
    {
        var normalized = (StageSettings.Default with
        {
            MatteColorSource = (MatteColorSource)999,
            PhotoSeparation = (PhotoSeparationMode)999,
        }).Normalize();

        Assert.Equal(MatteColorSource.Custom, normalized.MatteColorSource);
        Assert.Equal(PhotoSeparationMode.None, normalized.PhotoSeparation);
        Assert.False(normalized.RequiresPhotoStyleAnalysis());
    }

    [Theory]
    [InlineData((int)StageBackgroundMode.ColorGradient)]
    [InlineData((int)StageBackgroundMode.SoftGlow)]
    public void ExpressiveGradientModesRequireTheCanonicalPhotoAnalysis(int modeValue)
    {
        var stage = StageSettings.Default with
        {
            BackgroundMode = (StageBackgroundMode)modeValue,
        };

        Assert.True(stage.RequiresPhotoStyleAnalysis());
        Assert.False(stage.BackgroundMode.RequiresAmbient());
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