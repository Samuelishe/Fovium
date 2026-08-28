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
        StageColor boundary)
    {
        var colors = Enumerable.Repeat(average, 16).ToImmutableArray();
        return new PhotoStyleAnalysis(
            average,
            dominant,
            boundary,
            [new PhotoPaletteEntry(dominant, 1)],
            new PhotoColorField(4, 4, colors),
            new PixelSize(4, 4),
            16,
            TimeSpan.FromMilliseconds(1));
    }
}
