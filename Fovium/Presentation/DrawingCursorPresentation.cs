namespace Fovium.Presentation;

internal enum DrawingCursorKind
{
    Viewer,
    Highlight,
    Brush,
    Eraser,
    Precision,
    Hand,
}

internal enum ViewerSystemCursorMode
{
    Visible,
    Hidden,
    Hand,
}

internal static class ViewerSystemCursorPresentation
{
    public static ViewerSystemCursorMode Resolve(
        bool pointerInside,
        DrawingCursorKind feedbackKind) =>
        (pointerInside, feedbackKind) switch
        {
            (true, DrawingCursorKind.Hand) => ViewerSystemCursorMode.Hand,
            (true, DrawingCursorKind.Highlight or
                DrawingCursorKind.Brush or
                DrawingCursorKind.Eraser or
                DrawingCursorKind.Precision) => ViewerSystemCursorMode.Hidden,
            _ => ViewerSystemCursorMode.Visible,
        };
}

internal readonly record struct DrawingCursorPresentation(
    DrawingCursorKind Kind,
    double DiameterDip,
    double OutlineWidthDip,
    double CrosshairHalfExtentDip,
    PresentationColor Color,
    double Opacity)
{
    private const double PrecisionDiameterPhysicalPixels = 14;
    private const double OutlineWidthPhysicalPixels = 1;

    public static DrawingCursorPresentation Resolve(
        bool markupToolsVisible,
        bool highlightEnabled,
        MarkupTool tool,
        double strokePhysicalPixels,
        PresentationColor color,
        double opacity,
        double highlightRadiusPhysicalPixels,
        double renderScaling)
    {
        var scaling = double.IsFinite(renderScaling) && renderScaling > 0
            ? renderScaling
            : 1;
        if (markupToolsVisible)
        {
            return tool switch
            {
                MarkupTool.Hand => Create(DrawingCursorKind.Hand),
                MarkupTool.Brush => CreateCircular(DrawingCursorKind.Brush),
                MarkupTool.Eraser => CreateCircular(DrawingCursorKind.Eraser),
                _ => new DrawingCursorPresentation(
                    DrawingCursorKind.Precision,
                    PrecisionDiameterPhysicalPixels / scaling,
                    OutlineWidthPhysicalPixels / scaling,
                    PrecisionDiameterPhysicalPixels / (2 * scaling),
                    color,
                    opacity),
            };
        }

        return highlightEnabled
            ? new DrawingCursorPresentation(
                DrawingCursorKind.Highlight,
                highlightRadiusPhysicalPixels * 2 / scaling,
                0,
                0,
                color,
                opacity)
            : Create(DrawingCursorKind.Viewer);

        DrawingCursorPresentation CreateCircular(DrawingCursorKind kind) => new(
            kind,
            Math.Max(strokePhysicalPixels, 0) / scaling,
            OutlineWidthPhysicalPixels / scaling,
            0,
            color,
            opacity);

        DrawingCursorPresentation Create(DrawingCursorKind kind) => new(
            kind,
            0,
            0,
            0,
            color,
            opacity);
    }

    public static DrawingCursorPresentation CreateColorPicker(double renderScaling)
    {
        var scaling = double.IsFinite(renderScaling) && renderScaling > 0
            ? renderScaling
            : 1;
        return new DrawingCursorPresentation(
            DrawingCursorKind.Precision,
            PrecisionDiameterPhysicalPixels / scaling,
            OutlineWidthPhysicalPixels / scaling,
            PrecisionDiameterPhysicalPixels / (2 * scaling),
            new PresentationColor(0xFF, 0xFF, 0xFF),
            1);
    }
}
