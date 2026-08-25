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
            .WithBinding(ViewerCommand.ToggleMarkupTools, null)
            .WithBinding(ViewerCommand.TogglePhotoInfo, new ShortcutGesture("K"))
            .WithBinding(ViewerCommand.ToggleHistogram, new ShortcutGesture("G"));

        var state = ViewerOverlayMenuState.Capture(
            presentation,
            photoInfoVisible: true,
            histogramVisible: true,
            shortcuts);

        Assert.True(state.PhotoInfoChecked);
        Assert.True(state.HistogramChecked);
        Assert.True(state.HighlightChecked);
        Assert.True(state.MarkupChecked);
        Assert.Equal(new ShortcutGesture("J"), state.HighlightGesture);
        Assert.Null(state.MarkupGesture);
        Assert.Equal(new ShortcutGesture("K"), state.PhotoInfoGesture);
        Assert.Equal(new ShortcutGesture("G"), state.HistogramGesture);
    }
}
