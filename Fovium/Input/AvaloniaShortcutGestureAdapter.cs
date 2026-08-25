using Avalonia.Input;

namespace Fovium.Input;

internal static class AvaloniaShortcutGestureAdapter
{
    public static bool TryCreate(KeyEventArgs args, out ShortcutGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(args);
        return TryCreate(args.Key, args.KeyModifiers, out gesture);
    }

    public static bool TryCreate(Key key, KeyModifiers modifiers, out ShortcutGesture gesture)
    {
        var normalizedModifiers = ToShortcutModifiers(modifiers);
        if (!TryGetPrimaryKey(key, out var name))
        {
            gesture = default;
            return false;
        }

        if (key == Key.OemPlus)
        {
            normalizedModifiers &= ~ShortcutModifiers.Shift;
        }

        gesture = new ShortcutGesture(name, normalizedModifiers);
        return gesture.IsValid && !gesture.IsReserved;
    }

    public static bool TryGetPrimaryKey(Key key, out string name)
    {
        name = key switch
        {
            Key.Add or Key.OemPlus => "Plus",
            Key.Subtract or Key.OemMinus => "Minus",
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.OemComma => "Comma",
            Key.OemOpenBrackets => "OpenBracket",
            Key.OemCloseBrackets => "CloseBracket",
            _ => key.ToString(),
        };
        return key != Key.None;
    }

    public static KeyGesture? ToAvalonia(ShortcutGesture? gesture)
    {
        if (gesture is not { IsValid: true } value || !TryParseKey(value.Key, out var key))
        {
            return null;
        }

        return new KeyGesture(key, ToAvaloniaModifiers(value.Modifiers));
    }

    public static bool IsRepresentable(ShortcutGesture gesture) =>
        gesture.IsValid && !gesture.IsReserved && TryParseKey(gesture.Key, out _);

    private static bool TryParseKey(string value, out Key key)
    {
        key = value switch
        {
            "Plus" => Key.Add,
            "Minus" => Key.Subtract,
            "0" => Key.D0,
            "1" => Key.D1,
            "Comma" => Key.OemComma,
            "OpenBracket" => Key.OemOpenBrackets,
            "CloseBracket" => Key.OemCloseBrackets,
            _ when Enum.TryParse<Key>(value, out var parsed) => parsed,
            _ => Key.None,
        };
        return key != Key.None;
    }

    private static ShortcutModifiers ToShortcutModifiers(KeyModifiers modifiers)
    {
        var result = ShortcutModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            result |= ShortcutModifiers.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            result |= ShortcutModifiers.Alt;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            result |= ShortcutModifiers.Shift;
        }

        return result;
    }

    private static KeyModifiers ToAvaloniaModifiers(ShortcutModifiers modifiers)
    {
        var result = KeyModifiers.None;
        if (modifiers.HasFlag(ShortcutModifiers.Control))
        {
            result |= KeyModifiers.Control;
        }

        if (modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            result |= KeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            result |= KeyModifiers.Shift;
        }

        return result;
    }
}
