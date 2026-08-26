namespace Fovium.Presentation;

[Flags]
internal enum InteractionRenderLayer
{
    None = 0,
    Photo = 1 << 0,
    Markup = 1 << 1,
    Pointer = 1 << 2,
    FloatingUi = 1 << 3,
    Toolbar = 1 << 4,
}

internal static class InteractionRenderRouting
{
    public static InteractionRenderLayer ForPointerPosition() =>
        InteractionRenderLayer.Pointer;

    public static InteractionRenderLayer ForColorPickerPointerMotion() =>
        InteractionRenderLayer.Pointer;

    public static InteractionRenderLayer ForColorPickerSampleCommit() =>
        InteractionRenderLayer.FloatingUi;

    public static InteractionRenderLayer ForDockPosition() =>
        InteractionRenderLayer.FloatingUi;

    public static InteractionRenderLayer ForViewportChange() =>
        InteractionRenderLayer.Photo | InteractionRenderLayer.Markup;

    public static InteractionRenderLayer ForStageChange() =>
        InteractionRenderLayer.Photo;

    public static InteractionRenderLayer ForPresentationChange(PresentationChangeKind change)
    {
        var layers = InteractionRenderLayer.None;
        if (change.HasFlag(PresentationChangeKind.RenderContent))
        {
            layers |= InteractionRenderLayer.Markup;
        }

        if ((change & (
                PresentationChangeKind.ToolState |
                PresentationChangeKind.StyleState |
                PresentationChangeKind.Visibility |
                PresentationChangeKind.Highlight)) != 0)
        {
            layers |= InteractionRenderLayer.Pointer;
        }

        if ((change & (
                PresentationChangeKind.ToolState |
                PresentationChangeKind.StyleState |
                PresentationChangeKind.HistoryState |
                PresentationChangeKind.Visibility)) != 0)
        {
            layers |= InteractionRenderLayer.Toolbar;
        }

        return layers;
    }
}
