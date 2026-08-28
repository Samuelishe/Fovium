using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class ViewerEscapePolicyTests
{
    [Theory]
    [InlineData(true, true, true, (int)ViewerEscapeAction.None)]
    [InlineData(false, true, true, (int)ViewerEscapeAction.StopSlideshow)]
    [InlineData(false, true, false, (int)ViewerEscapeAction.StopSlideshow)]
    [InlineData(false, false, true, (int)ViewerEscapeAction.LeaveFullscreen)]
    [InlineData(false, false, false, (int)ViewerEscapeAction.CloseViewer)]
    public void EscapeUsesHoldThenSlideshowThenFullscreenThenClosePrecedence(
        bool holdCanceled,
        bool slideshowRunning,
        bool fullscreen,
        int expectedValue)
    {
        Assert.Equal(
            (ViewerEscapeAction)expectedValue,
            ViewerEscapePolicy.Resolve(holdCanceled, slideshowRunning, fullscreen));
    }
}
