using System.Buffers.Binary;
using Fovium.Home;
using Fovium.Tests.Imaging;
using SkiaSharp;

namespace Fovium.Tests.Home;

public sealed class RecentThumbnailProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Fovium.RecentThumbnail.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PreviewIsBoundedAndEncodedAsAUsablePng()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "large.jpg");
        await File.WriteAllBytesAsync(
            path,
            EncodedImageTestData.Create(SKEncodedImageFormat.Jpeg, 640, 320));
        using var provider = new RecentThumbnailProvider();

        var result = await provider.PrepareAsync(path);

        Assert.Equal(RecentThumbnailStatus.Ready, result.Status);
        Assert.NotNull(result.PngBytes);
        Assert.Equal(160, Math.Max(result.Size.Width, result.Size.Height));
        using var bitmap = SKBitmap.Decode(result.PngBytes);
        Assert.NotNull(bitmap);
        Assert.Equal(result.Size.Width, bitmap.Width);
        Assert.Equal(result.Size.Height, bitmap.Height);
    }

    [Fact]
    public async Task ExifOrientationIsAppliedToVisiblePreviewDimensions()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "oriented.jpg");
        var jpeg = EncodedImageTestData.Create(SKEncodedImageFormat.Jpeg, 12, 8);
        await File.WriteAllBytesAsync(path, AddExifOrientation(jpeg, 6));
        using var provider = new RecentThumbnailProvider();

        var result = await provider.PrepareAsync(path);

        Assert.Equal(RecentThumbnailStatus.Ready, result.Status);
        Assert.Equal(8, result.Size.Width);
        Assert.Equal(12, result.Size.Height);
    }

    [Fact]
    public async Task CachedPreviewSurvivesTemporarySourceUnavailability()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "temporary.jpg");
        await File.WriteAllBytesAsync(path, EncodedImageTestData.Create(SKEncodedImageFormat.Jpeg));
        using var provider = new RecentThumbnailProvider();
        var prepared = await provider.PrepareAsync(path);

        File.Delete(path);
        var cached = await provider.PrepareAsync(path);

        Assert.Equal(RecentThumbnailStatus.Ready, prepared.Status);
        Assert.Equal(RecentThumbnailStatus.Ready, cached.Status);
        Assert.True(cached.FromMemoryCache);
        Assert.Equal(prepared.PngBytes, cached.PngBytes);
    }

    [Fact]
    public async Task SourceMutationInvalidatesMemoryCache()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "mutable.jpg");
        await File.WriteAllBytesAsync(
            path,
            EncodedImageTestData.Create(SKEncodedImageFormat.Jpeg, 12, 8));
        using var provider = new RecentThumbnailProvider();
        var first = await provider.PrepareAsync(path);
        await File.WriteAllBytesAsync(
            path,
            EncodedImageTestData.Create(SKEncodedImageFormat.Jpeg, 20, 10));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));

        var second = await provider.PrepareAsync(path);

        Assert.False(first.FromMemoryCache);
        Assert.False(second.FromMemoryCache);
        Assert.Equal(20, second.Size.Width);
        Assert.Equal(10, second.Size.Height);
    }

    [Fact]
    public async Task CancellationAndDecodeFailureRemainHarmless()
    {
        Directory.CreateDirectory(_directory);
        var corruptPath = Path.Combine(_directory, "corrupt.jpg");
        await File.WriteAllBytesAsync(corruptPath, [1, 2, 3, 4]);
        using var provider = new RecentThumbnailProvider();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.PrepareAsync(corruptPath, cancellationToken: canceled.Token));
        var failed = await provider.PrepareAsync(corruptPath);

        Assert.NotEqual(RecentThumbnailStatus.Ready, failed.Status);
        Assert.Null(failed.PngBytes);
        Assert.Equal(1, provider.GetMetrics().Cancellations);
    }

    [Fact]
    public async Task MemoryCacheStaysWithinProductBounds()
    {
        Directory.CreateDirectory(_directory);
        using var provider = new RecentThumbnailProvider();
        for (var index = 0; index < 8; index++)
        {
            var path = Path.Combine(_directory, $"photo-{index}.jpg");
            await File.WriteAllBytesAsync(
                path,
                EncodedImageTestData.Create(SKEncodedImageFormat.Jpeg, 320 + index, 180));
            Assert.Equal(RecentThumbnailStatus.Ready, (await provider.PrepareAsync(path)).Status);
        }

        var metrics = provider.GetMetrics();
        Assert.InRange(metrics.CachedItems, 1, 6);
        Assert.InRange(metrics.RetainedBytes, 1, 2 * 1024 * 1024);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static byte[] AddExifOrientation(byte[] jpeg, ushort orientation)
    {
        var payload = new byte[32];
        "Exif\0\0"u8.CopyTo(payload);
        var tiff = payload.AsSpan(6);
        tiff[0] = (byte)'I';
        tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff[4..], 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[8..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[10..], 0x0112);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[12..], 3);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff[14..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[18..], orientation);
        var result = new byte[jpeg.Length + payload.Length + 4];
        result[0] = 0xFF;
        result[1] = 0xD8;
        result[2] = 0xFF;
        result[3] = 0xE1;
        var segmentLength = payload.Length + 2;
        result[4] = (byte)(segmentLength >> 8);
        result[5] = (byte)segmentLength;
        payload.CopyTo(result, 6);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(6 + payload.Length));
        return result;
    }
}