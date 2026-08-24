using System.Globalization;
using System.Text.Json;

namespace Fovium.Tools.ProjectStats;

internal static class ConsoleReportWriter
{
    public static void Write(ProjectStatsReport report, TextWriter writer)
    {
        writer.WriteLine("Fovium ProjectStats");
        writer.WriteLine($"Scanned files: {Format(report.TotalScannedFiles)}");
        writer.WriteLine($"Text files: {Format(report.TotalTextFiles)}");
        writer.WriteLine();
        writer.WriteLine("Files by extension:");
        foreach (var extension in report.FilesByExtension)
        {
            writer.WriteLine($"  {extension.Extension}: {Format(extension.FileCount)}");
        }

        writer.WriteLine();
        WriteLanguage(writer, "C#", report.CSharp.FileCount, report.CSharp.Lines, report.CSharp.Characters);
        writer.WriteLine(
            $"  Ownership: Production {Format(report.CSharp.ProductionFileCount)}, " +
            $"Tests {Format(report.CSharp.TestFileCount)}, Tooling {Format(report.CSharp.ToolingFileCount)}, " +
            $"Experimental {Format(report.CSharp.ExperimentalFileCount)}");
        WriteLanguage(writer, "XAML", report.Xaml.FileCount, report.Xaml.Lines, report.Xaml.Characters);
        WriteLanguage(writer, "Markdown", report.Markdown.FileCount, report.Markdown.Lines, report.Markdown.Characters);

        writer.WriteLine();
        writer.WriteLine(
            $"Tests (lexical): source files {Format(report.Tests.SourceFileCount)}, " +
            $"[Fact] {Format(report.Tests.ApproximateFactCount)}, " +
            $"[Theory] {Format(report.Tests.ApproximateTheoryCount)}");

        WriteProjects(writer, "Solutions", report.ProjectInventory.Solutions);
        WriteProjects(writer, "Projects", report.ProjectInventory.Projects);
        WriteLargest(writer, "Largest production C#", report.LargestFiles.ProductionCSharp);
        WriteLargest(writer, "Largest test C#", report.LargestFiles.TestCSharp);
        WriteLargest(writer, "Largest tooling C#", report.LargestFiles.ToolingCSharp);
        WriteLargest(writer, "Largest experimental C#", report.LargestFiles.ExperimentalCSharp);
        WriteLargest(writer, "Largest XAML", report.LargestFiles.Xaml);
        WriteLargest(writer, "Largest Markdown", report.LargestFiles.Markdown);

        writer.WriteLine();
        writer.WriteLine("Folder density (.cs, .xaml, .md):");
        foreach (var folder in report.FolderDensity)
        {
            writer.WriteLine(
                $"  {folder.Folder}: {Format(folder.FileCount)} files, " +
                $"{Format(folder.Lines)} lines, {Format(folder.Characters)} chars");
        }

        if (report.SkippedPaths.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("Skipped paths:");
            foreach (var skipped in report.SkippedPaths)
            {
                writer.WriteLine($"  {skipped.RelativePath}: {skipped.Reason}");
            }
        }
    }

    private static void WriteLanguage(
        TextWriter writer,
        string name,
        int files,
        long lines,
        long characters) =>
        writer.WriteLine(
            $"{name}: {Format(files)} files, {Format(lines)} lines, {Format(characters)} chars");

    private static void WriteProjects(
        TextWriter writer,
        string heading,
        IReadOnlyList<ProjectItem> items)
    {
        writer.WriteLine();
        writer.WriteLine($"{heading}:");
        if (items.Count == 0)
        {
            writer.WriteLine("  (none)");
            return;
        }

        foreach (var item in items)
        {
            writer.WriteLine($"  {item.Name}: {item.Path}");
        }
    }

    private static void WriteLargest(
        TextWriter writer,
        string heading,
        IReadOnlyList<FileStatistics> files)
    {
        writer.WriteLine();
        writer.WriteLine($"{heading}:");
        if (files.Count == 0)
        {
            writer.WriteLine("  (none)");
            return;
        }

        foreach (var file in files)
        {
            writer.WriteLine(
                $"  {file.Path}: {Format(file.Lines)} lines, {Format(file.Characters)} chars");
        }
    }

    private static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}

