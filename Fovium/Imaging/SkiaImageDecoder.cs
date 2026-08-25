using System.Diagnostics;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Imaging;

internal sealed class SkiaImageDecodeBackend : IImageDecodeBackend
{
    public ImageDecodeBackendResult Decode(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var probeWatch = Stopwatch.StartNew();
        PixelSize encodedSize;
        PixelSize orientedSize;
        ExifOrientation orientation;
        ImageFormatCapability formatCapability;
        int frameCount;
        SourceColorState colorState;
        bool reducedDecodeAdvertised;
        long encodedLength;
        long estimatedWorkingBytes;
        long estimatedRetainedBytes;

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            encodedLength = stream.Length;
            using var codec = SKCodec.Create(stream, out var creationResult);
            if (codec is null)
            {
                return creationResult == SKCodecResult.Unimplemented
                    ? ImageDecodeBackendResult.NotMyFormat()
                    : Failure(
                    ImageDecodeBackendResultKind.Corrupt,
                    $"SKCodec probe failed: {creationResult}.");
            }

            if (!ImageFormatCapabilities.TryGetDetected(codec.EncodedFormat, out var detectedCapability) ||
                detectedCapability is null)
            {
                return Failure(
                    ImageDecodeBackendResultKind.UnsupportedVariant,
                    $"Fovium does not support detected format {codec.EncodedFormat}.");
            }

            formatCapability = detectedCapability;

            var info = codec.Info;
            encodedSize = new PixelSize(info.Width, info.Height);
            if (!encodedSize.IsValid)
            {
                return Failure(ImageDecodeBackendResultKind.Corrupt, "The image reports invalid dimensions.");
            }

            orientation = ToExifOrientation(codec.EncodedOrigin);
            orientedSize = OrientationTransform.GetOrientedSize(encodedSize, orientation);
            frameCount = Math.Max(codec.FrameCount, 1);
            if (!ImageFormatCapabilities.SupportsFrameCount(formatCapability, frameCount))
            {
                return Failure(
                    ImageDecodeBackendResultKind.UnsupportedVariant,
                    $"Animated or multi-frame {formatCapability.DisplayName} is not supported yet.");
            }
            using var sourceColorSpace = info.ColorSpace;
            colorState = sourceColorSpace is null
                ? SourceColorState.AssumedSrgb
                : sourceColorSpace.IsSrgb
                    ? SourceColorState.NormalizedSrgb
                    : SourceColorState.NormalizedNonSrgb;
            var reducedDimensions = codec.GetScaledDimensions(0.5f);
            reducedDecodeAdvertised = reducedDimensions.Width < encodedSize.Width ||
                                      reducedDimensions.Height < encodedSize.Height;
            estimatedWorkingBytes = DecodeMemoryEstimator.EstimateWorkingBytes(
                encodedSize.Width,
                encodedSize.Height,
                encodedLength);
            estimatedRetainedBytes = DecodeMemoryEstimator.EstimateRetainedBytes(
                encodedSize.Width,
                encodedSize.Height,
                encodedLength);
        }

        probeWatch.Stop();
        if (estimatedWorkingBytes > allowance.MaximumWorkingBytes ||
            estimatedRetainedBytes > allowance.MaximumRetainedBytes)
        {
            return Failure(
                ImageDecodeBackendResultKind.ResourceLimit,
                $"Estimated working/retained bytes {estimatedWorkingBytes}/{estimatedRetainedBytes} " +
                $"exceed allowance {allowance.MaximumWorkingBytes}/{allowance.MaximumRetainedBytes}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var encodedSource = File.ReadAllBytes(path);
        cancellationToken.ThrowIfCancellationRequested();

        var decodeWatch = Stopwatch.StartNew();
        using var decodeStream = new MemoryStream(encodedSource, writable: false);
        using var decodeCodec = SKCodec.Create(decodeStream, out var decodeCreationResult);
        if (decodeCodec is null)
        {
            return Failure(ImageDecodeBackendResultKind.Corrupt, $"SKCodec decode open failed: {decodeCreationResult}.");
        }

        using var decodedColorSpace = decodeCodec.Info.ColorSpace;
        using var assumedSrgb = decodedColorSpace is null ? SKColorSpace.CreateSrgb() : null;
        var targetInfo = new SKImageInfo(
            encodedSize.Width,
            encodedSize.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            decodedColorSpace ?? assumedSrgb);
        var bitmap = new SKBitmap(targetInfo);
        SKImage? image = null;
        var ownershipTransferred = false;
        try
        {
            var decodeResult = decodeCodec.GetPixels(targetInfo, bitmap.GetPixels());
            if (decodeResult != SKCodecResult.Success)
            {
                return Failure(ImageDecodeBackendResultKind.Corrupt, $"SKCodec decode failed: {decodeResult}.");
            }

            decodeWatch.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            var preparationWatch = Stopwatch.StartNew();
            image = SKImage.FromBitmap(bitmap);
            if (image is null)
            {
                return Failure(ImageDecodeBackendResultKind.DecodeFailed, "Skia could not prepare a drawable image.");
            }

            preparationWatch.Stop();
            var descriptor = new ImageDescriptor(
                Path.GetFullPath(path),
                formatCapability.Id,
                encodedSize,
                orientedSize,
                orientation,
                frameCount,
                colorState,
                reducedDecodeAdvertised,
                $"{targetInfo.ColorType}/{targetInfo.AlphaType}",
                estimatedWorkingBytes,
                estimatedRetainedBytes,
                probeWatch.Elapsed,
                decodeWatch.Elapsed,
                preparationWatch.Elapsed);

            ownershipTransferred = true;
            return ImageDecodeBackendResult.Success(
                new DecodedImage(encodedSource, descriptor, bitmap, image));
        }
        finally
        {
            if (!ownershipTransferred)
            {
                image?.Dispose();
                bitmap.Dispose();
            }
        }
    }

    private static ImageDecodeBackendResult Failure(
        ImageDecodeBackendResultKind kind,
        string detail,
        Exception? exception = null) =>
        ImageDecodeBackendResult.Failure(kind, detail, exception);

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
