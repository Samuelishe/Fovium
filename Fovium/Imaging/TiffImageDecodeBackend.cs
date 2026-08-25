using System.Diagnostics;
using BitMiracle.LibTiff.Classic;
using Fovium.Rendering;
using SkiaSharp;
using TiffOrientation = BitMiracle.LibTiff.Classic.Orientation;

namespace Fovium.Imaging;

internal enum TiffSignature
{
    NotTiff,
    ClassicLittleEndian,
    ClassicBigEndian,
    BigTiffLittleEndian,
    BigTiffBigEndian,
}

internal static class TiffSignatureSniffer
{
    public const int SignatureLength = 4;

    public static TiffSignature Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < SignatureLength)
        {
            return TiffSignature.NotTiff;
        }

        if (bytes[0] == (byte)'I' && bytes[1] == (byte)'I')
        {
            return bytes[2] switch
            {
                42 when bytes[3] == 0 => TiffSignature.ClassicLittleEndian,
                43 when bytes[3] == 0 => TiffSignature.BigTiffLittleEndian,
                _ => TiffSignature.NotTiff,
            };
        }

        if (bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0)
        {
            return bytes[3] switch
            {
                42 => TiffSignature.ClassicBigEndian,
                43 => TiffSignature.BigTiffBigEndian,
                _ => TiffSignature.NotTiff,
            };
        }

        return TiffSignature.NotTiff;
    }
}

internal sealed class TiffImageDecodeBackend : IImageDecodeBackend
{
    private enum AlphaEncoding
    {
        None,
        Associated,
        Unassociated,
    }

    private sealed record DecodePlan(
        PixelSize EncodedSize,
        PixelSize OrientedSize,
        ExifOrientation Orientation,
        int SamplesPerPixel,
        Photometric Photometric,
        Compression Compression,
        AlphaEncoding Alpha,
        bool IsTiled,
        byte[]? IccProfile);

    private static readonly HashSet<Compression> SupportedCompressions =
    [
        Compression.NONE,
        Compression.LZW,
        Compression.DEFLATE,
        Compression.ADOBE_DEFLATE,
        Compression.PACKBITS,
    ];

