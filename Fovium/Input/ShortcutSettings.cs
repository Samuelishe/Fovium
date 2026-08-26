namespace Fovium.Input;

internal sealed record ShortcutSettings
{
    public Dictionary<string, ShortcutGesture?> Bindings { get; init; } = ShortcutDefaults.CreateBindings();

    public static ShortcutSettings Default => new();

    public ShortcutGesture? Get(ViewerCommand command) =>
        Bindings.TryGetValue(ViewerCommands.GetId(command), out var gesture) ? gesture : null;

    public ShortcutSettings Normalize() => NormalizeCore(
        evolvePreviousDefaults: false,
        out _);

    internal ShortcutSettings NormalizePersistedDefaults(out bool evolvedPreviousDefaults) =>
        NormalizeCore(evolvePreviousDefaults: true, out evolvedPreviousDefaults);

    private ShortcutSettings NormalizeCore(
        bool evolvePreviousDefaults,
        out bool evolvedPreviousDefaults)
    {
        evolvedPreviousDefaults = evolvePreviousDefaults &&
            Bindings is not null &&
            Bindings.TryGetValue(ViewerCommands.GetId(ViewerCommand.BlinkCompare), out var blink) &&
            blink == ShortcutDefaults.PreviousBlinkCompare &&
            Bindings.TryGetValue(ViewerCommands.GetId(ViewerCommand.ClearMarkup), out var clear) &&
            clear == ShortcutDefaults.PreviousClearMarkup;
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

        if (evolvedPreviousDefaults)
        {
            normalized[ViewerCommands.GetId(ViewerCommand.BlinkCompare)] =
                ShortcutDefaults.CurrentBlinkCompare;
            normalized[ViewerCommands.GetId(ViewerCommand.ClearMarkup)] =
                ShortcutDefaults.CurrentClearMarkup;
        }

        var used = new HashSet<(ViewerCommandScope Scope, ShortcutGesture Gesture)>();
        foreach (var definition in ViewerCommands.Definitions)
        {
            var gesture = normalized[definition.Id];
            if (gesture is not null && !used.Add((definition.Scope, gesture.Value)))
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
    internal static ShortcutGesture PreviousBlinkCompare { get; } = new("C");

    internal static ShortcutGesture PreviousClearMarkup { get; } =
        new("Delete", ShortcutModifiers.Control);

    internal static ShortcutGesture CurrentBlinkCompare { get; } =
        new("C", ShortcutModifiers.Shift);

    internal static ShortcutGesture CurrentClearMarkup { get; } = new("C");

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
        [ViewerCommands.GetId(ViewerCommand.BlinkCompare)] = CurrentBlinkCompare,
        [ViewerCommands.GetId(ViewerCommand.ToggleHighlight)] = new("H"),
        [ViewerCommands.GetId(ViewerCommand.ToggleMarkupTools)] = new("P"),
        [ViewerCommands.GetId(ViewerCommand.TogglePhotoInfo)] = new("I"),
        [ViewerCommands.GetId(ViewerCommand.ToggleHistogram)] = new("G"),
        [ViewerCommands.GetId(ViewerCommand.ToggleColorPicker)] = new("K"),
        [ViewerCommands.GetId(ViewerCommand.MarkupUndo)] = new("Z", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.MarkupRedo)] = new("Y", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.ClearMarkup)] = CurrentClearMarkup,
        [ViewerCommands.GetId(ViewerCommand.DecreaseMarkupThickness)] = new("OpenBracket"),
        [ViewerCommands.GetId(ViewerCommand.IncreaseMarkupThickness)] = new("CloseBracket"),
        [ViewerCommands.GetId(ViewerCommand.DecreaseMarkupOpacity)] =
            new("OpenBracket", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.IncreaseMarkupOpacity)] =
            new("CloseBracket", ShortcutModifiers.Control),
        [ViewerCommands.GetId(ViewerCommand.DecreaseHighlightRadius)] = new("OpenBracket"),
        [ViewerCommands.GetId(ViewerCommand.IncreaseHighlightRadius)] = new("CloseBracket"),
        [ViewerCommands.GetId(ViewerCommand.SelectHandTool)] = new("V"),
        [ViewerCommands.GetId(ViewerCommand.SelectBrushTool)] = new("B"),
        [ViewerCommands.GetId(ViewerCommand.SelectEraserTool)] = new("E"),
        [ViewerCommands.GetId(ViewerCommand.SelectLineTool)] = new("L"),
        [ViewerCommands.GetId(ViewerCommand.SelectRectangleTool)] = new("R"),
        [ViewerCommands.GetId(ViewerCommand.SelectEllipseTool)] = new("O"),
        [ViewerCommands.GetId(ViewerCommand.SelectArrowTool)] = new("A"),
        [ViewerCommands.GetId(ViewerCommand.TemporaryMarkupHand)] = new("Space"),
    };
}
