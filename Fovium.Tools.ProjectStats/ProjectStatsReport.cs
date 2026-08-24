namespace Fovium.Tools.ProjectStats;

internal sealed record ExtensionStatistics(string Extension, int FileCount);

internal sealed record FileStatistics(string Path, int Lines, long Characters);

internal sealed record LanguageStatistics(int FileCount, long Lines, long Characters);

internal sealed record CSharpStatistics(
    int FileCount,
    long Lines,
    long Characters,
    int ProductionFileCount,
    int TestFileCount,
    int ToolingFileCount,
    int ExperimentalFileCount);

internal sealed record ProjectItem(string Name, string Path);

internal sealed record ProjectInventory(
    IReadOnlyList<ProjectItem> Solutions,
    IReadOnlyList<ProjectItem> Projects);

internal sealed record TestStatistics(
    int SourceFileCount,
    int ApproximateFactCount,
    int ApproximateTheoryCount);

internal sealed record LargestFileGroups(
    IReadOnlyList<FileStatistics> ProductionCSharp,
    IReadOnlyList<FileStatistics> TestCSharp,
    IReadOnlyList<FileStatistics> ToolingCSharp,
    IReadOnlyList<FileStatistics> ExperimentalCSharp,
    IReadOnlyList<FileStatistics> Xaml,
    IReadOnlyList<FileStatistics> Markdown);

internal sealed record FolderStatistics(
    string Folder,
    int FileCount,
    long Lines,
    long Characters);

internal sealed record ProjectStatsReport(
    int TotalScannedFiles,
    int TotalTextFiles,
    IReadOnlyList<ExtensionStatistics> FilesByExtension,
    CSharpStatistics CSharp,
    LanguageStatistics Xaml,
    LanguageStatistics Markdown,
    ProjectInventory ProjectInventory,
    TestStatistics Tests,
    LargestFileGroups LargestFiles,
    IReadOnlyList<FolderStatistics> FolderDensity,
    IReadOnlyList<SkippedPath> SkippedPaths);

internal static class ProjectStatsCollector
{
    public static ProjectStatsReport Collect(FileScanResult scan, int top)
    {
        var files = scan.Files;
        var csharp = files.Where(IsCSharp).ToArray();
        var xaml = files.Where(file => HasExtension(file, ".xaml")).ToArray();
        var markdown = files.Where(file => HasExtension(file, ".md")).ToArray();
        var testCSharp = csharp
            .Where(file => file.CSharpOwnership == CSharpOwnership.Tests)
            .ToArray();

        var extensions = files
            .GroupBy(file => file.Extension, StringComparer.Ordinal)
            .Select(group => new ExtensionStatistics(group.Key, group.Count()))
            .OrderBy(item => item.Extension, StringComparer.Ordinal)
            .ToArray();

        var solutions = files
            .Where(file => HasExtension(file, ".sln"))
            .Select(ToProjectItem)
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();

        var projects = files
            .Where(file => HasExtension(file, ".csproj"))
            .Select(ToProjectItem)
            .OrderBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();

        var sourceLike = files.Where(file =>
            IsCSharp(file) ||
            HasExtension(file, ".xaml") ||
            HasExtension(file, ".md"));

        var folderDensity = sourceLike
            .GroupBy(file => GetFolder(file.RelativePath), StringComparer.Ordinal)
            .Select(group => new FolderStatistics(
                group.Key,
                group.Count(),
                group.Sum(file => (long)file.Lines),
                group.Sum(file => file.Characters)))
            .OrderBy(item => item.Folder, StringComparer.Ordinal)
            .ToArray();

        return new ProjectStatsReport(
            files.Count,
            files.Count(file => file.IsText),
            extensions,
            new CSharpStatistics(
                csharp.Length,
                csharp.Sum(file => (long)file.Lines),
                csharp.Sum(file => file.Characters),
                csharp.Count(file => file.CSharpOwnership == CSharpOwnership.Production),
                testCSharp.Length,
                csharp.Count(file => file.CSharpOwnership == CSharpOwnership.Tooling),
                csharp.Count(file => file.CSharpOwnership == CSharpOwnership.Experimental)),
            ToLanguageStatistics(xaml),
            ToLanguageStatistics(markdown),
            new ProjectInventory(solutions, projects),
            new TestStatistics(
                testCSharp.Length,
                testCSharp.Sum(file => file.ApproximateFactCount),
                testCSharp.Sum(file => file.ApproximateTheoryCount)),
            new LargestFileGroups(
                Largest(csharp.Where(file => file.CSharpOwnership == CSharpOwnership.Production), top),
                Largest(testCSharp, top),
                Largest(csharp.Where(file => file.CSharpOwnership == CSharpOwnership.Tooling), top),
                Largest(csharp.Where(file => file.CSharpOwnership == CSharpOwnership.Experimental), top),
                Largest(xaml, top),
                Largest(markdown, top)),
            folderDensity,
            scan.SkippedPaths);
    }

    private static bool IsCSharp(ScannedFile file) => HasExtension(file, ".cs");

    private static bool HasExtension(ScannedFile file, string extension) =>
        file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase);

    private static LanguageStatistics ToLanguageStatistics(IReadOnlyCollection<ScannedFile> files) =>
        new(
            files.Count,
            files.Sum(file => (long)file.Lines),
            files.Sum(file => file.Characters));

    private static ProjectItem ToProjectItem(ScannedFile file) =>
        new(Path.GetFileNameWithoutExtension(file.RelativePath), file.RelativePath);

    private static IReadOnlyList<FileStatistics> Largest(
        IEnumerable<ScannedFile> files,
        int top) =>
        files
            .OrderByDescending(file => file.Characters)
            .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
            .Take(top)
            .Select(file => new FileStatistics(file.RelativePath, file.Lines, file.Characters))
            .ToArray();

    private static string GetFolder(string relativePath)
    {
        var separator = relativePath.LastIndexOf('/');
        return separator < 0 ? "." : relativePath[..separator];
    }
}
