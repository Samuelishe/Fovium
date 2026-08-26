using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Rendering;

namespace Fovium.Viewer;

internal interface IPresentedImageSource
{
    event EventHandler? PresentedImageChanged;

    bool TryAcquirePresentedImage(out PresentedImageLease? image);
}

internal sealed class PhotoSampleRequestedEventArgs(
    PresentedImageLease image,
    PixelPoint orientedPixel) : EventArgs
{
    public PresentedImageLease Image { get; } = image;

    public PixelPoint OrientedPixel { get; } = orientedPixel;
}

internal sealed class PresentedImageLease : IDisposable
{
    private SharedResourceLease<DecodedImage>? _lease;

    public PresentedImageLease(SharedResourceLease<DecodedImage> lease, string presentationIdentity)
    {
        _lease = lease ?? throw new ArgumentNullException(nameof(lease));
        PresentationIdentity = string.IsNullOrWhiteSpace(presentationIdentity)
            ? throw new ArgumentException("A presentation identity is required.", nameof(presentationIdentity))
            : presentationIdentity;
    }

    public string PresentationIdentity { get; }

    public DecodedImage Image => GetLease().Value;

    public long ImageIdentity => Image.Identity;

    public PresentedImageLease Acquire() => new(GetLease().Acquire(), PresentationIdentity);

    public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();

    private SharedResourceLease<DecodedImage> GetLease() =>
        Volatile.Read(ref _lease) ?? throw new ObjectDisposedException(nameof(PresentedImageLease));
}
