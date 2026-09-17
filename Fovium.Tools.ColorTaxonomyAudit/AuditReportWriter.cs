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

        var deterministic = report with
        {
            Metrics = report.Metrics with { RuntimeSeconds = 0 },
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
        AppendMetric(builder, "High-severity anomalies", report.Metrics.HighSeverityAnomalies);
        AppendMetric(builder, "Medium-severity anomalies", report.Metrics.MediumSeverityAnomalies);

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
                $"abrupt Δ `{comparison.AbruptDiscontinuityDelta:+#;-#;0}`, reference disagreement Δ `{comparison.ReferenceDisagreementDelta:+#;-#;0}`.");
        }

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