using Fovium.ColorPicking;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class VocabularyCandidateAudit
{
    private const double ComponentRadius = 0.055;
    private const int MinimumComponentAnchors = 2;

    public static IReadOnlyList<VocabularyCandidateProfile> Analyze(ReferenceCatalog catalog)
    {
        return AnalyzeAll(catalog)
            .Where(profile => profile.DatasetSupport >= 2)
            .ToArray();
    }

    internal static IReadOnlyList<VocabularyCandidateProfile> AnalyzeAll(ReferenceCatalog catalog)
    {
        var adapter = new ProductionColorAdapter();
        var shipped = ProfessionalShadeCatalog.Definitions
            .Select(definition => definition.Term.ToString())
            .ToHashSet(StringComparer.Ordinal);

        return catalog.Anchors
            .Where(anchor => anchor.SpecificTerm is not null)
            .GroupBy(anchor => anchor.SpecificTerm!, StringComparer.Ordinal)
            .Select(group => CreateProfile(group.Key, group.ToArray(), adapter, shipped))
            .OrderByDescending(profile => profile.DatasetSupport)
            .ThenBy(profile => profile.P90DeltaE)
            .ThenByDescending(profile => profile.AnchorCount)
            .ThenBy(profile => profile.SpecificTerm, StringComparer.Ordinal)
            .ToArray();
    }

    private static VocabularyCandidateProfile CreateProfile(
        string term,
        ReferenceAnchor[] anchors,
        ProductionColorAdapter adapter,
        IReadOnlySet<string> shipped)
    {
        var medoid = anchors
            .Select(anchor => new
            {
                Anchor = anchor,
                Sum = anchors.Sum(other => DeltaE(anchor, other))
            })
            .OrderBy(item => item.Sum)
            .ThenBy(item => item.Anchor.Rgb.Packed)
            .First()
            .Anchor;
        var distances = anchors
            .Select(anchor => DeltaE(medoid, anchor))
            .Order()
            .ToArray();
        var classifications = anchors.Select(anchor => adapter.Classify(anchor.Rgb)).ToArray();

        var components = FindCompactComponents(anchors, adapter);
        return new VocabularyCandidateProfile(
            term,
            anchors.Length,
            anchors.Select(anchor => anchor.Dataset).Distinct(StringComparer.Ordinal).Count(),
            anchors.Select(anchor => anchor.Dataset).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            Percentile(distances, 0.50),
            Percentile(distances, 0.90),
            shipped.Contains(term),
            adapter.Classify(medoid.Rgb),
            new SortedDictionary<string, int>(
                classifications.GroupBy(item => item.Family, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            Components = components,
            NoiseAnchorCount = anchors.Length - components.Sum(component => component.AnchorCount),
            ResearchDomain = ProfessionalTermResearchCatalog.DescribeDomain(term).ToString()
        };
    }

    private static IReadOnlyList<VocabularyCandidateComponentProfile> FindCompactComponents(
        IReadOnlyList<ReferenceAnchor> anchors,
        ProductionColorAdapter adapter)
    {
        var remaining = anchors
            .OrderBy(anchor => anchor.Rgb.Packed)
            .ThenBy(anchor => anchor.Dataset, StringComparer.Ordinal)
            .ThenBy(anchor => anchor.Name, StringComparer.Ordinal)
            .ToList();
        var clusters = new List<ReferenceAnchor[]>();

        while (remaining.Count >= MinimumComponentAnchors)
        {
            var candidate = remaining
                .Select(seed => new
                {
                    Seed = seed,
                    Members = remaining.Where(anchor => DeltaE(seed, anchor) <= ComponentRadius).ToArray()
                })
                .Where(item => item.Members.Length >= MinimumComponentAnchors)
                .OrderByDescending(item => item.Members.Select(anchor => anchor.Dataset)
                    .Distinct(StringComparer.Ordinal).Count())
                .ThenByDescending(item => item.Members.Length)
                .ThenBy(item => item.Members.Sum(anchor => DeltaE(item.Seed, anchor)))
                .ThenBy(item => item.Seed.Rgb.Packed)
                .ThenBy(item => item.Seed.Dataset, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is null)
            {
                break;
            }

            clusters.Add(candidate.Members);
            foreach (var member in candidate.Members)
            {
                remaining.Remove(member);
            }
        }

        return clusters
            .Select(cluster => CreateComponent(cluster, adapter))
            .OrderByDescending(component => component.DatasetSupport)
            .ThenByDescending(component => component.AnchorCount)
            .ThenBy(component => component.Representative.Rgb.Packed)
            .Select((component, index) => component with { ComponentIndex = index + 1 })
            .ToArray();
    }

    private static VocabularyCandidateComponentProfile CreateComponent(
        ReferenceAnchor[] anchors,
        ProductionColorAdapter adapter)
    {
        var medoid = anchors
            .Select(anchor => new
            {
                Anchor = anchor,
                Sum = anchors.Sum(other => DeltaE(anchor, other))
            })
            .OrderBy(item => item.Sum)
            .ThenBy(item => item.Anchor.Rgb.Packed)
            .First()
            .Anchor;
        var distances = anchors.Select(anchor => DeltaE(medoid, anchor)).Order().ToArray();
        var classifications = anchors.Select(anchor => adapter.Classify(anchor.Rgb)).ToArray();

        return new VocabularyCandidateComponentProfile(
            0,
            anchors.Length,
            anchors.Select(anchor => anchor.Dataset).Distinct(StringComparer.Ordinal).Count(),
            anchors.Select(anchor => anchor.Dataset).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            Percentile(distances, 0.50),
            Percentile(distances, 0.90),
            adapter.Classify(medoid.Rgb),
            new SortedDictionary<string, int>(
                classifications.GroupBy(item => item.Family, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                StringComparer.Ordinal));
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static double DeltaE(ReferenceAnchor left, ReferenceAnchor right)
    {
        var deltaL = left.LabL - right.LabL;
        var deltaA = left.LabA - right.LabA;
        var deltaB = left.LabB - right.LabB;
        return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
    }
}