using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.Imaging;

public sealed class SkiaImageDecoderTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "photo.jpg", (int)ImageFormatId.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png, "photo.png", (int)ImageFormatId.Png)]
    [InlineData(SKEncodedImageFormat.Png, "photo.data", (int)ImageFormatId.Png)]
    [InlineData(SKEncodedImageFormat.Webp, "photo.webp", (int)ImageFormatId.Webp)]
    [InlineData(SKEncodedImageFormat.Webp, "webp-renamed.jpg", (int)ImageFormatId.Webp)]
    [InlineData(SKEncodedImageFormat.Jpeg, "jpeg-renamed.webp", (int)ImageFormatId.Jpeg)]
    public async Task ControlledDecoderRecognizesSupportedContentIndependentlyOfExtension(
        SKEncodedImageFormat format,
        string fileName,
        int expectedFormat)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, fileName);
        try
        {
            await File.WriteAllBytesAsync(path, EncodedImageTestData.Create(format));

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            using var image = result.Image;

            Assert.True(result.IsSuccess, result.Error?.TechnicalDetail);
            Assert.Null(result.Error);
            Assert.Equal((ImageFormatId)expectedFormat, image!.Descriptor.EncodedFormat);
            Assert.Equal(12, image.Descriptor.EncodedSize.Width);
            Assert.Equal(8, image.Descriptor.EncodedSize.Height);
            Assert.Equal(image.Descriptor.EncodedSize, image.Descriptor.OrientedSize);
            Assert.NotEmpty(image.EncodedSource);
            Assert.True(image.RetainedBytes > image.EncodedSource.Length);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Theory]
    [InlineData(SKWebpEncoderCompression.Lossy)]
    [InlineData(SKWebpEncoderCompression.Lossless)]
    public async Task StaticWebpLossyAndLosslessDecodeThroughNormalImageOwnership(
        SKWebpEncoderCompression compression)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, $"{compression}.webp");
        try
        {
            await File.WriteAllBytesAsync(path, EncodedImageTestData.CreateWebp(compression));

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            using var image = result.Image;

            Assert.True(result.IsSuccess, result.Error?.TechnicalDetail);
            Assert.Equal(ImageFormatId.Webp, image!.Descriptor.EncodedFormat);
            Assert.Equal("Bgra8888/Premul", image.Descriptor.PixelFormat);
            Assert.Equal(1, image.Descriptor.FrameCount);
            Assert.True(image.RetainedBytes > image.EncodedSource.LongLength);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task StaticWebpAlphaDecodesAsPremultipliedBgra()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "alpha.webp");
        try
        {
            await File.WriteAllBytesAsync(
                path,
                EncodedImageTestData.CreateWebp(SKWebpEncoderCompression.Lossless, withAlpha: true));

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.TechnicalDetail);
            using var image = result.Image;
            using var pixels = image!.AcquirePixelLease();

            Assert.Equal(SKColorType.Bgra8888, pixels.ColorType);
            Assert.Equal(SKAlphaType.Premul, pixels.AlphaType);
            Assert.Contains((byte)0, pixels.PixelBytes.ToArray().Where((_, index) => index % 4 == 3));
            Assert.Contains(byte.MaxValue, pixels.PixelBytes.ToArray().Where((_, index) => index % 4 == 3));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task MalformedInputReturnsTypedRecoverableFailure()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "broken.jpg");
        try
        {
            await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0x00, 0x01]);

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Error!.Kind, new[] { ImageLoadErrorKind.Corrupt, ImageLoadErrorKind.Unsupported });
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task MalformedWebpReturnsTypedRecoverableFailure()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "broken.webp");
        try
        {
            await File.WriteAllBytesAsync(path, "RIFF\0\0\0\0WEBPbroken"u8.ToArray());

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Image);
            Assert.Contains(
                result.Error!.Kind,
                new[] { ImageLoadErrorKind.Corrupt, ImageLoadErrorKind.Unsupported });
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task DetectedUnsupportedFormatUsesDurableRecoverableDiagnostic()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "unsupported.gif");
        try
        {
            byte[] gif =
            [
                0x47, 0x49, 0x46, 0x38, 0x39, 0x61,
                0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
                0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
                0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x02, 0x02, 0x44, 0x01, 0x00, 0x3B,
            ];
            await File.WriteAllBytesAsync(path, gif);

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ImageLoadErrorKind.Unsupported, result.Error!.Kind);
            Assert.Contains("Fovium does not support detected format Gif", result.Error.TechnicalDetail);
            Assert.DoesNotContain("R1", result.Error.TechnicalDetail, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task AnimatedWebpIsRejectedByGenericStaticImagePolicy()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "animated.webp");
        try
        {
            await File.WriteAllBytesAsync(path, EncodedImageTestData.CreateAnimatedWebp());

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Image);
            Assert.Equal(ImageLoadErrorKind.Unsupported, result.Error!.Kind);
            Assert.Contains("multi-frame WEBP", result.Error.TechnicalDetail, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task SkiaCodecCurrentlyDoesNotExposeWebpExifOrientation()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "oriented.webp");
        try
        {
            await File.WriteAllBytesAsync(path, EncodedImageTestData.CreateOrientedWebp());

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            using var image = result.Image;

            Assert.True(result.IsSuccess, result.Error?.TechnicalDetail);
            Assert.Equal(ExifOrientation.Normal, image!.Descriptor.Orientation);
            Assert.Equal(new PixelSize(12, 8), image.Descriptor.EncodedSize);
            Assert.Equal(new PixelSize(12, 8), image.Descriptor.OrientedSize);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp)]
    public async Task ResourcePolicyRejectsBeforeDecodedImageIsCreated(SKEncodedImageFormat format)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "large-for-allowance.png");
        try
        {
            await File.WriteAllBytesAsync(path, EncodedImageTestData.Create(format));

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(1, 1, false),
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ImageLoadErrorKind.ResourceLimit, result.Error!.Kind);
            Assert.Null(result.Image);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task RenderLeaseKeepsNativeImageAliveAfterDecodedOwnerIsDisposed()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "lifetime.png");
        try
        {
            await File.WriteAllBytesAsync(path, EncodedImageTestData.Create(SKEncodedImageFormat.Png));
            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            var decoded = Assert.IsType<DecodedImage>(result.Image);
            var retainedOperationLease = decoded.AcquireRenderLease();

            decoded.Dispose();

            Assert.Equal(12, retainedOperationLease.Image.Width);
            retainedOperationLease.Dispose();
            Assert.Throws<ObjectDisposedException>(() => retainedOperationLease.Image.Width);
        }
        finally
        {
            directory.Delete(true);
        }
    }

}
