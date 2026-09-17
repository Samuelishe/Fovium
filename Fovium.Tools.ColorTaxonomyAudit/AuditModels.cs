namespace Fovium.Tools.ColorTaxonomyAudit;

internal readonly record struct AuditRgb(byte Red, byte Green, byte Blue)
{
    public string Hex => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public int Packed => (Red << 16) | (Green << 8) | Blue;
}

internal sealed record AuditClassification(
    AuditRgb Rgb,
    double LabL,
    double LabA,
    double LabB,
    double OklchL,
    double OklchC,
    double OklchHue,
    string Role,
    string Undertone,
    string Family,
    string Lightness,
    string Chroma,
    string ShortName,
    string DetailedName);

internal sealed record AuditReferenceNeighbor(
    string Dataset,
    string Name,
    string Hex,
    string SemanticFamily,
    double DeltaE);

internal sealed record AuditAnomaly(
    string Id,
    string Severity,
    double Score,
    string Kind,
    string Reason,
    AuditClassification Sample,
    AuditClassification? Neighbor,
    double? DeltaE,
    IReadOnlyList<AuditReferenceNeighbor> References);

internal sealed record AuditMetrics(
    int TotalUniqueSamples,
    int StructuredSamples,
    int MonteCarloSamples,
    int RgbGridSamples,
    int BoundaryRefinementSamples,
    int BoundaryEdges,
    int AbruptDiscontinuities,
    int ChromaReversals,
    int LightnessOscillations,
    int ModifierReversals,
    int TinyComponents,
    int ThinSlivers,
    int ReferenceDisagreements,
    int HighSeverityAnomalies,
    int MediumSeverityAnomalies,
    double RuntimeSeconds);

internal sealed record AuditReport(
    string Schema,
    string Mode,
    int Seed,
    AuditConfiguration Configuration,
    AuditMetrics Metrics,
    IReadOnlyDictionary<string, int> FamilyCoverage,
    IReadOnlyDictionary<string, int> RoleCoverage,
    IReadOnlyDictionary<string, int> ComponentCounts,
    IReadOnlyList<ReferenceDatasetSummary> References,
    IReadOnlyList<AuditClassification> OwnerSeeds,
    IReadOnlyList<AuditAnomaly> Anomalies,
    AuditComparison? Comparison);

internal sealed record AuditComparison(
    string BaselinePath,
    int HighSeverityDelta,
    int MediumSeverityDelta,
    int AbruptDiscontinuityDelta,
    int ChromaReversalDelta,
    int LightnessOscillationDelta,
    int ModifierReversalDelta,
    int TinyComponentDelta,
    int ThinSliverDelta,
    int ReferenceDisagreementDelta);

internal sealed record ReferenceDatasetSummary(
    string Id,
    int AnchorCount,
    int NormalizedAnchorCount,
    string Source,
    string License,
    string Sha256);