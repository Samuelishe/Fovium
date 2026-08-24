using Fovium.Tools.ProjectStats;

namespace Fovium.Tests.ProjectStats;

public sealed class ProjectStatsCollectorTests
{
    [Fact]
    public void CollectsRepositoryMetricsAndPathBasedOwnership()
    {
        using var repository = new TemporaryRepository();
        repository.WriteFile("src/App.cs", "namespace Demo;\nclass App {}\n");
        repository.WriteFile("Fovium.Tools.Sample/Tool.cs", "namespace Demo;\nclass Tool {}\n");
        repository.WriteFile("experiments/Fovium.Probe/Probe.cs", "namespace Demo;\nclass Probe {}\n");
        repository.WriteFile(
            "Fovium.Tests/SampleTests.cs",
            "using Xunit;\nclass SampleTests {\n[Fact] public void A() {}\n[Theory] [InlineData(1)] public void B(int value) {}\n}\n");
        repository.WriteFile("View.xaml", "<View />\n");
        repository.WriteFile("docs/Guide.md", "# Guide\n");
        repository.WriteFile("Fovium.sln", "Microsoft Visual Studio Solution File\n");
        repository.WriteFile("src/App.csproj", "<Project />\n");

        var scan = new FileScanner().Scan(repository.Root, null);
        var report = ProjectStatsCollector.Collect(scan, 10);

        Assert.Equal(8, report.TotalScannedFiles);
        Assert.Equal(4, report.CSharp.FileCount);
        Assert.Equal(1, report.CSharp.ProductionFileCount);
        Assert.Equal(1, report.CSharp.TestFileCount);
        Assert.Equal(1, report.CSharp.ToolingFileCount);
        Assert.Equal(1, report.CSharp.ExperimentalFileCount);
        Assert.Equal(1, report.Xaml.FileCount);
        Assert.Equal(1, report.Markdown.FileCount);
        Assert.Equal(1, report.Tests.SourceFileCount);
        Assert.Equal(1, report.Tests.ApproximateFactCount);
        Assert.Equal(1, report.Tests.ApproximateTheoryCount);
        Assert.Equal("Fovium.sln", Assert.Single(report.ProjectInventory.Solutions).Path);
        Assert.Equal("src/App.csproj", Assert.Single(report.ProjectInventory.Projects).Path);
        Assert.Contains(report.FolderDensity, folder => folder.Folder == "Fovium.Tests");
        Assert.Equal(
            "experiments/Fovium.Probe/Probe.cs",
            Assert.Single(report.LargestFiles.ExperimentalCSharp).Path);
    }
}
