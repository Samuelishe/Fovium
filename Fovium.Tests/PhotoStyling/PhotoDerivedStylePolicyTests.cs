using System.Collections.Immutable;
using Fovium.PhotoStyling;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.PhotoStyling;

public sealed class PhotoDerivedStylePolicyTests
{
    [Fact]
    public void CustomMatteColorRemainsExactAndDoesNotRequireAnalysis()
    {
        var custom = new StageColor(250, 12, 218);
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColorSource = MatteColorSource.Custom,
            MatteColor = custom,
        };

        Assert.Equal(custom, PhotoDerivedStylePolicy.ResolveMatteColor(stage, null));
        Assert.False(stage.RequiresPhotoStyleAnalysis());
    }

    [Theory]
    [InlineData((int)MatteColorSource.Average)]
    [InlineData((int)MatteColorSource.Dominant)]
    public void AutoMatteToneIsDeterministicallyLightnessAndChromaBounded(int sourceValue)
    {
        var analysis = CreateAnalysis(
            average: new StageColor(255, 0, 255),
            dominant: new StageColor(0, 255, 0),
            boundary: new StageColor(255, 255, 255));
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColorSource = (MatteColorSource)sourceValue,
        };

        var first = PhotoDerivedStylePolicy.ResolveMatteColor(stage, analysis);
        var second = PhotoDerivedStylePolicy.ResolveMatteColor(stage, analysis);

        Assert.Equal(first, second);
        Assert.NotEqual(
            sourceValue == (int)MatteColorSource.Average
                ? analysis.AverageColor
                : analysis.DominantColor,
            first);
        Assert.True(stage.RequiresPhotoStyleAnalysis());
    }

    [Fact]
    public void MissingAnalysisUsesNeutralMatteAndOmitsHairline()
    {
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColorSource = MatteColorSource.Dominant,
            PhotoSeparation = PhotoSeparationMode.HairlineAuto,
        };

        Assert.Equal(
            new StageColor(46, 46, 46),
            PhotoDerivedStylePolicy.ResolveMatteColor(stage, null));
        Assert.Null(PhotoDerivedStylePolicy.ResolveHairline(stage, null, 1.5));
    }

    [Fact]
    public void HairlineChoosesMaximumBoundaryContrastAndOnePhysicalPixel()
    {
        var analysis = CreateAnalysis(
            average: new StageColor(24, 24, 24),
            dominant: new StageColor(24, 24, 24),
            boundary: new StageColor(245, 245, 245));
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColorSource = MatteColorSource.Average,
            PhotoSeparation = PhotoSeparationMode.HairlineAuto,
        };

        var result = Assert.IsType<HairlinePresentation>(
            PhotoDerivedStylePolicy.ResolveHairline(stage, analysis, 2));

        Assert.Equal(new StageColor(128, 128, 128), result.Color);
        Assert.Equal(StageDefaults.HairlineOpacity, result.Alpha);
        Assert.Equal(0.5, result.WidthDip);
    }

    [Fact]
    public void GeometryInputsDoNotParticipateInDerivedToneMapping()
    {
        var analysis = CreateAnalysis(
            average: new StageColor(90, 130, 180),
            dominant: new StageColor(30, 80, 140),
            boundary: new StageColor(60, 60, 60));
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColorSource = MatteColorSource.Average,
        };

        var expected = PhotoDerivedStylePolicy.ResolveMatteColor(stage, analysis);
        for (var index = 0; index < 50; index++)
        {
            _ = StageGeometry.CalculateRenderGeometry(
                stage,
                new RectD(10 + index, 20, 300, 200),
                null,
                new LogicalSize(800 + index, 600),
                1 + (index / 50d));
            Assert.Equal(expected, PhotoDerivedStylePolicy.ResolveMatteColor(stage, analysis));
        }
    }

    [Fact]
    public void ColorWashNormalizationIsDeterministicAndPresentationSafe()
    {
        var analysis = CreateAnalysis(
            average: new StageColor(255, 0, 255),
            dominant: new StageColor(0, 255, 0),
            boundary: new StageColor(0, 0, 0));

        var first = PhotoDerivedStylePolicy.ResolveWashField(analysis);
        var second = PhotoDerivedStylePolicy.ResolveWashField(analysis);

        Assert.True(first.Colors.SequenceEqual(second.Colors));
        Assert.All(first.Colors, color => Assert.NotEqual(analysis.AverageColor, color));
        Assert.All(first.Colors, color =>
        {
            Assert.InRange(color.Red, (byte)0, byte.MaxValue);
            Assert.InRange(color.Green, (byte)0, byte.MaxValue);
            Assert.InRange(color.Blue, (byte)0, byte.MaxValue);
        });
    }

    [Fact]
    public void ColorWashAppliesModestBoundedChromaGainWithoutChangingNeutralTone()
    {
        var chromatic = new StageColor(110, 80, 70);
        var neutral = new StageColor(96, 96, 96);

        var tunedChromatic = PhotoDerivedStylePolicy.NormalizeWashTone(chromatic);
        var tunedNeutral = PhotoDerivedStylePolicy.NormalizeWashTone(neutral);
        var sourceLab = PhotoStylingOklab.FromSrgb(chromatic);
        var tunedLab = PhotoStylingOklab.FromSrgb(tunedChromatic);
        var tunedSaturated = PhotoStylingOklab.FromSrgb(
            PhotoDerivedStylePolicy.NormalizeWashTone(new StageColor(255, 0, 255)));
        var tunedDark = PhotoStylingOklab.FromSrgb(
            PhotoDerivedStylePolicy.NormalizeWashTone(new StageColor(1, 1, 1)));
        var tunedBright = PhotoStylingOklab.FromSrgb(
            PhotoDerivedStylePolicy.NormalizeWashTone(new StageColor(254, 254, 254)));

        Assert.True(tunedLab.Chroma > sourceLab.Chroma);
        Assert.InRange(
            tunedSaturated.Chroma,
            0,
            0.162);
        Assert.InRange(tunedDark.L, 0.198, 0.22);
        Assert.InRange(tunedBright.L, 0.74, 0.762);
        Assert.Equal(1.18, PhotoDerivedStylePolicy.WashChromaGain);
        Assert.Equal(0.16, PhotoDerivedStylePolicy.WashMaximumChroma);
        Assert.Equal(0.20, PhotoDerivedStylePolicy.WashMinimumLightness);
        Assert.Equal(0.76, PhotoDerivedStylePolicy.WashMaximumLightness);
        Assert.Equal(neutral, tunedNeutral);
    }

    [Fact]
    public void DominantMatteNormalizesTheSameRepresentativeColorPublishedByAnalysis()
    {
        var representative = new StageColor(164, 82, 103);
        var analysis = CreateAnalysis(
            average: new StageColor(80, 80, 80),
            dominant: representative,
            boundary: new StageColor(50, 50, 50));
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColorSource = MatteColorSource.Dominant,
        };

        var expected = PhotoDerivedStylePolicy.NormalizeTone(
            representative,
            PhotoDerivedStylePolicy.MatteMinimumLightness,
            PhotoDerivedStylePolicy.MatteMaximumLightness,
            PhotoDerivedStylePolicy.MatteMaximumChroma);

        Assert.Equal(expected, PhotoDerivedStylePolicy.ResolveMatteColor(stage, analysis));
    }

    [Fact]
    public void ColorWashArtifactIsBoundedAndSmoothlyInterpolatesSpatialField()
    {
        var colors = Enumerable.Repeat(new StageColor(24, 48, 72), 16).ToArray();
        colors[0] = new StageColor(230, 30, 30);
        colors[1] = new StageColor(30, 80, 230);
        var analysis = new PhotoStyleAnalysis(
            new StageColor(80, 80, 80),
            new StageColor(80, 80, 80),
            new StageColor(80, 80, 80),
            [new PhotoPaletteEntry(new StageColor(80, 80, 80), 1)],
            new PhotoColorField(4, 4, colors.ToImmutableArray()),
            new PixelSize(4, 4),
            16,
            TimeSpan.Zero);

        using var image = PhotoDerivedStylePolicy.CreateColorWashImage(analysis);
        using var bitmap = SKBitmap.FromImage(image);

        Assert.Equal(StageDefaults.PhotoStyleWashRasterPixels, image.Width);
        Assert.Equal(StageDefaults.PhotoStyleWashRasterPixels, image.Height);
        Assert.NotEqual(bitmap.GetPixel(0, 0), bitmap.GetPixel(15, 0));
        Assert.NotEqual(bitmap.GetPixel(15, 0), bitmap.GetPixel(31, 0));
    }

    [Fact]
    public void LinearGradientSelectsTheStrongestLowFrequencySpatialAxisDeterministically()
    {
        var horizontalField = CreateField((column, _) => PhotoStylingOklab.Lerp(
            PhotoStylingOklab.FromSrgb(new StageColor(210, 70, 40)),
            PhotoStylingOklab.FromSrgb(new StageColor(30, 90, 210)),
            column / 5d).ToSrgb());
        var analysis = CreateAnalysisWithField(
            new StageColor(100, 90, 100),
            new StageColor(170, 70, 80),
            new StageColor(60, 70, 80),
            horizontalField);

        var first = PhotoDerivedStylePolicy.ResolveLinearGradient(analysis);
        var second = PhotoDerivedStylePolicy.ResolveLinearGradient(analysis);

        Assert.Equal(first, second);
        Assert.Equal(PhotoGradientAxis.Horizontal, first.Axis);
        Assert.NotEqual(first.Start, first.End);
        Assert.NotEqual(first.Start, first.Middle);
        Assert.NotEqual(first.Middle, first.End);
    }

    [Fact]
    public void LinearGradientCanSelectVerticalEvidenceWithoutViewportInputs()
    {
        var verticalField = CreateField((_, row) => PhotoStylingOklab.Lerp(
            PhotoStylingOklab.FromSrgb(new StageColor(30, 150, 70)),
            PhotoStylingOklab.FromSrgb(new StageColor(130, 70, 30)),
            row / 5d).ToSrgb());
        var analysis = CreateAnalysisWithField(
            new StageColor(80, 100, 70),
            new StageColor(50, 140, 70),
            new StageColor(60, 60, 50),
            verticalField);

        var expected = PhotoDerivedStylePolicy.ResolveLinearGradient(analysis);
        for (var index = 0; index < 20; index++)
        {
            _ = StageGeometry.CalculateRenderGeometry(
                StageSettings.Default,
                new RectD(index, index * 2, 300 + index, 200 + index),
                null,
                new LogicalSize(640 + index, 480 + index),
                1 + (index * 0.05));
            Assert.Equal(expected, PhotoDerivedStylePolicy.ResolveLinearGradient(analysis));
        }

        Assert.Equal(PhotoGradientAxis.Vertical, expected.Axis);
    }

    [Fact]
    public void RadialGlowUsesRepresentativeCoreAndBoundaryWithoutSubjectPositionInference()
    {
        var analysis = CreateAnalysis(
            average: new StageColor(90, 110, 140),
            dominant: new StageColor(220, 80, 45),
            boundary: new StageColor(20, 45, 80));

        var first = PhotoDerivedStylePolicy.ResolveRadialGlow(analysis);
        var second = PhotoDerivedStylePolicy.ResolveRadialGlow(analysis);

        Assert.Equal(first, second);
        Assert.NotEqual(first.Center, first.Edge);
        Assert.NotEqual(first.Middle, first.Edge);
    }

    [Fact]
    public void UniformNeutralAnalysisProducesNeutralStopsAndARestrainedGlowWithoutInventedHue()
    {
        var neutral = new StageColor(112, 112, 112);
        var analysis = CreateAnalysis(neutral, neutral, neutral);

        var linear = PhotoDerivedStylePolicy.ResolveLinearGradient(analysis);
        var radial = PhotoDerivedStylePolicy.ResolveRadialGlow(analysis);

        Assert.Equal(linear.Start.Red, linear.Start.Green);
        Assert.Equal(linear.Start.Green, linear.Start.Blue);
        Assert.Equal(linear.Start, linear.Middle);
        Assert.Equal(linear.Middle, linear.End);
        Assert.Equal(radial.Center.Red, radial.Center.Green);
        Assert.Equal(radial.Center.Green, radial.Center.Blue);
        Assert.Equal(radial.Middle.Red, radial.Middle.Green);
        Assert.Equal(radial.Middle.Green, radial.Middle.Blue);
        Assert.Equal(radial.Edge.Red, radial.Edge.Green);
        Assert.Equal(radial.Edge.Green, radial.Edge.Blue);
        var center = PhotoStylingOklab.FromSrgb(radial.Center);
        var edge = PhotoStylingOklab.FromSrgb(radial.Edge);
        Assert.True(center.L > edge.L);
        Assert.InRange(center.L - edge.L, 0.12, 0.16);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(255, 0, 255)]
    public void ExpressiveGradientStopsRemainWithinPresentationToneBounds(
        byte red,
        byte green,
        byte blue)
    {
        var source = new StageColor(red, green, blue);
        var analysis = CreateAnalysis(source, source, source);
        var linear = PhotoDerivedStylePolicy.ResolveLinearGradient(analysis);
        var radial = PhotoDerivedStylePolicy.ResolveRadialGlow(analysis);
        StageColor[] stops =
        [
            linear.Start,
            linear.Middle,
            linear.End,
            radial.Center,
            radial.Middle,
            radial.Edge,
        ];

        Assert.All(stops, stop =>
        {
            var lab = PhotoStylingOklab.FromSrgb(stop);
            Assert.InRange(lab.L, 0.178, 0.782);
            Assert.InRange(lab.Chroma, 0, 0.142);
        });
    }

    [Fact]
    public void HairlineNoneAndDisabledMatteNeverPublishSeparation()
    {
        var analysis = CreateAnalysis(
            average: new StageColor(220, 220, 220),
            dominant: new StageColor(220, 220, 220),
            boundary: new StageColor(220, 220, 220));

        Assert.Null(PhotoDerivedStylePolicy.ResolveHairline(
            StageSettings.Default with
            {
                MatteEnabled = true,
                PhotoSeparation = PhotoSeparationMode.None,
            },
            analysis,
            1));
        Assert.Null(PhotoDerivedStylePolicy.ResolveHairline(
            StageSettings.Default with
            {
                MatteEnabled = false,
                PhotoSeparation = PhotoSeparationMode.HairlineAuto,
            },
            analysis,
            1));
    }

    internal static PhotoStyleAnalysis CreateAnalysis(
        StageColor average,
        StageColor dominant,
        StageColor boundary,
        StageColor? notable = null)
    {
        var colors = Enumerable.Repeat(
                average,
                StageDefaults.PhotoStyleFieldColumns * StageDefaults.PhotoStyleFieldRows)
            .ToImmutableArray();
        return new PhotoStyleAnalysis(
            average,
            dominant,
            boundary,
            [new PhotoPaletteEntry(dominant, 1)],
            new PhotoColorField(
                StageDefaults.PhotoStyleFieldColumns,
                StageDefaults.PhotoStyleFieldRows,
                colors),
            new PixelSize(4, 4),
            16,
            TimeSpan.FromMilliseconds(1),
            notable is { } color
                ? [new PhotoNotableColor(color, 0.12, 0.10, 0.11, 0.75)]
                : []);
    }

    private static ImmutableArray<StageColor> CreateField(
        Func<int, int, StageColor> createColor) =>
        Enumerable.Range(0, StageDefaults.PhotoStyleFieldRows)
            .SelectMany(row => Enumerable.Range(0, StageDefaults.PhotoStyleFieldColumns)
                .Select(column => createColor(column, row)))
            .ToImmutableArray();

    private static PhotoStyleAnalysis CreateAnalysisWithField(
        StageColor average,
        StageColor dominant,
        StageColor boundary,
        ImmutableArray<StageColor> field) =>
        new(
            average,
            dominant,
            boundary,
            [new PhotoPaletteEntry(dominant, 1)],
            new PhotoColorField(
                StageDefaults.PhotoStyleFieldColumns,
                StageDefaults.PhotoStyleFieldRows,
                field),
            new PixelSize(6, 6),
            36,
            TimeSpan.FromMilliseconds(1));
}