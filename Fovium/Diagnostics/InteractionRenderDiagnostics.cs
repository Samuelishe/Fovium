using System.Diagnostics;

namespace Fovium.Diagnostics;

internal readonly record struct InteractionRenderMetrics(
    long PointerMovedCount,
    long PhotoPresentationRenderCount,
    long PhotoSkiaDrawCount,
    long MarkupOverlayDrawCount,
    long PointerFeedbackDrawCount,
    long FloatingDockDragUpdateCount,
    long ViewerLayoutSizeChangeCount,
    TimeSpan LongestPointerEventInterval);

internal sealed class InteractionRenderDiagnostics
{
    internal const string EnabledEnvironmentVariable = "FOVIUM_INTERACTION_DIAGNOSTICS";
    private static readonly TimeSpan MaximumContinuousPointerInterval =
        TimeSpan.FromMilliseconds(250);

    private readonly bool _enabled;
    private long _pointerMovedCount;
    private long _photoPresentationRenderCount;
    private long _photoSkiaDrawCount;
    private long _markupOverlayDrawCount;
    private long _pointerFeedbackDrawCount;
    private long _floatingDockDragUpdateCount;
    private long _viewerLayoutSizeChangeCount;
    private long _lastPointerTimestamp;
    private long _longestPointerEventIntervalTicks;

    public InteractionRenderDiagnostics(bool enabled = false)
    {
        _enabled = enabled;
    }

    public bool IsEnabled => _enabled;

    public static InteractionRenderDiagnostics CreateFromEnvironment() => new(
        string.Equals(
            Environment.GetEnvironmentVariable(EnabledEnvironmentVariable),
            "1",
            StringComparison.Ordinal));

    public void RecordPointerMoved()
    {
        if (!_enabled)
        {
            return;
        }

        Interlocked.Increment(ref _pointerMovedCount);
        var now = Stopwatch.GetTimestamp();
        var previous = Interlocked.Exchange(ref _lastPointerTimestamp, now);
        var interval = now - previous;
        if (previous != 0 &&
            interval <= MaximumContinuousPointerInterval.TotalSeconds * Stopwatch.Frequency)
        {
            UpdateMaximum(ref _longestPointerEventIntervalTicks, interval);
        }
    }

    public void RecordPhotoPresentationRender() =>
        IncrementIfEnabled(ref _photoPresentationRenderCount);

    public void RecordPhotoSkiaDraw() => IncrementIfEnabled(ref _photoSkiaDrawCount);

    public void RecordMarkupOverlayDraw() => IncrementIfEnabled(ref _markupOverlayDrawCount);

    public void RecordPointerFeedbackDraw() => IncrementIfEnabled(ref _pointerFeedbackDrawCount);

    public void RecordFloatingDockDragUpdate() =>
        IncrementIfEnabled(ref _floatingDockDragUpdateCount);

    public void RecordViewerLayoutSizeChange() =>
        IncrementIfEnabled(ref _viewerLayoutSizeChangeCount);

    public InteractionRenderMetrics GetMetrics() => new(
        Interlocked.Read(ref _pointerMovedCount),
        Interlocked.Read(ref _photoPresentationRenderCount),
        Interlocked.Read(ref _photoSkiaDrawCount),
        Interlocked.Read(ref _markupOverlayDrawCount),
        Interlocked.Read(ref _pointerFeedbackDrawCount),
        Interlocked.Read(ref _floatingDockDragUpdateCount),
        Interlocked.Read(ref _viewerLayoutSizeChangeCount),
        TimeSpan.FromSeconds(
            (double)Interlocked.Read(ref _longestPointerEventIntervalTicks) /
            Stopwatch.Frequency));

    private void IncrementIfEnabled(ref long counter)
    {
        if (_enabled)
        {
            Interlocked.Increment(ref counter);
        }
    }

    private static void UpdateMaximum(ref long target, long candidate)
    {
        var observed = Interlocked.Read(ref target);
        while (candidate > observed)
        {
            var previous = Interlocked.CompareExchange(ref target, candidate, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }
}
