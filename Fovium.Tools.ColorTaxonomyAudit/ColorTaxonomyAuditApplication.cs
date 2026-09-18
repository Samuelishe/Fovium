using System.Diagnostics;
using System.Globalization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class ColorTaxonomyAuditApplication
{
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        if (!AuditOptions.TryParse(args, out var parsed, out var parseError))
        {
            error.WriteLine(parseError);
            error.WriteLine(
                "Usage: --mode fast|deep --output <dir> [--references <dir>] [--baseline <summary.json>] [--seed <int>]");
            return 2;
        }

        var options = parsed!;
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var adapter = new ProductionColorAdapter();
            var structured = AuditSampling.GenerateStructured(options.Configuration, adapter);
            var monteCarlo = AuditSampling.GenerateMonteCarlo(
                options.Configuration.MonteCarloSamples,
                options.Seed,
                adapter);
            var rgbGrid = AuditSampling.GenerateRgbGrid(options.Configuration.RgbStep, adapter);
            var boundaryRefinement = AuditSampling.GenerateBoundaryRefinement(structured, adapter);
            var balancedSemantic = AuditSampling.GenerateBalancedSemantic(
                options.Configuration,
                options.Seed,
                adapter);
            var references = ReferenceCatalogLoader.Load(options.ReferenceDirectory);
            var report = TaxonomyAnalyzer.Analyze(
                options,
                structured,
                monteCarlo,
                rgbGrid,
                boundaryRefinement,
                balancedSemantic,
                references,
                runtimeSeconds: 0);
            report = report with
            {
                Metrics = report.Metrics with { RuntimeSeconds = stopwatch.Elapsed.TotalSeconds }
            };
            if (!string.IsNullOrWhiteSpace(options.BaselineReport))
            {
                var baseline = AuditReportWriter.Read(options.BaselineReport);
                if (baseline is null)
                {
                    error.WriteLine($"Baseline report not found or invalid: {options.BaselineReport}");
                    return 3;
                }

                report = report with { Comparison = Compare(options.BaselineReport, baseline, report) };
            }

            var signature = AuditReportWriter.Write(options.OutputDirectory, report);
            output.WriteLine($"Mode: {report.Mode}");
            output.WriteLine($"Samples: {report.Metrics.TotalUniqueSamples}");
            output.WriteLine(
                $"High/medium anomalies: {report.Metrics.HighSeverityAnomalies}/{report.Metrics.MediumSeverityAnomalies}");
            output.WriteLine(
                $"References: {string.Join(", ", report.References.Select(item => $"{item.Id}={item.AnchorCount}"))}");
            output.WriteLine($"Signature: {signature}");
            output.WriteLine($"Report: {Path.GetFullPath(options.OutputDirectory)}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidDataException)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static AuditComparison Compare(string path, AuditReport baseline, AuditReport current) => new(
        path,
        current.Metrics.HighSeverityAnomalies - baseline.Metrics.HighSeverityAnomalies,
        current.Metrics.MediumSeverityAnomalies - baseline.Metrics.MediumSeverityAnomalies,
        current.Metrics.AbruptDiscontinuities - baseline.Metrics.AbruptDiscontinuities,
        current.Metrics.ChromaReversals - baseline.Metrics.ChromaReversals,
        current.Metrics.LightnessOscillations - baseline.Metrics.LightnessOscillations,
        current.Metrics.ModifierReversals - baseline.Metrics.ModifierReversals,
        current.Metrics.TinyComponents - baseline.Metrics.TinyComponents,
        current.Metrics.ThinSlivers - baseline.Metrics.ThinSlivers,
        current.Metrics.ReferenceDisagreements - baseline.Metrics.ReferenceDisagreements,
        current.Metrics.BalancedIncompatibleDisagreements -
        baseline.Metrics.BalancedIncompatibleDisagreements,
        baseline.BalancedCohort
            .Join(
                current.BalancedCohort,
                before => before.CohortId,
                after => after.CohortId,
                (before, after) => new AuditClassificationChange(before.Sample, after.Sample),
                StringComparer.Ordinal)
            .Where(change => change.Before.Family != change.After.Family ||
                             change.Before.Role != change.After.Role ||
                             change.Before.ShortName != change.After.ShortName ||
                             change.Before.DetailedName != change.After.DetailedName)
            .OrderBy(change => change.After.Rgb.Packed)
            .ToArray());
}