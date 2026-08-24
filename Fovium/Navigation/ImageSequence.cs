namespace Fovium.Navigation;

internal enum NavigationDirection
{
    Previous = -1,
    Next = 1,
}

internal sealed class ImageSequence
{
    public ImageSequence(IEnumerable<string> paths, int initialIndex)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Paths = paths.Select(Path.GetFullPath).ToArray();
        if (Paths.Count == 0)
        {
            throw new ArgumentException("An image sequence cannot be empty.", nameof(paths));
        }

        if (initialIndex < 0 || initialIndex >= Paths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(initialIndex));
        }

        InitialIndex = initialIndex;
    }

    public IReadOnlyList<string> Paths { get; }

    public int InitialIndex { get; }

    public bool CanMoveFrom(int index, NavigationDirection direction) =>
        direction == NavigationDirection.Previous ? index > 0 : index < Paths.Count - 1;

    public IEnumerable<int> EnumerateFrom(int startIndex, NavigationDirection direction)
    {
        var step = (int)direction;
        for (var index = startIndex; index >= 0 && index < Paths.Count; index += step)
        {
            yield return index;
        }
    }
}
