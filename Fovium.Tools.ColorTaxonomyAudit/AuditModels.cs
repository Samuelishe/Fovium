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
    string DetailedName)
{
    public string? ProfessionalTerm { get; init; }

    public string Specificity { get; init; } = "GenericFamily";
}

internal sealed record AuditReferenceNeighbor(
    string Dataset,
    string Name,
    string Hex,
    string SemanticFamily,
    double DeltaE)
{
    public string? SpecificTerm { get; init; }
}

internal sealed record AuditSpecificityMetrics(
    int GenericFamilyOnly,
    int ExistingSpecificFamily,
    int ProfessionalTerm,
    int NeutralRole,
    int VocabularyGapCandidates);

internal sealed record VocabularyGapCandidate(
    string SpecificTerm,
    int DatasetSupport,
    IReadOnlyList<string> SupportingDatasets,
    AuditClassification Sample,
    AuditReferenceAssessment Reference);

internal sealed record VocabularyCandidateProfile(
    string SpecificTerm,
    int AnchorCount,
    int DatasetSupport,
    IReadOnlyList<string> SupportingDatasets,
    double MedianDeltaE,
    double P90DeltaE,
    bool IsShippedTerm,
    AuditClassification Representative,
    IReadOnlyDictionary<string, int> ProductionFamilyCoverage)
{
    public IReadOnlyList<VocabularyCandidateComponentProfile> Components { get; init; } = [];

    public int NoiseAnchorCount { get; init; }

    public string ResearchDomain { get; init; } = ProfessionalTermDomain.Other.ToString();
}

internal sealed record VocabularyCandidateComponentProfile(
    int ComponentIndex,
    int AnchorCount,
    int DatasetSupport,
    IReadOnlyList<string> SupportingDatasets,
    double MedianDeltaE,
    double P90DeltaE,
    AuditClassification Representative,
    IReadOnlyDictionary<string, int> ProductionFamilyCoverage);

internal sealed record MasterCandidateSourceOccurrence(
    string Dataset,
    string Independence,
    string IndependenceGroup,
    IReadOnlyList<string> Names,
    int AnchorCount)
{
    public int LexicalOccurrenceCount { get; init; }
}

internal sealed record MasterCandidateLexiconEntry(
    string CanonicalTerm,
    IReadOnlyList<string> Aliases,
    string ResearchDomain,
    CandidateResearchStatus Status,
    string Reason,
    string RussianCandidate,
    IReadOnlyList<MasterCandidateSourceOccurrence> SourceOccurrences,
    int IndependentSourceCount,
    int AnchorCount,
    int CompactComponentCount,
    double NoiseFraction,
    double MedianDeltaE,
    double P90DeltaE,
    AuditClassification? Representative,
    IReadOnlyDictionary<string, int> ProductionFamilyCoverage,
    string NearestShippedTerm,
    double? NearestShippedDeltaE,
    double PriorityScore)
{
    public int LexicalSourceCount { get; init; }

    public int NumericSourceCount { get; init; }

    public int IndependentNumericSourceGroupCount { get; init; }

    public IReadOnlyList<CandidateComponentEvidence> ComponentEvidence { get; init; } = [];
}

internal sealed record CandidateComponentEvidence(
    int ComponentIndex,
    int AnchorCount,
    int NumericSourceCount,
    int IndependentNumericSourceGroupCount,
    IReadOnlyList<string> SourceIds);

internal sealed record CandidateDomainCoverage(
    string Domain,
    int CandidateCount,
    int AcceptedCount,
    int EvidenceRichCount,
    string VocabularyDensity);

internal sealed record AuditReferenceAssessment(
    string ProductSemantic,
    string ConsensusSemantic,
    int ConsensusSupport,
    bool IsExactAgreement,
    bool IsCompatible,
    double MeanNearestDeltaE,
    IReadOnlyDictionary<string, string> DatasetVotes,
    IReadOnlyList<AuditReferenceNeighbor> Neighbors);

internal sealed record BalancedSemanticSample(
    string CohortId,
    string HueStratum,
    string LightnessStratum,
    string ChromaStratum,
    AuditClassification Sample,
    AuditReferenceAssessment? Reference);

internal sealed record FamilySemanticProfile(
    string Family,
    int SampleCount,
    int ReferenceAssessedCount,
    int IncompatibleReferenceCount,
    double IncompatibleReferenceRate,
    string NearestCompetingFamily,
    AuditClassification Center,
    AuditClassification Edge,
    AuditClassification Dark,
    AuditClassification Light,
    AuditClassification LowChroma,
    AuditClassification HighChroma);