    private static readonly Lazy<bool> DiagnosticHandlerInstalled = new(
        static () =>
        {
            Tiff.SetErrorHandler(new FoviumTiffErrorHandler());
            return true;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    public ImageDecodeBackendResult Decode(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureDiagnosticHandler();
            cancellationToken.ThrowIfCancellationRequested();

            Span<byte> signatureBytes = stackalloc byte[TiffSignatureSniffer.SignatureLength];
            using (var signatureStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete))
            {
                if (signatureStream.ReadAtLeast(
                        signatureBytes,
                        signatureBytes.Length,
                        throwOnEndOfStream: false) < signatureBytes.Length)
                {
                    return ImageDecodeBackendResult.NotMyFormat();
                }
            }

            var signature = TiffSignatureSniffer.Detect(signatureBytes);
            if (signature == TiffSignature.NotTiff)
            {
                return ImageDecodeBackendResult.NotMyFormat();
            }

            if (signature is TiffSignature.BigTiffLittleEndian or TiffSignature.BigTiffBigEndian)
            {
                return Failure(
                    ImageDecodeBackendResultKind.UnsupportedVariant,
                    "BigTIFF is not supported by the current TIFF backend.");
            }

            var probeWatch = Stopwatch.StartNew();
            DecodePlan plan;
            long encodedLength;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                encodedLength = stream.Length;
                using var tiff = Tiff.ClientOpen(path, "r", stream, new TiffStream());
                if (tiff is null)
                {
                    return Failure(ImageDecodeBackendResultKind.Corrupt, "The TIFF header or directory is corrupt.");
                }

                var planResult = TryCreatePlan(tiff);
                if (planResult.Error is not null)
                {
                    return planResult.Error;
                }

                plan = planResult.Plan!;
            }

            probeWatch.Stop();
            var estimatedWorkingBytes = DecodeMemoryEstimator.EstimateWorkingBytes(
                plan.EncodedSize.Width,
                plan.EncodedSize.Height,
                encodedLength);
            var estimatedRetainedBytes = DecodeMemoryEstimator.EstimateRetainedBytes(
                plan.EncodedSize.Width,
                plan.EncodedSize.Height,
                encodedLength);
            var allowanceFailure = CheckAllowance(
                allowance,
                estimatedWorkingBytes,
                estimatedRetainedBytes);
            if (allowanceFailure is not null)
            {
                return allowanceFailure;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var encodedSource = File.ReadAllBytes(path);
            cancellationToken.ThrowIfCancellationRequested();

            using var memory = new MemoryStream(encodedSource, writable: false);
            using var decodedTiff = Tiff.ClientOpen("retained TIFF", "r", memory, new TiffStream());
            if (decodedTiff is null)
            {
                return Failure(ImageDecodeBackendResultKind.Corrupt, "The retained TIFF data could not be reopened.");
            }

            var retainedPlanResult = TryCreatePlan(decodedTiff);
            if (retainedPlanResult.Error is not null)
            {
                return retainedPlanResult.Error;
            }

            plan = retainedPlanResult.Plan!;
            estimatedWorkingBytes = DecodeMemoryEstimator.EstimateWorkingBytes(
                plan.EncodedSize.Width,
                plan.EncodedSize.Height,
                encodedSource.LongLength);
            estimatedRetainedBytes = DecodeMemoryEstimator.EstimateRetainedBytes(
                plan.EncodedSize.Width,
                plan.EncodedSize.Height,
                encodedSource.LongLength);
            allowanceFailure = CheckAllowance(allowance, estimatedWorkingBytes, estimatedRetainedBytes);
            if (allowanceFailure is not null)
            {
                return allowanceFailure;
            }

            using var decodedColorSpace = TryCreateColorSpace(plan.IccProfile);
            using var assumedSrgb = decodedColorSpace is null ? SKColorSpace.CreateSrgb() : null;
            var colorState = plan.IccProfile is null
                ? SourceColorState.AssumedSrgb
                : decodedColorSpace is null
                    ? SourceColorState.EmbeddedProfileUnpreserved
                    : decodedColorSpace.IsSrgb
                        ? SourceColorState.NormalizedSrgb
                        : SourceColorState.NormalizedNonSrgb;
            var targetInfo = new SKImageInfo(
                plan.EncodedSize.Width,
                plan.EncodedSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul,
                decodedColorSpace ?? assumedSrgb);
            var bitmap = new SKBitmap(targetInfo);
            SKImage? image = null;
            var ownershipTransferred = false;
            try
            {
                var decodeWatch = Stopwatch.StartNew();
                var decoded = plan.IsTiled
                    ? DecodeTiles(decodedTiff, plan, bitmap, cancellationToken)
                    : DecodeScanlines(decodedTiff, plan, bitmap, cancellationToken);
                if (!decoded)
                {
                    return Failure(ImageDecodeBackendResultKind.Corrupt, "TIFF pixel decoding did not complete.");
                }

                decodeWatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                var preparationWatch = Stopwatch.StartNew();
                image = SKImage.FromBitmap(bitmap);
                if (image is null)
                {
                    return Failure(ImageDecodeBackendResultKind.DecodeFailed, "Skia could not prepare a drawable TIFF image.");
                }

                preparationWatch.Stop();
                var descriptor = new ImageDescriptor(
                    Path.GetFullPath(path),
                    ImageFormatId.Tiff,
                    plan.EncodedSize,
                    plan.OrientedSize,
                    plan.Orientation,
                    1,
                    colorState,
                    false,
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not (
            OutOfMemoryException or
            StackOverflowException or
            IOException or
            UnauthorizedAccessException or
            OverflowException))
        {
            return Failure(
                ImageDecodeBackendResultKind.Corrupt,
                "The recognized TIFF could not be parsed safely.",
                exception);
        }
    }

    private static (DecodePlan? Plan, ImageDecodeBackendResult? Error) TryCreatePlan(Tiff tiff)
    {
        var directoryCount = tiff.NumberOfDirectories();
        if (directoryCount <= 0)
        {
            return (null, Failure(
                ImageDecodeBackendResultKind.Corrupt,
                "The TIFF contains no readable image directory."));
        }

        if (directoryCount != 1)
        {
            return (null, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                "Multi-page TIFF is not supported yet."));
        }

        if (!TryGetInt(tiff, TiffTag.IMAGEWIDTH, false, out var width) ||
            !TryGetInt(tiff, TiffTag.IMAGELENGTH, false, out var height) ||
            width <= 0 ||
            height <= 0)
        {
            return (null, Failure(ImageDecodeBackendResultKind.Corrupt, "The TIFF reports invalid dimensions."));
        }

        if (!TryGetInt(tiff, TiffTag.BITSPERSAMPLE, true, out var bitsPerSample))
        {
            return (null, Failure(ImageDecodeBackendResultKind.Corrupt, "The TIFF omits BitsPerSample."));
        }

        if (!TryGetInt(tiff, TiffTag.SAMPLEFORMAT, true, out var sampleFormatValue))
        {
            sampleFormatValue = (int)SampleFormat.UINT;
        }

        var sampleFormat = (SampleFormat)sampleFormatValue;
        if (sampleFormat != SampleFormat.UINT)
        {
            var detail = sampleFormat == SampleFormat.IEEEFP
                ? "Floating-point TIFF is not supported by the current 8-bit rendering pipeline."
                : $"TIFF sample format {sampleFormat} is not supported.";
            return (null, Failure(ImageDecodeBackendResultKind.UnsupportedVariant, detail));
        }

        if (bitsPerSample != 8)
        {
            return (null, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                "High-bit-depth TIFF is not supported by the current 8-bit rendering pipeline."));
        }

        if (!TryGetInt(tiff, TiffTag.SAMPLESPERPIXEL, true, out var samplesPerPixel) ||
            !TryGetInt(tiff, TiffTag.PHOTOMETRIC, false, out var photometricValue) ||
            !TryGetInt(tiff, TiffTag.COMPRESSION, true, out var compressionValue) ||
            !TryGetInt(tiff, TiffTag.PLANARCONFIG, true, out var planarValue))
        {
            return (null, Failure(ImageDecodeBackendResultKind.Corrupt, "The TIFF omits required pixel-layout fields."));
        }

        if ((PlanarConfig)planarValue != PlanarConfig.CONTIG)
        {
            return (null, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                "Planar-separated TIFF is not supported yet."));
        }

