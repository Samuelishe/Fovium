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
        Assert.Empty(analysis.NotableColors);
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
    public void DiagnosticsCanCompareResolutionWithoutChangingTheProductionContract()
    {
        using var decoded = CreateDecoded(
            600,
            400,
            (x, y) => new SKColor((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256)));
        var analyzer = new PhotoStyleAnalyzer();

        var production = analyzer.Analyze(decoded, CancellationToken.None);
        var explicitNinetySix = PhotoStyleAnalyzer.AnalyzeWithDiagnostics(
            decoded,
            StageDefaults.PhotoStyleLongEdgePixels,
            CancellationToken.None).Analysis;
        var experimental = PhotoStyleAnalyzer.AnalyzeWithDiagnostics(
            decoded,
            160,
            CancellationToken.None).Analysis;

        Assert.Equal(new PixelSize(96, 64), production.AnalyzedSize);
        Assert.Equal(production.AverageColor, explicitNinetySix.AverageColor);
        Assert.Equal(production.DominantColor, explicitNinetySix.DominantColor);
        Assert.Equal(production.BoundaryColor, explicitNinetySix.BoundaryColor);
        Assert.True(production.Palette.SequenceEqual(explicitNinetySix.Palette));
        Assert.True(production.SpatialField.Colors.SequenceEqual(explicitNinetySix.SpatialField.Colors));
        Assert.True(production.NotableColors.SequenceEqual(explicitNinetySix.NotableColors));
        Assert.Equal(new PixelSize(160, 107), experimental.AnalyzedSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(513)]
    public void DiagnosticsRejectUnboundedResolution(int longEdgePixels)
    {
        using var decoded = CreateSolidDecoded(96, 64, SKColors.SteelBlue);

        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoStyleAnalyzer.AnalyzeWithDiagnostics(
            decoded,
            longEdgePixels,
            CancellationToken.None));
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

    [Fact]
    public void CoherentWarmMinoritySurfacesAsNotableWithoutBecomingFrequent()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 34 and < 62 && y is >= 18 and < 46
                ? WarmTone((x + y) % 6)
                : GreenTone(((x / 8) + (y / 8)) % 6));

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.All(analysis.Palette, entry => Assert.True(entry.Color.Green > entry.Color.Red));
        var warm = Assert.Single(analysis.NotableColors.Where(entry =>
            entry.Color.Red > entry.Color.Green + 35 && entry.Color.Green > entry.Color.Blue));
        Assert.Single(analysis.NotableColors);
        Assert.InRange(warm.SupportFraction, 0.09, 0.16);
        Assert.True(warm.LargestComponentFraction > 0.08);
    }

    [Fact]
    public void AdjacentWarmBinsConsolidateIntoOneNotableColor()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 30 and < 66 && y is >= 20 and < 44
                ? WideWarmShadeTone((x + y) % 2)
                : GreenTone(((x / 8) + (y / 8)) % 6));

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Single(analysis.NotableColors.Where(entry =>
            entry.Color.Red > entry.Color.Green + 35 && entry.Color.Green > entry.Color.Blue));
    }

    [Fact]
    public void MutedWarmRegionRemainsDistinctFromNearbyNeutralCandidate()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x switch
            {
                < 48 => GreenTone((x / 8 + y / 8) % 6),
                < 65 => new SKColor(160, 167, 177),
                < 79 => WarmMutedTone((x + y) % 4),
                _ => new SKColor(151, 174, 75)
            });

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Contains(analysis.NotableColors, entry =>
            entry.Color.Red > entry.Color.Green + 20 &&
            entry.Color.Green > entry.Color.Blue + 15);
    }

    [Fact]
    public void AchromaticCandidatesRemainBoundedAndDoNotCrowdOutCoherentChromaticAccent()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x switch
            {
                < 35 => new SKColor(55, 125, 185),
                < 55 => new SKColor(18, 21, 24),
                < 75 => new SKColor(90, 94, 98),
                < 90 => new SKColor(175, 177, 180),
                _ => WarmTone((x + y) % 6)
            });

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Contains(analysis.NotableColors, entry =>
            entry.Color.Red > entry.Color.Green + 35 &&
            entry.Color.Green > entry.Color.Blue);
        var achromatic = analysis.NotableColors
            .Where(entry => PhotoStylingOklab.FromSrgb(entry.Color).Chroma <= 0.04)
            .Select(entry => PhotoStylingOklab.FromSrgb(entry.Color))
            .OrderBy(entry => entry.L)
            .ToArray();
        Assert.InRange(achromatic.Length, 0, 2);
        if (achromatic.Length == 2)
        {
            Assert.True(achromatic[1].L - achromatic[0].L >= 0.24);
        }
    }

    [Fact]
    public void RepeatedSmallFlowersCanSurfaceAsOneNotableColor()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x % 16 is >= 2 and <= 4 && y % 16 is >= 2 and <= 4
                ? new SKColor(245, 205, 42)
                : GreenTone(((x / 8) + (y / 8)) % 6));

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        var flower = Assert.Single(analysis.NotableColors.Where(entry =>
            entry.Color.Red > 200 && entry.Color.Green > 160 && entry.Color.Blue < 90));
        Assert.InRange(flower.SupportFraction, 0.025, 0.045);
        Assert.True(flower.CoherentSupportFraction >= 0.02);
    }

    [Fact]
    public void LargeCoherentMutedWarmObjectUsesSecondaryMassAdmission()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 33 and < 61 && y is >= 18 and < 47
                ? WarmMutedTone((x + y) % 4)
                : GreenTone(((x / 7) + (y / 7)) % 6));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var warm = Assert.Single(result.Analysis.NotableColors.Where(IsWarm));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, warm.Color) < 0.04));
        Assert.True(candidate.Selected);
        Assert.True(candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.SubstantialCoherentMass) ||
                    candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.MutedDistinctMass));
        Assert.True(candidate.SupportFraction > 0.08);
        Assert.True(candidate.LargestComponentFraction > 0.05);
    }

    [Fact]
    public void CompactSaturatedRedComponentUsesChromaticAccentAdmission()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 45 and < 52 && y is >= 26 and < 33
                ? new SKColor(238, 28, 35)
                : NeutralSceneTone((x / 12 + y / 8) % 6));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var red = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Red > 190 && entry.Color.Red > entry.Color.Green * 2));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, red.Color) < 0.04));
        Assert.True(
            candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.CompactChromaticAccent),
            $"support={candidate.SupportFraction:F4}; largest={candidate.LargestComponentFraction:F4}; " +
            $"chroma={candidate.Chroma:F4}; novelty={candidate.GlobalNovelty:F4}; " +
            $"local={candidate.LocalContrast:F4}; reason={candidate.RejectionReason}");
        Assert.InRange(candidate.SupportFraction, 0.005, 0.012);
        Assert.True(candidate.LocalContrast > 0.20);
    }

    [Fact]
    public void LargeWhiteRegionOnDarkSurroundUsesNeutralLightnessAdmission()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 35 and < 59 && y is >= 17 and < 45
                ? new SKColor(238, 236, 228)
                : new SKColor(
                    (byte)(14 + ((x + y) % 4)),
                    (byte)(15 + ((x + y) % 4)),
                    (byte)(18 + ((x + y) % 4))));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var light = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Red > 220 && entry.Color.Green > 220 && entry.Color.Blue > 210));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, light.Color) < 0.04));
        Assert.True(candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.LightnessContrastNeutral));
        Assert.True(candidate.LocalLightnessContrast > 0.40);
        Assert.True(candidate.LargestComponentFraction > 0.08);
    }

    [Fact]
    public void LargeCoherentWarmLowChromaSubjectCanSurface()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 32 and < 64 && y is >= 12 and < 54
                ? new SKColor(145, 132, 124)
                : new SKColor(
                    (byte)(205 + ((x / 8 + y / 8) % 4) * 5),
                    (byte)(201 + ((x / 8 + y / 8) % 4) * 5),
                    (byte)(190 + ((x / 8 + y / 8) % 4) * 4)));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var subject = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Red is >= 125 and <= 165 &&
            entry.Color.Green is >= 115 and <= 150 &&
            entry.Color.Blue is >= 105 and <= 140));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, subject.Color) < 0.04));
        Assert.True(candidate.SupportFraction > 0.18);
        Assert.True(candidate.LargestComponentFraction > 0.15);
        Assert.True(candidate.Chroma < 0.04);
        Assert.True(candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.CoherentNeutralStructure));
    }

    [Fact]
    public void LargeLightNeutralStructureWithModerateBoundaryContrastCanSurface()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 8 and < 36 && y is >= 18 and < 48
                ? new SKColor(225, 226, 222)
                : new SKColor(
                    (byte)(115 + ((x / 12 + y / 9) % 4) * 5),
                    (byte)(151 + ((x / 12 + y / 9) % 4) * 4),
                    (byte)(177 + ((x / 12 + y / 9) % 4) * 5)));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var structure = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Red > 205 && entry.Color.Green > 205 && entry.Color.Blue > 200));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, structure.Color) < 0.04));
        Assert.True(candidate.SupportFraction > 0.10);
        Assert.True(candidate.LocalLightnessContrast > 0.12);
        Assert.True(candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.CoherentNeutralStructure));
    }

    [Fact]
    public void LargeCoherentFrequentShadeCanStillAddStructuralInformation()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => y >= 47
                ? new SKColor(73, 83, 98)
                : y < 18
                    ? new SKColor(213, 222, 225)
                    : GreenTone((x / 8 + y / 7) % 6));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        Assert.Contains(result.Analysis.Palette, entry =>
            entry.Color.Blue > entry.Color.Red + 15 && entry.Color.Blue > entry.Color.Green + 5);
        var structural = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Blue > entry.Color.Red + 15 && entry.Color.Blue > entry.Color.Green + 5));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, structural.Color) < 0.04));
        Assert.True(candidate.FrequentOverlap > 0.10);
        Assert.True(candidate.LargestComponentFraction > 0.15);
    }

    [Fact]
    public void MediumCoherentFrequentShadeCanStillAddStructuralInformation()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 58 and < 77 && y is >= 23 and < 41
                ? new SKColor(140, 100, 75)
                : new SKColor(
                    (byte)(54 + ((x / 7 + y / 5) % 7) * 7),
                    (byte)(91 + ((x / 9 + y / 6) % 6) * 9),
                    (byte)(65 + ((x / 5 + y / 8) % 7) * 6)));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        Assert.Contains(result.Analysis.Palette, entry =>
            entry.Color.Red > entry.Color.Green + 35 && entry.Color.Green > entry.Color.Blue + 20);
        var structural = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Red > entry.Color.Green + 35 && entry.Color.Green > entry.Color.Blue + 20));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, structural.Color) < 0.04));
        Assert.InRange(candidate.SupportFraction, 0.04, 0.08);
        Assert.True(candidate.LargestComponentFraction >= candidate.SupportFraction * 0.80);
    }

    [Fact]
    public void StronglySeparatedDarkAndLightNeutralStructuresCanBothSurface()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x < 20
                ? new SKColor(18, 20, 23)
                : x >= 76
                    ? new SKColor(232, 233, 230)
                    : new SKColor(84, 132, 154));

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);
        var achromatic = analysis.NotableColors
            .Where(entry => PhotoStylingOklab.FromSrgb(entry.Color).Chroma < 0.025)
            .OrderBy(entry => PhotoStylingOklab.FromSrgb(entry.Color).L)
            .ToArray();

        Assert.Equal(2, achromatic.Length);
        Assert.True(
            PhotoStylingOklab.FromSrgb(achromatic[1].Color).L -
            PhotoStylingOklab.FromSrgb(achromatic[0].Color).L > 0.50);
    }

    [Fact]
    public void RepeatedBrickStripesUseDistributedStructureAdmission()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 18 and < 78 && (y is >= 9 and < 12 || y is >= 23 and < 26 ||
                                              y is >= 37 and < 40 || y is >= 51 and < 54)
                ? new SKColor(154, 64, 48)
                : new SKColor((byte)(151 + ((x / 9 + y / 7) % 5) * 7),
                    (byte)(153 + ((x / 9 + y / 7) % 5) * 7),
                    (byte)(150 + ((x / 9 + y / 7) % 5) * 7)));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var brick = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Red > entry.Color.Green + 55 && entry.Color.Green > entry.Color.Blue));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, brick.Color) < 0.04));
        Assert.True(candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.DistributedRepeatedStructure));
        Assert.True(candidate.ComponentCount >= 4);
        Assert.True(candidate.TopComponentSupportFraction > candidate.LargestComponentFraction * 2.5);
        Assert.True(candidate.SpatialCellOccupancy >= 8);
    }

    [Fact]
    public void MediumMustardRegionSurvivesAgainstBurgundyBackground()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 51 and < 70 && y is >= 20 and < 44
                ? new SKColor(202, 157, 43)
                : new SKColor((byte)(72 + ((x + y) % 5) * 5), 27, (byte)(40 + ((x + y) % 4) * 4)));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var mustard = Assert.Single(result.Analysis.NotableColors.Where(entry =>
            entry.Color.Red > 170 && entry.Color.Green > 115 && entry.Color.Blue < 80));
        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            ColorDistance(entry.Color, mustard.Color) < 0.04));
        Assert.True(candidate.Selected);
        Assert.True(candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.SubstantialCoherentMass));
        Assert.True(candidate.RankingScore > 0.40);
    }

    [Fact]
    public void DownsampledMustardPatchUsesModerateChromaticAccentEvidence()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 48 and < 54 && y is >= 25 and < 31
                ? new SKColor(149, 121, 53)
                : x is >= 46 and < 56 && y is >= 23 and < 33
                    ? new SKColor(48, 51, 56)
                    : NeutralSceneTone((x / 8 + y / 7) % 6));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        var candidate = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            entry.Color.Red > entry.Color.Green + 20 && entry.Color.Green > entry.Color.Blue + 40));
        Assert.True(
            candidate.AdmissionPaths.HasFlag(NotableColorAdmissionPath.CompactChromaticAccent),
            $"support={candidate.SupportFraction:F4}; largest={candidate.LargestComponentFraction:F4}; " +
            $"chroma={candidate.Chroma:F4}; novelty={candidate.GlobalNovelty:F4}; " +
            $"local={candidate.LocalContrast:F4}; reason={candidate.RejectionReason}");
        Assert.InRange(candidate.SupportFraction, 0.0045, 0.008);
        Assert.True(candidate.LocalContrast >= 0.145);
        Assert.True(candidate.Selected,
            $"presentation={candidate.PresentationScore:F3}; reason={candidate.SelectionReason}");
    }

    [Fact]
    public void CrowdedNeutralSceneAddsOrangeAndBlueInformation()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 13 and < 25 && y is >= 18 and < 32
                ? new SKColor(232, 91, 25)
                : x is >= 69 and < 81 && y is >= 34 and < 48
                    ? new SKColor(38, 112, 205)
                    : NeutralSceneTone((x / 8 + y / 8) % 6));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        Assert.Contains(result.Analysis.NotableColors, entry =>
            entry.Color.Red > entry.Color.Green * 1.8 && entry.Color.Green > entry.Color.Blue);
        Assert.Contains(result.Analysis.NotableColors, entry =>
            entry.Color.Blue > entry.Color.Red * 1.8 && entry.Color.Blue > entry.Color.Green * 1.3);
        Assert.DoesNotContain(result.Analysis.NotableColors, entry =>
            Math.Abs(entry.Color.Red - entry.Color.Green) < 12 &&
            Math.Abs(entry.Color.Green - entry.Color.Blue) < 12);
    }

    [Fact]
    public void StrongColorAlreadyExplainedByFrequentPaletteIsNotRepeatedAsNotable()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 45 and < 51 && y is >= 27 and < 33
                ? new SKColor(149, 121, 53)
                : x is >= 43 and < 53 && y is >= 25 and < 35
                    ? NeutralSceneTone(0)
                    : NeutralSceneTone(Math.Min(2, x / 32) + 2));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        Assert.Contains(result.Analysis.Palette, entry =>
            entry.Color.Red > entry.Color.Green + 20 && entry.Color.Green > entry.Color.Blue + 40);
        Assert.DoesNotContain(result.Analysis.NotableColors, entry =>
            entry.Color.Red > entry.Color.Green + 20 && entry.Color.Green > entry.Color.Blue + 40);
        var mustard = Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry =>
            entry.Color.Red > entry.Color.Green + 20 && entry.Color.Green > entry.Color.Blue + 40));
        Assert.NotEqual(NotableColorAdmissionPath.None, mustard.AdmissionPaths);
        Assert.True(mustard.FrequentOverlap > 0.03);
        Assert.False(mustard.Selected);
    }

    [Fact]
    public void SparseRandomChromaticNoiseHasNoAdmissionPath()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x % 13 == 2 && y % 11 == 3
                ? ((x + y) % 2 == 0 ? new SKColor(250, 20, 30) : new SKColor(20, 90, 250))
                : new SKColor(116, 119, 122));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        Assert.Empty(result.Analysis.NotableColors);
        Assert.All(
            result.Diagnostics.Notable.Candidates.Where(entry => entry.Chroma > 0.10),
            entry =>
            {
                Assert.Equal(NotableColorAdmissionPath.None, entry.AdmissionPaths);
                Assert.Contains("coherence", entry.RejectionReason, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void UniformSceneProducesNoQualifiedCandidate()
    {
        using var decoded = CreateSolidDecoded(96, 64, new SKColor(94, 98, 102));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        Assert.Empty(result.Analysis.NotableColors);
        Assert.Empty(result.Diagnostics.Notable.Candidates);
    }

    [Fact]
    public void NearMonochromeGradientDoesNotRepeatItsFrequentShadeAsNotable()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) =>
            {
                var step = (x + y) % 5;
                return new SKColor(
                    (byte)(41 + step * 5),
                    (byte)(98 + step * 8),
                    (byte)(158 + step * 10));
            });

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Empty(analysis.NotableColors);
    }

    [Fact]
    public void SimilarShadesOfOneCoherentRegionConsolidateBeforeAdmission()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 34 and < 62 && y is >= 18 and < 46
                ? (((x + y) % 3) switch
                {
                    0 => new SKColor(198, 83, 32),
                    1 => new SKColor(214, 96, 39),
                    _ => new SKColor(228, 111, 48)
                })
                : new SKColor(48, 112, 52));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);

        Assert.Single(result.Analysis.NotableColors.Where(IsWarm));
        Assert.Single(result.Diagnostics.Notable.Candidates.Where(entry => IsWarm(entry.Color)));
    }

    [Fact]
    public void PerceptuallySeparatedWarmColorsAreNotMergedByBroadFamily()
    {
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) => x is >= 10 and < 25 && y is >= 12 and < 29
                ? new SKColor(235, 45, 34)
                : x is >= 70 and < 85 && y is >= 36 and < 53
                    ? new SKColor(157, 82, 24)
                    : NeutralSceneTone((x / 12 + y / 9) % 6));

        var result = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(decoded, CancellationToken.None);
        var warm = result.Analysis.NotableColors.Where(IsWarm).ToArray();

        Assert.Equal(2, warm.Length);
        Assert.True(ColorDistance(warm[0].Color, warm[1].Color) > NotableColorSelector.DuplicateDistance);
    }

    [Fact]
    public void AdaptiveSelectionCanExposeMoreThanThreeStrongCandidatesWithoutFillingTen()
    {
        var accents = new[]
        {
            (X: 6, Y: 7, Color: new SKColor(235, 42, 38)),
            (X: 27, Y: 38, Color: new SKColor(34, 108, 220)),
            (X: 48, Y: 8, Color: new SKColor(226, 174, 28)),
            (X: 69, Y: 38, Color: new SKColor(147, 53, 190)),
            (X: 80, Y: 8, Color: new SKColor(31, 159, 91)),
        };
        using var decoded = CreateDecoded(
            96,
            64,
            (x, y) =>
            {
                var match = accents.FirstOrDefault(candidate =>
                    x >= candidate.X && x < candidate.X + 10 &&
                    y >= candidate.Y && y < candidate.Y + 13);
                return match.Color == default
                    ? NeutralSceneTone((x / 8 + y / 8) % 6)
                    : match.Color;
            });

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.InRange(analysis.NotableColors.Length, 4, 9);
        Assert.True(analysis.NotableColors.Length <= NotableColorSelector.MaximumColors);
    }

    [Fact]
    public void SinglePixelNoiseDoesNotSurfaceAsNotable()
    {
        using var decoded = CreateDecoded(
            64,
            64,
            (x, y) => x == 31 && y == 31
                ? new SKColor(255, 15, 25)
                : new SKColor(118, 120, 121));

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Empty(analysis.NotableColors);
        Assert.Equal(new StageColor(118, 120, 121), analysis.Palette[0].Color);
    }

    [Fact]
    public void UniformImageHasNoInventedNotableColor()
    {
        using var decoded = CreateSolidDecoded(96, 64, new SKColor(225, 92, 28));

        var analysis = new PhotoStyleAnalyzer().Analyze(decoded, CancellationToken.None);

        Assert.Empty(analysis.NotableColors);
        Assert.Single(analysis.Palette);
    }

    [Fact]
    public void FullyTransparentImageProducesDeterministicNeutralGradientFallbacks()
    {
        using var decoded = CreateSolidDecoded(32, 24, SKColors.Transparent);
        var analyzer = new PhotoStyleAnalyzer();

        var first = analyzer.Analyze(decoded, CancellationToken.None);
        var second = analyzer.Analyze(decoded, CancellationToken.None);

        Assert.Equal(0, first.VisibleSampleCount);
        Assert.Equal(StageDefaults.NeutralColor, first.AverageColor);
        Assert.Equal(StageDefaults.NeutralColor, first.DominantColor);
        Assert.Empty(first.NotableColors);
        Assert.Equal(
            PhotoDerivedStylePolicy.ResolveLinearGradient(first),
            PhotoDerivedStylePolicy.ResolveLinearGradient(second));
        Assert.Equal(
            PhotoDerivedStylePolicy.ResolveRadialGlow(first),
            PhotoDerivedStylePolicy.ResolveRadialGlow(second));
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

    private static SKColor WarmTone(int index) => index switch
    {
        0 => new SKColor(205, 92, 34),
        1 => new SKColor(218, 105, 39),
        2 => new SKColor(232, 116, 42),
        3 => new SKColor(194, 78, 28),
        4 => new SKColor(225, 128, 52),
        _ => new SKColor(208, 112, 45),
    };

    private static SKColor GreenTone(int index) => index switch
    {
        0 => new SKColor(38, 104, 31),
        1 => new SKColor(48, 122, 36),
        2 => new SKColor(57, 136, 42),
        3 => new SKColor(44, 112, 54),
        4 => new SKColor(68, 128, 48),
        _ => new SKColor(52, 118, 29),
    };

    private static SKColor WarmMutedTone(int index) => index switch
    {
        0 => new SKColor(181, 143, 115),
        1 => new SKColor(194, 151, 113),
        2 => new SKColor(173, 128, 96),
        _ => new SKColor(188, 139, 102)
    };

    private static SKColor WideWarmShadeTone(int index) => index == 0
        ? new SKColor(185, 75, 25)
        : new SKColor(225, 125, 45);

    private static SKColor NeutralSceneTone(int index) => index switch
    {
        0 => new SKColor(38, 40, 43),
        1 => new SKColor(65, 68, 72),
        2 => new SKColor(91, 94, 99),
        3 => new SKColor(118, 121, 125),
        4 => new SKColor(145, 148, 152),
        _ => new SKColor(172, 175, 179),
    };

    private static bool IsWarm(PhotoNotableColor color) => IsWarm(color.Color);

    private static bool IsWarm(StageColor color) =>
        color.Red > color.Green + 25 && color.Green > color.Blue;

    private static double ColorDistance(StageColor first, StageColor second)
    {
        var firstLab = PhotoStylingOklab.FromSrgb(first);
        var secondLab = PhotoStylingOklab.FromSrgb(second);
        var deltaL = firstLab.L - secondLab.L;
        var deltaA = firstLab.A - secondLab.A;
        var deltaB = firstLab.B - secondLab.B;
        return Math.Sqrt((deltaL * deltaL) + (deltaA * deltaA) + (deltaB * deltaB));
    }
}