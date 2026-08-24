using Fovium.Tools.ProjectStats;

namespace Fovium.Tests.ProjectStats;

public sealed class ExperimentalClassificationTests
{
    [Fact]
    public void ProductionFoviumCSharpIsReportedAsProduction()
    {
        using var repository = new TemporaryRepository();
        repository.WriteFile("Fovium/Viewer/PhotoViewportControl.cs", "class PhotoViewportControl {}\n");
        repository.WriteFile("Fovium.Tools.ProjectStats/Program.cs", "class Program {}\n");
        repository.WriteFile("experiments/Fovium.RenderProbe/Probe.cs", "class Probe {}\n");

        var report = ProjectStatsCollector.Collect(new FileScanner().Scan(repository.Root, null), 10);

        Assert.Equal(1, report.CSharp.ProductionFileCount);
        Assert.Equal(1, report.CSharp.ToolingFileCount);
        Assert.Equal(1, report.CSharp.ExperimentalFileCount);
        Assert.Equal(
            "Fovium/Viewer/PhotoViewportControl.cs",
            Assert.Single(report.LargestFiles.ProductionCSharp).Path);
    }

    [Fact]
    public void ExperimentCSharpIsNotReportedAsProduction()
    {
        using var repository = new TemporaryRepository();
        repository.WriteFile("experiments/Fovium.RenderProbe/Probe.cs", "class Probe {}\n");

        var report = ProjectStatsCollector.Collect(new FileScanner().Scan(repository.Root, null), 10);

        Assert.Equal(0, report.CSharp.ProductionFileCount);
        Assert.Equal(1, report.CSharp.ExperimentalFileCount);
        Assert.Empty(report.LargestFiles.ProductionCSharp);
        Assert.Equal(
            "experiments/Fovium.RenderProbe/Probe.cs",
            Assert.Single(report.LargestFiles.ExperimentalCSharp).Path);
    }
}
