namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record SemanticAuditResult(
    IReadOnlyList<BalancedSemanticSample> Cohort,
    IReadOnlyList<FamilySemanticProfile> FamilyProfiles,
    IReadOnlyDictionary<string, int> HueCoverage,
    IReadOnlyDictionary<string, int> LightnessCoverage,
    IReadOnlyDictionary<string, int> ChromaCoverage,
    int AssessedCount,
    int IncompatibleCount);

internal static class SemanticReferenceAudit
{
    private static readonly IReadOnlyDictionary<string, double> MaximumUsefulDistance =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            // CSS has only 148 anchors, so a distant nearest name is not evidence.
            ["css"] = 0.050,
            ["iscc-nbs-centroids"] = 0.085,
            ["meodai"] = 0.040,
            ["xkcd"] = 0.060
        };

    private static readonly IReadOnlyDictionary<string, double> SemanticAngles =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["Red"] = 0,
            ["Crimson"] = 355,
            ["Burgundy"] = 350,
            ["Rose"] = 345,
            ["DustyPink"] = 342,
            ["Pink"] = 335,
            ["RedMagenta"] = 325,
            ["Magenta"] = 315,
            ["PinkLilac"] = 305,
            ["Violet"] = 290,
            ["BlueViolet"] = 270,
            ["Blue"] = 245,
            ["CyanBlue"] = 225,
            ["Cyan"] = 205,
            ["Turquoise"] = 180,
            ["Mint"] = 165,
            ["Green"] = 145,
            ["OliveGreen"] = 125,
            ["YellowGreen"] = 115,
            ["Olive"] = 105,
            ["Yellow"] = 95,
            ["Mustard"] = 88,
            ["Ochre"] = 82,
            ["Amber"] = 72,
            ["Apricot"] = 62,
            ["Orange"] = 55,
            ["Peach"] = 47,
            ["RedOrange"] = 42,
            ["Terracotta"] = 37,
            ["Coral"] = 30,
            ["Brown"] = 55,
            ["Sand"] = 72,
            ["Beige"] = 65,
            ["Greige"] = 65,
            ["Cream"] = 80
        };

    public static SemanticAuditResult Analyze(
        IReadOnlyList<BalancedSemanticSample> source,
        ReferenceCatalog catalog)
    {
        var evaluator = new ReferenceEvaluator(catalog);
        var cohort = source
            .Select(item => item with { Reference = evaluator.Assess(item.Sample) })
            .ToArray();
        var profiles = CreateFamilyProfiles(cohort);
        return new SemanticAuditResult(
            cohort,
            profiles,
            CountBy(cohort, item => item.HueStratum),
            CountBy(cohort, item => item.LightnessStratum),
            CountBy(cohort, item => item.ChromaStratum),
            cohort.Count(item => item.Reference is not null),
            cohort.Count(item => item.Reference is { IsCompatible: false }));
    }

    public static AuditReferenceAssessment? Assess(
        AuditClassification sample,
        ReferenceCatalog catalog) => new ReferenceEvaluator(catalog).Assess(sample);

    public static IReadOnlyDictionary<int, AuditReferenceAssessment> AssessMany(
        IEnumerable<AuditClassification> samples,
        ReferenceCatalog catalog)
    {
        var evaluator = new ReferenceEvaluator(catalog);
        return samples
            .GroupBy(sample => sample.Rgb.Packed)
            .Select(group => group.First())
            .Select(sample => (sample.Rgb.Packed, Assessment: evaluator.Assess(sample)))
            .Where(item => item.Assessment is not null)
            .ToDictionary(item => item.Packed, item => item.Assessment!);
    }

    internal static string ProductSemantic(AuditClassification sample)
    {
        if (sample.Role == "NearBlack")
        {
            return "Black";
        }

        if (sample.Role == "NearWhite")
        {
            return sample.Family == "Cream" ? "Cream" : "White";
        }

        return sample.Family switch
        {
            "Neutral" or "WarmGray" or "CoolGray" or "BlueGray" or "GreenGray" or "OliveGray" or
                "RoseGray" or "VioletGray" or "LilacGray" => "Gray",
            "Taupe" or "Greige" => "Greige",
            _ => sample.Family,
        };
    }

    internal static bool AreCompatible(string product, string reference)
    {
        if (product == reference)
        {
            return true;
        }

        if ((product == "Gray" && reference is "Black" or "White") ||
            (reference == "Gray" && product is "Black" or "White"))
        {
            return true;
        }

        if (EarthCompatibility.TryGetValue(product, out var compatible) && compatible.Contains(reference))
        {
            return true;
        }

        if (EarthSemantics.Contains(product) || EarthSemantics.Contains(reference))
        {
            return false;
        }

        if (!SemanticAngles.TryGetValue(product, out var productAngle) ||
            !SemanticAngles.TryGetValue(reference, out var referenceAngle))
        {
            return false;
        }

        var direct = Math.Abs(productAngle - referenceAngle);
        return Math.Min(direct, 360 - direct) <= 20;
    }

    private static readonly HashSet<string> EarthSemantics =
    [
        "Apricot", "Beige", "Brown", "Cream", "Greige", "Mustard", "Ochre", "Olive",
        "Peach", "Sand", "Taupe", "Terracotta"
    ];

    private static readonly IReadOnlyDictionary<string, HashSet<string>> EarthCompatibility =
        CreateSymmetricCompatibility(
        [
            ("Apricot", "Peach"), ("Apricot", "Orange"), ("Apricot", "Amber"),
            ("Apricot", "Cream"), ("Beige", "Greige"), ("Beige", "Sand"),
            ("Beige", "Cream"), ("Beige", "Peach"), ("Beige", "Taupe"),
            ("Brown", "Terracotta"), ("Brown", "Ochre"), ("Brown", "Taupe"),
            ("Cream", "Sand"), ("Greige", "Taupe"), ("Greige", "Gray"),
            ("Mustard", "Ochre"), ("Mustard", "Yellow"), ("Mustard", "Olive"),
            ("Ochre", "Amber"), ("Ochre", "Olive"), ("Ochre", "Sand"),
            ("Olive", "OliveGreen"), ("Olive", "YellowGreen"),
            ("Peach", "Coral"), ("Peach", "RedOrange"), ("Peach", "Orange"),
            ("Terracotta", "Coral"), ("Terracotta", "RedOrange"), ("Terracotta", "Orange")
        ]);

    private static IReadOnlyList<FamilySemanticProfile> CreateFamilyProfiles(
        IReadOnlyList<BalancedSemanticSample> cohort)
    {
        var samples = cohort.Select(item => item.Sample).ToArray();
        return cohort
            .GroupBy(item => item.Sample.Family, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.ToArray();
                var familySamples = items.Select(item => item.Sample).ToArray();
                var centerL = familySamples.Average(sample => sample.LabL);
                var centerA = familySamples.Average(sample => sample.LabA);
                var centerB = familySamples.Average(sample => sample.LabB);
                var center = familySamples.MinBy(sample => SquaredDistance(sample, centerL, centerA, centerB))!;
                var edge = familySamples.MinBy(sample => samples
                    .Where(other => other.Family != sample.Family)
                    .Select(other => DeltaE(sample, other))
                    .DefaultIfEmpty(double.MaxValue)
                    .Min())!;
                var assessed = items.Where(item => item.Reference is not null).ToArray();
                var incompatible = assessed.Count(item =>
                    item.Reference is { ConsensusSupport: >= 2, IsCompatible: false });
                var competitor = assessed
                    .Where(item => item.Reference is { ConsensusSupport: >= 2, IsCompatible: false })
                    .GroupBy(item => item.Reference!.ConsensusSemantic, StringComparer.Ordinal)
                    .OrderByDescending(candidate => candidate.Count())
                    .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
                    .Select(candidate => candidate.Key)
                    .FirstOrDefault() ?? string.Empty;
                return new FamilySemanticProfile(
                    group.Key,
                    items.Length,
                    assessed.Length,
                    incompatible,
                    assessed.Length == 0 ? 0 : (double)incompatible / assessed.Length,
                    competitor,
                    center,
                    edge,
                    familySamples.MinBy(sample => sample.OklchL)!,
                    familySamples.MaxBy(sample => sample.OklchL)!,
                    familySamples.MinBy(sample => sample.OklchC)!,
                    familySamples.MaxBy(sample => sample.OklchC)!);
            })
            .ToArray();
    }

    private static Dictionary<string, HashSet<string>> CreateSymmetricCompatibility(
        IEnumerable<(string Left, string Right)> pairs)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (left, right) in pairs)
        {
            if (!result.TryGetValue(left, out var leftValues))
            {
                result[left] = leftValues = new HashSet<string>(StringComparer.Ordinal);
            }

            if (!result.TryGetValue(right, out var rightValues))
            {
                result[right] = rightValues = new HashSet<string>(StringComparer.Ordinal);
            }

            leftValues.Add(right);
            rightValues.Add(left);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, int> CountBy(
        IEnumerable<BalancedSemanticSample> samples,
        Func<BalancedSemanticSample, string> selector) =>
        new SortedDictionary<string, int>(
            samples.GroupBy(selector, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static double SquaredDistance(AuditClassification sample, double l, double a, double b)
    {
        var deltaL = sample.LabL - l;
        var deltaA = sample.LabA - a;
        var deltaB = sample.LabB - b;
        return deltaL * deltaL + deltaA * deltaA + deltaB * deltaB;
    }

    private static double DeltaE(AuditClassification left, AuditClassification right) =>
        Math.Sqrt(SquaredDistance(left, right.LabL, right.LabA, right.LabB));

    private sealed class ReferenceEvaluator
    {
        private readonly IGrouping<string, ReferenceAnchor>[] _datasets;

        public ReferenceEvaluator(ReferenceCatalog catalog)
        {
            _datasets = catalog.Anchors
                .Where(anchor => anchor.SemanticFamily != "Unknown")
                .GroupBy(anchor => anchor.Dataset, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToArray();
        }

        public AuditReferenceAssessment? Assess(AuditClassification sample)
        {
            if (_datasets.Length == 0)
            {
                return null;
            }

            var neighbors = new List<AuditReferenceNeighbor>(_datasets.Length * 3);
            var votes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var dataset in _datasets)
            {
                var nearest = dataset
                    .Select(anchor => (Anchor: anchor, Delta: DeltaE(sample, anchor)))
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
                var maximumDistance = MaximumUsefulDistance.GetValueOrDefault(dataset.Key, 0.060);
                if (nearest[0].Delta > maximumDistance)
                {
                    continue;
                }

                votes[dataset.Key] = nearest
                    .GroupBy(item => item.Anchor.SemanticFamily, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Average(item => item.Delta))
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .First().Key;
            }

            if (votes.Count == 0)
            {
                return null;
            }

            var nearestByDataset = neighbors
                .GroupBy(neighbor => neighbor.Dataset, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Min(item => item.DeltaE), StringComparer.Ordinal);
            var consensus = votes.Values
                .Distinct(StringComparer.Ordinal)
                .Select(family => new
                {
                    Family = family,
                    Support = votes.Values.Count(vote => AreCompatible(family, vote)),
                    ExactSupport = votes.Values.Count(vote => vote == family),
                    MeanDistance = votes
                        .Where(vote => AreCompatible(family, vote.Value))
                        .Average(vote => nearestByDataset[vote.Key])
                })
                .OrderByDescending(candidate => candidate.Support)
                .ThenByDescending(candidate => candidate.ExactSupport)
                .ThenBy(candidate => candidate.MeanDistance)
                .ThenBy(candidate => candidate.Family, StringComparer.Ordinal)
                .First();
            var product = ProductSemantic(sample);
            return new AuditReferenceAssessment(
                product,
                consensus.Family,
                consensus.Support,
                product == consensus.Family,
                consensus.Support < 2 || AreCompatible(product, consensus.Family),
                votes.Keys.Average(dataset => nearestByDataset[dataset]),
                votes,
                neighbors.OrderBy(neighbor => neighbor.Dataset, StringComparer.Ordinal)
                    .ThenBy(neighbor => neighbor.DeltaE)
                    .ToArray());
        }

        private static double DeltaE(AuditClassification left, ReferenceAnchor right)
        {
            var deltaL = left.LabL - right.LabL;
            var deltaA = left.LabA - right.LabA;
            var deltaB = left.LabB - right.LabB;
            return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
        }
    }
}
