using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;
using Xunit.Abstractions;

namespace Fovium.Tests.Imaging;

public sealed class WebpDecodePerformanceSmokeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1200, 800)]
    [InlineData(3936, 2624)]
    public async Task RepresentativeStaticWebpReportsProbeDecodeAndPreparationEvidence(
        int width,
        int height)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Webp.Performance.");
        var path = Path.Combine(directory.FullName, $"{width}x{height}.webp");
        try
        {
            await File.WriteAllBytesAsync(
                path,
                EncodedImageTestData.CreateWebp(
                    SKWebpEncoderCompression.Lossy,
                    width: width,
                    height: height));

            var result = await ImageDecoder.CreateDefault().LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            using var image = result.Image;

            Assert.True(result.IsSuccess, result.Error?.TechnicalDetail);
            Assert.Equal(ImageFormatId.Webp, image!.Descriptor.EncodedFormat);
            Assert.Equal(new PixelSize(width, height), image.Descriptor.EncodedSize);
            Assert.True(image.Descriptor.EstimatedRetainedBytes >= checked((long)width * height * 4));
            output.WriteLine(
                "{0}x{1} WebP ({2:N0} encoded bytes): probe {3:F2} ms, decode {4:F2} ms, preparation {5:F2} ms.",
                width,
                height,
                image.EncodedSource.LongLength,
                image.Descriptor.ProbeDuration.TotalMilliseconds,
                image.Descriptor.DecodeDuration.TotalMilliseconds,
                image.Descriptor.PreparationDuration.TotalMilliseconds);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
