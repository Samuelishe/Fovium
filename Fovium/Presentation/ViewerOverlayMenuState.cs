using Fovium.Input;

namespace Fovium.Presentation;

internal readonly record struct ViewerOverlayMenuState(
    bool PhotoInfoChecked,
    bool HighlightChecked,
    bool MarkupChecked,
    ShortcutGesture? PhotoInfoGesture,
    ShortcutGesture? HighlightGesture,
    ShortcutGesture? MarkupGesture)
{
    public static ViewerOverlayMenuState Capture(
        PresentationOverlaySession presentation,
        bool photoInfoVisible,
        ShortcutSettings shortcuts)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(shortcuts);
        return new ViewerOverlayMenuState(
            photoInfoVisible,
            presentation.HighlightEnabled,
            presentation.MarkupToolsVisible,
            shortcuts.Get(ViewerCommand.TogglePhotoInfo),
            shortcuts.Get(ViewerCommand.ToggleHighlight),
            shortcuts.Get(ViewerCommand.ToggleMarkupTools));
    }
}
