using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Presentation;

internal static class SkiaMarkupOverlayRenderer
{
    public static void Draw(
        SKCanvas canvas,
        RectD destination,
        PixelSize orientedSourceSize,
        MarkupRenderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (snapshot.IsEmpty || destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        var transform = new MarkupTransform(destination, orientedSourceSize);
        canvas.Save();
        try
        {
            canvas.ClipRect(ToRect(destination));
            foreach (var element in snapshot.Elements)
            {
                DrawElement(canvas, transform, element);
            }

            if (snapshot.Draft is { } draft)
            {
                DrawElement(canvas, transform, draft);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private static void DrawElement(SKCanvas canvas, MarkupTransform transform, MarkupElement element)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(element.Color.Red, element.Color.Green, element.Color.Blue),
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = (float)Math.Max(1, transform.SourceStrokeToViewport(element.StrokeWidthSource)),
        };

        switch (element)
        {
            case BrushMarkup brush:
                DrawBrush(canvas, transform, brush, paint);
                break;
            case LineMarkup line:
                canvas.DrawLine(ToPoint(transform.SourceToViewport(line.Start)),
                    ToPoint(transform.SourceToViewport(line.End)), paint);
                break;
            case RectangleMarkup rectangle:
                canvas.DrawRect(NormalizeRect(
                    transform.SourceToViewport(rectangle.Start),
                    transform.SourceToViewport(rectangle.End)), paint);
                break;
            case ArrowMarkup arrow:
                DrawArrow(canvas, transform, arrow, paint);
                break;
        }
    }

    private static void DrawBrush(
        SKCanvas canvas,
        MarkupTransform transform,
        BrushMarkup brush,
        SKPaint paint)
    {
        if (brush.Points.Length == 0)
        {
            return;
        }

        var first = ToPoint(transform.SourceToViewport(brush.Points[0]));
        if (brush.Points.Length == 1)
        {
            canvas.DrawPoint(first, paint);
            return;
        }

        using var path = new SKPath();
        path.MoveTo(first);
        for (var index = 1; index < brush.Points.Length; index++)
        {
            path.LineTo(ToPoint(transform.SourceToViewport(brush.Points[index])));
        }

        canvas.DrawPath(path, paint);
    }

    private static void DrawArrow(
        SKCanvas canvas,
        MarkupTransform transform,
        ArrowMarkup arrow,
        SKPaint paint)
    {
        var start = transform.SourceToViewport(arrow.Start);
        var end = transform.SourceToViewport(arrow.End);
        canvas.DrawLine(ToPoint(start), ToPoint(end), paint);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0.5)
        {
            return;
        }

        var head = Math.Clamp(length * 0.22, paint.StrokeWidth * 3, 28);
        var ux = dx / length;
        var uy = dy / length;
        const double spread = 0.48;
        var left = new PointD(
            end.X - head * (ux * Math.Cos(spread) + uy * Math.Sin(spread)),
            end.Y - head * (uy * Math.Cos(spread) - ux * Math.Sin(spread)));
        var right = new PointD(
            end.X - head * (ux * Math.Cos(spread) - uy * Math.Sin(spread)),
            end.Y - head * (uy * Math.Cos(spread) + ux * Math.Sin(spread)));
        canvas.DrawLine(ToPoint(end), ToPoint(left), paint);
        canvas.DrawLine(ToPoint(end), ToPoint(right), paint);
    }

    private static SKRect NormalizeRect(PointD first, PointD second) => new(
        (float)Math.Min(first.X, second.X),
        (float)Math.Min(first.Y, second.Y),
        (float)Math.Max(first.X, second.X),
        (float)Math.Max(first.Y, second.Y));

    private static SKRect ToRect(RectD rect) => new(
        (float)rect.X,
        (float)rect.Y,
        (float)(rect.X + rect.Width),
        (float)(rect.Y + rect.Height));

    private static SKPoint ToPoint(PointD point) => new((float)point.X, (float)point.Y);
}
