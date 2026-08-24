using Fovium.Imaging;
using SkiaSharp;

namespace Fovium.Tests.Imaging;

public sealed class SkiaImageDecoderTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "photo.jpg", "Jpeg")]
    [InlineData(SKEncodedImageFormat.Png, "photo.png", "Png")]
    [InlineData(SKEncodedImageFormat.Png, "photo.data", "Png")]
    public async Task ControlledDecoderRecognizesJpegPngByContent(
        SKEncodedImageFormat format,
        string fileName,
        string expectedFormat)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, fileName);
        try
        {
            await File.WriteAllBytesAsync(path, CreateEncodedImage(format));

            var result = await new SkiaImageDecoder().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            using var image = result.Image;

            Assert.True(result.IsSuccess);
            Assert.Null(result.Error);
            Assert.Equal(expectedFormat, image!.Descriptor.EncodedFormat);
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
    public async Task ResourcePolicyRejectsBeforeDecodedImageIsCreated()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Decoder.Tests.");
        var path = Path.Combine(directory.FullName, "large-for-allowance.png");
        try
        {
            await File.WriteAllBytesAsync(path, CreateEncodedImage(SKEncodedImageFormat.Png));

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
            await File.WriteAllBytesAsync(path, CreateEncodedImage(SKEncodedImageFormat.Png));
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

    private static byte[] CreateEncodedImage(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(12, 8, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(new SKColor(25, 100, 200, 128));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
