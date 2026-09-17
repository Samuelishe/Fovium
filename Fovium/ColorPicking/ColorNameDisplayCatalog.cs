using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Fovium.Localization;

namespace Fovium.ColorPicking;

internal sealed class ColorNameDisplayCatalog
{
    internal const int MaximumDisplayNameLength = 64;
    internal const string ResourcePrefix = "Fovium.ColorNames.Localization.";

    private static readonly ConcurrentDictionary<string, ColorNameDisplayCatalog> EmbeddedCatalogs =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> EmptyNames =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, string> _localizedNames;

    private ColorNameDisplayCatalog(IReadOnlyDictionary<string, string> localizedNames)
    {
        _localizedNames = localizedNames;
    }

    public int Count => _localizedNames.Count;

    public IReadOnlyDictionary<string, string> Names => _localizedNames;

    public static ColorNameDisplayCatalog ForLocale(string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        var normalized = locale.Trim().ToLowerInvariant();
        return EmbeddedCatalogs.GetOrAdd(normalized, LoadEmbeddedOrFallback);
    }

    public string Resolve(string stableId, string canonicalEnglishName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalEnglishName);
        return _localizedNames.TryGetValue(stableId, out var localizedName)
            ? localizedName
            : canonicalEnglishName;
    }

    internal static ColorNameDisplayCatalog CreateForTests(
        IReadOnlyDictionary<string, string>? localizedNames = null) =>
        new(localizedNames ?? EmptyNames);

    internal static ColorNameDisplayCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A localized color-name catalog must be a JSON object.");
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name) || !names.TryAdd(property.Name, ReadName(property)))
            {
                throw new InvalidDataException(
                    "Localized color-name catalog stable IDs must be nonempty and unique.");
            }
        }

        return new ColorNameDisplayCatalog(names);
    }

    internal static ColorNameDisplayCatalog LoadOrFallback(Func<Stream?> openStream)
    {
        ArgumentNullException.ThrowIfNull(openStream);
        try
        {
            using var stream = openStream();
            return stream is null ? new ColorNameDisplayCatalog(EmptyNames) : Load(stream);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            Debug.WriteLine($"Fovium color-name localization catalog failed: {exception.Message}");
            return new ColorNameDisplayCatalog(EmptyNames);
        }
    }

    private static ColorNameDisplayCatalog LoadEmbeddedOrFallback(string locale)
    {
        if (locale.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return new ColorNameDisplayCatalog(EmptyNames);
        }

        var resourceName = $"{ResourcePrefix}{locale}.json";
        var catalog = LoadOrFallback(() =>
            typeof(ColorNameDisplayCatalog).Assembly.GetManifestResourceStream(resourceName));
        if (catalog.Count == 0)
        {
            Debug.WriteLine($"Fovium color-name localization fallback: {locale} -> en");
        }

        return catalog;
    }

    private static string ReadName(JsonProperty property)
    {
        if (property.Value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Localized color-name catalog entry '{property.Name}' must be a string.");
        }

        var name = property.Value.GetString();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumDisplayNameLength)
        {
            throw new InvalidDataException(
                $"Localized color-name catalog entry '{property.Name}' must be nonempty and bounded.");
        }

        return name;
    }
}

internal sealed class ColorSampleNameResolver(
    Localizer localizer,
    ColorNameDisplayCatalog colorNames)
{
    public string Resolve(ColorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.IsTransparent || string.IsNullOrWhiteSpace(sample.CanonicalName))
        {
            return localizer[UiStrings.ColorPickerTransparent];
        }

        return colorNames.Resolve(sample.ColorNameStableId, sample.CanonicalName);
    }
}