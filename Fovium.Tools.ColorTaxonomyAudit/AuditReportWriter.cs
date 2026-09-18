using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class AuditReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Write(string directory, AuditReport report)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "summary.json"),
            JsonSerializer.Serialize(report, JsonOptions) + Environment.NewLine,
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(directory, "summary.md"),
            CreateMarkdown(report),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(directory, "summary.html"),
            CreateHtml(report),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(directory, "anomalies.csv"),
            CreateCsv(report.Anomalies),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(directory, "contact-sheet.svg"),
            CreateSvg(report.Anomalies.Take(100).ToArray()),
            new UTF8Encoding(false));
        WriteSampleSheet(
            directory,
            "balanced-spectrum",
            "Balanced whole-spectrum cohort",
            report.BalancedCohort.Select(item => new SheetItem(
                item.CohortId,
                item.Sample,
                item.Reference,
                $"{item.HueStratum} · {item.LightnessStratum} · {item.ChromaStratum}")));
        WriteSampleSheet(
            directory,
            "owner-candidates",
            "Owner candidate regions",
            report.OwnerCandidates.Select(item => new SheetItem(
                item.Region,
                item.Sample,
                item.Reference,
                item.Region)));
        WriteSampleSheet(
            directory,
            "reference-disagreements",
            "Balanced external-reference disagreements",
            report.BalancedCohort
                .Where(item => item.Reference is { IsCompatible: false })
                .OrderByDescending(item => item.Reference!.ConsensusSupport)
                .ThenBy(item => item.Sample.Rgb.Packed)
                .Select(item => new SheetItem(item.CohortId, item.Sample, item.Reference, item.CohortId)));
        WriteSampleSheet(
            directory,
            "vocabulary-gaps",
            "Specific professional vocabulary-gap candidates",
            report.VocabularyGaps.Select(item => new SheetItem(
                item.SpecificTerm,
                item.Sample,
                item.Reference,
                $"{item.DatasetSupport} datasets: {string.Join(", ", item.SupportingDatasets)}")));
        WriteSampleSheet(
            directory,
            "vocabulary-candidates",
            "Reference-driven professional vocabulary clusters",
            report.VocabularyCandidates.Select(item => new SheetItem(
                item.SpecificTerm,
                item.Representative,
                null,
                $"{item.DatasetSupport} datasets · {item.AnchorCount} anchors · p90 ΔE {item.P90DeltaE:0.000}")));
        WriteSampleSheet(
            directory,
            "vocabulary-candidate-components",
            "Compact components inside recurring vocabulary candidates",
            report.VocabularyCandidates.SelectMany(item => item.Components.Select(component => new SheetItem(
                $"{item.SpecificTerm} · component {component.ComponentIndex}",
                component.Representative,
                null,
                $"{component.DatasetSupport} datasets · {component.AnchorCount} anchors · p90 ΔE {component.P90DeltaE:0.000} · noise {item.NoiseAnchorCount}"))));
        WriteSampleSheet(
            directory,
            "professional-terms",
            "Accepted professional-term reference anchors",
            report.ProfessionalTermSamples.Select(item => new SheetItem(
                item.Region,
                item.Sample,
                item.Reference,
                $"Base family: {item.Sample.Family} · region: {item.ProfessionalExplanation?.WinnerRegionStableId}")));
        WriteSampleSheet(
            directory,
            "professional-boundary-probes",
            "Professional-term boundary and counterexample probes",
            report.ProfessionalBoundarySamples.Select(item => new SheetItem(
                item.Region,
                item.Sample,
                null,
                $"Resolved: {item.Sample.ProfessionalTerm ?? item.Sample.Family} · winner: {item.ProfessionalExplanation?.WinnerRegionStableId ?? "fallback"}")));
        WriteSampleSheet(
            directory,
            "holdout-samples",
            report.Seed == AuditOptions.DefaultSeed ? "Canonical semantic cohort" : "Independent holdout cohort",
            report.BalancedCohort.Select(item => new SheetItem(
                item.CohortId,
                item.Sample,
                item.Reference,
                item.CohortId)));
        WriteSampleSheet(
            directory,
            "changed-regions",
            "Changed balanced regions",
            (report.Comparison?.ChangedBalancedSamples ?? [])
            .Select(change => new SheetItem(
                $"{change.Before.Family} → {change.After.Family}",
                change.After,
                null,
                $"{change.Before.DetailedName} → {change.After.DetailedName}")));
        File.WriteAllText(
            Path.Combine(directory, "family-profiles.html"),
            CreateFamilyProfilesHtml(report.FamilyProfiles),
            new UTF8Encoding(false));

        var deterministic = report with
        {
            Metrics = report.Metrics with
            {
                RuntimeSeconds = 0,
                ProfessionalClassificationNanosecondsPerSample = 0
            },
            Comparison = null,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(deterministic, JsonOptions);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        File.WriteAllText(
            Path.Combine(directory, "deterministic-signature.sha256"),
            hash + Environment.NewLine,
            new UTF8Encoding(false));
        return hash;
    }

    public static AuditReport? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AuditReport>(File.ReadAllText(path), JsonOptions);
    }

    private static string CreateMarkdown(AuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Fovium color taxonomy audit");
        builder.AppendLine();
        builder.AppendLine(
            $"Mode: `{report.Mode}` · Seed: `{report.Seed}` · Runtime: `{report.Metrics.RuntimeSeconds:0.###} s`");
        builder.AppendLine();
        builder.AppendLine("External references rank evidence only; they are not product ground truth.");
        builder.AppendLine();
        builder.AppendLine("## Metrics");
        builder.AppendLine();
        builder.AppendLine("| Metric | Value |");
        builder.AppendLine("| --- | ---: |");
        AppendMetric(builder, "Unique samples", report.Metrics.TotalUniqueSamples);
        AppendMetric(builder, "Structured in-gamut cells", report.Metrics.StructuredSamples);
        AppendMetric(builder, "Monte-Carlo samples", report.Metrics.MonteCarloSamples);
        AppendMetric(builder, "RGB-grid samples", report.Metrics.RgbGridSamples);
        AppendMetric(builder, "Boundary-refinement samples", report.Metrics.BoundaryRefinementSamples);
        AppendMetric(builder, "Boundary edges", report.Metrics.BoundaryEdges);
        AppendMetric(builder, "Abrupt discontinuities", report.Metrics.AbruptDiscontinuities);
        AppendMetric(builder, "Chroma role reversals", report.Metrics.ChromaReversals);
        AppendMetric(builder, "Lightness family oscillations", report.Metrics.LightnessOscillations);
        AppendMetric(builder, "Modifier reversals", report.Metrics.ModifierReversals);
        AppendMetric(builder, "Tiny components", report.Metrics.TinyComponents);
        AppendMetric(builder, "Thin slivers", report.Metrics.ThinSlivers);
        AppendMetric(builder, "Reference disagreements", report.Metrics.ReferenceDisagreements);
        AppendMetric(builder, "Balanced semantic samples", report.Metrics.BalancedSemanticSamples);
        AppendMetric(builder, "Balanced reference assessments", report.Metrics.BalancedReferenceAssessments);
        AppendMetric(
            builder,
            "Balanced incompatible disagreements",
            report.Metrics.BalancedIncompatibleDisagreements);
        AppendMetric(builder, "Generic-family-only balanced samples", report.Specificity.GenericFamilyOnly);
        AppendMetric(builder, "Existing specific-family balanced samples", report.Specificity.ExistingSpecificFamily);
        AppendMetric(builder, "Professional-term balanced samples", report.Specificity.ProfessionalTerm);
        AppendMetric(builder, "Neutral-role balanced samples", report.Specificity.NeutralRole);
        AppendMetric(builder, "Vocabulary-gap candidates", report.Specificity.VocabularyGapCandidates);
        AppendMetric(builder, "High-severity anomalies", report.Metrics.HighSeverityAnomalies);
        AppendMetric(builder, "Medium-severity anomalies", report.Metrics.MediumSeverityAnomalies);
        builder.AppendLine(
            $"| Professional classifier benchmark | {report.Metrics.ProfessionalClassificationNanosecondsPerSample:0.0} ns/sample |");

        if (report.References.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Reference inputs");
            builder.AppendLine();
            builder.AppendLine("| Dataset | Anchors | Normalized | Source | License / usage | SHA-256 |");
            builder.AppendLine("| --- | ---: | ---: | --- | --- | --- |");
            foreach (var reference in report.References)
            {
                builder.AppendLine(
                    $"| {reference.Id} | {reference.AnchorCount} | {reference.NormalizedAnchorCount} | {EscapeMarkdown(reference.Source)} | {EscapeMarkdown(reference.License)} | `{reference.Sha256}` |");
            }
        }

        if (report.Comparison is { } comparison)
        {
            builder.AppendLine();
            builder.AppendLine("## Baseline comparison");
            builder.AppendLine();
            builder.AppendLine($"Baseline: `{comparison.BaselinePath}`");
            builder.AppendLine();
            builder.AppendLine(
                $"High severity Δ `{comparison.HighSeverityDelta:+#;-#;0}`, medium Δ `{comparison.MediumSeverityDelta:+#;-#;0}`, " +
                $"abrupt Δ `{comparison.AbruptDiscontinuityDelta:+#;-#;0}`, reference disagreement Δ `{comparison.ReferenceDisagreementDelta:+#;-#;0}`, " +
                $"balanced incompatible Δ `{comparison.BalancedIncompatibleDisagreementDelta:+#;-#;0}`, " +
                $"changed balanced samples `{comparison.ChangedBalancedSamples.Count}`.");
        }

        builder.AppendLine();
        builder.AppendLine("## Balanced coordinate coverage");
        builder.AppendLine();
        AppendCoverage(builder, "Hue", report.BalancedHueCoverage);
        AppendCoverage(builder, "Lightness", report.BalancedLightnessCoverage);
        AppendCoverage(builder, "Chroma", report.BalancedChromaCoverage);

        builder.AppendLine();
        builder.AppendLine("## Family coverage");
        builder.AppendLine();
        builder.AppendLine("| Family | Samples | Share |");
        builder.AppendLine("| --- | ---: | ---: |");
        foreach (var (family, count) in report.FamilyCoverage.OrderByDescending(pair => pair.Value)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| {family} | {count} | {(double)count / report.Metrics.TotalUniqueSamples:P2} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Professional-term coverage");
        builder.AppendLine();
        builder.AppendLine("| Term | Samples | Share of audited sRGB cohort |");
        builder.AppendLine("| --- | ---: | ---: |");
        foreach (var (term, count) in report.ProfessionalTermCoverage.OrderByDescending(pair => pair.Value)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"| {term} | {count} | {(double)count / report.Metrics.TotalUniqueSamples:P2} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Professional-term match explanations");
        builder.AppendLine();
        builder.AppendLine("| Term | HEX | Winner region | Closest competing definitions |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var sample in report.ProfessionalTermSamples)
        {
            var explanation = sample.ProfessionalExplanation;
            var competitors = explanation is null
                ? string.Empty
                : string.Join(", ", explanation.Candidates
                    .Where(item => item.RegionStableId != explanation.WinnerRegionStableId)
                    .OrderByDescending(item => item.Priority)
                    .Take(5)
                    .Select(item => $"{item.RegionStableId}:{item.FailureReason}"));
            builder.AppendLine(
                $"| {sample.Region} | {sample.Sample.Rgb.Hex} | {explanation?.WinnerRegionStableId} | {EscapeMarkdown(competitors)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Balanced family semantic profiles");
        builder.AppendLine();
        builder.AppendLine(
            "| Family | Samples | Incompatible | Rate | Competitor | Center | Edge | Dark / light | Low / high C |");
        builder.AppendLine("| --- | ---: | ---: | ---: | --- | --- | --- | --- | --- |");
        foreach (var profile in report.FamilyProfiles)
        {
            builder.AppendLine(
                $"| {profile.Family} | {profile.SampleCount} | {profile.IncompatibleReferenceCount} | {profile.IncompatibleReferenceRate:P1} | {profile.NearestCompetingFamily} | {profile.Center.Rgb.Hex} | {profile.Edge.Rgb.Hex} | {profile.Dark.Rgb.Hex} / {profile.Light.Rgb.Hex} | {profile.LowChroma.Rgb.Hex} / {profile.HighChroma.Rgb.Hex} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Professional vocabulary gaps");
        builder.AppendLine();
        builder.AppendLine("| Candidate | Support | HEX | Current name | Base family | Datasets |");
        builder.AppendLine("| --- | ---: | --- | --- | --- | --- |");
        foreach (var gap in report.VocabularyGaps)
        {
            builder.AppendLine(
                $"| {gap.SpecificTerm} | {gap.DatasetSupport} | {gap.Sample.Rgb.Hex} | {EscapeMarkdown(gap.Sample.DetailedName)} | {gap.Sample.Family} | {string.Join(", ", gap.SupportingDatasets)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Reference-driven vocabulary candidate clusters");
        builder.AppendLine();
        builder.AppendLine(
            "| Candidate | Shipped | Datasets | Anchors | Medoid | Median ΔE | P90 ΔE | Production families |");
        builder.AppendLine("| --- | --- | ---: | ---: | --- | ---: | ---: | --- |");
        foreach (var candidate in report.VocabularyCandidates)
        {
            builder.AppendLine(
                $"| {candidate.SpecificTerm} | {candidate.IsShippedTerm} | {candidate.DatasetSupport} | {candidate.AnchorCount} | {candidate.Representative.Rgb.Hex} | {candidate.MedianDeltaE:0.000} | {candidate.P90DeltaE:0.000} | {EscapeMarkdown(string.Join(", ", candidate.ProductionFamilyCoverage.Select(pair => $"{pair.Key}={pair.Value}")))} |");
        }

        builder.AppendLine();
        builder.AppendLine("### Compact candidate components");
        builder.AppendLine();
        builder.AppendLine(
            "| Candidate | Component | Datasets | Anchors | Medoid | P90 ΔE | Noise | Production families |");
        builder.AppendLine("| --- | ---: | ---: | ---: | --- | ---: | ---: | --- |");
        foreach (var candidate in report.VocabularyCandidates)
        {
            foreach (var component in candidate.Components)
            {
                builder.AppendLine(
                    $"| {candidate.SpecificTerm} | {component.ComponentIndex} | {component.DatasetSupport} | {component.AnchorCount} | {component.Representative.Rgb.Hex} | {component.P90DeltaE:0.000} | {candidate.NoiseAnchorCount} | {EscapeMarkdown(string.Join(", ", component.ProductionFamilyCoverage.Select(pair => $"{pair.Key}={pair.Value}")))} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Owner regression seeds");
        builder.AppendLine();
        builder.AppendLine("| HEX | Role | Family | Short name | Detailed name |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var sample in report.OwnerSeeds)
        {
            builder.AppendLine(
                $"| {sample.Rgb.Hex} | {sample.Role} | {sample.Family} | {sample.ShortName} | {sample.DetailedName} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Owner candidate evidence");
        builder.AppendLine();
        builder.AppendLine(
            "| Region | HEX | Fovium | Product semantic | Reference consensus | Compatible | Dataset votes |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var candidate in report.OwnerCandidates)
        {
            var reference = candidate.Reference;
            builder.AppendLine(
                $"| {EscapeMarkdown(candidate.Region)} | {candidate.Sample.Rgb.Hex} | {candidate.Sample.DetailedName} | {reference?.ProductSemantic} | {reference?.ConsensusSemantic} | {reference?.IsCompatible} | {EscapeMarkdown(ReferenceVotes(reference))} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Top anomalies");
        builder.AppendLine();
        builder.AppendLine("| ID | Severity | Kind | HEX | Fovium | Reason | Reference evidence |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var anomaly in report.Anomalies.Take(100))
        {
            builder.AppendLine(
                $"| {anomaly.Id} | {anomaly.Severity} | {anomaly.Kind} | {anomaly.Sample.Rgb.Hex} | {anomaly.Sample.DetailedName} | {EscapeMarkdown(anomaly.Reason)} | {EscapeMarkdown(ReferenceEvidence(anomaly))} |");
        }

        return builder.ToString();
    }

    private static string CreateHtml(AuditReport report)
    {
        var rows = string.Join(
            Environment.NewLine,
            report.Anomalies.Take(100).Select(anomaly =>
                $"<tr><td><span class=\"swatch\" style=\"background:{anomaly.Sample.Rgb.Hex}\"></span></td>" +
                $"<td>{anomaly.Id}</td><td>{anomaly.Severity}</td><td>{anomaly.Kind}</td>" +
                $"<td>{anomaly.Sample.Rgb.Hex}</td><td>{Html(anomaly.Sample.DetailedName)}</td>" +
                $"<td>{Html(anomaly.Reason)}</td><td>{Html(ReferenceEvidence(anomaly))}</td></tr>"));
        return $$"""
                 <!doctype html>
                 <meta charset="utf-8">
                 <title>Fovium color taxonomy audit</title>
                 <style>
                 body{font:14px system-ui;margin:24px;background:#161616;color:#eee}table{border-collapse:collapse;width:100%}
                 th,td{border-bottom:1px solid #444;padding:6px;text-align:left}.swatch{display:block;width:42px;height:24px;border:1px solid #777}
                 </style>
                 <h1>Fovium color taxonomy audit</h1>
                 <p>Mode <code>{{report.Mode}}</code>, seed <code>{{report.Seed}}</code>, {{report.Metrics.TotalUniqueSamples}} unique samples.</p>
                 <p>External references rank evidence only; they are not product ground truth.</p>
                 <table><thead><tr><th>Swatch</th><th>ID</th><th>Severity</th><th>Kind</th><th>HEX</th><th>Fovium</th><th>Reason</th><th>Reference evidence</th></tr></thead>
                 <tbody>{{rows}}</tbody></table>
                 """;
    }

    private static string CreateCsv(IEnumerable<AuditAnomaly> anomalies)
    {
        var builder =
            new StringBuilder(
                "id,severity,score,kind,hex,rgb,oklch,role,family,name,neighborHex,deltaE,reason,references\n");
        foreach (var anomaly in anomalies)
        {
            var references = string.Join(';', anomaly.References.Select(reference =>
                $"{reference.Dataset}:{reference.Name}:{reference.SemanticFamily}:{reference.DeltaE:0.0000}"));
            var values = new[]
            {
                anomaly.Id,
                anomaly.Severity,
                anomaly.Score.ToString("0.###", CultureInfo.InvariantCulture),
                anomaly.Kind,
                anomaly.Sample.Rgb.Hex,
                $"{anomaly.Sample.Rgb.Red} {anomaly.Sample.Rgb.Green} {anomaly.Sample.Rgb.Blue}",
                $"{anomaly.Sample.OklchL:0.0000} {anomaly.Sample.OklchC:0.0000} {anomaly.Sample.OklchHue:0.0}",
                anomaly.Sample.Role,
                anomaly.Sample.Family,
                anomaly.Sample.DetailedName,
                anomaly.Neighbor?.Rgb.Hex ?? string.Empty,
                anomaly.DeltaE?.ToString("0.000000", CultureInfo.InvariantCulture) ?? string.Empty,
                anomaly.Reason,
                references,
            };
            builder.AppendLine(string.Join(',', values.Select(Csv)));
        }

        return builder.ToString();
    }

    private static string CreateSvg(IReadOnlyList<AuditAnomaly> anomalies)
    {
        const int columns = 5;
        const int cellWidth = 230;
        const int cellHeight = 94;
        var rows = Math.Max(1, (anomalies.Count + columns - 1) / columns);
        var builder = new StringBuilder();
        builder.AppendLine(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{columns * cellWidth}\" height=\"{rows * cellHeight}\" viewBox=\"0 0 {columns * cellWidth} {rows * cellHeight}\">");
        builder.AppendLine(
            "<rect width=\"100%\" height=\"100%\" fill=\"#171717\"/><style>text{font-family:system-ui;fill:#eee;font-size:12px}.small{fill:#bbb;font-size:10px}</style>");
        for (var index = 0; index < anomalies.Count; index++)
        {
            var anomaly = anomalies[index];
            var x = index % columns * cellWidth;
            var y = index / columns * cellHeight;
            builder.AppendLine(
                $"<rect x=\"{x + 8}\" y=\"{y + 8}\" width=\"54\" height=\"54\" rx=\"4\" fill=\"{anomaly.Sample.Rgb.Hex}\" stroke=\"#777\"/>");
            builder.AppendLine(
                $"<text x=\"{x + 70}\" y=\"{y + 22}\">{Xml(anomaly.Id)} · {Xml(anomaly.Sample.Rgb.Hex)}</text>");
            builder.AppendLine($"<text x=\"{x + 70}\" y=\"{y + 40}\">{Xml(anomaly.Sample.Family)}</text>");
            builder.AppendLine(
                $"<text class=\"small\" x=\"{x + 70}\" y=\"{y + 57}\">L {anomaly.Sample.OklchL:P0} C {anomaly.Sample.OklchC:0.000} h {anomaly.Sample.OklchHue:0}°</text>");
            builder.AppendLine(
                $"<text class=\"small\" x=\"{x + 8}\" y=\"{y + 82}\">{Xml(Trim(anomaly.Reason, 34))}</text>");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static void AppendMetric(StringBuilder builder, string name, int value) =>
        builder.AppendLine($"| {name} | {value} |");

    private static void AppendCoverage(
        StringBuilder builder,
        string label,
        IReadOnlyDictionary<string, int> coverage)
    {
        builder.AppendLine($"**{label}:** " + string.Join(
            ", ",
            coverage.Select(pair => $"{pair.Key}={pair.Value}")));
        builder.AppendLine();
    }

    private static void WriteSampleSheet(
        string directory,
        string fileName,
        string title,
        IEnumerable<SheetItem> source)
    {
        var items = source.ToArray();
        File.WriteAllText(
            Path.Combine(directory, fileName + ".html"),
            CreateSampleHtml(title, items),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(directory, fileName + ".svg"),
            CreateSampleSvg(title, items.Take(180).ToArray()),
            new UTF8Encoding(false));
    }

    private static string CreateSampleHtml(string title, IReadOnlyList<SheetItem> items)
    {
        var rows = string.Join(
            Environment.NewLine,
            items.Select(item =>
                $"<tr><td><span class=\"swatch\" style=\"background:{item.Sample.Rgb.Hex}\"></span></td>" +
                $"<td>{Html(item.Label)}</td><td>{item.Sample.Rgb.Hex}</td>" +
                $"<td>L {item.Sample.OklchL:P1} · C {item.Sample.OklchC:0.000} · h {item.Sample.OklchHue:0.0}°</td>" +
                $"<td>{Html(item.Sample.Role)}</td><td>{Html(item.Sample.Family)}</td><td>{Html(item.Sample.Specificity)}</td>" +
                $"<td>{Html(item.Sample.DetailedName)}</td><td>{Html(ReferenceSummary(item.Reference))}</td>" +
                $"<td>{Html(item.Note)}</td></tr>"));
        return $$"""
                 <!doctype html><meta charset="utf-8"><title>{{Html(title)}}</title>
                 <style>body{font:13px system-ui;margin:24px;background:#161616;color:#eee}table{border-collapse:collapse;width:100%}
                 th,td{border-bottom:1px solid #444;padding:6px;text-align:left;vertical-align:top}.swatch{display:block;width:54px;height:32px;border:1px solid #888}</style>
                 <h1>{{Html(title)}}</h1><p>External references are independent evidence, not product ground truth.</p>
                 <table><thead><tr><th>Swatch</th><th>Cohort</th><th>HEX</th><th>OKLCH</th><th>Role</th><th>Family</th><th>Specificity</th><th>Name</th><th>Reference</th><th>Note</th></tr></thead>
                 <tbody>{{rows}}</tbody></table>
                 """;
    }

    private static string CreateSampleSvg(string title, IReadOnlyList<SheetItem> items)
    {
        const int columns = 4;
        const int cellWidth = 300;
        const int cellHeight = 126;
        const int headerHeight = 42;
        var rows = Math.Max(1, (items.Count + columns - 1) / columns);
        var builder = new StringBuilder();
        builder.AppendLine(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{columns * cellWidth}\" height=\"{headerHeight + rows * cellHeight}\" viewBox=\"0 0 {columns * cellWidth} {headerHeight + rows * cellHeight}\">");
        builder.AppendLine(
            "<rect width=\"100%\" height=\"100%\" fill=\"#171717\"/><style>text{font-family:system-ui;fill:#eee;font-size:12px}.small{fill:#bbb;font-size:10px}</style>");
        builder.AppendLine($"<text x=\"12\" y=\"26\" font-size=\"18\">{Xml(title)}</text>");
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var x = index % columns * cellWidth;
            var y = headerHeight + index / columns * cellHeight;
            builder.AppendLine(
                $"<rect x=\"{x + 8}\" y=\"{y + 8}\" width=\"64\" height=\"64\" rx=\"4\" fill=\"{item.Sample.Rgb.Hex}\" stroke=\"#888\"/>");
            builder.AppendLine(
                $"<text x=\"{x + 80}\" y=\"{y + 18}\">{Xml(Trim(item.Label, 31))}</text>");
            builder.AppendLine(
                $"<text class=\"small\" x=\"{x + 80}\" y=\"{y + 36}\">{Xml(item.Sample.Rgb.Hex)} · {Xml(item.Sample.Family)}</text>");
            builder.AppendLine($"<text x=\"{x + 80}\" y=\"{y + 54}\">{Xml(Trim(item.Sample.DetailedName, 31))}</text>");
            builder.AppendLine(
                $"<text class=\"small\" x=\"{x + 80}\" y=\"{y + 70}\">L {item.Sample.OklchL:P1} C {item.Sample.OklchC:0.000} h {item.Sample.OklchHue:0}°</text>");
            builder.AppendLine(
                $"<text class=\"small\" x=\"{x + 8}\" y=\"{y + 91}\">{Xml(Trim(ReferenceSummary(item.Reference), 47))}</text>");
            builder.AppendLine(
                $"<text class=\"small\" x=\"{x + 8}\" y=\"{y + 108}\">{Xml(Trim(item.Note, 47))}</text>");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    private static string CreateFamilyProfilesHtml(IReadOnlyList<FamilySemanticProfile> profiles)
    {
        var rows = string.Join(
            Environment.NewLine,
            profiles.Select(profile =>
                $"<tr><td>{Html(profile.Family)}</td><td>{profile.SampleCount}</td><td>{profile.IncompatibleReferenceRate:P1}</td>" +
                $"<td>{Html(profile.NearestCompetingFamily)}</td><td>{profile.Center.Rgb.Hex}</td><td>{profile.Edge.Rgb.Hex}</td>" +
                $"<td>{profile.Dark.Rgb.Hex} / {profile.Light.Rgb.Hex}</td><td>{profile.LowChroma.Rgb.Hex} / {profile.HighChroma.Rgb.Hex}</td></tr>"));
        return $$"""
                 <!doctype html><meta charset="utf-8"><title>Family semantic profiles</title>
                 <style>body{font:13px system-ui;margin:24px;background:#161616;color:#eee}table{border-collapse:collapse;width:100%}th,td{border-bottom:1px solid #444;padding:6px;text-align:left}</style>
                 <h1>Family semantic profiles</h1><table><thead><tr><th>Family</th><th>Samples</th><th>Incompatible rate</th><th>Competitor</th><th>Center</th><th>Edge</th><th>Dark/light</th><th>Low/high C</th></tr></thead><tbody>{{rows}}</tbody></table>
                 """;
    }

    private static string ReferenceSummary(AuditReferenceAssessment? reference) => reference is null
        ? "No reference cache"
        : $"{reference.ProductSemantic} vs {reference.ConsensusSemantic} ({reference.ConsensusSupport}); " +
          (reference.IsCompatible ? "compatible" : "incompatible");

    private static string ReferenceVotes(AuditReferenceAssessment? reference) => reference is null
        ? string.Empty
        : string.Join(", ", reference.DatasetVotes.Select(pair => $"{pair.Key}:{pair.Value}"));

    private sealed record SheetItem(
        string Label,
        AuditClassification Sample,
        AuditReferenceAssessment? Reference,
        string Note);

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string EscapeMarkdown(string value) => value.Replace("|", "\\|");

    private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value);

    private static string ReferenceEvidence(AuditAnomaly anomaly) => string.Join(
        "; ",
        anomaly.References.GroupBy(reference => reference.Dataset, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: " + string.Join(
                ", ",
                group.Take(3).Select(reference =>
                    $"{reference.Name} [{reference.SemanticFamily}] ΔE {reference.DeltaE.ToString("0.0000", CultureInfo.InvariantCulture)}"))));

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value);

    private static string Trim(string value, int limit) => value.Length <= limit ? value : value[..(limit - 1)] + "…";
}