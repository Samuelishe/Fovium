using System.Globalization;
using Fovium.ColorPicking;
using Fovium.Localization;

namespace Fovium.Tests.ColorPicking;

public sealed class ProfessionalColorShadeTests
{
    [Theory]
    [InlineData("#C79FEF", ProfessionalColorTerm.Lavender, PerceptualHueFamily.BlueViolet)]
    [InlineData("#8E82FE", ProfessionalColorTerm.Periwinkle, PerceptualHueFamily.BlueViolet)]
    [InlineData("#01153E", ProfessionalColorTerm.Navy, PerceptualHueFamily.Blue)]
    [InlineData("#069AF3", ProfessionalColorTerm.Azure, PerceptualHueFamily.Blue)]
    [InlineData("#75BBFD", ProfessionalColorTerm.SkyBlue, PerceptualHueFamily.Blue)]
    [InlineData("#87AE73", ProfessionalColorTerm.Sage, PerceptualHueFamily.Green)]
    [InlineData("#01A049", ProfessionalColorTerm.Emerald, PerceptualHueFamily.Green)]
    [InlineData("#06470C", ProfessionalColorTerm.ForestGreen, PerceptualHueFamily.Green)]
    [InlineData("#04D8B2", ProfessionalColorTerm.Aquamarine, PerceptualHueFamily.Turquoise)]
    [InlineData("#029386", ProfessionalColorTerm.Teal, PerceptualHueFamily.Turquoise)]
    [InlineData("#FF796C", ProfessionalColorTerm.Salmon, PerceptualHueFamily.Coral)]
    [InlineData("#80013F", ProfessionalColorTerm.Wine, PerceptualHueFamily.Crimson)]
    [InlineData("#A83C09", ProfessionalColorTerm.Rust, PerceptualHueFamily.Brown)]
    [InlineData("#BE0119", ProfessionalColorTerm.Scarlet, PerceptualHueFamily.Red)]
    [InlineData("#FF9408", ProfessionalColorTerm.Tangerine, PerceptualHueFamily.Orange)]
    [InlineData("#FFFFCB", ProfessionalColorTerm.Ivory, PerceptualHueFamily.Cream)]
    [InlineData("#343837", ProfessionalColorTerm.Charcoal, PerceptualHueFamily.Neutral)]
    [InlineData("#516572", ProfessionalColorTerm.Slate, PerceptualHueFamily.BlueGray)]
    public void IndependentReferenceAnchorsResolveSpecificTermsWithoutReplacingBaseFamilies(
        string hex,
        object expectedTerm,
        object expectedFamily)
    {
        var description = Describe(hex);

        Assert.Equal((PerceptualHueFamily)expectedFamily, description.HueFamily);
        Assert.Equal((ProfessionalColorTerm)expectedTerm, description.ProfessionalTerm);
    }

    [Theory]
    [InlineData("#341D6D")]
    [InlineData("#00FF00")]
    [InlineData("#FF0000")]
    [InlineData("#FF00FF")]
    [InlineData("#C95E3A")]
    [InlineData("#FFD000")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#0080FF")]
    [InlineData("#77C081")]
    public void SpecificTermsDoNotConsumeAcceptedGenericOrAdjacentControls(string hex)
    {
        Assert.Null(Describe(hex).ProfessionalTerm);
    }

    [Fact]
    public void DeclarativeCatalogHasStableUniqueReachableLocalizedDefinitions()
    {
        var definitions = ProfessionalShadeCatalog.Definitions;

        Assert.Equal(18, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(item => item.StableId).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(item => item.Term).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(item => item.Priority).Distinct().Count());
        Assert.All(definitions, definition =>
        {
            Assert.StartsWith("professional-", definition.StableId, StringComparison.Ordinal);
            Assert.NotEmpty(definition.LocalizationKey);
            Assert.NotEmpty(definition.ParentFamilies);
            Assert.InRange(definition.MinimumLightness, 0, 1);
            Assert.InRange(definition.MaximumLightness, 0, 1.001);
            Assert.True(definition.MinimumLightness < definition.MaximumLightness);
            Assert.True(definition.MinimumChroma < definition.MaximumChroma);
            Assert.InRange(definition.MinimumHue, 0, 360);
            Assert.InRange(definition.MaximumHue, 0, 360);

            var english = Localizer.Create(CultureInfo.GetCultureInfo("en-US"))[definition.LocalizationKey];
            var russian = Localizer.Create(CultureInfo.GetCultureInfo("ru-RU"))[definition.LocalizationKey];
            Assert.NotEqual(definition.LocalizationKey, english);
            Assert.NotEqual(definition.LocalizationKey, russian);
            Assert.NotEqual(english, russian);
        });
    }

    [Theory]
    [InlineData(0.68, 0.12, 291.999, ProfessionalColorTerm.Periwinkle)]
    [InlineData(0.68, 0.12, 292.000, ProfessionalColorTerm.Lavender)]
    [InlineData(0.719999, 0.15, 245, ProfessionalColorTerm.Azure)]
    [InlineData(0.720000, 0.15, 245, ProfessionalColorTerm.SkyBlue)]
    [InlineData(0.479999, 0.15, 150, ProfessionalColorTerm.ForestGreen)]
    [InlineData(0.480000, 0.15, 150, ProfessionalColorTerm.Emerald)]
    public void AdjacentSpecificRegionsUseExplicitNonOverlappingBoundaries(
        double lightness,
        double chroma,
        double hue,
        object expected)
    {
        var family = hue switch
        {
            < 200 => PerceptualHueFamily.Green,
            < 270 => PerceptualHueFamily.Blue,
            _ => PerceptualHueFamily.BlueViolet
        };

        Assert.Equal(
            (ProfessionalColorTerm)expected,
            ProfessionalShadeClassifier.Classify(
                new OklchColor(lightness, chroma, hue),
                PerceptualColorRole.Chromatic,
                family));
    }

    [Theory]
    [InlineData("#C79FEF", "Lavender", "Лавандовый", "Blue-violet", "Сине-фиолетовый")]
    [InlineData("#87AE73", "Sage", "Шалфейный", "Green", "Зелёный")]
    [InlineData("#80013F", "Wine", "Винный", "Crimson", "Малиновый")]
    [InlineData("#343837", "Charcoal", "Угольный", "Neutral", "Нейтральный")]
    public void SpecificPrimaryNameAndGenericDetailToneRemainSeparate(
        string hex,
        string englishName,
        string russianName,
        string englishTone,
        string russianTone)
    {
        var description = Describe(hex);
        var english = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("en-US")));
        var russian = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("ru-RU")));

        Assert.Equal(englishName, english.ResolveShort(description));
        Assert.Equal(englishName, english.ResolveDetailed(description));
        Assert.Equal(russianName, russian.ResolveShort(description));
        Assert.Equal(russianName, russian.ResolveDetailed(description));
        Assert.Equal(englishTone, english.ResolveDetailTone(description));
        Assert.Equal(russianTone, russian.ResolveDetailTone(description));
    }

    private static PerceptualColorDescription Describe(string hex) =>
        PerceptualColorClassifier.Describe(new ColorSample(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16),
            byte.MaxValue,
            $"rgb-{hex[1..].ToLowerInvariant()}",
            null,
            ColorSampleAccuracy.Exact));
}