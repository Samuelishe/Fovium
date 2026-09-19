using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Fovium.ColorPicking;
using PickerHsvColor = Fovium.ColorPicking.HsvColor;

namespace Fovium.Views;

internal sealed class ColorValueStripControl : Control
{
    private static readonly Pen MarkerOuterPen = new(Brushes.Black, 5);
    private static readonly Pen MarkerInnerPen = new(Brushes.White, 2);
    private static readonly Pen FocusPen = new(new SolidColorBrush(Color.FromRgb(171, 126, 194)), 2);
    private IBrush _valueBrush = Brushes.Black;
    private bool _dragging;

    public ColorValueStripControl()
    {
        Focusable = true;
        UpdateBrush();
    }

    public event EventHandler<double>? ValueChanged;

    public double HueDegrees { get; private set; }

    public double Saturation { get; private set; }

    public double Value { get; private set; } = 1;

    public void SetSelection(PickerHsvColor source)
    {
        var hsv = source.Normalize();
        var brushChanged = Math.Abs(HueDegrees - hsv.HueDegrees) > 0.0001 ||
                           Math.Abs(Saturation - hsv.Saturation) > 0.0001;
        HueDegrees = hsv.HueDegrees;
        Saturation = hsv.Saturation;
        Value = hsv.Value;
        if (brushChanged)
        {
            UpdateBrush();
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var rect = new Rect(3, 3, Math.Max(0, Bounds.Width - 6), Math.Max(0, Bounds.Height - 6));
        context.DrawRectangle(_valueBrush, null, rect, 6, 6);
        var markerY = 3 + ColorWheelGeometry.MapValueToStripY(rect.Height, Value);
        context.DrawLine(MarkerOuterPen, new Point(0, markerY), new Point(Bounds.Width, markerY));
        context.DrawLine(MarkerInnerPen, new Point(0, markerY), new Point(Bounds.Width, markerY));
        if (IsKeyboardFocusWithin)
        {
            context.DrawRectangle(null, FocusPen, rect, 6, 6);
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
        UpdateFromPointer(e.GetPosition(this).Y);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        UpdateFromPointer(e.GetPosition(this).Y);
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
        var next = e.Key switch
        {
            Key.Up => Value + 0.01,
            Key.Down => Value - 0.01,
            Key.PageUp => Value + 0.1,
            Key.PageDown => Value - 0.1,
            _ => double.NaN,
        };
        if (double.IsNaN(next))
        {
            return;
        }

        Publish(Math.Clamp(next, 0, 1));
        e.Handled = true;
    }

    private void UpdateBrush()
    {
        var top = ColorSelectionModel.FromHsv(new PickerHsvColor(HueDegrees, Saturation, 1));
        _valueBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromRgb(top.Red, top.Green, top.Blue), 0),
                new GradientStop(Colors.Black, 1),
            ],
        };
    }

    private void UpdateFromPointer(double y) =>
        Publish(ColorWheelGeometry.MapValueStripY(Math.Max(0, Bounds.Height - 6), y - 3));

    private void Publish(double value)
    {
        Value = value;
        InvalidateVisual();
        ValueChanged?.Invoke(this, value);
    }
}