namespace Fovium.Home;

internal readonly record struct RecentThumbnailWindow(int FirstIndex, int LastIndex)
{
    public static RecentThumbnailWindow Empty { get; } = new(0, -1);

    public bool Contains(int index) => index >= FirstIndex && index <= LastIndex;
}

internal static class RecentThumbnailLoadingPolicy
{
    public const int LookAheadItems = 1;
    public const int InitialVisibleItems = 5;

    public static RecentThumbnailWindow Resolve(
        double offset,
        double viewportWidth,
        double itemStride,
        int itemCount)
    {
        if (itemCount <= 0 || itemStride <= 0)
        {
            return RecentThumbnailWindow.Empty;
        }

        var effectiveViewport = viewportWidth > 0
            ? viewportWidth
            : itemStride * InitialVisibleItems;
        var firstVisible = (int)Math.Floor(Math.Max(0, offset) / itemStride);
        var lastVisible = (int)Math.Ceiling((Math.Max(0, offset) + effectiveViewport) / itemStride) - 1;
        return new RecentThumbnailWindow(
            Math.Clamp(firstVisible - LookAheadItems, 0, itemCount - 1),
            Math.Clamp(lastVisible + LookAheadItems, 0, itemCount - 1));
    }
}