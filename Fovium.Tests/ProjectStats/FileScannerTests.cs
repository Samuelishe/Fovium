using Fovium.Tools.ProjectStats;

namespace Fovium.Tests.ProjectStats;

public sealed class FileScannerTests
{
    [Fact]
    public void BinObjAndIdeaDirectoriesAreNotScanned()
    {
        using var repository = new TemporaryRepository();
        repository.WriteFile("visible.md");
        repository.WriteFile("bin/hidden.cs");
        repository.WriteFile("obj/hidden.cs");
        repository.WriteFile(".idea/hidden.xml");

        var result = new FileScanner().Scan(repository.Root, null);

        var scannedPaths = result.Files.Select(file => file.RelativePath).ToArray();
        Assert.Equal(["visible.md"], scannedPaths);
    }

    [Fact]
    public void ResourcesTestImagesDirectoryIsNotTraversed()
    {
        using var repository = new TemporaryRepository();
        repository.WriteFile("visible.md");
        repository.WriteFile("resources/test-images/private-name.md", "must not be read");
        var scanner = new FileScanner(path =>
        {
            Assert.DoesNotContain("test-images", path, StringComparison.OrdinalIgnoreCase);
            return new StreamReader(path);
        });

        var result = scanner.Scan(repository.Root, null);

        Assert.Equal(["visible.md"], result.Files.Select(file => file.RelativePath));
        Assert.Empty(result.SkippedPaths);
    }

    [Fact]
    public void GeneratedAndExplicitOutputTargetsAreNotScanned()
    {
        using var repository = new TemporaryRepository();
        repository.WriteFile("visible.md");
        repository.WriteFile("project-stats.md");
        repository.WriteFile("docs/project-stats.md");
        var explicitOutput = repository.WriteFile("reports/custom-report.md");

        var result = new FileScanner().Scan(repository.Root, explicitOutput);

        var scannedPaths = result.Files.Select(file => file.RelativePath).ToArray();
        Assert.Equal(["docs/project-stats.md", "visible.md"], scannedPaths);
    }

    [Fact]
    public void FilesAreSortedByOrdinalRepositoryRelativePath()
    {
        using var repository = new TemporaryRepository();
        repository.WriteFile("zeta.md");
        repository.WriteFile("alpha/second.md");
        repository.WriteFile("middle.md");

        var result = new FileScanner().Scan(repository.Root, null);

        var scannedPaths = result.Files.Select(file => file.RelativePath).ToArray();
        Assert.Equal(["alpha/second.md", "middle.md", "zeta.md"], scannedPaths);
    }

    [Fact]
    public void UnreadableFileIsRecordedWithoutDiscardingReadableFiles()
    {
        using var repository = new TemporaryRepository();
        var blockedPath = repository.WriteFile("blocked.md");
        repository.WriteFile("readable.md");
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var scanner = new FileScanner(path =>
        {
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(blockedPath), comparison))
            {
                throw new UnauthorizedAccessException("Simulated unreadable file.");
            }

            return new StreamReader(path);
        });

        var result = scanner.Scan(repository.Root, null);

        Assert.Equal(["readable.md"], result.Files.Select(file => file.RelativePath));
        var skipped = Assert.Single(result.SkippedPaths);
        Assert.Equal("blocked.md", skipped.RelativePath);
        Assert.Equal(nameof(UnauthorizedAccessException), skipped.Reason);
    }
}
