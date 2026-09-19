namespace Fovium.Application;

internal enum ActivationMode
{
    Home,
    Directory,
    Folder,
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
            0 => new ActivationPlan(ActivationMode.Home, normalized),
            1 when System.IO.Directory.Exists(normalized[0]) =>
                new ActivationPlan(ActivationMode.Folder, normalized),
            1 => new ActivationPlan(ActivationMode.Directory, normalized),
            _ => new ActivationPlan(ActivationMode.ExplicitSelection, normalized),
        };
    }

    public static ActivationPlan CreateFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new ActivationPlan(ActivationMode.Folder, [Path.GetFullPath(path)]);
    }

    public static ActivationPlan? CreateDrop(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var dropped = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        var files = dropped.Where(File.Exists).ToArray();
        if (files.Length > 0)
        {
            return Create(files);
        }

        var folder = dropped.FirstOrDefault(Directory.Exists);
        return folder is null ? null : CreateFolder(folder);
    }
}