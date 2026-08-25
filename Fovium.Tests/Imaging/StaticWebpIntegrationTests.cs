using System.Globalization;
using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Metadata;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.Imaging;

public sealed class StaticWebpIntegrationTests
{
    [Fact]
    public async Task RetainedAlphaWebpFeedsPhotoInfoHistogramAndAmbientWithoutFormatBranches()
    {
        using var decoded = await DecodeAlphaWebpAsync();

        var photoInfo = PhotoInfoFormatter.Format(
            new PhotoInfoState(
                new PhotoInfoBase(
                    decoded.Identity,
                    decoded.Descriptor.SourcePath,
                    decoded.Descriptor.EncodedFormat,
                    decoded.Descriptor.OrientedSize,
                    decoded.EncodedSource.LongLength),
                PhotoMetadataSummary.Empty,
                IsMetadataLoading: false),
            CultureInfo.InvariantCulture);
        var histogramResult = await new SkiaDecodedHistogramReader().ReadAsync(
            decoded,
            CancellationToken.None);
        using var ambient = new AmbientStagePreparer().Prepare(
            decoded,
            StageDefaults.AmbientBlurSigmaPixels,
            CancellationToken.None);

        var histogram = Assert.IsType<HistogramData>(histogramResult.Data);
        Assert.Contains("alpha.webp · WEBP ·", photoInfo.File, StringComparison.Ordinal);
        Assert.Equal(new PixelSize(12, 8), decoded.Descriptor.OrientedSize);
        Assert.Equal(48, histogram.SampleCount);
        Assert.Equal(histogram.SampleCount, histogram.Red.Sum());
        Assert.Equal(histogram.SampleCount, histogram.Green.Sum());
        Assert.Equal(histogram.SampleCount, histogram.Blue.Sum());
        Assert.Equal(new PixelSize(12, 8), ambient.Size);
        Assert.True(ambient.RetainedBytes > 0);
    }

    [Fact]
    public async Task TransparentWebpPixelsRevealMatteAndOpaquePixelsRemainPhotographic()
    {
        using var decoded = await DecodeAlphaWebpAsync();
        using var surface = SKSurface.Create(new SKImageInfo(
            64,
            48,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        var destination = new RectD(8, 8, 48, 32);
        var matteColor = new StageColor(0x66, 0x55, 0x44);
        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 64, 48),
            destination,
            1,
            StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Custom,
                CustomBackgroundColor = new StageColor(0x11, 0x22, 0x33),
                MatteEnabled = true,
                MatteColor = matteColor,
            },
            null,
            null);
        using (var lease = decoded.AcquireRenderLease())
        {
            surface.Canvas.DrawImage(
                lease.Image,
                new SKRect(0, 0, 12, 8),
                new SKRect(8, 8, 56, 40),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        }

        using var snapshot = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);
        var transparentSide = pixels.GetPixel(16, 24);
        var opaqueSide = pixels.GetPixel(48, 24);

        Assert.Equal(new SKColor(matteColor.Red, matteColor.Green, matteColor.Blue), transparentSide);
        Assert.NotEqual(transparentSide, opaqueSide);
        Assert.Equal(byte.MaxValue, opaqueSide.Alpha);
    }

    private static async Task<DecodedImage> DecodeAlphaWebpAsync()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Webp.Integration.");
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
            return Assert.IsType<DecodedImage>(result.Image);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
