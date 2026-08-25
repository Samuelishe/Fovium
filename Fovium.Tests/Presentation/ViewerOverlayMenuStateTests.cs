using Fovium.Input;
using Fovium.Presentation;

namespace Fovium.Tests.Presentation;

public sealed class ViewerOverlayMenuStateTests
{
    [Fact]
    public void CheckedStateAndEffectiveReboundShortcutsComeFromSharedOwners()
    {
        var presentation = new PresentationOverlaySession(PresentationSettings.Default);
        presentation.ToggleHighlight();
        presentation.ToggleMarkupTools();
        var shortcuts = ShortcutSettings.Default
            .WithBinding(ViewerCommand.ToggleHighlight, new ShortcutGesture("J"))
            .WithBinding(ViewerCommand.ToggleMarkupTools, null);

        var state = ViewerOverlayMenuState.Capture(presentation, shortcuts);

        Assert.True(state.HighlightChecked);
        Assert.True(state.MarkupChecked);
        Assert.Equal(new ShortcutGesture("J"), state.HighlightGesture);
        Assert.Null(state.MarkupGesture);
    }
}
