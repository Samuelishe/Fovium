namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class MasterCandidateLexiconAudit
{
    public static IReadOnlyList<MasterCandidateLexiconEntry> Analyze(ReferenceCatalog catalog)
    {
        var sourceById = catalog.Summaries.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var profiles = VocabularyCandidateAudit.AnalyzeAll(catalog)
            .ToDictionary(item => item.SpecificTerm, StringComparer.Ordinal);
        var shippedProfiles = profiles.Values
            .Where(item => item.IsShippedTerm)
            .ToArray();

        return ProfessionalTermResearchCatalog.Terms
            .Select(descriptor => CreateEntry(
                descriptor,
                catalog,
                sourceById,
                profiles.GetValueOrDefault(descriptor.CanonicalTerm),
                shippedProfiles))
            .OrderByDescending(item => item.PriorityScore)
            .ThenBy(item => item.CanonicalTerm, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<CandidateDomainCoverage> SummarizeDomains(
        IReadOnlyList<MasterCandidateLexiconEntry> entries)
    {
        return entries
            .GroupBy(item => item.ResearchDomain, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var accepted = group.Count(item => item.Status == CandidateResearchStatus.Accepted);
                var evidenceRich = group.Count(item =>
                    item.IndependentSourceCount >= 2 && item.CompactComponentCount > 0);
                var density = accepted >= 15 ? "Dense" : accepted >= 8 ? "Medium" : "Sparse";
                return new CandidateDomainCoverage(group.Key, group.Count(), accepted, evidenceRich, density);
            })
            .ToArray();
    }

    private static MasterCandidateLexiconEntry CreateEntry(
        ProfessionalTermResearchDescriptor descriptor,
        ReferenceCatalog catalog,
        IReadOnlyDictionary<string, ReferenceDatasetSummary> sourceById,
        VocabularyCandidateProfile? profile,
        IReadOnlyList<VocabularyCandidateProfile> shippedProfiles)
    {
        var anchorOccurrences = catalog.Anchors
            .Where(item => item.SpecificTerm == descriptor.CanonicalTerm)
            .Select(item => (item.Dataset, item.Name, IsAnchor: true));
        var lexicalOccurrences = catalog.LexicalOccurrences
            .Where(item => item.SpecificTerm == descriptor.CanonicalTerm)
            .Select(item => (item.Dataset, item.Name, IsAnchor: false));
        var occurrences = anchorOccurrences
            .Concat(lexicalOccurrences)
            .GroupBy(item => item.Dataset, StringComparer.Ordinal)
            .Select(group =>
            {
                sourceById.TryGetValue(group.Key, out var source);
                return new MasterCandidateSourceOccurrence(
                    group.Key,
                    source?.Independence ?? "Uncertain",
                    string.IsNullOrWhiteSpace(source?.IndependenceGroup)
                        ? group.Key
                        : source.IndependenceGroup,
                    group.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                    group.Count(item => item.IsAnchor));
            })
            .OrderBy(item => item.Dataset, StringComparer.Ordinal)
            .ToArray();
        var independentSources = occurrences
            .Where(item => item.Independence == "Independent")
            .Select(item => item.IndependenceGroup)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var (nearestTerm, nearestDistance) = FindNearestShipped(profile, shippedProfiles);
        var componentCount = profile?.Components.Count ?? 0;
        var anchorCount = profile?.AnchorCount ?? 0;
        var noiseFraction = anchorCount == 0 ? 0 : (double)profile!.NoiseAnchorCount / anchorCount;
        var compactSupport = profile?.Components.Sum(item => item.DatasetSupport) ?? 0;
        var distinctness = nearestDistance is null ? 0 : Math.Min(nearestDistance.Value, 0.15) * 100;
        var priorityScore = independentSources * 100 + occurrences.Length * 20 + compactSupport * 12 +
            distinctness - noiseFraction * 25;

        return new MasterCandidateLexiconEntry(
            descriptor.CanonicalTerm,
            descriptor.Aliases,
            descriptor.Domain.ToString(),
            descriptor.Status,
            descriptor.Reason,
            descriptor.RussianCandidate,
            occurrences,
            independentSources,
            anchorCount,
            componentCount,
            noiseFraction,
            profile?.MedianDeltaE ?? 0,
            profile?.P90DeltaE ?? 0,
            profile?.Representative,
            profile?.ProductionFamilyCoverage ?? new Dictionary<string, int>(StringComparer.Ordinal),
            nearestTerm,
            nearestDistance,
            priorityScore);
    }

    private static (string Term, double? Distance) FindNearestShipped(
        VocabularyCandidateProfile? profile,
        IReadOnlyList<VocabularyCandidateProfile> shippedProfiles)
    {
        if (profile is null)
        {
            return (string.Empty, null);
        }

        var nearest = shippedProfiles
            .Where(item => item.SpecificTerm != profile.SpecificTerm)
            .Select(item => new
            {
                item.SpecificTerm,
                Distance = DeltaE(profile.Representative, item.Representative)
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.SpecificTerm, StringComparer.Ordinal)
            .FirstOrDefault();
        return nearest is null ? (string.Empty, null) : (nearest.SpecificTerm, nearest.Distance);
    }

    private static double DeltaE(AuditClassification left, AuditClassification right)
    {
        var deltaL = left.LabL - right.LabL;
        var deltaA = left.LabA - right.LabA;
        var deltaB = left.LabB - right.LabB;
        return Math.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
    }
}