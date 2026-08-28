using Fovium.Loading;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.PhotoStyling;

internal sealed class PreparedColorWash : IRetainedResource
{
    private SKImage? _image;

    public PreparedColorWash(SKImage image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        Size = new PixelSize(image.Width, image.Height);
        RetainedBytes = checked((long)image.Width * image.Height * 4);
    }

    public SKImage Image =>
        Volatile.Read(ref _image) ?? throw new ObjectDisposedException(nameof(PreparedColorWash));

    public PixelSize Size { get; }

    public long RetainedBytes { get; }

    public void Dispose() => Interlocked.Exchange(ref _image, null)?.Dispose();
}
