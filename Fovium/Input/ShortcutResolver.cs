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
    public static ViewerCommand? Resolve(ShortcutSettings settings, ShortcutGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!AvaloniaShortcutGestureAdapter.IsRepresentable(gesture))
        {
            return null;
        }

        foreach (var definition in ViewerCommands.Definitions)
        {
            if (settings.Get(definition.Command) == gesture)
            {
                return definition.Command;
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

        var conflict = ViewerCommands.Definitions
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
}