        var photometric = (Photometric)photometricValue;
        if (photometric is not (Photometric.RGB or Photometric.MINISBLACK or Photometric.MINISWHITE))
        {
            return (null, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                $"TIFF photometric model {photometric} is not supported."));
        }

        var compression = (Compression)compressionValue;
        if (!SupportedCompressions.Contains(compression))
        {
            return (null, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                $"TIFF compression {compression} is not supported by the current product scope."));
        }

        var baseSamples = photometric == Photometric.RGB ? 3 : 1;
        var alphaResult = TryGetAlpha(tiff, samplesPerPixel, baseSamples);
        if (alphaResult.Error is not null)
        {
            return (null, alphaResult.Error);
        }

        if (!TryGetInt(tiff, TiffTag.ORIENTATION, true, out var orientationValue))
        {
            orientationValue = (int)TiffOrientation.TOPLEFT;
        }

        if (!TryMapOrientation((TiffOrientation)orientationValue, out var orientation))
        {
            return (null, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                $"TIFF orientation {orientationValue} is not supported."));
        }

        var encodedSize = new PixelSize(width, height);
        var iccFields = tiff.GetField(TiffTag.ICCPROFILE);
        var iccProfile = iccFields is { Length: >= 2 } && iccFields[1].Value is byte[] profileBytes
            ? profileBytes
            : null;
        return (new DecodePlan(
            encodedSize,
            OrientationTransform.GetOrientedSize(encodedSize, orientation),
            orientation,
            samplesPerPixel,
            photometric,
            compression,
            alphaResult.Alpha,
            tiff.IsTiled(),
            iccProfile), null);
    }

    private static (AlphaEncoding Alpha, ImageDecodeBackendResult? Error) TryGetAlpha(
        Tiff tiff,
        int samplesPerPixel,
        int baseSamples)
    {
        if (samplesPerPixel == baseSamples)
        {
            return (AlphaEncoding.None, null);
        }

        if (samplesPerPixel != baseSamples + 1)
        {
            return (default, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                "TIFF extra samples are not a supported alpha layout."));
        }

        var fields = tiff.GetField(TiffTag.EXTRASAMPLES);
        if (fields is null || fields.Length < 2 || fields[1].Value is not ExtraSample[] extras || extras.Length != 1)
        {
            return (default, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                "TIFF extra sample is not explicitly declared as alpha."));
        }

        return extras[0] switch
        {
            ExtraSample.ASSOCALPHA => (AlphaEncoding.Associated, null),
            ExtraSample.UNASSALPHA => (AlphaEncoding.Unassociated, null),
            _ => (default, Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                "TIFF extra sample is not a supported alpha representation.")),
        };
    }

    private static bool DecodeScanlines(
        Tiff tiff,
        DecodePlan plan,
        SKBitmap bitmap,
        CancellationToken cancellationToken)
    {
        var sourceRow = new byte[tiff.ScanlineSize()];
        var requiredRowBytes = checked(plan.EncodedSize.Width * plan.SamplesPerPixel);
        if (sourceRow.Length < requiredRowBytes)
        {
            return false;
        }

        var destination = bitmap.GetPixelSpan();
        for (var row = 0; row < plan.EncodedSize.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!tiff.ReadScanline(sourceRow, row))
            {
                return false;
            }

            ConvertPixels(
                sourceRow.AsSpan(0, requiredRowBytes),
                destination.Slice(row * bitmap.RowBytes, plan.EncodedSize.Width * 4),
                plan,
                plan.EncodedSize.Width);
        }

        return true;
    }

    private static bool DecodeTiles(
        Tiff tiff,
        DecodePlan plan,
        SKBitmap bitmap,
        CancellationToken cancellationToken)
    {
        if (!TryGetInt(tiff, TiffTag.TILEWIDTH, false, out var tileWidth) ||
            !TryGetInt(tiff, TiffTag.TILELENGTH, false, out var tileHeight) ||
            tileWidth <= 0 ||
            tileHeight <= 0)
        {
            return false;
        }

        var tile = new byte[tiff.TileSize()];
        var tileRowBytes = checked(tileWidth * plan.SamplesPerPixel);
        if (tile.Length < checked(tileRowBytes * tileHeight))
        {
            return false;
        }

        var destination = bitmap.GetPixelSpan();
        for (var tileY = 0; tileY < plan.EncodedSize.Height; tileY += tileHeight)
        {
            for (var tileX = 0; tileX < plan.EncodedSize.Width; tileX += tileWidth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tiff.ReadTile(tile, 0, tileX, tileY, 0, 0) < 0)
                {
                    return false;
                }

                var copyWidth = Math.Min(tileWidth, plan.EncodedSize.Width - tileX);
                var copyHeight = Math.Min(tileHeight, plan.EncodedSize.Height - tileY);
                for (var localY = 0; localY < copyHeight; localY++)
                {
                    ConvertPixels(
                        tile.AsSpan(localY * tileRowBytes, copyWidth * plan.SamplesPerPixel),
                        destination.Slice(
                            ((tileY + localY) * bitmap.RowBytes) + (tileX * 4),
                            copyWidth * 4),
                        plan,
                        copyWidth);
                }
            }
        }

        return true;
    }

    private static void ConvertPixels(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        DecodePlan plan,
        int pixelCount)
    {
        var sourceOffset = 0;
        var destinationOffset = 0;
        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            byte red;
            byte green;
            byte blue;
            if (plan.Photometric == Photometric.RGB)
            {
                red = source[sourceOffset];
                green = source[sourceOffset + 1];
                blue = source[sourceOffset + 2];
            }
            else
            {
                var gray = source[sourceOffset];
                if (plan.Photometric == Photometric.MINISWHITE)
                {
                    gray = (byte)(byte.MaxValue - gray);
                }

                red = gray;
                green = gray;
                blue = gray;
            }

            var alpha = plan.Alpha == AlphaEncoding.None
                ? byte.MaxValue
                : source[sourceOffset + plan.SamplesPerPixel - 1];
            if (plan.Alpha == AlphaEncoding.Unassociated)
            {
                red = Premultiply(red, alpha);
                green = Premultiply(green, alpha);
                blue = Premultiply(blue, alpha);
            }
            else if (alpha == 0)
            {
                red = 0;
                green = 0;
                blue = 0;
            }

            destination[destinationOffset] = blue;
            destination[destinationOffset + 1] = green;
            destination[destinationOffset + 2] = red;
            destination[destinationOffset + 3] = alpha;
            sourceOffset += plan.SamplesPerPixel;
            destinationOffset += 4;
        }
    }

    private static byte Premultiply(byte value, byte alpha) =>
        (byte)(((value * alpha) + 127) / byte.MaxValue);

    private static SKColorSpace? TryCreateColorSpace(byte[]? iccProfile)
    {
        if (iccProfile is null)
        {
            return null;
        }

        try
        {
            return SKColorSpace.CreateIcc(iccProfile);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool TryGetInt(Tiff tiff, TiffTag tag, bool useDefault, out int value)
    {
        var fields = useDefault ? tiff.GetFieldDefaulted(tag) : tiff.GetField(tag);
        if (fields is null || fields.Length == 0)
        {
            value = 0;
            return false;
        }

        value = fields[0].ToInt();
        return true;
    }

    private static bool TryMapOrientation(TiffOrientation value, out ExifOrientation orientation)
    {
        orientation = value switch
        {
            TiffOrientation.TOPLEFT => ExifOrientation.Normal,
            TiffOrientation.TOPRIGHT => ExifOrientation.MirrorHorizontal,
            TiffOrientation.BOTRIGHT => ExifOrientation.Rotate180,
            TiffOrientation.BOTLEFT => ExifOrientation.MirrorVertical,
            TiffOrientation.LEFTTOP => ExifOrientation.Transpose,
            TiffOrientation.RIGHTTOP => ExifOrientation.Rotate90,
            TiffOrientation.RIGHTBOT => ExifOrientation.Transverse,
            TiffOrientation.LEFTBOT => ExifOrientation.Rotate270,
            _ => default,
        };
        return value is >= TiffOrientation.TOPLEFT and <= TiffOrientation.LEFTBOT;
    }

    private static ImageDecodeBackendResult? CheckAllowance(
        ImageLoadAllowance allowance,
        long workingBytes,
        long retainedBytes) =>
        workingBytes > allowance.MaximumWorkingBytes || retainedBytes > allowance.MaximumRetainedBytes
            ? Failure(
                ImageDecodeBackendResultKind.ResourceLimit,
                $"Estimated working/retained bytes {workingBytes}/{retainedBytes} " +
                $"exceed allowance {allowance.MaximumWorkingBytes}/{allowance.MaximumRetainedBytes}.")
            : null;

    private static ImageDecodeBackendResult Failure(
        ImageDecodeBackendResultKind kind,
        string detail,
        Exception? exception = null) =>
        ImageDecodeBackendResult.Failure(kind, detail, exception);

    private static void EnsureDiagnosticHandler()
    {
        _ = DiagnosticHandlerInstalled.Value;
    }

    private sealed class FoviumTiffErrorHandler : TiffErrorHandler
    {
        public override void ErrorHandler(Tiff tif, string method, string format, params object[] args) =>
            WriteDiagnostic("error", method, format, args);

        public override void ErrorHandlerExt(
            Tiff tif,
            object clientData,
            string method,
            string format,
            params object[] args) =>
            WriteDiagnostic("error", method, format, args);

        public override void WarningHandler(Tiff tif, string method, string format, params object[] args) =>
            WriteDiagnostic("warning", method, format, args);

        public override void WarningHandlerExt(
            Tiff tif,
            object clientData,
            string method,
            string format,
            params object[] args) =>
            WriteDiagnostic("warning", method, format, args);

        [Conditional("DEBUG")]
        private static void WriteDiagnostic(string severity, string method, string format, object[] args)
        {
            var detail = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args);
            Debug.WriteLine($"TIFF {severity} in {method}: {detail}");
        }
    }
}
