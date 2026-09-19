namespace Fovium.Home;

internal static class HomeCursorPolicy
{
    public static bool ShouldRunViewerIdleTimer(
        ViewerContentMode mode,
        bool lifetimeCanceled,
        bool contextMenuOpen) =>
        mode == ViewerContentMode.Viewer && !lifetimeCanceled && !contextMenuOpen;

    public static bool ShouldHideViewerCursor(
        ViewerContentMode mode,
        bool contextMenuOpen,
        bool hasPointerActivity,
        TimeSpan idleDuration,
        TimeSpan hideDelay) =>
        mode == ViewerContentMode.Viewer &&
        !contextMenuOpen &&
        hasPointerActivity &&
        idleDuration >= hideDelay;
}