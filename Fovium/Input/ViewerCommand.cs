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
    MarkupUndo,
    MarkupRedo,
    ClearMarkup,
    DecreaseMarkupThickness,
    IncreaseMarkupThickness,
    DecreaseMarkupOpacity,
    IncreaseMarkupOpacity,
    DecreaseHighlightRadius,
    IncreaseHighlightRadius,
    SelectHandTool,
    SelectBrushTool,
    SelectEraserTool,
    SelectLineTool,
    SelectRectangleTool,
    SelectEllipseTool,
    SelectArrowTool,
    TemporaryMarkupHand,
}

internal enum ViewerCommandTrigger
{
    Press,
    Hold,
}

internal enum ViewerCommandScope
{
    Global,
    Highlight,
    Markup,
}

internal enum ViewerCommandGroup
{
    Navigation,
    Viewing,
    Inspection,
    Presentation,
    Markup,
    Application,
}

internal readonly record struct ViewerShortcutContext(
    bool MarkupToolsVisible,
    bool HighlightEnabled)
{
    public static ViewerShortcutContext Global { get; } = new(false, false);
}

internal sealed record ViewerCommandDefinition(
    ViewerCommand Command,
    string Id,
    ViewerCommandGroup Group,
    ViewerCommandScope Scope = ViewerCommandScope.Global,
    ViewerCommandTrigger Trigger = ViewerCommandTrigger.Press);

internal static class ViewerCommands
{
    private static readonly ViewerCommandDefinition[] DefinitionsValue =
    [
        new(ViewerCommand.PreviousImage, "viewer.previous", ViewerCommandGroup.Navigation),
        new(ViewerCommand.NextImage, "viewer.next", ViewerCommandGroup.Navigation),
        new(ViewerCommand.ZoomIn, "viewer.zoomIn", ViewerCommandGroup.Viewing),
        new(ViewerCommand.ZoomOut, "viewer.zoomOut", ViewerCommandGroup.Viewing),
        new(ViewerCommand.Fit, "viewer.fit", ViewerCommandGroup.Viewing),
        new(ViewerCommand.ActualSize, "viewer.actualSize", ViewerCommandGroup.Viewing),
        new(ViewerCommand.ToggleMatte, "viewer.toggleMatte", ViewerCommandGroup.Viewing),
        new(ViewerCommand.Fullscreen, "viewer.fullscreen", ViewerCommandGroup.Viewing),
        new(ViewerCommand.Open, "viewer.open", ViewerCommandGroup.Application),
        new(ViewerCommand.Settings, "viewer.settings", ViewerCommandGroup.Application),
        new(
            ViewerCommand.Peek100,
            "viewer.peek100",
            ViewerCommandGroup.Inspection,
            Trigger: ViewerCommandTrigger.Hold),
        new(
            ViewerCommand.BlinkCompare,
            "viewer.blinkCompare",
            ViewerCommandGroup.Inspection,
            Trigger: ViewerCommandTrigger.Hold),
        new(ViewerCommand.ToggleHighlight, "viewer.toggleHighlight", ViewerCommandGroup.Presentation),
        new(ViewerCommand.ToggleMarkupTools, "viewer.toggleMarkupTools", ViewerCommandGroup.Presentation),
        new(ViewerCommand.MarkupUndo, "viewer.markupUndo", ViewerCommandGroup.Markup),
        new(ViewerCommand.MarkupRedo, "viewer.markupRedo", ViewerCommandGroup.Markup),
        new(
            ViewerCommand.ClearMarkup,
            "viewer.clearMarkup",
            ViewerCommandGroup.Markup,
            ViewerCommandScope.Markup),
        new(
            ViewerCommand.DecreaseMarkupThickness,
            "viewer.markupThicknessDown",
            ViewerCommandGroup.Markup,
            ViewerCommandScope.Markup),
        new(
            ViewerCommand.IncreaseMarkupThickness,
            "viewer.markupThicknessUp",
            ViewerCommandGroup.Markup,
            ViewerCommandScope.Markup),
        new(
            ViewerCommand.DecreaseMarkupOpacity,
            "viewer.markupOpacityDown",
            ViewerCommandGroup.Markup,
            ViewerCommandScope.Markup),
        new(
            ViewerCommand.IncreaseMarkupOpacity,
            "viewer.markupOpacityUp",
            ViewerCommandGroup.Markup,
            ViewerCommandScope.Markup),
        new(
            ViewerCommand.DecreaseHighlightRadius,
            "viewer.highlightRadiusDown",
            ViewerCommandGroup.Presentation,
            ViewerCommandScope.Highlight),
        new(
            ViewerCommand.IncreaseHighlightRadius,
            "viewer.highlightRadiusUp",
            ViewerCommandGroup.Presentation,
            ViewerCommandScope.Highlight),
        new(ViewerCommand.SelectHandTool, "viewer.markupTool.hand", ViewerCommandGroup.Markup, ViewerCommandScope.Markup),
        new(ViewerCommand.SelectBrushTool, "viewer.markupTool.brush", ViewerCommandGroup.Markup, ViewerCommandScope.Markup),
        new(ViewerCommand.SelectEraserTool, "viewer.markupTool.eraser", ViewerCommandGroup.Markup, ViewerCommandScope.Markup),
        new(ViewerCommand.SelectLineTool, "viewer.markupTool.line", ViewerCommandGroup.Markup, ViewerCommandScope.Markup),
        new(ViewerCommand.SelectRectangleTool, "viewer.markupTool.rectangle", ViewerCommandGroup.Markup, ViewerCommandScope.Markup),
        new(ViewerCommand.SelectEllipseTool, "viewer.markupTool.ellipse", ViewerCommandGroup.Markup, ViewerCommandScope.Markup),
        new(ViewerCommand.SelectArrowTool, "viewer.markupTool.arrow", ViewerCommandGroup.Markup, ViewerCommandScope.Markup),
        new(
            ViewerCommand.TemporaryMarkupHand,
            "viewer.markupTemporaryHand",
            ViewerCommandGroup.Markup,
            ViewerCommandScope.Markup,
            ViewerCommandTrigger.Hold),
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
