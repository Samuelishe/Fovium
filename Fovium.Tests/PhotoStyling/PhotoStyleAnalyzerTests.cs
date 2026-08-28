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
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

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
    public void DominantColorUsesMostPopulatedDeterministicQuantizedCluster()
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
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), analysis.AnalyzedSize);
        Assert.InRange(
            analysis.VisibleSampleCount,
            1,
            StageDefaults.PhotoStyleLongEdgePixels * StageDefaults.PhotoStyleLongEdgePixels);
        Assert.Equal(4, analysis.SpatialField.Columns);
        Assert.Equal(4, analysis.SpatialField.Rows);
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

        Assert.NotEqual(analysis.SpatialField[0, 0], analysis.SpatialField[3, 0]);
        Assert.NotEqual(analysis.SpatialField[0, 0], analysis.SpatialField[0, 3]);
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
    public void EqualDominantClustersUseStableLowestBinTieBreak()
    {
        using var decoded = CreateDecoded(
            2,
            1,
            (x, _) => x == 0 ? new SKColor(240, 16, 16) : new SKColor(16, 16, 240));
        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Equal(new StageColor(16, 16, 240), analysis.DominantColor);
        Assert.Equal(0.5, analysis.Palette[0].Weight, 10);
        Assert.Equal(0.5, analysis.Palette[1].Weight, 10);
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
