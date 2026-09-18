namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record ColorSemanticsReport(
    string Schema,
    ColorSemanticsMetadata Metadata,
    ColorSemanticsSummary Summary,
    IReadOnlyList<ReportBroadFamily> BroadFamilies,
    IReadOnlyList<ReportProfessionalTerm> ProfessionalTerms,
    IReadOnlyList<ReportRegion> Regions,
    IReadOnlyList<ReportCreativeAnchor> CreativeAnchors,
    IReadOnlyList<ReportRelation> Relations,
    ReportGamut Gamut,
    ReportResearch Research,
    IReadOnlyList<ReportWarning> Warnings,
    string ProductionSignature,
    string ReportSignature)
{
    public required ReportSignatures Signatures { get; init; }

    public required ReportSampling Sampling { get; init; }

    public ReportDeepEvidence DeepEvidence { get; init; } = ReportDeepEvidence.Empty;
}

internal sealed record ReportSignatures(
    string ProductionDefinition,
    string ClassificationOutcomes,
    string CanonicalReport,
    string NumericCanonicalization);

internal sealed record ColorSemanticsMetadata(
    string ProductVersion,
    string Commit,
    string EvidenceMode,
    string SourceColorDomain);

internal sealed record ColorSemanticsSummary(
    int BroadFamilyCount,
    int ProfessionalTermCount,
    int RegionCount,
    int MultiRegionTermCount,
    int CreativeAnchorCount,
    int GamutSampleCount,
    int ResearchCandidateCount);

internal sealed record ReportBroadFamily(
    string Id,
    string Identity,
    string EnglishName,
    string RussianName,
    int SampleCount);

internal sealed record ReportProfessionalTerm(
    string Id,
    string Identity,
    string EnglishName,
    string RussianName,
    string Domain,
    int RegionCount,
    IReadOnlyList<string> RegionIds,
    IReadOnlyList<string> ParentFamilyIds,
    ReportColorPoint? RepresentativeCore)
{
    public ReportColorPoint? ReachabilityWitness { get; init; }

    public ReportLocalStability? LocalStability { get; init; }
}

internal sealed record ReportRegion(
    string Id,
    string TermId,
    int LobeIndex,
    IReadOnlyList<string> ParentFamilyIds,
    IReadOnlyList<string> Roles,
    ReportRange Lightness,
    ReportRange Chroma,
    ReportHueRange Hue,
    int Priority,
    ReportColorPoint? RepresentativeCore)
{
    public ReportColorPoint? ReachabilityWitness { get; init; }

    public ReportLocalStability? LocalStability { get; init; }
}

internal sealed record ReportLocalStability(
    int NeighborhoodSampleCount,
    int RetainedWinnerCount,
    double RetentionFraction,
    int MinimumRgbTransitionSteps,
    double NormalizedRegionMargin);

internal sealed record ReportRange(double MinimumInclusive, double MaximumExclusive);

internal sealed record ReportHueRange(
    double MinimumInclusive,
    double MaximumExclusive,
    bool WrapsZero);

internal sealed record ReportRelation(
    string Kind,
    string SourceId,
    string TargetId,
    string Semantics);

internal sealed record ReportGamut(
    string Space,
    int ChannelStep,
    IReadOnlyList<ReportColorPoint> Samples);

internal sealed record ReportSampling(
    IReadOnlyList<ReportSamplingCohort> Cohorts,
    IReadOnlyList<ReportClassificationOutcome> ClassificationOutcomes);

internal sealed record ReportSamplingCohort(
    string Id,
    string Purpose,
    string DenominatorMeaning,
    int SampleCount,
    int ProfessionalHitCount,
    double ProfessionalHitRate,
    bool IsCoverageAuthority);

internal sealed record ReportClassificationOutcome(
    string SampleId,
    string Hex,
    string Role,
    string Undertone,
    string BroadFamilyId,
    string LightnessClass,
    string ChromaClass,
    string? ProfessionalTermId,
    string? ProfessionalRegionId);

