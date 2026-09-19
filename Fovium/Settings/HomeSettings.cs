namespace Fovium.Settings;

internal enum RecentLocationKind
{
    File,
    Folder,
}

internal sealed record RecentLocation
{
    public RecentLocationKind Kind { get; init; }

    public string Path { get; init; } = string.Empty;

    public string? PreviewPath { get; init; }
}

internal sealed record HomeSettings
{
    public const int MaximumRecentLocations = 20;

    public bool RememberRecentPhotos { get; init; } = true;

    public IReadOnlyList<RecentLocation> RecentLocations { get; init; } = [];

    public static HomeSettings Default { get; } = new();

    public HomeSettings Normalize()
    {
        if (!RememberRecentPhotos)
        {
            return this with { RecentLocations = [] };
        }

        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var seen = new HashSet<string>(pathComparer);
        var normalized = new List<RecentLocation>(MaximumRecentLocations);
        foreach (var location in RecentLocations ?? [])
        {
            if (location is null ||
                !Enum.IsDefined(location.Kind) ||
                string.IsNullOrWhiteSpace(location.Path))
            {
                continue;
            }

            string path;
            try
            {
                path = System.IO.Path.GetFullPath(location.Path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                continue;
            }

            if (!seen.Add(path))
            {
                continue;
            }

            string? previewPath = null;
            var candidatePreviewPath = location.Kind == RecentLocationKind.File
                ? path
                : location.PreviewPath;
            if (!string.IsNullOrWhiteSpace(candidatePreviewPath))
            {
                try
                {
                    previewPath = System.IO.Path.GetFullPath(candidatePreviewPath);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
                {
                }
            }

            normalized.Add(location with { Path = path, PreviewPath = previewPath });
            if (normalized.Count == MaximumRecentLocations)
            {
                break;
            }
        }

        return this with { RecentLocations = normalized };
    }
}