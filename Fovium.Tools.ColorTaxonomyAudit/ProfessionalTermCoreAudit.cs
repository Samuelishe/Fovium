using Fovium.ColorPicking;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class ProfessionalTermCoreAudit
{
    public static IReadOnlyList<ProfessionalTermCoreProfile> Analyze(
        IReadOnlyList<OwnerCandidateSample> boundarySamples,
        ProfessionalOverlapReport overlaps)
    {
        var overlapByTerm = overlaps.Terms.ToDictionary(item => item.Term, StringComparer.Ordinal);
        return ProfessionalShadeCatalog.Definitions
            .Select(definition =>
            {
                var regionIds = definition.Regions.Select(region => region.StableId).ToArray();
                var interior = boundarySamples.Where(sample =>
                        regionIds.Any(regionId => sample.Region.StartsWith(regionId + ":", StringComparison.Ordinal)) &&
                        (sample.Region.EndsWith(":center", StringComparison.Ordinal) ||
                         sample.Region.EndsWith("-inside", StringComparison.Ordinal)))
                    .ToArray();
                var winning = interior.Count(sample =>
                    sample.ProfessionalExplanation?.WinnerTerm == definition.Term.ToString());
                var representative = interior.FirstOrDefault(sample =>
                                         sample.Region.EndsWith(":center", StringComparison.Ordinal) &&
                                         sample.ProfessionalExplanation?.WinnerTerm == definition.Term.ToString())
                                     ?? interior.FirstOrDefault(sample =>
                                         sample.ProfessionalExplanation?.WinnerTerm == definition.Term.ToString());
                var overlap = overlapByTerm.GetValueOrDefault(definition.Term.ToString());
                var winningRatio = interior.Length == 0 ? 0 : (double)winning / interior.Length;
                var confidence = overlap is null || overlap.WinningSamples == 0 ||
                                 overlap.IsMostlyDisputed || winningRatio < 0.5
                    ? "Low"
                    : winningRatio >= 0.80
                        ? "High"
                        : "Medium";
                return new ProfessionalTermCoreProfile(
                    definition.Term.ToString(),
                    representative?.Sample.Rgb.Hex ?? string.Empty,
                    definition.Regions.Count,
                    interior.Length,
                    winning,
                    overlap?.MatchedSamples ?? 0,
                    overlap?.WinningSamples ?? 0,
                    overlap?.MaximumContainmentRatio ?? 0,
                    confidence,
                    overlap?.IsMostlyDisputed ?? true);
            })
            .OrderBy(item => item.Term, StringComparer.Ordinal)
            .ToArray();
    }
}