using System.Diagnostics;
using System.Globalization;
using System.Text;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Stage;
using Fovium.Viewer;

namespace Fovium.Diagnostics;

internal sealed class AmbientSoakTrace : IDisposable
{
    internal const string TracePathEnvironmentVariable = "FOVIUM_AMBIENT_SOAK_TRACE";

    private sealed record PendingTransition(
        int Ordinal,
        int SequenceIndex,
        bool FromCache,
        TimeSpan PublicationLatency,
        int Width,
        int Height,
        long EstimatedRetainedBytes,
        bool InitialMatchingAmbient,
        AmbientRenderFrameMetrics InitialFrames);

    private readonly StreamWriter? _writer;
    private PendingTransition? _pending;
    private int _nextOrdinal;

    private AmbientSoakTrace(StreamWriter? writer)
    {
        _writer = writer;
        if (_writer is null)
        {
            return;
        }

        _writer.WriteLine(
            "ordinal,sequenceIndex,width,height,estimatedRetainedBytes,fromCache,publicationMs," +
            "initialMatchingAmbient,finalMatchingAmbient,cacheRetainedBytes,cacheBudgetBytes," +
            "cacheRemainingBytes,cacheItemCount,cacheEvictions,cacheRejectedAdds," +
            "foregroundLoadAttempts,foregroundLoadSuccesses,speculativeRequests," +
            "speculativeLoadAttempts,speculativeLoadSuccesses,speculativeCacheHits," +
            "speculativeResourceLimits,speculativeCancellations,speculativeCacheAdds," +
            "speculativeCacheAddRejections,lastSpeculativeIndex,lastSpeculativeOutcome," +
            "currentAmbientCacheHits,currentAmbientPrepares,adjacentAmbientPrepares," +
            "lastPhotoToAmbientMs,lastCurrentAmbientWasCacheHit,blackFallbackFrameDelta," +
            "matchingAmbientFrameDelta,viewportRenderDelta,customDrawScheduledDelta," +
            "customDrawEnteredDelta,skiaLeaseAcquiredDelta,skiaLeaseUnavailableDelta," +
            "workingSetBytes,privateBytes");
        _writer.Flush();
    }

    public bool IsEnabled => _writer is not null;

    public static AmbientSoakTrace CreateFromEnvironment()
    {
        var path = Environment.GetEnvironmentVariable(TracePathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return new AmbientSoakTrace(null);
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new AmbientSoakTrace(new StreamWriter(fullPath, append: false, new UTF8Encoding(false)));
    }

    public void BeginTransition(
        SelectionResult<DecodedImage> result,
        DecodedImage image,
        bool initialMatchingAmbient,
        AmbientRenderFrameMetrics initialFrames)
    {
        if (_writer is null || result.Index is null)
        {
            return;
        }

        var descriptor = image.Descriptor;
        _pending = new PendingTransition(
            ++_nextOrdinal,
            result.Index.Value,
            result.FromCache,
            result.PublicationLatency,
            descriptor.OrientedSize.Width,
            descriptor.OrientedSize.Height,
            descriptor.EstimatedRetainedBytes,
            initialMatchingAmbient,
            initialFrames);
    }

    public void CompleteCurrent(
        ViewerSessionMetrics session,
        AmbientStageMetrics ambient,
        AmbientRenderFrameMetrics frames,
        ViewportAmbientPresentationState viewport)
    {
        if (_writer is null || Interlocked.Exchange(ref _pending, null) is not { } pending)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var values = new object?[]
        {
            pending.Ordinal,
            pending.SequenceIndex,
            pending.Width,
            pending.Height,
            pending.EstimatedRetainedBytes,
            pending.FromCache,
            pending.PublicationLatency.TotalMilliseconds,
            pending.InitialMatchingAmbient,
            viewport.HasMatchingAmbient,
            session.CacheRetainedBytes,
            session.CacheBudgetBytes,
            session.CacheRemainingBytes,
            session.CacheItemCount,
            session.CacheEvictions,
            session.CacheRejectedAdds,
            session.ForegroundLoadAttempts,
            session.ForegroundLoadSuccesses,
            session.SpeculativeRequests,
            session.SpeculativeLoadAttempts,
            session.SpeculativeLoadSuccesses,
            session.SpeculativeCacheHits,
            session.SpeculativeResourceLimitRejections,
            session.SpeculativeCancellations,
            session.SpeculativeCacheAdds,
            session.SpeculativeCacheAddRejections,
            session.LastSpeculativeCandidateIndex,
            session.LastSpeculativeOutcome,
            ambient.CurrentAmbientCacheHitCount,
            ambient.CurrentAmbientPrepareCount,
            ambient.AdjacentAmbientPreparedCount,
            ambient.LastPhotoToAmbientPresentationGap?.TotalMilliseconds,
            ambient.LastCurrentAmbientWasCacheHit,
            frames.BlackFallbackRenderedFrameCount - pending.InitialFrames.BlackFallbackRenderedFrameCount,
            frames.MatchingAmbientRenderedFrameCount - pending.InitialFrames.MatchingAmbientRenderedFrameCount,
            frames.ViewportRenderCount - pending.InitialFrames.ViewportRenderCount,
            frames.CustomDrawScheduledCount - pending.InitialFrames.CustomDrawScheduledCount,
            frames.CustomDrawEnteredCount - pending.InitialFrames.CustomDrawEnteredCount,
            frames.SkiaLeaseAcquiredCount - pending.InitialFrames.SkiaLeaseAcquiredCount,
            frames.SkiaLeaseUnavailableCount - pending.InitialFrames.SkiaLeaseUnavailableCount,
            process.WorkingSet64,
            process.PrivateMemorySize64,
        };
        _writer.WriteLine(string.Join(',', values.Select(Format)));
        _writer.Flush();
    }

    public void Dispose()
    {
        _pending = null;
        _writer?.Dispose();
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        bool boolean => boolean ? "true" : "false",
        double number => number.ToString("0.###", CultureInfo.InvariantCulture),
        float number => number.ToString("0.###", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