internal sealed record ReportColorPoint(
    string Hex,
    byte Red,
    byte Green,
    byte Blue,
    double Lightness,
    double Chroma,
    double HueDegrees,
    double X,
    double Y,
    double Z,
    string BroadFamilyId,
    string? ProfessionalTermId,
    string? ProfessionalRegionId);

internal sealed record ReportCreativeAnchor(
    string Id,
    string EnglishName,
    string RussianName,
    ReportColorPoint Point);

internal sealed record ReportResearch(
    bool Available,
    string Status,
    IReadOnlyList<ReportResearchSource> Sources,
    IReadOnlyList<ReportResearchCandidate> Candidates,
    IReadOnlyList<ReportDomainCoverage> DomainCoverage);

internal sealed record ReportResearchSource(
    string Id,
    int AnchorCount,
    string Source,
    string License,
    string Sha256,
    string Independence,
    string IndependenceGroup,
    string CachePolicy)
{
    public int LexicalOccurrenceCount { get; init; }

    public string SourceQuality { get; init; } = "UncertainProvenance";
}

internal sealed record ReportResearchCandidate(
    string CanonicalTerm,
    IReadOnlyList<string> Aliases,
    string Domain,
    string Disposition,
    string Reason,
    string RussianCandidate,
    int IndependentSourceCount,
    int AnchorCount,
    int CompactComponentCount,
    double NoiseFraction,
    string NearestShippedTerm,
    double? NearestShippedDeltaE,
    double PriorityScore,
    ReportColorPoint? Representative)
{
    public int LexicalSourceCount { get; init; }

    public int NumericSourceCount { get; init; }

    public int IndependentNumericSourceGroupCount { get; init; }

    public IReadOnlyList<ReportResearchComponent> Components { get; init; } = [];

    public IReadOnlyList<string> SynonymTermIds { get; init; } = [];
}

internal sealed record ReportResearchComponent(
    int Index,
    int AnchorCount,
    int NumericSourceCount,
    int IndependentNumericSourceGroupCount,
    double MedianDeltaE,
    double P90DeltaE,
    ReportColorPoint Representative,
    IReadOnlyList<string> SourceIds);

internal sealed record ReportDomainCoverage(
    string Domain,
    int CandidateCount,
    int AcceptedCount,
    int EvidenceRichCount,
    string VocabularyDensity);

internal sealed record ReportWarning(
    string Id,
    string Severity,
    string Kind,
    string Message,
    IReadOnlyList<string> RelatedIds);

internal sealed record ReportDeepEvidence(
    bool Available,
    IReadOnlyList<ReportRegionReachability> RegionReachability,
    IReadOnlyList<ReportOverlapPair> Overlaps,
    IReadOnlyList<ReportTermCoreEvidence> TermCores,
    IReadOnlyList<ReportBoundaryProbe> BoundaryProbes)
{
    public static ReportDeepEvidence Empty { get; } = new(false, [], [], [], []);
}

internal sealed record ReportRegionReachability(
    string RegionId,
    int MatchedSamples,
    int WinningSamples,
    bool Shadowed);

internal sealed record ReportOverlapPair(
    string WinnerTermId,
    string CompetingTermId,
    int SampleCount,
    double WinnerOverlapRatio,
    double CompetitorContainmentRatio,
    double DiceSimilarity,
    bool NearTotalContainment,
    bool SameCoreWarning,
    string Severity,
    string RepresentativeHex);

internal sealed record ReportTermCoreEvidence(
    string TermId,
    string RepresentativeHex,
    int InteriorProbeCount,
    int WinningInteriorProbeCount,
    int MatchedSamples,
    int WinningSamples,
    double MaximumContainmentRatio,
    string ConfidenceTier,
    bool MostlyDisputed);

internal sealed record ReportBoundaryProbe(
    string ProbeId,
    string Hex,
    string? WinnerTermId,
    string? WinnerRegionId,
    IReadOnlyList<string> CompetingRegionIds);