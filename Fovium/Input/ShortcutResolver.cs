namespace Fovium.Input;

internal enum ShortcutAssignmentStatus
{
    Applied,
    Conflict,
    Invalid,
}

internal sealed record ShortcutAssignmentResult(
    ShortcutAssignmentStatus Status,
    ShortcutSettings Settings,
    ViewerCommand? ConflictingCommand = null);

internal static class ShortcutResolver
{
    public static ViewerCommand? Resolve(
        ShortcutSettings settings,
        ShortcutGesture gesture,
        ViewerShortcutContext context = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!AvaloniaShortcutGestureAdapter.IsRepresentable(gesture))
        {
            return null;
        }

        if (context.ColorPickerEnabled &&
            settings.Get(ViewerCommand.TemporaryMarkupHand) == gesture)
        {
            return ViewerCommand.TemporaryMarkupHand;
        }

        foreach (var scope in ActiveScopes(context))
        {
            foreach (var definition in ViewerCommands.Definitions.Where(item => item.Scope == scope))
            {
                if (settings.Get(definition.Command) == gesture)
                {
                    return definition.Command;
                }
            }
        }

        return null;
    }

    public static ShortcutAssignmentResult Assign(
        ShortcutSettings settings,
        ViewerCommand command,
        ShortcutGesture gesture,
        bool replaceConflict)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!AvaloniaShortcutGestureAdapter.IsRepresentable(gesture))
        {
            return new ShortcutAssignmentResult(ShortcutAssignmentStatus.Invalid, settings);
        }

        var scope = ViewerCommands.GetDefinition(command).Scope;
        var conflict = ViewerCommands.Definitions
            .Where(definition => definition.Scope == scope)
            .Select(definition => definition.Command)
            .FirstOrDefault(
                candidate => candidate != command && settings.Get(candidate) == gesture,
                (ViewerCommand)(-1));
        if ((int)conflict >= 0 && !replaceConflict)
        {
            return new ShortcutAssignmentResult(ShortcutAssignmentStatus.Conflict, settings, conflict);
        }

        var updated = settings;
        if ((int)conflict >= 0)
        {
            updated = updated.WithBinding(conflict, null);
        }

        updated = updated.WithBinding(command, gesture);
        return new ShortcutAssignmentResult(ShortcutAssignmentStatus.Applied, updated, (int)conflict >= 0 ? conflict : null);
    }

    private static IEnumerable<ViewerCommandScope> ActiveScopes(ViewerShortcutContext context)
    {
        if (context.MarkupToolsVisible)
        {
            yield return ViewerCommandScope.Markup;
        }

        if (context.HighlightEnabled)
        {
            yield return ViewerCommandScope.Highlight;
        }

        yield return ViewerCommandScope.Global;
    }
}
