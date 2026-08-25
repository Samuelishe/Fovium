using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.Histogram;

public sealed class ImageHistogramReaderTests
{
    [Fact]
    public async Task ExactBgraScanCountsEachRgbChannelAndExcludesTransparentPixels()
    {
        using var image = CreateImage(6, 1, bytes =>
        {
            SetPixel(bytes, 0, 0, 0, 255, 255);       // red
            SetPixel(bytes, 1, 0, 255, 0, 255);       // green
            SetPixel(bytes, 2, 255, 0, 0, 255);       // blue
            SetPixel(bytes, 3, 0, 0, 0, 255);         // black
            SetPixel(bytes, 4, 255, 255, 255, 255);   // white
            SetPixel(bytes, 5, 17, 33, 49, 0);        // excluded
        });
        var reader = new SkiaDecodedHistogramReader();

        var result = await reader.ReadAsync(image, CancellationToken.None);

        var data = Assert.IsType<HistogramData>(result.Data);
        Assert.Equal(HistogramReadStatus.Success, result.Status);
        Assert.False(data.WasSampled);
        Assert.Equal(6, data.SampledLocationCount);
        Assert.Equal(5, data.SampleCount);
        Assert.Equal(3, data.Red[0]);
        Assert.Equal(2, data.Red[255]);
        Assert.Equal(3, data.Green[0]);
        Assert.Equal(2, data.Green[255]);
        Assert.Equal(3, data.Blue[0]);
        Assert.Equal(2, data.Blue[255]);
        Assert.Equal(data.SampleCount, data.Red.Sum());
        Assert.Equal(data.SampleCount, data.Green.Sum());
        Assert.Equal(data.SampleCount, data.Blue.Sum());
    }

    [Fact]
    public async Task PremultipliedPartialAlphaIsUnpremultipliedBeforeBinning()
    {
        using var image = CreateImage(2, 1, bytes =>
        {
            SetPixel(bytes, 0, 0, 0, 64, 128);
            SetPixel(bytes, 1, 0, 0, 128, 255);
        });
        var reader = new SkiaDecodedHistogramReader();

        var data = Assert.IsType<HistogramData>((await reader.ReadAsync(image, default)).Data);

        Assert.Equal(2, data.Red[128]);
        Assert.Equal(2, data.SampleCount);
    }

    [Fact]
    public void SamplingPlanIsExactBelowLimitAndBoundedDeterministicAcrossWholeLargeImage()
    {
        var exact = HistogramSamplingPlan.Create(1000, 1000, 2_000_000);
        var first = HistogramSamplingPlan.Create(6000, 4000, 2_000_000);
        var second = HistogramSamplingPlan.Create(6000, 4000, 2_000_000);

        Assert.False(exact.IsSampled);
        Assert.Equal(1_000_000, exact.SampleLocationCount);
        Assert.True(first.IsSampled);
        Assert.InRange(first.SampleLocationCount, 1, 2_000_000);
        Assert.Equal(first, second);
        Assert.Equal(0, first.MapX(0));
        Assert.Equal(5999, first.MapX(first.Columns - 1));
        Assert.Equal(0, first.MapY(0));
        Assert.Equal(3999, first.MapY(first.Rows - 1));
    }

    [Fact]
    public void SamplingThresholdIncludesExactLimitAndBoundsTheFirstPixelBeyondIt()
    {
        var exact = HistogramSamplingPlan.Create(2000, 1000, 2_000_000);
        var sampled = HistogramSamplingPlan.Create(2001, 1000, 2_000_000);

        Assert.False(exact.IsSampled);
        Assert.Equal(2_000_000, exact.SampleLocationCount);
        Assert.True(sampled.IsSampled);
        Assert.InRange(sampled.SampleLocationCount, 1, 2_000_000);
    }

    [Fact]
    public async Task PreCanceledReadDoesNotAcquireOrPublishPixels()
    {
        using var image = CreateImage(1, 1, bytes => SetPixel(bytes, 0, 0, 0, 0, 255));
        var reader = new SkiaDecodedHistogramReader();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.ReadAsync(image, cancellation.Token));
    }

    [Fact]
    public async Task CancellationDuringRowIterationStopsTheOwnedWorker()
    {
        using var image = CreateImage(2000, 1000, bytes => bytes.Fill(255));
        using var cancellation = new CancellationTokenSource();
        var reader = new SkiaDecodedHistogramReader(
            int.MaxValue,
            row =>
            {
                if (row == 2)
                {
                    cancellation.Cancel();
                }
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reader.ReadAsync(image, cancellation.Token));
    }

    [Fact]
    public async Task UnexpectedDecodedPixelLayoutIsRecoverable()
    {
        using var image = CreateImage(
            1,
            1,
            bytes => bytes.Fill(255),
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        var reader = new SkiaDecodedHistogramReader();

        var result = await reader.ReadAsync(image, CancellationToken.None);

        Assert.Equal(HistogramReadStatus.UnsupportedPixelLayout, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task PixelLeaseKeepsNativePixelsAliveAfterDecodedOwnerIsReleased()
    {
        var image = CreateImage(1, 1, bytes => SetPixel(bytes, 0, 9, 19, 29, 255));
        using var pixels = image.AcquirePixelLease();

        image.Dispose();

        Assert.Equal(4, pixels.PixelBytes.Length);
        Assert.Equal((byte)9, pixels.PixelBytes[0]);
        Assert.Equal((byte)19, pixels.PixelBytes[1]);
        Assert.Equal((byte)29, pixels.PixelBytes[2]);
    }

    private static DecodedImage CreateImage(
        int width,
        int height,
        Action<Span<byte>> fill,
        SKColorType colorType = SKColorType.Bgra8888,
        SKAlphaType alphaType = SKAlphaType.Premul)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, colorType, alphaType));
        fill(bitmap.GetPixelSpan());
        var image = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(width, height);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.AssumedSrgb,
                false,
                "Bgra8888/Premul",
                bitmap.ByteCount,
                bitmap.ByteCount,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            image);
    }

    private static void SetPixel(
        Span<byte> bytes,
        int x,
        byte blue,
        byte green,
        byte red,
        byte alpha)
    {
        var offset = x * 4;
        bytes[offset] = blue;
        bytes[offset + 1] = green;
        bytes[offset + 2] = red;
        bytes[offset + 3] = alpha;
    }
}
