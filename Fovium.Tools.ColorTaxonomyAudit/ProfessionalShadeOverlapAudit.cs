using Fovium.ColorPicking;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record ProfessionalOverlapPair(
    string WinnerTerm,
    string CompetingTerm,
    int SampleCount,
    string RepresentativeHex);

internal sealed record ProfessionalRegionOverlapProfile(
    string RegionStableId,
    int MatchedSamples,
    int WinningSamples,
    bool IsShadowed);

internal sealed record ProfessionalOverlapReport(
    int SampleCount,
    int SamplesWithMultipleTerms,
    IReadOnlyList<ProfessionalOverlapPair> Pairs,
    IReadOnlyList<ProfessionalRegionOverlapProfile> Regions);

internal static class ProfessionalShadeOverlapAudit
{
    public static ProfessionalOverlapReport Analyze(
        ProductionColorAdapter adapter,
        IReadOnlyList<AuditRgb> samples)
    {
        var regionMatches = ProfessionalShadeCatalog.Definitions
            .SelectMany(definition => definition.Regions)
            .ToDictionary(region => region.StableId, _ => 0, StringComparer.Ordinal);
        var regionWins = regionMatches.Keys
            .ToDictionary(regionId => regionId, _ => 0, StringComparer.Ordinal);
        var pairs = new Dictionary<(string Winner, string Competitor), (int Count, string Hex)>();
        var samplesWithMultipleTerms = 0;

        foreach (var sample in samples.OrderBy(item => item.Packed))
        {
            var explanation = adapter.ExplainProfessional(sample);
            var matches = explanation.Candidates
                .Where(item => item.Matched)
                .ToArray();
            foreach (var match in matches)
            {
                regionMatches[match.RegionStableId]++;
            }

            if (explanation.WinnerRegionStableId is { } winnerRegion)
            {
                regionWins[winnerRegion]++;
            }

            if (explanation.WinnerTerm is not { } winnerTerm)
            {
                continue;
            }

            var competingTerms = matches
                .Select(item => item.Term)
                .Where(term => term != winnerTerm)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (competingTerms.Length > 0)
            {
                samplesWithMultipleTerms++;
            }

            foreach (var competitor in competingTerms)
            {
                var key = (winnerTerm, competitor);
                if (pairs.TryGetValue(key, out var value))
                {
                    pairs[key] = (value.Count + 1, value.Hex);
                }
                else
                {
                    pairs[key] = (1, sample.Hex);
                }
            }
        }

        return new ProfessionalOverlapReport(
            samples.Count,
            samplesWithMultipleTerms,
            pairs.Select(item => new ProfessionalOverlapPair(
                    item.Key.Winner,
                    item.Key.Competitor,
                    item.Value.Count,
                    item.Value.Hex))
                .OrderByDescending(item => item.SampleCount)
                .ThenBy(item => item.WinnerTerm, StringComparer.Ordinal)
                .ThenBy(item => item.CompetingTerm, StringComparer.Ordinal)
                .ToArray(),
            regionMatches.Select(item => new ProfessionalRegionOverlapProfile(
                    item.Key,
                    item.Value,
                    regionWins[item.Key],
                    item.Value > 0 && regionWins[item.Key] == 0))
                .OrderBy(item => item.RegionStableId, StringComparer.Ordinal)
                .ToArray());
    }
}