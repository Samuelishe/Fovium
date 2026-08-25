namespace Fovium.Input;

internal enum ViewerCommand
{
    PreviousImage,
    NextImage,
    ZoomIn,
    ZoomOut,
    Fit,
    ActualSize,
    ToggleMatte,
    Fullscreen,
    Open,
    Settings,
    Peek100,
    BlinkCompare,
    ToggleHighlight,
    ToggleMarkupTools,
}

internal enum ViewerCommandTrigger
{
    Press,
    Hold,
}

internal sealed record ViewerCommandDefinition(
    ViewerCommand Command,
    string Id,
    ViewerCommandTrigger Trigger = ViewerCommandTrigger.Press);

internal static class ViewerCommands
{
    private static readonly ViewerCommandDefinition[] DefinitionsValue =
    [
        new(ViewerCommand.PreviousImage, "viewer.previous"),
        new(ViewerCommand.NextImage, "viewer.next"),
        new(ViewerCommand.ZoomIn, "viewer.zoomIn"),
        new(ViewerCommand.ZoomOut, "viewer.zoomOut"),
        new(ViewerCommand.Fit, "viewer.fit"),
        new(ViewerCommand.ActualSize, "viewer.actualSize"),
        new(ViewerCommand.ToggleMatte, "viewer.toggleMatte"),
        new(ViewerCommand.Fullscreen, "viewer.fullscreen"),
        new(ViewerCommand.Open, "viewer.open"),
        new(ViewerCommand.Settings, "viewer.settings"),
        new(ViewerCommand.Peek100, "viewer.peek100", ViewerCommandTrigger.Hold),
        new(ViewerCommand.BlinkCompare, "viewer.blinkCompare", ViewerCommandTrigger.Hold),
        new(ViewerCommand.ToggleHighlight, "viewer.toggleHighlight"),
        new(ViewerCommand.ToggleMarkupTools, "viewer.toggleMarkupTools"),
    ];

    private static readonly IReadOnlyDictionary<ViewerCommand, ViewerCommandDefinition> ByCommand =
        DefinitionsValue.ToDictionary(definition => definition.Command);

    private static readonly IReadOnlyDictionary<string, ViewerCommandDefinition> ById =
        DefinitionsValue.ToDictionary(definition => definition.Id, StringComparer.Ordinal);

    public static IReadOnlyList<ViewerCommandDefinition> Definitions => DefinitionsValue;

    public static string GetId(ViewerCommand command) => ByCommand[command].Id;

    public static ViewerCommandDefinition GetDefinition(ViewerCommand command) => ByCommand[command];

    public static bool TryGetById(string id, out ViewerCommand command)
    {
        if (ById.TryGetValue(id, out var definition))
        {
            command = definition.Command;
            return true;
        }

        command = default;
        return false;
    }
}
