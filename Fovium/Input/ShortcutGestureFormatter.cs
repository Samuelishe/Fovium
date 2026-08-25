namespace Fovium.Input;

internal static class ShortcutGestureFormatter
{
    public static string Format(ShortcutGesture? gesture, string unassigned)
    {
        if (gesture is null)
        {
            return unassigned;
        }

        var parts = new List<string>(4);
        if (gesture.Value.Modifiers.HasFlag(ShortcutModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (gesture.Value.Modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (gesture.Value.Modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            parts.Add("Shift");
        }

        parts.Add(gesture.Value.Key switch
        {
            "Left" => "←",
            "Right" => "→",
            "Plus" => "+",
            "Minus" => "−",
            "Comma" => ",",
            _ => gesture.Value.Key,
        });
        return string.Join('+', parts);
    }
}
