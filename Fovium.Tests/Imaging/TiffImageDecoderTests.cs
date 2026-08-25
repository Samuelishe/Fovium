using BitMiracle.LibTiff.Classic;
using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Rendering;
using TiffOrientation = BitMiracle.LibTiff.Classic.Orientation;

namespace Fovium.Tests.Imaging;

public sealed class TiffImageDecoderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentClassicEndianFixtureDecodesExactRgbPixels(bool bigEndian)
    {
        using var image = await DecodeAsync(
            TiffTestData.CreateIndependentUncompressedRgb(bigEndian),
            bigEndian ? "independent-big.tif" : "independent-little.tif");
        using var pixels = image.AcquirePixelLease();

        Assert.Equal(ImageFormatId.Tiff, image.Descriptor.EncodedFormat);
        Assert.Equal(new PixelSize(2, 1), image.Descriptor.EncodedSize);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }, pixels.PixelBytes.ToArray());
    }

    [Theory]
    [InlineData((int)Compression.NONE)]
    [InlineData((int)Compression.LZW)]
    [InlineData((int)Compression.DEFLATE)]
    [InlineData((int)Compression.PACKBITS)]
    public async Task AcceptedStripCompressionsDecodeEquivalentPattern(int compressionValue)
    {
        using var image = await DecodeAsync(
            TiffTestData.CreateRgb((Compression)compressionValue),
            $"compression-{compressionValue}.tiff");
        using var pixels = image.AcquirePixelLease();

        Assert.Equal(new PixelSize(3, 2), image.Descriptor.EncodedSize);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 0, 0, 255, 0, 0, 255);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 2, 1, 255, 0, 255, 255);
    }

    [Fact]
    public async Task TiledRgbDecodesWithoutAFormatSpecificViewerRepresentation()
    {
        using var image = await DecodeAsync(TiffTestData.CreateRgb(tiled: true), "tiled.tif");
        using var pixels = image.AcquirePixelLease();

        Assert.Equal("Bgra8888/Premul", image.Descriptor.PixelFormat);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 1, 0, 0, 255, 0, 255);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 0, 1, 255, 255, 0, 255);
    }

    [Fact]
    public async Task KnownButInvalidEmbeddedProfileIsNotFalselyReportedAsAssumedSrgb()
    {
        var encoded = TiffTestData.CreateRgb(withInvalidIccProfile: true);
        using var image = await DecodeAsync(encoded, "profiled.tif");

        Assert.Equal(SourceColorState.EmbeddedProfileUnpreserved, image.Descriptor.ColorState);
        Assert.Equal(encoded, image.EncodedSource);
    }

    [Theory]
    [InlineData((int)Photometric.MINISBLACK, 0, 127, 255)]
    [InlineData((int)Photometric.MINISWHITE, 255, 128, 0)]
    public async Task GrayscalePhotometricConvertsToExpectedRgb(
        int photometricValue,
        byte first,
        byte middle,
        byte last)
    {
        using var image = await DecodeAsync(
            TiffTestData.CreateGray((Photometric)photometricValue),
            "gray.tif");
        using var pixels = image.AcquirePixelLease();

        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 0, 0, first, first, first, 255);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 1, 0, middle, middle, middle, 255);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 2, 0, last, last, last, 255);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredAlphaProducesPremultipliedBgraAndHistogramSourceValues(bool associated)
    {
        using var image = await DecodeAsync(TiffTestData.CreateRgba(associated), "alpha.tif");
        using var pixels = image.AcquirePixelLease();

        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 0, 0, 255, 0, 0, 255);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 1, 0, 0, 128, 0, 128);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 2, 0, 0, 0, 0, 0);

        var histogram = Assert.IsType<HistogramData>(
            (await new SkiaDecodedHistogramReader().ReadAsync(image, CancellationToken.None)).Data);
        Assert.Equal(2, histogram.SampleCount);
        Assert.Equal(1, histogram.Red[255]);
        Assert.Equal(1, histogram.Green[255]);
    }

    [Fact]
    public async Task UnspecifiedExtraSampleIsRejectedRatherThanGuessedAsAlpha()
    {
        var result = await DecodeResultAsync(
            TiffTestData.CreateRgba(false, ExtraSample.UNSPECIFIED),
            "unknown-extra.tif");

        Assert.False(result.IsSuccess);
        Assert.Equal(ImageLoadErrorKind.Unsupported, result.Error!.Kind);
        Assert.Contains("alpha", result.Error.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData((int)TiffOrientation.TOPLEFT, (int)ExifOrientation.Normal, 3, 2)]
    [InlineData((int)TiffOrientation.TOPRIGHT, (int)ExifOrientation.MirrorHorizontal, 3, 2)]
    [InlineData((int)TiffOrientation.BOTRIGHT, (int)ExifOrientation.Rotate180, 3, 2)]
    [InlineData((int)TiffOrientation.BOTLEFT, (int)ExifOrientation.MirrorVertical, 3, 2)]
    [InlineData((int)TiffOrientation.LEFTTOP, (int)ExifOrientation.Transpose, 2, 3)]
    [InlineData((int)TiffOrientation.RIGHTTOP, (int)ExifOrientation.Rotate90, 2, 3)]
    [InlineData((int)TiffOrientation.RIGHTBOT, (int)ExifOrientation.Transverse, 2, 3)]
    [InlineData((int)TiffOrientation.LEFTBOT, (int)ExifOrientation.Rotate270, 2, 3)]
    public async Task OrientationIsCarriedExactlyOnceThroughDescriptor(
        int tiffOrientationValue,
        int expectedOrientationValue,
        int orientedWidth,
        int orientedHeight)
    {
        using var image = await DecodeAsync(
            TiffTestData.CreateRgb(orientation: (TiffOrientation)tiffOrientationValue),
            "oriented.tif");
        using var pixels = image.AcquirePixelLease();

        Assert.Equal(new PixelSize(3, 2), image.Descriptor.EncodedSize);
        Assert.Equal(new PixelSize(orientedWidth, orientedHeight), image.Descriptor.OrientedSize);
        Assert.Equal((ExifOrientation)expectedOrientationValue, image.Descriptor.Orientation);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 0, 0, 255, 0, 0, 255);
        AssertPixel(pixels.PixelBytes, pixels.RowBytes, 2, 1, 255, 0, 255, 255);
    }

    [Theory]
    [InlineData("high-bit", 16, (int)SampleFormat.UINT, (int)Photometric.RGB, false, "High-bit-depth")]
    [InlineData("float", 32, (int)SampleFormat.IEEEFP, (int)Photometric.RGB, false, "Floating-point")]
    [InlineData("multipage", 8, (int)SampleFormat.UINT, (int)Photometric.RGB, true, "Multi-page")]
    [InlineData("separated", 8, (int)SampleFormat.UINT, (int)Photometric.SEPARATED, false, "photometric")]
    public async Task UnsupportedVariantsFailRecoverably(
        string name,
        int bits,
        int sampleFormatValue,
        int photometricValue,
        bool multiplePages,
        string expectedDetail)
    {
        var result = await DecodeResultAsync(
            TiffTestData.CreateUnsupported(
                bits,
                (SampleFormat)sampleFormatValue,
                (Photometric)photometricValue,
                multiplePages),
            $"{name}.tif");

        Assert.False(result.IsSuccess);
        Assert.Equal(ImageLoadErrorKind.Unsupported, result.Error!.Kind);
        Assert.Contains(expectedDetail, result.Error.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BigTiffSignatureIsExplicitlyUnsupported(bool bigEndian)
    {
        var result = await DecodeResultAsync(TiffTestData.CreateBigTiffSignature(bigEndian), "large.tif");

        Assert.False(result.IsSuccess);
        Assert.Equal(ImageLoadErrorKind.Unsupported, result.Error!.Kind);
        Assert.Contains("BigTIFF", result.Error.TechnicalDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TiffResourceLimitIsCheckedBeforeDecodedRasterAllocation()
    {
        var result = await DecodeResultAsync(
            TiffTestData.CreateRgb(),
            "limited.tif",
            new ImageLoadAllowance(1, 1, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(ImageLoadErrorKind.ResourceLimit, result.Error!.Kind);
        Assert.Null(result.Image);
    }

    [Fact]
    public async Task TinyEncodedHugeDimensionTiffCannotBypassDecodedMemoryAdmission()
    {
        var result = await DecodeResultAsync(
            TiffTestData.CreateIndependentUncompressedRgb(false, 1_000_000, 1_000_000),
            "dimension-bomb.tif",
            new ImageLoadAllowance(1_000_000_000, 1_000_000_000, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(ImageLoadErrorKind.ResourceLimit, result.Error!.Kind);
        Assert.Null(result.Image);
    }

    [Fact]
    public async Task ContentTruthRoutesTiffRenamedJpegAndJpegRenamedTiff()
    {
        using var tiff = await DecodeAsync(TiffTestData.CreateRgb(), "tiff-renamed.jpg");
        using var jpeg = await DecodeAsync(
            EncodedImageTestData.Create(SkiaSharp.SKEncodedImageFormat.Jpeg),
            "jpeg-renamed.tif");

        Assert.Equal(ImageFormatId.Tiff, tiff.Descriptor.EncodedFormat);
        Assert.Equal(ImageFormatId.Jpeg, jpeg.Descriptor.EncodedFormat);
    }

    [Fact]
    public async Task MalformedRecognizedTiffReturnsRecoverableFailure()
    {
        var result = await DecodeResultAsync(
            [0x49, 0x49, 0x2A, 0x00, 0xFF, 0xFF, 0xFF, 0x7F],
            "broken.tif");

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Error!.Kind,
            new[] { ImageLoadErrorKind.Corrupt, ImageLoadErrorKind.DecodeFailed });
    }

    [Fact]
    public async Task ParallelTiffAndSkiaLoadsRemainSafeBehindSharedGate()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.MixedDecoder.Stress.");
        try
        {
            var paths = new List<string>();
            for (var index = 0; index < 12; index++)
            {
                var isTiff = index % 2 == 0;
                var path = Path.Combine(directory.FullName, isTiff ? $"{index}.tif" : $"{index}.png");
                await File.WriteAllBytesAsync(
                    path,
                    isTiff
                        ? TiffTestData.CreateRgb()
                        : EncodedImageTestData.Create(SkiaSharp.SKEncodedImageFormat.Png));
                paths.Add(path);
            }

            using var decoder = ImageDecoder.CreateDefault();
            var results = await Task.WhenAll(paths.Select(path => decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None)));
            try
            {
                Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.TechnicalDetail));
                Assert.Equal(6, results.Count(result => result.Image!.Descriptor.EncodedFormat == ImageFormatId.Tiff));
                Assert.Equal(6, results.Count(result => result.Image!.Descriptor.EncodedFormat == ImageFormatId.Png));
            }
            finally
            {
                foreach (var result in results)
                {
                    result.Image?.Dispose();
                }
            }
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static async Task<DecodedImage> DecodeAsync(byte[] encoded, string fileName)
    {
        var result = await DecodeResultAsync(encoded, fileName);
        Assert.True(
            result.IsSuccess,
            $"{result.Error?.TechnicalDetail} {result.Error?.Exception}");
        return Assert.IsType<DecodedImage>(result.Image);
    }

    private static async Task<ImageLoadResult<DecodedImage>> DecodeResultAsync(
        byte[] encoded,
        string fileName,
        ImageLoadAllowance? allowance = null)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.TiffDecoder.Tests.");
        var path = Path.Combine(directory.FullName, fileName);
        await File.WriteAllBytesAsync(path, encoded);
        try
        {
            using var decoder = ImageDecoder.CreateDefault();
            return await decoder.LoadAsync(
                path,
                allowance ?? new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static void AssertPixel(
        ReadOnlySpan<byte> pixels,
        int rowBytes,
        int x,
        int y,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        var offset = (y * rowBytes) + (x * 4);
        Assert.Equal(blue, pixels[offset]);
        Assert.Equal(green, pixels[offset + 1]);
        Assert.Equal(red, pixels[offset + 2]);
        Assert.Equal(alpha, pixels[offset + 3]);
    }
}
