namespace Fovium.Input;

internal enum PhotoPresentationInteraction
{
    WheelZoom,
    DoubleClickZoom,
    DragPan,
    HandPan,
    Peek,
    Blink,
    MarkupDrawing,
    ColorSampling,
}

internal static class PhotoPresentationInputPolicy
{
    public static bool Allows(ViewerCommand command, bool presentationEnabled) =>
        !presentationEnabled || command switch
        {
            ViewerCommand.ZoomIn or
            ViewerCommand.ZoomOut or
            ViewerCommand.Fit or
            ViewerCommand.ActualSize or
            ViewerCommand.Peek100 or
            ViewerCommand.BlinkCompare or
            ViewerCommand.SelectHandTool or
            ViewerCommand.TemporaryMarkupHand => false,
            _ => true,
        };

    public static bool Allows(
        PhotoPresentationInteraction interaction,
        bool presentationEnabled) => !presentationEnabled || interaction switch
        {
            PhotoPresentationInteraction.WheelZoom or
            PhotoPresentationInteraction.DoubleClickZoom or
            PhotoPresentationInteraction.DragPan or
            PhotoPresentationInteraction.HandPan or
            PhotoPresentationInteraction.Peek or
            PhotoPresentationInteraction.Blink => false,
            _ => true,
        };
}
