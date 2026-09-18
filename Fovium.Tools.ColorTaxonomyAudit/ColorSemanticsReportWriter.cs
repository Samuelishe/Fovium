using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record ColorSemanticsWrittenArtifacts(
    string OutputDirectory,
    IReadOnlyList<string> Paths,
    long TotalBytes);

internal static class ColorSemanticsReportWriter
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions CompactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ColorSemanticsWrittenArtifacts Write(
        ColorSemanticsReport report,
        ColorSemanticsReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);
        var root = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(root);
        var paths = new List<string>();

        if (options.Json)
        {
            WriteArtifact("taxonomy.json", JsonSerializer.Serialize(report, IndentedJson));
            WriteArtifact("analysis-summary.json", BuildCompactAnalysisJson(report));
        }

        if (options.Text)
        {
            WriteArtifact("summary.txt", BuildTextSummary(report));
            WriteArtifact("summary.md", BuildMarkdownSummary(report));
            WriteArtifact("analysis-summary.md", BuildCompactAnalysisMarkdown(report));
        }

        if (options.Static)
        {
            WriteArtifact(Path.Combine("static", "overview.svg"), BuildOverviewSvg(report));
            WriteArtifact(Path.Combine("static", "relations.svg"), BuildRelationsSvg(report));
            foreach (var domain in report.ProfessionalTerms.Select(term => term.Domain)
                         .Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                WriteArtifact(
                    Path.Combine("static", "domains", FileId(domain) + ".svg"),
                    BuildDomainSvg(report, domain));
            }
        }

        if (options.Html)
        {
            WriteArtifact("index.html", BuildHtml(report));
        }

        var bytes = paths.Sum(path => new FileInfo(path).Length);
        return new ColorSemanticsWrittenArtifacts(root, paths, bytes);

        void WriteArtifact(string relativePath, string content)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
            paths.Add(path);
        }
    }

    internal static string BuildTextSummary(ColorSemanticsReport report)
    {
        var disposition = report.Research.Candidates
            .GroupBy(candidate => candidate.Disposition)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"  {group.Key}: {group.Count()}");
        var domains = report.ProfessionalTerms.GroupBy(term => term.Domain)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"  {group.Key}: {group.Count()} terms / {group.Sum(term => term.RegionCount)} regions");
        var lines = new List<string>
        {
            "Fovium Color Semantics",
            $"Version: {report.Metadata.ProductVersion}",
            $"Commit: {report.Metadata.Commit}",
            $"Schema: {report.Schema}",
            $"Production signature: {report.ProductionSignature}",
            $"Classification outcomes: {report.Signatures.ClassificationOutcomes}",
            $"Report signature: {report.ReportSignature}",
            string.Empty,
            $"Broad families: {report.Summary.BroadFamilyCount}",
            $"Professional terms: {report.Summary.ProfessionalTermCount}",
            $"Regions: {report.Summary.RegionCount}",
            $"Multi-region terms: {report.Summary.MultiRegionTermCount}",
            $"Creative anchors: {report.Summary.CreativeAnchorCount}",
            $"Reference-sRGB samples: {report.Summary.GamutSampleCount}",
            $"Research candidates: {report.Summary.ResearchCandidateCount}",
            $"Warnings: {report.Warnings.Count}",
            string.Empty,
            "Domains:"
        };
        lines.AddRange(domains);
        lines.Add(string.Empty);
        lines.Add("Explicit sample cohorts:");
        lines.AddRange(report.Sampling.Cohorts.Select(cohort =>
            $"  {cohort.Id}: {cohort.ProfessionalHitCount}/{cohort.SampleCount} Professional hits " +
            $"({cohort.ProfessionalHitRate:P1}); denominator={cohort.DenominatorMeaning}"));
        lines.Add(string.Empty);
        lines.Add($"Research: {(report.Research.Available ? "available" : "not supplied")}");
        lines.AddRange(disposition);
        lines.Add(string.Empty);
        return string.Join('\n', lines);
    }

    internal static string BuildMarkdownSummary(ColorSemanticsReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Fovium Color Semantics report");
        builder.AppendLine();
        builder.AppendLine($"- Product version: `{report.Metadata.ProductVersion}`");
        builder.AppendLine($"- Commit: `{report.Metadata.Commit}`");
        builder.AppendLine($"- Schema: `{report.Schema}`");
        builder.AppendLine($"- Production signature: `{report.ProductionSignature}`");
        builder.AppendLine($"- Classification-outcome signature: `{report.Signatures.ClassificationOutcomes}`");
        builder.AppendLine($"- Report signature: `{report.ReportSignature}`");
        builder.AppendLine(
            $"- Evidence: {report.Metadata.EvidenceMode}; source domain: {report.Metadata.SourceColorDomain}");
        builder.AppendLine();
        builder.AppendLine("| Metric | Count |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| Broad families | {report.Summary.BroadFamilyCount} |");
        builder.AppendLine($"| Professional terms | {report.Summary.ProfessionalTermCount} |");
        builder.AppendLine($"| Regions / lobes | {report.Summary.RegionCount} |");
        builder.AppendLine($"| Multi-region terms | {report.Summary.MultiRegionTermCount} |");
        builder.AppendLine($"| Creative anchors | {report.Summary.CreativeAnchorCount} |");
        builder.AppendLine($"| Gamut samples | {report.Summary.GamutSampleCount} |");
        builder.AppendLine($"| Research candidates | {report.Summary.ResearchCandidateCount} |");
        builder.AppendLine();
        builder.AppendLine("## Semantic domains");
        builder.AppendLine();
        builder.AppendLine("| Domain | Terms | Regions |");
        builder.AppendLine("| --- | ---: | ---: |");
        foreach (var group in report.ProfessionalTerms.GroupBy(term => term.Domain).OrderBy(group => group.Key))
        {
            builder.AppendLine(
                $"| {EscapeMarkdown(group.Key)} | {group.Count()} | {group.Sum(term => term.RegionCount)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        foreach (var warning in report.Warnings)
        {
            builder.AppendLine(
                $"- **{EscapeMarkdown(warning.Severity)} / {EscapeMarkdown(warning.Kind)}:** {EscapeMarkdown(warning.Message)}");
        }

        return builder.ToString();
    }

    internal static string BuildCompactAnalysisJson(ColorSemanticsReport report)
    {
        var compact = new
        {
            Schema = "fovium-color-semantics-analysis-summary/v1",
            report.Metadata,
            report.Summary,
            report.Signatures,
            Sampling = report.Sampling.Cohorts,
            MultiLobeTerms = report.ProfessionalTerms.Where(term => term.RegionCount > 1)
                .Select(term => new { term.Id, term.EnglishName, term.RussianName, term.RegionIds })
                .ToArray(),
            Warnings = report.Warnings,
            WeakestLocalStability = report.ProfessionalTerms.Where(term => term.LocalStability is not null)
                .OrderBy(term => term.LocalStability!.RetentionFraction)
                .ThenBy(term => term.Id, StringComparer.Ordinal)
                .Take(20)
                .Select(term => new { term.Id, term.LocalStability })
                .ToArray(),
            Overlaps = report.DeepEvidence.Overlaps,
            ShadowedRegions = report.DeepEvidence.RegionReachability.Where(region => region.Shadowed).ToArray(),
            DomainVocabulary = report.Research.DomainCoverage,
            ResearchFrontier = report.Research.Candidates
                .OrderByDescending(candidate => candidate.IndependentNumericSourceGroupCount)
                .ThenByDescending(candidate => candidate.PriorityScore)
                .ThenBy(candidate => candidate.CanonicalTerm, StringComparer.Ordinal)
                .Select(candidate => new
                {
                    candidate.CanonicalTerm,
                    candidate.Domain,
                    candidate.Disposition,
                    candidate.Reason,
                    candidate.LexicalSourceCount,
                    candidate.NumericSourceCount,
                    candidate.IndependentNumericSourceGroupCount,
                    candidate.AnchorCount,
                    candidate.CompactComponentCount,
                    candidate.NoiseFraction,
                    candidate.NearestShippedTerm,
                    candidate.NearestShippedDeltaE
                }).ToArray()
        };
        return JsonSerializer.Serialize(compact, IndentedJson);
    }

    internal static string BuildCompactAnalysisMarkdown(ColorSemanticsReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Fovium Color Semantics compact analysis");
        builder.AppendLine();
        builder.AppendLine($"- Definition: `{report.Signatures.ProductionDefinition}`");
        builder.AppendLine($"- Classification outcomes: `{report.Signatures.ClassificationOutcomes}`");
        builder.AppendLine($"- Canonical report: `{report.Signatures.CanonicalReport}`");
        builder.AppendLine($"- Taxonomy: {report.Summary.BroadFamilyCount} broad / " +
                           $"{report.Summary.ProfessionalTermCount} Professional / {report.Summary.RegionCount} lobes");
        builder.AppendLine($"- Deep evidence: {(report.DeepEvidence.Available ? "available" : "not supplied")}");
        builder.AppendLine();
        builder.AppendLine("## Sampling cohorts");
        builder.AppendLine();
        foreach (var cohort in report.Sampling.Cohorts)
        {
            builder.AppendLine($"- `{cohort.Id}`: {cohort.ProfessionalHitCount}/{cohort.SampleCount} " +
                               $"({cohort.ProfessionalHitRate:P1}); {cohort.DenominatorMeaning}.");
        }

        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        foreach (var warning in report.Warnings)
        {
            builder.AppendLine($"- `{warning.Id}` {warning.Severity}/{warning.Kind}: {warning.Message}");
        }

        return builder.ToString();
    }

    internal static string BuildOverviewSvg(ColorSemanticsReport report)
    {
        const int width = 1800;
        const int height = 1240;
        var slices = new[] { 0.15, 0.30, 0.45, 0.60, 0.75, 0.90 };
        var adapter = new ProductionColorAdapter();
        var builder = SvgStart(width, height, "Fovium Color Semantics — reference-sRGB OKLCH atlas");
        builder.AppendLine(
            "<defs><pattern id=\"unreachable\" width=\"10\" height=\"10\" patternUnits=\"userSpaceOnUse\"><path d=\"M0 10L10 0\" stroke=\"#263142\" stroke-width=\"1\" opacity=\".5\"/></pattern></defs>");
        builder.AppendLine(
            "<text class=\"title\" x=\"70\" y=\"70\">Reference-sRGB semantic coverage in OKLCH slices</text>");
        builder.AppendLine(
            "<text class=\"muted\" x=\"70\" y=\"104\">Hue is angular, chroma radial, unreachable mathematical OKLCH is hatched. Gold rings mark a winning Professional region.</text>");
        for (var index = 0; index < slices.Length; index++)
        {
            var column = index % 3;
            var row = index / 3;
            var centerX = 310 + column * 590;
            var centerY = 380 + row * 520;
            const double radius = 205;
            builder.AppendLine(
                $"<g id=\"slice-l-{slices[index].ToString("0.00", Invariant)}\" data-layer=\"oklch-slice\">");
            builder.AppendLine(
                $"<circle cx=\"{centerX}\" cy=\"{centerY}\" r=\"{radius}\" fill=\"url(#unreachable)\" stroke=\"#42506a\" stroke-width=\"2\"/>");
            for (var chroma = 0.0; chroma <= 0.3301; chroma += 0.015)
            {
                for (var hue = 0; hue < 360; hue += 10)
                {
                    if (!AuditSampling.TryOklchToSrgb(slices[index], chroma, hue, out var rgb))
                    {
                        continue;
                    }

                    var radians = hue * Math.PI / 180;
                    var x = centerX + Math.Cos(radians) * (chroma / 0.34) * radius;
                    var y = centerY + Math.Sin(radians) * (chroma / 0.34) * radius;
                    var classification = adapter.Classify(rgb);
                    var professional = classification.ProfessionalTerm is not null;
                    builder.Append("<circle data-family=\"")
                        .Append(EscapeXml(classification.Family))
                        .Append("\" cx=\"").Append(F(x))
                        .Append("\" cy=\"").Append(F(y))
                        .Append("\" r=\"5.6\" fill=\"").Append(rgb.Hex)
                        .Append("\" stroke=\"").Append(professional ? "#f7cf6b" : "none")
                        .Append("\" stroke-width=\"").Append(professional ? "1.2" : "0")
                        .AppendLine("\"/>");
                }
            }

            builder.AppendLine(
                $"<text class=\"slice\" x=\"{centerX - 198}\" y=\"{centerY - 224}\">L = {slices[index]:0.00}</text>");
            builder.AppendLine($"<text class=\"axis\" x=\"{centerX + 210}\" y=\"{centerY + 4}\">0° red</text>");
            builder.AppendLine($"<text class=\"axis\" x=\"{centerX - 23}\" y=\"{centerY - 214}\">270°</text>");
            builder.AppendLine("</g>");
        }

        builder.AppendLine(
            $"<text class=\"footer\" x=\"70\" y=\"1190\">{report.Summary.BroadFamilyCount} broad families · {report.Summary.ProfessionalTermCount} Professional terms · {report.Summary.RegionCount} lobes · production {EscapeXml(report.ProductionSignature[..12])}</text>");
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    internal static string BuildDomainSvg(ColorSemanticsReport report, string domain)
    {
        var terms = report.ProfessionalTerms.Where(term => term.Domain == domain)
            .OrderBy(term => term.Id, StringComparer.Ordinal)
            .ToArray();
        const int width = 1700;
        var height = 610 + Math.Max(1, terms.Length) * 58;
        var builder = SvgStart(width, height, $"{domain} — Professional taxonomy");
        builder.AppendLine(
            $"<text class=\"title\" x=\"70\" y=\"70\">{EscapeXml(domain)} · Professional taxonomy</text>");
        builder.AppendLine(
            $"<text class=\"muted\" x=\"70\" y=\"104\">{terms.Length} terms; numbered markers are reachable winning cores in the a/b projection. Lightness is printed in the table.</text>");
        const double centerX = 320;
        const double centerY = 335;
        const double scale = 680;
        builder.AppendLine(
            $"<circle cx=\"{centerX}\" cy=\"{centerY}\" r=\"230\" fill=\"#101722\" stroke=\"#42506a\" stroke-width=\"2\"/>");
        foreach (var ring in new[] { 0.10, 0.20, 0.30 })
        {
            builder.AppendLine(
                $"<circle cx=\"{centerX}\" cy=\"{centerY}\" r=\"{F(ring * scale)}\" fill=\"none\" stroke=\"#2b374a\"/>");
        }

        for (var index = 0; index < terms.Length; index++)
        {
            var term = terms[index];
            var core = term.RepresentativeCore;
            if (core is not null)
            {
                var x = centerX + core.X * scale;
                var y = centerY + core.Y * scale;
                builder.AppendLine(
                    $"<g id=\"marker-{EscapeXml(term.Id)}\" data-term=\"{EscapeXml(term.Id)}\"><circle cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"14\" fill=\"{core.Hex}\" stroke=\"#ffffff\" stroke-width=\"2\"/><text class=\"marker\" x=\"{F(x)}\" y=\"{F(y + 5)}\" text-anchor=\"middle\">{index + 1}</text></g>");
            }

            var rowY = 585 + index * 58;
            var regionText = string.Join(" · ", report.Regions.Where(region => term.RegionIds.Contains(region.Id))
                .OrderBy(region => region.LobeIndex)
                .Select(region =>
                    $"L{region.Lightness.MinimumInclusive:0.00}–{region.Lightness.MaximumExclusive:0.00} C{region.Chroma.MinimumInclusive:0.000}–{region.Chroma.MaximumExclusive:0.000} h{region.Hue.MinimumInclusive:0}–{region.Hue.MaximumExclusive:0}°"));
            builder.AppendLine(
                $"<rect x=\"620\" y=\"{rowY - 31}\" width=\"1010\" height=\"48\" rx=\"9\" fill=\"{(index % 2 == 0 ? "#151f2e" : "#111a27")}\"/>");
            builder.AppendLine($"<text class=\"row-number\" x=\"642\" y=\"{rowY}\">{index + 1}</text>");
            builder.AppendLine(
                $"<text class=\"term\" x=\"685\" y=\"{rowY - 7}\">{EscapeXml(term.EnglishName)} · {EscapeXml(term.RussianName)}</text>");
            builder.AppendLine(
                $"<text class=\"row-detail\" x=\"685\" y=\"{rowY + 12}\">{EscapeXml(term.Id)} · {EscapeXml(regionText)}</text>");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    internal static string BuildRelationsSvg(ColorSemanticsReport report)
    {
        var rows = report.BroadFamilies.Select(family => new
        {
            Family = family,
            Terms = report.ProfessionalTerms.Where(term => term.ParentFamilyIds.Contains(family.Id))
                    .OrderBy(term => term.Id, StringComparer.Ordinal)
                    .ToArray()
        })
            .Where(row => row.Terms.Length > 0)
            .ToArray();
        var rowHeights = rows.Select(row => 48 + (int)Math.Ceiling(row.Terms.Length / 4d) * 42).ToArray();
        var height = 180 + rowHeights.Sum() + 180;
        const int width = 1900;
        var builder = SvgStart(width, height, "Broad fallback, Professional terms, and lobes");
        builder.AppendLine("<text class=\"title\" x=\"70\" y=\"70\">Explainability relations</text>");
        builder.AppendLine(
            "<text class=\"muted\" x=\"70\" y=\"104\">Rows show broad fallback overlap → Professional term. The badge is the exact lobe count; overlap is not asserted as strict inheritance.</text>");
        var y = 150;
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowHeight = rowHeights[rowIndex];
            builder.AppendLine(
                $"<g id=\"relation-{EscapeXml(row.Family.Id)}\" data-relation=\"broad-fallback\"><rect x=\"50\" y=\"{y}\" width=\"1800\" height=\"{rowHeight - 8}\" rx=\"12\" fill=\"{(rowIndex % 2 == 0 ? "#111a27" : "#151f2e")}\"/>");
            builder.AppendLine(
                $"<text class=\"family\" x=\"76\" y=\"{y + 38}\">{EscapeXml(row.Family.EnglishName)}</text><text class=\"family-ru\" x=\"76\" y=\"{y + 62}\">{EscapeXml(row.Family.RussianName)}</text>");
            for (var index = 0; index < row.Terms.Length; index++)
            {
                var column = index % 4;
                var line = index / 4;
                var x = 430 + column * 345;
                var chipY = y + 18 + line * 42;
                var term = row.Terms[index];
                builder.AppendLine(
                    $"<g data-term=\"{EscapeXml(term.Id)}\"><path d=\"M345 {y + 41}H{x - 12}\" stroke=\"#34435a\"/><rect x=\"{x}\" y=\"{chipY}\" width=\"320\" height=\"32\" rx=\"8\" fill=\"#202d40\" stroke=\"#445773\"/><text class=\"chip\" x=\"{x + 12}\" y=\"{chipY + 21}\">{EscapeXml(term.EnglishName)} <tspan class=\"badge\">[{term.RegionCount}]</tspan></text></g>");
            }

            builder.AppendLine("</g>");
            y += rowHeight;
        }

        builder.AppendLine(
            $"<text class=\"footer\" x=\"70\" y=\"{height - 70}\">[n] = region/lobe children · {report.Relations.Count(relation => relation.Kind == "broad-fallback")} fallback overlaps · {report.Relations.Count(relation => relation.Kind == "lobe")} term→lobe relations</text>");
        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    internal static string BuildHtml(ColorSemanticsReport report)
    {
        var json = JsonSerializer.Serialize(report, CompactJson)
            .Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);
        var domainLinks = string.Join(string.Empty, report.ProfessionalTerms.Select(term => term.Domain)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(domain => $"<a href=\"static/domains/{FileId(domain)}.svg\">{EscapeXml(domain)}</a>"));
        return $$$$"""
                   <!doctype html>
                   <html lang="en">
                   <head>
                   <meta charset="utf-8">
                   <meta name="viewport" content="width=device-width,initial-scale=1">
                   <title>Fovium Color Semantics Explorer</title>
                   <style>
                   :root{color-scheme:dark;--bg:#0a1019;--panel:#111a27;--panel2:#172233;--line:#2f4058;--text:#edf3fb;--muted:#9aabc1;--accent:#7ed8ff;--gold:#f0c86b}*{box-sizing:border-box}body{margin:0;background:radial-gradient(circle at 20% 0,#172538 0,#0a1019 42%);color:var(--text);font:14px/1.45 Inter,Segoe UI,Arial,sans-serif}header{padding:28px 36px 18px;border-bottom:1px solid var(--line)}h1{margin:0;font-size:28px;letter-spacing:.2px}header p{margin:6px 0 0;color:var(--muted)}nav{display:flex;gap:7px;padding:12px 28px;position:sticky;top:0;z-index:10;background:#0a1019e8;backdrop-filter:blur(12px);border-bottom:1px solid var(--line)}button,.button,select,input{background:var(--panel2);color:var(--text);border:1px solid var(--line);border-radius:8px;padding:8px 10px}nav button.active{border-color:var(--accent);color:var(--accent)}main{padding:24px 30px}.tab{display:none}.tab.active{display:block}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(165px,1fr));gap:12px}.card,.panel{background:linear-gradient(145deg,#162232,#0f1723);border:1px solid var(--line);border-radius:14px;padding:18px}.value{font-size:28px;font-weight:700}.label{color:var(--muted)}.sign{font:12px Consolas,monospace;word-break:break-all;color:#b8c9dd}.explorer{display:grid;grid-template-columns:minmax(620px,1fr) 330px;gap:16px}.canvas-wrap{position:relative;min-height:680px;background:#070c13;border:1px solid var(--line);border-radius:14px;overflow:hidden}canvas{width:100%;height:680px;display:block}.hint{position:absolute;left:14px;bottom:12px;color:var(--muted);pointer-events:none}.controls{display:grid;gap:10px;align-content:start}.control{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:10px}.control label{display:flex;align-items:center;gap:8px;margin:5px 0}.control input[type=checkbox]{accent-color:#7ed8ff}.control input[type=range]{width:100%}.detail{min-height:180px;white-space:pre-wrap}.artifact{width:100%;height:78vh;border:1px solid var(--line);background:#0b111a;border-radius:12px}.links{display:flex;flex-wrap:wrap;gap:8px;margin:12px 0}.links a{color:var(--accent);background:var(--panel);padding:7px 10px;border-radius:8px;text-decoration:none}.table{width:100%;border-collapse:collapse}.table th,.table td{text-align:left;border-bottom:1px solid var(--line);padding:8px}.pill{display:inline-block;border:1px solid var(--line);border-radius:999px;padding:2px 8px;margin:2px}.warning{border-left:3px solid var(--gold);padding:9px 12px;background:var(--panel);margin:8px 0}@media(max-width:1000px){.explorer{grid-template-columns:1fr}.canvas-wrap,canvas{min-height:560px;height:560px}}
                   </style>
                   </head>
                   <body>
                   <header><h1>Fovium Color Semantics Explorer</h1><p>Production taxonomy, creative names, and optional research evidence · offline · deterministic</p></header>
                   <nav><button data-tab="summary" class="active">Summary</button><button data-tab="explorer">3D Explorer</button><button data-tab="atlas">OKLCH atlas</button><button data-tab="relations">Relations</button><button data-tab="research">Warnings / frontier</button><button data-tab="metadata">Metadata</button></nav>
                   <main>
                   <section id="summary" class="tab active"><div class="cards" id="summary-cards"></div><div class="panel" style="margin-top:16px"><h2>Semantic domains</h2><table class="table" id="domain-table"></table></div></section>
                   <section id="explorer" class="tab"><div class="explorer"><div class="canvas-wrap"><canvas id="scene"></canvas><div class="hint">Drag to rotate · Shift-drag to pan · wheel to zoom · click a point for details</div></div><aside class="controls"><div class="control"><input id="search" placeholder="Brick Red, Ecru, stable ID…" style="width:100%"><select id="domain" style="width:100%;margin-top:8px"><option value="">All domains</option></select><select id="locale" style="width:100%;margin-top:8px"><option value="en">English</option><option value="ru">Русский</option></select><button id="reset-view" style="width:100%;margin-top:8px">Reset view / selection</button></div><div class="control"><strong>Layers</strong><label><input id="gamut" type="checkbox" checked> reference-sRGB gamut</label><label><input id="families" type="checkbox"> broad-family color</label><label><input id="regions" type="checkbox" checked> Professional coverage</label><label><input id="cores" type="checkbox" checked> representative cores</label><label><input id="anchors" type="checkbox"> creative anchors</label><label><input id="accepted" type="checkbox"> research: accepted</label><label><input id="deferred" type="checkbox"> research: deferred</label><label><input id="synonym" type="checkbox"> research: synonym</label><label><input id="rejected" type="checkbox"> research: rejected</label></div><div class="control"><label>Lightness minimum <output id="lmin-o">0.00</output></label><input id="lmin" type="range" min="0" max="1" value="0" step=".01"><label>Lightness maximum <output id="lmax-o">1.00</output></label><input id="lmax" type="range" min="0" max="1" value="1" step=".01"><label>Point density <output id="density-o">35%</output></label><input id="density" type="range" min="5" max="100" value="35" step="5"></div><div class="control detail" id="detail">Select or search a term, lobe, anchor, or research candidate.</div></aside></div></section>
                   <section id="atlas" class="tab"><div class="links">{{{{domainLinks}}}}</div><object class="artifact" data="static/overview.svg" type="image/svg+xml"></object></section>
                   <section id="relations" class="tab"><object class="artifact" data="static/relations.svg" type="image/svg+xml"></object></section>
                   <section id="research" class="tab"><div id="warnings"></div><div class="panel"><h2>Research frontier</h2><table class="table" id="research-table"></table></div></section>
                   <section id="metadata" class="tab"><div class="panel"><h2>Canonical metadata</h2><pre id="metadata-json" class="sign"></pre></div></section>
                   </main>
                   <script id="taxonomy-data" type="application/json">{{{{json}}}}</script>
                   <script>
                   'use strict';const report=JSON.parse(document.getElementById('taxonomy-data').textContent);const $=id=>document.getElementById(id);document.querySelectorAll('nav button').forEach(b=>b.onclick=()=>{document.querySelectorAll('nav button,.tab').forEach(x=>x.classList.remove('active'));b.classList.add('active');$(b.dataset.tab).classList.add('active');if(b.dataset.tab==='explorer')resize()});
                   const metrics=[['Broad families',report.summary.broadFamilyCount],['Professional terms',report.summary.professionalTermCount],['Regions / lobes',report.summary.regionCount],['Multi-lobe terms',report.summary.multiRegionTermCount],['Creative anchors',report.summary.creativeAnchorCount],['sRGB samples',report.summary.gamutSampleCount],['Research candidates',report.summary.researchCandidateCount],['Warnings',report.warnings.length]];$('summary-cards').innerHTML=metrics.map(x=>`<div class=card><div class=value>${x[1]}</div><div class=label>${x[0]}</div></div>`).join('')+`<div class=card style="grid-column:1/-1"><div class=label>Production signature</div><div class=sign>${report.productionSignature}</div></div>`;
                   const domains=[...new Set(report.professionalTerms.map(x=>x.domain))].sort();$('domain').innerHTML+=[...domains].map(x=>`<option>${x}</option>`).join('');$('domain-table').innerHTML='<tr><th>Domain</th><th>Terms</th><th>Regions</th></tr>'+domains.map(d=>{const t=report.professionalTerms.filter(x=>x.domain===d);return `<tr><td>${d}</td><td>${t.length}</td><td>${t.reduce((n,x)=>n+x.regionCount,0)}</td></tr>`}).join('');
                   $('warnings').innerHTML=report.warnings.map(x=>`<div class=warning><b>${x.severity} · ${x.kind}</b><br>${x.message}</div>`).join('');$('research-table').innerHTML='<tr><th>Candidate</th><th>Disposition</th><th>Domain</th><th>Lexical / numeric / independent numeric</th><th>Reason</th></tr>'+report.research.candidates.slice().sort((a,b)=>a.canonicalTerm.localeCompare(b.canonicalTerm)).map(x=>`<tr><td>${x.canonicalTerm}</td><td>${x.disposition}</td><td>${x.domain}</td><td>${x.lexicalSourceCount} / ${x.numericSourceCount} / ${x.independentNumericSourceGroupCount}</td><td>${x.reason}</td></tr>`).join('');$('metadata-json').textContent=JSON.stringify({schema:report.schema,metadata:report.metadata,summary:report.summary,signatures:report.signatures,sampling:report.sampling.cohorts,research:{available:report.research.available,status:report.research.status,sources:report.research.sources}},null,2);
                   const canvas=$('scene'),ctx=canvas.getContext('2d');let yaw=-.65,pitch=.45,zoom=1,panX=0,panY=0,drag=null,projected=[],selectedTermId=null;const termById=new Map(report.professionalTerms.map(x=>[x.id,x]));const familyById=new Map(report.broadFamilies.map(x=>[x.id,x]));const overlaps=report.deepEvidence?.overlaps||[];
                   function resize(){const r=canvas.getBoundingClientRect(),d=Math.min(devicePixelRatio||1,2);canvas.width=Math.max(1,Math.round(r.width*d));canvas.height=Math.max(1,Math.round(r.height*d));ctx.setTransform(d,0,0,d,0,0);draw()}
                   function projection(p){let x=p.x,y=p.y,z=(p.z-.5)*.55;const cy=Math.cos(yaw),sy=Math.sin(yaw),cp=Math.cos(pitch),sp=Math.sin(pitch);const x1=x*cy-y*sy,y1=x*sy+y*cy,y2=y1*cp-z*sp,z2=y1*sp+z*cp;const rect=canvas.getBoundingClientRect(),s=Math.min(rect.width,rect.height)*1.32*zoom,k=1/(1-z2*1.25);return{x:rect.width/2+panX+x1*s*k,y:rect.height/2+panY-y2*s*k,d:z2,k}}
                   function allowed(p){return p.lightness>=+$('lmin').value&&p.lightness<=+$('lmax').value}function dot(item,r,color,label,kind){const q=projection(item);ctx.beginPath();ctx.arc(q.x,q.y,r*q.k,0,Math.PI*2);ctx.fillStyle=color;ctx.fill();if(label){ctx.strokeStyle='#fff';ctx.lineWidth=1;ctx.stroke();projected.push({x:q.x,y:q.y,r:Math.max(7,r*q.k),label,kind,item})}}
                   function axis(a,b,label){const p=projection(a),q=projection(b);ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(q.x,q.y);ctx.strokeStyle='#536783';ctx.lineWidth=1;ctx.stroke();ctx.fillStyle='#91a5bf';ctx.font='12px Segoe UI';ctx.fillText(label,q.x+5,q.y-4)}
                   function draw(){if(!ctx)return;const rect=canvas.getBoundingClientRect();ctx.clearRect(0,0,rect.width,rect.height);ctx.fillStyle='#070c13';ctx.fillRect(0,0,rect.width,rect.height);projected=[];axis({x:0,y:0,z:0},{x:0,y:0,z:1},'L');axis({x:0,y:0,z:0},{x:.34,y:0,z:0},'C · h 0°');axis({x:0,y:0,z:0},{x:0,y:.34,z:0},'C · h 90°');const density=Math.max(1,Math.round(100/+$('density').value));if($('gamut').checked){ctx.globalAlpha=selectedTermId ? .12 : .30;report.gamut.samples.forEach((p,i)=>{if(i%density||!allowed(p))return;const q=projection(p);ctx.fillStyle=$('families').checked?p.hex:'#7890aa';ctx.fillRect(q.x,q.y,1.7*q.k,1.7*q.k)});ctx.globalAlpha=1}if($('regions').checked){ctx.globalAlpha=.52;report.gamut.samples.forEach((p,i)=>{if(i%density||!p.professionalRegionId||!allowed(p)||(selectedTermId&&p.professionalTermId!==selectedTermId))return;dot(p,2.4,p.hex,null,'region')});ctx.globalAlpha=1}const domain=$('domain').value;if($('cores').checked)report.regions.forEach(r=>{const t=termById.get(r.termId);if(!r.representativeCore||!allowed(r.representativeCore)||(domain&&t.domain!==domain))return;ctx.globalAlpha=selectedTermId&&r.termId!==selectedTermId ? .15 : 1;dot(r.representativeCore,t.regionCount>1?6:5,r.representativeCore.hex,{region:r,term:t},'lobe');ctx.globalAlpha=1});if($('anchors').checked)report.creativeAnchors.forEach((a,i)=>{if(i%2||!allowed(a.point))return;dot(a.point,3,a.point.hex,a,'creative anchor')});for(const disposition of ['accepted','deferred','synonym','rejected'])if($(disposition).checked)report.research.candidates.filter(x=>x.disposition.toLowerCase()===disposition&&x.representative&&allowed(x.representative)).forEach(x=>dot(x.representative,5,{accepted:'#71e39a',deferred:'#f0c86b',synonym:'#b89cff',rejected:'#ff7485'}[disposition],x,'research '+disposition));}
                   function describe(hit){const locale=$('locale').value,item=hit.label;if(hit.kind.includes('term'))return `${locale==='ru'?item.russianName:item.englishName}\n${item.id}\n${item.domain}\n${item.regionCount} region/lobe${item.regionCount===1?'':'s'}\n${item.regionIds.join('\n')}\nrepresentative ${item.representativeCore.hex}\nwitness ${item.reachabilityWitness?.hex||'—'}\nretention ${(100*(item.localStability?.retentionFraction||0)).toFixed(1)}% · transition ${item.localStability?.minimumRgbTransitionSteps||'—'} RGB steps`;if(hit.kind==='lobe'){const r=item.region,t=item.term,ov=overlaps.filter(x=>x.winnerTermId===t.id||x.competingTermId===t.id);return `${locale==='ru'?t.russianName:t.englishName}\n${r.id} · lobe ${r.lobeIndex}/${t.regionCount}\nparents ${r.parentFamilyIds.join(', ')}\nroles ${r.roles.join(', ')}\nL [${r.lightness.minimumInclusive.toFixed(3)}, ${r.lightness.maximumExclusive.toFixed(3)})\nC [${r.chroma.minimumInclusive.toFixed(3)}, ${r.chroma.maximumExclusive.toFixed(3)})\nh ${r.hue.minimumInclusive.toFixed(1)}–${r.hue.maximumExclusive.toFixed(1)}°${r.hue.wrapsZero?' (wraps 0°)':''}\npriority ${r.priority}\nrepresentative ${r.representativeCore?.hex||'—'} · witness ${r.reachabilityWitness?.hex||'—'}\nlocal retention ${(100*(r.localStability?.retentionFraction||0)).toFixed(1)}%\noverlap competitors ${ov.map(x=>(x.winnerTermId===t.id?x.competingTermId:x.winnerTermId)+' Dice '+x.diceSimilarity.toFixed(3)).join('; ')||'none'}`};if(hit.kind==='creative anchor')return `${locale==='ru'?item.russianName:item.englishName}\n${item.id}\n${item.point.hex}\n${familyById.get(item.point.broadFamilyId)?.[locale==='ru'?'russianName':'englishName']}`;return `${item.canonicalTerm}\n${hit.kind}\n${item.domain}\n${item.reason}\nlexical sources ${item.lexicalSourceCount} · numeric sources ${item.numericSourceCount} · independent numeric groups ${item.independentNumericSourceGroupCount}`}
                   function pick(x,y,show){let best=null,dist=1e9;for(const p of projected){const d=Math.hypot(x-p.x,p.y-y);if(d<p.r+6&&d<dist){best=p;dist=d}}if(best&&show){selectedTermId=best.label?.term?.id||best.label?.id||null;$('detail').textContent=describe(best);draw()}return best}
                   canvas.onpointerdown=e=>{canvas.setPointerCapture(e.pointerId);drag={x:e.clientX,y:e.clientY,shift:e.shiftKey}};canvas.onpointermove=e=>{if(!drag){canvas.style.cursor=pick(e.offsetX,e.offsetY,false)?'pointer':'grab';return}const dx=e.clientX-drag.x,dy=e.clientY-drag.y;if(drag.shift){panX+=dx;panY+=dy}else{yaw+=dx*.008;pitch=Math.max(-1.35,Math.min(1.35,pitch+dy*.008))}drag.x=e.clientX;drag.y=e.clientY;draw()};canvas.onpointerup=e=>{if(drag&&Math.hypot(e.clientX-drag.x,e.clientY-drag.y)<3)pick(e.offsetX,e.offsetY,true);drag=null};canvas.onwheel=e=>{e.preventDefault();zoom=Math.max(.45,Math.min(3.5,zoom*Math.exp(-e.deltaY*.001)));draw()};
                   document.querySelectorAll('.controls input,.controls select').forEach(x=>x.oninput=()=>{if(x.id==='lmin')$('lmin-o').textContent=(+x.value).toFixed(2);if(x.id==='lmax')$('lmax-o').textContent=(+x.value).toFixed(2);if(x.id==='density')$('density-o').textContent=x.value+'%';draw()});$('reset-view').onclick=()=>{yaw=-.65;pitch=.45;zoom=1;panX=panY=0;selectedTermId=null;$('detail').textContent='Select or search a term, lobe, anchor, or research candidate.';draw()};$('search').onchange=()=>{const q=$('search').value.trim().toLowerCase();if(!q)return;const t=report.professionalTerms.find(x=>[x.id,x.identity,x.englishName,x.russianName].some(v=>v.toLowerCase().includes(q)));if(t&&t.representativeCore){selectedTermId=t.id;$('detail').textContent=describe({label:t,kind:t.regionCount>1?'multi-lobe term':'term'});const target=Math.atan2(t.representativeCore.y,t.representativeCore.x);yaw=-target;draw();return}const c=report.research.candidates.find(x=>[x.canonicalTerm,x.russianCandidate].some(v=>(v||'').toLowerCase().includes(q)));if(c){$(c.disposition.toLowerCase()).checked=true;$('detail').textContent=describe({label:c,kind:'research '+c.disposition.toLowerCase()});if(c.representative)yaw=-Math.atan2(c.representative.y,c.representative.x);draw();return}const a=report.creativeAnchors.find(x=>[x.id,x.englishName,x.russianName].some(v=>v.toLowerCase().includes(q)));if(a){$('anchors').checked=true;$('detail').textContent=describe({label:a,kind:'creative anchor'});yaw=-Math.atan2(a.point.y,a.point.x);draw();return}$('detail').textContent='No matching term, research candidate, or creative anchor.'};window.addEventListener('resize',resize);const initialTab=location.hash.slice(1);const initialButton=document.querySelector(`nav button[data-tab="${initialTab}"]`);if(initialButton){initialButton.click();setTimeout(()=>window.scrollTo(0,0),0)}else resize();
                   </script>
                   </body></html>
                   """;
    }

    private static StringBuilder SvgStart(int width, int height, string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"{EscapeXml(title)}\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\">");
        builder.AppendLine(
            "<style>text{font-family:'Segoe UI',Arial,sans-serif;fill:#edf3fb}.title{font-size:34px;font-weight:700}.muted{font-size:17px;fill:#9aabc1}.slice{font-size:21px;font-weight:650}.axis{font-size:13px;fill:#8294ac}.footer{font-size:16px;fill:#a9b9cd}.marker{font-size:11px;font-weight:800;fill:#08101a}.term{font-size:16px;font-weight:650}.row-number{font-size:17px;font-weight:700;fill:#7ed8ff}.row-detail{font:12px Consolas,monospace;fill:#99aac0}.family{font-size:18px;font-weight:700}.family-ru{font-size:14px;fill:#9aabc1}.chip{font-size:13px}.badge{fill:#f0c86b}</style>");
        builder.AppendLine($"<rect width=\"{width}\" height=\"{height}\" fill=\"#0a1019\"/>");
        return builder;
    }

    private static string F(double value) => value.ToString("0.###", Invariant);

    private static string FileId(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            if (char.IsUpper(character) && builder.Length > 0)
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string EscapeXml(string value) => WebUtility.HtmlEncode(value);

    private static string EscapeMarkdown(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}