internal sealed record OwnerCandidateSample(
    string Region,
    AuditClassification Sample,
    AuditReferenceAssessment? Reference)
{
    public AuditProfessionalExplanation? ProfessionalExplanation { get; init; }
}

internal sealed record AuditProfessionalRegionEvaluation(
    string Term,
    string TermStableId,
    string RegionStableId,
    int Priority,
    bool Matched,
    string? FailureReason);

internal sealed record AuditProfessionalExplanation(
    string? WinnerTerm,
    string? WinnerTermStableId,
    string? WinnerRegionStableId,
    IReadOnlyList<AuditProfessionalRegionEvaluation> Candidates);

internal sealed record AuditClassificationChange(
    AuditClassification Before,
    AuditClassification After);

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
    int BalancedSemanticSamples,
    int BalancedReferenceAssessments,
    int BalancedIncompatibleDisagreements,
    int HighSeverityAnomalies,
    int MediumSeverityAnomalies,
    double RuntimeSeconds)
{
    public double ProfessionalClassificationNanosecondsPerSample { get; init; }

    public double ResearchClusteringMilliseconds { get; init; }
}

internal sealed record AuditReport(
    string Schema,
    string Mode,
    int Seed,
    AuditConfiguration Configuration,
    AuditMetrics Metrics,
    IReadOnlyDictionary<string, int> FamilyCoverage,
    IReadOnlyDictionary<string, int> RoleCoverage,
    IReadOnlyDictionary<string, int> ComponentCounts,
    IReadOnlyDictionary<string, int> BalancedHueCoverage,
    IReadOnlyDictionary<string, int> BalancedLightnessCoverage,
    IReadOnlyDictionary<string, int> BalancedChromaCoverage,
    IReadOnlyList<ReferenceDatasetSummary> References,
    IReadOnlyList<AuditClassification> OwnerSeeds,
    IReadOnlyList<OwnerCandidateSample> OwnerCandidates,
    IReadOnlyList<BalancedSemanticSample> BalancedCohort,
    IReadOnlyList<FamilySemanticProfile> FamilyProfiles,
    IReadOnlyList<AuditAnomaly> Anomalies,
    AuditComparison? Comparison)
{
    public AuditSpecificityMetrics Specificity { get; init; } = new(0, 0, 0, 0, 0);

    public IReadOnlyList<VocabularyGapCandidate> VocabularyGaps { get; init; } = [];

    public IReadOnlyList<VocabularyCandidateProfile> VocabularyCandidates { get; init; } = [];

    public IReadOnlyList<MasterCandidateLexiconEntry> MasterCandidateLexicon { get; init; } = [];

    public IReadOnlyList<CandidateDomainCoverage> CandidateDomainCoverage { get; init; } = [];

    public IReadOnlyList<OwnerCandidateSample> ProfessionalTermSamples { get; init; } = [];

    public IReadOnlyList<OwnerCandidateSample> ProfessionalBoundarySamples { get; init; } = [];

    public IReadOnlyDictionary<string, int> ProfessionalTermCoverage { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public ProfessionalOverlapReport ProfessionalOverlaps { get; init; } =
        new(0, 0, [], [], []);

    public IReadOnlyList<ProfessionalTermCoreProfile> ProfessionalTermCores { get; init; } = [];
}

internal sealed record ProfessionalTermCoreProfile(
    string Term,
    string RepresentativeCoreHex,
    int RegionCount,
    int InteriorProbeCount,
    int WinningInteriorProbeCount,
    int MatchedSamples,
    int WinningSamples,
    double MaximumContainmentRatio,
    string ConfidenceTier,
    bool IsMostlyDisputed);

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
    int ReferenceDisagreementDelta,
    int BalancedIncompatibleDisagreementDelta,
    IReadOnlyList<AuditClassificationChange> ChangedBalancedSamples);

internal sealed record ReferenceDatasetSummary(
    string Id,
    int AnchorCount,
    int NormalizedAnchorCount,
    string Source,
    string License,
    string Sha256)
{
    public string Independence { get; init; } = "Uncertain";

    public string IndependenceGroup { get; init; } = string.Empty;

    public string CachePolicy { get; init; } = "IgnoredCacheOnly";

    public string SourceQuality { get; init; } = "UncertainProvenance";

    public int LexicalOccurrenceCount { get; init; }
}