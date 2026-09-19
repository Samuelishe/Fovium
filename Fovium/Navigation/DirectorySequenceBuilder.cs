using Fovium.Imaging;

namespace Fovium.Navigation;

internal sealed class DirectorySequenceBuilder
{
    public Task<ImageSequence> BuildAsync(string selectedPath, CancellationToken cancellationToken) =>
        Task.Run(() => Build(selectedPath, cancellationToken), cancellationToken);

    internal ImageSequence Build(string selectedPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        var fullSelectedPath = Path.GetFullPath(selectedPath);
        var directory = Path.GetDirectoryName(fullSelectedPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return new ImageSequence([fullSelectedPath], 0);
        }

        var candidates = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ImageFormatCapabilities.IsCandidateExtension(Path.GetExtension(path)))
                {
                    candidates.Add(Path.GetFullPath(path));
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return new ImageSequence([fullSelectedPath], 0);
        }
        catch (IOException)
        {
            return new ImageSequence([fullSelectedPath], 0);
        }

        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (!candidates.Contains(fullSelectedPath, pathComparison))
        {
            candidates.Add(fullSelectedPath);
        }

        candidates.Sort(NaturalPathComparer.Instance);
        var initialIndex = candidates.FindIndex(path => pathComparison.Equals(path, fullSelectedPath));
        return new ImageSequence(candidates, initialIndex);
    }

    public Task<ImageSequence?> BuildFolderAsync(
        string folderPath,
        CancellationToken cancellationToken) =>
        Task.Run(() => BuildFolder(folderPath, cancellationToken), cancellationToken);

    internal ImageSequence? BuildFolder(string folderPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var fullFolderPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullFolderPath))
        {
            return null;
        }

        var candidates = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(
                         fullFolderPath,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ImageFormatCapabilities.IsCandidateExtension(Path.GetExtension(path)))
                {
                    candidates.Add(Path.GetFullPath(path));
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        candidates.Sort(NaturalPathComparer.Instance);
        return new ImageSequence(candidates, 0);
    }
}