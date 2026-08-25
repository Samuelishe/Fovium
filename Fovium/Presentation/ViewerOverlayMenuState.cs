using Fovium.Input;

namespace Fovium.Presentation;

internal readonly record struct ViewerOverlayMenuState(
    bool HighlightChecked,
    bool MarkupChecked,
    ShortcutGesture? HighlightGesture,
    ShortcutGesture? MarkupGesture)
{
    public static ViewerOverlayMenuState Capture(
        PresentationOverlaySession presentation,
        ShortcutSettings shortcuts)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(shortcuts);
        return new ViewerOverlayMenuState(
            presentation.HighlightEnabled,
            presentation.MarkupToolsVisible,
            shortcuts.Get(ViewerCommand.ToggleHighlight),
            shortcuts.Get(ViewerCommand.ToggleMarkupTools));
    }
}
