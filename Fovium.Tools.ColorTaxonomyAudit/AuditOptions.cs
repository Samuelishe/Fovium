namespace Fovium.Tools.ColorTaxonomyAudit;

internal enum AuditMode
{
    Fast,
    Deep,
}

internal sealed record AuditOptions(
    AuditMode Mode,
    string OutputDirectory,
    string? ReferenceDirectory,
    string? BaselineReport,
    int Seed)
{
    public const int DefaultSeed = 0x5F0A_2026;

    public static bool TryParse(
        IReadOnlyList<string> args,
        out AuditOptions? options,
        out string? error)
    {
        var mode = AuditMode.Fast;
        var output = Path.Combine("artifacts", "color-taxonomy-audit", "latest");
        string? references = null;
        string? baseline = null;
        var seed = DefaultSeed;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--mode" && TryRead(args, ref index, out var modeValue))
            {
                if (!Enum.TryParse(modeValue, true, out mode))
                {
                    return Fail($"Unsupported audit mode: {modeValue}", out options, out error);
                }
            }
            else if (argument == "--output" && TryRead(args, ref index, out var outputValue))
            {
                output = outputValue;
            }
            else if (argument == "--references" && TryRead(args, ref index, out var referenceValue))
            {
                references = referenceValue;
            }
            else if (argument == "--baseline" && TryRead(args, ref index, out var baselineValue))
            {
                baseline = baselineValue;
            }
            else if (argument == "--seed" && TryRead(args, ref index, out var seedValue))
            {
                if (!int.TryParse(seedValue, out seed))
                {
                    return Fail($"Invalid integer seed: {seedValue}", out options, out error);
                }
            }
            else
            {
                return Fail($"Unknown or incomplete option: {argument}", out options, out error);
            }
        }

        options = new AuditOptions(mode, output, references, baseline, seed);
        error = null;
        return true;
    }

    public AuditConfiguration Configuration => Mode == AuditMode.Fast
        ? new AuditConfiguration(
            Mode,
            LightnessStep: 0.05,
            ChromaStep: 0.02,
            HueStep: 5,
            MonteCarloSamples: 25_000,
            RgbStep: 32,
            ReferenceCandidateLimit: 250)
        : new AuditConfiguration(
            Mode,
            LightnessStep: 0.025,
            ChromaStep: 0.01,
            HueStep: 2,
            MonteCarloSamples: 150_000,
            RgbStep: 16,
            ReferenceCandidateLimit: 600);

    private static bool TryRead(IReadOnlyList<string> args, ref int index, out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool Fail(string message, out AuditOptions? options, out string? error)
    {
        options = null;
        error = message;
        return false;
    }
}

internal sealed record AuditConfiguration(
    AuditMode Mode,
    double LightnessStep,
    double ChromaStep,
    int HueStep,
    int MonteCarloSamples,
    int RgbStep,
    int ReferenceCandidateLimit);
