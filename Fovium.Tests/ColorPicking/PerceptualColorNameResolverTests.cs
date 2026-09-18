using System.Globalization;
using Fovium.ColorPicking;
using Fovium.Localization;

namespace Fovium.Tests.ColorPicking;

public sealed class PerceptualColorNameResolverTests
{
    [Theory]
    [InlineData("en-US", "Dark burgundy", "Dark muted burgundy")]
    [InlineData("ru-RU", "Тёмный бордовый", "Тёмный приглушённый бордовый")]
    public void ShortAndDetailedNamesUseBoundedLocaleTerms(
        string cultureName,
        string expectedShort,
        string expectedDetailed)
    {
        var description = Describe(0x69, 0x40, 0x44);
        var resolver = CreateResolver(cultureName);

        Assert.Equal(expectedShort, resolver.ResolveShort(description));
        Assert.Equal(expectedDetailed, resolver.ResolveDetailed(description));
    }

    [Fact]
    public void LocaleChangesPresentationWithoutChangingClassificationIdentity()
    {
        var description = Describe(0xC8, 0x63, 0x51);
        var english = CreateResolver("en-US");
        var russian = CreateResolver("ru-RU");

        Assert.Equal("Coral red", english.ResolveShort(description));
        Assert.Equal("Кораллово-красный", russian.ResolveShort(description));
        Assert.Equal(PerceptualHueFamily.Coral, description.HueFamily);
        Assert.Equal(PerceptualLightnessClass.Medium, description.LightnessClass);
        Assert.Equal(PerceptualChromaClass.Moderate, description.ChromaClass);
    }

    [Fact]
    public void MissingRussianSemanticTermsFallBackToEnglish()
    {
        var english = new Dictionary<string, string>
        {
            [UiStrings.ColorPickerHueBurgundy] = "Burgundy",
            [UiStrings.ColorPickerModifierDark] = "Dark",
            [UiStrings.ColorPickerModifierMuted] = "Muted",
            [UiStrings.ColorPickerNameLightnessHue] = "{0} {1}",
            [UiStrings.ColorPickerNameLightnessChromaHue] = "{0} {1} {2}"
        };
        var localizer = new Localizer(
            "ru",
            english,
            new Dictionary<string, string>());
        var resolver = new PerceptualColorNameResolver(localizer);

        Assert.Equal("Dark burgundy", resolver.ResolveShort(Describe(0x69, 0x40, 0x44)));
        Assert.Equal("Dark muted burgundy", resolver.ResolveDetailed(Describe(0x69, 0x40, 0x44)));
    }

    [Theory]
    [InlineData("#344F67", "Slate", "Сланцевый")]
    [InlineData("#190B0B", "Red-black", "Красновато-чёрный")]
    [InlineData("#F8E2CD", "Cream white", "Кремово-белый")]
    [InlineData("#80A53E", "Olive-green", "Оливково-зелёный")]
    [InlineData("#59A3A6", "Teal", "Сине-зелёный")]
    [InlineData("#828FC4", "Periwinkle", "Барвинковый")]
    [InlineData("#A4256C", "Saturated crimson", "Насыщенный малиновый")]
    [InlineData("#FDC8F6", "Very light pink-lilac", "Очень светлый розово-лиловый")]
    public void EmpiricalSemanticNamesHaveEnglishAndRussianParity(
        string hex,
        string expectedEnglish,
        string expectedRussian)
    {
        var description = Describe(hex);

        Assert.Equal(expectedEnglish, CreateResolver("en-US").ResolveShort(description));
        Assert.Equal(expectedRussian, CreateResolver("ru-RU").ResolveShort(description));
    }

    [Theory]
    [InlineData("#C2992D", "Mustard", "Горчичный")]
    [InlineData("#B09E8A", "Greige", "Серо-бежевый")]
    [InlineData("#DBC5AE", "Light beige", "Светлый бежевый")]
    [InlineData("#DDBB96", "Light sand", "Светлый песочный")]
    [InlineData("#FFD5A8", "Very light apricot", "Очень светлый абрикосовый")]
    [InlineData("#FEB08C", "Light peach", "Светлый персиковый")]
    [InlineData("#EF9D77", "Light peach", "Светлый персиковый")]
    [InlineData("#737189", "Violet-gray", "Фиолетово-серый")]
    [InlineData("#C6C0CA", "Light lilac-gray", "Светлый лилово-серый")]
    public void EarthAndPurpleNeutralNamesHaveEnglishAndRussianParity(
        string hex,
        string expectedEnglish,
        string expectedRussian)
    {
        var description = Describe(hex);

        Assert.Equal(expectedEnglish, CreateResolver("en-US").ResolveShort(description));
        Assert.Equal(expectedRussian, CreateResolver("ru-RU").ResolveShort(description));
    }

