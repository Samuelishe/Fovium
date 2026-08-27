using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Rendering;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace Fovium.ColorManagement;

internal sealed class ManagedPhotoSurface : IDisposable
{
    private SKBitmap? _bitmap;
    private SKImage? _image;

    public ManagedPhotoSurface(
        ManagedPhotoKey key,
        ManagedPhotoCoverage coverage,
        SKBitmap bitmap,
        SKImage image,
        TimeSpan sourceRenderDuration,
        TimeSpan transformDuration,
        TimeSpan finalizationDuration)
    {
        Key = key;
        Coverage = coverage;
        _bitmap = bitmap;
        _image = image;
        SourceRenderDuration = sourceRenderDuration;
        TransformDuration = transformDuration;
        FinalizationDuration = finalizationDuration;
    }

    public ManagedPhotoKey Key { get; }

    public ManagedPhotoCoverage Coverage { get; }

    public RectD Destination => Coverage.RasterDestination;

    public RectD OrientedSourceCoverage => Coverage.OrientedSourceRect;

    public SKImage Image => Volatile.Read(ref _image)
        ?? throw new ObjectDisposedException(nameof(ManagedPhotoSurface));

    public PixelSize PixelSize => new(Image.Width, Image.Height);

    public long RetainedBytes => checked((long)Image.Width * Image.Height * 4);

    internal byte[] CopyPixelBytes() => (_bitmap
        ?? throw new ObjectDisposedException(nameof(ManagedPhotoSurface))).GetPixelSpan().ToArray();

    public TimeSpan SourceRenderDuration { get; }

    public TimeSpan TransformDuration { get; }

    public TimeSpan FinalizationDuration { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _image, null)?.Dispose();
        Interlocked.Exchange(ref _bitmap, null)?.Dispose();
    }
}

internal enum ManagedPhotoPresentationQuality
{
    Exact,
    Proxy,
}

internal enum ManagedPhotoPendingReason
{
    None,
    NoPresentationYet,
    SourceChanged,
    DestinationChanged,
    GeometryRefinementPending,
}

internal sealed class ManagedPhotoPresentationLease : IDisposable
{
    private SharedResourceLease<ManagedPhotoSurface>? _surface;

    public ManagedPhotoPresentationLease(
        SharedResourceLease<ManagedPhotoSurface> surface,
        RectD targetDestination,
        ManagedPhotoPresentationQuality quality,
        bool coversVisiblePhoto,
        bool underResolved)
    {
        _surface = surface;
        TargetDestination = targetDestination;
        Quality = quality;
        CoversVisiblePhoto = coversVisiblePhoto;
        UnderResolved = underResolved;
    }

    public ManagedPhotoSurface Surface => Volatile.Read(ref _surface)?.Value
        ?? throw new ObjectDisposedException(nameof(ManagedPhotoPresentationLease));

    public RectD TargetDestination { get; }

    public ManagedPhotoPresentationQuality Quality { get; }

    public bool CoversVisiblePhoto { get; }

    public bool UnderResolved { get; }

    public void Dispose() => Interlocked.Exchange(ref _surface, null)?.Dispose();
}

internal sealed record ManagedPhotoRenderRequest(
    ManagedPhotoKey Key,
    ImageDescriptor Descriptor,
    DecodedImage.RenderLease Source,
    byte[] DestinationProfile) : IDisposable
{
    public void Dispose() => Source.Dispose();
}

internal interface IManagedPhotoRenderer : IDisposable
{
    ManagedPhotoSurface Render(ManagedPhotoRenderRequest request);
}

internal sealed class SkiaLittleCmsPhotoRenderer : IManagedPhotoRenderer
{
    private const int MaximumTransforms = 4;
    private readonly IColorTransformEngine _engine;
    private readonly Dictionary<DisplayProfileIdentity, IColorTransform> _transforms = [];
    private readonly Queue<DisplayProfileIdentity> _transformOrder = [];
    private bool _disposed;

