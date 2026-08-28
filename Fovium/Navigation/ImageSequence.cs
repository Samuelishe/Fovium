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

    public IEnumerable<int> EnumerateAfter(
        int currentIndex,
        NavigationDirection direction,
        bool wrap)
    {
        if (currentIndex < 0 || currentIndex >= Paths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(currentIndex));
        }

        var step = (int)direction;
        var index = currentIndex;
        for (var visited = 0; visited < Paths.Count - 1; visited++)
        {
            index += step;
            if (index < 0 || index >= Paths.Count)
            {
                if (!wrap)
                {
                    yield break;
                }

                index = direction == NavigationDirection.Next ? 0 : Paths.Count - 1;
            }

            yield return index;
        }
    }
}
