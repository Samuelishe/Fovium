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
        TimeSpan finalizationDuration,
        ManagedPhotoSurfaceRole role = ManagedPhotoSurfaceRole.Detail)
    {
        Key = key;
        Coverage = coverage;
        _bitmap = bitmap;
        _image = image;
        SourceRenderDuration = sourceRenderDuration;
        TransformDuration = transformDuration;
        FinalizationDuration = finalizationDuration;
        Role = role;
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

    public ManagedPhotoSurfaceRole Role { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _image, null)?.Dispose();
        Interlocked.Exchange(ref _bitmap, null)?.Dispose();
    }
}

internal enum ManagedPhotoSurfaceRole
{
    Base,
    Detail,
}

internal enum ManagedPhotoPresentationQuality
{
    Exact,
    Proxy,
    Base,
}

internal enum ManagedPhotoPendingReason
{
    None,
    NoPresentationYet,
    SourceChanged,
    DestinationChanged,
    GeometryRefinementPending,
    CoverageRefinementPending,
    QualityRefinementPending,
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
    public ManagedPhotoSurfaceRole Role { get; init; } = ManagedPhotoSurfaceRole.Detail;

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

        var coverage = request.Role == ManagedPhotoSurfaceRole.Base
            ? ManagedPhotoBaseCoveragePlanner.Create(
                request.Key.Geometry,
                request.Descriptor.OrientedSize)
            : ManagedPhotoCoveragePlanner.Create(
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
                finalizationDuration,
                request.Role);
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
            var sourceCoverage = coverage.OrientedSourceRect;
            var scaleX = pixelWidth / sourceCoverage.Width;
            var scaleY = pixelHeight / sourceCoverage.Height;
            var matrix = new SKMatrix(
                (float)(affine.A * scaleX),
                (float)(affine.B * scaleX),
                (float)((affine.C - sourceCoverage.X) * scaleX),
                (float)(affine.D * scaleY),
                (float)(affine.E * scaleY),
                (float)((affine.F - sourceCoverage.Y) * scaleY),
                0,
                0,
                1);
            canvas.Concat(in matrix);
            using var paint = new SKPaint { IsAntialias = false };
            var sampling = request.Role == ManagedPhotoSurfaceRole.Detail &&
                request.Key.Geometry.ExactPixelSampling
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
    double LastOverscanFactor,
    long BaseRasterBytes,
    long DetailRasterBytes,
    long MaximumCombinedRasterBytes,
    long BaseFrames,
    long BaseFallbackFrames,
    long PartialCoverageRejected,
    long CoverageRefinementRequests,
    long CoverageHits,
    long CoverageMisses,
    long ManagedIncompletePhotoFrames,
    TimeSpan LastRequestToWorkerStartDuration,
    TimeSpan LastCoverageRequestToWorkerStartDuration,
    TimeSpan LastQualityRequestToWorkerStartDuration,
    TimeSpan LastPublicationToFrameDuration);

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
    private SharedResource<ManagedPhotoSurface>? _base;
    private SharedResource<ManagedPhotoSurface>? _detail;
    private Task _worker = Task.CompletedTask;
    private long _generation;
    private long _identityGeneration;
    private long _pendingGeneration;
    private long _pendingIdentityGeneration;
    private long _pendingRequestedTimestamp;
    private ManagedPhotoPendingReason _pendingReason;
    private bool _pendingRequiresBase;
    private ManagedPhotoSurfaceRole? _activeRole;
    private ManagedPhotoKey? _activeKey;
    private long _activeIdentityGeneration;
    private long _requests;
    private long _coalescedRequests;
    private long _completed;
    private long _staleResults;
    private long _failures;
    private long _maximumRasterBytes;
    private long _maximumCombinedRasterBytes;
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
    private long _coverageRefinementRequests;
    private long _baseFrames;
    private long _baseFallbackFrames;
    private long _partialCoverageRejected;
    private long _coverageHits;
    private long _coverageMisses;
    private long _managedIncompletePhotoFrames;
    private long _lastPublicationTimestamp;
    private TimeSpan _lastRequestToWorkerStartDuration;
    private TimeSpan _lastCoverageRequestToWorkerStartDuration;
    private TimeSpan _lastQualityRequestToWorkerStartDuration;
    private TimeSpan _lastPublicationToFrameDuration;
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
                    RetainedBytes(_base) + RetainedBytes(_detail),
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
                    _lastOverscanFactor,
                    RetainedBytes(_base),
                    RetainedBytes(_detail),
                    _maximumCombinedRasterBytes,
                    _baseFrames,
                    _baseFallbackFrames,
                    _partialCoverageRejected,
                    _coverageRefinementRequests,
                    _coverageHits,
                    _coverageMisses,
                    _managedIncompletePhotoFrames,
                    _lastRequestToWorkerStartDuration,
                    _lastCoverageRequestToWorkerStartDuration,
                    _lastQualityRequestToWorkerStartDuration,
                    _lastPublicationToFrameDuration);
            }
        }
    }

    public void Request(ManagedPhotoRenderRequest request) => Request(
        request,
        deferGeometryRefinement: false,
        ManagedPhotoPendingReason.NoPresentationYet,
        qualityRefinement: false,
        ensureFullSourceBase: false);

    public void Request(
        ManagedPhotoRenderRequest request,
        bool deferGeometryRefinement,
        ManagedPhotoPendingReason pendingReason,
        bool qualityRefinement,
        bool ensureFullSourceBase = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ManagedPhotoRenderRequest? replaced;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _requests++;
            _generation++;
            var identityChanged = _lastRequestedKey is { } previous &&
                !HasSamePresentationIdentity(previous, request.Key);
            if (identityChanged)
            {
                _identityGeneration++;
            }

            RecordRequestTransition(request.Key);
            _lastRequestedKey = request.Key;
            _lastPendingReason = pendingReason;
            if (qualityRefinement)
            {
                _qualityRefinementRequests++;
            }

            if (pendingReason == ManagedPhotoPendingReason.CoverageRefinementPending)
            {
                _coverageRefinementRequests++;
            }

            replaced = _pending;
            if (replaced is not null)
            {
                _coalescedRequests++;
            }

            _pending = request;
            _pendingGeneration = _generation;
            _pendingIdentityGeneration = _identityGeneration;
            _pendingRequestedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _pendingReason = pendingReason;
            _pendingRequiresBase = ensureFullSourceBase && !HasCompatibleBaseOrBaseWork(request.Key);
            if (!_active)
            {
                if (deferGeometryRefinement && !_pendingRequiresBase)
                {
                    _refinementScheduler.Schedule(StartDeferredWorker);
                }
                else
                {
                    _refinementScheduler.Cancel();
                    StartWorkerLocked();
                }
            }
        }

        replaced?.Dispose();
    }

    public bool TryAcquire(ManagedPhotoKey key, out SharedResourceLease<ManagedPhotoSurface>? surface)
    {
        lock (_sync)
        {
            var candidate = ExactResource(key);
            if (candidate is null)
            {
                surface = null;
                return false;
            }

            surface = candidate.Acquire();
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
            var orientedSize = OrientationTransform.GetOrientedSize(
                requestedKey.EncodedSize,
                requestedKey.Orientation);
            var requestedVisibleSource = ManagedPhotoCoveragePlanner.VisibleSourceRect(
                requestedKey.Geometry,
                orientedSize);

            if (TryAcquireFullyCoveringDetail(
                    requestedKey,
                    orientedSize,
                    requestedVisibleSource,
                    out presentation,
                    out unavailableReason))
            {
                return true;
            }

            if (TryAcquireFullyCoveringBase(
                    requestedKey,
                    orientedSize,
                    requestedVisibleSource,
                    out presentation,
                    out unavailableReason))
            {
                return true;
            }

            presentation = null;
            unavailableReason = DetermineUnavailableReason(requestedKey);
            return false;
        }
    }

    public void RecordFrame(ManagedPhotoPresentationQuality quality, bool coversVisiblePhoto = true)
    {
        lock (_sync)
        {
            if (!coversVisiblePhoto)
            {
                _managedIncompletePhotoFrames++;
                return;
            }

            if (_lastPublicationTimestamp != 0)
            {
                _lastPublicationToFrameDuration =
                    System.Diagnostics.Stopwatch.GetElapsedTime(_lastPublicationTimestamp);
            }

            if (quality == ManagedPhotoPresentationQuality.Exact)
            {
                _exactFrames++;
            }
            else if (quality == ManagedPhotoPresentationQuality.Proxy)
            {
                _proxyFrames++;
            }
            else
            {
                _baseFrames++;
                _baseFallbackFrames++;
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

    private bool TryAcquireFullyCoveringDetail(
        ManagedPhotoKey requestedKey,
        PixelSize orientedSize,
        RectD requestedVisibleSource,
        out ManagedPhotoPresentationLease? presentation,
        out ManagedPhotoPendingReason pendingReason)
    {
        if (_detail is null ||
            !_detail.TryGetValue(out var value) ||
            !HasSamePresentationIdentity(value!.Key, requestedKey))
        {
            presentation = null;
            pendingReason = ManagedPhotoPendingReason.CoverageRefinementPending;
            return false;
        }

        var detail = value!;
        if (!ManagedPhotoCoveragePlanner.Contains(detail.OrientedSourceCoverage, requestedVisibleSource))
        {
            if (ManagedPhotoCoveragePlanner.Intersects(
                    detail.OrientedSourceCoverage,
                    requestedVisibleSource))
            {
                _partialCoverageRejected++;
            }

            _overscanMisses++;
            _coverageMisses++;
            presentation = null;
            pendingReason = ManagedPhotoPendingReason.CoverageRefinementPending;
            return false;
        }

        _overscanHits++;
        _coverageHits++;
        var quality = detail.Key == requestedKey
            ? ManagedPhotoPresentationQuality.Exact
            : ManagedPhotoPresentationQuality.Proxy;
        presentation = CreateLease(
            _detail,
            detail,
            requestedKey,
            orientedSize,
            quality);
        pendingReason = quality == ManagedPhotoPresentationQuality.Exact
            ? ManagedPhotoPendingReason.None
            : ManagedPhotoPendingReason.QualityRefinementPending;
        return true;
    }

    private bool TryAcquireFullyCoveringBase(
        ManagedPhotoKey requestedKey,
        PixelSize orientedSize,
        RectD requestedVisibleSource,
        out ManagedPhotoPresentationLease? presentation,
        out ManagedPhotoPendingReason pendingReason)
    {
        if (_base is null ||
            !_base.TryGetValue(out var value) ||
            !HasSamePresentationIdentity(value!.Key, requestedKey) ||
            !ManagedPhotoCoveragePlanner.Contains(value.OrientedSourceCoverage, requestedVisibleSource))
        {
            presentation = null;
            pendingReason = ManagedPhotoPendingReason.CoverageRefinementPending;
            return false;
        }

        _coverageHits++;
        var baseSurface = value!;
        var underResolved = IsUnderResolved(baseSurface, requestedKey, orientedSize);
        var quality = baseSurface.Key == requestedKey && !underResolved
            ? ManagedPhotoPresentationQuality.Exact
            : ManagedPhotoPresentationQuality.Base;
        presentation = CreateLease(
            _base,
            baseSurface,
            requestedKey,
            orientedSize,
            quality);
        pendingReason = quality == ManagedPhotoPresentationQuality.Exact
            ? ManagedPhotoPendingReason.None
            : ManagedPhotoPendingReason.CoverageRefinementPending;
        return true;
    }

    private static ManagedPhotoPresentationLease CreateLease(
        SharedResource<ManagedPhotoSurface> resource,
        ManagedPhotoSurface surface,
        ManagedPhotoKey requestedKey,
        PixelSize orientedSize,
        ManagedPhotoPresentationQuality quality) => new(
            resource.Acquire(),
            ManagedPhotoCoveragePlanner.MapSourceToDestination(
                surface.OrientedSourceCoverage,
                requestedKey.Geometry.PhotoDestination,
                orientedSize),
            quality,
            coversVisiblePhoto: true,
            IsUnderResolved(surface, requestedKey, orientedSize));

    private static bool IsUnderResolved(
        ManagedPhotoSurface surface,
        ManagedPhotoKey requestedKey,
        PixelSize orientedSize)
    {
        var requestedDensityX = requestedKey.Geometry.PhotoDestination.Width *
            requestedKey.Geometry.RenderScaling / orientedSize.Width;
        var requestedDensityY = requestedKey.Geometry.PhotoDestination.Height *
            requestedKey.Geometry.RenderScaling / orientedSize.Height;
        var currentDensityX = surface.PixelSize.Width / surface.OrientedSourceCoverage.Width;
        var currentDensityY = surface.PixelSize.Height / surface.OrientedSourceCoverage.Height;
        var densityRatio = Math.Min(
            currentDensityX / requestedDensityX,
            currentDensityY / requestedDensityY);
        return densityRatio < 0.75;
    }

    private ManagedPhotoPendingReason DetermineUnavailableReason(ManagedPhotoKey requestedKey)
    {
        var candidate = FirstAvailableSurface();
        if (candidate is null)
        {
            return ManagedPhotoPendingReason.NoPresentationYet;
        }

        if (!HasSameSourceIdentity(candidate.Key, requestedKey))
        {
            return ManagedPhotoPendingReason.SourceChanged;
        }

        return candidate.Key.DestinationIdentity != requestedKey.DestinationIdentity
            ? ManagedPhotoPendingReason.DestinationChanged
            : ManagedPhotoPendingReason.CoverageRefinementPending;
    }

    public void Clear()
    {
        ManagedPhotoRenderRequest? pending;
        SharedResource<ManagedPhotoSurface>? baseSurface;
        SharedResource<ManagedPhotoSurface>? detail;
        lock (_sync)
        {
            _generation++;
            _identityGeneration++;
            _refinementScheduler.Cancel();
            pending = _pending;
            _pending = null;
            _pendingRequiresBase = false;
            baseSurface = _base;
            detail = _detail;
            _base = null;
            _detail = null;
        }

        pending?.Dispose();
        baseSurface?.ReleaseOwner();
        detail?.ReleaseOwner();
        PresentationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        ManagedPhotoRenderRequest? pending;
        SharedResource<ManagedPhotoSurface>? baseSurface;
        SharedResource<ManagedPhotoSurface>? detail;
        var disposeRenderer = false;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
            _identityGeneration++;
            _refinementScheduler.Cancel();
            pending = _pending;
            _pending = null;
            _pendingRequiresBase = false;
            baseSurface = _base;
            detail = _detail;
            _base = null;
            _detail = null;
            disposeRenderer = !_active;
        }

        pending?.Dispose();
        baseSurface?.ReleaseOwner();
        detail?.ReleaseOwner();
        if (disposeRenderer)
        {
            _renderer.Dispose();
        }

        _refinementScheduler.Dispose();
    }

    private void Process(ManagedPhotoWork initialWork)
    {
        var work = initialWork;
        while (true)
        {
            if (work.Request.Role == ManagedPhotoSurfaceRole.Detail)
            {
                lock (_sync)
                {
                    var requestToWorker =
                        System.Diagnostics.Stopwatch.GetElapsedTime(work.RequestedTimestamp);
                    _lastRequestToWorkerStartDuration = requestToWorker;
                    if (work.PendingReason == ManagedPhotoPendingReason.CoverageRefinementPending)
                    {
                        _lastCoverageRequestToWorkerStartDuration = requestToWorker;
                    }
                    else if (work.PendingReason == ManagedPhotoPendingReason.QualityRefinementPending)
                    {
                        _lastQualityRequestToWorkerStartDuration = requestToWorker;
                    }
                }
            }

            ManagedPhotoSurface? result = null;
            Exception? failure = null;
            try
            {
                result = _renderer.Render(work.Request);
            }
            catch (Exception exception) when (exception is
                InvalidDataException or InvalidOperationException or ArgumentException or
                ArithmeticException or ExternalException)
            {
                failure = exception;
            }
            finally
            {
                work.Request.Dispose();
            }

            SharedResource<ManagedPhotoSurface>? previous = null;
            ManagedPhotoRenderRequest? redundantPending = null;
            var changed = false;
            var failed = false;
            ManagedPhotoWork? next;
            var disposeRenderer = false;
            lock (_sync)
            {
                var isCurrent = !_disposed &&
                    (work.Request.Role == ManagedPhotoSurfaceRole.Base
                        ? work.IdentityGeneration == _identityGeneration &&
                            _lastRequestedKey is { } requested &&
                            HasSamePresentationIdentity(work.Request.Key, requested)
                        : work.Generation == _generation);
                if (!isCurrent)
                {
                    _staleResults++;
                }
                else if (failure is not null || result is null)
                {
                    _failures++;
                    failed = true;
                    if (work.Request.Role == ManagedPhotoSurfaceRole.Base)
                    {
                        redundantPending = _pending;
                        _pending = null;
                        _pendingRequiresBase = false;
                    }
                }
                else
                {
                    var published = result;
                    if (work.Request.Role == ManagedPhotoSurfaceRole.Base)
                    {
                        previous = _base;
                        _base = new SharedResource<ManagedPhotoSurface>(published);
                        if (_pending is { } pending &&
                            _pendingGeneration == _generation &&
                            published.Key == pending.Key &&
                            !IsUnderResolved(
                                published,
                                pending.Key,
                                pending.Descriptor.OrientedSize))
                        {
                            redundantPending = _pending;
                            _pending = null;
                        }
                    }
                    else
                    {
                        previous = _detail;
                        _detail = new SharedResource<ManagedPhotoSurface>(published);
                    }

                    _lastRasterSize = published.PixelSize;
                    _maximumRasterBytes = Math.Max(_maximumRasterBytes, published.RetainedBytes);
                    _maximumCombinedRasterBytes = Math.Max(
                        _maximumCombinedRasterBytes,
                        RetainedBytes(_base) + RetainedBytes(_detail));
                    _lastSourceRenderDuration = published.SourceRenderDuration;
                    _lastTransformDuration = published.TransformDuration;
                    _lastFinalizationDuration = published.FinalizationDuration;
                    _lastOverscanFactor = published.Coverage.OverscanFactor;
                    _lastPendingReason = _pending is null
                        ? ManagedPhotoPendingReason.None
                        : work.Request.Role == ManagedPhotoSurfaceRole.Base
                            ? ManagedPhotoPendingReason.CoverageRefinementPending
                            : _lastPendingReason;
                    _lastPublicationTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    result = null;
                    _completed++;
                    changed = true;
                }

                next = TakeNextWorkLocked();
                if (next is null)
                {
                    _active = false;
                    _activeRole = null;
                    _activeKey = null;
                    _activeIdentityGeneration = 0;
                    disposeRenderer = _disposed;
                }
                else
                {
                    _activeRole = next.Value.Request.Role;
                    _activeKey = next.Value.Request.Key;
                    _activeIdentityGeneration = next.Value.IdentityGeneration;
                }
            }

            result?.Dispose();
            redundantPending?.Dispose();
            previous?.ReleaseOwner();
            if (failed)
            {
                PresentationFailed?.Invoke(this, EventArgs.Empty);
            }
            else if (changed)
            {
                PresentationChanged?.Invoke(this, EventArgs.Empty);
            }

            if (next is null)
            {
                if (disposeRenderer)
                {
                    _renderer.Dispose();
                }

                return;
            }

            work = next.Value;
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

            StartWorkerLocked();
        }
    }

    private void StartWorkerLocked()
    {
        var work = TakeNextWorkLocked();
        if (work is null)
        {
            return;
        }

        _active = true;
        _activeRole = work.Value.Request.Role;
        _activeKey = work.Value.Request.Key;
        _activeIdentityGeneration = work.Value.IdentityGeneration;
        _worker = Task.Run(() => Process(work.Value));
    }

    private ManagedPhotoWork? TakeNextWorkLocked()
    {
        if (_pending is null)
        {
            return null;
        }

        if (_pendingRequiresBase)
        {
            _pendingRequiresBase = false;
            return new ManagedPhotoWork(
                _pending with
                {
                    Source = _pending.Source.Acquire(),
                    Role = ManagedPhotoSurfaceRole.Base,
                },
                _pendingGeneration,
                _pendingIdentityGeneration,
                _pendingRequestedTimestamp,
                _pendingReason);
        }

        var request = _pending;
        _pending = null;
        return new ManagedPhotoWork(
            request,
            _pendingGeneration,
            _pendingIdentityGeneration,
            _pendingRequestedTimestamp,
            _pendingReason);
    }

    private bool HasCompatibleBaseOrBaseWork(ManagedPhotoKey key)
    {
        if (_base is not null &&
            _base.TryGetValue(out var baseSurface) &&
            HasSamePresentationIdentity(baseSurface!.Key, key))
        {
            return true;
        }

        return _activeRole == ManagedPhotoSurfaceRole.Base &&
            _activeIdentityGeneration == _identityGeneration &&
            _activeKey is { } activeKey &&
            HasSamePresentationIdentity(activeKey, key);
    }

    private SharedResource<ManagedPhotoSurface>? ExactResource(ManagedPhotoKey key)
    {
        if (_detail is not null &&
            _detail.TryGetValue(out var detail) &&
            detail!.Key == key)
        {
            return _detail;
        }

        if (_base is not null &&
            _base.TryGetValue(out var baseSurface) &&
            baseSurface!.Key == key &&
            !IsUnderResolved(
                baseSurface,
                key,
                OrientationTransform.GetOrientedSize(key.EncodedSize, key.Orientation)))
        {
            return _base;
        }

        return null;
    }

    private ManagedPhotoSurface? FirstAvailableSurface()
    {
        if (_detail is not null && _detail.TryGetValue(out var detail))
        {
            return detail;
        }

        return _base is not null && _base.TryGetValue(out var baseSurface)
            ? baseSurface
            : null;
    }

    private static bool HasSamePresentationIdentity(ManagedPhotoKey left, ManagedPhotoKey right) =>
        HasSameSourceIdentity(left, right) &&
        left.DestinationIdentity == right.DestinationIdentity;

    private static bool HasSameSourceIdentity(ManagedPhotoKey left, ManagedPhotoKey right) =>
        left.ImageIdentity == right.ImageIdentity &&
        left.EncodedSize == right.EncodedSize &&
        left.Orientation == right.Orientation;

    private static long RetainedBytes(SharedResource<ManagedPhotoSurface>? resource) =>
        resource is not null && resource.TryGetValue(out var surface)
            ? surface!.RetainedBytes
            : 0;

    private readonly record struct ManagedPhotoWork(
        ManagedPhotoRenderRequest Request,
        long Generation,
        long IdentityGeneration,
        long RequestedTimestamp,
        ManagedPhotoPendingReason PendingReason);

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
