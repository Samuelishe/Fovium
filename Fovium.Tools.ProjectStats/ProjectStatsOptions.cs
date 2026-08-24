namespace Fovium.Tools.ProjectStats;

internal enum ReportFormat
{
    Console,
    Markdown,
    Json,
}

internal sealed record ProjectStatsOptions(
    string RepositoryRoot,
    int Top,
    ReportFormat Format,
    string? OutputPath);

internal sealed record OptionsParseResult(
    ProjectStatsOptions? Options,
    string? Error,
    bool ShowHelp)
{
    public bool IsSuccess => Options is not null;
}

internal static class ProjectStatsOptionsParser
{
    public const int DefaultTop = 10;

    public static OptionsParseResult Parse(
        IReadOnlyList<string> arguments,
        string? currentDirectory = null)
    {
        currentDirectory ??= Directory.GetCurrentDirectory();

        string? repositoryRootArgument = null;
        string? outputArgument = null;
        var top = DefaultTop;
        var format = ReportFormat.Console;
        var explicitFormat = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--help" or "-h":
                    return new OptionsParseResult(null, null, true);

                case "--top":
                    if (!TryReadValue(arguments, ref index, out var topValue) ||
                        !int.TryParse(topValue, out top) ||
                        top <= 0)
                    {
                        return Failure("--top requires a positive integer.");
                    }

                    break;

                case "--markdown":
                    if (explicitFormat && format != ReportFormat.Markdown)
                    {
                        return Failure("--markdown and --json cannot be combined.");
                    }

                    format = ReportFormat.Markdown;
                    explicitFormat = true;
                    break;

                case "--json":
                    if (explicitFormat && format != ReportFormat.Json)
                    {
                        return Failure("--markdown and --json cannot be combined.");
                    }

                    format = ReportFormat.Json;
                    explicitFormat = true;
                    break;

                case "--output":
                    if (!TryReadValue(arguments, ref index, out outputArgument))
                    {
                        return Failure("--output requires a file path.");
                    }

                    break;

                default:
                    if (argument.StartsWith("-", StringComparison.Ordinal))
                    {
                        return Failure($"Unknown option: {argument}");
                    }

                    if (repositoryRootArgument is not null)
                    {
                        return Failure("Only one repository root may be specified.");
                    }

                    repositoryRootArgument = argument;
                    break;
            }
        }

        var repositoryRoot = Path.GetFullPath(repositoryRootArgument ?? currentDirectory, currentDirectory);
        if (!Directory.Exists(repositoryRoot))
        {
            return Failure($"Repository root does not exist: {repositoryRoot}");
        }

        string? outputPath = null;
        if (outputArgument is not null)
        {
            outputPath = Path.GetFullPath(outputArgument, repositoryRoot);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (outputDirectory is null || !Directory.Exists(outputDirectory))
            {
                return Failure($"Output directory does not exist: {outputDirectory}");
            }
        }

        var options = new ProjectStatsOptions(repositoryRoot, top, format, outputPath);
        return new OptionsParseResult(options, null, false);
    }

    private static bool TryReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        out string value)
    {
        if (index + 1 >= arguments.Count ||
            arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = arguments[++index];
        return true;
    }

    private static OptionsParseResult Failure(string message) =>
        new(null, message, false);
}
