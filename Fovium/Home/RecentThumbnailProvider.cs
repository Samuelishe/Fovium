using System.Diagnostics;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Home;

internal enum RecentThumbnailStatus
{
    Ready,
    Unavailable,
    Unsupported,
    Failed
}

internal sealed record RecentThumbnailResult(
    RecentThumbnailStatus Status,
    byte[]? PngBytes,
    PixelSize Size,
    bool FromMemoryCache,
    TimeSpan Duration);

internal readonly record struct RecentThumbnailMetrics(
    long Requests,
    long Prepared,
    long MemoryCacheHits,
    long Failures,
    long Cancellations,
    long RetainedBytes,
    int CachedItems,
    TimeSpan LastDuration,
    TimeSpan TotalPreparationDuration);

internal sealed class RecentThumbnailProvider : IDisposable
{
    public const int TargetLongEdge = 160;
    public const int MaximumCachedItems = 6;
    public const long MaximumCachedBytes = 2 * 1024 * 1024;
    private const int MaximumIntermediateLongEdge = 2_048;
    private const long MaximumIntermediateBytes = 16 * 1024 * 1024;
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _decodeGate = new(2, 2);

    private readonly Dictionary<string, CacheEntry> _cache = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private long _accessSequence;
    private long _requests;
    private long _prepared;
    private long _memoryCacheHits;
    private long _failures;
    private long _cancellations;
    private long _lastDurationTicks;
    private long _totalPreparationTicks;
    private bool _disposed;

    public async Task<RecentThumbnailResult> PrepareAsync(
        string path,
        int targetLongEdge = TargetLongEdge,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetLongEdge);
        ObjectDisposedException.ThrowIf(_disposed, this);
        Interlocked.Increment(ref _requests);
        try
        {
            await _decodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return await Task.Run(
                        () => PrepareCore(path, targetLongEdge, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _decodeGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref _cancellations);
            throw;
        }
    }

    public RecentThumbnailMetrics GetMetrics()
    {
        lock (_sync)
        {
            return new RecentThumbnailMetrics(
                Interlocked.Read(ref _requests),
                Interlocked.Read(ref _prepared),
                Interlocked.Read(ref _memoryCacheHits),
                Interlocked.Read(ref _failures),
                Interlocked.Read(ref _cancellations),
                _cache.Values.Sum(entry => (long)entry.PngBytes.Length),
                _cache.Count,
                TimeSpan.FromTicks(Interlocked.Read(ref _lastDurationTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _totalPreparationTicks)));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cache.Clear();
        }
    }

    private RecentThumbnailResult PrepareCore(
        string path,
        int targetLongEdge,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return TryGetMissingPathCache(path, watch.Elapsed) ??
                       Complete(RecentThumbnailStatus.Unavailable, null, default, false, watch.Elapsed);
            }

            var identity = new FileIdentity(info.Length, info.LastWriteTimeUtc.Ticks);
            if (TryGetCurrentCache(path, identity, out var cached))
            {
                Interlocked.Increment(ref _memoryCacheHits);
                return Complete(
                    RecentThumbnailStatus.Ready,
                    cached.PngBytes,
                    cached.Size,
                    true,
                    watch.Elapsed);
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete);
            using var codec = SKCodec.Create(stream, out var creationResult);
            if (codec is null)
            {
                return Complete(
                    creationResult == SKCodecResult.Unimplemented
                        ? RecentThumbnailStatus.Unsupported
                        : RecentThumbnailStatus.Failed,
                    null,
                    default,
                    false,
                    watch.Elapsed);
            }

            if (!ImageFormatCapabilities.TryGetDetected(codec.EncodedFormat, out var capability) ||
                capability is null ||
                !ImageFormatCapabilities.SupportsFrameCount(capability, Math.Max(codec.FrameCount, 1)))
            {
                return Complete(
                    RecentThumbnailStatus.Unsupported,
                    null,
                    default,
                    false,
                    watch.Elapsed);
            }

            var encodedSize = new PixelSize(codec.Info.Width, codec.Info.Height);
            if (!encodedSize.IsValid)
            {
                return Complete(RecentThumbnailStatus.Failed, null, default, false, watch.Elapsed);
            }

