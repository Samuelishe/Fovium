using System.Globalization;
using System.Reflection;
using System.Text;
using Fovium.ColorSemantics;
using Fovium.Localization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class ColorSemanticsReportBuilder
{
    public const string Schema = "fovium-color-semantics-report/v2";
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
        var witnesses = FindReachabilityWitnesses(boundary, definitions, adapter);
        var cores = definitions.SelectMany(definition => definition.Regions.Select(region => new
        {
            region.StableId,
            Core = FindRepresentativeCore(definition, region, adapter)
        }))
            .ToDictionary(item => item.StableId, item => item.Core, StringComparer.Ordinal);
        foreach (var region in definitions.SelectMany(definition => definition.Regions))
        {
            if (cores[region.StableId] is null)
            {
                cores[region.StableId] = FindSampleCore(region, samples);
            }

            if (witnesses[region.StableId] is null)
            {
                witnesses[region.StableId] = cores[region.StableId];
            }
        }

        var stability = definitions.SelectMany(definition => definition.Regions.Select(region => new
        {
            region.StableId,
            Stability = MeasureLocalStability(definition, region, cores[region.StableId], adapter)
        }))
            .ToDictionary(item => item.StableId, item => item.Stability, StringComparer.Ordinal);

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
                new ReportRange(
                    CanonicalSemanticIdentity.Semantic(region.MinimumLightness),
                    CanonicalSemanticIdentity.Semantic(region.MaximumLightness)),
                new ReportRange(
                    CanonicalSemanticIdentity.Semantic(region.MinimumChroma),
                    CanonicalSemanticIdentity.Semantic(region.MaximumChroma)),
                new ReportHueRange(
                    CanonicalSemanticIdentity.Semantic(region.MinimumHue),
                    CanonicalSemanticIdentity.Semantic(region.MaximumHue),
                    region.MinimumHue > region.MaximumHue),
                region.Priority,
                cores.GetValueOrDefault(region.StableId))
            {
                ReachabilityWitness = witnesses.GetValueOrDefault(region.StableId),
                LocalStability = stability.GetValueOrDefault(region.StableId)
            }))
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
                var representative = definition.Regions.Select(region => cores.GetValueOrDefault(region.StableId))
                    .Where(core => core is not null)
                    .OrderByDescending(core => stability.GetValueOrDefault(
                        core!.ProfessionalRegionId!)?.NormalizedRegionMargin ?? 0)
                    .ThenBy(core => core!.Hex, StringComparer.Ordinal)
                    .FirstOrDefault();
                return new ReportProfessionalTerm(
                    definition.StableId,
                    definition.Term.ToString(),
                    english.ResolveProfessionalTerm(definition.Term),
                    russian.ResolveProfessionalTerm(definition.Term),
                    ProfessionalTermResearchCatalog.DescribeDomain(definition.Term.ToString()).ToString(),
                    regionIds.Length,
                    regionIds,
                    parentIds,
                    representative)
                {
                    ReachabilityWitness = definition.Regions
                        .Select(region => witnesses.GetValueOrDefault(region.StableId))
                        .FirstOrDefault(point => point is not null),
                    LocalStability = representative?.ProfessionalRegionId is { } representativeRegion
                        ? stability.GetValueOrDefault(representativeRegion)
                        : null
                };
            })
            .OrderBy(term => term.Id, StringComparer.Ordinal)
            .ToArray();
        var relations = BuildRelations(terms, regions);
        var research = LoadResearch(researchReport, out var deepEvidence);
        var warnings = BuildWarnings(terms, regions, research, deepEvidence);
        var classificationOutcomes = BuildClassificationOutcomes(adapter, definitionByIdentity);
        var sampling = BuildSampling(samples, classificationOutcomes, boundary, witnesses, creativeAnchors);
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
        var productionSignature = CanonicalSemanticIdentity.BuildDefinitionSignature();
        var outcomeSignature = CanonicalSemanticIdentity.BuildOutcomeSignature(classificationOutcomes);
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
            DeepEvidence = deepEvidence,
            Sampling = sampling,
            ProductionSignature = productionSignature,
            ClassificationOutcomeSignature = outcomeSignature,
            CanonicalSemanticIdentity.NumericContract
        };
        var reportSignature = CanonicalSemanticIdentity.Hash(unsigned);
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
            reportSignature)
        {
            Signatures = new ReportSignatures(
                productionSignature,
                outcomeSignature,
                reportSignature,
                CanonicalSemanticIdentity.NumericContract),
            Sampling = sampling,
            DeepEvidence = deepEvidence
        };
    }

    internal static (double X, double Y, double Z) ToCartesian(double lightness, double chroma, double hueDegrees)
    {
        var radians = hueDegrees * Math.PI / 180;
        return (
            CanonicalSemanticIdentity.Derived(chroma * Math.Cos(radians)),
            CanonicalSemanticIdentity.Derived(chroma * Math.Sin(radians)),
            CanonicalSemanticIdentity.Derived(lightness));
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

    private static Dictionary<string, ReportColorPoint?> FindReachabilityWitnesses(
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

    private static ReportColorPoint? FindRepresentativeCore(
        ProfessionalShadeDefinition definition,
        ProfessionalShadeRegionDefinition region,
        ProductionColorAdapter adapter)
    {
        var positions = new[] { 0.50, 0.40, 0.60, 0.30, 0.70, 0.20, 0.80 };
        var candidates = new List<(ReportColorPoint Point, double Margin)>();
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
                    if (explanation.WinnerRegionStableId != region.StableId)
                    {
                        continue;
                    }

                    candidates.Add((
                        ToPoint(adapter.Classify(rgb), definition.StableId, region.StableId),
                        RegionMargin(lightness, chroma, hue, region)));
                }
            }
        }

        return candidates.OrderByDescending(item => CanonicalSemanticIdentity.Derived(item.Margin))
            .ThenBy(item => item.Point.Hex, StringComparer.Ordinal)
            .Select(item => item.Point)
            .FirstOrDefault();
    }

    private static ReportColorPoint? FindSampleCore(
        ProfessionalShadeRegionDefinition region,
        IReadOnlyList<ReportColorPoint> samples)
    {
        var centerLightness = (region.MinimumLightness + region.MaximumLightness) / 2;
        var centerChroma = (region.MinimumChroma + region.MaximumChroma) / 2;
        var centerHue = InterpolateHue(region.MinimumHue, region.MaximumHue, 0.5);
        return samples.Where(sample => sample.ProfessionalRegionId == region.StableId)
            .OrderBy(sample =>
            {
                var hueDistance = Math.Abs(sample.HueDegrees - centerHue);
                hueDistance = Math.Min(hueDistance, 360 - hueDistance) / 180;
                return CanonicalSemanticIdentity.Derived(
                    Math.Pow(sample.Lightness - centerLightness, 2) +
                    Math.Pow(sample.Chroma - centerChroma, 2) +
                    Math.Pow(hueDistance, 2));
            })
            .ThenBy(sample => sample.Hex, StringComparer.Ordinal)
            .FirstOrDefault();
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

    private static ReportLocalStability? MeasureLocalStability(
        ProfessionalShadeDefinition definition,
        ProfessionalShadeRegionDefinition region,
        ReportColorPoint? representative,
        ProductionColorAdapter adapter)
    {
        if (representative is null)
        {
            return null;
        }

        var retained = 0;
        var total = 0;
        var transition = 9;
        for (var radius = 1; radius <= 8; radius++)
        {
            var radiusChanged = false;
            foreach (var (red, green, blue) in new[]
                     {
                         (radius, 0, 0), (-radius, 0, 0), (0, radius, 0),
                         (0, -radius, 0), (0, 0, radius), (0, 0, -radius)
                     })
            {
                var r = representative.Red + red;
                var g = representative.Green + green;
                var b = representative.Blue + blue;
                if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
                {
                    continue;
                }

                total++;
                var explanation = adapter.ExplainProfessional(new AuditRgb((byte)r, (byte)g, (byte)b));
                if (explanation.WinnerTermStableId == definition.StableId &&
                    explanation.WinnerRegionStableId == region.StableId)
                {
                    retained++;
                }
                else
                {
                    radiusChanged = true;
                }
            }

            if (radiusChanged && transition == 9)
            {
                transition = radius;
            }
        }

        return new ReportLocalStability(
            total,
            retained,
            total == 0 ? 0 : CanonicalSemanticIdentity.Derived((double)retained / total),
            transition,
            CanonicalSemanticIdentity.Derived(RegionMargin(
                representative.Lightness,
                representative.Chroma,
                representative.HueDegrees,
                region)));
    }

    private static double RegionMargin(
        double lightness,
        double chroma,
        double hue,
        ProfessionalShadeRegionDefinition region)
    {
        var lightnessSpan = region.MaximumLightness - region.MinimumLightness;
        var chromaSpan = region.MaximumChroma - region.MinimumChroma;
        var hueSpan = region.MinimumHue <= region.MaximumHue
            ? region.MaximumHue - region.MinimumHue
            : 360 - region.MinimumHue + region.MaximumHue;
        var hueOffset = (hue - region.MinimumHue + 360) % 360;
        return new[]
        {
            (lightness - region.MinimumLightness) / lightnessSpan,
            (region.MaximumLightness - lightness) / lightnessSpan,
            (chroma - region.MinimumChroma) / chromaSpan,
            (region.MaximumChroma - chroma) / chromaSpan,
            hueOffset / hueSpan,
            (hueSpan - hueOffset) / hueSpan
        }.Min();
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

    private static IReadOnlyList<ReportClassificationOutcome> BuildClassificationOutcomes(
        ProductionColorAdapter adapter,
        IReadOnlyDictionary<string, ProfessionalShadeDefinition> definitionByIdentity)
    {
        var colors = new SortedSet<int>();
        for (var red = 0; red <= 255; red += GamutChannelStep)
        {
            for (var green = 0; green <= 255; green += GamutChannelStep)
            {
                for (var blue = 0; blue <= 255; blue += GamutChannelStep)
                {
                    colors.Add((red << 16) | (green << 8) | blue);
                }
            }
        }

        colors.Add(0xFFFFFF);
        var nearNeutral = new SortedSet<int>();
        foreach (var gray in Enumerable.Range(0, 33).Select(index => Math.Min(255, index * 8)))
        {
            foreach (var redOffset in new[] { -6, -3, -1, 0, 1, 3, 6 })
            {
                foreach (var blueOffset in new[] { -6, -3, -1, 0, 1, 3, 6 })
                {
                    var red = Math.Clamp(gray + redOffset, 0, 255);
                    var blue = Math.Clamp(gray + blueOffset, 0, 255);
                    nearNeutral.Add((red << 16) | (gray << 8) | blue);
                }
            }
        }

        return colors.Select(packed => CreateOutcome("spectrum", packed))
            .Concat(nearNeutral.Select(packed => CreateOutcome("near-neutral", packed)))
            .OrderBy(item => item.SampleId, StringComparer.Ordinal)
            .ToArray();

        ReportClassificationOutcome CreateOutcome(string cohort, int packed)
        {
            var rgb = new AuditRgb((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
            var classification = adapter.Classify(rgb);
            var explanation = adapter.ExplainProfessional(rgb);
            var termId = classification.ProfessionalTerm is { } identity &&
                         definitionByIdentity.TryGetValue(identity, out var definition)
                ? definition.StableId
                : null;
            return new ReportClassificationOutcome(
                $"{cohort}:{rgb.Hex}",
                rgb.Hex,
                classification.Role,
                classification.Undertone,
                StableId("family", classification.Family),
                classification.Lightness,
                classification.Chroma,
                termId,
                explanation.WinnerRegionStableId);
        }
    }

    private static ReportSampling BuildSampling(
        IReadOnlyList<ReportColorPoint> gamut,
        IReadOnlyList<ReportClassificationOutcome> outcomes,
        IReadOnlyList<OwnerCandidateSample> boundary,
        IReadOnlyDictionary<string, ReportColorPoint?> witnesses,
        IReadOnlyList<ReportCreativeAnchor> creativeAnchors)
    {
        var spectrum = outcomes.Where(item => item.SampleId.StartsWith("spectrum:", StringComparison.Ordinal))
            .ToArray();
        var nearNeutral = outcomes.Where(item => item.SampleId.StartsWith("near-neutral:", StringComparison.Ordinal))
            .ToArray();
        var cohorts = new[]
        {
            Cohort("visualization-gamut-cloud", "Bounded interactive reference-sRGB shape",
                "16×16×16 RGB grid; visualization only", gamut.Count,
                gamut.Count(item => item.ProfessionalTermId is not null), false),
            Cohort("whole-spectrum-semantic-audit", "Fixed RGB classification outcome regression",
                "16×16×16 RGB grid including exact white", spectrum.Length,
                spectrum.Count(item => item.ProfessionalTermId is not null), true),
            Cohort("near-neutral-targeted", "Thin neutral/off-white and weak-tint exercise",
                "Gray axis at 8-code steps with bounded red/blue offsets", nearNeutral.Length,
                nearNeutral.Count(item => item.ProfessionalTermId is not null), true),
            Cohort("region-reachability", "One winning witness sought for every production lobe",
                "All production regions/lobes", witnesses.Count,
                witnesses.Count(item => item.Value is not null), true),
            Cohort("boundary-counterexamples", "Inside/outside and sibling/fallback probes",
                "Generated deterministic region boundary probes", boundary.Count,
                boundary.Count(item => item.ProfessionalExplanation?.WinnerTermStableId is not null), true),
            Cohort("creative-anchors", "Optional creative-name reference anchors",
                "Tracked creative catalog; not structural taxonomy truth", creativeAnchors.Count,
                creativeAnchors.Count(item => item.Point.ProfessionalTermId is not null), false)
        };
        return new ReportSampling(cohorts, outcomes);

        static ReportSamplingCohort Cohort(
            string id,
            string purpose,
            string denominator,
            int count,
            int hits,
            bool authority) => new(
            id,
            purpose,
            denominator,
            count,
            hits,
            count == 0 ? 0 : CanonicalSemanticIdentity.Derived((double)hits / count),
            authority);
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
            CanonicalSemanticIdentity.Derived(sample.OklchL),
            CanonicalSemanticIdentity.Derived(sample.OklchC),
            CanonicalSemanticIdentity.Hue(sample.OklchHue),
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
                    CanonicalSemanticIdentity.Derived(oklch.L),
                    CanonicalSemanticIdentity.Derived(oklch.C),
                    CanonicalSemanticIdentity.Hue(oklch.HueDegrees),
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

    private static ReportResearch LoadResearch(string? path, out ReportDeepEvidence deepEvidence)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            deepEvidence = ReportDeepEvidence.Empty;
            return new ReportResearch(false, "Research audit not supplied; production truth is complete.", [], [], []);
        }

        var audit = AuditReportWriter.Read(path);
        if (audit is null)
        {
            deepEvidence = ReportDeepEvidence.Empty;
            return new ReportResearch(false, "Research audit was invalid; production truth is complete.", [], [], []);
        }

        var termIds = ProfessionalShadeCatalog.Definitions.ToDictionary(
            definition => definition.Term.ToString(),
            definition => definition.StableId,
            StringComparer.Ordinal);
        var candidateProfiles = audit.VocabularyCandidates.ToDictionary(
            candidate => candidate.SpecificTerm,
            StringComparer.Ordinal);

        var sources = audit.References.Select(source => new ReportResearchSource(
                source.Id,
                source.AnchorCount,
                source.Source,
                source.License,
                source.Sha256,
                source.Independence,
                source.IndependenceGroup,
                source.CachePolicy)
        {
            LexicalOccurrenceCount = source.LexicalOccurrenceCount
        })
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
                    : ToPoint(candidate.Representative, null, null))
        {
            LexicalSourceCount = candidate.LexicalSourceCount,
            NumericSourceCount = candidate.NumericSourceCount,
            IndependentNumericSourceGroupCount = candidate.IndependentNumericSourceGroupCount,
            Components = candidateProfiles.GetValueOrDefault(candidate.CanonicalTerm)?.Components
                    .Select(component => new ReportResearchComponent(
                        component.ComponentIndex,
                        component.AnchorCount,
                        component.SupportingDatasets.Count,
                        candidate.ComponentEvidence
                            .FirstOrDefault(item => item.ComponentIndex == component.ComponentIndex)
                            ?.IndependentNumericSourceGroupCount ?? 0,
                        CanonicalSemanticIdentity.Derived(component.MedianDeltaE),
                        CanonicalSemanticIdentity.Derived(component.P90DeltaE),
                        ToPoint(component.Representative, null, null),
                        component.SupportingDatasets.Order(StringComparer.Ordinal).ToArray()))
                    .OrderBy(component => component.Index)
                    .ToArray() ?? [],
            SynonymTermIds = candidate.Status == CandidateResearchStatus.Synonym
                    ? termIds.Where(item => candidate.Reason.Contains(item.Key, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.Value)
                        .Order(StringComparer.Ordinal)
                        .ToArray()
                    : []
        })
            .OrderBy(candidate => candidate.CanonicalTerm, StringComparer.Ordinal)
            .ToArray();
        var coverage = audit.CandidateDomainCoverage.Select(domain => new ReportDomainCoverage(
                domain.Domain,
                domain.CandidateCount,
                domain.AcceptedCount,
                domain.EvidenceRichCount,
                domain.VocabularyDensity))
            .OrderBy(domain => domain.Domain, StringComparer.Ordinal)
            .ToArray();
        deepEvidence = new ReportDeepEvidence(
            true,
            audit.ProfessionalOverlaps.Regions.Select(region => new ReportRegionReachability(
                    region.RegionStableId,
                    region.MatchedSamples,
                    region.WinningSamples,
                    region.IsShadowed))
                .OrderBy(region => region.RegionId, StringComparer.Ordinal)
                .ToArray(),
            audit.ProfessionalOverlaps.Pairs.Select(pair => new ReportOverlapPair(
                    termIds.GetValueOrDefault(pair.WinnerTerm, pair.WinnerTerm),
                    termIds.GetValueOrDefault(pair.CompetingTerm, pair.CompetingTerm),
                    pair.SampleCount,
                    CanonicalSemanticIdentity.Derived(pair.WinnerOverlapRatio),
                    CanonicalSemanticIdentity.Derived(pair.CompetitorContainmentRatio),
                    CanonicalSemanticIdentity.Derived(pair.SimilarityScore),
                    pair.NearTotalContainment,
                    pair.SameCoreDuplicateWarning,
                    pair.Severity.ToString(),
                    pair.RepresentativeHex))
                .OrderBy(pair => pair.WinnerTermId, StringComparer.Ordinal)
                .ThenBy(pair => pair.CompetingTermId, StringComparer.Ordinal)
                .ToArray(),
            audit.ProfessionalTermCores.Select(core => new ReportTermCoreEvidence(
                    termIds.GetValueOrDefault(core.Term, core.Term),
                    core.RepresentativeCoreHex,
                    core.InteriorProbeCount,
                    core.WinningInteriorProbeCount,
                    core.MatchedSamples,
                    core.WinningSamples,
                    CanonicalSemanticIdentity.Derived(core.MaximumContainmentRatio),
                    core.ConfidenceTier,
                    core.IsMostlyDisputed))
                .OrderBy(core => core.TermId, StringComparer.Ordinal)
                .ToArray(),
            audit.ProfessionalBoundarySamples.Select(sample => new ReportBoundaryProbe(
                    sample.Region,
                    sample.Sample.Rgb.Hex,
                    sample.ProfessionalExplanation?.WinnerTermStableId,
                    sample.ProfessionalExplanation?.WinnerRegionStableId,
                    sample.ProfessionalExplanation?.Candidates.Where(candidate => candidate.Matched)
                        .Select(candidate => candidate.RegionStableId)
                        .Order(StringComparer.Ordinal)
                        .ToArray() ?? []))
                .OrderBy(probe => probe.ProbeId, StringComparer.Ordinal)
                .ThenBy(probe => probe.Hex, StringComparer.Ordinal)
                .ToArray());
        return new ReportResearch(true, $"Research audit loaded from schema {audit.Schema}.", sources, candidates,
            coverage);
    }

    private static IReadOnlyList<ReportWarning> BuildWarnings(
        IReadOnlyList<ReportProfessionalTerm> terms,
        IReadOnlyList<ReportRegion> regions,
        ReportResearch research,
        ReportDeepEvidence deepEvidence)
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
        warnings.AddRange(deepEvidence.RegionReachability.Where(region => region.Shadowed)
            .Select(region => new ReportWarning(
                $"shadowed-{region.RegionId}",
                "error",
                "shadowed-region",
                "The deep audit observed matches but no wins for this production lobe.",
                [region.RegionId])));
        warnings.AddRange(deepEvidence.Overlaps.Where(pair => pair.SameCoreWarning)
            .Select(pair => new ReportWarning(
                $"same-core-{pair.WinnerTermId}-{pair.CompetingTermId}",
                "warning",
                "same-core-overlap",
                $"Dice {pair.DiceSimilarity:0.000}; directional containment " +
                $"{pair.WinnerOverlapRatio:0.000}/{pair.CompetitorContainmentRatio:0.000}.",
                [pair.WinnerTermId, pair.CompetingTermId])));
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
}