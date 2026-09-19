using System.Globalization;

namespace Fovium.Localization;

internal static class LocaleResolver
{
    public static string Resolve(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? "ru"
            : "en";

    public static string Resolve(UiLanguage language, CultureInfo systemCulture) => language switch
    {
        UiLanguage.SystemDefault => Resolve(systemCulture),
        UiLanguage.English => "en",
        UiLanguage.Russian => "ru",
        _ => Resolve(systemCulture),
    };
}