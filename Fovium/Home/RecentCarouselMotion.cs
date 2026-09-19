namespace Fovium.Home;

internal readonly record struct RecentCarouselUpdate(
    double Offset,
    bool StartedDragging,
    bool IsDragging);

internal readonly record struct RecentCarouselOverflow(
    bool IsScrollable,
    bool ShowLeftFade,
    bool ShowRightFade,
    double MaximumOffset)
{
    public static RecentCarouselOverflow Resolve(
        double offset,
        double extent,
        double viewport)
    {
        var maximum = Math.Max(0, extent - viewport);
        if (maximum <= 0.5)
        {
            return new RecentCarouselOverflow(false, false, false, 0);
        }

        var clamped = Math.Clamp(offset, 0, maximum);
        return new RecentCarouselOverflow(
            true,
            clamped > 0.5,
            clamped < maximum - 0.5,
            maximum);
    }
}

internal sealed class RecentCarouselMotion
{
    public const double DragThreshold = 6;
    public const double MaximumCoastDistance = 160;
    private const double HorizontalIntentRatio = 1.15;
    private const double MinimumInertiaVelocity = 120;
    private const double MaximumInertiaVelocity = 1_800;
    private const double FrictionPerSecond = 10;
    private static readonly TimeSpan VelocityWindow = TimeSpan.FromMilliseconds(120);
    private readonly List<PointerSample> _samples = [];
    private GestureState _gesture;
    private double _startX;
    private double _startY;
    private double _startOffset;
    public double Offset { get; private set; }
    private double _velocity;
    private double _coastDistance;
    private bool _suppressNextActivation;

    public bool IsDragging => _gesture == GestureState.Dragging;

    public bool HasInertia => _gesture == GestureState.Inertia;

    public void Begin(double x, double y, double offset, TimeSpan timestamp)
    {
        CancelInertia();
        _gesture = GestureState.Pending;
        _startX = x;
        _startY = y;
        _startOffset = Math.Max(0, offset);
        Offset = _startOffset;
        _samples.Clear();
        _samples.Add(new PointerSample(timestamp, x));
    }

    public RecentCarouselUpdate Move(
        double x,
        double y,
        double maximumOffset,
        TimeSpan timestamp)
    {
        if (_gesture is GestureState.Idle or GestureState.Inertia or GestureState.VerticalCanceled)
        {
            return new RecentCarouselUpdate(Offset, false, false);
        }

        var deltaX = x - _startX;
        var deltaY = y - _startY;
        var started = false;
        if (_gesture == GestureState.Pending)
        {
            var horizontal = Math.Abs(deltaX);
            var vertical = Math.Abs(deltaY);
            if (horizontal < DragThreshold && vertical < DragThreshold)
            {
                return new RecentCarouselUpdate(Offset, false, false);
            }

            if (horizontal < vertical * HorizontalIntentRatio)
            {
                _gesture = GestureState.VerticalCanceled;
                _samples.Clear();
                return new RecentCarouselUpdate(Offset, false, false);
            }

            _gesture = GestureState.Dragging;
            _suppressNextActivation = true;
            started = true;
        }

        Offset = Math.Clamp(_startOffset - deltaX, 0, Math.Max(0, maximumOffset));
        AddSample(timestamp, x);
        return new RecentCarouselUpdate(Offset, started, true);
    }

    public bool End(double maximumOffset, TimeSpan timestamp)
    {
        if (_gesture != GestureState.Dragging)
        {
            _gesture = GestureState.Idle;
            _samples.Clear();
            return false;
        }

        AddSample(timestamp, _samples.Count == 0 ? _startX : _samples[^1].X);
        var first = _samples[0];
        var last = _samples[^1];
        var seconds = (last.Timestamp - first.Timestamp).TotalSeconds;
        var pointerVelocity = seconds > 0 ? (last.X - first.X) / seconds : 0;
        _velocity = Math.Clamp(-pointerVelocity, -MaximumInertiaVelocity, MaximumInertiaVelocity);
        _coastDistance = 0;
        _gesture = Math.Abs(_velocity) >= MinimumInertiaVelocity && maximumOffset > 0.5
            ? GestureState.Inertia
            : GestureState.Idle;
        _samples.Clear();
        return HasInertia;
    }

    public double AdvanceInertia(double elapsedSeconds, double maximumOffset)
    {
        if (!HasInertia || elapsedSeconds <= 0)
        {
            return Offset;
        }

        var decay = Math.Exp(-FrictionPerSecond * elapsedSeconds);
        var nextVelocity = _velocity * decay;
        var delta = (_velocity - nextVelocity) / FrictionPerSecond;
        var remaining = Math.Max(0, MaximumCoastDistance - _coastDistance);
        if (Math.Abs(delta) > remaining)
        {
            delta = Math.CopySign(remaining, delta);
        }

        var maximum = Math.Max(0, maximumOffset);
        var nextOffset = Math.Clamp(Offset + delta, 0, maximum);
        _coastDistance += Math.Abs(nextOffset - Offset);
        var reachedEdge = Math.Abs(nextOffset - (Offset + delta)) > 0.01;
        Offset = nextOffset;
        _velocity = nextVelocity;
        if (reachedEdge ||
            _coastDistance >= MaximumCoastDistance - 0.01 ||
            Math.Abs(_velocity) < 15)
        {
            CancelInertia();
        }

        return Offset;
    }

    public void SynchronizeOffset(double offset, double maximumOffset)
    {
        Offset = Math.Clamp(offset, 0, Math.Max(0, maximumOffset));
        if (HasInertia && maximumOffset <= 0.5)
        {
            CancelInertia();
        }
    }

    public bool ConsumeSuppressedActivation()
    {
        var suppressed = _suppressNextActivation;
        _suppressNextActivation = false;
        return suppressed;
    }

    public void Cancel()
    {
        _gesture = GestureState.Idle;
        _velocity = 0;
        _coastDistance = 0;
        _samples.Clear();
        _suppressNextActivation = false;
    }

    private void CancelInertia()
    {
        if (_gesture == GestureState.Inertia)
        {
            _gesture = GestureState.Idle;
        }

        _velocity = 0;
        _coastDistance = 0;
    }

    private void AddSample(TimeSpan timestamp, double x)
    {
        _samples.Add(new PointerSample(timestamp, x));
        var minimum = timestamp - VelocityWindow;
        while (_samples.Count > 2 && _samples[1].Timestamp < minimum)
        {
            _samples.RemoveAt(0);
        }
    }

    private readonly record struct PointerSample(TimeSpan Timestamp, double X);

    private enum GestureState
    {
        Idle,
        Pending,
        Dragging,
        VerticalCanceled,
        Inertia
    }
}