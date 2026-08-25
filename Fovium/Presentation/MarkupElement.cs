using Fovium.Rendering;

namespace Fovium.Presentation;

internal enum MarkupTool
{
    Hand,
    Brush,
    Eraser,
    Line,
    Rectangle,
    Ellipse,
    Arrow,
}

[Flags]
internal enum MarkupDrawingModifiers
{
    None = 0,
    Constrain = 1,
}

internal abstract record MarkupElement(
    PresentationColor Color,
    double StrokeWidthSource,
    double Opacity)
{
    public abstract int PointCount { get; }
}

internal sealed record BrushMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    MarkupStrokePoints Points,
    double Opacity = 1) : MarkupElement(Color, StrokeWidthSource, Opacity)
{
    public override int PointCount => Points.Count;
}

internal sealed record LineMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End,
    double Opacity = 1) : MarkupElement(Color, StrokeWidthSource, Opacity)
{
    public override int PointCount => 2;
}

internal sealed record RectangleMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End,
    double Opacity = 1) : MarkupElement(Color, StrokeWidthSource, Opacity)
{
    public override int PointCount => 2;
}

internal sealed record EllipseMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End,
    double Opacity = 1) : MarkupElement(Color, StrokeWidthSource, Opacity)
{
    public override int PointCount => 2;
}

internal sealed record ArrowMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End,
    double Opacity = 1) : MarkupElement(Color, StrokeWidthSource, Opacity)
{
    public override int PointCount => 2;
}