            var orientation = SkiaImageDecodeBackend.ToExifOrientation(codec.EncodedOrigin);
            var orientedSize = OrientationTransform.GetOrientedSize(encodedSize, orientation);
            var targetSize = BoundedImageSize.Calculate(orientedSize, targetLongEdge);
            var scale = Math.Min(1f, (float)targetLongEdge /
                                     Math.Max(encodedSize.Width, encodedSize.Height));
            var scaled = codec.GetScaledDimensions(scale);
            if (scaled.Width <= 0 || scaled.Height <= 0 ||
                Math.Max(scaled.Width, scaled.Height) > MaximumIntermediateLongEdge ||
                checked((long)scaled.Width * scaled.Height * 4) > MaximumIntermediateBytes)
            {
                return Complete(
                    RecentThumbnailStatus.Unsupported,
                    null,
                    default,
                    false,
                    watch.Elapsed);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var srgb = SKColorSpace.CreateSrgb();
            var decodeInfo = new SKImageInfo(
                scaled.Width,
                scaled.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul,
                srgb);
            using var bitmap = new SKBitmap(decodeInfo);
            var decodeResult = codec.GetPixels(decodeInfo, bitmap.GetPixels());
            if (decodeResult is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
            {
                return Complete(RecentThumbnailStatus.Failed, null, default, false, watch.Elapsed);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var source = SKImage.FromBitmap(bitmap);
            if (source is null)
            {
                return Complete(RecentThumbnailStatus.Failed, null, default, false, watch.Elapsed);
            }

            var targetInfo = new SKImageInfo(
                targetSize.Width,
                targetSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul,
                srgb);
            using var surface = SKSurface.Create(targetInfo);
            if (surface is null)
            {
                return Complete(RecentThumbnailStatus.Failed, null, default, false, watch.Elapsed);
            }

            OrientedImageRenderer.Draw(
                surface.Canvas,
                source,
                new PixelSize(scaled.Width, scaled.Height),
                orientation,
                targetSize,
                SKColors.Transparent);
            cancellationToken.ThrowIfCancellationRequested();
            using var prepared = surface.Snapshot();
            using var encoded = prepared.Encode(SKEncodedImageFormat.Png, 92);
            var bytes = encoded?.ToArray();
            if (bytes is null || bytes.Length == 0)
            {
                return Complete(RecentThumbnailStatus.Failed, null, default, false, watch.Elapsed);
            }

            AddCache(path, identity, targetSize, bytes);
            Interlocked.Increment(ref _prepared);
            var duration = watch.Elapsed;
            Interlocked.Exchange(ref _lastDurationTicks, duration.Ticks);
            Interlocked.Add(ref _totalPreparationTicks, duration.Ticks);
            return Complete(RecentThumbnailStatus.Ready, bytes, targetSize, false, duration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              InvalidDataException or NotSupportedException or ArgumentException)
        {
            Interlocked.Increment(ref _failures);
            return Complete(RecentThumbnailStatus.Failed, null, default, false, watch.Elapsed);
        }
    }

    private RecentThumbnailResult? TryGetMissingPathCache(string path, TimeSpan duration)
    {
        lock (_sync)
        {
            if (!_cache.TryGetValue(path, out var entry))
            {
                return null;
            }

            entry.LastAccess = ++_accessSequence;
            Interlocked.Increment(ref _memoryCacheHits);
            return Complete(
                RecentThumbnailStatus.Ready,
                entry.PngBytes,
                entry.Size,
                true,
                duration);
        }
    }

    private bool TryGetCurrentCache(string path, FileIdentity identity, out CacheEntry entry)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(path, out var found))
            {
                if (found.Identity == identity)
                {
                    found.LastAccess = ++_accessSequence;
                    entry = found;
                    return true;
                }

                _cache.Remove(path);
            }

            entry = null!;
            return false;
        }
    }

    private void AddCache(string path, FileIdentity identity, PixelSize size, byte[] bytes)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _cache[path] = new CacheEntry(identity, size, bytes, ++_accessSequence);
            while (_cache.Count > MaximumCachedItems ||
                   _cache.Values.Sum(entry => (long)entry.PngBytes.Length) > MaximumCachedBytes)
            {
                var oldest = _cache.MinBy(pair => pair.Value.LastAccess);
                if (oldest.Key is null)
                {
                    break;
                }

                _cache.Remove(oldest.Key);
            }
        }
    }

    private static RecentThumbnailResult Complete(
        RecentThumbnailStatus status,
        byte[]? bytes,
        PixelSize size,
        bool fromMemoryCache,
        TimeSpan duration) =>
        new(status, bytes, size, fromMemoryCache, duration);

    private readonly record struct FileIdentity(long Length, long LastWriteTicks);

    private sealed class CacheEntry(
        FileIdentity identity,
        PixelSize size,
        byte[] pngBytes,
        long lastAccess)
    {
        public FileIdentity Identity { get; } = identity;

        public PixelSize Size { get; } = size;

        public byte[] PngBytes { get; } = pngBytes;

        public long LastAccess { get; set; } = lastAccess;
    }
}