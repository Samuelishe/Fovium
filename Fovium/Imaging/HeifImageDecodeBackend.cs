using System.Diagnostics;
using System.Runtime.InteropServices;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Imaging;

internal sealed class HeifImageDecodeBackend : IImageDecodeBackend, IDisposable
{
    private const uint HevcItemType = 0x68766331; // hvc1
    private const uint Av1ItemType = 0x61763031; // av01
    private const uint DerivedImageReferenceType = 0x64696D67; // dimg
    private const uint NclxColorProfileType = 0x6E636C78; // nclx
    private const int MaximumReferencedItems = 1024;
    private const int MaximumReferenceGroupsPerItem = 64;
    private const int MaximumNativeImageDimension = 32768;
    private const nuint MaximumIccProfileBytes = 16 * 1024 * 1024;
    private const int HeifColorspaceRgb = 1;
    private const int HeifChromaInterleavedRgba = 11;
    private const int HeifChannelInterleaved = 10;
    private const int PqTransfer = 16;
    private const int HlgTransfer = 18;

    private readonly LibHeifRuntimeLocator _runtimeLocator;
    private readonly object _runtimeSync = new();
    private LibHeifRuntimeAvailability? _runtimeAvailability;
    private bool _disposed;

    public HeifImageDecodeBackend(LibHeifRuntimeLocator? runtimeLocator = null)
    {
        _runtimeLocator = runtimeLocator ?? new LibHeifRuntimeLocator();
    }

    internal string? LoadedNativeLibraryPath => GetRuntimeAvailability().Runtime?.LoadedLibraryPath;

    internal string RuntimeTechnicalDetail => GetRuntimeAvailability().TechnicalDetail;

