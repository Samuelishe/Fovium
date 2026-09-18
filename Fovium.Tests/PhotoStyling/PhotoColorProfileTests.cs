using System.Collections.Immutable;
using Fovium.ColorSemantics;
using Fovium.PhotoStyling;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Tests.PhotoStyling;

public sealed class PhotoColorProfileTests
{
    [Fact]
    public void ProfessionalStructuralIdentityWinsWhileCreativeNameRemainsSecondary()
    {
        var burntOrange = new StageColor(203, 99, 43);
        var projector = CreateProjector(burntOrange, "Unexpected creative label");

        var profile = projector.Create(CreateAnalysis(
            burntOrange,
            burntOrange,
            [new PhotoPaletteEntry(burntOrange, 1)]));

        var actual = Assert.IsType<PhotoColorProfile>(profile);
        Assert.Equal(ProfessionalColorTerm.BurntOrange, actual.Dominant.Description.ProfessionalTerm);
        Assert.Equal("professional-burnt-orange", actual.Dominant.ProfessionalMatch?.TermStableId);
        Assert.Equal("creative-test", actual.Dominant.CreativeName.StableId);
        Assert.Equal("Unexpected creative label", actual.Dominant.CreativeName.CanonicalName);
    }

    [Fact]
    public void StructuralIdentityFallsBackToBroadFamilyWhenNoProfessionalRegionWins()
    {
        var cyan = new StageColor(0, 212, 255);
        var projector = CreateProjector(cyan, "Electric cyan");

        var profile = projector.Create(CreateAnalysis(
            cyan,
            cyan,
            [new PhotoPaletteEntry(cyan, 1)]));

        var actual = Assert.IsType<PhotoColorProfile>(profile);
        Assert.Null(actual.Dominant.ProfessionalMatch);
        Assert.Null(actual.Dominant.Description.ProfessionalTerm);
        Assert.Equal(PerceptualHueFamily.Cyan, actual.Dominant.Description.HueFamily);
    }

    [Fact]
    public void RawPaletteColorsWeightsAndPopulationOrderArePreserved()
    {
        var colors = new[]
        {
            new StageColor(53, 88, 123),
            new StageColor(70, 105, 140),
            new StageColor(228, 184, 118),
            new StageColor(34, 39, 44),
            new StageColor(122, 126, 129),
        };
        var weights = new[] { 0.34, 0.26, 0.18, 0.13, 0.09 };
        var projector = CreateProjector(colors);
        var raw = colors.Select((color, index) => new PhotoPaletteEntry(color, weights[index]))
            .ToImmutableArray();

        var profile = projector.Create(CreateAnalysis(colors[4], colors[0], raw));

        var actual = Assert.IsType<PhotoColorProfile>(profile);
        Assert.Equal(colors, actual.Palette.Select(entry => entry.Color.Color));
        Assert.Equal(weights, actual.Palette.Select(entry => entry.Weight));
        Assert.Equal(5, actual.Palette.Length);
        Assert.Equal(1, actual.Palette.Sum(entry => entry.Weight), 12);
        Assert.Equal(colors[4], actual.Average.Color);
    }

    [Fact]
    public void RepeatedStructuralNamesDoNotCollapseDistinctRawPhotoClusters()
    {
        var darkerBlue = new StageColor(53, 88, 123);
        var lighterBlue = new StageColor(70, 105, 140);
        var projector = CreateProjector(darkerBlue, lighterBlue);

        var profile = projector.Create(CreateAnalysis(
            darkerBlue,
            darkerBlue,
            [
                new PhotoPaletteEntry(darkerBlue, 0.55),
                new PhotoPaletteEntry(lighterBlue, 0.45),
            ]));

        var actual = Assert.IsType<PhotoColorProfile>(profile);
        Assert.Equal(2, actual.Palette.Length);
        Assert.Equal(
            actual.Palette[0].Color.Description.HueFamily,
            actual.Palette[1].Color.Description.HueFamily);
        Assert.NotEqual(actual.Palette[0].Color.Color, actual.Palette[1].Color.Color);
    }

