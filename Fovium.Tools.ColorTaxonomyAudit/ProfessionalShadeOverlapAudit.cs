using Fovium.ColorPicking;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record ProfessionalOverlapPair(
    string WinnerTerm,
    string CompetingTerm,
    int SampleCount,
    string RepresentativeHex,
    double SampleShare,
    double WinnerOverlapRatio,
    double CompetitorContainmentRatio,
    double SimilarityScore,
    bool NearTotalContainment,
    bool SameCoreDuplicateWarning,
    ProfessionalOverlapSeverity Severity);

internal sealed record ProfessionalRegionOverlapProfile(
    string RegionStableId,
    int MatchedSamples,
    int WinningSamples,
    bool IsShadowed);

internal sealed record ProfessionalTermOverlapProfile(
    string Term,
    int MatchedSamples,
    int WinningSamples,
    double MaximumContainmentRatio,
    bool IsMostlyDisputed);

internal sealed record ProfessionalOverlapReport(
    int SampleCount,
    int SamplesWithMultipleTerms,
    IReadOnlyList<ProfessionalOverlapPair> Pairs,
    IReadOnlyList<ProfessionalRegionOverlapProfile> Regions,
    IReadOnlyList<ProfessionalTermOverlapProfile> Terms);

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
        var termMatches = new Dictionary<string, int>(StringComparer.Ordinal);
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

            foreach (var term in matches.Select(item => item.Term).Distinct(StringComparer.Ordinal))
            {
                termMatches[term] = termMatches.GetValueOrDefault(term) + 1;
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

        var pairProfiles = pairs.Select(item =>
            {
                var share = (double)item.Value.Count / samples.Count;
                var semanticSeverity = ProfessionalTermResearchCatalog.ClassifyOverlap(
                    item.Key.Winner,
                    item.Key.Competitor);
                var winnerOverlap = (double)item.Value.Count / termMatches[item.Key.Winner];
                var competitorContainment = (double)item.Value.Count / termMatches[item.Key.Competitor];
                var similarity = 2d * item.Value.Count /
                                 (termMatches[item.Key.Winner] + termMatches[item.Key.Competitor]);
                var nearTotalContainment = Math.Max(winnerOverlap, competitorContainment) >= 0.80;
                var sameCoreDuplicate = IsSameCoreDuplicate(
                    winnerOverlap,
                    competitorContainment,
                    similarity);
                var severity = share >= 0.01
                    ? ProfessionalOverlapSeverity.ExcessiveVolume
                    : sameCoreDuplicate
                        ? ProfessionalOverlapSeverity.SuspiciousSibling
                        : semanticSeverity;
                return new ProfessionalOverlapPair(
                    item.Key.Winner,
                    item.Key.Competitor,
                    item.Value.Count,
                    item.Value.Hex,
                    share,
                    winnerOverlap,
                    competitorContainment,
                    similarity,
                    nearTotalContainment,
                    sameCoreDuplicate,
                    severity);
            })
            .OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.SampleCount)
            .ThenBy(item => item.WinnerTerm, StringComparer.Ordinal)
            .ThenBy(item => item.CompetingTerm, StringComparer.Ordinal)
            .ToArray();
        var termWins = ProfessionalShadeCatalog.Definitions
            .ToDictionary(
                definition => definition.Term.ToString(),
                definition => definition.Regions.Sum(region => regionWins[region.StableId]),
                StringComparer.Ordinal);

        return new ProfessionalOverlapReport(
            samples.Count,
            samplesWithMultipleTerms,
            pairProfiles,
            regionMatches.Select(item => new ProfessionalRegionOverlapProfile(
                    item.Key,
                    item.Value,
                    regionWins[item.Key],
                    item.Value > 0 && regionWins[item.Key] == 0))
                .OrderBy(item => item.RegionStableId, StringComparer.Ordinal)
                .ToArray(),
            ProfessionalShadeCatalog.Definitions.Select(definition =>
                {
                    var term = definition.Term.ToString();
                    var matchedSamples = termMatches.GetValueOrDefault(term);
                    var maximumContainment = pairProfiles
                        .Where(pair => pair.WinnerTerm == term || pair.CompetingTerm == term)
                        .Select(pair => pair.WinnerTerm == term
                            ? pair.WinnerOverlapRatio
                            : pair.CompetitorContainmentRatio)
                        .DefaultIfEmpty(0)
                        .Max();
                    return new ProfessionalTermOverlapProfile(
                        term,
                        matchedSamples,
                        termWins.GetValueOrDefault(term),
                        maximumContainment,
                        matchedSamples > 0 &&
                        (termWins.GetValueOrDefault(term) == 0 || pairProfiles.Any(pair =>
                            pair.SameCoreDuplicateWarning &&
                            (pair.WinnerTerm == term || pair.CompetingTerm == term))));
                })
                .OrderBy(item => item.Term, StringComparer.Ordinal)
                .ToArray());
    }

    internal static bool IsSameCoreDuplicate(
        double winnerOverlap,
        double competitorContainment,
        double similarity) =>
        Math.Max(winnerOverlap, competitorContainment) >= 0.80 && similarity >= 0.65;
}