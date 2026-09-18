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

    public IReadOnlyList<ReferenceLexicalOccurrence> LexicalOccurrences { get; init; } = [];
}

internal sealed record ReferenceLexicalOccurrence(
    string Dataset,
    string Name,
    string SpecificTerm);

internal static partial class ReferenceCatalogLoader
{
    private const int MaximumSurveyAnchorsPerTerm = 128;

    private static readonly Regex WiktionaryTitlePattern = new(
        "<a href=\"/wiki/[^\"#]+(?:#English)?\" title=\"(?<name>[^\"]+)\">",
        RegexOptions.CultureInvariant);

    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.CultureInvariant);

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
        LoadWikidata(Path.Combine(directory, "wikidata-colors.csv"), anchors);
        LoadUwColorNames(Path.Combine(directory, "uw-color-names.csv"), anchors);
        LoadStanfordColorReference(Path.Combine(directory, "stanford-color-reference.csv"), anchors);
        LoadIsccNbsDictionaries(Path.Combine(directory, "nbs-iscc-dictionaries"), anchors);

        var lexicalOccurrences = new List<ReferenceLexicalOccurrence>();
        LoadWiktionary(Path.Combine(directory, "wiktionary-colors.html"), lexicalOccurrences);
        LoadLexicalText(Path.Combine(directory, "ridgway-1912.txt"), "ridgway-1912", lexicalOccurrences);
        LoadLexicalText(Path.Combine(directory, "werner-1821.txt"), "werner-1821", lexicalOccurrences);

        var summaries = provenance.Values
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item =>
            {
                var sourceAnchors = anchors.Where(anchor => anchor.Dataset == item.Id).ToArray();
                return new ReferenceDatasetSummary(
                    item.Id,
                    sourceAnchors.Length,
                    sourceAnchors.Count(anchor => anchor.SemanticFamily != "Unknown"),
                    item.Source,
                    item.License,
                    item.Sha256)
                {
                    Independence = item.Independence,
                    IndependenceGroup = item.IndependenceGroup,
                    CachePolicy = item.CachePolicy,
                    SourceQuality = item.SourceQuality,
                    LexicalOccurrenceCount = lexicalOccurrences.Count(occurrence => occurrence.Dataset == item.Id)
                };
            })
            .ToArray();
        return new ReferenceCatalog(anchors, summaries)
        {
            LexicalOccurrences = lexicalOccurrences
                .DistinctBy(item => (item.Dataset, item.Name, item.SpecificTerm))
                .OrderBy(item => item.SpecificTerm, StringComparer.Ordinal)
                .ThenBy(item => item.Dataset, StringComparer.Ordinal)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToArray()
        };
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

    private static void LoadWikidata(string path, ICollection<ReferenceAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var fields = ParseCsv(line);
            if (fields.Count < 3 ||
                (fields[1].StartsWith('Q') && fields[1].Skip(1).All(char.IsDigit)) ||
                !TryParseHex(fields[2], out var rgb))
            {
                continue;
            }

            Add("wikidata-colors", fields[1], rgb, anchors);
        }
    }

    private static void LoadUwColorNames(string path, ICollection<ReferenceAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var lines = File.ReadLines(path).GetEnumerator();
        if (!lines.MoveNext())
        {
            return;
        }

        var columns = CreateColumnMap(ParseCsv(lines.Current));
        if (!HasColumns(columns, "participantId", "lang", "name", "colorSpace", "r", "g", "b",
                "trialNum", "tileNum", "studyVersion"))
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<PendingAnchor>();
        while (lines.MoveNext())
        {
            var fields = ParseCsv(lines.Current);
            if (!TryField(fields, columns, "lang", out var language) ||
                language != "English (English)" ||
                !TryField(fields, columns, "colorSpace", out var colorSpace) ||
                colorSpace != "rgb" ||
                !TryField(fields, columns, "name", out var name) ||
                SpecificColorTermNormalizer.NormalizeExact(name) is not { } term ||
                !TryByte(fields, columns, "r", out var red) ||
                !TryByte(fields, columns, "g", out var green) ||
                !TryByte(fields, columns, "b", out var blue))
            {
                continue;
            }

            var rowIdentity = string.Join('\u001f', fields);
            if (!seen.Add(rowIdentity))
            {
                continue;
            }

            pending.Add(new PendingAnchor(name.Trim(), term, new AuditRgb(red, green, blue), rowIdentity));
        }

        AddBounded("uw-labinthewild", pending, MaximumSurveyAnchorsPerTerm, anchors);
    }

    private static void LoadStanfordColorReference(string path, ICollection<ReferenceAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var lines = File.ReadLines(path).GetEnumerator();
        if (!lines.MoveNext())
        {
            return;
        }

        var columns = CreateColumnMap(ParseCsv(lines.Current));
        if (!HasColumns(columns, "gameid", "roundNum", "msgTime", "role", "contents", "source",
                "clickStatus", "clickColH", "clickColS", "clickColL",
                "alt1Status", "alt1ColH", "alt1ColS", "alt1ColL",
                "alt2Status", "alt2ColH", "alt2ColS", "alt2ColL"))
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<PendingAnchor>();
        while (lines.MoveNext())
        {
            var fields = ParseCsv(lines.Current);
            if (!TryField(fields, columns, "role", out var role) || role != "speaker" ||
                !TryField(fields, columns, "source", out var source) || source != "human" ||
                !TryField(fields, columns, "contents", out var name) ||
                SpecificColorTermNormalizer.NormalizeExact(name) is not { } term ||
                !TryTargetHsl(fields, columns, out var hue, out var saturation, out var lightness))
            {
                continue;
            }

            var rowIdentity = string.Join('\u001f', fields);
            if (!seen.Add(rowIdentity))
            {
                continue;
            }

            pending.Add(new PendingAnchor(
                name.Trim(),
                term,
                HslToSrgb(hue, saturation, lightness),
                rowIdentity));
        }

        AddBounded("stanford-color-reference", pending, MaximumSurveyAnchorsPerTerm, anchors);
    }

    private static void LoadIsccNbsDictionaries(string directory, ICollection<ReferenceAnchor> anchors)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var seen = new HashSet<(string Term, string Name, int Packed)>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.pm", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.Ordinal))
        {
            foreach (var line in File.ReadLines(path))
            {
                var match = IsccNbsDictionaryColorRegex().Match(line);
                if (!match.Success ||
                    !TryParseHex(match.Groups["hex"].Value, out var rgb))
                {
                    continue;
                }

                var name = match.Groups["name"].Value.Trim();
                if (SpecificColorTermNormalizer.NormalizeExact(name) is not { } term ||
                    !seen.Add((term, name.ToLowerInvariant(), rgb.Packed)))
                {
                    continue;
                }

                AddSpecific("iscc-nbs-dictionary", name, rgb, term, anchors);
            }
        }
    }

    private static void AddBounded(
        string dataset,
        IReadOnlyList<PendingAnchor> pending,
        int maximumPerTerm,
        ICollection<ReferenceAnchor> anchors)
    {
        foreach (var group in pending.GroupBy(item => item.Term, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(item => item.Rgb.Packed)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RowIdentity, StringComparer.Ordinal)
                .ToArray();
            var selected = ordered.Length <= maximumPerTerm
                ? ordered
                : Enumerable.Range(0, maximumPerTerm)
                    .Select(index => ordered[(int)((long)index * ordered.Length / maximumPerTerm)])
                    .ToArray();
            foreach (var item in selected)
            {
                AddSpecific(dataset, item.Name, item.Rgb, item.Term, anchors);
            }
        }
    }

    private static Dictionary<string, int> CreateColumnMap(IReadOnlyList<string> header) =>
        header.Select((name, index) => (Name: name.Trim(), Index: index))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.Ordinal);

    private static bool HasColumns(IReadOnlyDictionary<string, int> columns, params string[] names) =>
        names.All(columns.ContainsKey);

    private static bool TryField(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columns,
        string name,
        out string value)
    {
        if (columns.TryGetValue(name, out var index) && index < fields.Count)
        {
            value = fields[index];
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryByte(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columns,
        string name,
        out byte value)
    {
        value = 0;
        return TryField(fields, columns, name, out var text) &&
               byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryTargetHsl(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columns,
        out double hue,
        out double saturation,
        out double lightness)
    {
        foreach (var prefix in new[] { "click", "alt1", "alt2" })
        {
            if (TryField(fields, columns, prefix + "Status", out var status) &&
                status == "target" &&
                TryDouble(fields, columns, prefix + "ColH", out hue) &&
                TryDouble(fields, columns, prefix + "ColS", out saturation) &&
                TryDouble(fields, columns, prefix + "ColL", out lightness) &&
                hue is >= 0 and <= 360 &&
                saturation is >= 0 and <= 100 &&
                lightness is >= 0 and <= 100)
            {
                return true;
            }
        }

        hue = 0;
        saturation = 0;
        lightness = 0;
        return false;
    }

    private static bool TryDouble(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columns,
        string name,
        out double value)
    {
        value = 0;
        return TryField(fields, columns, name, out var text) &&
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
               double.IsFinite(value);
    }

    private static AuditRgb HslToSrgb(double hue, double saturation, double lightness)
    {
        var normalizedHue = hue % 360 / 60;
        var normalizedSaturation = saturation / 100;
        var normalizedLightness = lightness / 100;
        var chroma = (1 - Math.Abs(2 * normalizedLightness - 1)) * normalizedSaturation;
        var secondary = chroma * (1 - Math.Abs(normalizedHue % 2 - 1));
        var (red, green, blue) = normalizedHue switch
        {
            < 1 => (chroma, secondary, 0d),
            < 2 => (secondary, chroma, 0d),
            < 3 => (0d, chroma, secondary),
            < 4 => (0d, secondary, chroma),
            < 5 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };
        var offset = normalizedLightness - chroma / 2;
        return new AuditRgb(ToByte(red + offset), ToByte(green + offset), ToByte(blue + offset));
    }

    private static byte ToByte(double value) =>
        (byte)Math.Clamp((int)Math.Round(value * 255, MidpointRounding.AwayFromZero), 0, 255);

    private static void LoadWiktionary(
        string path,
        ICollection<ReferenceLexicalOccurrence> occurrences)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var html = File.ReadAllText(path);
        foreach (Match match in WiktionaryTitlePattern.Matches(html))
        {
            var name = System.Net.WebUtility.HtmlDecode(match.Groups["name"].Value);
            if (SpecificColorTermNormalizer.Normalize(name) is { } term)
            {
                occurrences.Add(new ReferenceLexicalOccurrence("wiktionary-colors", name, term));
            }
        }
    }

    private static void LoadLexicalText(
        string path,
        string dataset,
        ICollection<ReferenceLexicalOccurrence> occurrences)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var text = WhitespacePattern.Replace(File.ReadAllText(path), " ");
        foreach (var (term, alias) in SpecificColorTermNormalizer.FindOccurrences(text))
        {
            occurrences.Add(new ReferenceLexicalOccurrence(dataset, alias, term));
        }
    }

    private static void Add(
        string dataset,
        string name,
        AuditRgb rgb,
        ICollection<ReferenceAnchor> anchors)
    {
        var lab = Fovium.ColorSemantics.OklabColor.FromSrgb(rgb.Red, rgb.Green, rgb.Blue);
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

    private static void AddSpecific(
        string dataset,
        string name,
        AuditRgb rgb,
        string specificTerm,
        ICollection<ReferenceAnchor> anchors)
    {
        var lab = Fovium.ColorSemantics.OklabColor.FromSrgb(rgb.Red, rgb.Green, rgb.Blue);
        anchors.Add(new ReferenceAnchor(
            dataset,
            name,
            rgb,
            SemanticNameNormalizer.Normalize(name),
            lab.L,
            lab.A,
            lab.B)
        {
            SpecificTerm = specificTerm
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

    private sealed record ProvenanceItem(
        string Id,
        string Source,
        string License,
        string Sha256,
        string Independence = "Uncertain",
        string IndependenceGroup = "",
        string CachePolicy = "IgnoredCacheOnly",
        string SourceQuality = "UncertainProvenance");

    private sealed record PendingAnchor(string Name, string Term, AuditRgb Rgb, string RowIdentity);

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

    [GeneratedRegex(
        @"^\s*(?<name>.+?)\s{2,}\S+\s+#(?<hex>[0-9a-fA-F]{6})\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IsccNbsDictionaryColorRegex();
}

internal static class SpecificColorTermNormalizer
{
    private static readonly (string Term, string[] Aliases)[] Rules =
    [
        // Discovery vocabulary intentionally exceeds the shipped product terms.
        // It is audit-only: repeated, distance-qualified anchors nominate a
        // research cluster but never become a runtime classification rule.
        // Compound aliases precede their broader tokens so discovery keeps the
        // conventional specific identity rather than collapsing into a parent.
        ("Alabaster", ["alabaster"]),
        ("AlizarinCrimson", ["alizarin crimson", "alizarine crimson"]),
        ("AliceBlue", ["alice blue"]),
        ("Almond", ["almond"]),
        ("Amaranth", ["amaranth"]),
        ("Artichoke", ["artichoke"]),
        ("AshGray", ["ash gray", "ash grey"]),
        ("Asparagus", ["asparagus"]),
        ("Auburn", ["auburn"]),
        ("BabyPink", ["baby pink"]),
        ("BattleshipGray", ["battleship gray", "battleship grey"]),
        ("Beaver", ["beaver"]),
        ("Biscuit", ["biscuit"]),
        ("Bisque", ["bisque"]),
        ("BloodRed", ["blood red"]),
        ("Bone", ["bone white", "bone"]),
        ("BrickRed", ["brick red"]),
        ("BritishRacingGreen", ["british racing green"]),
        ("Buckskin", ["buckskin"]),
        ("Buff", ["buff"]),
        ("BurntOrange", ["burnt orange"]),
        ("CadetBlue", ["cadet blue"]),
        ("CadmiumRed", ["cadmium red"]),
        ("CadmiumYellow", ["cadmium yellow"]),
        ("CambridgeBlue", ["cambridge blue"]),
        ("Camel", ["camel"]),
        ("Cappuccino", ["cappuccino"]),
        ("Cardinal", ["cardinal red", "cardinal"]),
        ("Carnelian", ["carnelian"]),
        ("Carrot", ["carrot orange", "carrot"]),
        ("Cerise", ["cerise"]),
        ("Chamois", ["chamois"]),
        ("ChineseRed", ["chinese red"]),
        ("Cinnabar", ["cinnabar red", "cinnabar"]),
        ("Citrine", ["citrine"]),
        ("Citron", ["citron"]),
        ("Claret", ["claret"]),
        ("Cocoa", ["cocoa brown", "cocoa"]),
        ("Cochineal", ["cochineal red", "cochineal"]),
        ("CoolGray", ["cool gray", "cool grey"]),
        ("Daffodil", ["daffodil yellow", "daffodil"]),
        ("Dandelion", ["dandelion yellow", "dandelion"]),
        ("DesertSand", ["desert sand"]),
        ("DuckEggBlue", ["duck egg blue", "duck-egg blue"]),
        ("EauDeNil", ["eau de nil"]),
        ("Ebony", ["ebony"]),
        ("EgyptianBlue", ["egyptian blue"]),
        ("Eucalyptus", ["eucalyptus"]),
        ("Fawn", ["fawn"]),
        ("Firebrick", ["fire brick", "firebrick"]),
        ("Flame", ["flame red", "flame"]),
        ("Fuscous", ["fuscous"]),
        ("Gainsboro", ["gainsboro"]),
        ("Gamboge", ["gamboge"]),
        ("Garnet", ["garnet red", "garnet"]),
        ("GentianBlue", ["gentian blue"]),
        ("Geranium", ["geranium red", "geranium"]),
        ("Ginger", ["ginger"]),
        ("Glaucous", ["glaucous"]),
        ("Goldenrod", ["goldenrod"]),
        ("GreenEarth", ["terre verte", "green earth"]),
        ("Greige", ["greige"]),
        ("HarvestGold", ["harvest gold"]),
        ("Hazel", ["hazel"]),
        ("Heliotrope", ["heliotrope"]),
        ("Henna", ["henna"]),
        ("HotPink", ["hot pink"]),
        ("IndianRed", ["indian red"]),
        ("IndianYellow", ["indian yellow"]),
        ("IronGray", ["iron gray", "iron grey"]),
        ("Jet", ["jet black", "jet"]),
        ("Jonquil", ["jonquil"]),
        ("JungleGreen", ["jungle green"]),
        ("LapisLazuli", ["lapis lazuli"]),
        ("LaurelGreen", ["laurel green"]),
        ("Licorice", ["liquorice", "licorice"]),
        ("LincolnGreen", ["lincoln green"]),
        ("Loden", ["loden green", "loden"]),
        ("Madder", ["rose madder", "madder red", "madder"]),
        ("Magnolia", ["magnolia"]),
        ("Malachite", ["malachite green", "malachite"]),
        ("Manatee", ["manatee gray", "manatee grey", "manatee"]),
        ("Mocha", ["mocha"]),
        ("MummyBrown", ["mummy brown"]),
        ("Myrtle", ["myrtle green", "myrtle"]),
        ("NileBlue", ["nile blue"]),
        ("Oatmeal", ["oatmeal"]),
        ("OldGold", ["old gold"]),
        ("Onyx", ["onyx"]),
        ("Oxblood", ["oxblood red", "oxblood"]),
        ("Oyster", ["oyster white", "oyster"]),
        ("Paprika", ["paprika"]),
        ("ParisGreen", ["paris green"]),
        ("PeacockBlue", ["peacock blue"]),
        ("PeaGreen", ["pea green"]),
        ("PearlGray", ["pearl gray", "pearl grey"]),
        ("PhthaloBlue", ["phthalocyanine blue", "phthalo blue"]),
        ("PhthaloGreen", ["phthalocyanine green", "phthalo green"]),
        ("Platinum", ["platinum gray", "platinum grey", "platinum"]),
        ("Pomegranate", ["pomegranate red", "pomegranate"]),
        ("PompeianRed", ["pompeian red"]),
        ("Poppy", ["poppy red", "poppy"]),
        ("Primrose", ["primrose yellow", "primrose"]),
        ("Puce", ["puce"]),
        ("Putty", ["putty"]),
        ("Quartz", ["quartz gray", "quartz grey", "quartz"]),
        ("RawSienna", ["raw sienna", "natural sienna"]),
        ("RifleGreen", ["rifle green"]),
        ("RobinsEggBlue", ["robin's egg blue", "robin egg blue"]),
        ("RoyalPurple", ["royal purple"]),
        ("Russet", ["russet brown", "russet"]),
        ("Sable", ["sable brown", "sable"]),
        ("SaddleBrown", ["saddle brown"]),
        ("Sangria", ["sangria red", "sangria"]),
        ("SaxeBlue", ["saxe blue"]),
        ("Seashell", ["sea shell", "seashell"]),
        ("ShockingPink", ["shocking pink"]),
        ("SlateBlue", ["slate blue"]),
        ("SpringGreen", ["spring green"]),
        ("Straw", ["straw yellow", "straw"]),
        ("Strawberry", ["strawberry red", "strawberry"]),
        ("Sunflower", ["sunflower yellow", "sunflower"]),
        ("Tawny", ["tawny brown", "tawny"]),
        ("Teak", ["teak brown", "teak"]),
        ("Timberwolf", ["timberwolf gray", "timberwolf grey", "timberwolf"]),
        ("TyrianPurple", ["tyrian purple"]),
        ("VandykeBrown", ["vandyke brown", "van dyke brown"]),
        ("VenetianRed", ["venetian red"]),
        ("Verdigris", ["verdigris"]),
        ("Walnut", ["walnut brown", "walnut"]),
        ("WedgwoodBlue", ["wedgwood blue"]),
        ("Wenge", ["wenge"]),
        ("Zaffre", ["zaffre blue", "zaffre"]),
        ("Aubergine", ["aubergine", "eggplant"]),
        ("Amethyst", ["amethyst"]),
        ("Orchid", ["orchid"]),
        ("Raspberry", ["raspberry"]),
        ("Cranberry", ["cranberry red", "cranberry"]),
        ("Mulberry", ["mulberry"]),
        ("Wisteria", ["wisteria"]),
        ("Heather", ["heather"]),
        ("Plum", ["plum"]),
        ("Fuchsia", ["fuchsia"]),
        ("CornflowerBlue", ["cornflower blue", "cornflower"]),
        ("MidnightBlue", ["midnight blue"]),
        ("PrussianBlue", ["prussian blue"]),
        ("Ultramarine", ["ultramarine blue", "ultramarine"]),
        ("ElectricBlue", ["electric blue"]),
        ("BabyBlue", ["baby blue"]),
        ("PetrolBlue", ["petrol blue"]),
        ("OxfordBlue", ["oxford blue"]),
        ("BottleGreen", ["bottle-green", "bottle green"]),
        ("Viridian", ["viridian green", "viridian"]),
        ("HunterGreen", ["hunter green"]),
        ("KellyGreen", ["kelly green"]),
        ("Avocado", ["avocado green", "avocado"]),
        ("Fern", ["fern green", "fern"]),
        ("PineGreen", ["pine green"]),
        ("Saffron", ["saffron"]),
        ("Marigold", ["marigold"]),
        ("BurntSienna", ["burnt sienna"]),
        ("BurntUmber", ["burnt umber"]),
        ("RawUmber", ["natural umber", "raw umber"]),
        ("Sienna", ["sienna"]),
        ("Umber", ["umber"]),
        ("Chocolate", ["chocolate brown", "chocolate"]),
        ("Coffee", ["coffee brown", "coffee"]),
        ("Sepia", ["sepia"]),
        ("Bronze", ["bronze"]),
        ("Vermilion", ["vermilion"]),
        ("Carmine", ["carmine"]),
        ("Cherry", ["cherry red", "cherry"]),
        ("Ruby", ["ruby red", "ruby"]),
        ("Tomato", ["tomato red", "tomato"]),
        ("Eggshell", ["egg shell", "eggshell"]),
        ("Parchment", ["parchment"]),
        ("AntiqueWhite", ["antique white"]),
        ("DoveGray", ["dove gray", "dove grey"]),
        ("Gunmetal", ["gun metal", "gunmetal"]),
        ("Espresso", ["espresso"]),
        ("NaplesYellow", ["naples yellow"]),
        ("PaynesGray", ["payne's grey", "payne's gray", "payne grey", "payne gray"]),
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
        ("Sapphire", ["sapphire blue", "sapphire"]),
        ("Jade", ["jade"]),
        ("Moss", ["moss"]),
        ("Khaki", ["khaki"]),
        ("Celadon", ["celadon green", "celadon"]),
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
        ("Vanilla", ["vanilla cream", "vanilla"]),
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

    private static readonly IReadOnlyList<(string Term, string Alias)> AliasRules = Rules
        .SelectMany(rule => rule.Aliases.Select(alias => (rule.Term, Alias: alias)))
        .OrderByDescending(rule => rule.Alias.Length)
        .ThenBy(rule => rule.Term, StringComparer.Ordinal)
        .ToArray();

    private static readonly IReadOnlyDictionary<string, string> ExactAliases = Rules
        .SelectMany(rule => rule.Aliases.Select(alias => (rule.Term, Alias: NormalizeExactText(alias))))
        .ToDictionary(rule => rule.Alias, rule => rule.Term, StringComparer.Ordinal);

    internal static IReadOnlyList<(string Term, string[] Aliases)> Vocabulary => Rules;

    public static string? Normalize(string name)
    {
        var normalized = name.Trim().ToLowerInvariant().Replace('_', ' ');
        if (normalized.Contains("navy green", StringComparison.Ordinal))
        {
            return null;
        }

        // These are documented homonyms/marketing compounds, not numeric
        // evidence for the conventional red cinnabar or yellow primrose core.
        if (normalized.Contains("cinnabar green", StringComparison.Ordinal) ||
            (normalized.Contains("primrose", StringComparison.Ordinal) &&
             normalized != "primrose yellow"))
        {
            return null;
        }

        foreach (var (term, alias) in AliasRules)
        {
            if (ContainsToken(normalized, alias))
            {
                return term;
            }
        }

        return null;
    }

    public static string? NormalizeExact(string name) =>
        ExactAliases.GetValueOrDefault(NormalizeExactText(name));

    private static string NormalizeExactText(string value) => string.Join(
        ' ',
        value.Trim()
            .ToLowerInvariant()
            .Replace('’', '\'')
            .Replace('_', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal static IReadOnlyList<(string Term, string Alias)> FindOccurrences(string text)
    {
        var normalized = text.ToLowerInvariant();
        var occupied = new bool[normalized.Length];
        var occurrences = new List<(string Term, string Alias)>();
        foreach (var (term, alias) in AliasRules)
        {
            var index = normalized.IndexOf(alias, StringComparison.Ordinal);
            while (index >= 0)
            {
                var end = index + alias.Length;
                var before = index == 0 || !char.IsLetterOrDigit(normalized[index - 1]);
                var after = end == normalized.Length || !char.IsLetterOrDigit(normalized[end]);
                if (before && after && !occupied.AsSpan(index, alias.Length).Contains(true))
                {
                    occupied.AsSpan(index, alias.Length).Fill(true);
                    occurrences.Add((term, alias));
                }

                index = normalized.IndexOf(alias, index + 1, StringComparison.Ordinal);
            }
        }

        return occurrences
            .Distinct()
            .OrderBy(item => item.Term, StringComparer.Ordinal)
            .ThenBy(item => item.Alias, StringComparer.Ordinal)
            .ToArray();
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