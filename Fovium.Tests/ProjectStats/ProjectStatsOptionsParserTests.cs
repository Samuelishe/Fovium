using Fovium.Tools.ProjectStats;

namespace Fovium.Tests.ProjectStats;

public sealed class ProjectStatsOptionsParserTests
{
    [Fact]
    public void ValidTopIsParsed()
    {
        using var repository = new TemporaryRepository();

        var result = ProjectStatsOptionsParser.Parse(
            [repository.Root, "--top", "25"],
            repository.Root);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(25, result.Options.Top);
        Assert.Equal(Path.GetFullPath(repository.Root), result.Options.RepositoryRoot);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void InvalidTopReturnsAnError(string value)
    {
        using var repository = new TemporaryRepository();

        var result = ProjectStatsOptionsParser.Parse(
            [repository.Root, "--top", value],
            repository.Root);

        Assert.False(result.IsSuccess);
        Assert.Contains("positive integer", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownOutputFormatIsSelected()
    {
        using var repository = new TemporaryRepository();

        var result = ProjectStatsOptionsParser.Parse(
            [repository.Root, "--markdown"],
            repository.Root);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReportFormat.Markdown, result.Options!.Format);
    }

    [Fact]
    public void JsonOutputFormatAndRepositoryRelativeOutputPathAreSelected()
    {
        using var repository = new TemporaryRepository();

        var result = ProjectStatsOptionsParser.Parse(
            [repository.Root, "--json", "--output", "report.json"],
            repository.Root);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReportFormat.Json, result.Options!.Format);
        Assert.Equal(Path.Combine(repository.Root, "report.json"), result.Options.OutputPath);
    }

    [Fact]
    public void InvalidArgumentsProduceANonZeroApplicationExitCode()
    {
        using var repository = new TemporaryRepository();
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();

        var exitCode = ProjectStatsApplication.Run(
            [repository.Root, "--top", "invalid"],
            standardOutput,
            standardError,
            repository.Root);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", standardError.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, standardOutput.ToString());
    }
}
