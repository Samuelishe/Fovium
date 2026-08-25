using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Presentation;

internal static class SkiaMarkupOverlayRenderer
{
    public static void Draw(
        SKCanvas canvas,
        RectD destination,
        PixelSize orientedSourceSize,
        MarkupRenderSnapshot snapshot,
        RectD? viewportBounds = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (snapshot.IsEmpty || destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        var visibleBounds = Intersect(destination, viewportBounds ?? destination);
        if (visibleBounds.Width <= 0 || visibleBounds.Height <= 0)
        {
            return;
        }

        var transform = new MarkupTransform(destination, orientedSourceSize);
        var destinationRect = ToRect(destination);
        var layerRect = ToRect(visibleBounds);
        canvas.Save();
        try
        {
            canvas.ClipRect(destinationRect);
            canvas.ClipRect(layerRect);
            canvas.SaveLayer(layerRect, null);
            try
            {
                foreach (var operation in snapshot.Operations)
                {
                    ReplayOperation(canvas, transform, layerRect, operation);
                }

                if (snapshot.Draft is { } draft)
                {
                    ReplayOperation(canvas, transform, layerRect, draft);
                }
            }
            finally
            {
                canvas.Restore();
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private static void ReplayOperation(
        SKCanvas canvas,
        MarkupTransform transform,
        SKRect layerBounds,
        MarkupOperation operation)
    {
        switch (operation)
        {
            case DrawMarkupOperation draw:
                DrawElement(canvas, transform, draw.Element);
                break;
            case EraseMarkupOperation erase:
                EraseStroke(canvas, transform, erase);
                break;
            case ClearMarkupOperation:
                using (var clearPaint = new SKPaint { BlendMode = SKBlendMode.Clear })
                {
                    canvas.DrawRect(layerBounds, clearPaint);
                }

                break;
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
                DrawStroke(canvas, transform, brush.Points, paint);
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

    private static void EraseStroke(
        SKCanvas canvas,
        MarkupTransform transform,
        EraseMarkupOperation erase)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            BlendMode = SKBlendMode.Clear,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = (float)Math.Max(1, transform.SourceStrokeToViewport(erase.StrokeWidthSource)),
        };
        DrawStroke(canvas, transform, erase.Points, paint);
    }

    private static void DrawStroke(
        SKCanvas canvas,
        MarkupTransform transform,
        IReadOnlyList<PointD> points,
        SKPaint paint)
    {
        if (points.Count == 0)
        {
            return;
        }

        var first = ToPoint(transform.SourceToViewport(points[0]));
        if (points.Count == 1)
        {
            canvas.DrawPoint(first, paint);
            return;
        }

        using var path = new SKPath();
        path.MoveTo(first);
        for (var index = 1; index < points.Count; index++)
        {
            path.LineTo(ToPoint(transform.SourceToViewport(points[index])));
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

        var maximumHead = Math.Min(28, length * 0.6);
        var minimumHead = Math.Min(maximumHead, paint.StrokeWidth * 3);
        var head = Math.Clamp(length * 0.22, minimumHead, maximumHead);
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

    private static RectD Intersect(RectD first, RectD second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);
        return new RectD(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
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
