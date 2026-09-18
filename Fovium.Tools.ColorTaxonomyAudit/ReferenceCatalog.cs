using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record ReferenceAnchor(
    string Dataset,
    string Name,
    AuditRgb Rgb,
    string SemanticFamily,
    double LabL,
    double LabA,
    double LabB)
{
    public string? SpecificTerm { get; init; }
}

internal sealed record ReferenceCatalog(
    IReadOnlyList<ReferenceAnchor> Anchors,
    IReadOnlyList<ReferenceDatasetSummary> Summaries)
{
    public static ReferenceCatalog Empty { get; } = new([], []);
}

internal static partial class ReferenceCatalogLoader
{
    private static readonly JsonSerializerOptions ProvenanceJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ReferenceCatalog Load(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return ReferenceCatalog.Empty;
        }

        var provenance = LoadProvenance(Path.Combine(directory, "provenance.json"));
        var anchors = new List<ReferenceAnchor>();
        LoadXkcd(Path.Combine(directory, "xkcd-rgb.txt"), anchors);
        LoadCss(Path.Combine(directory, "css-color-4.html"), anchors);
        LoadMeodai(Path.Combine(directory, "meodai-colornames.csv"), anchors);
        LoadIsccNbs(Path.Combine(directory, "nbs-iscc.txt"), anchors);

