using System.Globalization;
using Fovium.Metadata;
using Fovium.Rendering;

namespace Fovium.Tests.Metadata;

public sealed class PhotoInfoFormatterTests
{
    [Theory]
    [InlineData(1, 8000, "1/8000")]
    [InlineData(1, 320, "1/320")]
    [InlineData(1, 2, "1/2")]
    [InlineData(4, 5, "0.8 s")]
    [InlineData(2, 1, "2 s")]
    public void ExposureUsesFamiliarPhotographicNotation(long numerator, long denominator, string expected)
    {
        Assert.Equal(expected, PhotoInfoFormatter.FormatExposure(new PhotoRational(numerator, denominator)));
    }

    [Theory]
    [InlineData(1.8, "ƒ/1.8")]
    [InlineData(2, "ƒ/2")]
    [InlineData(2.8, "ƒ/2.8")]
    public void ApertureUsesPhotographicNotation(double aperture, string expected)
    {
        Assert.Equal(expected, PhotoInfoFormatter.FormatAperture(aperture));
    }

    [Fact]
    public void BaseInfoUsesOrientedDimensionsBasenameEncodedLengthAndFormat()
    {
        var state = new PhotoInfoState(
            new PhotoInfoBase(7, @"C:\private\portrait.jpg", "Jpeg", new PixelSize(4000, 6000), 15_519_744),
            PhotoMetadataSummary.Empty,
            IsMetadataLoading: false);

        var text = PhotoInfoFormatter.Format(state, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("4000 × 6000 · 24 MP", text.Dimensions);
        Assert.Equal("portrait.jpg · JPEG · 14.8 MB", text.File);
        Assert.Null(text.Camera);
        Assert.Null(text.Lens);
        Assert.Null(text.Exposure);
    }

    [Fact]
    public void SparseExposureLineIncludesOnlyPresentValuesAndAvoidsDuplicatedMake()
    {
        var metadata = PhotoMetadataSummary.Empty with
        {
            CameraMake = "SONY",
            CameraModel = "SONY ILCE-7M4",
            FocalLengthMillimeters = 85,
            Iso = 400,
        };
        var state = new PhotoInfoState(
            new PhotoInfoBase(1, "photo.jpg", "Jpeg", new PixelSize(6048, 4024), 1024),
            metadata,
            IsMetadataLoading: false);

        var text = PhotoInfoFormatter.Format(state, CultureInfo.InvariantCulture);

        Assert.Equal("SONY ILCE-7M4", text.Camera);
        Assert.Equal("85 mm · ISO 400", text.Exposure);
        Assert.DoesNotContain("—", text.Exposure);
    }

    [Fact]
    public void CaptureClockRemainsUnspecifiedAndIsNotTimezoneConverted()
    {
        var recorded = new DateTime(2026, 8, 25, 18, 42, 0, DateTimeKind.Unspecified);
        var metadata = PhotoMetadataSummary.Empty with
        {
            CaptureDateTime = new PhotoCaptureTime(recorded, TimeSpan.FromHours(3)),
        };
        var state = new PhotoInfoState(
            new PhotoInfoBase(1, "photo.jpg", "Jpeg", new PixelSize(2, 2), 2048),
            metadata,
            IsMetadataLoading: false);

        var text = PhotoInfoFormatter.Format(state, CultureInfo.GetCultureInfo("ru-RU"));

        Assert.Contains("18:42", text.CaptureDateTime);
        Assert.Equal(DateTimeKind.Unspecified, metadata.CaptureDateTime.Value.UnspecifiedClockTime.Kind);
    }
}