    [Theory]
    [InlineData("en-US", "Very light mint")]
    [InlineData("ru-RU", "Очень светлый мятный")]
    public void MintUsesBoundedUiLocalizationTerms(string cultureName, string expected)
    {
        Assert.Equal(expected, CreateResolver(cultureName).ResolveShort(Describe("#ADF0D1")));
    }

    [Theory]
    [InlineData("en-US", "Color tone", "Mustard", "Undertone", "Cream")]
    [InlineData("ru-RU", "Цветовой тон", "Горчичный", "Подтон", "Кремовый")]
    public void DetailToneLabelDistinguishesChromaticToneFromNeutralUndertone(
        string cultureName,
        string chromaticLabel,
        string chromaticTone,
        string neutralLabel,
        string neutralTone)
    {
        var resolver = CreateResolver(cultureName);
        var chromatic = Describe("#C2992D");
        var nearWhite = Describe("#FFFDE8");

        Assert.Equal(chromaticLabel, resolver.ResolveDetailToneLabel(chromatic));
        Assert.Equal(chromaticTone, resolver.ResolveDetailTone(chromatic));
        Assert.Equal(neutralLabel, resolver.ResolveDetailToneLabel(nearWhite));
        Assert.Equal(neutralTone, resolver.ResolveDetailTone(nearWhite));
    }

    [Fact]
    public void NeutralDetailToneReportsUndertoneInsteadOfGrayFamilyName()
    {
        var resolver = CreateResolver("en-US");

        Assert.Equal("Blue", resolver.ResolveDetailTone(Describe("#9BA8B8")));
        Assert.Equal("Lilac", resolver.ResolveDetailTone(Describe("#C6C0CA")));
        Assert.Equal("Beige", resolver.ResolveDetailTone(Describe("#B09E8A")));
    }

    [Fact]
    public void EarthToneNamesDoNotAddRedundantMutedModifier()
    {
        var resolver = CreateResolver("en-US");
        var description = Describe("#DBC5AE");

        Assert.Equal("Light beige", resolver.ResolveShort(description));
        Assert.Equal("Light beige", resolver.ResolveDetailed(description));
    }

    [Fact]
    public void NeutralShortNamesUseLightnessWithoutInventingHue()
    {
        var resolver = CreateResolver("en-US");

        Assert.Equal("Black", resolver.ResolveShort(Describe(0, 0, 0)));
        Assert.Equal("Gray", resolver.ResolveShort(Describe(128, 128, 128)));
        Assert.Equal("White", resolver.ResolveShort(Describe(255, 255, 255)));
    }

    [Fact]
    public void TransparentNameUsesOrdinaryUiLocalization()
    {
        var english = CreateResolver("en-US");
        var russian = CreateResolver("ru-RU");

        Assert.Equal("Transparent", english.ResolveShort(PerceptualColorDescription.Transparent));
        Assert.Equal("Прозрачный", russian.ResolveDetailed(PerceptualColorDescription.Transparent));
    }

    [Fact]
    public void OklchFormattingIsLocaleIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

            Assert.Equal(
                "L 42.1% · C 0.058 · h 14°",
                PerceptualColorNameResolver.FormatOklch(new OklchColor(0.421, 0.058, 13.5)));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static PerceptualColorNameResolver CreateResolver(string cultureName) =>
        new(Localizer.Create(CultureInfo.GetCultureInfo(cultureName)));

    private static PerceptualColorDescription Describe(byte red, byte green, byte blue) =>
        PerceptualColorClassifier.Describe(new ColorSample(
            red,
            green,
            blue,
            byte.MaxValue,
            $"rgb-{red:x2}{green:x2}{blue:x2}",
            "Creative",
            ColorSampleAccuracy.Exact));

    private static PerceptualColorDescription Describe(string hex) => Describe(
        Convert.ToByte(hex.Substring(1, 2), 16),
        Convert.ToByte(hex.Substring(3, 2), 16),
        Convert.ToByte(hex.Substring(5, 2), 16));
}