using System.Globalization;
using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Metadata;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.Imaging;

public sealed class HeifImageDecoderTests
{
    private const string RequireRuntimeEnvironmentVariable = "FOVIUM_REQUIRE_HEIF_TEST_RUNTIME";

    [Fact]
    public void UnrelatedContentDoesNotRequireOrInitializeTheNativeRuntime()
    {
        var result = DecodeWithMissingRuntime([0xFF, 0xD8, 0xFF, 0xE0], "renamed.heic");

        Assert.Equal(ImageDecodeBackendResultKind.NotMyFormat, result.Kind);
        Assert.Null(result.Image);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void UnrelatedContentOverHeifAllowanceStillFallsThroughByContent()
    {
        var result = DecodeWithMissingRuntime(
            [0xFF, 0xD8, 0xFF, 0xE0],
            "renamed.heic",
            new ImageLoadAllowance(1, 1, false));

        Assert.Equal(ImageDecodeBackendResultKind.NotMyFormat, result.Kind);
        Assert.Null(result.Image);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void EmptyInputIsNotClaimedAsHeifOrAvif()
    {
        var result = DecodeWithMissingRuntime([], "empty.avif");

        Assert.Equal(ImageDecodeBackendResultKind.NotMyFormat, result.Kind);
        Assert.Null(result.Image);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void RecognizedStillWithMissingRuntimeIsUnavailableRatherThanCorrupt()
    {
        var result = DecodeWithMissingRuntime(
            IsoBmffFileTypeProbeTests.CreateFileTypeBox("heic", "mif1"),
            "photo.heic");

        Assert.Equal(ImageDecodeBackendResultKind.BackendUnavailable, result.Kind);
        Assert.Null(result.Image);
        Assert.Null(result.Exception);
        Assert.Contains("Fovium-owned", result.TechnicalDetail, StringComparison.Ordinal);
        Assert.Contains("missing", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DispatcherMapsMissingRuntimeToRecoverableDecodeFailure()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.HeifUnavailable.Tests.");
        try
        {
            var path = Path.Combine(directory.FullName, "photo.avif");
            await File.WriteAllBytesAsync(path, IsoBmffFileTypeProbeTests.CreateFileTypeBox("avif", "mif1"));
            using var backend = new HeifImageDecodeBackend(
                new LibHeifRuntimeLocator(Path.Combine(directory.FullName, "missing"), "win-x64"));
            using var decoder = new ImageDecoder([backend]);

            var result = await decoder.LoadAsync(path, UnlimitedAllowance, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Image);
            Assert.Equal(ImageLoadErrorKind.DecodeFailed, result.Error!.Kind);
            Assert.Contains("Fovium-owned", result.Error.TechnicalDetail, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Theory]
    [InlineData("avis")]
    [InlineData("hevc")]
    public void SequenceBrandsAreUnsupportedBeforeNativeRuntimeResolution(string brand)
    {
        var result = DecodeWithMissingRuntime(IsoBmffFileTypeProbeTests.CreateFileTypeBox(brand), $"sequence-{brand}.bin");

        Assert.Equal(ImageDecodeBackendResultKind.UnsupportedVariant, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("sequences and animation", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("static primary images", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedRecognizedFileTypeIsCorruptBeforeNativeRuntimeResolution()
    {
        var malformed = IsoBmffFileTypeProbeTests.CreateFileTypeBox("avif");
        malformed[3] = checked((byte)(malformed.Length + 8));

        var result = DecodeWithMissingRuntime(malformed, "broken.avif");

        Assert.Equal(ImageDecodeBackendResultKind.Corrupt, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("malformed", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("fovium-rgb8.heic")]
    [InlineData("fovium-rgb8.avif")]
    public void EncodedBytesOverAllowanceAreRejectedBeforeNativeRuntimeResolution(string fixtureName)
    {
        var encoded = File.ReadAllBytes(HeifTestFixtures.PathFor(fixtureName));

        var result = DecodeWithMissingRuntime(
            encoded,
            fixtureName,
            new ImageLoadAllowance(encoded.LongLength - 1, encoded.LongLength - 1, false));

        Assert.Equal(ImageDecodeBackendResultKind.ResourceLimit, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("Encoded source size", result.TechnicalDetail, StringComparison.Ordinal);
        Assert.Contains("allowance", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EitherEncodedByteAllowanceIndependentlyRejectsBeforeRuntimeResolution(bool constrainWorkingBytes)
    {
        var encoded = IsoBmffFileTypeProbeTests.CreateFileTypeBox("heic", "mif1");
        var allowance = constrainWorkingBytes
            ? new ImageLoadAllowance(encoded.LongLength - 1, long.MaxValue, false)
            : new ImageLoadAllowance(long.MaxValue, encoded.LongLength - 1, false);

        var result = DecodeWithMissingRuntime(encoded, "limited.heic", allowance);

        Assert.Equal(ImageDecodeBackendResultKind.ResourceLimit, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("allowance", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodedBytesExactlyAtAllowanceAdvanceToRuntimeResolution()
    {
        var encoded = IsoBmffFileTypeProbeTests.CreateFileTypeBox("avif", "mif1");
        var allowance = new ImageLoadAllowance(encoded.LongLength, encoded.LongLength, false);

        var result = DecodeWithMissingRuntime(encoded, "exact-limit.avif", allowance);

        Assert.Equal(ImageDecodeBackendResultKind.BackendUnavailable, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("Fovium-owned", result.TechnicalDetail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fovium-rgb8.heic", (int)ImageFormatId.Heif)]
    [InlineData("fovium-rgb8.avif", (int)ImageFormatId.Avif)]
    public void TrackedRgb8FixtureDecodesThroughProductionBackendWhenRuntimeIsAvailableOrRequired(
        string fixtureName,
        int expectedFormat)
    {
        var path = HeifTestFixtures.PathFor(fixtureName);
        var encoded = File.ReadAllBytes(path);
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (result.Kind == ImageDecodeBackendResultKind.BackendUnavailable)
        {
            AssertRuntimeWasOptional(result.TechnicalDetail);
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.Success, result.Kind);
        using var image = Assert.IsType<DecodedImage>(result.Image);
        using var pixels = image.AcquirePixelLease();
        Assert.Null(result.Exception);
        Assert.Equal((ImageFormatId)expectedFormat, image.Descriptor.EncodedFormat);
        Assert.Equal(new PixelSize(16, 12), image.Descriptor.EncodedSize);
        Assert.Equal(new PixelSize(16, 12), image.Descriptor.OrientedSize);
        Assert.Equal(ExifOrientation.Normal, image.Descriptor.Orientation);
        Assert.Equal(1, image.Descriptor.FrameCount);
        Assert.Equal("Bgra8888/Premul", image.Descriptor.PixelFormat);
        Assert.Equal(SKColorType.Bgra8888, pixels.ColorType);
        Assert.Equal(SKAlphaType.Premul, pixels.AlphaType);
        Assert.Equal(encoded, image.EncodedSource);
        Assert.Equal(16 * 12 * 4, pixels.PixelBytes.Length);
        Assert.All(AlphaValues(pixels), alpha => Assert.Equal(byte.MaxValue, alpha));
        AssertQuadrantDominance(pixels);
        Assert.NotNull(image.Descriptor.SourceColorDescription);
        Assert.Contains("Source depth 8-bit", image.Descriptor.SourceColorDescription, StringComparison.Ordinal);
        Assert.NotNull(backend.LoadedNativeLibraryPath);
        Assert.True(Path.IsPathFullyQualified(backend.LoadedNativeLibraryPath));
        Assert.Contains(
            Path.Combine("runtimes", LibHeifRuntimeLocator.GetSupportedCurrentRid()!, "native"),
            backend.LoadedNativeLibraryPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrackedAlphaFixtureProducesZeroPartialAndOpaqueSinglePremultipliedPixelsWhenRuntimeIsAvailableOrRequired()
    {
        var path = HeifTestFixtures.PathFor("fovium-alpha8.avif");
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.Success, result.Kind);
        using var image = Assert.IsType<DecodedImage>(result.Image);
        using var pixels = image.AcquirePixelLease();
        var transparent = ReadPixel(pixels, 4, 12);
        var partial = ReadPixel(pixels, 16, 12);
        var opaque = ReadPixel(pixels, 28, 12);

        Assert.Equal(ImageFormatId.Avif, image.Descriptor.EncodedFormat);
        Assert.Equal(new PixelSize(32, 24), image.Descriptor.OrientedSize);
        Assert.Equal(SKAlphaType.Premul, pixels.AlphaType);
        Assert.Equal((byte)0, transparent.Alpha);
        Assert.Equal(new SKColor(0, 0, 0, 0), transparent);
        Assert.Equal((byte)128, partial.Alpha);
        Assert.Equal(byte.MaxValue, opaque.Alpha);
        Assert.InRange(opaque.Red, (byte)0, (byte)40);
        Assert.InRange(opaque.Green, (byte)50, (byte)100);
        Assert.InRange(opaque.Blue, (byte)220, byte.MaxValue);
        AssertPremultipliedOnce(partial.Red, opaque.Red, partial.Alpha, "red");
        AssertPremultipliedOnce(partial.Green, opaque.Green, partial.Alpha, "green");
        AssertPremultipliedOnce(partial.Blue, opaque.Blue, partial.Alpha, "blue");
        Assert.True(partial.Blue > 100, $"Partial-alpha blue was {partial.Blue}; a double premultiply would be near 60.");
        Assert.Contains((byte)0, AlphaValues(pixels));
        Assert.Contains((byte)128, AlphaValues(pixels));
        Assert.Contains(byte.MaxValue, AlphaValues(pixels));
    }

    [Fact]
    public void TrackedRotate90FixtureProducesPresentationDimensionsAndPixelsExactlyOnceWhenRuntimeIsAvailableOrRequired()
    {
        var path = HeifTestFixtures.PathFor("fovium-rotate90.avif");
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.Success, result.Kind);
        using var image = Assert.IsType<DecodedImage>(result.Image);
        using var pixels = image.AcquirePixelLease();

        Assert.Equal(new PixelSize(12, 20), image.Descriptor.EncodedSize);
        Assert.Equal(new PixelSize(12, 20), image.Descriptor.OrientedSize);
        Assert.Equal(ExifOrientation.Normal, image.Descriptor.Orientation);
        AssertSyntheticColor(ReadPixel(pixels, 3, 3), SyntheticColor.Green);
        AssertSyntheticColor(ReadPixel(pixels, 9, 3), SyntheticColor.Yellow);
        AssertSyntheticColor(ReadPixel(pixels, 3, 16), SyntheticColor.Red);
        AssertSyntheticColor(ReadPixel(pixels, 9, 16), SyntheticColor.Blue);
    }

    [Fact]
    public void TrackedMirrorFixtureProducesVisibleLeftRightMirrorExactlyOnceWhenRuntimeIsAvailableOrRequired()
    {
        var path = HeifTestFixtures.PathFor("fovium-mirror.avif");
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.Success, result.Kind);
        using var image = Assert.IsType<DecodedImage>(result.Image);
        using var pixels = image.AcquirePixelLease();

        Assert.Equal(new PixelSize(20, 12), image.Descriptor.OrientedSize);
        Assert.Equal(ExifOrientation.Normal, image.Descriptor.Orientation);
        AssertSyntheticColor(ReadPixel(pixels, 4, 3), SyntheticColor.Green);
        AssertSyntheticColor(ReadPixel(pixels, 15, 3), SyntheticColor.Red);
        AssertSyntheticColor(ReadPixel(pixels, 4, 9), SyntheticColor.Yellow);
        AssertSyntheticColor(ReadPixel(pixels, 15, 9), SyntheticColor.Blue);
    }

    [Fact]
    public void TrackedTenBitPrimaryIsUnsupportedRatherThanQuantizedWhenRuntimeIsAvailableOrRequired()
    {
        var path = HeifTestFixtures.PathFor("fovium-rgb10.avif");
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.UnsupportedVariant, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("10-bit", result.TechnicalDetail, StringComparison.Ordinal);
        Assert.Contains("limited to 8-bit", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not quantize", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("fovium-pq8.avif", "PQ / ST 2084")]
    [InlineData("fovium-hlg8.avif", "HLG")]
    public void TrackedHdrTransferIsUnsupportedWithoutToneMappingWhenRuntimeIsAvailableOrRequired(
        string fixtureName,
        string transferName)
    {
        var path = HeifTestFixtures.PathFor(fixtureName);
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.UnsupportedVariant, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains(transferName, result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SDR-only", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not tone-map", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrackedSequenceIsUnsupportedWithoutPublishingFrameZero()
    {
        var path = HeifTestFixtures.PathFor("fovium-sequence.avif");
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);

        Assert.Equal(ImageDecodeBackendResultKind.UnsupportedVariant, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("sequences and animation", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("static primary images only", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrackedTruncatedContainerIsCorruptWithoutNativeExceptionEscapeWhenRuntimeIsAvailableOrRequired()
    {
        var path = HeifTestFixtures.PathFor("fovium-truncated.avif");
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.Corrupt, result.Kind);
        Assert.Null(result.Image);
        Assert.Null(result.Exception);
        Assert.Contains("libheif", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidFixtureWithPixelBudgetBelowDecodedEstimateIsRejectedAfterNativeProbeWhenRuntimeIsAvailableOrRequired()
    {
        var path = HeifTestFixtures.PathFor("fovium-rgb8.avif");
        var encodedLength = new FileInfo(path).Length;
        var allowance = new ImageLoadAllowance(encodedLength + 1, encodedLength + 1, false);
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, allowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.ResourceLimit, result.Kind);
        Assert.Null(result.Image);
        Assert.Contains("Estimated working/retained bytes", result.TechnicalDetail, StringComparison.Ordinal);
        Assert.Contains($"allowance {allowance.MaximumWorkingBytes}/{allowance.MaximumRetainedBytes}", result.TechnicalDetail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fovium-rgb8.heic", "HEIF")]
    [InlineData("fovium-rgb8.avif", "AVIF")]
    public async Task DecodedHeifOrAvifFeedsPhotoInfoHistogramAndAmbientWithoutAnotherDecodeWhenRuntimeIsAvailableOrRequired(
        string fixtureName,
        string expectedFormat)
    {
        var path = HeifTestFixtures.PathFor(fixtureName);
        using var backend = new HeifImageDecodeBackend();

        var result = backend.Decode(path, UnlimitedAllowance, CancellationToken.None);
        if (RuntimeUnavailableWasOptional(result))
        {
            return;
        }

        Assert.Equal(ImageDecodeBackendResultKind.Success, result.Kind);
        using var image = Assert.IsType<DecodedImage>(result.Image);
        var photoInfo = PhotoInfoFormatter.Format(
            new PhotoInfoState(
                new PhotoInfoBase(
                    image.Identity,
                    image.Descriptor.SourcePath,
                    image.Descriptor.EncodedFormat,
                    image.Descriptor.OrientedSize,
                    image.EncodedSource.LongLength),
                PhotoMetadataSummary.Empty,
                IsMetadataLoading: false),
            CultureInfo.InvariantCulture);
        var histogramResult = await new SkiaDecodedHistogramReader().ReadAsync(image, CancellationToken.None);
        using var ambient = new AmbientStagePreparer().Prepare(
            image,
            StageDefaults.AmbientBlurSigmaPixels,
            CancellationToken.None);

        var histogram = Assert.IsType<HistogramData>(histogramResult.Data);
        Assert.Contains($"{fixtureName} · {expectedFormat} ·", photoInfo.File, StringComparison.Ordinal);
        Assert.Equal("16 × 12 · 0 MP", photoInfo.Dimensions);
        Assert.Equal(HistogramReadStatus.Success, histogramResult.Status);
        Assert.Equal(16 * 12, histogram.SampleCount);
        Assert.Equal(new PixelSize(16, 12), ambient.Size);
        Assert.True(ambient.RetainedBytes > 0);
    }

    [Theory]
    [InlineData("fovium-rgb8.heic", "heif-as-jpeg.jpg", (int)ImageFormatId.Heif)]
    [InlineData("fovium-rgb8.avif", "avif-as-png.png", (int)ImageFormatId.Avif)]
    [InlineData("fovium-rgb8.heic", "heif-as-bin.bin", (int)ImageFormatId.Heif)]
    public async Task TrackedContentIdentityOverridesMisleadingOrUnusualExtensionWhenRuntimeIsAvailableOrRequired(
        string fixtureName,
        string renamedFile,
        int expectedFormat)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.HeifContentTruth.Tests.");
        try
        {
            var path = Path.Combine(directory.FullName, renamedFile);
            await File.WriteAllBytesAsync(path, File.ReadAllBytes(HeifTestFixtures.PathFor(fixtureName)));
            using var decoder = ImageDecoder.CreateDefault();

            var result = await decoder.LoadAsync(path, UnlimitedAllowance, CancellationToken.None);
            if (!result.IsSuccess &&
                result.Error?.TechnicalDetail.Contains("Fovium-owned", StringComparison.Ordinal) == true)
            {
                AssertRuntimeWasOptional(result.Error.TechnicalDetail);
                return;
            }

            using var image = result.Image;
            Assert.True(result.IsSuccess, result.Error?.TechnicalDetail);
            Assert.Null(result.Error);
            Assert.Equal((ImageFormatId)expectedFormat, image!.Descriptor.EncodedFormat);
            Assert.Equal(Path.GetFullPath(path), image.Descriptor.SourcePath);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task ExistingJpegAndTiffContentOverrideHeifAndAvifExtensionsWithoutNativeRuntime()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.HeifExistingContentTruth.Tests.");
        try
        {
            var jpegPath = Path.Combine(directory.FullName, "jpeg-as-heic.heic");
            var tiffPath = Path.Combine(directory.FullName, "tiff-as-avif.avif");
            await File.WriteAllBytesAsync(jpegPath, EncodedImageTestData.Create(SKEncodedImageFormat.Jpeg));
            await File.WriteAllBytesAsync(tiffPath, TiffTestData.CreateRgb());
            using var decoder = ImageDecoder.CreateDefault();

            var jpegResult = await decoder.LoadAsync(jpegPath, UnlimitedAllowance, CancellationToken.None);
            var tiffResult = await decoder.LoadAsync(tiffPath, UnlimitedAllowance, CancellationToken.None);
            using var jpeg = jpegResult.Image;
            using var tiff = tiffResult.Image;

            Assert.True(jpegResult.IsSuccess, jpegResult.Error?.TechnicalDetail);
            Assert.True(tiffResult.IsSuccess, tiffResult.Error?.TechnicalDetail);
            Assert.Equal(ImageFormatId.Jpeg, jpeg!.Descriptor.EncodedFormat);
            Assert.Equal(ImageFormatId.Tiff, tiff!.Descriptor.EncodedFormat);
            Assert.Null(jpegResult.Error);
            Assert.Null(tiffResult.Error);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static ImageDecodeBackendResult DecodeWithMissingRuntime(
        byte[] encoded,
        string fileName,
        ImageLoadAllowance? allowance = null)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.HeifBackend.Tests.");
        try
        {
            var path = Path.Combine(directory.FullName, fileName);
            File.WriteAllBytes(path, encoded);
            using var backend = new HeifImageDecodeBackend(
                new LibHeifRuntimeLocator(Path.Combine(directory.FullName, "missing"), "win-x64"));
            return backend.Decode(path, allowance ?? UnlimitedAllowance, CancellationToken.None);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static IEnumerable<byte> AlphaValues(DecodedImage.PixelLease pixels)
    {
        var bytes = pixels.PixelBytes.ToArray();
        for (var offset = 3; offset < bytes.Length; offset += 4)
        {
            yield return bytes[offset];
        }
    }

    private static void AssertQuadrantDominance(DecodedImage.PixelLease pixels)
    {
        var topLeft = ReadPixel(pixels, 3, 3);
        var topRight = ReadPixel(pixels, 12, 3);
        var bottomLeft = ReadPixel(pixels, 3, 9);
        var bottomRight = ReadPixel(pixels, 12, 9);

        Assert.True(topLeft.Red > topLeft.Green + 80 && topLeft.Red > topLeft.Blue + 80, $"Top-left was {topLeft}.");
        Assert.True(topRight.Green > topRight.Red + 80 && topRight.Green > topRight.Blue + 80, $"Top-right was {topRight}.");
        Assert.True(bottomLeft.Blue > bottomLeft.Red + 80 && bottomLeft.Blue > bottomLeft.Green + 80, $"Bottom-left was {bottomLeft}.");
        Assert.True(bottomRight.Red > 160 && bottomRight.Green > 160 && bottomRight.Blue < 100, $"Bottom-right was {bottomRight}.");
    }

    private static void AssertSyntheticColor(SKColor actual, SyntheticColor expected)
    {
        switch (expected)
        {
            case SyntheticColor.Red:
                Assert.True(actual.Red > 180 && actual.Red > actual.Green + 80 && actual.Red > actual.Blue + 80, $"Expected red, got {actual}.");
                break;
            case SyntheticColor.Green:
                Assert.True(actual.Green > 180 && actual.Green > actual.Red + 80 && actual.Green > actual.Blue + 80, $"Expected green, got {actual}.");
                break;
            case SyntheticColor.Blue:
                Assert.True(actual.Blue > 180 && actual.Blue > actual.Red + 80 && actual.Blue > actual.Green + 80, $"Expected blue, got {actual}.");
                break;
            case SyntheticColor.Yellow:
                Assert.True(actual.Red > 160 && actual.Green > 160 && actual.Blue < 100, $"Expected yellow, got {actual}.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expected));
        }
    }

    private static void AssertPremultipliedOnce(byte actual, byte opaque, byte alpha, string channel)
    {
        var expected = ((opaque * alpha) + 127) / byte.MaxValue;
        Assert.InRange(
            (int)actual,
            Math.Max(0, expected - 3),
            Math.Min(byte.MaxValue, expected + 3));
        Assert.True(actual <= alpha, $"Premultiplied {channel} channel {actual} exceeded alpha {alpha}.");
    }

    private static SKColor ReadPixel(DecodedImage.PixelLease pixels, int x, int y)
    {
        var offset = (y * pixels.RowBytes) + (x * 4);
        var bytes = pixels.PixelBytes;
        return new SKColor(bytes[offset + 2], bytes[offset + 1], bytes[offset], bytes[offset + 3]);
    }

    private static void AssertRuntimeWasOptional(string? technicalDetail)
    {
        Assert.False(
            string.Equals(Environment.GetEnvironmentVariable(RequireRuntimeEnvironmentVariable), "1", StringComparison.Ordinal),
            $"{RequireRuntimeEnvironmentVariable}=1 requires the materialized Fovium-owned runtime. {technicalDetail}");
    }

    private static bool RuntimeUnavailableWasOptional(ImageDecodeBackendResult result)
    {
        if (result.Kind != ImageDecodeBackendResultKind.BackendUnavailable)
        {
            return false;
        }

        Assert.Null(result.Image);
        AssertRuntimeWasOptional(result.TechnicalDetail);
        return true;
    }

    private static ImageLoadAllowance UnlimitedAllowance { get; } =
        new(long.MaxValue, long.MaxValue, false);

    private enum SyntheticColor
    {
        Red,
        Green,
        Blue,
        Yellow,
    }
}

internal static class HeifTestFixtures
{
    public static string PathFor(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Heif", fileName);
        Assert.True(File.Exists(path), $"Tracked HEIF/AVIF fixture was not materialized: {path}");
        return path;
    }
}
