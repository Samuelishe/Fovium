using System.Text.Json;
using Fovium.Tools.ColorTaxonomyAudit;

namespace Fovium.Tests.ColorSemantics;

public sealed class ColorSemanticsReportTests
{
    [Fact]
    public void CoreReportContainsEveryProductionSemanticExactlyOnceInStableOrder()
    {
        var report = ColorSemanticsReportBuilder.Build("test-commit");

        Assert.Equal("fovium-color-semantics-report/v1", report.Schema);
        Assert.Equal(45, report.Summary.BroadFamilyCount);
        Assert.Equal(94, report.Summary.ProfessionalTermCount);
        Assert.Equal(99, report.Summary.RegionCount);
        Assert.Equal(5, report.Summary.MultiRegionTermCount);
        Assert.Equal(1800, report.Summary.CreativeAnchorCount);
        Assert.Equal(4096, report.Summary.GamutSampleCount);
        Assert.Equal(report.BroadFamilies.Select(item => item.Id).Order(),
            report.BroadFamilies.Select(item => item.Id));
        Assert.Equal(report.ProfessionalTerms.Select(item => item.Id).Order(),
            report.ProfessionalTerms.Select(item => item.Id));
        Assert.Equal(report.Regions.Select(item => item.Id).Order(), report.Regions.Select(item => item.Id));
        Assert.Equal(report.BroadFamilies.Count, report.BroadFamilies.Select(item => item.Id).Distinct().Count());
        Assert.Equal(report.ProfessionalTerms.Count,
            report.ProfessionalTerms.Select(item => item.Id).Distinct().Count());
        Assert.Equal(report.Regions.Count, report.Regions.Select(item => item.Id).Distinct().Count());
        Assert.Equal(
            ProfessionalShadeCatalog.Definitions.Select(definition => definition.StableId).Order(),
            report.ProfessionalTerms.Select(term => term.Id));
        Assert.Equal(
            ProfessionalShadeCatalog.Definitions.SelectMany(definition => definition.Regions)
                .Select(region => region.StableId)
                .Order(),
            report.Regions.Select(region => region.Id));
        Assert.All(report.ProfessionalTerms, term => Assert.NotNull(term.RepresentativeCore));
        Assert.All(report.Regions, region => Assert.NotNull(region.RepresentativeCore));
    }

    [Fact]
    public void CoreReportIsDeterministicAndPinsTheProductionSemanticFingerprint()
    {
        var first = ColorSemanticsReportBuilder.Build("same-commit");
        var repeated = ColorSemanticsReportBuilder.Build("same-commit");
        var otherCommit = ColorSemanticsReportBuilder.Build("other-commit");

        Assert.Equal(first.ProductionSignature, repeated.ProductionSignature);
        Assert.Equal(first.ProductionSignature, otherCommit.ProductionSignature);
        Assert.Equal(first.ReportSignature, repeated.ReportSignature);
        Assert.NotEqual(first.ReportSignature, otherCommit.ReportSignature);
        Assert.Equal(
            JsonSerializer.Serialize(first, JsonOptions),
            JsonSerializer.Serialize(repeated, JsonOptions));
        Assert.Equal(
            "5d2af1ce16e3f2ed9d5541fab68865b8a96d77fb0df47b39d7fb68715cc8a85b",
            first.ProductionSignature);
    }

    [Fact]
    public void ReportOptionsSelectDocumentedBundlesAndRejectConflictingModes()
    {
        Assert.True(ColorSemanticsReportOptions.TryParse([], out var defaults, out var defaultError));
        Assert.Null(defaultError);
        Assert.NotNull(defaults);
        Assert.True(defaults.Html);
        Assert.True(defaults.Static);
        Assert.True(defaults.Json);
        Assert.True(defaults.Text);

        Assert.True(ColorSemanticsReportOptions.TryParse(["--html-only"], out var html, out _));
        Assert.NotNull(html);
        Assert.True(html.Html);
        Assert.False(html.Static);
        Assert.False(html.Json);
        Assert.False(html.Text);

        Assert.True(ColorSemanticsReportOptions.TryParse(["--static-only"], out var @static, out _));
        Assert.NotNull(@static);
        Assert.False(@static.Html);
        Assert.True(@static.Static);
        Assert.True(@static.Json);
        Assert.True(@static.Text);

        Assert.False(ColorSemanticsReportOptions.TryParse(
            ["--html-only", "--static-only"],
            out _,
            out var conflict));
        Assert.Equal("Report output modes cannot be combined.", conflict);
    }

