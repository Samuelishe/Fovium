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

    private static PhotoStyleAnalysis CreateAnalysis(
        StageColor color,
        ImmutableArray<PhotoPaletteEntry> palette)
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
            TimeSpan.Zero);
    }
}