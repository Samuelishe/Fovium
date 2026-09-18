using Fovium.ColorPicking;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class VocabularyCandidateAudit
{
    public static IReadOnlyList<VocabularyCandidateProfile> Analyze(ReferenceCatalog catalog)
    {
        var adapter = new ProductionColorAdapter();
        var shipped = ProfessionalShadeCatalog.Definitions
            .Select(definition => definition.Term.ToString())
            .ToHashSet(StringComparer.Ordinal);

        return catalog.Anchors
            .Where(anchor => anchor.SpecificTerm is not null)
            .GroupBy(anchor => anchor.SpecificTerm!, StringComparer.Ordinal)
            .Select(group => CreateProfile(group.Key, group.ToArray(), adapter, shipped))
            .Where(profile => profile.DatasetSupport >= 2)
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