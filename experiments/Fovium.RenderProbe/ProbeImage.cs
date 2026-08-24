using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Fovium.RenderProbe;

internal sealed record DecodeDiagnostics(
    string Source,
    string DecoderPath,
    ImageSize EncodedSize,
    ImageSize OrientedSize,
    ExifOrientation Orientation,
    string EncodedFormat,
    string PixelFormat,
    string AlphaType,
    string ColorState,
    bool RawEmbeddedProfileAvailable,
    bool ReducedDecodeAdvertised,
    int FrameCount,
    long EstimatedWorkingBytes,
    double HeaderMilliseconds,
    double SkiaDecodeMilliseconds,
    double AvaloniaDecodeMilliseconds,
    double PreparationMilliseconds);

internal sealed class ProbeImage : IDisposable
{
    private bool _disposed;

    public ProbeImage(
        Bitmap avaloniaBitmap,
        SKBitmap skiaBitmap,
        SKImage skiaImage,
        DecodeDiagnostics diagnostics,
        byte[]? encodedSource)
    {
        AvaloniaBitmap = avaloniaBitmap;
        SkiaBitmap = skiaBitmap;
        SkiaImage = skiaImage;
        Diagnostics = diagnostics;
        EncodedSource = encodedSource;
    }

    public Bitmap AvaloniaBitmap { get; }

    public SKBitmap SkiaBitmap { get; }

    public SKImage SkiaImage { get; }

    public DecodeDiagnostics Diagnostics { get; }

    // Retained in R0 so a future profile extractor is not blocked by the decoder boundary.
    public byte[]? EncodedSource { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SkiaImage.Dispose();
        SkiaBitmap.Dispose();
        AvaloniaBitmap.Dispose();
    }
}
