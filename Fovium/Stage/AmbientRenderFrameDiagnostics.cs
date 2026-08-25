using System.Diagnostics;

namespace Fovium.Stage;

internal readonly record struct AmbientRenderedFrame(
    long Timestamp,
    long ImageIdentity,
    StageBackgroundMode BackgroundMode,
    long? AmbientIdentity,
    bool UsedBlackFallback);

internal readonly record struct AmbientRenderFrameMetrics(
    long BlackFallbackRenderedFrameCount,
    long MatchingAmbientRenderedFrameCount,
    long ViewportRenderCount,
    long CustomDrawScheduledCount,
    long CustomDrawEnteredCount,
    long SkiaLeaseAcquiredCount,
    long SkiaLeaseUnavailableCount,
    AmbientRenderedFrame LastFrame);

internal sealed class AmbientRenderFrameDiagnostics
{
    private long _blackFallbackRenderedFrameCount;
    private long _matchingAmbientRenderedFrameCount;
    private long _viewportRenderCount;
    private long _customDrawScheduledCount;
    private long _customDrawEnteredCount;
    private long _skiaLeaseAcquiredCount;
    private long _skiaLeaseUnavailableCount;
    private long _lastTimestamp;
    private long _lastImageIdentity;
    private long _lastAmbientIdentity;
    private int _lastBackgroundMode;
    private int _lastUsedBlackFallback;
    private int _pipelineTrackingEnabled;

    public void EnablePipelineTracking() => Volatile.Write(ref _pipelineTrackingEnabled, 1);

    public void RecordViewportRender() => IncrementIfPipelineTrackingEnabled(ref _viewportRenderCount);

    public void RecordCustomDrawScheduled() => IncrementIfPipelineTrackingEnabled(ref _customDrawScheduledCount);

    public void RecordCustomDrawEntered() => IncrementIfPipelineTrackingEnabled(ref _customDrawEnteredCount);

    public void RecordSkiaLeaseAcquired() => IncrementIfPipelineTrackingEnabled(ref _skiaLeaseAcquiredCount);

    public void RecordSkiaLeaseUnavailable() => IncrementIfPipelineTrackingEnabled(ref _skiaLeaseUnavailableCount);

    public void Record(
        long imageIdentity,
        StageBackgroundMode backgroundMode,
        long? ambientIdentity,
        bool ambientPresent)
    {
        if (!backgroundMode.RequiresAmbient())
        {
            return;
        }

        var matchingAmbient = ambientPresent && ambientIdentity == imageIdentity;
        if (matchingAmbient)
        {
            Interlocked.Increment(ref _matchingAmbientRenderedFrameCount);
        }
        else
        {
            Interlocked.Increment(ref _blackFallbackRenderedFrameCount);
        }

        Interlocked.Exchange(ref _lastTimestamp, Stopwatch.GetTimestamp());
        Interlocked.Exchange(ref _lastImageIdentity, imageIdentity);
        Interlocked.Exchange(ref _lastAmbientIdentity, ambientIdentity ?? 0);
        Volatile.Write(ref _lastBackgroundMode, (int)backgroundMode);
        Volatile.Write(ref _lastUsedBlackFallback, matchingAmbient ? 0 : 1);
    }

    public AmbientRenderFrameMetrics GetMetrics()
    {
        var ambientIdentity = Interlocked.Read(ref _lastAmbientIdentity);
        return new AmbientRenderFrameMetrics(
            Interlocked.Read(ref _blackFallbackRenderedFrameCount),
            Interlocked.Read(ref _matchingAmbientRenderedFrameCount),
            Interlocked.Read(ref _viewportRenderCount),
            Interlocked.Read(ref _customDrawScheduledCount),
            Interlocked.Read(ref _customDrawEnteredCount),
            Interlocked.Read(ref _skiaLeaseAcquiredCount),
            Interlocked.Read(ref _skiaLeaseUnavailableCount),
            new AmbientRenderedFrame(
                Interlocked.Read(ref _lastTimestamp),
                Interlocked.Read(ref _lastImageIdentity),
                (StageBackgroundMode)Volatile.Read(ref _lastBackgroundMode),
                ambientIdentity == 0 ? null : ambientIdentity,
                Volatile.Read(ref _lastUsedBlackFallback) != 0));
    }

    private void IncrementIfPipelineTrackingEnabled(ref long counter)
    {
        if (Volatile.Read(ref _pipelineTrackingEnabled) != 0)
        {
            Interlocked.Increment(ref counter);
        }
    }
}
