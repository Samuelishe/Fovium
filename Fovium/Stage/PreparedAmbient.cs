using Fovium.Loading;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Stage;

internal sealed class PreparedAmbient : IRetainedResource
{
    private SKImage? _image;

    public PreparedAmbient(SKImage image, PixelSize size, TimeSpan preparationDuration)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        Size = size.IsValid ? size : throw new ArgumentOutOfRangeException(nameof(size));
        PreparationDuration = preparationDuration;
        RetainedBytes = checked((long)size.Width * size.Height * 4);
    }

    public SKImage Image =>
        Volatile.Read(ref _image) ?? throw new ObjectDisposedException(nameof(PreparedAmbient));

    public PixelSize Size { get; }

    public TimeSpan PreparationDuration { get; }

    public long RetainedBytes { get; }

    public void Dispose() => Interlocked.Exchange(ref _image, null)?.Dispose();
}
