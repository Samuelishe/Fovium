using System.Globalization;

namespace Fovium.Localization;

internal static class LocaleResolver
{
    public static string Resolve(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? "ru"
            : "en";
}
