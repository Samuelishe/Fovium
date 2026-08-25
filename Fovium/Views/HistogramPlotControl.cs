using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Fovium.Histogram;

namespace Fovium.Views;

internal sealed class HistogramPlotControl : Control
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(18, 18, 18));
    private static readonly IBrush LoadingBrush = new SolidColorBrush(Color.FromArgb(90, 210, 210, 210));
    private static readonly ChannelStyle RedStyle = new(
        new SolidColorBrush(Color.FromArgb(82, 230, 78, 78)),
        new Pen(new SolidColorBrush(Color.FromArgb(185, 240, 98, 98)), 1));
    private static readonly ChannelStyle GreenStyle = new(
        new SolidColorBrush(Color.FromArgb(74, 78, 205, 112)),
        new Pen(new SolidColorBrush(Color.FromArgb(180, 100, 220, 132)), 1));
    private static readonly ChannelStyle BlueStyle = new(
        new SolidColorBrush(Color.FromArgb(82, 75, 125, 235)),
        new Pen(new SolidColorBrush(Color.FromArgb(190, 100, 150, 245)), 1));

    private HistogramPlotModel? _model;
    private bool _isLoading;

    public void SetState(HistogramData? data, bool isLoading)
    {
        _model = data is null ? null : HistogramPlotModel.Create(data);
        _isLoading = isLoading;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        context.DrawRectangle(BackgroundBrush, null, bounds, 4, 4);
        if (_model is { CommonMaximum: > 0 } model)
        {
            DrawChannel(context, bounds, model.Red, RedStyle);
            DrawChannel(context, bounds, model.Green, GreenStyle);
            DrawChannel(context, bounds, model.Blue, BlueStyle);
        }
        else if (_isLoading)
        {
            var width = Math.Min(bounds.Width * 0.24, 56);
            context.DrawLine(
                new Pen(LoadingBrush, 2),
                new Point((bounds.Width - width) / 2, bounds.Height / 2),
                new Point((bounds.Width + width) / 2, bounds.Height / 2));
        }
    }

    private static void DrawChannel(
        DrawingContext context,
        Rect bounds,
        IReadOnlyList<HistogramPlotPoint> points,
        ChannelStyle style)
    {
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new Point(0, bounds.Height), true);
            foreach (var point in points)
            {
                stream.LineTo(new Point(point.X * bounds.Width, bounds.Height - (point.Y * bounds.Height)));
            }

            stream.LineTo(new Point(bounds.Width, bounds.Height));
            stream.EndFigure(true);
        }

        context.DrawGeometry(style.Fill, style.Outline, geometry);
    }

    private sealed record ChannelStyle(IBrush Fill, Pen Outline);
}
