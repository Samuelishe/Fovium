using System.Diagnostics;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Fovium.RenderProbe;

internal static class ProbeImageFactory
{
    public static Task<ProbeImage> LoadFileAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() => LoadFile(path, cancellationToken), cancellationToken);

    public static ProbeImage CreatePattern(PatternKind kind)
    {
        var stopwatch = Stopwatch.StartNew();
        var bitmap = PatternGenerator.Create(kind);
        SKImage? image = null;
        Bitmap? avaloniaBitmap = null;
        var ownershipTransferred = false;
        try
        {
            image = SKImage.FromBitmap(bitmap) ?? throw new InvalidOperationException("Skia could not create an image.");
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("Skia could not encode the generated pattern.");
            using var stream = encoded.AsStream();
            avaloniaBitmap = new Bitmap(stream);
            stopwatch.Stop();

            var size = new ImageSize(bitmap.Width, bitmap.Height);
            var diagnostics = new DecodeDiagnostics(
                $"Generated: {kind}",
                "Project-generated BGRA pattern",
                size,
                size,
                ExifOrientation.Normal,
                "Generated",
                bitmap.ColorType.ToString(),
                bitmap.AlphaType.ToString(),
                "Generated in sRGB",
                false,
                false,
                1,
                DecodeMemoryEstimator.EstimateBytes(size.Width, size.Height),
                0,
                0,
                0,
                stopwatch.Elapsed.TotalMilliseconds);

            ownershipTransferred = true;
            return new ProbeImage(avaloniaBitmap, bitmap, image, diagnostics, null);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                avaloniaBitmap?.Dispose();
                image?.Dispose();
                bitmap.Dispose();
            }
        }
    }

    private static ProbeImage LoadFile(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var headerWatch = Stopwatch.StartNew();
        using var codecStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var codec = SKCodec.Create(codecStream, out var creationResult);
        if (codec is null)
        {
            throw new InvalidDataException($"Skia could not probe the image ({creationResult}).");
        }

        var sourceInfo = codec.Info;
        var encodedSize = new ImageSize(sourceInfo.Width, sourceInfo.Height);
        var orientation = ToExifOrientation(codec.EncodedOrigin);
        var orientedSize = OrientationTransform.GetOrientedSize(encodedSize, orientation);
        var estimatedBytes = DecodeMemoryEstimator.EstimateBytes(encodedSize.Width, encodedSize.Height);
        if (estimatedBytes > DecodeMemoryEstimator.ProbeWorkingSetCapBytes)
        {
            throw new InvalidDataException(
                $"R0 safety guard rejected an estimated {estimatedBytes:N0}-byte decode " +
                $"(cap {DecodeMemoryEstimator.ProbeWorkingSetCapBytes:N0} bytes).");
        }

        using var sourceColorSpace = sourceInfo.ColorSpace;
        var colorState = DescribeColorSpace(sourceColorSpace);
        var reducedDimensions = codec.GetScaledDimensions(0.5f);
        var reducedDecodeAdvertised = reducedDimensions.Width < encodedSize.Width ||
                                      reducedDimensions.Height < encodedSize.Height;
        headerWatch.Stop();

        cancellationToken.ThrowIfCancellationRequested();
        var decodeWatch = Stopwatch.StartNew();
        using var assumedSrgb = sourceColorSpace is null ? SKColorSpace.CreateSrgb() : null;
        var targetInfo = new SKImageInfo(
            encodedSize.Width,
            encodedSize.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            sourceColorSpace ?? assumedSrgb);
        var skiaBitmap = new SKBitmap(targetInfo);
        Bitmap? avaloniaBitmap = null;
        SKImage? skiaImage = null;
        var ownershipTransferred = false;
        try
        {
            var decodeResult = codec.GetPixels(targetInfo, skiaBitmap.GetPixels());
            if (decodeResult is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
            {
                throw new InvalidDataException($"Skia decode failed ({decodeResult}).");
            }

            decodeWatch.Stop();

            cancellationToken.ThrowIfCancellationRequested();
            var avaloniaWatch = Stopwatch.StartNew();
            var encodedBytes = File.ReadAllBytes(path);
            using (var avaloniaStream = new MemoryStream(encodedBytes, writable: false))
            {
                avaloniaBitmap = new Bitmap(avaloniaStream);
            }

            avaloniaWatch.Stop();

            var preparationWatch = Stopwatch.StartNew();
            skiaImage = SKImage.FromBitmap(skiaBitmap);
            if (skiaImage is null)
            {
                throw new InvalidDataException("Skia could not prepare a drawable image.");
            }

            preparationWatch.Stop();

            var diagnostics = new DecodeDiagnostics(
                Path.GetFileName(path),
                "Avalonia Bitmap + Skia SKCodec (separate R0 comparison decodes)",
                encodedSize,
                orientedSize,
                orientation,
                codec.EncodedFormat.ToString(),
                targetInfo.ColorType.ToString(),
                targetInfo.AlphaType.ToString(),
                colorState,
                false,
                reducedDecodeAdvertised,
                codec.FrameCount,
                estimatedBytes,
                headerWatch.Elapsed.TotalMilliseconds,
                decodeWatch.Elapsed.TotalMilliseconds,
                avaloniaWatch.Elapsed.TotalMilliseconds,
                preparationWatch.Elapsed.TotalMilliseconds);

            ownershipTransferred = true;
            return new ProbeImage(avaloniaBitmap, skiaBitmap, skiaImage, diagnostics, encodedBytes);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                skiaImage?.Dispose();
                avaloniaBitmap?.Dispose();
                skiaBitmap.Dispose();
            }
        }
    }

    private static string DescribeColorSpace(SKColorSpace? colorSpace)
    {
        if (colorSpace is null)
        {
            return "Absent; R0 display policy assumes sRGB";
        }

        using var equivalentProfile = colorSpace.ToProfile();
        var profileDescription = equivalentProfile is null
            ? "no equivalent ICC export"
            : $"equivalent ICC export {equivalentProfile.Size:N0} bytes";
        return colorSpace.IsSrgb
            ? $"Skia normalized sRGB; {profileDescription}"
            : $"Skia normalized non-sRGB color space; {profileDescription}";
    }

    private static ExifOrientation ToExifOrientation(SKEncodedOrigin origin) =>
        origin switch
        {
            SKEncodedOrigin.TopLeft => ExifOrientation.Normal,
            SKEncodedOrigin.TopRight => ExifOrientation.MirrorHorizontal,
            SKEncodedOrigin.BottomRight => ExifOrientation.Rotate180,
            SKEncodedOrigin.BottomLeft => ExifOrientation.MirrorVertical,
            SKEncodedOrigin.LeftTop => ExifOrientation.Transpose,
            SKEncodedOrigin.RightTop => ExifOrientation.Rotate90,
            SKEncodedOrigin.RightBottom => ExifOrientation.Transverse,
            SKEncodedOrigin.LeftBottom => ExifOrientation.Rotate270,
            _ => ExifOrientation.Normal,
        };
}
