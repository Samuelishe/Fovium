using System.Globalization;
using Fovium.Localization;

namespace Fovium.Tests.Localization;

public sealed class LocalizationTests
{
    [Fact]
    public void EnglishCultureResolvesToEnglish()
    {
        Assert.Equal("en", LocaleResolver.Resolve(CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void RussianCultureResolvesToRussian()
    {
        Assert.Equal("ru", LocaleResolver.Resolve(CultureInfo.GetCultureInfo("ru-RU")));
    }

    [Fact]
    public void UnsupportedCultureFallsBackToEnglish()
    {
        Assert.Equal("en", LocaleResolver.Resolve(CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public void MissingRussianKeyFallsBackToEnglishValue()
    {
        var localizer = new Localizer(
            "ru",
            new Dictionary<string, string> { ["menu.open"] = "Open" },
            new Dictionary<string, string>());

        Assert.Equal("Open", localizer["menu.open"]);
    }

    [Fact]
    public void MissingEnglishKeyReturnsVisibleKey()
    {
        var localizer = new Localizer("en", new Dictionary<string, string>());

        Assert.Equal("missing.key", localizer["missing.key"]);
    }

    [Theory]
    [InlineData("en-US", "Open…")]
    [InlineData("ru-RU", "Открыть…")]
    public void EmbeddedCatalogsLoadForSupportedLocales(string cultureName, string expected)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expected, localizer[UiStrings.MenuOpen]);
    }
}