internal static class MarkdownReportWriter
{
    public static void Write(ProjectStatsReport report, TextWriter writer)
    {
        writer.WriteLine("# Fovium ProjectStats");
        writer.WriteLine();
        writer.WriteLine("Generated repository diagnostics. This report is not a quality gate.");
        writer.WriteLine();
        writer.WriteLine("## Summary");
        writer.WriteLine();
        writer.WriteLine("| Metric | Value |");
        writer.WriteLine("| --- | ---: |");
        writer.WriteLine($"| Scanned files | {report.TotalScannedFiles} |");
        writer.WriteLine($"| Text files | {report.TotalTextFiles} |");
        writer.WriteLine($"| C# files | {report.CSharp.FileCount} |");
        writer.WriteLine($"| C# lines | {report.CSharp.Lines} |");
        writer.WriteLine($"| C# characters | {report.CSharp.Characters} |");
        writer.WriteLine($"| XAML files | {report.Xaml.FileCount} |");
        writer.WriteLine($"| XAML lines | {report.Xaml.Lines} |");
        writer.WriteLine($"| XAML characters | {report.Xaml.Characters} |");
        writer.WriteLine($"| Markdown files | {report.Markdown.FileCount} |");
        writer.WriteLine($"| Markdown lines | {report.Markdown.Lines} |");
        writer.WriteLine($"| Markdown characters | {report.Markdown.Characters} |");

        writer.WriteLine();
        writer.WriteLine("## Files by extension");
        WriteExtensionTable(writer, report.FilesByExtension);

        writer.WriteLine();
        writer.WriteLine("## C# ownership");
        writer.WriteLine();
        writer.WriteLine("Path-based classification; it is not semantic analysis.");
        writer.WriteLine();
        writer.WriteLine("| Ownership | Files |");
        writer.WriteLine("| --- | ---: |");
        writer.WriteLine($"| Production | {report.CSharp.ProductionFileCount} |");
        writer.WriteLine($"| Tests | {report.CSharp.TestFileCount} |");
        writer.WriteLine($"| Tooling | {report.CSharp.ToolingFileCount} |");
        writer.WriteLine($"| Experimental | {report.CSharp.ExperimentalFileCount} |");

        writer.WriteLine();
        writer.WriteLine("## Projects");
        WriteProjectTable(writer, report.ProjectInventory);

        writer.WriteLine();
        writer.WriteLine("## Tests (lexical estimates)");
        writer.WriteLine();
        writer.WriteLine("| Metric | Count |");
        writer.WriteLine("| --- | ---: |");
        writer.WriteLine($"| Test source files | {report.Tests.SourceFileCount} |");
        writer.WriteLine($"| Approximate `[Fact]` | {report.Tests.ApproximateFactCount} |");
        writer.WriteLine($"| Approximate `[Theory]` | {report.Tests.ApproximateTheoryCount} |");

        WriteLargestSection(writer, "Production C#", report.LargestFiles.ProductionCSharp);
        WriteLargestSection(writer, "Test C#", report.LargestFiles.TestCSharp);
        WriteLargestSection(writer, "Tooling C#", report.LargestFiles.ToolingCSharp);
        WriteLargestSection(writer, "Experimental C#", report.LargestFiles.ExperimentalCSharp);
        WriteLargestSection(writer, "XAML", report.LargestFiles.Xaml);
        WriteLargestSection(writer, "Markdown", report.LargestFiles.Markdown);

        writer.WriteLine();
        writer.WriteLine("## Folder density");
        writer.WriteLine();
        writer.WriteLine("Includes `.cs`, `.xaml`, and `.md` files.");
        writer.WriteLine();
        writer.WriteLine("| Folder | Files | Lines | Characters |");
        writer.WriteLine("| --- | ---: | ---: | ---: |");
        foreach (var folder in report.FolderDensity)
        {
            writer.WriteLine(
                $"| `{Escape(folder.Folder)}` | {folder.FileCount} | {folder.Lines} | {folder.Characters} |");
        }

        writer.WriteLine();
        writer.WriteLine("## Skipped paths");
        writer.WriteLine();
        if (report.SkippedPaths.Count == 0)
        {
            writer.WriteLine("None.");
        }
        else
        {
            writer.WriteLine("| Path | Reason |");
            writer.WriteLine("| --- | --- |");
            foreach (var skipped in report.SkippedPaths)
            {
                writer.WriteLine($"| `{Escape(skipped.RelativePath)}` | {Escape(skipped.Reason)} |");
            }
        }
    }

    private static void WriteExtensionTable(
        TextWriter writer,
        IReadOnlyList<ExtensionStatistics> extensions)
    {
        writer.WriteLine();
        writer.WriteLine("| Extension | Files |");
        writer.WriteLine("| --- | ---: |");
        foreach (var extension in extensions)
        {
            writer.WriteLine($"| `{Escape(extension.Extension)}` | {extension.FileCount} |");
        }
    }

    private static void WriteProjectTable(TextWriter writer, ProjectInventory inventory)
    {
        writer.WriteLine();
        writer.WriteLine("| Kind | Name | Path |");
        writer.WriteLine("| --- | --- | --- |");
        foreach (var solution in inventory.Solutions)
        {
            writer.WriteLine($"| Solution | {Escape(solution.Name)} | `{Escape(solution.Path)}` |");
        }

        foreach (var project in inventory.Projects)
        {
            writer.WriteLine($"| C# project | {Escape(project.Name)} | `{Escape(project.Path)}` |");
        }
    }

    private static void WriteLargestSection(
        TextWriter writer,
        string heading,
        IReadOnlyList<FileStatistics> files)
    {
        writer.WriteLine();
        writer.WriteLine($"## Largest {heading} files");
        writer.WriteLine();
        if (files.Count == 0)
        {
            writer.WriteLine("None.");
            return;
        }

        writer.WriteLine("| Path | Lines | Characters |");
        writer.WriteLine("| --- | ---: | ---: |");
        foreach (var file in files)
        {
            writer.WriteLine($"| `{Escape(file.Path)}` | {file.Lines} | {file.Characters} |");
        }
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}

internal static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Write(ProjectStatsReport report, TextWriter writer)
    {
        writer.Write(JsonSerializer.Serialize(report, Options));
        writer.WriteLine();
    }
}
