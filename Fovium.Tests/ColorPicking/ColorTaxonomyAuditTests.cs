using System.Text.Json;
using Fovium.ColorPicking;
using Fovium.Tools.ColorTaxonomyAudit;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorTaxonomyAuditTests
{
    [Fact]
    public void OptionsSelectDocumentedDeterministicFastAndDeepProfiles()
    {
        Assert.True(AuditOptions.TryParse([], out var defaultOptions, out var defaultError));
        Assert.Null(defaultError);
        Assert.NotNull(defaultOptions);
        Assert.Equal(AuditMode.Fast, defaultOptions.Mode);
        Assert.Equal(AuditOptions.DefaultSeed, defaultOptions.Seed);
        Assert.Equal(25_000, defaultOptions.Configuration.MonteCarloSamples);

        Assert.True(AuditOptions.TryParse(
            ["--mode", "deep", "--seed", "42", "--output", "report"],
            out var deepOptions,
            out var deepError));
        Assert.Null(deepError);
        Assert.NotNull(deepOptions);
        Assert.Equal(AuditMode.Deep, deepOptions.Mode);
        Assert.Equal(42, deepOptions.Seed);
        Assert.Equal(150_000, deepOptions.Configuration.MonteCarloSamples);
        Assert.True(deepOptions.Configuration.LightnessStep < defaultOptions.Configuration.LightnessStep);
        Assert.True(deepOptions.Configuration.ChromaStep < defaultOptions.Configuration.ChromaStep);
    }

    [Fact]
    public void MonteCarloSamplingIsStableForSeedAndChangesForAnotherSeed()
    {
        var adapter = new ProductionColorAdapter();

        var first = AuditSampling.GenerateMonteCarlo(256, 1234, adapter)
            .Select(sample => sample.Rgb.Packed)
            .ToArray();
        var repeated = AuditSampling.GenerateMonteCarlo(256, 1234, adapter)
            .Select(sample => sample.Rgb.Packed)
            .ToArray();
        var different = AuditSampling.GenerateMonteCarlo(256, 1235, adapter)
            .Select(sample => sample.Rgb.Packed)
            .ToArray();

        Assert.Equal(first, repeated);
        Assert.NotEqual(first, different);
        Assert.Equal(256, first.Distinct().Count());
    }

    [Fact]
    public void BalancedSemanticCohortIsCoordinateDrivenDeterministicAndSeedControlled()
    {
        var adapter = new ProductionColorAdapter();
        var configuration = new AuditOptions(
            AuditMode.Deep,
            "unused",
            null,
            null,
            AuditOptions.DefaultSeed).Configuration;

        var canonical = AuditSampling.GenerateBalancedSemantic(configuration, AuditOptions.DefaultSeed, adapter);
        var repeated = AuditSampling.GenerateBalancedSemantic(configuration, AuditOptions.DefaultSeed, adapter);
        var holdout = AuditSampling.GenerateBalancedSemantic(configuration, 0x6A09_E667, adapter);

        Assert.Equal(
            canonical.Select(item => (item.CohortId, item.Sample.Rgb.Packed)),
            repeated.Select(item => (item.CohortId, item.Sample.Rgb.Packed)));
        Assert.NotEqual(
            canonical.Select(item => item.Sample.Rgb.Packed),
            holdout.Select(item => item.Sample.Rgb.Packed));
        Assert.True(canonical.Count > 300);
        Assert.Equal(
            36,
            canonical.Where(item => item.HueStratum != "Neutral")
                .Select(item => item.HueStratum)
                .Distinct()
                .Count());
        Assert.Equal(
            ["Dark", "Light", "Medium", "VeryDark", "VeryLight"],
            canonical.Select(item => item.LightnessStratum).Distinct().Order().ToArray());
        Assert.Contains("NearNeutral", canonical.Select(item => item.ChromaStratum));
        Assert.Contains("Muted", canonical.Select(item => item.ChromaStratum));
        Assert.Contains("Moderate", canonical.Select(item => item.ChromaStratum));
        Assert.Contains("Saturated", canonical.Select(item => item.ChromaStratum));
        Assert.Contains("Vivid", canonical.Select(item => item.ChromaStratum));
        Assert.Contains(canonical, item => item.Sample.Role == "NearBlack");
        Assert.Contains(canonical, item => item.Sample.Role == "NearWhite");
    }

    [Fact]
    public void BoundaryRefinementSamplesDeterministicLocalRgbNeighborhoods()
    {
        var adapter = new ProductionColorAdapter();
        var red = adapter.Classify(new AuditRgb(255, 0, 0));
        var blue = adapter.Classify(new AuditRgb(0, 0, 255));
        var grid = new StructuredGrid(
            1,
            1,
            2,
            new Dictionary<GridKey, AuditClassification>
            {
                [new GridKey(0, 0, 0)] = red,
                [new GridKey(0, 0, 1)] = blue
            });

        var refined = AuditSampling.GenerateBoundaryRefinement(grid, adapter);
        var packed = refined.Select(sample => sample.Rgb.Packed).ToArray();

        Assert.Equal(packed.Order().ToArray(), packed);
        Assert.Equal(packed.Distinct().Count(), packed.Length);
        Assert.Contains(new AuditRgb(255, 0, 0).Packed, packed);
        Assert.Contains(new AuditRgb(254, 0, 0).Packed, packed);
        Assert.Contains(new AuditRgb(0, 0, 255).Packed, packed);
        Assert.Contains(new AuditRgb(0, 1, 255).Packed, packed);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0)]
    [InlineData(1, 0, 0, 255, 255, 255)]
    [InlineData(0.6279553606, 0.2576833077, 29.2338852, 255, 0, 0)]
    public void OklchGridConversionReturnsExpectedInGamutSrgb(
        double lightness,
        double chroma,
        double hue,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        Assert.True(AuditSampling.TryOklchToSrgb(lightness, chroma, hue, out var actual));
        Assert.Equal(new AuditRgb(expectedRed, expectedGreen, expectedBlue), actual);
    }

    [Fact]
    public void OklchGridConversionRejectsOutOfGamutCoordinates()
    {
        Assert.False(AuditSampling.TryOklchToSrgb(0.50, 0.50, 120, out _));
    }

    [Theory]
    [InlineData("dark mustard", "Mustard")]
    [InlineData("navy blue", "Blue")]
    [InlineData("dusty rose", "DustyPink")]
    [InlineData("reddish orange", "RedOrange")]
    [InlineData("tangerine", "Orange")]
    [InlineData("purplish blue", "BlueViolet")]
    [InlineData("mint green", "Mint")]
    [InlineData("unparseable fantasy", "Unknown")]
    public void ReferenceNamesNormalizeToBoundedAuditSemantics(string name, string expected)
    {
        Assert.Equal(expected, SemanticNameNormalizer.Normalize(name));
    }

    [Theory]
    [InlineData("light lavender", "Lavender")]
    [InlineData("navy blue", "Navy")]
    [InlineData("forest green", "ForestGreen")]
    [InlineData("salmon pink", "Salmon")]
    [InlineData("charcoal grey", "Charcoal")]
    [InlineData("pale lilac", "Lilac")]
    [InlineData("dusty mauve", "Mauve")]
    [InlineData("deep cerulean blue", "Cerulean")]
    [InlineData("seafoam green", "Seafoam")]
    [InlineData("powder blue", "PowderBlue")]
    [InlineData("steel blue", "SteelBlue")]
    [InlineData("royal blue", "RoyalBlue")]
    [InlineData("blood orange", "BloodOrange")]
    [InlineData("rose quartz", "RoseQuartz")]
    [InlineData("olive drab", "OliveDrab")]
    [InlineData("navy green", null)]
    [InlineData("unparseable fantasy", null)]
    public void ReferenceNamesExposeSpecificVocabularyWithoutTreatingFantasyAsTruth(
        string name,
        string? expected)
    {
        Assert.Equal(expected, SpecificColorTermNormalizer.Normalize(name));
    }

    [Fact]
    public void ReferenceLoaderReadsCachedFormatsAndPreservesProvenance()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "xkcd-rgb.txt"), "dark mustard\t#7c6c1b\n");
            File.WriteAllText(
                Path.Combine(directory, "css-color-4.html"),
                "<dfn id=\"valdef-color-coral\">coral</dfn><td>#ff7f50");
            File.WriteAllText(Path.Combine(directory, "meodai-colornames.csv"), "name,hex\nRoyal Blue,#4169e1\n");
            File.WriteAllText(Path.Combine(directory, "nbs-iscc.txt"), "\"Vivid Reddish Orange\" sRGB:E25822\n");
            File.WriteAllText(
                Path.Combine(directory, "provenance.json"),
                "[{\"id\":\"xkcd\",\"source\":\"https://xkcd.com/color/rgb.txt\",\"license\":\"cache only\",\"sha256\":\"abc\"}]");

            var catalog = ReferenceCatalogLoader.Load(directory);

            Assert.Equal(4, catalog.Anchors.Count);
            Assert.Contains(catalog.Anchors, anchor =>
                anchor.Dataset == "xkcd" && anchor.SemanticFamily == "Mustard");
            Assert.Contains(catalog.Anchors, anchor =>
                anchor.Dataset == "css" && anchor.SemanticFamily == "Coral");
            Assert.Contains(catalog.Anchors, anchor =>
                anchor.Dataset == "meodai" && anchor.SemanticFamily == "Blue");
            Assert.Contains(catalog.Anchors, anchor =>
                anchor.Dataset == "iscc-nbs-centroids" && anchor.SemanticFamily == "RedOrange");
            var xkcd = Assert.Single(catalog.Summaries, summary => summary.Id == "xkcd");
            Assert.Equal("cache only", xkcd.License);
            Assert.Equal("abc", xkcd.Sha256);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void VocabularyCandidateProfilesAreReferenceDrivenAndExposeClusterCoherence()
    {
        var catalog = new ReferenceCatalog(
            [
                CreateSpecificReference("xkcd", "seafoam", new AuditRgb(128, 249, 173), "Seafoam"),
                CreateSpecificReference("meodai", "sea foam green", new AuditRgb(126, 237, 177), "Seafoam"),
                CreateSpecificReference("meodai", "dark seafoam", new AuditRgb(31, 181, 122), "Seafoam")
            ],
            []);

        var profile = Assert.Single(VocabularyCandidateAudit.Analyze(catalog));

        Assert.Equal("Seafoam", profile.SpecificTerm);
        Assert.Equal(3, profile.AnchorCount);
        Assert.Equal(2, profile.DatasetSupport);
        Assert.Equal(["meodai", "xkcd"], profile.SupportingDatasets);
        Assert.True(profile.IsShippedTerm);
        Assert.True(profile.P90DeltaE > 0);
        Assert.Equal(3, profile.ProductionFamilyCoverage.Values.Sum());
        Assert.Contains(profile.Representative.Rgb, catalog.Anchors.Select(anchor => anchor.Rgb));
    }

    [Fact]
    public void VocabularyCandidateProfilesSeparateCompactComponentsFromRemoteNoiseDeterministically()
    {
        var anchors = new[]
        {
            CreateSpecificReference("xkcd", "gold", new AuditRgb(238, 190, 44), "Gold"),
            CreateSpecificReference("meodai", "golden yellow", new AuditRgb(244, 193, 38), "Gold"),
            CreateSpecificReference("xkcd", "dark gold", new AuditRgb(164, 126, 23), "Gold"),
            CreateSpecificReference("meodai", "antique gold", new AuditRgb(170, 132, 29), "Gold"),
            CreateSpecificReference("meodai", "green gold noise", new AuditRgb(85, 142, 53), "Gold")
        };
        var catalog = new ReferenceCatalog(anchors, []);

        var profile = Assert.Single(VocabularyCandidateAudit.Analyze(catalog));

        Assert.Equal(2, profile.Components.Count);
        Assert.Equal(1, profile.NoiseAnchorCount);
        Assert.All(profile.Components, component =>
        {
            Assert.Equal(2, component.AnchorCount);
            Assert.Equal(2, component.DatasetSupport);
            Assert.Equal(["meodai", "xkcd"], component.SupportingDatasets);
            Assert.Contains(component.Representative.Rgb, anchors.Select(anchor => anchor.Rgb));
        });
        Assert.Equal(
            profile.Components.OrderByDescending(component => component.DatasetSupport)
                .ThenByDescending(component => component.AnchorCount)
                .ThenBy(component => component.Representative.Rgb.Packed)
                .Select(component => component.Representative.Rgb)
                .ToArray(),
            profile.Components.Select(component => component.Representative.Rgb).ToArray());
    }

    [Fact]
    public void ProfessionalExplanationReportsWinningCompositeRegionAndRejectedCompetitor()
    {
        var explanation = new ProductionColorAdapter().ExplainProfessional(new AuditRgb(176, 224, 230));

        Assert.Equal("PowderBlue", explanation.WinnerTerm);
        Assert.Equal("professional-powder-blue", explanation.WinnerTermStableId);
        Assert.Equal("professional-powder-blue-cyan", explanation.WinnerRegionStableId);
        Assert.Contains(explanation.Candidates, item =>
            item.RegionStableId == "professional-powder-blue-blue" &&
            !item.Matched &&
            item.FailureReason == "parent-family");
    }

    [Fact]
    public void ProfessionalBoundaryProbesCoverEveryRegionAndRemainDeterministic()
    {
        var first = ProfessionalShadeBoundaryAudit.Analyze(new ProductionColorAdapter());
        var repeated = ProfessionalShadeBoundaryAudit.Analyze(new ProductionColorAdapter());
        var regionIds = ProfessionalShadeCatalog.Definitions
            .SelectMany(definition => definition.Regions)
            .Select(region => region.StableId)
            .ToArray();

        Assert.Equal(
            first.Select(item => (item.Region, item.Sample.Rgb.Packed)),
            repeated.Select(item => (item.Region, item.Sample.Rgb.Packed)));
        Assert.Equal(first.Count, first.Select(item => item.Region).Distinct().Count());
        Assert.All(regionIds, regionId => Assert.Contains(first, item => item.Region.StartsWith(regionId + ":")));
        Assert.Contains(first, item => item.Region == "professional-gold-yellow:hue-low-inside");
        Assert.Contains(
            first,
            item => item.Region.StartsWith("professional-gold-yellow:hue-") &&
                    item.Region.EndsWith("-outside"));
        Assert.Contains(first, item =>
            item is { Region: "professional-copper-core:center", Sample.ProfessionalTerm: "Copper" });
        Assert.Contains(first, item =>
            item is { Region: "professional-caramel-core:center", Sample.ProfessionalTerm: "Caramel" });
        Assert.All(
            first.Where(item => item.Sample.ProfessionalTerm is not null),
            item => Assert.NotNull(item.ProfessionalExplanation));
        Assert.Contains(first, item =>
            item is
            {
                Region: "professional-caramel-core:center",
                ProfessionalExplanation.WinnerRegionStableId: "professional-caramel-core"
            });
    }

    [Theory]
    [InlineData("Coral", "RedOrange", true)]
    [InlineData("Orange", "RedOrange", true)]
    [InlineData("Coral", "Blue", false)]
    [InlineData("Gray", "White", true)]
    [InlineData("Mint", "Green", true)]
    public void SemanticCompatibilityKeepsAdjacentNamesButExposesDistantMismatches(
        string product,
        string reference,
        bool expected)
    {
        Assert.Equal(expected, SemanticReferenceAudit.AreCompatible(product, reference));
    }

    [Fact]
    public void SemanticReferenceConsensusRejectsDistantSparseAnchorsAndClustersAdjacentVotes()
    {
        var adapter = new ProductionColorAdapter();
        var sample = adapter.Classify(new AuditRgb(255, 107, 10)) with { Family = "Coral" };
        var catalog = new ReferenceCatalog(
            [
                CreateReference("css", "distant beige", new AuditRgb(245, 245, 220), "Beige"),
                CreateReference("iscc-nbs-centroids", "reddish orange", new AuditRgb(226, 88, 34), "RedOrange"),
                CreateReference("meodai", "orange", new AuditRgb(255, 110, 28), "Orange"),
                CreateReference("xkcd", "bright orange", new AuditRgb(255, 91, 0), "Orange")
            ],
            []);

        var assessment = Assert.IsType<AuditReferenceAssessment>(SemanticReferenceAudit.Assess(sample, catalog));

        Assert.DoesNotContain("css", assessment.DatasetVotes.Keys);
        Assert.Equal("Orange", assessment.ConsensusSemantic);
        Assert.Equal(3, assessment.ConsensusSupport);
        Assert.False(assessment.IsCompatible);
        Assert.Contains(assessment.Neighbors, neighbor =>
            neighbor is { Dataset: "css", SemanticFamily: "Beige" });
    }

    [Theory]
    [InlineData("Brown", "Orange", false)]
    [InlineData("Brown", "Terracotta", true)]
    [InlineData("Beige", "Greige", true)]
    [InlineData("Ochre", "Blue", false)]
    public void SemanticCompatibilityDoesNotCollapseEarthAndSpectralFamilies(
        string product,
        string reference,
        bool expected)
    {
        Assert.Equal(expected, SemanticReferenceAudit.AreCompatible(product, reference));
    }

    [Fact]
    public void ReportSignatureIgnoresRuntimeButMachineReportRetainsIt()
    {
        var firstDirectory = CreateTemporaryDirectory();
        var secondDirectory = CreateTemporaryDirectory();
        try
        {
            var report = CreateMinimalReport(runtimeSeconds: 1.25);

            var first = AuditReportWriter.Write(firstDirectory, report);
            var second = AuditReportWriter.Write(
                secondDirectory,
                report with
                {
                    Metrics = report.Metrics with
                    {
                        RuntimeSeconds = 9.75,
                        ProfessionalClassificationNanosecondsPerSample = 1234.5
                    }
                });

            Assert.Equal(first, second);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(secondDirectory, "summary.json")));
            Assert.Equal(9.75, json.RootElement.GetProperty("metrics").GetProperty("runtimeSeconds").GetDouble());
            Assert.True(File.Exists(Path.Combine(firstDirectory, "summary.md")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "contact-sheet.svg")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "balanced-spectrum.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "family-profiles.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "owner-candidates.svg")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "reference-disagreements.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "vocabulary-gaps.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "vocabulary-candidates.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "vocabulary-candidate-components.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "professional-terms.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "professional-boundary-probes.html")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "holdout-samples.svg")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "changed-regions.html")));
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
        }
    }

    [Fact]
    public void ProfessionalClassifierBenchmarkMeasuresTheIndexedProductionRoute()
    {
        var nanoseconds = ProfessionalShadeBenchmark.MeasureNanosecondsPerSample(rounds: 8);

        Assert.True(double.IsFinite(nanoseconds));
        Assert.True(nanoseconds > 0);
    }

    [Fact]
    public void ApplicationProducesACompleteOfflineFastReport()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = ColorTaxonomyAuditApplication.Run(
                ["--mode", "fast", "--output", directory],
                output,
                error);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, error.ToString());
            Assert.Contains("Mode: Fast", output.ToString());
            Assert.True(File.Exists(Path.Combine(directory, "summary.json")));
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "summary.json")));
            Assert.Equal("fovium-color-taxonomy-audit/v5", json.RootElement.GetProperty("schema").GetString());
            Assert.True(json.RootElement.GetProperty("balancedCohort").GetArrayLength() > 300);
            Assert.True(json.RootElement.GetProperty("specificity").GetProperty("genericFamilyOnly").GetInt32() > 0);
            Assert.Equal(43, json.RootElement.GetProperty("professionalTermSamples").GetArrayLength());
            Assert.Equal(41, json.RootElement.GetProperty("professionalTermCoverage").EnumerateObject().Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static AuditReport CreateMinimalReport(double runtimeSeconds)
    {
        var sample = new ProductionColorAdapter().Classify(new AuditRgb(117, 90, 19));
        return new AuditReport(
            "fovium-color-taxonomy-audit/v2",
            "Fast",
            AuditOptions.DefaultSeed,
            new AuditOptions(AuditMode.Fast, "unused", null, null, AuditOptions.DefaultSeed).Configuration,
            new AuditMetrics(1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, runtimeSeconds),
            new Dictionary<string, int> { [sample.Family] = 1 },
            new Dictionary<string, int> { [sample.Role] = 1 },
            new Dictionary<string, int> { [sample.Family] = 1 },
            new Dictionary<string, int> { ["H080"] = 1 },
            new Dictionary<string, int> { ["Medium"] = 1 },
            new Dictionary<string, int> { ["Moderate"] = 1 },
            [],
            [sample],
            [],
            [new BalancedSemanticSample("sample", "H080", "Medium", "Moderate", sample, null)],
            [],
            [],
            null);
    }

    private static ReferenceAnchor CreateReference(
        string dataset,
        string name,
        AuditRgb rgb,
        string semanticFamily)
    {
        var classified = new ProductionColorAdapter().Classify(rgb);
        return new ReferenceAnchor(
            dataset,
            name,
            rgb,
            semanticFamily,
            classified.LabL,
            classified.LabA,
            classified.LabB);
    }

    private static ReferenceAnchor CreateSpecificReference(
        string dataset,
        string name,
        AuditRgb rgb,
        string specificTerm) =>
        CreateReference(dataset, name, rgb, SemanticNameNormalizer.Normalize(name)) with
        {
            SpecificTerm = specificTerm
        };

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FoviumColorTaxonomyAuditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}