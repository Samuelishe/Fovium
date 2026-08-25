using Fovium.Rendering;

namespace Fovium.Presentation;

internal enum MarkupTool
{
    Brush,
    Eraser,
    Line,
    Rectangle,
    Arrow,
}

internal abstract record MarkupElement(PresentationColor Color, double StrokeWidthSource)
{
    public abstract int PointCount { get; }
}

internal sealed record BrushMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    MarkupStrokePoints Points) : MarkupElement(Color, StrokeWidthSource)
{
    public override int PointCount => Points.Count;
}

internal sealed record LineMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End) : MarkupElement(Color, StrokeWidthSource)
{
    public override int PointCount => 2;
}

internal sealed record RectangleMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End) : MarkupElement(Color, StrokeWidthSource)
{
    public override int PointCount => 2;
}

internal sealed record ArrowMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End) : MarkupElement(Color, StrokeWidthSource)
{
    public override int PointCount => 2;
}
