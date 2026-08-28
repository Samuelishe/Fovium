namespace Fovium.Input;

internal enum ViewerEscapeAction
{
    None,
    StopSlideshow,
    LeaveFullscreen,
    CloseViewer,
}

internal static class ViewerEscapePolicy
{
    public static ViewerEscapeAction Resolve(
        bool transientHoldCanceled,
        bool slideshowRunning,
        bool fullscreen) => transientHoldCanceled
        ? ViewerEscapeAction.None
        : slideshowRunning
            ? ViewerEscapeAction.StopSlideshow
            : fullscreen
                ? ViewerEscapeAction.LeaveFullscreen
                : ViewerEscapeAction.CloseViewer;
}
