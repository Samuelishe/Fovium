using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Fovium.Diagnostics;
using Fovium.Presentation;
using Fovium.Rendering;

namespace Fovium.Viewer;

internal sealed class PointerFeedbackOverlayControl : Control
{
    private readonly TranslateTransform _translation = new();
    private InteractionRenderDiagnostics _diagnostics = new();
    private DrawingCursorPresentation _presentation;
    private PointD? _pointer;
    private IBrush? _fill;
    private Pen? _outerPen;
    private Pen? _innerPen;

    public PointerFeedbackOverlayControl()
    {
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        RenderTransform = _translation;
        IsVisible = false;
    }

    internal DrawingCursorPresentation Presentation => _presentation;

    internal PointD? Pointer => _pointer;

    internal void ConfigureDiagnostics(InteractionRenderDiagnostics diagnostics) =>
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

    internal void SetPresentation(DrawingCursorPresentation presentation)
    {
        if (_presentation == presentation)
        {
            return;
        }

        _presentation = presentation;
        RebuildResources();
        UpdateFootprint();
        UpdateTranslationAndVisibility();
        InvalidateVisual();
    }

    internal void SetPointerPosition(PointD? pointer)
    {
        if (_pointer == pointer)
        {
            return;
        }

        _pointer = pointer;
        UpdateTranslationAndVisibility();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!HasSoftwareFeedback())
        {
            return;
        }

        _diagnostics.RecordPointerFeedbackDraw();
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        switch (_presentation.Kind)
        {
            case DrawingCursorKind.Highlight:
            case DrawingCursorKind.Brush:
                {
                    var radius = _presentation.DiameterDip / 2;
                    context.DrawEllipse(_fill, null, center, radius, radius);
                    if (_presentation.Kind == DrawingCursorKind.Brush)
                    {
                        DrawHighContrastCircle(context, center, radius);
                    }

                    break;
                }

            case DrawingCursorKind.Eraser:
                DrawHighContrastCircle(context, center, _presentation.DiameterDip / 2);
                break;

            case DrawingCursorKind.Precision:
                DrawPrecisionCursor(context, center);
                break;
        }
    }

    private bool HasSoftwareFeedback() =>
        _pointer is not null &&
        _presentation.Kind is not (DrawingCursorKind.Viewer or DrawingCursorKind.Hand);

    private void UpdateFootprint()
    {
        var radius = _presentation.Kind == DrawingCursorKind.Precision
            ? _presentation.CrosshairHalfExtentDip
            : _presentation.DiameterDip / 2;
        var padding = Math.Max(_presentation.OutlineWidthDip * 3, 1) + 2;
        var extent = Math.Max(radius + padding, 1);
        Width = extent * 2;
        Height = extent * 2;
    }

    private void UpdateTranslationAndVisibility()
    {
        IsVisible = HasSoftwareFeedback();
        if (_pointer is not { } pointer)
        {
            return;
        }

        _translation.X = pointer.X - (Width / 2);
        _translation.Y = pointer.Y - (Height / 2);
    }

    private void RebuildResources()
    {
        if (_presentation.Kind is DrawingCursorKind.Highlight or DrawingCursorKind.Brush)
        {
            var alpha = (byte)Math.Round(_presentation.Opacity * byte.MaxValue);
            var color = _presentation.Color;
            _fill = new SolidColorBrush(Color.FromArgb(alpha, color.Red, color.Green, color.Blue));
        }
        else
        {
            _fill = null;
        }

        _outerPen = _presentation.OutlineWidthDip > 0
            ? new Pen(Brushes.Black, _presentation.OutlineWidthDip * 3)
            : null;
        _innerPen = _presentation.OutlineWidthDip > 0
            ? new Pen(Brushes.White, _presentation.OutlineWidthDip)
            : null;
    }

    private void DrawHighContrastCircle(DrawingContext context, Point center, double radius)
    {
        context.DrawEllipse(null, _outerPen, center, radius, radius);
        context.DrawEllipse(null, _innerPen, center, radius, radius);
    }

    private void DrawPrecisionCursor(DrawingContext context, Point center)
    {
        var half = _presentation.CrosshairHalfExtentDip;
        var gap = Math.Min(half / 3, 3 * _presentation.OutlineWidthDip);
        var segments = new[]
        {
            (new Point(center.X - half, center.Y), new Point(center.X - gap, center.Y)),
            (new Point(center.X + gap, center.Y), new Point(center.X + half, center.Y)),
            (new Point(center.X, center.Y - half), new Point(center.X, center.Y - gap)),
            (new Point(center.X, center.Y + gap), new Point(center.X, center.Y + half)),
        };
        foreach (var (start, end) in segments)
        {
            if (_outerPen is { } outerPen)
            {
                context.DrawLine(outerPen, start, end);
            }

            if (_innerPen is { } innerPen)
            {
                context.DrawLine(innerPen, start, end);
            }
        }
    }
}
