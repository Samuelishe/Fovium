using System.Collections.Immutable;
using System.Globalization;
using Fovium.ColorSemantics;
using Fovium.Localization;
using Fovium.Metadata;
using Fovium.PhotoStyling;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Tests.Metadata;

public sealed class PhotoColorProfilePresentationTests
{
    [Theory]
    [InlineData("en-US", "Burnt orange", "Creative orange", "62%")]
    [InlineData("ru-RU", "Жжёный оранжевый", "Образный оранжевый", "62 %")]
    public void StructuralAndCreativeNamesRemainSeparateAndLocalized(
        string cultureName,
        string expectedStructural,
        string expectedCreative,
        string expectedShare)
    {
        var color = new StageColor(203, 99, 43);
        var matcher = new ColorNameMatcher(ColorNameCatalog.CreateForTests(
        [
            new ColorNameEntry("creative-orange", color.Red, color.Green, color.Blue, "Creative orange"),
        ]));
        var projector = new PhotoColorProfileProjector(matcher);
        var profile = Assert.IsType<PhotoColorProfile>(projector.Create(CreateAnalysis(
            color,
            [new PhotoPaletteEntry(color, 0.62)])));
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var localizer = Localizer.Create(culture);
        var creativeNames = ColorNameDisplayCatalog.CreateForTests(
            culture.TwoLetterISOLanguageName == "ru"
                ? new Dictionary<string, string> { ["creative-orange"] = "Образный оранжевый" }
                : null);

        var actual = PhotoColorProfilePresenter.Format(
            profile,
            culture,
            new PerceptualColorNameResolver(localizer),
            creativeNames);

        Assert.Equal(expectedStructural, actual.Dominant.StructuralName);
        Assert.Equal(expectedCreative, actual.Dominant.CreativeName);
        Assert.Equal("#CB632B", actual.Dominant.Hex);
        Assert.StartsWith("L ", actual.Dominant.Oklch, StringComparison.Ordinal);
        Assert.Equal(expectedShare, actual.Palette[0].Share);
        Assert.Empty(actual.NotableColors);
    }

    [Fact]
    public void LongCreativeNameDoesNotReplaceOrMutateStructuralDisplayName()
    {
        var color = new StageColor(203, 99, 43);
        var longCreativeName = new string('C', ColorNameDisplayCatalog.MaximumDisplayNameLength);
        var projector = new PhotoColorProfileProjector(new ColorNameMatcher(
            ColorNameCatalog.CreateForTests(
            [
                new ColorNameEntry("creative-long", color.Red, color.Green, color.Blue, longCreativeName),
            ])));
        var profile = Assert.IsType<PhotoColorProfile>(projector.Create(CreateAnalysis(
            color,
            [new PhotoPaletteEntry(color, 1)])));
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo("en-US"));

        var actual = PhotoColorProfilePresenter.Format(
            profile,
            CultureInfo.GetCultureInfo("en-US"),
            new PerceptualColorNameResolver(localizer),
            ColorNameDisplayCatalog.CreateForTests());

        Assert.Equal("Burnt orange", actual.Dominant.StructuralName);
        Assert.Equal(longCreativeName, actual.Dominant.CreativeName);
        Assert.NotEqual(actual.Dominant.CreativeName, actual.Dominant.StructuralName);
    }

    [Theory]
    [InlineData("en-US", 0.004, "0.4%")]
    [InlineData("ru-RU", 0.004, "0,4 %")]
    [InlineData("en-US", 0.02, "2%")]
    public void SubPercentSharesRemainTruthfulInsteadOfRoundingToZero(
        string cultureName,
        double weight,
        string expected)
    {
        var color = new StageColor(53, 88, 123);
        var profile = Assert.IsType<PhotoColorProfile>(new PhotoColorProfileProjector().Create(
            CreateAnalysis(color, [new PhotoPaletteEntry(color, weight)])));
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var localizer = Localizer.Create(culture);

        var actual = PhotoColorProfilePresenter.Format(
            profile,
            culture,
            new PerceptualColorNameResolver(localizer),
            ColorNameDisplayCatalog.CreateForTests());

        Assert.Equal(expected, actual.Palette[0].Share);
    }

    [Fact]
    public void NotablePresentationHasNoFrequencyShareAndKeepsExactColorDetail()
    {
        var green = new StageColor(52, 118, 42);
        var orange = new StageColor(218, 105, 39);
        var profile = Assert.IsType<PhotoColorProfile>(new PhotoColorProfileProjector().Create(
            CreateAnalysis(
                green,
                [new PhotoPaletteEntry(green, 1)],
                [new PhotoNotableColor(orange, 0.12, 0.10, 0.12, 0.8)])));
        var culture = CultureInfo.GetCultureInfo("en-US");
        var localizer = Localizer.Create(culture);

        var actual = PhotoColorProfilePresenter.Format(
            profile,
            culture,
            new PerceptualColorNameResolver(localizer),
            ColorNameDisplayCatalog.CreateForTests());

        var notable = Assert.Single(actual.NotableColors);
        Assert.Equal(orange, notable.Color);
        Assert.Equal("#DA6927", notable.Hex);
        Assert.False(string.IsNullOrWhiteSpace(notable.StructuralName));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(5, 1, 5)]
    [InlineData(6, 2, 5)]
    [InlineData(10, 2, 5)]
    [InlineData(12, 2, 5)]
    public void NotableLayoutUsesAtMostTwoRowsOfFive(
        int requested,
        int expectedRows,
        int expectedMaximumRowLength)
    {
        var colors = Enumerable.Range(0, requested)
            .Select(index => new PhotoColorProfileDisplayColor(
                new StageColor((byte)(20 + index), (byte)(40 + index), (byte)(60 + index)),
                $"Structural {index}",
                $"Creative {index}",
                $"#{index:X6}",
                $"L {index}"))
            .ToImmutableArray();

        var rows = PhotoColorProfileLayout.ArrangeNotableColors(colors);

        Assert.Equal(expectedRows, rows.Length);
        Assert.True(rows.Length <= 2);
        Assert.All(rows, row => Assert.InRange(row.Length, 1, 5));
        if (rows.Length > 0)
        {
            Assert.Equal(expectedMaximumRowLength, rows.Max(row => row.Length));
        }

        Assert.Equal(Math.Min(requested, 10), rows.Sum(row => row.Length));
    }

    private static PhotoStyleAnalysis CreateAnalysis(
        StageColor color,
        ImmutableArray<PhotoPaletteEntry> palette,
        ImmutableArray<PhotoNotableColor> notableColors = default)
    {
        var field = Enumerable.Repeat(color, 36).ToImmutableArray();
        return new PhotoStyleAnalysis(
            color,
            color,
            color,
            palette,
            new PhotoColorField(6, 6, field),
            new PixelSize(8, 8),
            64,
            TimeSpan.Zero,
            notableColors);
    }
}