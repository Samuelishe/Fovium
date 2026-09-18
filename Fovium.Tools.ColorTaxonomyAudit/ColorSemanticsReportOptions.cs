namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record ColorSemanticsReportOptions(
    string OutputDirectory,
    string? ResearchReport,
    string Commit,
    bool Html,
    bool Static,
    bool Json,
    bool Text)
{
    public static bool TryParse(
        IReadOnlyList<string> args,
        out ColorSemanticsReportOptions? options,
        out string? error)
    {
        var output = Path.Combine("artifacts", "reports", "color-semantics");
        string? research = null;
        var commit = "unknown";
        var html = true;
        var @static = true;
        var json = true;
        var text = true;
        string? outputMode = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--output" && TryRead(args, ref index, out var value))
            {
                output = value;
            }
            else if (argument == "--research-report" && TryRead(args, ref index, out value))
            {
                research = value;
            }
            else if (argument == "--commit" && TryRead(args, ref index, out value))
            {
                commit = value;
            }
            else if (argument == "--html-only")
            {
                if (outputMode is not null)
                {
                    options = null;
                    error = "Report output modes cannot be combined.";
                    return false;
                }

                outputMode = argument;
                html = true;
                @static = false;
                json = false;
                text = false;
            }
            else if (argument == "--static-only")
            {
                if (outputMode is not null)
                {
                    options = null;
                    error = "Report output modes cannot be combined.";
                    return false;
                }

                outputMode = argument;
                html = false;
                @static = true;
                json = true;
                text = true;
            }
            else
            {
                options = null;
                error = $"Unknown or incomplete report option: {argument}";
                return false;
            }
        }

        options = new ColorSemanticsReportOptions(output, research, commit, html, @static, json, text);
        error = null;
        return true;
    }

    private static bool TryRead(IReadOnlyList<string> args, ref int index, out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return !string.IsNullOrWhiteSpace(value);
    }
}