namespace Fovium.Home;

internal sealed class RecentNavigationCapture
{
    private readonly StringComparison _pathComparison;
    private string? _pendingPath;

    public RecentNavigationCapture()
        : this(OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal)
    {
    }

    internal RecentNavigationCapture(StringComparison pathComparison)
    {
        _pathComparison = pathComparison;
    }

    public void Arm(string path) => _pendingPath = path;

    public void Cancel() => _pendingPath = null;

    public bool TryConsume(
        string? presentedImageIdentity,
        bool inspectionActive,
        out string capturedPath)
    {
        capturedPath = string.Empty;
        if (_pendingPath is null || inspectionActive)
        {
            return false;
        }

        var pendingPath = _pendingPath;
        _pendingPath = null;
        if (!string.Equals(presentedImageIdentity, pendingPath, _pathComparison))
        {
            return false;
        }

        capturedPath = pendingPath;
        return true;
    }
}