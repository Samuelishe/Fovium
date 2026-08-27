using System.Runtime.InteropServices;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.ColorManagement;

internal sealed class ManagedPhotoSource : IDisposable
{
    private SKBitmap? _bitmap;
    private SKImage? _image;

    public ManagedPhotoSource(
        ManagedPhotoKey key,
        SKBitmap bitmap,
        SKImage image,
        TimeSpan sourceReadDuration,
        TimeSpan transformDuration,
        TimeSpan finalizationDuration)
    {
        Key = key;
        _bitmap = bitmap;
        _image = image;
        SourceReadDuration = sourceReadDuration;
        TransformDuration = transformDuration;
        FinalizationDuration = finalizationDuration;
    }

    public ManagedPhotoKey Key { get; }

    public SKImage Image => Volatile.Read(ref _image)
        ?? throw new ObjectDisposedException(nameof(ManagedPhotoSource));

    public PixelSize PixelSize => new(Image.Width, Image.Height);

    public long RetainedBytes => checked((long)Image.Width * Image.Height * 4);

    public TimeSpan SourceReadDuration { get; }

    public TimeSpan TransformDuration { get; }

    public TimeSpan FinalizationDuration { get; }

    internal byte[] CopyPixelBytes() => (_bitmap
        ?? throw new ObjectDisposedException(nameof(ManagedPhotoSource))).GetPixelSpan().ToArray();

    public void Dispose()
    {
        Interlocked.Exchange(ref _image, null)?.Dispose();
        Interlocked.Exchange(ref _bitmap, null)?.Dispose();
    }
}

internal sealed class ManagedPhotoSourceLease : IDisposable
{
    private SharedResourceLease<ManagedPhotoSource>? _source;

    public ManagedPhotoSourceLease(SharedResourceLease<ManagedPhotoSource> source)
    {
        _source = source;
    }

    public ManagedPhotoSource Source => Volatile.Read(ref _source)?.Value
        ?? throw new ObjectDisposedException(nameof(ManagedPhotoSourceLease));

    public ManagedPhotoSourceLease Acquire()
    {
        var source = Volatile.Read(ref _source)
            ?? throw new ObjectDisposedException(nameof(ManagedPhotoSourceLease));
        return new ManagedPhotoSourceLease(source.Acquire());
    }

    public void Dispose() => Interlocked.Exchange(ref _source, null)?.Dispose();
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
    ManagedPhotoSource Render(ManagedPhotoRenderRequest request);
}

internal sealed class ManagedPhotoPresentationEventArgs(ManagedPhotoKey key) : EventArgs
{
    public ManagedPhotoKey Key { get; } = key;
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