        var summaries = anchors
            .GroupBy(anchor => anchor.Dataset, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                provenance.TryGetValue(group.Key, out var item);
                return new ReferenceDatasetSummary(
                    group.Key,
                    group.Count(),
                    group.Count(anchor => anchor.SemanticFamily != "Unknown"),
                    item?.Source ?? string.Empty,
                    item?.License ?? string.Empty,
                    item?.Sha256 ?? string.Empty);
            })
            .ToArray();
        return new ReferenceCatalog(anchors, summaries);
    }

    private static void LoadXkcd(string path, ICollection<ReferenceAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var match = HexAtEndRegex().Match(line);
            if (!match.Success || !TryParseHex(match.Groups["hex"].Value, out var rgb))
            {
                continue;
            }

            var name = line[..match.Index].Trim().TrimEnd(':').Trim();
            Add("xkcd", name, rgb, anchors);
        }
    }

    private static void LoadCss(string path, ICollection<ReferenceAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var html = File.ReadAllText(path);
        foreach (Match match in CssColorRegex().Matches(html))
        {
            if (TryParseHex(match.Groups["hex"].Value, out var rgb))
            {
                Add("css", match.Groups["name"].Value, rgb, anchors);
            }
        }
    }

    private static void LoadMeodai(string path, ICollection<ReferenceAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var fields = ParseCsv(line);
            if (fields.Count < 2 || !TryParseHex(fields[1], out var rgb))
            {
                continue;
            }

            Add("meodai", fields[0], rgb, anchors);
        }
    }

    private static void LoadIsccNbs(string path, ICollection<ReferenceAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var match = IsccNbsColorRegex().Match(line);
            if (match.Success && TryParseHex(match.Groups["hex"].Value, out var rgb))
            {
                Add("iscc-nbs-centroids", match.Groups["name"].Value, rgb, anchors);
            }
        }
    }

    private static void Add(
        string dataset,
        string name,
        AuditRgb rgb,
        ICollection<ReferenceAnchor> anchors)
    {
        var lab = Fovium.ColorPicking.OklabColor.FromSrgb(rgb.Red, rgb.Green, rgb.Blue);
        anchors.Add(new ReferenceAnchor(
            dataset,
            name,
            rgb,
            SemanticNameNormalizer.Normalize(name),
            lab.L,
            lab.A,
            lab.B)
        {
            SpecificTerm = SpecificColorTermNormalizer.Normalize(name)
        });
    }

    private static Dictionary<string, ProvenanceItem> LoadProvenance(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, ProvenanceItem>(StringComparer.Ordinal);
        }

        var items = JsonSerializer.Deserialize<ProvenanceItem[]>(
            File.ReadAllText(path),
            ProvenanceJsonOptions) ?? [];
        return items.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static bool TryParseHex(string value, out AuditRgb rgb)
    {
        var normalized = value.Trim().TrimStart('#');
        if (normalized.Length == 6 &&
            byte.TryParse(normalized.AsSpan(0, 2), NumberStyles.HexNumber, null, out var red) &&
            byte.TryParse(normalized.AsSpan(2, 2), NumberStyles.HexNumber, null, out var green) &&
            byte.TryParse(normalized.AsSpan(4, 2), NumberStyles.HexNumber, null, out var blue))
        {
            rgb = new AuditRgb(red, green, blue);
            return true;
        }

        rgb = default;
        return false;
    }

    private sealed record ProvenanceItem(string Id, string Source, string License, string Sha256);

    [GeneratedRegex(@"(?<hex>#[0-9a-fA-F]{6})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HexAtEndRegex();

    [GeneratedRegex(
        """id="valdef-color-(?<name>[a-z]+)">[a-z]+</dfn>\s*<td>#(?<hex>[0-9a-fA-F]{6})""",
        RegexOptions.CultureInvariant)]
    private static partial Regex CssColorRegex();

    [GeneratedRegex(
        @"\x22(?<name>[^\x22]+)\x22\s+sRGB:(?<hex>[0-9a-fA-F]{6})",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsccNbsColorRegex();
}

internal static class SpecificColorTermNormalizer
{
    private static readonly (string Term, string[] Aliases)[] Rules =
    [
        // Discovery vocabulary intentionally exceeds the shipped product terms.
        // It is audit-only: repeated, distance-qualified anchors nominate a
        // research cluster but never become a runtime classification rule.
        ("BloodOrange", ["blood orange"]),
        ("RoseQuartz", ["rose quartz"]),
        ("PowderBlue", ["powder blue"]),
        ("SteelBlue", ["steel blue"]),
        ("IceBlue", ["ice blue"]),
        ("DustyBlue", ["dusty blue"]),
        ("RoyalBlue", ["royal blue"]),
        ("OliveDrab", ["olive drab"]),
        ("AppleGreen", ["apple green"]),
        ("SeaGreen", ["sea green"]),
        ("SkyBlue", ["sky blue"]),
        ("ForestGreen", ["forest green"]),
        ("Seafoam", ["sea foam", "seafoam"]),
        ("Lilac", ["lilac"]),
        ("Mauve", ["mauve"]),
        ("Cobalt", ["cobalt"]),
        ("Cerulean", ["cerulean"]),
        ("Indigo", ["indigo"]),
        ("Ecru", ["ecru"]),
        ("Linen", ["linen"]),
        ("Champagne", ["champagne"]),
        ("Stone", ["stone"]),
        ("Mushroom", ["mushroom"]),
        ("Pewter", ["pewter"]),
        ("Pumpkin", ["pumpkin"]),
        ("Blush", ["blush"]),
        ("Denim", ["denim"]),
        ("Sapphire", ["sapphire"]),
        ("Jade", ["jade"]),
        ("Moss", ["moss"]),
        ("Khaki", ["khaki"]),
        ("Celadon", ["celadon"]),
        ("Pistachio", ["pistachio"]),
        ("Chartreuse", ["chartreuse"]),
        ("Lime", ["lime"]),
        ("Petrol", ["petrol"]),
        ("Brick", ["brick"]),
        ("Copper", ["copper"]),
        ("Mahogany", ["mahogany"]),
        ("Chestnut", ["chestnut"]),
        ("Cinnamon", ["cinnamon"]),
        ("Caramel", ["caramel"]),
        ("Honey", ["honey"]),
        ("Gold", ["golden", "gold"]),
        ("Lemon", ["lemon"]),
        ("Canary", ["canary"]),
        ("Vanilla", ["vanilla"]),
        ("Pearl", ["pearl"]),
        ("Silver", ["silver"]),
        ("Graphite", ["graphite"]),
        ("Ash", ["ash"]),
        ("Smoke", ["smoke"]),
        ("Lavender", ["lavender"]),
        ("Periwinkle", ["periwinkle"]),
        ("Navy", ["navy blue", "navy"]),
        ("Azure", ["azure"]),
        ("Sage", ["sage green", "sage"]),
        ("Emerald", ["emerald green", "emerald"]),
        ("Aquamarine", ["aquamarine"]),
        ("Teal", ["teal"]),
        ("Salmon", ["salmon"]),
        ("Wine", ["wine red", "red wine", "wine"]),
        ("Rust", ["rusty", "rust"]),
        ("Scarlet", ["scarlet"]),
        ("Tangerine", ["tangerine"]),
        ("Ivory", ["ivory"]),
        ("Charcoal", ["charcoal"]),
        ("Slate", ["slate"]),
    ];

    public static string? Normalize(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Replace('_', ' ');
        if (normalized.Contains("navy green", StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var (term, aliases) in Rules)
        {
            if (aliases.Any(alias => ContainsToken(normalized, alias)))
            {
                return term;
            }
        }

        return null;
    }

    private static bool ContainsToken(string value, string term)
    {
        var index = value.IndexOf(term, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
            var afterIndex = index + term.Length;
            var after = afterIndex == value.Length || !char.IsLetterOrDigit(value[afterIndex]);
            if (before && after)
            {
                return true;
            }

            index = value.IndexOf(term, index + 1, StringComparison.Ordinal);
        }

        return false;
    }
}

internal static class SemanticNameNormalizer
{
    private static readonly (string Family, string[] Terms)[] Rules =
    [
        ("RedOrange", ["reddish orange", "red orange", "red-orange"]),
        ("Amber", ["orange yellow", "orange-yellow", "amber"]),
        ("YellowGreen", ["greenish yellow", "yellow green", "yellow-green", "lime", "chartreuse"]),
        ("OliveGreen", ["olive green", "olive-green"]),
        ("Turquoise", ["bluish green", "blue green", "blue-green", "turquoise", "teal", "aquamarine"]),
        ("CyanBlue", ["greenish blue", "cyan blue", "cyan-blue"]),
        ("BlueViolet", ["purplish blue", "violet blue", "blue violet", "blue-violet", "indigo"]),
        ("RedMagenta", ["reddish purple", "red purple", "red-purple"]),
        ("PinkLilac", ["purplish pink", "pink lilac", "pink-lilac"]),
        ("DustyPink", ["dusty pink", "dusty rose", "old rose"]),
        ("Mint", ["mint"]),
        ("Cream", ["cream", "ivory", "cornsilk", "seashell", "old lace"]),
        ("Greige", ["greige", "taupe"]),
        ("Beige", ["beige", "tan", "sand", "khaki", "wheat", "burlywood"]),
        ("Mustard", ["mustard"]),
        ("Ochre", ["ochre", "ocher", "gold", "goldenrod"]),
        ("Olive", ["olive"]),
        ("Yellow", ["yellow"]),
        ("Orange", ["orange", "tangerine"]),
        ("Apricot", ["apricot"]),
        ("Peach", ["peach"]),
        ("Terracotta", ["terracotta", "terra cotta", "burnt sienna", "rust"]),
        ("Brown", ["brown", "umber", "sienna", "chocolate"]),
        ("Burgundy", ["burgundy", "maroon", "wine"]),
        ("Crimson", ["crimson", "scarlet"]),
        ("Rose", ["rose"]),
        ("Pink", ["pink"]),
        ("Coral", ["coral"]),
        ("Coral", ["salmon"]),
        ("Red", ["red", "vermilion"]),
        ("Magenta", ["magenta", "fuchsia"]),
        ("Violet", ["purple", "violet", "lilac", "lavender", "mauve", "plum"]),
        ("Blue", ["blue", "navy", "azure", "periwinkle"]),
        ("Cyan", ["cyan", "aqua"]),
        ("Green", ["green", "emerald"]),
        ("Gray", ["gray", "grey", "silver", "slate", "charcoal"]),
        ("Black", ["black", "ebony"]),
        ("White", ["white", "snow"]),
    ];

    public static string Normalize(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Replace('_', ' ');
        foreach (var (family, terms) in Rules)
        {
            if (terms.Any(term => ContainsToken(normalized, term)))
            {
                return family;
            }
        }

        return "Unknown";
    }

    private static bool ContainsToken(string value, string term)
    {
        var index = value.IndexOf(term, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
            var afterIndex = index + term.Length;
            var after = afterIndex == value.Length || !char.IsLetterOrDigit(value[afterIndex]);
            if (before && after)
            {
                return true;
            }

            index = value.IndexOf(term, index + 1, StringComparison.Ordinal);
        }

        return false;
    }
}