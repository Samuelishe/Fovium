namespace Fovium.Input;

internal static class ViewerCommandDisplay
{
    public static string FormatToolTip(
        string localizedName,
        ShortcutGesture? gesture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizedName);
        var formatted = ShortcutGestureFormatter.Format(gesture, string.Empty);
        return string.IsNullOrEmpty(formatted)
            ? localizedName
            : $"{localizedName} ({formatted})";
    }
}