    [Fact]
    public void R10BaselineFastAuditFingerprintRemainsSemanticallyEquivalent()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var exitCode = ColorTaxonomyAuditApplication.Run(
                ["--mode", "fast", "--output", directory],
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.Equal(
                "3dd7072f7db6fd8e5afdd78735424cc9816491bd7a47d8a5eb49231aeb58dd34",
                File.ReadAllText(Path.Combine(directory, "deterministic-signature.sha256")).Trim());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(0.5, 0.2, 0, 0.2, 0, 0.5)]
    [InlineData(0.5, 0.2, 90, 0, 0.2, 0.5)]
    [InlineData(0.5, 0.2, 360, 0.2, 0, 0.5)]
    [InlineData(1, 0, 275, 0, 0, 1)]
    public void OklchCartesianGeometryPreservesMeaningfulAxes(
        double lightness,
        double chroma,
        double hue,
        double expectedX,
        double expectedY,
        double expectedZ)
    {
        var actual = ColorSemanticsReportBuilder.ToCartesian(lightness, chroma, hue);

        Assert.Equal(expectedX, actual.X, 10);
        Assert.Equal(expectedY, actual.Y, 10);
        Assert.Equal(expectedZ, actual.Z, 10);
        Assert.All(new[] { actual.X, actual.Y, actual.Z }, value => Assert.True(double.IsFinite(value)));
    }

    [Fact]
    public void GamutCloudContainsOnlyReachableReferenceSrgbSamplesAndKnownAnchors()
    {
        var report = ColorSemanticsReportBuilder.Build("gamut");

        Assert.Contains(report.Gamut.Samples, sample => sample.Hex == "#000000" && sample.Lightness == 0);
        Assert.Contains(report.Gamut.Samples,
            sample => sample.Hex == "#FFFFFF" && Math.Abs(sample.Lightness - 1) < 0.000001);
        Assert.Contains(report.Gamut.Samples, sample => sample.Hex == "#FF0000" && sample.HueDegrees is > 29 and < 30);
        Assert.All(report.Gamut.Samples, sample =>
        {
            var converts = AuditSampling.TryOklchToSrgb(
                sample.Lightness,
                sample.Chroma,
                sample.HueDegrees,
                out var roundTrip);
            Assert.True(converts || sample.Red is 0 or 255 || sample.Green is 0 or 255 || sample.Blue is 0 or 255);
            if (converts)
            {
                Assert.InRange(Math.Abs(roundTrip.Red - sample.Red), 0, 1);
                Assert.InRange(Math.Abs(roundTrip.Green - sample.Green), 0, 1);
                Assert.InRange(Math.Abs(roundTrip.Blue - sample.Blue), 0, 1);
            }
        });
        Assert.False(AuditSampling.TryOklchToSrgb(0.5, 0.5, 120, out _));
    }

    [Fact]
    public void StaticAndHtmlArtifactsExposeSemanticLayersWithoutNetworkDependencies()
    {
        var report = ColorSemanticsReportBuilder.Build("artifact-test");
        var overview = ColorSemanticsReportWriter.BuildOverviewSvg(report);
        var relations = ColorSemanticsReportWriter.BuildRelationsSvg(report);
        var html = ColorSemanticsReportWriter.BuildHtml(report);

        Assert.Contains("data-layer=\"oklch-slice\"", overview);
        Assert.Contains("unreachable", overview);
        Assert.Contains("data-relation=\"broad-fallback\"", relations);
        Assert.DoesNotContain("NaN", overview);
        Assert.DoesNotContain("Infinity", overview);
        Assert.DoesNotContain("NaN", relations);
        Assert.DoesNotContain("Infinity", relations);
        Assert.Contains("3D Explorer", html);
        Assert.Contains("reference-sRGB gamut", html);
        Assert.Contains("id=\"taxonomy-data\"", html);
        Assert.Contains("static/overview.svg", html);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn", html, StringComparison.OrdinalIgnoreCase);
        Assert.All(report.ProfessionalTerms, term =>
            Assert.Contains(term.Id, ColorSemanticsReportWriter.BuildDomainSvg(report, term.Domain)));

        var jsonStart = html.IndexOf('>', html.IndexOf("id=\"taxonomy-data\"", StringComparison.Ordinal)) + 1;
        var jsonEnd = html.IndexOf("</script>", jsonStart, StringComparison.Ordinal);
        using var embedded = JsonDocument.Parse(html[jsonStart..jsonEnd]);
        Assert.Equal(report.Schema, embedded.RootElement.GetProperty("schema").GetString());
    }

