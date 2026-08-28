using Fovium.Imaging;
using Fovium.PhotoStyling;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.PhotoStyling;

public sealed class PhotoStyleAnalyzerTests
{
    [Fact]
    public void SolidReferenceSrgbImageProducesExactAverageDominantPaletteAndField()
    {
        var color = new SKColor(32, 96, 224);
        using var decoded = CreateDecoded(40, 20, (_, _) => color);
        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(
            decoded,
            CancellationToken.None);
        var analysis = result.Analysis;

        var expected = new StageColor(color.Red, color.Green, color.Blue);
        Assert.Equal(expected, analysis.AverageColor);
        Assert.Equal(expected, analysis.DominantColor);
        Assert.Equal(expected, analysis.BoundaryColor);
        Assert.Single(analysis.Palette);
        Assert.Equal(1, analysis.Palette[0].Weight, 10);
        Assert.All(analysis.SpatialField.Colors, actual => Assert.Equal(expected, actual));
        Assert.Equal(800, analysis.VisibleSampleCount);
    }

    [Fact]
    public void RepresentativeDominantIsDeterministicForChromaticMajority()
    {
        using var decoded = CreateDecoded(
            4,
            1,
            (x, _) => x < 3 ? new SKColor(240, 24, 16) : new SKColor(16, 32, 240));
        var analyzer = new PhotoStyleAnalyzer();
        var first = analyzer.Analyze(decoded, CancellationToken.None);
        var second = analyzer.Analyze(decoded, CancellationToken.None);

        Assert.Equal(new StageColor(240, 24, 16), first.DominantColor);
        Assert.Equal(first.AverageColor, second.AverageColor);
        Assert.Equal(first.DominantColor, second.DominantColor);
        Assert.Equal(first.BoundaryColor, second.BoundaryColor);
        Assert.True(first.Palette.SequenceEqual(second.Palette));
        Assert.True(first.SpatialField.Colors.SequenceEqual(second.SpatialField.Colors));
    }

