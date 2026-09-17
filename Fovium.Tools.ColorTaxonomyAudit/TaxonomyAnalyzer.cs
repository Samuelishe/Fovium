namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class TaxonomyAnalyzer
{
    private static readonly string[] OwnerSeedHex =
    [
        "#755A13", "#57420D", "#7E661C", "#755B1E", "#9A8045", "#5F4C22", "#3C341D",
        "#6E6336", "#505E23", "#6A693D", "#5F6058", "#BC9763", "#B2927D", "#B6B3A4",
        "#D3F5FF", "#666577", "#FF634A"
    ];

    public static AuditReport Analyze(
        AuditOptions options,
        StructuredGrid structured,
        IReadOnlyList<AuditClassification> monteCarlo,
        IReadOnlyList<AuditClassification> rgbGrid,
        IReadOnlyList<AuditClassification> boundaryRefinement,
        ReferenceCatalog references,
        double runtimeSeconds)
    {
        var all = structured.Nodes.Values
            .Concat(monteCarlo)
            .Concat(rgbGrid)
            .Concat(boundaryRefinement)
            .GroupBy(sample => sample.Rgb.Packed)
            .Select(group => group.First())
            .OrderBy(sample => sample.Rgb.Packed)
            .ToArray();
        var anomalies = new List<AuditAnomaly>();
        var boundaryCandidates = new List<AuditClassification>();
        var boundaryEdges = AnalyzeBoundaryEdges(structured, anomalies, boundaryCandidates);
        var abruptDiscontinuities = anomalies.Count(anomaly => anomaly.Kind == "AbruptBoundary");
        var adapter = new ProductionColorAdapter();
        var chromaReversals = AnalyzeRoleMonotonicity(
            structured,
            options.Configuration,
            anomalies);
        var lightnessOscillations = AnalyzeFamilyOscillations(
            structured,
            options.Configuration,
            anomalies);
        var modifierReversals = AnalyzeModifierMonotonicity(structured, anomalies);
        var (componentCounts, tinyComponents, thinSlivers) = AnalyzeComponents(structured, anomalies);

        var ownerSeeds = OwnerSeedHex.Select(hex => adapter.Classify(ParseHex(hex))).ToArray();
        var referenceCandidates = ownerSeeds
            .Concat(boundaryCandidates)
            .Concat(anomalies.Select(anomaly => anomaly.Sample))
            .GroupBy(sample => sample.Rgb.Packed)
            .Select(group => group.First())
            .OrderBy(sample => OwnerSeedHex.Contains(sample.Rgb.Hex, StringComparer.Ordinal) ? 0 : 1)
            .ThenBy(sample => sample.Rgb.Packed)
            .Take(options.Configuration.ReferenceCandidateLimit)
            .ToArray();
        var referenceDisagreements = AnalyzeReferences(referenceCandidates, references, anomalies);

        var ranked = ClusterAndRank(anomalies);
        var metrics = new AuditMetrics(
            all.Length,
            structured.Nodes.Count,
            monteCarlo.Count,
            rgbGrid.Count,
            boundaryRefinement.Count,
            boundaryEdges,
            abruptDiscontinuities,
            chromaReversals,
            lightnessOscillations,
            modifierReversals,
            tinyComponents,
            thinSlivers,
            referenceDisagreements,
            ranked.Count(anomaly => anomaly.Severity == "High"),
            ranked.Count(anomaly => anomaly.Severity == "Medium"),
            runtimeSeconds);

        return new AuditReport(
            "fovium-color-taxonomy-audit/v1",
            options.Mode.ToString(),
            options.Seed,
            options.Configuration,
            metrics,
            CountBy(all, sample => sample.Family),
            CountBy(all, sample => sample.Role),
            componentCounts,
            references.Summaries,
            ownerSeeds,
            ranked,
            null);
    }

    private static int AnalyzeBoundaryEdges(
        StructuredGrid grid,
        ICollection<AuditAnomaly> anomalies,
        ICollection<AuditClassification> candidates)
    {
        var count = 0;
        foreach (var (key, sample) in grid.Nodes.OrderBy(pair => pair.Key.Lightness)
                     .ThenBy(pair => pair.Key.Chroma)
                     .ThenBy(pair => pair.Key.Hue))
        {
            foreach (var neighborKey in ForwardNeighbors(key, grid))
            {
                if (!grid.Nodes.TryGetValue(neighborKey, out var neighbor) || sample.Family == neighbor.Family)
                {
                    continue;
                }

                count++;
                candidates.Add(sample);
                var delta = DeltaE(sample, neighbor);
                var gap = SemanticGap(sample, neighbor);
                if (delta <= 0.035 && gap >= 3)
                {
                    var score = 72 + gap * 6 + (0.035 - delta) * 200;
                    anomalies.Add(CreateAnomaly(
                        "AbruptBoundary",
                        score,
                        $"Close colors jump from {sample.Family} to {neighbor.Family} (semantic gap {gap}).",
                        sample,
                        neighbor,
                        delta));
                }
            }
        }

        return count;
    }

    private static int AnalyzeRoleMonotonicity(
        StructuredGrid grid,
        AuditConfiguration configuration,
        ICollection<AuditAnomaly> anomalies)
    {
        var count = 0;
        foreach (var line in grid.Nodes.GroupBy(pair => (pair.Key.Lightness, pair.Key.Hue)))
        {
            var ordered = line.OrderBy(pair => pair.Key.Chroma).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var current = ordered[index];
                var previous = ordered[index - 1];
                var currentRole = ProductionColorAdapter.ClassifyRole(
                    current.Key.Lightness * configuration.LightnessStep,
                    current.Key.Chroma * configuration.ChromaStep,
                    current.Key.Hue * configuration.HueStep);
                var previousRole = ProductionColorAdapter.ClassifyRole(
                    previous.Key.Lightness * configuration.LightnessStep,
                    previous.Key.Chroma * configuration.ChromaStep,
                    previous.Key.Hue * configuration.HueStep);
                if (RoleRank(currentRole) >= RoleRank(previousRole))
                {
                    continue;
                }

                count++;
                anomalies.Add(CreateAnomaly(
                    "ChromaRoleReversal",
                    92,
                    $"Increasing chroma reverses role {previousRole} → {currentRole}.",
                    current.Value,
                    previous.Value,
                    DeltaE(current.Value, previous.Value)));
            }
        }

        return count;
    }

    private static int AnalyzeFamilyOscillations(
        StructuredGrid grid,
        AuditConfiguration configuration,
        ICollection<AuditAnomaly> anomalies)
    {
        var count = 0;
        foreach (var line in grid.Nodes.GroupBy(pair => (pair.Key.Chroma, pair.Key.Hue)))
        {
            var sequence = CompressFamilies(
                line.OrderBy(pair => pair.Key.Lightness),
                configuration);
            for (var index = 2; index < sequence.Count; index++)
            {
                if (sequence[index - 2].Family != sequence[index].Family ||
                    sequence[index - 1].Family == sequence[index].Family)
                {
                    continue;
                }

                count++;
                anomalies.Add(CreateAnomaly(
                    "LightnessOscillation",
                    64,
                    $"Lightness path oscillates {sequence[index].Family} → {sequence[index - 1].Family} → {sequence[index].Family}.",
                    sequence[index - 1].Sample,
                    sequence[index].Sample,
                    DeltaE(sequence[index - 1].Sample, sequence[index].Sample)));
            }
        }

        return count;
    }

    private static int AnalyzeModifierMonotonicity(StructuredGrid grid, ICollection<AuditAnomaly> anomalies)
    {
        var count = 0;
        count += CountRankReversals(
            grid.Nodes.GroupBy(pair => (pair.Key.Chroma, pair.Key.Hue)),
            pair => pair.Key.Lightness,
            pair => LightnessRank(pair.Value.Lightness),
            "LightnessModifierReversal",
            anomalies);
        count += CountRankReversals(
            grid.Nodes.GroupBy(pair => (pair.Key.Lightness, pair.Key.Hue)),
            pair => pair.Key.Chroma,
            pair => ChromaRank(pair.Value.Chroma),
            "ChromaModifierReversal",
            anomalies);
        return count;
    }

    private static int CountRankReversals<TKey>(
        IEnumerable<IGrouping<TKey, KeyValuePair<GridKey, AuditClassification>>> lines,
        Func<KeyValuePair<GridKey, AuditClassification>, int> order,
        Func<KeyValuePair<GridKey, AuditClassification>, int> rank,
        string kind,
        ICollection<AuditAnomaly> anomalies)
        where TKey : notnull
    {
        var count = 0;
        foreach (var line in lines)
        {
            var ordered = line.OrderBy(order).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                if (rank(ordered[index]) >= rank(ordered[index - 1]))
                {
                    continue;
                }

                count++;
                anomalies.Add(CreateAnomaly(
                    kind,
                    86,
                    $"Monotonic modifier order reverses between {ordered[index - 1].Value.Rgb.Hex} and {ordered[index].Value.Rgb.Hex}.",
                    ordered[index].Value,
                    ordered[index - 1].Value,
                    DeltaE(ordered[index].Value, ordered[index - 1].Value)));
            }
        }

        return count;
    }

    private static (IReadOnlyDictionary<string, int> Counts, int Tiny, int Thin) AnalyzeComponents(
        StructuredGrid grid,
        ICollection<AuditAnomaly> anomalies)
    {
        var visited = new HashSet<GridKey>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var tiny = 0;
        var thin = 0;
        foreach (var start in grid.Nodes.Keys.OrderBy(key => key.Lightness).ThenBy(key => key.Chroma)
                     .ThenBy(key => key.Hue))
        {
            if (!visited.Add(start))
            {
                continue;
            }

            var family = grid.Nodes[start].Family;
            var queue = new Queue<GridKey>();
            var component = new List<GridKey>();
            queue.Enqueue(start);
            while (queue.TryDequeue(out var current))
            {
                component.Add(current);
                foreach (var neighbor in AllNeighbors(current, grid))
                {
                    if (grid.Nodes.TryGetValue(neighbor, out var sample) &&
                        sample.Family == family &&
                        visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            counts[family] = counts.GetValueOrDefault(family) + 1;
            if (component.Count <= 3)
            {
                tiny++;
                anomalies.Add(CreateAnomaly(
                    "TinyIsland",
                    68,
                    $"{family} component contains only {component.Count} structured cells.",
                    grid.Nodes[start]));
            }

            var lightnessWidth = component.Max(key => key.Lightness) - component.Min(key => key.Lightness) + 1;
            var chromaWidth = component.Max(key => key.Chroma) - component.Min(key => key.Chroma) + 1;
            var hueWidth = CircularHueWidth(component.Select(key => key.Hue), grid.HueCount);
            if (component.Count >= 4 && Math.Min(lightnessWidth, Math.Min(chromaWidth, hueWidth)) == 1)
            {
                thin++;
                anomalies.Add(CreateAnomaly(
                    "ThinSliver",
                    58,
                    $"{family} component is one structured cell thick on at least one axis.",
                    grid.Nodes[start]));
            }
        }

        return (new SortedDictionary<string, int>(counts, StringComparer.Ordinal), tiny, thin);
    }

    private static int AnalyzeReferences(
        IReadOnlyList<AuditClassification> candidates,
        ReferenceCatalog catalog,
        ICollection<AuditAnomaly> anomalies)
    {
        if (catalog.Anchors.Count == 0)
        {
            return 0;
        }

        var datasets = catalog.Anchors
            .Where(anchor => anchor.SemanticFamily != "Unknown")
            .GroupBy(anchor => anchor.Dataset, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        var count = 0;
        foreach (var candidate in candidates)
        {
            var neighbors = new List<AuditReferenceNeighbor>();
            var votes = new List<string>();
            foreach (var dataset in datasets)
            {
                var nearest = dataset
                    .Select(anchor => (Anchor: anchor, Delta: DeltaE(candidate, anchor)))
                    .OrderBy(item => item.Delta)
                    .ThenBy(item => item.Anchor.Name, StringComparer.Ordinal)
                    .Take(3)
                    .ToArray();
                neighbors.AddRange(nearest.Select(item => new AuditReferenceNeighbor(
                    item.Anchor.Dataset,
                    item.Anchor.Name,
                    item.Anchor.Rgb.Hex,
                    item.Anchor.SemanticFamily,
                    item.Delta)));
                var vote = nearest
                    .GroupBy(item => item.Anchor.SemanticFamily, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Average(item => item.Delta))
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .First().Key;
                votes.Add(vote);
            }

            var consensus = votes.GroupBy(vote => vote, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .First();
            var productSemantic = ProductSemantic(candidate);
            if (consensus.Count() < 2 || consensus.Key == productSemantic)
            {
                continue;
            }

            count++;
            var isOwnerSeed = OwnerSeedHex.Contains(candidate.Rgb.Hex, StringComparer.Ordinal);
            var score = 62 + consensus.Count() * 8 + (isOwnerSeed ? 8 : 0);
            anomalies.Add(CreateAnomaly(
                "ReferenceDisagreement",
                score,
                $"{consensus.Count()} datasets support {consensus.Key}; Fovium maps to {productSemantic} ({candidate.Family}).",
                candidate,
                references: neighbors.OrderBy(item => item.Dataset, StringComparer.Ordinal)
                    .ThenBy(item => item.DeltaE)
                    .ToArray()));
        }

        return count;
    }

    private static IReadOnlyList<AuditAnomaly> ClusterAndRank(IEnumerable<AuditAnomaly> anomalies)
    {
        return anomalies
            .GroupBy(
                anomaly =>
                    $"{anomaly.Kind}|{anomaly.Sample.Family}|{anomaly.Neighbor?.Family}|{NormalizeReason(anomaly.Reason)}",
                StringComparer.Ordinal)
            .SelectMany(group => group.OrderByDescending(anomaly => anomaly.Score)
                .ThenBy(anomaly => anomaly.Sample.Rgb.Packed)
                .Take(3))
            .OrderByDescending(anomaly => anomaly.Score)
            .ThenBy(anomaly => anomaly.Kind, StringComparer.Ordinal)
            .ThenBy(anomaly => anomaly.Sample.Rgb.Packed)
            .Take(300)
            .Select((anomaly, index) => anomaly with { Id = $"A{index + 1:000}" })
            .ToArray();
    }

    private static AuditAnomaly CreateAnomaly(
        string kind,
        double score,
        string reason,
        AuditClassification sample,
        AuditClassification? neighbor = null,
        double? delta = null,
        IReadOnlyList<AuditReferenceNeighbor>? references = null) =>
        new(
            string.Empty,
            score >= 80 ? "High" : score >= 55 ? "Medium" : "Low",
            Math.Round(score, 3),
            kind,
            reason,
            sample,
            neighbor,
            delta is null ? null : Math.Round(delta.Value, 6),
            references ?? []);

    private static IEnumerable<GridKey> ForwardNeighbors(GridKey key, StructuredGrid grid)
    {
        if (key.Lightness + 1 < grid.LightnessCount)
        {
            yield return key with { Lightness = key.Lightness + 1 };
        }

        if (key.Chroma + 1 < grid.ChromaCount)
        {
            yield return key with { Chroma = key.Chroma + 1 };
        }

        yield return key with { Hue = (key.Hue + 1) % grid.HueCount };
    }

    private static IEnumerable<GridKey> AllNeighbors(GridKey key, StructuredGrid grid)
    {
        if (key.Lightness > 0)
        {
            yield return key with { Lightness = key.Lightness - 1 };
        }

        if (key.Lightness + 1 < grid.LightnessCount)
        {
            yield return key with { Lightness = key.Lightness + 1 };
        }

        if (key.Chroma > 0)
        {
            yield return key with { Chroma = key.Chroma - 1 };
        }

        if (key.Chroma + 1 < grid.ChromaCount)
        {
            yield return key with { Chroma = key.Chroma + 1 };
        }

        yield return key with { Hue = (key.Hue + grid.HueCount - 1) % grid.HueCount };
        yield return key with { Hue = (key.Hue + 1) % grid.HueCount };
    }

    private static int CircularHueWidth(IEnumerable<int> values, int count)
    {
        var ordered = values.Distinct().Order().ToArray();
        if (ordered.Length <= 1)
        {
            return ordered.Length;
        }

        var largestGap = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            var next = index + 1 < ordered.Length ? ordered[index + 1] : ordered[0] + count;
            largestGap = Math.Max(largestGap, next - ordered[index]);
        }

        return count - largestGap + 1;
    }

    private static List<FamilyPoint> CompressFamilies(
        IEnumerable<KeyValuePair<GridKey, AuditClassification>> points,
        AuditConfiguration configuration)
    {
        var result = new List<FamilyPoint>();
        foreach (var (key, sample) in points)
        {
            var family = ProductionColorAdapter.ClassifyFamily(
                key.Lightness * configuration.LightnessStep,
                key.Chroma * configuration.ChromaStep,
                key.Hue * configuration.HueStep);
            if (result.Count == 0 || result[^1].Family != family)
            {
                result.Add(new FamilyPoint(family, sample));
            }
        }

        return result;
    }

    private sealed record FamilyPoint(string Family, AuditClassification Sample);

    private static IReadOnlyDictionary<string, int> CountBy(
        IEnumerable<AuditClassification> samples,
        Func<AuditClassification, string> selector) =>
        new SortedDictionary<string, int>(
            samples.GroupBy(selector, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static double DeltaE(AuditClassification left, AuditClassification right)
    {
        var deltaL = left.LabL - right.LabL;
        var deltaA = left.LabA - right.LabA;
        var deltaB = left.LabB - right.LabB;
        return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
    }

    private static double DeltaE(AuditClassification left, ReferenceAnchor right)
    {
        var deltaL = left.LabL - right.LabL;
        var deltaA = left.LabA - right.LabA;
        var deltaB = left.LabB - right.LabB;
        return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
    }

    private static int SemanticGap(AuditClassification first, AuditClassification second)
    {
        var firstSemantic = ProductSemantic(first);
        var secondSemantic = ProductSemantic(second);
        var firstAngle = HueAngle(firstSemantic);
        var secondAngle = HueAngle(secondSemantic);
        if (firstAngle is null || secondAngle is null)
        {
            return firstSemantic == secondSemantic ? 0 : 2;
        }

        var direct = Math.Abs(firstAngle.Value - secondAngle.Value);
        return (int)Math.Ceiling(Math.Min(direct, 360 - direct) / 30);
    }

    private static double? HueAngle(string semantic) => semantic switch
    {
        "Red" => 0,
        "Burgundy" => 10,
        "Coral" => 25,
        "Terracotta" => 40,
        "Orange" => 50,
        "Brown" => 60,
        "Ochre" => 80,
        "Yellow" => 100,
        "Olive" => 110,
        "YellowGreen" => 125,
        "Green" => 145,
        "Turquoise" => 180,
        "Cyan" => 210,
        "Blue" => 250,
        "Violet" => 295,
        "Magenta" => 330,
        "Pink" => 350,
        "Crimson" => 355,
        _ => null
    };

    internal static string ProductSemantic(string family) => family switch
    {
        "Neutral" or "WarmGray" or "CoolGray" or "BlueGray" or "GreenGray" or "OliveGray" or
            "RoseGray" or "VioletGray" or "LilacGray" => "Gray",
        "Greige" or "Beige" or "Sand" or "Taupe" => "Beige",
        "Cream" => "Cream",
        "Peach" or "Apricot" => "Peach",
        "Ochre" or "Mustard" => "Ochre",
        "Terracotta" => "Terracotta",
        "Red" or "RedOrange" => "Red",
        "Coral" => "Coral",
        "Orange" or "Amber" => "Orange",
        "Yellow" => "Yellow",
        "YellowGreen" => "YellowGreen",
        "Olive" or "OliveGreen" => "Olive",
        "Green" => "Green",
        "Turquoise" or "TurquoiseCyan" => "Turquoise",
        "Cyan" or "CyanBlue" => "Cyan",
        "Blue" => "Blue",
        "BlueViolet" or "Violet" or "PinkLilac" => "Violet",
        "Magenta" or "RedMagenta" => "Magenta",
        "Pink" or "Rose" => "Pink",
        "Crimson" => "Crimson",
        "Burgundy" => "Burgundy",
        "Brown" => "Brown",
        _ => family,
    };

    internal static string ProductSemantic(AuditClassification sample) => sample.Role switch
    {
        "NearBlack" => "Black",
        "NearWhite" when sample.Family == "Cream" => "Cream",
        "NearWhite" => "White",
        _ => ProductSemantic(sample.Family),
    };

    private static int RoleRank(string role) => role switch
    {
        "Neutral" or "NearBlack" or "NearWhite" => 0,
        "NearNeutral" => 1,
        "TintedNeutral" => 2,
        "Chromatic" => 3,
        _ => -1,
    };

    private static int LightnessRank(string value) => value switch
    {
        "VeryDark" => 0,
        "Dark" => 1,
        "Medium" => 2,
        "Light" => 3,
        "VeryLight" => 4,
        _ => -1,
    };

    private static int ChromaRank(string value) => value switch
    {
        "Neutral" => 0,
        "Muted" => 1,
        "Moderate" => 2,
        "Saturated" => 3,
        "Vivid" => 4,
        _ => -1,
    };

    private static AuditRgb ParseHex(string hex) => new(
        Convert.ToByte(hex.Substring(1, 2), 16),
        Convert.ToByte(hex.Substring(3, 2), 16),
        Convert.ToByte(hex.Substring(5, 2), 16));

    private static string NormalizeReason(string reason)
    {
        var arrow = reason.IndexOf('→');
        return arrow < 0 ? reason.Split('(')[0].Trim() : reason[..arrow].Trim();
    }
}