    [Fact]
    public void WriterProducesPortableCoreBundleAndApplicationRejectsBadArguments()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var options = new ColorSemanticsReportOptions(directory, null, "writer", true, true, true, true);
            var artifacts = ColorSemanticsReportWriter.Write(ColorSemanticsReportBuilder.Build("writer"), options);

            Assert.Equal(13, artifacts.Paths.Count);
            Assert.True(artifacts.TotalBytes > 1_000_000);
            Assert.All(artifacts.Paths, path => Assert.StartsWith(directory, path, StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(directory, "index.html")));
            Assert.True(File.Exists(Path.Combine(directory, "taxonomy.json")));
            Assert.True(File.Exists(Path.Combine(directory, "static", "overview.svg")));
            Assert.True(File.Exists(Path.Combine(directory, "static", "relations.svg")));

            var error = new StringWriter();
            Assert.Equal(2, ColorSemanticsReportApplication.Run(["--unsupported"], TextWriter.Null, error));
            Assert.Contains("Unknown or incomplete report option", error.ToString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingAndPresentResearchEvidenceNeverChangeProductionTruth()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var core = ColorSemanticsReportBuilder.Build("research");
            var missing = ColorSemanticsReportBuilder.Build("research", Path.Combine(directory, "missing.json"));
            var auditPath = Path.Combine(directory, "audit.json");
            File.WriteAllText(auditPath, JsonSerializer.Serialize(CreateResearchAudit(), JsonOptions));
            var deep = ColorSemanticsReportBuilder.Build("research", auditPath);

            Assert.False(core.Research.Available);
            Assert.False(missing.Research.Available);
            Assert.True(deep.Research.Available);
            Assert.Equal(core.ProductionSignature, missing.ProductionSignature);
            Assert.Equal(core.ProductionSignature, deep.ProductionSignature);
            Assert.Equal(
                JsonSerializer.Serialize(core.ProfessionalTerms, JsonOptions),
                JsonSerializer.Serialize(deep.ProfessionalTerms, JsonOptions));
            Assert.Equal(
                JsonSerializer.Serialize(core.Regions, JsonOptions),
                JsonSerializer.Serialize(deep.Regions, JsonOptions));
            var candidate = Assert.Single(deep.Research.Candidates);
            Assert.Equal("Parchment", candidate.CanonicalTerm);
            Assert.Equal("Deferred", candidate.Disposition);
            Assert.NotNull(candidate.Representative);
            Assert.Single(deep.Research.Sources);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AuditReport CreateResearchAudit()
    {
        var sample = new ProductionColorAdapter().Classify(new AuditRgb(230, 220, 190));
        return new AuditReport(
            "fovium-color-taxonomy-audit/v8",
            "Deep",
            AuditOptions.DefaultSeed,
            new AuditOptions(AuditMode.Deep, "unused", null, null, AuditOptions.DefaultSeed).Configuration,
            new AuditMetrics(1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            [
                new ReferenceDatasetSummary("museum", 3, 3, "https://example.invalid", "Public domain", "abc")
                {
                    Independence = "Independent",
                    IndependenceGroup = "museum",
                    CachePolicy = "IgnoredCacheOnly"
                }
            ],
            [],
            [],
            [],
            [],
            [],
            null)
        {
            MasterCandidateLexicon =
            [
                new MasterCandidateLexiconEntry(
                    "Parchment",
                    ["parchment white"],
                    "NeutralOffWhite",
                    CandidateResearchStatus.Deferred,
                    "Evidence core remains broad.",
                    "Пергаментный",
                    [],
                    1,
                    3,
                    1,
                    0,
                    0.01,
                    0.02,
                    sample,
                    new Dictionary<string, int>(),
                    "AntiqueWhite",
                    0.02,
                    1.5)
            ],
            CandidateDomainCoverage = [new CandidateDomainCoverage("NeutralOffWhite", 1, 0, 1, "Sparse")]
        };
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fovium-color-semantics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}