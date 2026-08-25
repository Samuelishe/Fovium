using Fovium.Rendering;

namespace Fovium.Presentation;

internal enum MarkupTool
{
    Brush,
    Line,
    Rectangle,
    Arrow,
}

internal abstract record MarkupElement(PresentationColor Color, double StrokeWidthSource);

internal sealed record BrushMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD[] Points) : MarkupElement(Color, StrokeWidthSource);

internal sealed record LineMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End) : MarkupElement(Color, StrokeWidthSource);

internal sealed record RectangleMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End) : MarkupElement(Color, StrokeWidthSource);

internal sealed record ArrowMarkup(
    PresentationColor Color,
    double StrokeWidthSource,
    PointD Start,
    PointD End) : MarkupElement(Color, StrokeWidthSource);

internal readonly record struct MarkupRenderSnapshot(
    IReadOnlyList<MarkupElement> Elements,
    MarkupElement? Draft)
{
    public static MarkupRenderSnapshot Empty { get; } = new(Array.Empty<MarkupElement>(), null);

    public bool IsEmpty => Elements.Count == 0 && Draft is null;
}
