namespace Fovium.Application;

internal enum ActivationMode
{
    FilePicker,
    Directory,
    ExplicitSelection,
}

internal sealed record ActivationPlan(ActivationMode Mode, IReadOnlyList<string> Paths)
{
    public static ActivationPlan Create(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var normalized = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();

        return normalized.Length switch
        {
            0 => new ActivationPlan(ActivationMode.FilePicker, normalized),
            1 => new ActivationPlan(ActivationMode.Directory, normalized),
            _ => new ActivationPlan(ActivationMode.ExplicitSelection, normalized),
        };
    }
}
