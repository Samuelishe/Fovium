using System.Globalization;
using Fovium.Localization;

namespace Fovium.Tests.Localization;

public sealed class LanguageSelectionTests
{
    [Theory]
    [InlineData((int)UiLanguage.English, "ru-RU", "en")]
    [InlineData((int)UiLanguage.Russian, "en-US", "ru")]
    public void ExplicitLanguageOverridesSystemCulture(
        int languageValue,
        string systemCulture,
        string expectedLocale)
    {
        var resolved = LocaleResolver.Resolve(
            (UiLanguage)languageValue,
            CultureInfo.GetCultureInfo(systemCulture));

        Assert.Equal(expectedLocale, resolved);
    }

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")]
    public void SystemDefaultRetainsSupportedCultureResolution(
        string systemCulture,
        string expectedLocale)
    {
        var resolved = LocaleResolver.Resolve(
            UiLanguage.SystemDefault,
            CultureInfo.GetCultureInfo(systemCulture));

        Assert.Equal(expectedLocale, resolved);
    }

    [Theory]
    [InlineData((int)UiLanguage.English, "ru-RU", "en", "Language", "System default")]
    [InlineData((int)UiLanguage.Russian, "en-US", "ru", "Язык", "Как в системе")]
    public void SelectedLanguageLoadsItsOwnCompleteSettingsCatalog(
        int languageValue,
        string systemCulture,
        string expectedLocale,
        string languageHeading,
        string systemDefaultLabel)
    {
        var localizer = Localizer.Create(
            (UiLanguage)languageValue,
            CultureInfo.GetCultureInfo(systemCulture));

        Assert.Equal(expectedLocale, localizer.Locale);
        Assert.Equal(languageHeading, localizer[UiStrings.SettingsLanguage]);
        Assert.Equal(systemDefaultLabel, localizer[UiStrings.SettingsLanguageSystemDefault]);
        Assert.NotEqual(UiStrings.SettingsLanguageRestart, localizer[UiStrings.SettingsLanguageRestart]);
    }
}