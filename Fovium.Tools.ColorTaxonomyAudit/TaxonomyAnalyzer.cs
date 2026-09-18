namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class TaxonomyAnalyzer
{
    private static readonly string[] OwnerSeedHex =
    [
        "#755A13", "#57420D", "#7E661C", "#755B1E", "#9A8045", "#5F4C22", "#3C341D",
        "#6E6336", "#505E23", "#6A693D", "#5F6058", "#BC9763", "#B2927D", "#B6B3A4",
        "#D3F5FF", "#666577", "#FF634A"
    ];

    private static readonly (string Region, string Hex)[] OwnerCandidateHex =
    [
        ("Coral / red-orange / orange", "#FF6B0A"),
        ("Brown / terracotta", "#C95E3A"),
        ("Burgundy / red-magenta", "#321020"),
        ("Mint", "#ADF0D1"), ("Mint", "#CDFFCC"), ("Mint", "#7EFFD4"), ("Mint", "#C7FCEC"),
        ("Warm near-white", "#F8EFD2"), ("Warm near-white", "#F7F5E6"),
        ("Warm near-white", "#FEF8DE"), ("Warm near-white", "#F8F0DB"),
        ("Warm near-white", "#F0DEC8"), ("Warm near-white", "#F8E8D8"),
        ("Warm near-white", "#F4F2E3"), ("Warm near-white", "#EEEDDB"),
        ("Warm near-white", "#FEF2CA"), ("Warm near-white", "#E6D4C0"),
        ("Warm rose", "#DFAAA4"), ("Warm rose", "#D4A299"),
        ("Violet / purple", "#A562B1"), ("Violet / purple", "#8A4794"),
        ("Yellow / yellow-green", "#D4D88E"), ("Yellow / yellow-green", "#D5DB5D"),
        ("Yellow / yellow-green", "#C7CD75"), ("Yellow / yellow-green", "#CBDFA2"),
        ("Yellow / yellow-green", "#CBD97A"), ("Yellow / yellow-green", "#D5E29C"),
        ("Yellow / yellow-green", "#CBD862"), ("Yellow / yellow-green", "#AFBC4A"),
        ("Yellow / yellow-green", "#B9D147"),
        ("F8 Indigo", "#4B0082"), ("F8 Indigo", "#380282"),
        ("F8 Powder blue", "#B0E0E6"), ("F8 Powder blue", "#B1D1FC"),
        ("F8 Steel blue", "#4682B4"), ("F8 Steel blue", "#5A7D9A"),
        ("F8 Olive drab", "#6B8E23"), ("F8 Olive drab", "#6F7632"),
        ("F8 Lime", "#00FF00"), ("F8 Lime", "#AAFF32"),
        ("F8 Chartreuse", "#7FFF00"), ("F8 Chartreuse", "#C1F80A"),
        ("F8 Seafoam", "#80F9AD"), ("F8 Seafoam", "#3EAF76"),
        ("F8 Lilac", "#CEA2FD"), ("F8 Lilac", "#9C6DA5"),
        ("F8 Mauve", "#AE7181"), ("F8 Mauve", "#C292A1"),
        ("F8 Cobalt", "#0047AB"), ("F8 Cobalt", "#1E488F"),
        ("F8 Cerulean", "#007BA7"), ("F8 Cerulean", "#0485D1"),
        ("F8 Blood orange", "#FE4B03"), ("F8 Pumpkin", "#E17701"),
        ("F8 Blush", "#F29E8E"), ("F8 Pistachio", "#C0FA8B"),
        ("F8 Linen", "#FAF0E6")
    ];

    private static readonly string[] ProfessionalTermAnchorHex =
    [
        "#C79FEF", "#8E82FE", "#01153E", "#069AF3", "#75BBFD", "#87AE73",
        "#01A049", "#06470C", "#04D8B2", "#029386", "#FF796C", "#80013F",
        "#A83C09", "#BE0119", "#FF9408", "#FFFFCB", "#343837", "#516572",
        "#4B0082", "#B0E0E6", "#4682B4", "#6B8E23", "#00FF00", "#C1F80A",
        "#80F9AD", "#0047AB", "#007BA7", "#FE4B03", "#E17701", "#F29E8E",
        "#C0FA8B", "#FAF0E6", "#C0C0C0"
    ];

    public static AuditReport Analyze(
        AuditOptions options,
        StructuredGrid structured,
        IReadOnlyList<AuditClassification> monteCarlo,
        IReadOnlyList<AuditClassification> rgbGrid,
        IReadOnlyList<AuditClassification> boundaryRefinement,
        IReadOnlyList<BalancedSemanticSample> balancedSemantic,
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
        var semantic = SemanticReferenceAudit.Analyze(balancedSemantic, references);
        var vocabularyGaps = FindVocabularyGaps(semantic.Cohort);
        var vocabularyCandidates = VocabularyCandidateAudit.Analyze(references);
        var professionalTermClassifications = ProfessionalTermAnchorHex
            .Select(hex => adapter.Classify(ParseHex(hex)))
            .ToArray();
        var professionalTermAssessments = SemanticReferenceAudit.AssessMany(
            professionalTermClassifications,
            references);
        var ownerClassifications = OwnerCandidateHex
            .Select(item => (item.Region, Sample: adapter.Classify(ParseHex(item.Hex))))
            .ToArray();
        var ownerAssessments = SemanticReferenceAudit.AssessMany(
            ownerClassifications.Select(item => item.Sample),
            references);
        var ownerCandidates = ownerClassifications
            .Select(item => new OwnerCandidateSample(
                item.Region,
                item.Sample,
                ownerAssessments.GetValueOrDefault(item.Sample.Rgb.Packed)))
            .ToArray();

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
            semantic.Cohort.Count,
            semantic.AssessedCount,
            semantic.IncompatibleCount,
            ranked.Count(anomaly => anomaly.Severity == "High"),
            ranked.Count(anomaly => anomaly.Severity == "Medium"),
            runtimeSeconds);

        var report = new AuditReport(
            "fovium-color-taxonomy-audit/v4",
            options.Mode.ToString(),
            options.Seed,
            options.Configuration,
            metrics,
            CountBy(all, sample => sample.Family),
            CountBy(all, sample => sample.Role),
            componentCounts,
            semantic.HueCoverage,
            semantic.LightnessCoverage,
            semantic.ChromaCoverage,
            references.Summaries,
            ownerSeeds,
            ownerCandidates,
            semantic.Cohort,
            semantic.FamilyProfiles,
            ranked,
            null);
        return report with
        {
            Specificity = new AuditSpecificityMetrics(
                semantic.Cohort.Count(item => item.Sample.Specificity == "GenericFamily"),
                semantic.Cohort.Count(item => item.Sample.Specificity == "ExistingSpecificFamily"),
                semantic.Cohort.Count(item => item.Sample.Specificity == "ProfessionalTerm"),
                semantic.Cohort.Count(item => item.Sample.Specificity == "NeutralRole"),
                vocabularyGaps.Count),
            VocabularyGaps = vocabularyGaps,
            VocabularyCandidates = vocabularyCandidates,
            ProfessionalTermSamples = professionalTermClassifications
                .Select(sample => new OwnerCandidateSample(
                    sample.ProfessionalTerm ?? sample.Family,
                    sample,
                    professionalTermAssessments.GetValueOrDefault(sample.Rgb.Packed))
                {
                    ProfessionalExplanation = adapter.ExplainProfessional(sample.Rgb)
                })
                .ToArray(),
            ProfessionalTermCoverage = CountBy(
                all.Where(sample => sample.ProfessionalTerm is not null),
                sample => sample.ProfessionalTerm!)
        };
    }

    private static IReadOnlyList<VocabularyGapCandidate> FindVocabularyGaps(
        IReadOnlyList<BalancedSemanticSample> cohort)
    {
        return cohort
            .Where(item => item.Sample.Specificity == "GenericFamily" && item.Reference is not null)
            .Select(item => CreateVocabularyGap(item.Sample, item.Reference!))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .GroupBy(candidate => candidate.SpecificTerm, StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderByDescending(candidate => candidate.DatasetSupport)
                .ThenBy(candidate => candidate.Reference.MeanNearestDeltaE)
                .ThenBy(candidate => candidate.Sample.Rgb.Packed)
                .Take(8))
            .OrderByDescending(candidate => candidate.DatasetSupport)
            .ThenBy(candidate => candidate.SpecificTerm, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Sample.Rgb.Packed)
            .ToArray();
    }

    private static VocabularyGapCandidate? CreateVocabularyGap(
        AuditClassification sample,
        AuditReferenceAssessment reference)
    {
        var votes = reference.Neighbors
            .Where(neighbor => neighbor.SpecificTerm is not null && neighbor.DeltaE <= 0.060)
            .GroupBy(neighbor => neighbor.Dataset, StringComparer.Ordinal)
            .Select(group => group.OrderBy(neighbor => neighbor.DeltaE).First())
            .GroupBy(neighbor => neighbor.SpecificTerm!, StringComparer.Ordinal)
            .Select(group => new
            {
                Term = group.Key,
                Datasets = group.Select(neighbor => neighbor.Dataset)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                MeanDistance = group.Average(neighbor => neighbor.DeltaE)
            })
            .OrderByDescending(candidate => candidate.Datasets.Length)
            .ThenBy(candidate => candidate.MeanDistance)
            .ThenBy(candidate => candidate.Term, StringComparer.Ordinal)
            .FirstOrDefault();
        if (votes is null || votes.Datasets.Length < 2 || votes.Term == sample.ProfessionalTerm)
        {
            return null;
        }

        return new VocabularyGapCandidate(
            votes.Term,
            votes.Datasets.Length,
            votes.Datasets,
            sample,
            reference);
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

        var assessments = SemanticReferenceAudit.AssessMany(candidates, catalog);
        var count = 0;
        foreach (var candidate in candidates)
        {
            if (!assessments.TryGetValue(candidate.Rgb.Packed, out var assessment) ||
                assessment.ConsensusSupport < 2 ||
                assessment.IsCompatible)
            {
                continue;
            }

            count++;
            var isOwnerSeed = OwnerSeedHex.Contains(candidate.Rgb.Hex, StringComparer.Ordinal);
            var score = 62 + assessment.ConsensusSupport * 8 + (isOwnerSeed ? 8 : 0);
            anomalies.Add(CreateAnomaly(
                "ReferenceDisagreement",
                score,
                $"{assessment.ConsensusSupport} datasets support {assessment.ConsensusSemantic}; " +
                $"Fovium maps to {assessment.ProductSemantic} ({candidate.Family}).",
                candidate,
                references: assessment.Neighbors));
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
        _ => SemanticReferenceAudit.ProductSemantic(sample),
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