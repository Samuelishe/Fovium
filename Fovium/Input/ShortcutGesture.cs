using System.Text.Json.Serialization;

namespace Fovium.Input;

[Flags]
internal enum ShortcutModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
}

internal readonly record struct ShortcutGesture(string Key, ShortcutModifiers Modifiers = ShortcutModifiers.None)
{
    private static readonly HashSet<string> RejectedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Escape",
        "LeftCtrl",
        "RightCtrl",
        "LeftShift",
        "RightShift",
        "LeftAlt",
        "RightAlt",
        "LWin",
        "RWin",
        "None",
    };

    [JsonIgnore]
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Key) &&
        !RejectedKeys.Contains(Key) &&
        (Modifiers & ~(ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Shift)) == 0;

    [JsonIgnore]
    public bool IsReserved => string.Equals(Key, "Escape", StringComparison.OrdinalIgnoreCase);
}
