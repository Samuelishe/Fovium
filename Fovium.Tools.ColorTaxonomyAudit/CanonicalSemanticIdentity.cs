using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fovium.ColorSemantics;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class CanonicalSemanticIdentity
{
    // Taxonomy bounds are authored to at most three decimal places. Six decimal
    // places preserve a thousandfold safety margin while removing libm noise.
    public const int SemanticDecimalPlaces = 6;
    public const int DerivedDecimalPlaces = 9;
    public const int HueDecimalPlaces = 5;

    public const string NumericContract =
        "semantic bounds: 1e-6; derived report geometry: 1e-9; raw report hue: 1e-5 degree; midpoint: away-from-zero";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static double Semantic(double value) =>
        Math.Round(value, SemanticDecimalPlaces, MidpointRounding.AwayFromZero);

    public static double Derived(double value) =>
        Math.Round(value, DerivedDecimalPlaces, MidpointRounding.AwayFromZero);

    public static double Hue(double value) =>
        Math.Round(value, HueDecimalPlaces, MidpointRounding.AwayFromZero);

    public static string Hash<T>(T value) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)));

    public static string BuildDefinitionSignature()
    {
        var definitions = ProfessionalShadeCatalog.Definitions
            .OrderBy(item => item.StableId, StringComparer.Ordinal)
            .Select(item => new
            {
                item.StableId,
                Term = item.Term.ToString(),
                Regions = item.Regions.OrderBy(region => region.StableId, StringComparer.Ordinal).Select(region => new
                {
                    region.StableId,
                    Parents = region.ParentFamilies.Select(family => family.ToString())
                        .Order(StringComparer.Ordinal).ToArray(),
                    Roles = region.Roles.ToString(),
                    MinimumLightness = Semantic(region.MinimumLightness),
                    MaximumLightness = Semantic(region.MaximumLightness),
                    MinimumChroma = Semantic(region.MinimumChroma),
                    MaximumChroma = Semantic(region.MaximumChroma),
                    MinimumHue = Semantic(region.MinimumHue),
                    MaximumHue = Semantic(region.MaximumHue),
                    region.Priority
                }).ToArray()
            }).ToArray();
        var creative = ColorNameCatalog.LoadEmbedded().Entries
            .OrderBy(item => item.StableId, StringComparer.Ordinal)
            .Select(item => new { item.StableId, item.CanonicalName, item.Red, item.Green, item.Blue })
            .ToArray();
        return Hash(new
            { Schema = "fovium-color-semantics-definition/v1", Definitions = definitions, Creative = creative });
    }

    public static string BuildOutcomeSignature(IEnumerable<ReportClassificationOutcome> outcomes) =>
        Hash(new
        {
            Schema = "fovium-color-semantics-outcomes/v1",
            Outcomes = outcomes.OrderBy(item => item.SampleId, StringComparer.Ordinal).ToArray()
        });

    public static string BuildAuditOutcomeSignature(AuditReport report)
    {
        var outcomes = EnumerateAuditClassifications(report)
            .GroupBy(item => item.Rgb.Packed)
            .Select(group => group.First())
            .OrderBy(item => item.Rgb.Packed)
            .Select(item => new
            {
                item.Rgb.Packed,
                item.Role,
                item.Undertone,
                item.Family,
                item.Lightness,
                item.Chroma,
                item.ProfessionalTerm,
                item.Specificity
            })
            .ToArray();
        return Hash(new
        {
            Schema = "fovium-color-taxonomy-audit-outcomes/v1",
            Definition = BuildDefinitionSignature(),
            report.Mode,
            report.Seed,
            Outcomes = outcomes
        });
    }

    private static IEnumerable<AuditClassification> EnumerateAuditClassifications(AuditReport report)
    {
        foreach (var sample in report.OwnerSeeds)
        {
            yield return sample;
        }

        foreach (var sample in report.OwnerCandidates.Concat(report.ProfessionalTermSamples)
                     .Concat(report.ProfessionalBoundarySamples))
        {
            yield return sample.Sample;
        }

        foreach (var sample in report.BalancedCohort)
        {
            yield return sample.Sample;
        }

        foreach (var candidate in report.VocabularyCandidates)
        {
            yield return candidate.Representative;
        }
    }
}