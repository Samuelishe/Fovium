using System.Diagnostics;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Imaging;

internal sealed class SkiaImageDecoder : IImageLoader<DecodedImage>
{
    private readonly SemaphoreSlim _decodeSlots = new(2, 2);

    public async Task<ImageLoadResult<DecodedImage>> LoadAsync(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        await _decodeSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => Load(path, allowance, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _decodeSlots.Release();
        }
    }

    private static ImageLoadResult<DecodedImage> Load(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                return Failure(ImageLoadErrorKind.Missing, "The source file does not exist.");
            }

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
                    return Failure(
                        creationResult == SKCodecResult.Unimplemented
                            ? ImageLoadErrorKind.Unsupported
                            : ImageLoadErrorKind.Corrupt,
                        $"SKCodec probe failed: {creationResult}.");
                }

                if (!ImageFormatCapabilities.TryGetDetected(codec.EncodedFormat, out var detectedCapability) ||
                    detectedCapability is null)
                {
                    return Failure(
                        ImageLoadErrorKind.Unsupported,
                        $"Fovium does not support detected format {codec.EncodedFormat}.");
                }

                formatCapability = detectedCapability;

                var info = codec.Info;
                encodedSize = new PixelSize(info.Width, info.Height);
                if (!encodedSize.IsValid)
                {
                    return Failure(ImageLoadErrorKind.Corrupt, "The image reports invalid dimensions.");
                }

                orientation = ToExifOrientation(codec.EncodedOrigin);
                orientedSize = OrientationTransform.GetOrientedSize(encodedSize, orientation);
                frameCount = Math.Max(codec.FrameCount, 1);
                if (!ImageFormatCapabilities.SupportsFrameCount(formatCapability, frameCount))
                {
                    return Failure(
                        ImageLoadErrorKind.Unsupported,
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
                    ImageLoadErrorKind.ResourceLimit,
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
                return Failure(ImageLoadErrorKind.Corrupt, $"SKCodec decode open failed: {decodeCreationResult}.");
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
                    return Failure(ImageLoadErrorKind.Corrupt, $"SKCodec decode failed: {decodeResult}.");
                }

                decodeWatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                var preparationWatch = Stopwatch.StartNew();
                image = SKImage.FromBitmap(bitmap);
                if (image is null)
                {
                    return Failure(ImageLoadErrorKind.DecodeFailed, "Skia could not prepare a drawable image.");
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
                return ImageLoadResult<DecodedImage>.Success(
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            return Failure(ImageLoadErrorKind.Missing, exception.Message, exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            return Failure(ImageLoadErrorKind.Missing, exception.Message, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ImageLoadErrorKind.DecodeFailed, "Access to the source was denied.", exception);
        }
        catch (OverflowException exception)
        {
            return Failure(ImageLoadErrorKind.ResourceLimit, "Decoded resource estimation overflowed.", exception);
        }
        catch (IOException exception)
        {
            return Failure(ImageLoadErrorKind.DecodeFailed, exception.Message, exception);
        }
        catch (InvalidOperationException exception)
        {
            return Failure(ImageLoadErrorKind.DecodeFailed, exception.Message, exception);
        }
    }

    private static ImageLoadResult<DecodedImage> Failure(
        ImageLoadErrorKind kind,
        string detail,
        Exception? exception = null) =>
        ImageLoadResult<DecodedImage>.Failure(new ImageLoadError(kind, detail, exception));

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
