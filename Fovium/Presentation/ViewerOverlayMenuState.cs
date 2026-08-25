using Fovium.Input;

namespace Fovium.Presentation;

internal readonly record struct ViewerOverlayMenuState(
    bool PhotoInfoChecked,
    bool HistogramChecked,
    bool HighlightChecked,
    bool MarkupChecked,
    ShortcutGesture? PhotoInfoGesture,
    ShortcutGesture? HistogramGesture,
    ShortcutGesture? HighlightGesture,
    ShortcutGesture? MarkupGesture)
{
    public static ViewerOverlayMenuState Capture(
        PresentationOverlaySession presentation,
        bool photoInfoVisible,
        bool histogramVisible,
        ShortcutSettings shortcuts)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(shortcuts);
        return new ViewerOverlayMenuState(
            photoInfoVisible,
            histogramVisible,
            presentation.HighlightEnabled,
            presentation.MarkupToolsVisible,
            shortcuts.Get(ViewerCommand.TogglePhotoInfo),
            shortcuts.Get(ViewerCommand.ToggleHistogram),
            shortcuts.Get(ViewerCommand.ToggleHighlight),
            shortcuts.Get(ViewerCommand.ToggleMarkupTools));
    }
}
