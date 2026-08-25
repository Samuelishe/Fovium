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
    AmbientRenderedFrame LastFrame);

internal sealed class AmbientRenderFrameDiagnostics
{
    private long _blackFallbackRenderedFrameCount;
    private long _matchingAmbientRenderedFrameCount;
    private long _lastTimestamp;
    private long _lastImageIdentity;
    private long _lastAmbientIdentity;
    private int _lastBackgroundMode;
    private int _lastUsedBlackFallback;

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
            new AmbientRenderedFrame(
                Interlocked.Read(ref _lastTimestamp),
                Interlocked.Read(ref _lastImageIdentity),
                (StageBackgroundMode)Volatile.Read(ref _lastBackgroundMode),
                ambientIdentity == 0 ? null : ambientIdentity,
                Volatile.Read(ref _lastUsedBlackFallback) != 0));
    }
}