    public ManagedPhotoSource Render(ManagedPhotoRenderRequest request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        var size = request.Descriptor.EncodedSize;
        if (!size.IsValid || request.Source.Image.Width != size.Width || request.Source.Image.Height != size.Height)
        {
            throw new ArgumentException("Managed source dimensions must match the canonical encoded pixels.", nameof(request));
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var referenceBitmap = ReadFullSourceReferenceSrgb(request, size);
        var sourceReadDuration = stopwatch.Elapsed;

        var pixels = referenceBitmap.GetPixelSpan();
        var transform = GetTransform(request.Key.DestinationIdentity, request.DestinationProfile);
        // Little CMS 2.19 explicitly exercises same-format in-place transforms in its testbed.
        transform.Transform(pixels, pixels);
        var transformDuration = stopwatch.Elapsed - sourceReadDuration;

        var finalBitmap = new SKBitmap(new SKImageInfo(
            size.Width,
            size.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        try
        {
            Premultiply(pixels, finalBitmap.GetPixelSpan());
            var finalImage = SKImage.FromBitmap(finalBitmap)
                ?? throw new InvalidOperationException("Skia could not create the managed source image.");
            var finalizationDuration = stopwatch.Elapsed - sourceReadDuration - transformDuration;
            return new ManagedPhotoSource(
                request.Key,
                finalBitmap,
                finalImage,
                sourceReadDuration,
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

    private static SKBitmap ReadFullSourceReferenceSrgb(
        ManagedPhotoRenderRequest request,
        PixelSize size)
    {
        using var referenceColorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(
            size.Width,
            size.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul,
            referenceColorSpace);
        var bitmap = new SKBitmap(info);
        if (request.Source.Image.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
        {
            return bitmap;
        }

        bitmap.Dispose();
        throw new InvalidOperationException("Skia could not read the full source into reference sRGB.");
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
    TimeSpan LastSourceReadDuration,
    TimeSpan LastTransformDuration,
    TimeSpan LastFinalizationDuration,
    TimeSpan LastRequestToWorkerStartDuration,
    long SourceChanges,
    long DestinationChanges,
    long GeometryRequests,
    long ManagedSourceFrames,
    long MatteWithoutPhotoFrames,
    long AtomicPresentationCommits,
    TimeSpan LastAtomicPresentationWait,
    TimeSpan MaximumAtomicPresentationWait);

internal sealed class ManagedPhotoPresentationCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly IManagedPhotoRenderer _renderer;
    private ManagedPhotoRenderRequest? _pending;
    private SharedResource<ManagedPhotoSource>? _current;
    private long _generation;
    private long _pendingGeneration;
    private long _pendingTimestamp;
    private ManagedPhotoKey? _lastRequestedKey;
    private bool _active;
    private bool _disposed;
    private long _requests;
    private long _coalescedRequests;
    private long _completed;
    private long _staleResults;
    private long _failures;
    private long _maximumRasterBytes;
    private PixelSize _lastRasterSize;
    private TimeSpan _lastSourceReadDuration;
    private TimeSpan _lastTransformDuration;
    private TimeSpan _lastFinalizationDuration;
    private TimeSpan _lastRequestToWorkerStartDuration;
    private long _sourceChanges;
    private long _destinationChanges;
    private long _managedSourceFrames;
    private long _matteWithoutPhotoFrames;
    private long _atomicPresentationCommits;
    private TimeSpan _lastAtomicPresentationWait;
    private TimeSpan _maximumAtomicPresentationWait;
    private TaskCompletionSource _idleCompletion = CompletedIdleCompletion();

    public ManagedPhotoPresentationCoordinator(IManagedPhotoRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public event EventHandler<ManagedPhotoPresentationEventArgs>? PresentationChanged;

    public event EventHandler<ManagedPhotoPresentationEventArgs>? PresentationFailed;

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
                    RetainedBytes(_current),
                    _maximumRasterBytes,
                    _lastRasterSize,
                    _lastSourceReadDuration,
                    _lastTransformDuration,
                    _lastFinalizationDuration,
                    _lastRequestToWorkerStartDuration,
                    _sourceChanges,
                    _destinationChanges,
                    0,
                    _managedSourceFrames,
                    _matteWithoutPhotoFrames,
                    _atomicPresentationCommits,
                    _lastAtomicPresentationWait,
                    _maximumAtomicPresentationWait);
            }
        }
    }

    public void Request(ManagedPhotoRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ManagedPhotoRenderRequest? replaced;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _requests++;
            _generation++;
            RecordIdentityTransition(request.Key);
            _lastRequestedKey = request.Key;
            replaced = _pending;
            if (replaced is not null)
            {
                _coalescedRequests++;
            }

            _pending = request;
            _pendingGeneration = _generation;
            _pendingTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!_active)
            {
                _idleCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                StartWorkerLocked();
            }
        }

        replaced?.Dispose();
    }

    public bool TryAcquire(ManagedPhotoKey key, out ManagedPhotoSourceLease? source)
    {
        lock (_sync)
        {
            if (_disposed ||
                _current is null ||
                !_current.TryGetValue(out var current) ||
                current!.Key != key)
            {
                source = null;
                return false;
            }

            source = new ManagedPhotoSourceLease(_current.Acquire());
            return true;
        }
    }

    internal Task WaitForIdleAsync()
    {
        lock (_sync)
        {
            return _idleCompletion.Task;
        }
    }

    public void RecordManagedSourceFrame()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _managedSourceFrames++;
            }
        }
    }

    public void RecordMatteWithoutPhotoFrame()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _matteWithoutPhotoFrames++;
            }
        }
    }

    public void RecordAtomicPresentationCommit(TimeSpan wait)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _atomicPresentationCommits++;
            _lastAtomicPresentationWait = wait;
            _maximumAtomicPresentationWait = TimeSpan.FromTicks(Math.Max(
                _maximumAtomicPresentationWait.Ticks,
                wait.Ticks));
        }
    }

    public void Clear()
    {
        ManagedPhotoRenderRequest? pending;
        SharedResource<ManagedPhotoSource>? current;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _generation++;
            pending = _pending;
            _pending = null;
            current = _current;
            _current = null;
        }

        pending?.Dispose();
        current?.ReleaseOwner();
    }

    public void Dispose()
    {
        ManagedPhotoRenderRequest? pending;
        SharedResource<ManagedPhotoSource>? current;
        var disposeRenderer = false;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _generation++;
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
    }

    private void StartWorkerLocked()
    {
        var work = TakePendingLocked();
        if (work is null)
        {
            return;
        }

        _active = true;
        _ = Task.Run(() => Process(work.Value));
    }

    private void Process(ManagedPhotoWork initialWork)
    {
        var work = initialWork;
        while (true)
        {
            lock (_sync)
            {
                _lastRequestToWorkerStartDuration =
                    System.Diagnostics.Stopwatch.GetElapsedTime(work.RequestedTimestamp);
            }

            ManagedPhotoSource? result = null;
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

            SharedResource<ManagedPhotoSource>? previous = null;
            var changed = false;
            var failed = false;
            ManagedPhotoWork? next;
            var disposeRenderer = false;
            TaskCompletionSource? idleCompletion = null;
            lock (_sync)
            {
                var isCurrent = !_disposed && work.Generation == _generation;
                if (!isCurrent)
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
                    _current = new SharedResource<ManagedPhotoSource>(result);
                    _lastRasterSize = result.PixelSize;
                    _maximumRasterBytes = Math.Max(_maximumRasterBytes, result.RetainedBytes);
                    _lastSourceReadDuration = result.SourceReadDuration;
                    _lastTransformDuration = result.TransformDuration;
                    _lastFinalizationDuration = result.FinalizationDuration;
                    result = null;
                    _completed++;
                    changed = true;
                }

                next = TakePendingLocked();
                if (next is null)
                {
                    _active = false;
                    disposeRenderer = _disposed;
                    idleCompletion = _idleCompletion;
                }
            }

            result?.Dispose();
            previous?.ReleaseOwner();
            if (failed)
            {
                PresentationFailed?.Invoke(this, new ManagedPhotoPresentationEventArgs(work.Request.Key));
            }
            else if (changed)
            {
                PresentationChanged?.Invoke(this, new ManagedPhotoPresentationEventArgs(work.Request.Key));
            }

            idleCompletion?.TrySetResult();

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

    private ManagedPhotoWork? TakePendingLocked()
    {
        if (_pending is null)
        {
            return null;
        }

        var request = _pending;
        _pending = null;
        return new ManagedPhotoWork(request, _pendingGeneration, _pendingTimestamp);
    }

    private void RecordIdentityTransition(ManagedPhotoKey key)
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
    }

    private static long RetainedBytes(SharedResource<ManagedPhotoSource>? resource) =>
        resource is not null && resource.TryGetValue(out var source)
            ? source!.RetainedBytes
            : 0;

    private static TaskCompletionSource CompletedIdleCompletion()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        completion.SetResult();
        return completion;
    }

    private readonly record struct ManagedPhotoWork(
        ManagedPhotoRenderRequest Request,
        long Generation,
        long RequestedTimestamp);
}
