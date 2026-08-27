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
        RectD destination,
        SKBitmap bitmap,
        SKImage image,
        TimeSpan sourceRenderDuration,
        TimeSpan transformDuration,
        TimeSpan finalizationDuration)
    {
        Key = key;
        Destination = destination;
        _bitmap = bitmap;
        _image = image;
        SourceRenderDuration = sourceRenderDuration;
        TransformDuration = transformDuration;
        FinalizationDuration = finalizationDuration;
    }

    public ManagedPhotoKey Key { get; }

    public RectD Destination { get; }

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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var referenceBitmap = RenderReferenceSrgb(request);
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
                request.Key.Geometry.VisiblePhotoBounds,
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

    private static SKBitmap RenderReferenceSrgb(ManagedPhotoRenderRequest request)
    {
        var geometry = request.Key.Geometry;
        var visible = geometry.VisiblePhotoBounds;
        var pixelWidth = Math.Max(1, checked((int)Math.Ceiling(visible.Width * geometry.RenderScaling)));
        var pixelHeight = Math.Max(1, checked((int)Math.Ceiling(visible.Height * geometry.RenderScaling)));
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
                (float)((geometry.PhotoDestination.X - visible.X) * geometry.RenderScaling + affine.C * scaleX),
                (float)(affine.D * scaleY),
                (float)(affine.E * scaleY),
                (float)((geometry.PhotoDestination.Y - visible.Y) * geometry.RenderScaling + affine.F * scaleY),
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
    TimeSpan LastFinalizationDuration);

internal sealed class ManagedPhotoPresentationCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly IManagedPhotoRenderer _renderer;
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
    private bool _active;
    private bool _disposed;

    public ManagedPhotoPresentationCoordinator(IManagedPhotoRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
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
                    _lastFinalizationDuration);
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
            replaced = _pending;
            if (replaced is not null)
            {
                _coalescedRequests++;
            }

            _pending = request;
            if (!_active)
            {
                _active = true;
                _worker = Task.Run(Process);
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

    public void Clear()
    {
        ManagedPhotoRenderRequest? pending;
        SharedResource<ManagedPhotoSurface>? current;
        lock (_sync)
        {
            _generation++;
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
}