    public unsafe ImageDecodeBackendResult Decode(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var probeWatch = Stopwatch.StartNew();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var encodedLength = stream.Length;
        if (encodedLength <= 0)
        {
            return ImageDecodeBackendResult.NotMyFormat();
        }

        var probeLength = checked((int)Math.Min(encodedLength, IsoBmffFileTypeProbe.MaximumProbeBytes));
        var probeBuffer = new byte[probeLength];
        stream.ReadExactly(probeBuffer);
        cancellationToken.ThrowIfCancellationRequested();
        var fileType = IsoBmffFileTypeProbe.Probe(probeBuffer);
        if (fileType.Kind == IsoBmffProbeKind.NotRecognized)
        {
            return ImageDecodeBackendResult.NotMyFormat();
        }

        if (fileType.Kind == IsoBmffProbeKind.Malformed)
        {
            return Failure(ImageDecodeBackendResultKind.Corrupt, "The ISO-BMFF file-type box is malformed.");
        }

        if (fileType.Kind == IsoBmffProbeKind.Sequence)
        {
            return Failure(
                ImageDecodeBackendResultKind.UnsupportedVariant,
                "HEIF/AVIF sequences and animation are not supported; Fovium displays static primary images only.");
        }

        if (encodedLength > allowance.MaximumWorkingBytes || encodedLength > allowance.MaximumRetainedBytes)
        {
            return Failure(
                ImageDecodeBackendResultKind.ResourceLimit,
                $"Encoded source size {encodedLength} exceeds the available decode allowance.");
        }

        if (encodedLength > Array.MaxLength)
        {
            return Failure(
                ImageDecodeBackendResultKind.ResourceLimit,
                $"Encoded source size {encodedLength} exceeds the managed buffer limit.");
        }

        var availability = GetRuntimeAvailability();
        if (availability.Runtime is null)
        {
            return Failure(ImageDecodeBackendResultKind.BackendUnavailable, availability.TechnicalDetail);
        }

        var encodedSource = new byte[checked((int)encodedLength)];
        probeBuffer.CopyTo(encodedSource, 0);
        stream.ReadExactly(encodedSource.AsSpan(probeLength));
        cancellationToken.ThrowIfCancellationRequested();

        var runtime = availability.Runtime;
        var api = runtime.Api;
        nint context = 0;
        nint handle = 0;
        fixed (byte* encodedPointer = encodedSource)
        {
            try
            {
                context = api.ContextAlloc();
                if (context == 0)
                {
                    return Failure(ImageDecodeBackendResultKind.ResourceLimit, "libheif could not allocate a decode context.");
                }

                // Keep upstream security defaults and make Fovium's accepted maximum dimension explicit.
                api.ContextSetMaximumImageSize(context, MaximumNativeImageDimension);
                api.ContextSetMaximumThreads(context, 0);
                var readError = api.ContextReadMemory(
                    context,
                    (nint)encodedPointer,
                    checked((nuint)encodedSource.LongLength),
                    nint.Zero);
                if (!readError.IsSuccess)
                {
                    return NativeFailure(readError, "libheif container probe");
                }

                var topLevelCount = api.ContextGetTopLevelCount(context);
                if (topLevelCount <= 0)
                {
                    return Failure(ImageDecodeBackendResultKind.Corrupt, "The container has no valid top-level primary image.");
                }

                if (topLevelCount > 1)
                {
                    return Failure(
                        ImageDecodeBackendResultKind.UnsupportedVariant,
                        $"Fovium requires one unambiguous primary still image; the container reports {topLevelCount} top-level images.");
                }

                var primaryError = api.ContextGetPrimaryHandle(context, out handle);
                if (!primaryError.IsSuccess || handle == 0)
                {
                    return NativeFailure(primaryError, "libheif primary-image probe");
                }

                var format = DetectPrimaryFormat(api, context, handle);
                if (format is null)
                {
                    return Failure(
                        ImageDecodeBackendResultKind.UnsupportedVariant,
                        "The primary item is not an HEVC-compressed HEIF image or an AV1-compressed AVIF image.");
                }

                var orientedWidth = api.ImageHandleGetWidth(handle);
                var orientedHeight = api.ImageHandleGetHeight(handle);
                if (!TryCreateSize(orientedWidth, orientedHeight, out var orientedSize))
                {
                    return Failure(ImageDecodeBackendResultKind.Corrupt, "The primary image reports invalid presentation dimensions.");
                }

                // The native output already includes container rotation, mirror, and
                // clean-aperture transforms. ISPE can expose codec padding (for example
                // 64 x 64 for a 16 x 12 HEVC image), so the normal-oriented Fovium
                // descriptor deliberately uses the truthful presentation raster size.
                var encodedSize = orientedSize;

                var lumaBits = api.ImageHandleGetLumaBits(handle);
                var chromaBits = api.ImageHandleGetChromaBits(handle);
                if (lumaBits <= 0)
                {
                    return Failure(
                        ImageDecodeBackendResultKind.UnsupportedVariant,
                        "The primary source precision could not be established; Fovium's current pipeline is limited to 8-bit input.");
                }

                var maximumSourceBits = Math.Max(lumaBits, chromaBits > 0 ? chromaBits : lumaBits);
                if (maximumSourceBits > 8)
                {
                    return Failure(
                        ImageDecodeBackendResultKind.UnsupportedVariant,
                        $"The primary source uses {maximumSourceBits}-bit samples; Fovium's current pipeline is limited to 8-bit input and does not quantize higher precision.");
                }

                var colorEvidence = ReadColorEvidence(api, handle);
                if (colorEvidence.TransferCharacteristics is PqTransfer or HlgTransfer)
                {
                    var transferName = colorEvidence.TransferCharacteristics == PqTransfer ? "PQ / ST 2084" : "HLG";
                    return Failure(
                        ImageDecodeBackendResultKind.UnsupportedVariant,
                        $"The primary image is signaled as {transferName} HDR; R7-C is SDR-only and does not tone-map HDR input.");
                }

                var estimatedWorkingBytes = DecodeMemoryEstimator.EstimateWorkingBytes(
                    orientedSize.Width,
                    orientedSize.Height,
                    encodedSource.LongLength);
                var estimatedRetainedBytes = DecodeMemoryEstimator.EstimateRetainedBytes(
                    orientedSize.Width,
                    orientedSize.Height,
                    encodedSource.LongLength);
                if (estimatedWorkingBytes > allowance.MaximumWorkingBytes ||
                    estimatedRetainedBytes > allowance.MaximumRetainedBytes)
                {
                    return Failure(
                        ImageDecodeBackendResultKind.ResourceLimit,
                        $"Estimated working/retained bytes {estimatedWorkingBytes}/{estimatedRetainedBytes} " +
                        $"exceed allowance {allowance.MaximumWorkingBytes}/{allowance.MaximumRetainedBytes}.");
                }

                probeWatch.Stop();
                cancellationToken.ThrowIfCancellationRequested();
                return DecodePixels(
                    path,
                    encodedSource,
                    format.Value,
                    encodedSize,
                    orientedSize,
                    maximumSourceBits,
                    api.ImageHandleHasAlpha(handle) != 0,
                    colorEvidence,
                    estimatedWorkingBytes,
                    estimatedRetainedBytes,
                    probeWatch.Elapsed,
                    api,
                    handle,
                    cancellationToken);
            }
            catch (OverflowException exception)
            {
                return Failure(
                    ImageDecodeBackendResultKind.ResourceLimit,
                    "HEIF/AVIF dimensions or native plane sizes exceed safe numeric limits.",
                    exception);
            }
            finally
            {
                if (handle != 0)
                {
                    api.ImageHandleRelease(handle);
                }

                if (context != 0)
                {
                    api.ContextFree(context);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_runtimeSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _runtimeAvailability?.Runtime?.Dispose();
        }
    }

    private static unsafe ImageDecodeBackendResult DecodePixels(
        string path,
        byte[] encodedSource,
        ImageFormatId format,
        PixelSize encodedSize,
        PixelSize orientedSize,
        int sourceBits,
        bool hasAlpha,
        ColorEvidence colorEvidence,
        long estimatedWorkingBytes,
        long estimatedRetainedBytes,
        TimeSpan probeDuration,
        LibHeifNativeApi api,
        nint handle,
        CancellationToken cancellationToken)
    {
        var decodeWatch = Stopwatch.StartNew();
        var decodeError = api.DecodeImage(
            handle,
            out var nativeImage,
            HeifColorspaceRgb,
            HeifChromaInterleavedRgba,
            nint.Zero);
        if (!decodeError.IsSuccess || nativeImage == 0)
        {
            return NativeFailure(decodeError, "libheif pixel decode");
        }

        try
        {
            decodeWatch.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            var preparationWatch = Stopwatch.StartNew();
            // libheif 1.23.1 normalizes NCLX-signaled output to sRGB when decoding
            // with default options. An ICC profile is attached only when NCLX is
            // absent, so source ICC bytes are never used to mis-tag normalized pixels.
            using var embeddedColorSpace = colorEvidence.HasNclx
                ? null
                : TryCreateColorSpace(colorEvidence.IccProfile);
            using var assumedSrgb = embeddedColorSpace is null ? SKColorSpace.CreateSrgb() : null;
            var targetInfo = new SKImageInfo(
                orientedSize.Width,
                orientedSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul,
                embeddedColorSpace ?? assumedSrgb);
            var bitmap = new SKBitmap(targetInfo);
            SKImage? image = null;
            var ownershipTransferred = false;
            try
            {
                if (bitmap.GetPixels() == nint.Zero)
                {
                    return Failure(ImageDecodeBackendResultKind.ResourceLimit, "Skia could not allocate the final image raster.");
                }

                var plane = api.ImageGetPlaneReadonly(nativeImage, HeifChannelInterleaved, out var sourceStride);
                var sourceRowBytes = checked((nuint)orientedSize.Width * 4);
                if (plane == 0 || sourceStride < sourceRowBytes || sourceStride > int.MaxValue)
                {
                    return Failure(ImageDecodeBackendResultKind.Corrupt, "libheif returned an invalid interleaved RGBA plane.");
                }

                _ = checked(sourceStride * (nuint)orientedSize.Height);
                var destination = bitmap.GetPixelSpan();
                var destinationStride = bitmap.RowBytes;
                var requiredDestinationBytes = checked(destinationStride * orientedSize.Height);
                if (destinationStride < checked(orientedSize.Width * 4) || destination.Length < requiredDestinationBytes)
                {
                    return Failure(ImageDecodeBackendResultKind.DecodeFailed, "Skia returned an invalid BGRA raster layout.");
                }

                var nativePremultiplied = api.ImageIsPremultipliedAlpha(nativeImage) != 0;
                preparationWatch.Stop();
                var copyWatch = Stopwatch.StartNew();
                CopyRgbaToPremultipliedBgra(
                    (byte*)plane,
                    sourceStride,
                    destination,
                    destinationStride,
                    orientedSize.Width,
                    orientedSize.Height,
                    hasAlpha,
                    nativePremultiplied);
                copyWatch.Stop();

                preparationWatch.Start();
                cancellationToken.ThrowIfCancellationRequested();
                image = SKImage.FromBitmap(bitmap);
                if (image is null)
                {
                    return Failure(ImageDecodeBackendResultKind.DecodeFailed, "Skia could not prepare a drawable HEIF/AVIF image.");
                }

                preparationWatch.Stop();
                var colorState = colorEvidence.HasNclx
                    ? SourceColorState.NormalizedSrgbFromNclx
                    : colorEvidence.IccProfilePresent
                    ? embeddedColorSpace is null
                        ? SourceColorState.EmbeddedProfileUnpreserved
                        : embeddedColorSpace.IsSrgb
                            ? SourceColorState.NormalizedSrgb
                            : SourceColorState.NormalizedNonSrgb
                    : colorEvidence.HasUnpreservedColorProfile
                        ? SourceColorState.EmbeddedProfileUnpreserved
                        : SourceColorState.AssumedSrgb;
                var descriptor = new ImageDescriptor(
                    Path.GetFullPath(path),
                    format,
                    encodedSize,
                    orientedSize,
                    ExifOrientation.Normal,
                    1,
                    colorState,
                    ReducedDecodeAdvertised: false,
                    $"{targetInfo.ColorType}/{targetInfo.AlphaType}",
                    estimatedWorkingBytes,
                    estimatedRetainedBytes,
                    probeDuration,
                    decodeWatch.Elapsed,
                    preparationWatch.Elapsed,
                    colorEvidence.Describe(sourceBits),
                    copyWatch.Elapsed);

                ownershipTransferred = true;
                return ImageDecodeBackendResult.Success(new DecodedImage(encodedSource, descriptor, bitmap, image));
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
        finally
        {
            api.ImageRelease(nativeImage);
        }
    }

    private static unsafe void CopyRgbaToPremultipliedBgra(
        byte* source,
        nuint sourceStride,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        bool hasAlpha,
        bool sourcePremultiplied)
    {
        for (var y = 0; y < height; y++)
        {
            var sourceOffset = checked(sourceStride * (nuint)y);
            var sourceRow = new ReadOnlySpan<byte>(source + checked((nint)sourceOffset), checked(width * 4));
            var destinationRow = destination.Slice(checked(y * destinationStride), checked(width * 4));
            for (var x = 0; x < width; x++)
            {
                var offset = x * 4;
                var red = sourceRow[offset];
                var green = sourceRow[offset + 1];
                var blue = sourceRow[offset + 2];
                var alpha = hasAlpha ? sourceRow[offset + 3] : byte.MaxValue;
                if (!sourcePremultiplied && alpha != byte.MaxValue)
                {
                    red = Premultiply(red, alpha);
                    green = Premultiply(green, alpha);
                    blue = Premultiply(blue, alpha);
                }

                destinationRow[offset] = blue;
                destinationRow[offset + 1] = green;
                destinationRow[offset + 2] = red;
                destinationRow[offset + 3] = alpha;
            }
        }
    }

    private LibHeifRuntimeAvailability GetRuntimeAvailability()
    {
        lock (_runtimeSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _runtimeAvailability ??= _runtimeLocator.TryLoad();
        }
    }

    private static unsafe ImageFormatId? DetectPrimaryFormat(
        LibHeifNativeApi api,
        nint context,
        nint handle)
    {
        var pending = new Queue<uint>();
        var visited = new HashSet<uint>();
        pending.Enqueue(api.ImageHandleGetItemId(handle));
        ImageFormatId? detectedFormat = null;

        while (pending.Count > 0 && visited.Count < MaximumReferencedItems)
        {
            var itemId = pending.Dequeue();
            if (!visited.Add(itemId))
            {
                continue;
            }

            var itemFormat = api.ItemGetItemType(context, itemId) switch
            {
                HevcItemType => ImageFormatId.Heif,
                Av1ItemType => ImageFormatId.Avif,
                _ => (ImageFormatId?)null,
            };
            if (itemFormat is not null)
            {
                if (detectedFormat is not null && detectedFormat != itemFormat)
                {
                    return null;
                }

                detectedFormat = itemFormat;
                continue;
            }

            for (var index = 0; index < MaximumReferenceGroupsPerItem; index++)
            {
                var referenceCount = api.ContextGetItemReferences(
                    context,
                    itemId,
                    index,
                    out var referenceType,
                    out var references);
                if (referenceCount == 0)
                {
                    break;
                }

                try
                {
                    if (references == 0 || referenceCount > MaximumReferencedItems)
                    {
                        return null;
                    }

                    if (referenceType != DerivedImageReferenceType)
                    {
                        continue;
                    }

                    var referencedItems = new ReadOnlySpan<uint>(
                        (void*)references,
                        checked((int)referenceCount));
                    foreach (var referencedItem in referencedItems)
                    {
                        if (visited.Count + pending.Count >= MaximumReferencedItems)
                        {
                            return null;
                        }

                        pending.Enqueue(referencedItem);
                    }
                }
                finally
                {
                    if (references != 0)
                    {
                        api.ReleaseItemReferences(context, ref references);
                    }
                }
            }
        }

        return pending.Count == 0 ? detectedFormat : null;
    }

    private static unsafe ColorEvidence ReadColorEvidence(LibHeifNativeApi api, nint handle)
    {
        var profileType = api.ImageHandleGetColorProfileType(handle);
        byte[]? iccProfile = null;
        var iccSize = api.ImageHandleGetRawProfileSize(handle);
        var iccPresent = iccSize > 0;
        if (iccSize is > 0 and <= MaximumIccProfileBytes && iccSize <= int.MaxValue)
        {
            iccProfile = new byte[(int)iccSize];
            fixed (byte* profilePointer = iccProfile)
            {
                var profileError = api.ImageHandleGetRawProfile(handle, (nint)profilePointer);
                if (!profileError.IsSuccess)
                {
                    iccProfile = null;
                }
            }
        }

        var nclxError = api.ImageHandleGetNclx(handle, out var nclxPointer);
        if (!nclxError.IsSuccess || nclxPointer == 0)
        {
            return new ColorEvidence(
                iccProfile,
                iccPresent,
                false,
                profileType == NclxColorProfileType,
                null,
                null,
                null,
                null);
        }

        try
        {
            var nclx = Marshal.PtrToStructure<LibHeifNclxProfile>(nclxPointer);
            return new ColorEvidence(
                iccProfile,
                iccPresent,
                true,
                false,
                nclx.ColorPrimaries,
                nclx.TransferCharacteristics,
                nclx.MatrixCoefficients,
                nclx.FullRangeFlag != 0);
        }
        finally
        {
            api.NclxProfileFree(nclxPointer);
        }
    }

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

    private static bool TryCreateSize(int width, int height, out PixelSize size)
    {
        size = new PixelSize(width, height);
        return size.IsValid && width <= MaximumNativeImageDimension && height <= MaximumNativeImageDimension;
    }

    private static byte Premultiply(byte value, byte alpha) =>
        (byte)(((value * alpha) + 127) / byte.MaxValue);

    private static ImageDecodeBackendResult NativeFailure(LibHeifError error, string operation)
    {
        if (error.IsSuccess)
        {
            return Failure(ImageDecodeBackendResultKind.DecodeFailed, $"{operation} returned no native object.");
        }

        var kind = error.Subcode == 6003
            ? ImageDecodeBackendResultKind.BackendUnavailable
            : error.Subcode == 1000 || error.Code == 6
                ? ImageDecodeBackendResultKind.ResourceLimit
                : error.Code is 3 or 4
                    ? ImageDecodeBackendResultKind.UnsupportedVariant
                    : error.Code == 2
                        ? ImageDecodeBackendResultKind.Corrupt
                        : ImageDecodeBackendResultKind.DecodeFailed;
        return Failure(kind, $"{operation} failed: {error.Detail} (native {error.Code}/{error.Subcode}).");
    }

    private static ImageDecodeBackendResult Failure(
        ImageDecodeBackendResultKind kind,
        string detail,
        Exception? exception = null) =>
        ImageDecodeBackendResult.Failure(kind, detail, exception);

    private sealed record ColorEvidence(
        byte[]? IccProfile,
        bool IccProfilePresent,
        bool HasNclx,
        bool HasUnpreservedColorProfile,
        int? Primaries,
        int? TransferCharacteristics,
        int? MatrixCoefficients,
        bool? FullRange)
    {
        public string Describe(int sourceBits)
        {
            var profile = IccProfilePresent
                ? IccProfile is null ? "ICC present but not preserved" : $"ICC {IccProfile.Length} bytes"
                : "ICC absent";
            var nclx = HasNclx
                ? $"NCLX primaries={Primaries}, transfer={TransferCharacteristics}, matrix={MatrixCoefficients}, full-range={FullRange}"
                : "NCLX absent";
            return $"Source depth {sourceBits}-bit; {profile}; {nclx}.";
        }
    }
}
