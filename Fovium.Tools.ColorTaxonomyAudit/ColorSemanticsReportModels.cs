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
    string ReportSignature);

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
    ReportColorPoint? RepresentativeCore);

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
    ReportColorPoint? RepresentativeCore);

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
    string CachePolicy);

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
    ReportColorPoint? Representative);

internal sealed record ReportDomainCoverage(
    string Domain,
    int CandidateCount,
    int AcceptedCount,
    int EvidenceRichCount,
    string Density);

internal sealed record ReportWarning(
    string Id,
    string Severity,
    string Kind,
    string Message,
    IReadOnlyList<string> RelatedIds);