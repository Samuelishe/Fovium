using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Fovium.ColorPicking;
using PickerHsvColor = Fovium.ColorPicking.HsvColor;

namespace Fovium.Views;

internal sealed class ColorWheelControl : Control
{
    private static readonly IBrush HueBrush = new ConicGradientBrush
    {
        Center = RelativePoint.Center,
        Angle = 90,
        GradientStops =
        [
            new GradientStop(Color.FromRgb(255, 0, 0), 0),
            new GradientStop(Color.FromRgb(255, 255, 0), 1d / 6),
            new GradientStop(Color.FromRgb(0, 255, 0), 2d / 6),
            new GradientStop(Color.FromRgb(0, 255, 255), 3d / 6),
            new GradientStop(Color.FromRgb(0, 0, 255), 4d / 6),
            new GradientStop(Color.FromRgb(255, 0, 255), 5d / 6),
            new GradientStop(Color.FromRgb(255, 0, 0), 1),
        ],
    };

    private static readonly IBrush SaturationBrush = new RadialGradientBrush
    {
        Center = RelativePoint.Center,
        GradientOrigin = RelativePoint.Center,
        GradientStops =
        [
            new GradientStop(Colors.White, 0),
            new GradientStop(Color.FromArgb(0, 255, 255, 255), 1),
        ],
    };

    private static readonly Pen MarkerOuterPen = new(Brushes.Black, 4);
    private static readonly Pen MarkerInnerPen = new(Brushes.White, 2);
    private static readonly Pen FocusPen = new(new SolidColorBrush(Color.FromRgb(171, 126, 194)), 2);
    private bool _dragging;

    public ColorWheelControl()
    {
        Focusable = true;
    }

    public event EventHandler<PickerHsvColor>? SelectionChanged;

    public double HueDegrees { get; private set; }

    public double Saturation { get; private set; }

    public double Value { get; private set; } = 1;

    public void SetSelection(PickerHsvColor source)
    {
        var hsv = source.Normalize();
        HueDegrees = hsv.HueDegrees;
        Saturation = hsv.Saturation;
        Value = hsv.Value;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 3);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        context.DrawEllipse(HueBrush, null, center, radius, radius);
        context.DrawEllipse(SaturationBrush, null, center, radius, radius);

        var marker = ColorWheelGeometry.MapHueSaturationToPoint(
            radius * 2,
            radius * 2,
            HueDegrees,
            Saturation);
        var markerCenter = new Point(marker.X + center.X - radius, marker.Y + center.Y - radius);
        context.DrawEllipse(null, MarkerOuterPen, markerCenter, 7, 7);
        context.DrawEllipse(null, MarkerInnerPen, markerCenter, 7, 7);
        if (IsKeyboardFocusWithin)
        {
            context.DrawEllipse(null, FocusPen, center, radius + 2, radius + 2);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        _dragging = true;
        e.Pointer.Capture(this);
        UpdateFromPointer(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        UpdateFromPointer(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var hue = HueDegrees;
        var saturation = Saturation;
        switch (e.Key)
        {
            case Key.Left:
                hue -= 1;
                break;
            case Key.Right:
                hue += 1;
                break;
            case Key.Up:
                saturation += 0.01;
                break;
            case Key.Down:
                saturation -= 0.01;
                break;
            default:
                return;
        }

        Publish(new PickerHsvColor(hue, saturation, Value).Normalize());
        e.Handled = true;
    }

    private void UpdateFromPointer(Point point)
    {
        var radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 3);
        var localX = point.X - ((Bounds.Width - (radius * 2)) / 2);
        var localY = point.Y - ((Bounds.Height - (radius * 2)) / 2);
        Publish(ColorWheelGeometry.MapPointToHueSaturation(
            radius * 2,
            radius * 2,
            localX,
            localY,
            Value));
    }

    private void Publish(PickerHsvColor hsv)
    {
        SetSelection(hsv);
        SelectionChanged?.Invoke(this, hsv);
    }
}