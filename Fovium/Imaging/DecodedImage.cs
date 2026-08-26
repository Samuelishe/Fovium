using Fovium.Loading;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Imaging;

internal enum SourceColorState
{
    AssumedSrgb,
    NormalizedSrgb,
    NormalizedSrgbFromNclx,
    NormalizedNonSrgb,
    EmbeddedProfileUnpreserved,
}

internal sealed record ImageDescriptor(
    string SourcePath,
    ImageFormatId EncodedFormat,
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
    TimeSpan PreparationDuration,
    string? SourceColorDescription = null,
    TimeSpan? PixelCopyDuration = null);

internal sealed class DecodedImage : IRetainedResource
{
    private static long _nextIdentity;

    internal sealed class NativePayload : IDisposable
    {
        public NativePayload(SKBitmap bitmap, SKImage image)
        {
            Bitmap = bitmap;
            Image = image;
        }

        public SKBitmap Bitmap { get; }

        public SKImage Image { get; }

        public void Dispose()
        {
            Image.Dispose();
            Bitmap.Dispose();
        }
    }

    private readonly SharedResource<NativePayload> _native;
    private readonly object _ownershipSync = new();
    private SharedResource<PreparedAmbient>? _ambient;
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
        Identity = Interlocked.Increment(ref _nextIdentity);
    }

    public byte[] EncodedSource { get; }

    public ImageDescriptor Descriptor { get; }

    public long Identity { get; }

    public long RetainedBytes
    {
        get
        {
            lock (_ownershipSync)
            {
                var ambientBytes = _ambient is not null && _ambient.TryGetValue(out var ambient)
                    ? ambient!.RetainedBytes
                    : 0;
                return checked(Descriptor.EstimatedRetainedBytes + ambientBytes);
            }
        }
    }

    public bool HasAmbient
    {
        get
        {
            lock (_ownershipSync)
            {
                return _ambient is not null;
            }
        }
    }

    public bool HasAmbientForBlur(double blur)
    {
        lock (_ownershipSync)
        {
            return _ambient is not null &&
                _ambient.TryGetValue(out var ambient) &&
                ambient is not null &&
                ambient.Blur.Equals(blur);
        }
    }

    public RenderLease AcquireRenderLease()
    {
        lock (_ownershipSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new RenderLease(_native.Acquire());
        }
    }

    public PixelLease AcquirePixelLease()
    {
        lock (_ownershipSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new PixelLease(_native.Acquire());
        }
    }

    public bool TryAttachAmbient(PreparedAmbient ambient)
    {
        ArgumentNullException.ThrowIfNull(ambient);
        lock (_ownershipSync)
        {
            if (_disposed || _ambient is not null)
            {
                return false;
            }

            _ambient = new SharedResource<PreparedAmbient>(ambient);
            return true;
        }
    }

    public bool TrySetAmbient(PreparedAmbient ambient)
    {
        ArgumentNullException.ThrowIfNull(ambient);
        SharedResource<PreparedAmbient>? release;
        lock (_ownershipSync)
        {
            if (_disposed)
            {
                return false;
            }

            release = _ambient;
            _ambient = new SharedResource<PreparedAmbient>(ambient);
        }

        release?.ReleaseOwner();
        return true;
    }

    public AmbientLease? TryAcquireAmbient()
    {
        lock (_ownershipSync)
        {
            return _disposed || _ambient is null
                ? null
                : new AmbientLease(_ambient.Acquire());
        }
    }

    public bool RemoveAmbient(PreparedAmbient expected)
    {
        SharedResource<PreparedAmbient>? release;
        lock (_ownershipSync)
        {
            if (_ambient is null || !_ambient.References(expected))
            {
                return false;
            }

            release = _ambient;
            _ambient = null;
        }

        release.ReleaseOwner();
        return true;
    }

    public void Dispose()
    {
        SharedResource<PreparedAmbient>? ambient;
        lock (_ownershipSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ambient = _ambient;
            _ambient = null;
        }

        ambient?.ReleaseOwner();
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

    internal sealed class PixelLease : IDisposable
    {
        private SharedResourceLease<NativePayload>? _lease;

        internal PixelLease(SharedResourceLease<NativePayload> lease)
        {
            _lease = lease;
        }

        public int Width => GetBitmap().Width;

        public int Height => GetBitmap().Height;

        public int RowBytes => GetBitmap().RowBytes;

        public SKColorType ColorType => GetBitmap().ColorType;

        public SKAlphaType AlphaType => GetBitmap().AlphaType;

        public ReadOnlySpan<byte> PixelBytes => GetBitmap().GetPixelSpan();

        public unsafe bool TryReadSrgbUnpremultiplied(
            int sourceX,
            int sourceY,
            Span<byte> destinationBgra)
        {
            if (destinationBgra.Length < 4 ||
                sourceX < 0 || sourceX >= Width ||
                sourceY < 0 || sourceY >= Height)
            {
                return false;
            }

            using var srgb = SKColorSpace.CreateSrgb();
            var targetInfo = new SKImageInfo(
                1,
                1,
                SKColorType.Bgra8888,
                SKAlphaType.Unpremul,
                srgb);
            fixed (byte* destination = destinationBgra)
            {
                return GetLease().Value.Image.ReadPixels(
                    targetInfo,
                    (IntPtr)destination,
                    4,
                    sourceX,
                    sourceY);
            }
        }

        public PixelLease Acquire() => new(GetLease().Acquire());

        public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();

        private SKBitmap GetBitmap() => GetLease().Value.Bitmap;

        private SharedResourceLease<NativePayload> GetLease() =>
            Volatile.Read(ref _lease) ?? throw new ObjectDisposedException(nameof(PixelLease));
    }

    internal sealed class AmbientLease : IDisposable
    {
        private SharedResourceLease<PreparedAmbient>? _lease;

        internal AmbientLease(SharedResourceLease<PreparedAmbient> lease)
        {
            _lease = lease;
        }

        public SKImage Image => GetLease().Value.Image;

        public PixelSize Size => GetLease().Value.Size;

        public long RetainedBytes => GetLease().Value.RetainedBytes;

        public double Blur => GetLease().Value.Blur;

        public AmbientLease Acquire() => new(GetLease().Acquire());

        public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();

        private SharedResourceLease<PreparedAmbient> GetLease() =>
            Volatile.Read(ref _lease) ?? throw new ObjectDisposedException(nameof(AmbientLease));
    }
}
