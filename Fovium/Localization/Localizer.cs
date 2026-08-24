using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Fovium.Localization;

internal sealed class Localizer
{
    private readonly IReadOnlyDictionary<string, string> _english;
    private readonly IReadOnlyDictionary<string, string> _selected;

    internal Localizer(
        string locale,
        IReadOnlyDictionary<string, string> english,
        IReadOnlyDictionary<string, string>? selected = null)
    {
        Locale = locale;
        _english = english;
        _selected = selected ?? english;
    }

    public string Locale { get; }

    public static Localizer CreateForCurrentCulture() => Create(CultureInfo.CurrentUICulture);

    public static Localizer Create(CultureInfo culture)
    {
        var locale = LocaleResolver.Resolve(culture);
        var assembly = typeof(Localizer).Assembly;
        var english = LoadCatalog(assembly, "en");
        var selected = locale == "en" ? english : LoadCatalog(assembly, locale);
        return new Localizer(locale, english, selected);
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_selected.TryGetValue(key, out var selectedValue))
        {
            return selectedValue;
        }

        if (_english.TryGetValue(key, out var englishValue))
        {
            Debug.WriteLine($"Fovium localization fallback: {Locale}:{key} -> en");
            return englishValue;
        }

        Debug.WriteLine($"Fovium localization missing key: {key}");
        return key;
    }

    private static IReadOnlyDictionary<string, string> LoadCatalog(Assembly assembly, string locale)
    {
        var resourceName = $"Fovium.Localization.{locale}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded localization catalog is missing: {resourceName}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Localization catalog is empty: {resourceName}");
    }
}
