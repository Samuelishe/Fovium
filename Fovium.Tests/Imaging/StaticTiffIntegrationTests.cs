using System.Globalization;
using Fovium.Histogram;
using Fovium.Imaging;
using Fovium.Metadata;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.Imaging;

public sealed class StaticTiffIntegrationTests
{
    [Fact]
    public async Task RetainedTiffFeedsPhotoInfoHistogramAmbientAndMetadataBoundaries()
    {
        using var decoded = await DecodeAsync(TiffTestData.CreateRgb(), "rgb.tif");

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
        var metadata = await new MetadataExtractorPhotoMetadataReader().ReadAsync(
            decoded.EncodedSource,
            CancellationToken.None);
        var histogramResult = await new SkiaDecodedHistogramReader().ReadAsync(
            decoded,
            CancellationToken.None);
        using var ambient = new AmbientStagePreparer().Prepare(
            decoded,
            StageDefaults.AmbientBlurSigmaPixels,
            CancellationToken.None);

        var histogram = Assert.IsType<HistogramData>(histogramResult.Data);
        Assert.Contains("rgb.tif · TIFF ·", photoInfo.File, StringComparison.Ordinal);
        Assert.Equal("3 × 2 · 0 MP", photoInfo.Dimensions);
        Assert.NotEqual(PhotoMetadataReadStatus.Failed, metadata.Status);
        Assert.Equal(6, histogram.SampleCount);
        Assert.Equal(new PixelSize(3, 2), ambient.Size);
        Assert.True(ambient.RetainedBytes > 0);
    }

    [Fact]
    public async Task TransparentTiffPixelsRevealMatteAndOpaquePixelsRemainPhotographic()
    {
        using var decoded = await DecodeAsync(TiffTestData.CreateRgba(associated: false), "alpha.tiff");
        using var surface = SKSurface.Create(new SKImageInfo(
            60,
            20,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        var destination = new RectD(0, 0, 60, 20);
        var matteColor = new StageColor(0x66, 0x55, 0x44);
        SkiaStageRenderer.Draw(
            surface.Canvas,
            destination,
            destination,
            1,
            StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Black,
                MatteEnabled = true,
                MatteColor = matteColor,
            },
            null,
            null);
        using (var lease = decoded.AcquireRenderLease())
        {
            surface.Canvas.DrawImage(
                lease.Image,
                new SKRect(0, 0, 3, 1),
                new SKRect(0, 0, 60, 20),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        }

        using var snapshot = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);
        var opaque = pixels.GetPixel(10, 10);
        var transparent = pixels.GetPixel(50, 10);

        Assert.Equal(new SKColor(255, 0, 0), opaque);
        Assert.Equal(new SKColor(matteColor.Red, matteColor.Green, matteColor.Blue), transparent);
    }

    private static async Task<DecodedImage> DecodeAsync(byte[] encoded, string fileName)
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.Tiff.Integration.");
        var path = Path.Combine(directory.FullName, fileName);
        try
        {
            await File.WriteAllBytesAsync(path, encoded);
            using var decoder = ImageDecoder.CreateDefault();
            var result = await decoder.LoadAsync(
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
