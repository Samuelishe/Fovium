using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Fovium.Tools.ProjectStats;

internal enum CSharpOwnership
{
    Production,
    Tests,
    Tooling,
    Experimental,
}

internal sealed record ScannedFile(
    string RelativePath,
    string Extension,
    bool IsText,
    int Lines,
    long Characters,
    CSharpOwnership? CSharpOwnership,
    int ApproximateFactCount,
    int ApproximateTheoryCount);

internal sealed record SkippedPath(string RelativePath, string Reason);

internal sealed record FileScanResult(
    IReadOnlyList<ScannedFile> Files,
    IReadOnlyList<SkippedPath> SkippedPaths);

internal sealed class FileScanner
{
    private static readonly HashSet<string> ExcludedDirectoryNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".idea",
        ".vs",
        ".vscode",
        "bin",
        "obj",
        "packages",
        "artifacts",
        "publish",
        "TestResults",
        ".codex-cache",
    };

    private static readonly HashSet<string> ExcludedFileNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "project-stats.md",
        "project-stats.json",
    };

    private static readonly HashSet<string> ExcludedFileExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".user",
        ".tmp",
        ".temp",
        ".cache",
    };

    private static readonly HashSet<string> TextExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".axaml",
        ".json",
        ".md",
        ".props",
        ".ps1",
        ".sln",
        ".targets",
        ".xaml",
        ".xml",
        ".yaml",
        ".yml",
    };

    private static readonly Regex FactPattern = new(
        @"\[\s*(?:Xunit\.)?Fact(?:Attribute)?(?:\s|\(|\])",
        RegexOptions.CultureInvariant);

    private static readonly Regex TheoryPattern = new(
        @"\[\s*(?:Xunit\.)?Theory(?:Attribute)?(?:\s|\(|\])",
        RegexOptions.CultureInvariant);

    private readonly Func<string, TextReader> _openText;

    public FileScanner(Func<string, TextReader>? openText = null)
    {
        _openText = openText ?? OpenText;
    }

    public FileScanResult Scan(string repositoryRoot, string? outputPath)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var normalizedOutputPath = outputPath is null ? null : Path.GetFullPath(outputPath);
        var files = new List<ScannedFile>();
        var skipped = new List<SkippedPath>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();
            string[] entries;

            try
            {
                entries = Directory.GetFileSystemEntries(directory);
            }
            catch (UnauthorizedAccessException exception)
            {
                skipped.Add(ToSkipped(root, directory, exception));
                continue;
            }
            catch (IOException exception)
            {
                skipped.Add(ToSkipped(root, directory, exception));
                continue;
            }
            catch (SecurityException exception)
            {
                skipped.Add(ToSkipped(root, directory, exception));
                continue;
            }

            foreach (var entry in entries.OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (UnauthorizedAccessException exception)
                {
                    skipped.Add(ToSkipped(root, entry, exception));
                    continue;
                }
                catch (IOException exception)
                {
                    skipped.Add(ToSkipped(root, entry, exception));
                    continue;
                }
                catch (SecurityException exception)
                {
                    skipped.Add(ToSkipped(root, entry, exception));
                    continue;
                }

                var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    skipped.Add(new SkippedPath(
                        ToRelativePath(root, entry),
                        isDirectory ? "ReparsePointDirectory" : "ReparsePointFile"));
                    continue;
                }

                if (isDirectory)
                {
                    if (!ShouldExcludeDirectory(root, entry))
                    {
                        pendingDirectories.Push(entry);
                    }

                    continue;
                }

                if (ShouldExcludeFile(root, entry, normalizedOutputPath))
                {
                    continue;
                }

                var relativePath = ToRelativePath(root, entry);
                var extension = NormalizeExtension(entry);
                var isText = IsTextFile(entry, extension);
                if (!isText)
                {
                    files.Add(new ScannedFile(relativePath, extension, false, 0, 0, null, 0, 0));
                    continue;
                }

                try
                {
                    using var reader = _openText(entry);
                    var text = reader.ReadToEnd();
                    CSharpOwnership? ownership = extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                        ? ClassifyCSharp(relativePath)
                        : null;
                    var factCount = ownership == CSharpOwnership.Tests ? FactPattern.Count(text) : 0;
                    var theoryCount = ownership == CSharpOwnership.Tests ? TheoryPattern.Count(text) : 0;

                    files.Add(new ScannedFile(
                        relativePath,
                        extension,
                        true,
                        CountLines(text),
                        text.Length,
                        ownership,
                        factCount,
                        theoryCount));
                }
                catch (UnauthorizedAccessException exception)
                {
                    skipped.Add(ToSkipped(root, entry, exception));
                }
                catch (IOException exception)
                {
                    skipped.Add(ToSkipped(root, entry, exception));
                }
                catch (SecurityException exception)
                {
                    skipped.Add(ToSkipped(root, entry, exception));
                }
            }
        }

        return new FileScanResult(
            files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(),
            skipped.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray());
    }

    private static TextReader OpenText(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return new StreamReader(stream, Encoding.UTF8, true);
    }

    private static bool ShouldExcludeFile(string root, string path, string? outputPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (outputPath is not null &&
            string.Equals(Path.GetFullPath(path), outputPath, comparison))
        {
            return true;
        }

        var containingDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        var isRootGeneratedReport = containingDirectory is not null &&
                                    string.Equals(containingDirectory, root, comparison) &&
                                    ExcludedFileNames.Contains(Path.GetFileName(path));

        return isRootGeneratedReport ||
               ExcludedFileExtensions.Contains(Path.GetExtension(path));
    }

    private static bool ShouldExcludeDirectory(string root, string path) =>
        ExcludedDirectoryNames.Contains(Path.GetFileName(path)) ||
        ToRelativePath(root, path).Equals(
            "resources/test-images",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsTextFile(string path, string extension) =>
        TextExtensions.Contains(extension) ||
        Path.GetFileName(path).Equals(".gitignore", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) ? "[none]" : extension.ToLowerInvariant();
    }

    private static CSharpOwnership ClassifyCSharp(string relativePath)
    {
        var segments = relativePath.Split('/');
        if (segments.Length > 0 &&
            segments[0].Equals("experiments", StringComparison.OrdinalIgnoreCase))
        {
            return CSharpOwnership.Experimental;
        }

        if (segments.Any(segment =>
                segment.Equals("Fovium.Tests", StringComparison.OrdinalIgnoreCase) ||
                segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)))
        {
            return CSharpOwnership.Tests;
        }

        if (segments.Any(segment =>
                segment.StartsWith("Fovium.Tools.", StringComparison.OrdinalIgnoreCase)))
        {
            return CSharpOwnership.Tooling;
        }

        return CSharpOwnership.Production;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var lines = 1;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        return text[^1] == '\n' ? lines - 1 : lines;
    }

    private static SkippedPath ToSkipped(string root, string path, Exception exception) =>
        new(ToRelativePath(root, path), exception.GetType().Name);

    private static string ToRelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
