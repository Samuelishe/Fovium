using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Fovium.Home;

namespace Fovium.Views;

internal sealed class HomeRecentCarousel : IDisposable
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);
    private readonly ScrollViewer _scroller;
    private readonly Control _leftFade;
    private readonly Control _rightFade;
    private readonly RecentCarouselMotion _motion = new();
    private readonly DispatcherTimer _inertiaTimer;
    private readonly Cursor _grabCursor = new(StandardCursorType.DragMove);
    private readonly Cursor _grabbingCursor = new(StandardCursorType.SizeAll);
    private IPointer? _pointer;
    private long _lastFrameTimestamp;
    private bool _disposed;

    public event Action<double, double>? ViewportChanged;

    public HomeRecentCarousel(
        ScrollViewer scroller,
        Control leftFade,
        Control rightFade)
    {
        _scroller = scroller ?? throw new ArgumentNullException(nameof(scroller));
        _leftFade = leftFade ?? throw new ArgumentNullException(nameof(leftFade));
        _rightFade = rightFade ?? throw new ArgumentNullException(nameof(rightFade));
        _inertiaTimer = new DispatcherTimer { Interval = FrameInterval };
        _inertiaTimer.Tick += OnInertiaTick;
        _scroller.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _scroller.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _scroller.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _scroller.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnPointerWheelChanged,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _scroller.PointerCaptureLost += OnPointerCaptureLost;
        _scroller.ScrollChanged += OnScrollChanged;
        _scroller.SizeChanged += OnSizeChanged;
    }

    public bool ConsumeSuppressedActivation() => _motion.ConsumeSuppressedActivation();

    public void Refresh()
    {
        Stop();
        Dispatcher.UIThread.Post(UpdateOverflow);
    }

    public void Stop()
    {
        _pointer?.Capture(null);
        _pointer = null;
        _inertiaTimer.Stop();
        _motion.Cancel();
        _scroller.Cursor = null;
        UpdateOverflow();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _inertiaTimer.Tick -= OnInertiaTick;
        _scroller.PointerCaptureLost -= OnPointerCaptureLost;
        _scroller.ScrollChanged -= OnScrollChanged;
        _scroller.SizeChanged -= OnSizeChanged;
        _grabCursor.Dispose();
        _grabbingCursor.Dispose();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pointer is not null ||
            !e.GetCurrentPoint(_scroller).Properties.IsLeftButtonPressed ||
            IsNestedAction(e.Source as Visual))
        {
            return;
        }

        _inertiaTimer.Stop();
        var point = e.GetPosition(_scroller);
        _motion.Begin(point.X, point.Y, _scroller.Offset.X, Now());
        _pointer = e.Pointer;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pointer != e.Pointer)
        {
            return;
        }

        if (!e.GetCurrentPoint(_scroller).Properties.IsLeftButtonPressed)
        {
            CancelPointerGesture();
            return;
        }

        var point = e.GetPosition(_scroller);
        var update = _motion.Move(point.X, point.Y, MaximumOffset, Now());
        if (update.StartedDragging)
        {
            e.Pointer.Capture(_scroller);
        }

        if (!update.IsDragging)
        {
            return;
        }

        SetOffset(update.Offset);
        _scroller.Cursor = _grabbingCursor;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pointer != e.Pointer)
        {
            return;
        }

        var wasDragging = _motion.IsDragging;
        _pointer = null;
        e.Pointer.Capture(null);
        if (_motion.End(MaximumOffset, Now()))
        {
            _lastFrameTimestamp = Stopwatch.GetTimestamp();
            _inertiaTimer.Start();
        }

        UpdateOverflow();
        if (wasDragging)
        {
            e.Handled = true;
            Dispatcher.UIThread.Post(() => _motion.ConsumeSuppressedActivation());
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = Math.Abs(e.Delta.X) > 0.01
            ? e.Delta.X
            : e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                ? e.Delta.Y
                : 0;
        if (Math.Abs(delta) <= 0.01 || MaximumOffset <= 0.5)
        {
            return;
        }

        _inertiaTimer.Stop();
        _motion.Cancel();
        SetOffset(Math.Clamp(_scroller.Offset.X - delta * 48, 0, MaximumOffset));
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_pointer == e.Pointer)
        {
            CancelPointerGesture();
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_motion.IsDragging && !_motion.HasInertia)
        {
            _motion.SynchronizeOffset(_scroller.Offset.X, MaximumOffset);
        }

        UpdateOverflow();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _inertiaTimer.Stop();
        _motion.SynchronizeOffset(_scroller.Offset.X, MaximumOffset);
        UpdateOverflow();
    }

    private void OnInertiaTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastFrameTimestamp, now).TotalSeconds;
        _lastFrameTimestamp = now;
        SetOffset(_motion.AdvanceInertia(elapsed, MaximumOffset));
        if (!_motion.HasInertia)
        {
            _inertiaTimer.Stop();
            UpdateOverflow();
        }
    }

    private void CancelPointerGesture()
    {
        _pointer?.Capture(null);
        _pointer = null;
        _motion.Cancel();
        UpdateOverflow();
    }

    private void SetOffset(double offset)
    {
        _scroller.Offset = new Vector(offset, _scroller.Offset.Y);
        UpdateOverflow();
    }

    private void UpdateOverflow()
    {
        var overflow = RecentCarouselOverflow.Resolve(
            _scroller.Offset.X,
            _scroller.Extent.Width,
            _scroller.Viewport.Width);
        _leftFade.IsVisible = overflow.ShowLeftFade;
        _rightFade.IsVisible = overflow.ShowRightFade;
        _scroller.Cursor = overflow.IsScrollable
            ? _motion.IsDragging ? _grabbingCursor : _grabCursor
            : null;
        ViewportChanged?.Invoke(_scroller.Offset.X, _scroller.Viewport.Width);
    }

    private double MaximumOffset => Math.Max(0, _scroller.Extent.Width - _scroller.Viewport.Width);

    private bool IsNestedAction(Visual? origin)
    {
        for (var current = origin; current is not null; current = current.GetVisualParent())
        {
            if (current is Control control && control.Classes.Contains("recent-item-action"))
            {
                return true;
            }

            if (ReferenceEquals(current, _scroller))
            {
                break;
            }
        }

        return false;
    }

    private static TimeSpan Now() => TimeSpan.FromSeconds(
        (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
}