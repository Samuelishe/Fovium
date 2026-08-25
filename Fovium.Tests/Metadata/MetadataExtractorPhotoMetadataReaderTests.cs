using Fovium.Metadata;
using Fovium.Tests.Imaging;

namespace Fovium.Tests.Metadata;

public sealed class MetadataExtractorPhotoMetadataReaderTests
{
    [Fact]
    public async Task GeneratedExifFixtureMapsToProjectOwnedPhotographicFields()
    {
        var reader = new MetadataExtractorPhotoMetadataReader();

        var result = await reader.ReadAsync(MetadataTestImages.CreateJpegWithExif(), CancellationToken.None);

        Assert.Equal(PhotoMetadataReadStatus.Success, result.Status);
        Assert.Equal("TESTMAKE", result.Summary.CameraMake);
        Assert.Equal("TESTMODEL", result.Summary.CameraModel);
        Assert.Equal("TEST 85mm", result.Summary.LensModel);
        Assert.Equal(85, result.Summary.FocalLengthMillimeters);
        Assert.Equal(2, result.Summary.Aperture);
        Assert.Equal(new PhotoRational(1, 320), result.Summary.ExposureTime);
        Assert.Equal(400, result.Summary.Iso);
        var captured = Assert.IsType<PhotoCaptureTime>(result.Summary.CaptureDateTime);
        Assert.Equal(new DateTime(2026, 8, 25, 18, 42, 0), captured.UnspecifiedClockTime);
        Assert.Equal(DateTimeKind.Unspecified, captured.UnspecifiedClockTime.Kind);
    }

    [Fact]
    public async Task GeneratedWebpExifFixtureMapsThroughExistingMetadataBoundary()
    {
        var jpeg = MetadataTestImages.CreateJpegWithExif();
        var segmentLength = (jpeg[4] << 8) | jpeg[5];
        var exifPayload = jpeg.AsSpan(6, segmentLength - 2).ToArray();
        var webp = EncodedImageTestData.CreateWebpWithExif(exifPayload);
        var reader = new MetadataExtractorPhotoMetadataReader();

        var result = await reader.ReadAsync(webp, CancellationToken.None);

        Assert.Equal(PhotoMetadataReadStatus.Success, result.Status);
        Assert.Equal("TESTMAKE", result.Summary.CameraMake);
        Assert.Equal("TESTMODEL", result.Summary.CameraModel);
        Assert.Equal("TEST 85mm", result.Summary.LensModel);
        Assert.Equal(85, result.Summary.FocalLengthMillimeters);
        Assert.Equal(2, result.Summary.Aperture);
        Assert.Equal(new PhotoRational(1, 320), result.Summary.ExposureTime);
        Assert.Equal(400, result.Summary.Iso);
    }

    [Fact]
    public async Task JpegWithoutExifReturnsRecoverableEmptyResult()
    {
        var reader = new MetadataExtractorPhotoMetadataReader();

        var result = await reader.ReadAsync(MetadataTestImages.CreateJpegWithoutExif(), CancellationToken.None);

        Assert.Equal(PhotoMetadataReadStatus.NoMetadata, result.Status);
        Assert.False(result.Summary.HasUsefulMetadata);
    }

    [Fact]
    public async Task MalformedExifNeverEscapesTheMetadataBoundary()
    {
        var reader = new MetadataExtractorPhotoMetadataReader();

        var result = await reader.ReadAsync(
            MetadataTestImages.CreateMalformedExifJpeg(),
            CancellationToken.None);

        Assert.NotEqual(PhotoMetadataReadStatus.Success, result.Status);
        Assert.False(result.Summary.HasUsefulMetadata);
    }

    [Fact]
    public async Task PartialExifKeepsAvailableFieldsWithoutPlaceholders()
    {
        var reader = new MetadataExtractorPhotoMetadataReader();

        var result = await reader.ReadAsync(
            MetadataTestImages.CreateJpegWithExif(includeExposure: false),
            CancellationToken.None);

        Assert.Equal(PhotoMetadataReadStatus.Success, result.Status);
        Assert.Equal("TESTMAKE", result.Summary.CameraMake);
        Assert.Equal("TESTMODEL", result.Summary.CameraModel);
        Assert.Equal("TEST 85mm", result.Summary.LensModel);
        Assert.Null(result.Summary.ExposureTime);
        Assert.Null(result.Summary.Aperture);
        Assert.Null(result.Summary.FocalLengthMillimeters);
        Assert.Null(result.Summary.Iso);
    }
}
