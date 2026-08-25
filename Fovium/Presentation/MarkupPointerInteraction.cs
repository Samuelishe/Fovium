namespace Fovium.Presentation;

internal enum MarkupPointerGesture
{
    Draw,
    Pan,
}

internal static class MarkupPointerInteraction
{
    public static MarkupPointerGesture ForTool(MarkupTool tool) =>
        tool == MarkupTool.Hand ? MarkupPointerGesture.Pan : MarkupPointerGesture.Draw;
}
