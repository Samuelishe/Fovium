using System.Reflection;
using System.Text.Json;

namespace Fovium.ColorPicking;

internal sealed record ColorNameEntry(
    string StableId,
    byte Red,
    byte Green,
    byte Blue,
    string CanonicalName);

internal sealed class ColorNameCatalog
{
    public const int ExpectedCount = 1800;
    internal const string ResourceName = "Fovium.ColorNames.fovium-color-names.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private ColorNameCatalog(IReadOnlyList<ColorNameEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<ColorNameEntry> Entries { get; }

    public static ColorNameCatalog LoadEmbedded() => Load(typeof(ColorNameCatalog).Assembly);

    internal static ColorNameCatalog Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded color-name catalog is missing: {ResourceName}");
        var serialized = JsonSerializer.Deserialize<CatalogEntry[]>(stream, SerializerOptions)
            ?? throw new InvalidDataException("The embedded color-name catalog is empty.");
        var entries = new ColorNameEntry[serialized.Length];
        for (var index = 0; index < serialized.Length; index++)
        {
            var item = serialized[index];
            if (!TryParseHex(item.Hex, out var red, out var green, out var blue))
            {
                throw new InvalidDataException($"Color-name catalog entry {index} has invalid RGB data.");
            }

            entries[index] = new ColorNameEntry(
                item.Id ?? string.Empty,
                red,
                green,
                blue,
                item.Name ?? string.Empty);
        }

        Validate(entries);
        return new ColorNameCatalog(entries);
    }

    internal static ColorNameCatalog CreateForTests(
        IEnumerable<ColorNameEntry> entries,
        bool allowDuplicateRgb = false)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var materialized = entries.ToArray();
        Validate(materialized, enforceCountRange: false, allowDuplicateRgb);
        return new ColorNameCatalog(materialized);
    }

    private static void Validate(
        IReadOnlyList<ColorNameEntry> entries,
        bool enforceCountRange = true,
        bool allowDuplicateRgb = false)
    {
        if (enforceCountRange && entries.Count != ExpectedCount)
        {
            throw new InvalidDataException(
                $"Color-name catalog must contain exactly {ExpectedCount} entries; found {entries.Count}.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rgb = new HashSet<int>();
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.StableId) || !ids.Add(entry.StableId))
            {
                throw new InvalidDataException("Color-name catalog stable IDs must be nonempty and unique.");
            }

            if (string.IsNullOrWhiteSpace(entry.CanonicalName) ||
                entry.CanonicalName.Length > 64 ||
                !names.Add(entry.CanonicalName))
            {
                throw new InvalidDataException("Color-name catalog names must be bounded and unique.");
            }

            var packed = (entry.Red << 16) | (entry.Green << 8) | entry.Blue;
            if (!rgb.Add(packed) && !allowDuplicateRgb)
            {
                throw new InvalidDataException("Color-name catalog RGB anchors must be unique.");
            }
        }
    }

    private static bool TryParseHex(string? value, out byte red, out byte green, out byte blue)
    {
        red = green = blue = 0;
        return value is { Length: 7 } &&
            value[0] == '#' &&
            byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out red) &&
            byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out green) &&
            byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out blue);
    }

    private sealed record CatalogEntry(string? Id, string? Hex, string? Name);
}
