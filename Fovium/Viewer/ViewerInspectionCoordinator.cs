using System.Diagnostics;
using Fovium.Imaging;
using Fovium.Input;
using Fovium.Loading;
using Fovium.Navigation;
using Fovium.Settings;
using Fovium.Stage;

namespace Fovium.Viewer;

internal readonly record struct ViewerInspectionMetrics(
    TimeSpan LastPeekBeginLatency,
    TimeSpan LastCachedBlinkLatency,
    TimeSpan LastNonCachedBlinkLatency,
    TimeSpan LastReleaseLatency);

internal sealed class ViewerInspectionCoordinator(
    PhotoViewportControl viewport,
    ViewerSession<DecodedImage> session,
    SettingsService settings) : IViewerHoldAction
{
    private readonly object _sync = new();
    private CancellationTokenSource? _workCancellation;
    private long _authority;
    private long _lastPeekBeginTicks;
    private long _lastCachedBlinkTicks;
    private long _lastNonCachedBlinkTicks;
    private long _lastReleaseTicks;

    public InspectionMode Mode { get; private set; }

    public ViewerInspectionMetrics GetMetrics() => new(
        TimeSpan.FromTicks(Interlocked.Read(ref _lastPeekBeginTicks)),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastCachedBlinkTicks)),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastNonCachedBlinkTicks)),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastReleaseTicks)));

    public async Task BeginAsync(ViewerCommand command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case ViewerCommand.Peek100:
                BeginPeek();
                return;
            case ViewerCommand.BlinkCompare:
                await BeginBlinkAsync(cancellationToken);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    public void End()
    {
        lock (_sync)
        {
            if (Mode == InspectionMode.None)
            {
                return;
            }

            Mode = InspectionMode.None;
            _authority++;
            _workCancellation?.Cancel();
            _workCancellation?.Dispose();
            _workCancellation = null;
        }

        var started = Stopwatch.GetTimestamp();
        viewport.EndInspection();
        var elapsed = Stopwatch.GetElapsedTime(started);
        Interlocked.Exchange(ref _lastReleaseTicks, elapsed.Ticks);
        Debug.WriteLine($"Fovium inspection release restored current presentation in {elapsed.TotalMilliseconds:F2} ms.");
    }

    private void BeginPeek()
    {
        lock (_sync)
        {
            if (Mode != InspectionMode.None)
            {
                return;
            }

            Mode = InspectionMode.Peek100;
            _authority++;
        }

        var started = Stopwatch.GetTimestamp();
        viewport.BeginPeek100();
        var elapsed = Stopwatch.GetElapsedTime(started);
        Interlocked.Exchange(ref _lastPeekBeginTicks, elapsed.Ticks);
        Debug.WriteLine($"Fovium Peek 100% presentation changed in {elapsed.TotalMilliseconds:F2} ms.");
    }

    private async Task BeginBlinkAsync(CancellationToken cancellationToken)
    {
        long authority;
        StageSettings stage;
        CancellationToken token;
        lock (_sync)
        {
            if (Mode != InspectionMode.None)
            {
                return;
            }

            Mode = InspectionMode.BlinkCompare;
            authority = ++_authority;
            stage = settings.Current.Stage;
            _workCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            token = _workCancellation.Token;
        }

        if (!viewport.BeginBlinkCompare())
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var result = await session.AcquireNeighborForInspectionAsync(
            NavigationDirection.Previous,
            token);
        var image = result.Image;
        if (result.Status != InspectionAcquisitionStatus.Acquired || image is null)
        {
            image?.Dispose();
            return;
        }

        try
        {
            lock (_sync)
            {
                if (Mode != InspectionMode.BlinkCompare ||
                    authority != _authority ||
                    token.IsCancellationRequested)
                {
                    return;
                }
            }

            using var presentation = TemporaryStagePresentation.Create(stage, image.Value);
            if (!viewport.ShowBlinkComparison(image, presentation.Stage, presentation.TakeAmbient()))
            {
                return;
            }

            image = null;
            var pressToPresentation = Stopwatch.GetElapsedTime(started);
            var ticks = pressToPresentation.Ticks;
            if (result.FromCache)
            {
                Interlocked.Exchange(ref _lastCachedBlinkTicks, ticks);
            }
            else
            {
                Interlocked.Exchange(ref _lastNonCachedBlinkTicks, ticks);
            }
            Debug.WriteLine(
                $"Fovium Blink {(result.FromCache ? "cached" : "decoded")} presentation queued in " +
                $"{pressToPresentation.TotalMilliseconds:F2} ms " +
                $"(session acquisition {result.AcquisitionLatency.TotalMilliseconds:F2} ms).");
        }
        finally
        {
            image?.Dispose();
        }
    }
}
