namespace Fovium.Input;

internal sealed record ShortcutSettings
{
    public Dictionary<string, ShortcutGesture?> Bindings { get; init; } = ShortcutDefaults.CreateBindings();

    public static ShortcutSettings Default => new();

    public ShortcutGesture? Get(ViewerCommand command) =>
        Bindings.TryGetValue(ViewerCommands.GetId(command), out var gesture) ? gesture : null;

    public ShortcutSettings Normalize()
    {
        var normalized = ShortcutDefaults.CreateBindings();
        if (Bindings is null)
        {
            return new ShortcutSettings { Bindings = normalized };
        }

        foreach (var definition in ViewerCommands.Definitions)
        {
            if (!Bindings.TryGetValue(definition.Id, out var gesture))
            {
                continue;
            }

            normalized[definition.Id] = gesture is { } value &&
                AvaloniaShortcutGestureAdapter.IsRepresentable(value)
                ? gesture
                : null;
        }

        var used = new HashSet<ShortcutGesture>();
        foreach (var definition in ViewerCommands.Definitions)
        {
            var gesture = normalized[definition.Id];
            if (gesture is not null && !used.Add(gesture.Value))
            {
                normalized[definition.Id] = null;
            }
        }

        return new ShortcutSettings { Bindings = normalized };
    }

    public ShortcutSettings WithBinding(ViewerCommand command, ShortcutGesture? gesture)
    {
        var copy = new Dictionary<string, ShortcutGesture?>(Bindings, StringComparer.Ordinal)
        {
            [ViewerCommands.GetId(command)] = gesture,
        };
        return this with { Bindings = copy };
    }
}

internal static class ShortcutDefaults
{
    public static Dictionary<string, ShortcutGesture?> CreateBindings() => new(StringComparer.Ordinal)
    {
        [ViewerCommands.GetId(ViewerCommand.PreviousImage)] = new("Left"),
        [ViewerCommands.GetId(ViewerCommand.NextImage)] = new("Right"),
        [ViewerCommands.GetId(ViewerCommand.ZoomIn)] = new("Plus"),
        [ViewerCommands.GetId(ViewerCommand.ZoomOut)] = new("Minus"),
        [ViewerCommands.GetId(ViewerCommand.Fit)] = new("0"),
        [ViewerCommands.GetId(ViewerCommand.ActualSize)] = new("1"),
        [ViewerCommands.GetId(ViewerCommand.ToggleMatte)] = new("M"),
        [ViewerCommands.GetId(ViewerCommand.Fullscreen)] = new("F11"),
        [ViewerCommands.GetId(ViewerCommand.Open)] = new("O", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.Settings)] = new("Comma", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.Peek100)] = new("Z"),
        [ViewerCommands.GetId(ViewerCommand.BlinkCompare)] = new("C"),
        [ViewerCommands.GetId(ViewerCommand.ToggleHighlight)] = new("H"),
        [ViewerCommands.GetId(ViewerCommand.ToggleMarkupTools)] = new("P"),
        [ViewerCommands.GetId(ViewerCommand.MarkupUndo)] = new("Z", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.MarkupRedo)] = new("Y", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.ClearMarkup)] = new("Delete", ShortcutModifiers.Control),
    };
}
