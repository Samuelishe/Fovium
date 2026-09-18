using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fovium.ColorSemantics;
using Fovium.Localization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class ColorSemanticsReportBuilder
{
    public const string Schema = "fovium-color-semantics-report/v1";
    public const int GamutChannelStep = 17;

    public static ColorSemanticsReport Build(string commit, string? researchReport = null)
    {
        var english = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("en-US")));
        var russian = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("ru-RU")));
        var adapter = new ProductionColorAdapter();
        var definitions = ProfessionalShadeCatalog.Definitions
            .OrderBy(definition => definition.StableId, StringComparer.Ordinal)
            .ToArray();
        var definitionByIdentity = definitions.ToDictionary(
            definition => definition.Term.ToString(),
            StringComparer.Ordinal);
        var boundary = ProfessionalShadeBoundaryAudit.Analyze(adapter);
        var samples = BuildGamutSamples(adapter, definitionByIdentity);
        var cores = FindCores(boundary, definitions, adapter);
        foreach (var region in definitions.SelectMany(definition => definition.Regions))
        {
            if (cores[region.StableId] is null)
            {
                cores[region.StableId] = FindSampleCore(region, samples);
            }
        }

        var creativeAnchors = BuildCreativeAnchors(definitionByIdentity);
        var families = Enum.GetValues<PerceptualHueFamily>()
            .Select(family => new ReportBroadFamily(
                StableId("family", family.ToString()),
                family.ToString(),
                english.ResolveHue(family),
                russian.ResolveHue(family),
                samples.Count(sample => sample.BroadFamilyId == StableId("family", family.ToString()))))
            .OrderBy(family => family.Id, StringComparer.Ordinal)
            .ToArray();
        var regions = definitions
            .SelectMany(definition => definition.Regions.Select((region, index) => new ReportRegion(
                region.StableId,
                definition.StableId,
                index + 1,
                region.ParentFamilies.Select(family => StableId("family", family.ToString()))
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                ExpandRoles(region.Roles),
                new ReportRange(region.MinimumLightness, region.MaximumLightness),
                new ReportRange(region.MinimumChroma, region.MaximumChroma),
                new ReportHueRange(region.MinimumHue, region.MaximumHue, region.MinimumHue > region.MaximumHue),
                region.Priority,
                cores.GetValueOrDefault(region.StableId))))
            .OrderBy(region => region.Id, StringComparer.Ordinal)
            .ToArray();
        var terms = definitions.Select(definition =>
            {
                var regionIds = definition.Regions.Select(region => region.StableId)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var parentIds = definition.Regions.SelectMany(region => region.ParentFamilies)
                    .Distinct()
                    .Select(family => StableId("family", family.ToString()))
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                return new ReportProfessionalTerm(
                    definition.StableId,
                    definition.Term.ToString(),
                    english.ResolveProfessionalTerm(definition.Term),
                    russian.ResolveProfessionalTerm(definition.Term),
                    ProfessionalTermResearchCatalog.DescribeDomain(definition.Term.ToString()).ToString(),
                    regionIds.Length,
                    regionIds,
                    parentIds,
                    definition.Regions.Select(region => cores.GetValueOrDefault(region.StableId))
                        .FirstOrDefault(core => core is not null));
            })
            .OrderBy(term => term.Id, StringComparer.Ordinal)
            .ToArray();
        var relations = BuildRelations(terms, regions);
        var research = LoadResearch(researchReport);
        var warnings = BuildWarnings(terms, regions, research);
        var version = typeof(PerceptualColorClassifier).Assembly
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? "unknown";
        var summary = new ColorSemanticsSummary(
            families.Length,
            terms.Length,
            regions.Length,
            terms.Count(term => term.RegionCount > 1),
            ColorNameCatalog.ExpectedCount,
            samples.Count,
            research.Candidates.Count);
        var metadata = new ColorSemanticsMetadata(
            version,
            commit,
            research.Available ? "production+research" : "production",
            "reference-sRGB");
        var productionSignature = Signature(new
        {
            Schema,
            Metadata = new { metadata.ProductVersion, metadata.SourceColorDomain },
            Summary = new
            {
                summary.BroadFamilyCount,
                summary.ProfessionalTermCount,
                summary.RegionCount,
                summary.MultiRegionTermCount,
                summary.CreativeAnchorCount,
                summary.GamutSampleCount
            },
            BroadFamilies = families,
            ProfessionalTerms = terms,
            Regions = regions,
            Relations = relations,
            CreativeAnchors = creativeAnchors,
            Gamut = new ReportGamut("OKLCH/reference-sRGB", GamutChannelStep, samples)
        });
        var unsigned = new
        {
            Schema,
            Metadata = metadata,
            Summary = summary,
            BroadFamilies = families,
            ProfessionalTerms = terms,
            Regions = regions,
            CreativeAnchors = creativeAnchors,
            Relations = relations,
            Gamut = new ReportGamut("OKLCH/reference-sRGB", GamutChannelStep, samples),
            Research = research,
            Warnings = warnings,
            ProductionSignature = productionSignature
        };
        var reportSignature = Signature(unsigned);
        return new ColorSemanticsReport(
            Schema,
            metadata,
            summary,
            families,
            terms,
            regions,
            creativeAnchors,
            relations,
            new ReportGamut("OKLCH/reference-sRGB", GamutChannelStep, samples),
            research,
            warnings,
            productionSignature,
            reportSignature);
    }

    internal static (double X, double Y, double Z) ToCartesian(double lightness, double chroma, double hueDegrees)
    {
        var radians = hueDegrees * Math.PI / 180;
        return (chroma * Math.Cos(radians), chroma * Math.Sin(radians), lightness);
    }

    internal static string StableId(string prefix, string identity)
    {
        var builder = new StringBuilder(prefix.Length + identity.Length + 8);
        builder.Append(prefix).Append('-');
        foreach (var character in identity)
        {
            if (char.IsUpper(character) && builder.Length > prefix.Length + 1)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static Dictionary<string, ReportColorPoint?> FindCores(
        IReadOnlyList<OwnerCandidateSample> boundary,
        IReadOnlyList<ProfessionalShadeDefinition> definitions,
        ProductionColorAdapter adapter)
    {
        var result = new Dictionary<string, ReportColorPoint?>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            foreach (var region in definition.Regions)
            {
                var prefix = region.StableId + ":";
                var probe = boundary.Where(item => item.Region.StartsWith(prefix, StringComparison.Ordinal))
                    .Where(item => item.ProfessionalExplanation?.WinnerTermStableId == definition.StableId)
                    .OrderBy(item => item.Region.EndsWith(":center", StringComparison.Ordinal) ? 0 : 1)
                    .ThenBy(item => item.Region, StringComparer.Ordinal)
                    .FirstOrDefault();
                result[region.StableId] = probe is null
                    ? FindReachableCore(definition, region, adapter)
                    : ToPoint(probe.Sample, definition.StableId, region.StableId);
            }
        }

        return result;
    }

    private static ReportColorPoint? FindSampleCore(
        ProfessionalShadeRegionDefinition region,
        IReadOnlyList<ReportColorPoint> samples)
    {
        var centerLightness = (region.MinimumLightness + region.MaximumLightness) / 2;
        var centerChroma = (region.MinimumChroma + region.MaximumChroma) / 2;
        var centerHue = InterpolateHue(region.MinimumHue, region.MaximumHue, 0.5);
        return samples.Where(sample => sample.ProfessionalRegionId == region.StableId)
            .MinBy(sample =>
            {
                var hueDistance = Math.Abs(sample.HueDegrees - centerHue);
                hueDistance = Math.Min(hueDistance, 360 - hueDistance) / 180;
                return Math.Pow(sample.Lightness - centerLightness, 2) +
                       Math.Pow(sample.Chroma - centerChroma, 2) +
                       Math.Pow(hueDistance, 2);
            });
    }

    private static ReportColorPoint? FindReachableCore(
        ProfessionalShadeDefinition definition,
        ProfessionalShadeRegionDefinition region,
        ProductionColorAdapter adapter)
    {
        var positions = new[] { 0.50, 0.35, 0.65, 0.20, 0.80 };
        foreach (var lightnessPosition in positions)
        {
            foreach (var chromaPosition in positions)
            {
                foreach (var huePosition in positions)
                {
                    var lightness = Interpolate(region.MinimumLightness, region.MaximumLightness, lightnessPosition);
                    var chroma = Interpolate(region.MinimumChroma, region.MaximumChroma, chromaPosition);
                    var hue = InterpolateHue(region.MinimumHue, region.MaximumHue, huePosition);
                    if (!AuditSampling.TryOklchToSrgb(lightness, chroma, hue, out var rgb))
                    {
                        continue;
                    }

                    var explanation = adapter.ExplainProfessional(rgb);
                    if (explanation.WinnerTermStableId == definition.StableId &&
                        explanation.WinnerRegionStableId == region.StableId)
                    {
                        return ToPoint(adapter.Classify(rgb), definition.StableId, region.StableId);
                    }
                }
            }
        }

        return null;
    }

    private static double Interpolate(double minimum, double maximum, double position) =>
        minimum + ((maximum - minimum) * position);

    private static double InterpolateHue(double minimum, double maximum, double position)
    {
        var span = minimum <= maximum ? maximum - minimum : 360 - minimum + maximum;
        return (minimum + span * position) % 360;
    }

    private static IReadOnlyList<ReportColorPoint> BuildGamutSamples(
        ProductionColorAdapter adapter,
        IReadOnlyDictionary<string, ProfessionalShadeDefinition> definitionByIdentity)
    {
        var values = Enumerable.Range(0, (255 / GamutChannelStep) + 1)
            .Select(index => (byte)Math.Min(255, index * GamutChannelStep))
            .Distinct()
            .ToArray();
        var result = new List<ReportColorPoint>(values.Length * values.Length * values.Length);
        foreach (var red in values)
        {
            foreach (var green in values)
            {
                foreach (var blue in values)
                {
                    var classification = adapter.Classify(new AuditRgb(red, green, blue));
                    var termId = classification.ProfessionalTerm is { } identity &&
                                 definitionByIdentity.TryGetValue(identity, out var definition)
                        ? definition.StableId
                        : null;
                    var regionId = termId is null
                        ? null
                        : adapter.ExplainProfessional(new AuditRgb(red, green, blue)).WinnerRegionStableId;
                    result.Add(ToPoint(classification, termId, regionId));
                }
            }
        }

        return result.OrderBy(sample => sample.Red)
            .ThenBy(sample => sample.Green)
            .ThenBy(sample => sample.Blue)
            .ToArray();
    }

    private static ReportColorPoint ToPoint(
        AuditClassification sample,
        string? termId,
        string? regionId)
    {
        var (x, y, z) = ToCartesian(sample.OklchL, sample.OklchC, sample.OklchHue);
        return new ReportColorPoint(
            sample.Rgb.Hex,
            sample.Rgb.Red,
            sample.Rgb.Green,
            sample.Rgb.Blue,
            sample.OklchL,
            sample.OklchC,
            sample.OklchHue,
            x,
            y,
            z,
            StableId("family", sample.Family),
            termId,
            regionId);
    }

    private static IReadOnlyList<ReportCreativeAnchor> BuildCreativeAnchors(
        IReadOnlyDictionary<string, ProfessionalShadeDefinition> definitionByIdentity)
    {
        var russian = ColorNameDisplayCatalog.ForLocale("ru");
        return ColorNameCatalog.LoadEmbedded().Entries
            .Select(entry =>
            {
                var description = PerceptualColorClassifier.Describe(entry.Red, entry.Green, entry.Blue);
                var professionalIdentity = description.ProfessionalTerm?.ToString();
                var termId = professionalIdentity is not null &&
                             definitionByIdentity.TryGetValue(professionalIdentity, out var definition)
                    ? definition.StableId
                    : null;
                var oklch = description.Oklch!.Value;
                var (x, y, z) = ToCartesian(oklch.L, oklch.C, oklch.HueDegrees);
                var point = new ReportColorPoint(
                    $"#{entry.Red:X2}{entry.Green:X2}{entry.Blue:X2}",
                    entry.Red,
                    entry.Green,
                    entry.Blue,
                    oklch.L,
                    oklch.C,
                    oklch.HueDegrees,
                    x,
                    y,
                    z,
                    StableId("family", description.HueFamily!.Value.ToString()),
                    termId,
                    null);
                return new ReportCreativeAnchor(
                    entry.StableId,
                    entry.CanonicalName,
                    russian.Resolve(entry.StableId, entry.CanonicalName),
                    point);
            })
            .OrderBy(anchor => anchor.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ExpandRoles(PerceptualRoleSet roles) =>
        Enum.GetValues<PerceptualColorRole>()
            .Where(role => roles.HasFlag(role switch
            {
                PerceptualColorRole.Chromatic => PerceptualRoleSet.Chromatic,
                PerceptualColorRole.Neutral => PerceptualRoleSet.Neutral,
                PerceptualColorRole.NearNeutral => PerceptualRoleSet.NearNeutral,
                PerceptualColorRole.TintedNeutral => PerceptualRoleSet.TintedNeutral,
                PerceptualColorRole.NearBlack => PerceptualRoleSet.NearBlack,
                PerceptualColorRole.NearWhite => PerceptualRoleSet.NearWhite,
                _ => PerceptualRoleSet.None
            }))
            .Select(role => role.ToString())
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ReportRelation> BuildRelations(
        IReadOnlyList<ReportProfessionalTerm> terms,
        IReadOnlyList<ReportRegion> regions)
    {
        var result = new List<ReportRelation>();
        foreach (var term in terms)
        {
            foreach (var family in term.ParentFamilyIds)
            {
                result.Add(new ReportRelation("broad-fallback", family, term.Id, "overlapping semantic fallback"));
            }
        }

        result.AddRange(regions.Select(region =>
            new ReportRelation("lobe", region.TermId, region.Id, "bounded region component")));
        return result.OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.SourceId, StringComparer.Ordinal)
            .ThenBy(item => item.TargetId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ReportResearch LoadResearch(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new ReportResearch(false, "Research audit not supplied; production truth is complete.", [], [], []);
        }

        var audit = AuditReportWriter.Read(path);
        if (audit is null)
        {
            return new ReportResearch(false, "Research audit was invalid; production truth is complete.", [], [], []);
        }

        var sources = audit.References.Select(source => new ReportResearchSource(
                source.Id,
                source.AnchorCount,
                source.Source,
                source.License,
                source.Sha256,
                source.Independence,
                source.IndependenceGroup,
                source.CachePolicy))
            .OrderBy(source => source.Id, StringComparer.Ordinal)
            .ToArray();
        var candidates = audit.MasterCandidateLexicon.Select(candidate => new ReportResearchCandidate(
                candidate.CanonicalTerm,
                candidate.Aliases.Order(StringComparer.Ordinal).ToArray(),
                candidate.ResearchDomain,
                candidate.Status.ToString(),
                candidate.Reason,
                candidate.RussianCandidate,
                candidate.IndependentSourceCount,
                candidate.AnchorCount,
                candidate.CompactComponentCount,
                candidate.NoiseFraction,
                candidate.NearestShippedTerm,
                candidate.NearestShippedDeltaE,
                candidate.PriorityScore,
                candidate.Representative is null
                    ? null
                    : ToPoint(candidate.Representative, null, null)))
            .OrderBy(candidate => candidate.CanonicalTerm, StringComparer.Ordinal)
            .ToArray();
        var coverage = audit.CandidateDomainCoverage.Select(domain => new ReportDomainCoverage(
                domain.Domain,
                domain.CandidateCount,
                domain.AcceptedCount,
                domain.EvidenceRichCount,
                domain.Density))
            .OrderBy(domain => domain.Domain, StringComparer.Ordinal)
            .ToArray();
        return new ReportResearch(true, $"Research audit loaded from schema {audit.Schema}.", sources, candidates,
            coverage);
    }

    private static IReadOnlyList<ReportWarning> BuildWarnings(
        IReadOnlyList<ReportProfessionalTerm> terms,
        IReadOnlyList<ReportRegion> regions,
        ReportResearch research)
    {
        var warnings = new List<ReportWarning>();
        warnings.AddRange(terms.Where(term => term.RepresentativeCore is null)
            .Select(term => new ReportWarning(
                $"missing-core-{term.Id}",
                "warning",
                "representative-core",
                "No winning reachable core probe was found for this production term.",
                [term.Id])));
        warnings.AddRange(regions.Where(region => region.RepresentativeCore is null)
            .Select(region => new ReportWarning(
                $"missing-core-{region.Id}",
                "info",
                "region-core",
                "This lobe has no winning center/inside probe; the term may be represented by another lobe.",
                [region.TermId, region.Id])));
        if (!research.Available)
        {
            warnings.Add(new ReportWarning(
                "research-unavailable",
                "info",
                "evidence-layer",
                research.Status,
                []));
        }

        return warnings.OrderBy(warning => warning.Id, StringComparer.Ordinal).ToArray();
    }

    private static string Signature<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}