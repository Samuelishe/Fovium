namespace Fovium.Tools.ProjectStats;

internal static class ProjectStatsApplication
{
    public static int Run(
        IReadOnlyList<string> arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        string? currentDirectory = null)
    {
        var parseResult = ProjectStatsOptionsParser.Parse(arguments, currentDirectory);
        if (parseResult.ShowHelp)
        {
            WriteUsage(standardOutput);
            return 0;
        }

        if (!parseResult.IsSuccess)
        {
            standardError.WriteLine(parseResult.Error);
            WriteUsage(standardError);
            return 2;
        }

        var options = parseResult.Options!;
        var scan = new FileScanner().Scan(options.RepositoryRoot, options.OutputPath);
        var report = ProjectStatsCollector.Collect(scan, options.Top);

        try
        {
            if (options.OutputPath is null)
            {
                WriteReport(report, options.Format, standardOutput);
                return 0;
            }

            using var stream = new FileStream(
                options.OutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream);
            WriteReport(report, options.Format, writer);
            standardOutput.WriteLine($"Wrote {options.Format.ToString().ToLowerInvariant()} report: {options.OutputPath}");
            return 0;
        }
        catch (UnauthorizedAccessException exception)
        {
            standardError.WriteLine($"Cannot write output: {exception.Message}");
            return 1;
        }
        catch (IOException exception)
        {
            standardError.WriteLine($"Cannot write output: {exception.Message}");
            return 1;
        }
    }

    private static void WriteReport(
        ProjectStatsReport report,
        ReportFormat format,
        TextWriter writer)
    {
        switch (format)
        {
            case ReportFormat.Console:
                ConsoleReportWriter.Write(report, writer);
                break;
            case ReportFormat.Markdown:
                MarkdownReportWriter.Write(report, writer);
                break;
            case ReportFormat.Json:
                JsonReportWriter.Write(report, writer);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: ProjectStats [repository-root] [--top N] [--markdown | --json] [--output path]");
        writer.WriteLine("If repository-root is omitted, the current directory is scanned.");
    }
}
