using System.Diagnostics;
using System.Globalization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class ColorSemanticsReportApplication
{
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        if (!ColorSemanticsReportOptions.TryParse(args, out var parsed, out var parseError))
        {
            error.WriteLine(parseError);
            error.WriteLine(
                "Usage: report [--output <dir>] [--research-report <summary.json>] [--commit <sha>] " +
                "[--html-only|--static-only]");
            return 2;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var report = ColorSemanticsReportBuilder.Build(parsed!.Commit, parsed.ResearchReport);
            var buildTime = stopwatch.Elapsed;
            var artifacts = ColorSemanticsReportWriter.Write(report, parsed);
            stopwatch.Stop();
            output.Write(ColorSemanticsReportWriter.BuildTextSummary(report));
            output.WriteLine($"Generation: {stopwatch.Elapsed.TotalSeconds:0.000} s " +
                             $"(model {buildTime.TotalSeconds:0.000} s)");
            output.WriteLine($"Artifacts: {artifacts.Paths.Count} files / {artifacts.TotalBytes / 1024d:0.0} KiB");
            output.WriteLine($"Report: {artifacts.OutputDirectory}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or InvalidDataException or System.Text.Json.JsonException)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }
}