    [Fact]
    public void FullyTransparentAnalysisDoesNotInventAProfileFromStylingFallbackColors()
    {
        var neutral = new StageColor(32, 32, 32);
        var projector = CreateProjector(neutral, "Neutral fallback");

        var profile = projector.Create(CreateAnalysis(
            neutral,
            neutral,
            [new PhotoPaletteEntry(neutral, 1)],
            visibleSampleCount: 0));

        Assert.Null(profile);
    }

    [Theory]
    [InlineData(0, 0, 0, "NearBlack")]
    [InlineData(255, 255, 255, "NearWhite")]
    [InlineData(128, 128, 128, "Neutral")]
    public void UniformLowInformationImagesProduceOneTruthfulSemanticColor(
        byte red,
        byte green,
        byte blue,
        string expectedRole)
    {
        var color = new StageColor(red, green, blue);
        var projector = CreateProjector(color);

        var profile = projector.Create(CreateAnalysis(
            color,
            color,
            [new PhotoPaletteEntry(color, 1)]));

        var actual = Assert.IsType<PhotoColorProfile>(profile);
        Assert.Single(actual.Palette);
        Assert.Equal(1, actual.Palette[0].Weight, 12);
        Assert.Equal(expectedRole, actual.Dominant.Description.Role.ToString());
    }

    [Fact]
    public void ProjectionIsDeterministicAndRetainsNoRasterPayload()
    {
        var dominant = new StageColor(203, 99, 43);
        var average = new StageColor(112, 105, 98);
        var projector = CreateProjector(dominant, average);
        var analysis = CreateAnalysis(
            average,
            dominant,
            [
                new PhotoPaletteEntry(dominant, 0.7),
                new PhotoPaletteEntry(average, 0.3),
            ]);

        var first = Assert.IsType<PhotoColorProfile>(projector.Create(analysis));
        var second = Assert.IsType<PhotoColorProfile>(projector.Create(analysis));

        Assert.Equal(first.Dominant, second.Dominant);
        Assert.Equal(first.Average, second.Average);
        Assert.True(first.Palette.SequenceEqual(second.Palette));
        Assert.Equal(first.RetainedBytes, second.RetainedBytes);
        Assert.InRange(first.RetainedBytes, 1, 4096);
    }

    private static PhotoColorProfileProjector CreateProjector(
        StageColor color,
        string name = "Creative") => CreateProjector([color], name);

    private static PhotoColorProfileProjector CreateProjector(params StageColor[] colors) =>
        CreateProjector(colors, "Creative");

    private static PhotoColorProfileProjector CreateProjector(
        IReadOnlyList<StageColor> colors,
        string name)
    {
        var entries = colors.Distinct().Select((color, index) => new ColorNameEntry(
            index == 0 ? "creative-test" : $"creative-test-{index}",
            color.Red,
            color.Green,
            color.Blue,
            index == 0 ? name : $"{name} {index}"));
        return new PhotoColorProfileProjector(
            new ColorNameMatcher(ColorNameCatalog.CreateForTests(entries)));
    }

    private static PhotoStyleAnalysis CreateAnalysis(
        StageColor average,
        StageColor dominant,
        ImmutableArray<PhotoPaletteEntry> palette,
        int visibleSampleCount = 96)
    {
        var field = Enumerable.Repeat(
                average,
                StageDefaults.PhotoStyleFieldColumns * StageDefaults.PhotoStyleFieldRows)
            .ToImmutableArray();
        return new PhotoStyleAnalysis(
            average,
            dominant,
            average,
            palette,
            new PhotoColorField(
                StageDefaults.PhotoStyleFieldColumns,
                StageDefaults.PhotoStyleFieldRows,
                field),
            new PixelSize(12, 8),
            visibleSampleCount,
            TimeSpan.Zero);
    }
}