    public SkiaLittleCmsPhotoRenderer(IColorTransformEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public ManagedPhotoSurface Render(ManagedPhotoRenderRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Key.Geometry.IsValid)
        {
            throw new ArgumentException("Managed photo geometry is invalid.", nameof(request));
        }

        var coverage = ManagedPhotoCoveragePlanner.Create(
            request.Key.Geometry,
            request.Descriptor.OrientedSize);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var referenceBitmap = RenderReferenceSrgb(request, coverage);
        var sourceRenderDuration = stopwatch.Elapsed;

        var input = referenceBitmap.GetPixelSpan();
        var transformed = new byte[input.Length];
        var transform = GetTransform(request.Key.DestinationIdentity, request.DestinationProfile);
        transform.Transform(input, transformed);
        var transformDuration = stopwatch.Elapsed - sourceRenderDuration;

        var finalInfo = new SKImageInfo(
            referenceBitmap.Width,
            referenceBitmap.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        var finalBitmap = new SKBitmap(finalInfo);
        try
        {
            Premultiply(transformed, finalBitmap.GetPixelSpan());
            var finalImage = SKImage.FromBitmap(finalBitmap)
                ?? throw new InvalidOperationException("Skia could not create the managed presentation image.");
            var finalizationDuration = stopwatch.Elapsed - sourceRenderDuration - transformDuration;
            return new ManagedPhotoSurface(
                request.Key,
                coverage,
                finalBitmap,
                finalImage,
                sourceRenderDuration,
                transformDuration,
                finalizationDuration);
        }
        catch
        {
            finalBitmap.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var transform in _transforms.Values)
        {
            transform.Dispose();
        }

        _transforms.Clear();
        _transformOrder.Clear();
        _engine.Dispose();
    }

    internal static void Premultiply(ReadOnlySpan<byte> unpremultiplied, Span<byte> premultiplied)
    {
        if (unpremultiplied.Length != premultiplied.Length || unpremultiplied.Length % 4 != 0)
        {
            throw new ArgumentException("BGRA buffers must have the same whole-pixel length.");
        }

        for (var offset = 0; offset < unpremultiplied.Length; offset += 4)
        {
            var alpha = unpremultiplied[offset + 3];
            if (alpha == 0)
            {
                premultiplied.Slice(offset, 4).Clear();
                continue;
            }

            premultiplied[offset] = PremultiplyChannel(unpremultiplied[offset], alpha);
            premultiplied[offset + 1] = PremultiplyChannel(unpremultiplied[offset + 1], alpha);
            premultiplied[offset + 2] = PremultiplyChannel(unpremultiplied[offset + 2], alpha);
            premultiplied[offset + 3] = alpha;
        }
    }

    private static SKBitmap RenderReferenceSrgb(
        ManagedPhotoRenderRequest request,
        ManagedPhotoCoverage coverage)
    {
        var geometry = request.Key.Geometry;
        var rasterDestination = coverage.RasterDestination;
        var pixelWidth = coverage.RasterPixelSize.Width;
        var pixelHeight = coverage.RasterPixelSize.Height;
        using var referenceColorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(
            pixelWidth,
            pixelHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul,
            referenceColorSpace);
        var bitmap = new SKBitmap(info);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            var affine = OrientationAffine.Create(request.Descriptor.EncodedSize, request.Descriptor.Orientation);
            var oriented = request.Descriptor.OrientedSize;
            var scaleX = geometry.PhotoDestination.Width / oriented.Width * geometry.RenderScaling;
            var scaleY = geometry.PhotoDestination.Height / oriented.Height * geometry.RenderScaling;
            var matrix = new SKMatrix(
                (float)(affine.A * scaleX),
                (float)(affine.B * scaleX),
                (float)((geometry.PhotoDestination.X - rasterDestination.X) * geometry.RenderScaling + affine.C * scaleX),
                (float)(affine.D * scaleY),
                (float)(affine.E * scaleY),
                (float)((geometry.PhotoDestination.Y - rasterDestination.Y) * geometry.RenderScaling + affine.F * scaleY),
                0,
                0,
                1);
            canvas.Concat(in matrix);
            using var paint = new SKPaint { IsAntialias = false };
            var sampling = geometry.ExactPixelSampling
                ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
                : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            canvas.DrawImage(request.Source.Image, 0, 0, sampling, paint);
            canvas.Flush();
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private IColorTransform GetTransform(
        DisplayProfileIdentity identity,
        ReadOnlyMemory<byte> destinationProfile)
    {
        if (_transforms.TryGetValue(identity, out var transform))
        {
            return transform;
        }

        transform = _engine.CreateTransform(destinationProfile);
        _transforms.Add(identity, transform);
        _transformOrder.Enqueue(identity);
        while (_transformOrder.Count > MaximumTransforms)
        {
            var obsolete = _transformOrder.Dequeue();
            _transforms.Remove(obsolete, out var removed);
            removed?.Dispose();
        }

        return transform;
    }

    private static byte PremultiplyChannel(byte value, byte alpha) =>
        checked((byte)((value * alpha + 127) / byte.MaxValue));
}

internal readonly record struct ManagedPhotoCoordinatorMetrics(
    long Requests,
    long CoalescedRequests,
    long Completed,
    long StaleResults,
    long Failures,
    int Active,
    int Pending,
    long CurrentRasterBytes,
    long MaximumRasterBytes,
    PixelSize LastRasterSize,
    TimeSpan LastSourceRenderDuration,
    TimeSpan LastTransformDuration,
    TimeSpan LastFinalizationDuration,
    long GeometryRequests,
    long ExactPresentationRequests,
    long ProxyFrames,
    long ExactFrames,
    long GeometryOnlyBlackFallbackFrames,
    long OverscanHits,
    long OverscanMisses,
    long QualityRefinementRequests,
    long SourceChanges,
    long DestinationChanges,
    ManagedPhotoPendingReason LastPendingReason,
    double LastOverscanFactor);

internal interface IManagedPhotoRefinementScheduler : IDisposable
{
    void Schedule(Action action);

    void Cancel();
}

internal sealed class ManagedPhotoRefinementTimer : IManagedPhotoRefinementScheduler
{
    private const int DebounceMilliseconds = 100;
    private readonly object _sync = new();
    private readonly Timer _timer;
    private Action? _pending;
    private bool _disposed;

    public ManagedPhotoRefinementTimer()
    {
        _timer = new Timer(
            static state => ((ManagedPhotoRefinementTimer)state!).Fire(),
            this,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    public void Schedule(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pending = action;
            _timer.Change(DebounceMilliseconds, Timeout.Infinite);
        }
    }

    public void Cancel()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _pending = null;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
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
            _pending = null;
        }

        _timer.Dispose();
    }

    private void Fire()
    {
        Action? action;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            action = _pending;
            _pending = null;
        }

        action?.Invoke();
    }
}

internal sealed class ManagedPhotoPresentationCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly IManagedPhotoRenderer _renderer;
    private readonly IManagedPhotoRefinementScheduler _refinementScheduler;
    private ManagedPhotoRenderRequest? _pending;
    private SharedResource<ManagedPhotoSurface>? _current;
    private Task _worker = Task.CompletedTask;
    private long _generation;
    private long _requests;
    private long _coalescedRequests;
    private long _completed;
    private long _staleResults;
    private long _failures;
    private long _maximumRasterBytes;
    private PixelSize _lastRasterSize;
    private TimeSpan _lastSourceRenderDuration;
    private TimeSpan _lastTransformDuration;
    private TimeSpan _lastFinalizationDuration;
    private ManagedPhotoKey? _lastRequestedKey;
    private long _geometryRequests;
    private long _proxyFrames;
    private long _exactFrames;
    private long _geometryOnlyBlackFallbackFrames;
    private long _overscanHits;
    private long _overscanMisses;
    private long _qualityRefinementRequests;
    private long _sourceChanges;
    private long _destinationChanges;
    private ManagedPhotoPendingReason _lastPendingReason;
    private double _lastOverscanFactor;
    private bool _active;
    private bool _disposed;

    public ManagedPhotoPresentationCoordinator(
        IManagedPhotoRenderer renderer,
        IManagedPhotoRefinementScheduler? refinementScheduler = null)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _refinementScheduler = refinementScheduler ?? new ManagedPhotoRefinementTimer();
    }

    public event EventHandler? PresentationChanged;

    public event EventHandler? PresentationFailed;

    public ManagedPhotoCoordinatorMetrics Metrics
    {
        get
        {
            lock (_sync)
            {
                return new ManagedPhotoCoordinatorMetrics(
                    _requests,
                    _coalescedRequests,
                    _completed,
                    _staleResults,
                    _failures,
                    _active ? 1 : 0,
                    _pending is null ? 0 : 1,
                    _current is not null && _current.TryGetValue(out var surface)
                        ? surface!.RetainedBytes
                        : 0,
                    _maximumRasterBytes,
                    _lastRasterSize,
                    _lastSourceRenderDuration,
                    _lastTransformDuration,
                    _lastFinalizationDuration,
                    _geometryRequests,
                    _requests,
                    _proxyFrames,
                    _exactFrames,
                    _geometryOnlyBlackFallbackFrames,
                    _overscanHits,
                    _overscanMisses,
                    _qualityRefinementRequests,
                    _sourceChanges,
                    _destinationChanges,
                    _lastPendingReason,
                    _lastOverscanFactor);
            }
        }
    }

    public void Request(ManagedPhotoRenderRequest request) => Request(
        request,
        deferGeometryRefinement: false,
        ManagedPhotoPendingReason.NoPresentationYet,
        qualityRefinement: false);

    public void Request(
        ManagedPhotoRenderRequest request,
        bool deferGeometryRefinement,
        ManagedPhotoPendingReason pendingReason,
        bool qualityRefinement)
    {
        ArgumentNullException.ThrowIfNull(request);
        ManagedPhotoRenderRequest? replaced;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _requests++;
            _generation++;
            RecordRequestTransition(request.Key);
            _lastRequestedKey = request.Key;
            _lastPendingReason = pendingReason;
            if (qualityRefinement)
            {
                _qualityRefinementRequests++;
            }

            replaced = _pending;
            if (replaced is not null)
            {
                _coalescedRequests++;
            }

            _pending = request;
            if (!_active)
            {
                if (deferGeometryRefinement)
                {
                    _refinementScheduler.Schedule(StartDeferredWorker);
                }
                else
                {
                    _refinementScheduler.Cancel();
                    _active = true;
                    _worker = Task.Run(Process);
                }
            }
        }

        replaced?.Dispose();
    }

    public bool TryAcquire(ManagedPhotoKey key, out SharedResourceLease<ManagedPhotoSurface>? surface)
    {
        lock (_sync)
        {
            if (_current is null || !_current.TryGetValue(out var value) || value!.Key != key)
            {
                surface = null;
                return false;
            }

            surface = _current.Acquire();
            return true;
        }
    }

    public bool TryAcquirePresentation(
        ManagedPhotoKey requestedKey,
        out ManagedPhotoPresentationLease? presentation,
        out ManagedPhotoPendingReason unavailableReason)
    {
        lock (_sync)
        {
            if (_current is null || !_current.TryGetValue(out var value))
            {
                presentation = null;
                unavailableReason = ManagedPhotoPendingReason.NoPresentationYet;
                return false;
            }

            var current = value!;
            var currentKey = current.Key;
            if (currentKey.ImageIdentity != requestedKey.ImageIdentity ||
                currentKey.EncodedSize != requestedKey.EncodedSize ||
                currentKey.Orientation != requestedKey.Orientation)
            {
                presentation = null;
                unavailableReason = ManagedPhotoPendingReason.SourceChanged;
                return false;
            }

            if (currentKey.DestinationIdentity != requestedKey.DestinationIdentity)
            {
                presentation = null;
                unavailableReason = ManagedPhotoPendingReason.DestinationChanged;
                return false;
            }

            var orientedSize = OrientationTransform.GetOrientedSize(
                requestedKey.EncodedSize,
                requestedKey.Orientation);
            var requestedVisibleSource = ManagedPhotoCoveragePlanner.VisibleSourceRect(
                requestedKey.Geometry,
                orientedSize);
            if (!ManagedPhotoCoveragePlanner.Intersects(
                    current.OrientedSourceCoverage,
                    requestedVisibleSource))
            {
                presentation = null;
                unavailableReason = ManagedPhotoPendingReason.GeometryRefinementPending;
                _overscanMisses++;
                return false;
            }

            var coversVisible = ManagedPhotoCoveragePlanner.Contains(
                current.OrientedSourceCoverage,
                requestedVisibleSource);
            if (coversVisible)
            {
                _overscanHits++;
            }
            else
            {
                _overscanMisses++;
            }

            var targetDestination = ManagedPhotoCoveragePlanner.MapSourceToDestination(
                current.OrientedSourceCoverage,
                requestedKey.Geometry.PhotoDestination,
                orientedSize);
            var requestedDensityX = requestedKey.Geometry.PhotoDestination.Width *
                requestedKey.Geometry.RenderScaling / orientedSize.Width;
            var requestedDensityY = requestedKey.Geometry.PhotoDestination.Height *
                requestedKey.Geometry.RenderScaling / orientedSize.Height;
            var currentDensityX = current.PixelSize.Width / current.OrientedSourceCoverage.Width;
            var currentDensityY = current.PixelSize.Height / current.OrientedSourceCoverage.Height;
            var densityRatio = Math.Min(
                currentDensityX / requestedDensityX,
                currentDensityY / requestedDensityY);
            var quality = currentKey == requestedKey
                ? ManagedPhotoPresentationQuality.Exact
                : ManagedPhotoPresentationQuality.Proxy;
            presentation = new ManagedPhotoPresentationLease(
                _current.Acquire(),
                targetDestination,
                quality,
                coversVisible,
                densityRatio < 0.75);
            unavailableReason = quality == ManagedPhotoPresentationQuality.Exact
                ? ManagedPhotoPendingReason.None
                : ManagedPhotoPendingReason.GeometryRefinementPending;
            return true;
        }
    }

    public void RecordFrame(ManagedPhotoPresentationQuality quality)
    {
        lock (_sync)
        {
            if (quality == ManagedPhotoPresentationQuality.Exact)
            {
                _exactFrames++;
            }
            else
            {
                _proxyFrames++;
            }
        }
    }

    public void RecordGeometryOnlyBlackFallback()
    {
        lock (_sync)
        {
            _geometryOnlyBlackFallbackFrames++;
        }
    }

    public void Clear()
    {
        ManagedPhotoRenderRequest? pending;
        SharedResource<ManagedPhotoSurface>? current;
        lock (_sync)
        {
            _generation++;
            _refinementScheduler.Cancel();
            pending = _pending;
            _pending = null;
            current = _current;
            _current = null;
        }

        pending?.Dispose();
        current?.ReleaseOwner();
        PresentationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        ManagedPhotoRenderRequest? pending;
        SharedResource<ManagedPhotoSurface>? current;
        var disposeRenderer = false;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
            _refinementScheduler.Cancel();
            pending = _pending;
            _pending = null;
            current = _current;
            _current = null;
            disposeRenderer = !_active;
        }

        pending?.Dispose();
        current?.ReleaseOwner();
        if (disposeRenderer)
        {
            _renderer.Dispose();
        }

        _refinementScheduler.Dispose();
    }

    private void Process()
    {
        while (true)
        {
            ManagedPhotoRenderRequest? request;
            long generation;
            lock (_sync)
            {
                request = _pending;
                _pending = null;
                generation = _generation;
                if (request is null)
                {
                    _active = false;
                    if (_disposed)
                    {
                        _renderer.Dispose();
                    }

                    return;
                }
            }

            ManagedPhotoSurface? result = null;
            Exception? failure = null;
            try
            {
                result = _renderer.Render(request);
            }
            catch (Exception exception) when (exception is
                InvalidDataException or InvalidOperationException or ArgumentException or
                ArithmeticException or ExternalException)
            {
                failure = exception;
            }
            finally
            {
                request.Dispose();
            }

            SharedResource<ManagedPhotoSurface>? previous = null;
            var changed = false;
            var failed = false;
            lock (_sync)
            {
                if (_disposed || generation != _generation)
                {
                    _staleResults++;
                }
                else if (failure is not null || result is null)
                {
                    _failures++;
                    failed = true;
                }
                else
                {
                    previous = _current;
                    _current = new SharedResource<ManagedPhotoSurface>(result);
                    _lastRasterSize = result.PixelSize;
                    _maximumRasterBytes = Math.Max(_maximumRasterBytes, result.RetainedBytes);
                    _lastSourceRenderDuration = result.SourceRenderDuration;
                    _lastTransformDuration = result.TransformDuration;
                    _lastFinalizationDuration = result.FinalizationDuration;
                    _lastOverscanFactor = result.Coverage.OverscanFactor;
                    _lastPendingReason = ManagedPhotoPendingReason.None;
                    result = null;
                    _completed++;
                    changed = true;
                }
            }

            result?.Dispose();
            previous?.ReleaseOwner();
            if (failed)
            {
                PresentationFailed?.Invoke(this, EventArgs.Empty);
            }
            else if (changed)
            {
                PresentationChanged?.Invoke(this, EventArgs.Empty);
            }

        }
    }

    private void StartDeferredWorker()
    {
        lock (_sync)
        {
            if (_disposed || _active || _pending is null)
            {
                return;
            }

            _active = true;
            _worker = Task.Run(Process);
        }
    }

    private void RecordRequestTransition(ManagedPhotoKey key)
    {
        if (_lastRequestedKey is not { } previous)
        {
            return;
        }

        if (previous.ImageIdentity != key.ImageIdentity ||
            previous.EncodedSize != key.EncodedSize ||
            previous.Orientation != key.Orientation)
        {
            _sourceChanges++;
        }
        else if (previous.DestinationIdentity != key.DestinationIdentity)
        {
            _destinationChanges++;
        }
        else if (previous.Geometry != key.Geometry)
        {
            _geometryRequests++;
        }
    }
}
