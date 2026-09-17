using System.Text.Json;
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
    [InlineData("dark mustard", "Ochre")]
    [InlineData("navy blue", "Blue")]
    [InlineData("dusty rose", "Pink")]
    [InlineData("unparseable fantasy", "Unknown")]
    public void ReferenceNamesNormalizeToBoundedAuditSemantics(string name, string expected)
    {
        Assert.Equal(expected, SemanticNameNormalizer.Normalize(name));
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
            File.WriteAllText(
                Path.Combine(directory, "provenance.json"),
                "[{\"id\":\"xkcd\",\"source\":\"https://xkcd.com/color/rgb.txt\",\"license\":\"cache only\",\"sha256\":\"abc\"}]");

            var catalog = ReferenceCatalogLoader.Load(directory);

            Assert.Equal(3, catalog.Anchors.Count);
            Assert.Contains(catalog.Anchors, anchor =>
                anchor.Dataset == "xkcd" && anchor.SemanticFamily == "Ochre");
            Assert.Contains(catalog.Anchors, anchor =>
                anchor.Dataset == "css" && anchor.SemanticFamily == "Coral");
            Assert.Contains(catalog.Anchors, anchor =>
                anchor.Dataset == "meodai" && anchor.SemanticFamily == "Blue");
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
                report with { Metrics = report.Metrics with { RuntimeSeconds = 9.75 } });

            Assert.Equal(first, second);
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(secondDirectory, "summary.json")));
            Assert.Equal(9.75, json.RootElement.GetProperty("metrics").GetProperty("runtimeSeconds").GetDouble());
            Assert.True(File.Exists(Path.Combine(firstDirectory, "summary.md")));
            Assert.True(File.Exists(Path.Combine(firstDirectory, "contact-sheet.svg")));
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
        }
    }

    private static AuditReport CreateMinimalReport(double runtimeSeconds)
    {
        var sample = new ProductionColorAdapter().Classify(new AuditRgb(117, 90, 19));
        return new AuditReport(
            "fovium-color-taxonomy-audit/v1",
            "Fast",
            AuditOptions.DefaultSeed,
            new AuditOptions(AuditMode.Fast, "unused", null, null, AuditOptions.DefaultSeed).Configuration,
            new AuditMetrics(1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, runtimeSeconds),
            new Dictionary<string, int> { [sample.Family] = 1 },
            new Dictionary<string, int> { [sample.Role] = 1 },
            new Dictionary<string, int> { [sample.Family] = 1 },
            [],
            [sample],
            [],
            null);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FoviumColorTaxonomyAuditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}