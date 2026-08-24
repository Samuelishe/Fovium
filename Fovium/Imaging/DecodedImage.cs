using Fovium.Loading;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Imaging;

internal enum SourceColorState
{
    AssumedSrgb,
    NormalizedSrgb,
    NormalizedNonSrgb,
}

internal sealed record ImageDescriptor(
    string SourcePath,
    string EncodedFormat,
    PixelSize EncodedSize,
    PixelSize OrientedSize,
    ExifOrientation Orientation,
    int FrameCount,
    SourceColorState ColorState,
    bool ReducedDecodeAdvertised,
    string PixelFormat,
    long EstimatedWorkingBytes,
    long EstimatedRetainedBytes,
    TimeSpan ProbeDuration,
    TimeSpan DecodeDuration,
    TimeSpan PreparationDuration);

internal sealed class DecodedImage : IRetainedResource
{
    internal sealed class NativePayload(SKBitmap bitmap, SKImage image) : IDisposable
    {
        public SKImage Image { get; } = image;

        public void Dispose()
        {
            Image.Dispose();
            bitmap.Dispose();
        }
    }

    private readonly SharedResource<NativePayload> _native;
    private bool _disposed;

    public DecodedImage(
        byte[] encodedSource,
        ImageDescriptor descriptor,
        SKBitmap bitmap,
        SKImage image)
    {
        EncodedSource = encodedSource ?? throw new ArgumentNullException(nameof(encodedSource));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _native = new SharedResource<NativePayload>(new NativePayload(bitmap, image));
    }

    public byte[] EncodedSource { get; }

    public ImageDescriptor Descriptor { get; }

    public long RetainedBytes => Descriptor.EstimatedRetainedBytes;

    public RenderLease AcquireRenderLease()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new RenderLease(_native.Acquire());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _native.ReleaseOwner();
    }

    internal sealed class RenderLease : IDisposable
    {
        private SharedResourceLease<NativePayload>? _lease;

        internal RenderLease(SharedResourceLease<NativePayload> lease)
        {
            _lease = lease;
        }

        public SKImage Image => GetLease().Value.Image;

        public RenderLease Acquire() => new(GetLease().Acquire());

        public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();

        private SharedResourceLease<NativePayload> GetLease() =>
            Volatile.Read(ref _lease) ?? throw new ObjectDisposedException(nameof(RenderLease));
    }
}
