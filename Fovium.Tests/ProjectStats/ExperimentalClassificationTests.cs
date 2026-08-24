using Fovium.Tools.ProjectStats;

namespace Fovium.Tests.ProjectStats;

public sealed class ExperimentalClassificationTests
{
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