    [Theory]
    [InlineData(6000, 4000, 96, 64)]
    [InlineData(4000, 6000, 64, 96)]
    public void AnalysisIsBoundedToNinetySixPixelLongEdge(
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        using var decoded = CreateSolidDecoded(width, height, SKColors.SteelBlue);
        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(
            decoded,
            CancellationToken.None);
        var analysis = result.Analysis;

        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), analysis.AnalyzedSize);
        Assert.InRange(
            analysis.VisibleSampleCount,
            1,
            StageDefaults.PhotoStyleLongEdgePixels * StageDefaults.PhotoStyleLongEdgePixels);
        Assert.Equal(6, analysis.SpatialField.Columns);
        Assert.Equal(6, analysis.SpatialField.Rows);
        Assert.Equal(5, StageDefaults.PhotoStylePaletteSize);
    }

    [Fact]
    public void SpatialFieldRetainsImageDistributionInsteadOfCollapsingToAverage()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x < 48
                ? (y < 32 ? SKColors.OrangeRed : SKColors.Gold)
                : (y < 32 ? SKColors.DeepSkyBlue : SKColors.ForestGreen));
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.NotEqual(analysis.SpatialField[0, 0], analysis.SpatialField[5, 0]);
        Assert.NotEqual(analysis.SpatialField[0, 0], analysis.SpatialField[0, 5]);
        using var wash = PhotoDerivedStylePolicy.CreateColorWashImage(analysis);
        Assert.Equal(StageDefaults.PhotoStyleWashRasterPixels, wash.Width);
        Assert.Equal(StageDefaults.PhotoStyleWashRasterPixels, wash.Height);
    }

    [Fact]
    public void AverageUsesLinearLightAndIgnoresFullyTransparentPixels()
    {
        using var decoded = CreateDecoded(
            3,
            1,
            (x, _) => x switch
            {
                0 => SKColors.Black,
                1 => SKColors.White,
                _ => new SKColor(255, 0, 255, 0),
            });
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Equal(new StageColor(188, 188, 188), analysis.AverageColor);
        Assert.Equal(2, analysis.VisibleSampleCount);
        Assert.DoesNotContain(
            analysis.Palette,
            entry => entry.Color == new StageColor(255, 0, 255));
    }

    [Fact]
    public void EqualPopulationChromaticClustersProduceStableRepresentativeWinner()
    {
        using var decoded = CreateDecoded(
            2,
            1,
            (x, _) => x == 0 ? new SKColor(240, 16, 16) : new SKColor(16, 16, 240));
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        var repeated = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Equal(analysis.DominantColor, repeated.DominantColor);
        Assert.Equal(0.5, analysis.Palette[0].Weight, 10);
        Assert.Equal(0.5, analysis.Palette[1].Weight, 10);
    }

    [Fact]
    public void SubstantialRedRegionDefeatsLargerDarkNeutralClusterWithoutChangingRawPalette()
    {
        using var decoded = CreateDecoded(
            96,
            1,
            (x, _) => x switch
            {
                < 44 => new SKColor(35, 35, 38),
                < 78 => new SKColor(205, 35, 45),
                _ => new SKColor(100, 105, 110),
            });
        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(
            decoded,
            CancellationToken.None);
        var analysis = result.Analysis;

        Assert.Equal(new StageColor(35, 35, 38), analysis.Palette[0].Color);
        Assert.Equal(44d / 96, analysis.Palette[0].Weight, 10);
        Assert.NotEqual(analysis.Palette[0].Color, analysis.DominantColor);
        Assert.True(analysis.DominantColor.Red > 175);
        Assert.True(analysis.DominantColor.Red > analysis.DominantColor.Green * 3);
        Assert.True(analysis.DominantColor.Red > analysis.DominantColor.Blue * 3);
        Assert.Equal(new StageColor(35, 35, 38), result.Diagnostics.RawLargestColor);
        Assert.Equal(44d / 96, result.Diagnostics.RawLargestPopulation, 10);
        Assert.InRange(result.Diagnostics.RepresentativePopulation, 0.30, 0.40);
        Assert.InRange(result.Diagnostics.RepresentativeLightness, 0.40, 0.55);
        Assert.True(result.Diagnostics.RepresentativeChroma > 0.15);
        Assert.True(result.Diagnostics.RawLargestDiffers);
    }

    [Fact]
    public void TinySaturatedAccentCannotHijackNeutralRepresentative()
    {
        using var decoded = CreateDecoded(
            96,
            1,
            (x, _) => x switch
            {
                < 82 => new SKColor(115, 118, 120),
                < 87 => new SKColor(230, 25, 35),
                _ => new SKColor(80, 82, 85),
            });
        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(
            decoded,
            CancellationToken.None);
        var analysis = result.Analysis;

        Assert.Equal(new StageColor(115, 118, 120), analysis.Palette[0].Color);
        Assert.InRange(
            Math.Max(analysis.DominantColor.Red, Math.Max(
                analysis.DominantColor.Green,
                analysis.DominantColor.Blue)) -
            Math.Min(analysis.DominantColor.Red, Math.Min(
                analysis.DominantColor.Green,
                analysis.DominantColor.Blue)),
            0,
            8);
        Assert.True(analysis.DominantColor.Red < 150);
        Assert.Equal(new StageColor(115, 118, 120), result.Diagnostics.RawLargestColor);
        Assert.Equal(82d / 96, result.Diagnostics.RawLargestPopulation, 10);
        Assert.True(result.Diagnostics.RepresentativePopulation > 0.80);
        Assert.InRange(result.Diagnostics.RepresentativeChroma, 0, 0.02);
        Assert.True(result.Diagnostics.RawLargestDiffers);
    }

    [Fact]
    public void GrayscaleGradientKeepsNeutralRepresentative()
    {
        using var decoded = CreateDecoded(
            96,
            8,
            (x, _) =>
            {
                var value = (byte)(24 + (208d * x / 95));
                return new SKColor(value, value, value);
            });
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Equal(analysis.DominantColor.Red, analysis.DominantColor.Green);
        Assert.Equal(analysis.DominantColor.Green, analysis.DominantColor.Blue);
    }

    [Fact]
    public void MostlyBlackNightImageKeepsDarkRepresentativeDespiteTinyColoredLights()
    {
        using var decoded = CreateDecoded(
            96,
            1,
            (x, _) => x switch
            {
                < 86 => new SKColor(5, 7, 10),
                < 91 => new SKColor(20, 35, 65),
                _ => new SKColor(230, 170, 35),
            });
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.InRange(analysis.DominantColor.Red, (byte)0, (byte)20);
        Assert.InRange(analysis.DominantColor.Green, (byte)0, (byte)20);
        Assert.InRange(analysis.DominantColor.Blue, (byte)0, (byte)25);
    }

    [Fact]
    public void HighKeyImageMayKeepVeryLightNeutralRepresentative()
    {
        using var decoded = CreateDecoded(
            96,
            1,
            (x, _) => x switch
            {
                < 85 => new SKColor(244, 243, 240),
                < 93 => new SKColor(220, 224, 230),
                _ => new SKColor(235, 185, 170),
            });
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.InRange(analysis.DominantColor.Red, (byte)225, byte.MaxValue);
        Assert.InRange(analysis.DominantColor.Green, (byte)225, byte.MaxValue);
        Assert.InRange(analysis.DominantColor.Blue, (byte)225, byte.MaxValue);
    }

    private static DecodedImage CreateDecoded(
        int width,
        int height,
        Func<int, int, SKColor> pixel)
    {
        var info = new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, pixel(x, y));
            }
        }

        var image = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(width, height);
        var retained = checked((long)width * height * 4);
        return new DecodedImage(
            [1, 2, 3],
            new ImageDescriptor(
                "analysis.png",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.AssumedSrgb,
                false,
                "Bgra8888/Premul",
                retained,
                retained,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            image);
    }

    private static DecodedImage CreateSolidDecoded(int width, int height, SKColor color)
    {
        var info = new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        bitmap.Erase(color);
        var image = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(width, height);
        var retained = checked((long)width * height * 4);
        return new DecodedImage(
            [1, 2, 3],
            new ImageDescriptor(
                "bounded-analysis.png",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.AssumedSrgb,
                false,
                "Bgra8888/Premul",
                retained,
                retained,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            image);
    }
}
