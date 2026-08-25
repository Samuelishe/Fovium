using System.Diagnostics;
using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;
using Xunit.Abstractions;

namespace Fovium.Tests.Histogram;

public sealed class HistogramPerformanceSmokeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task TwentyFourMegapixelSamplingIsBoundedAndComparedWithExactScan()
    {
        using var image = CreateImage(6000, 4000);
        var sampledReader = new SkiaDecodedHistogramReader();
        var exactReader = new SkiaDecodedHistogramReader(int.MaxValue);

        var sampledClock = Stopwatch.StartNew();
        var sampled = Assert.IsType<HistogramData>(
            (await sampledReader.ReadAsync(image, CancellationToken.None)).Data);
        sampledClock.Stop();
        var exactClock = Stopwatch.StartNew();
        var exact = Assert.IsType<HistogramData>(
            (await exactReader.ReadAsync(image, CancellationToken.None)).Data);
        exactClock.Stop();

        output.WriteLine(
            "24 MP sampled: {0:F2} ms / {1:N0} locations; exact: {2:F2} ms / {3:N0} locations.",
            sampledClock.Elapsed.TotalMilliseconds,
            sampled.SampledLocationCount,
            exactClock.Elapsed.TotalMilliseconds,
            exact.SampledLocationCount);
        Assert.True(sampled.WasSampled);
        Assert.InRange(sampled.SampledLocationCount, 1, SkiaDecodedHistogramReader.MaximumHistogramSamples);
        Assert.False(exact.WasSampled);
        Assert.Equal(24_000_000, exact.SampledLocationCount);
        Assert.Equal(0, sampled.SampleCount);
        Assert.Equal(0, exact.SampleCount);
    }

    private static DecodedImage CreateImage(int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.GetPixelSpan().Clear();
        var nativeImage = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(width, height);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                "Raw",
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
            nativeImage);
    }
}
