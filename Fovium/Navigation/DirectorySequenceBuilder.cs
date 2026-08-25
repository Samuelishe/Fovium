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
}
