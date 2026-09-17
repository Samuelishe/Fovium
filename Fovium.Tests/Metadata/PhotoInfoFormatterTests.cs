using System.Globalization;
using Fovium.Imaging;
using Fovium.Localization;
using Fovium.Metadata;
using Fovium.Rendering;

namespace Fovium.Tests.Metadata;

public sealed class PhotoInfoFormatterTests
{
    [Theory]
    [InlineData(1, 8000, "1/8000 s")]
    [InlineData(10, 80000, "1/8000 s")]
    [InlineData(1, 100, "1/100 s")]
    [InlineData(10, 12500, "1/1250 s")]
    [InlineData(1, 2, "1/2 s")]
    [InlineData(4, 5, "0.8 s")]
    [InlineData(2, 1, "2 s")]
    public void ExposureUsesFamiliarPhotographicNotation(long numerator, long denominator, string expected)
    {
        Assert.Equal(expected, PhotoInfoFormatter.FormatExposure(new PhotoRational(numerator, denominator)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 100)]
    public void InvalidOrNonPositiveExposureIsOmitted(long numerator, long denominator)
    {
        Assert.Null(PhotoInfoFormatter.FormatExposure(new PhotoRational(numerator, denominator)));
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
        var sourcePath = Path.Combine(Path.GetTempPath(), "private", "portrait.jpg");
        var state = new PhotoInfoState(
            new PhotoInfoBase(7, sourcePath, ImageFormatId.Jpeg, new PixelSize(4000, 6000), 15_519_744),
            PhotoMetadataSummary.Empty,
            IsMetadataLoading: false);

        var localizer = Localizer.Create(CultureInfo.GetCultureInfo("en-US"));
        var text = PhotoInfoFormatter.Format(state, CultureInfo.GetCultureInfo("en-US"), localizer.Get);

        Assert.Equal("4000 × 6000 · 24 MP", text.Dimensions);
        Assert.Equal("portrait.jpg · JPEG · 14.8 MB", text.File);
        Assert.Null(text.Camera);
        Assert.Null(text.Lens);
        Assert.Null(text.Shutter);
        Assert.Null(text.Iso);
    }

    [Fact]
    public void SparseFieldsRemainIndependentAndAvoidDuplicatedMake()
    {
        var metadata = PhotoMetadataSummary.Empty with
        {
            CameraMake = "SONY",
            CameraModel = "SONY ILCE-7M4",
            FocalLengthMillimeters = 85,
            Iso = 400,
        };
        var state = new PhotoInfoState(
            new PhotoInfoBase(1, "photo.jpg", ImageFormatId.Jpeg, new PixelSize(6048, 4024), 1024),
            metadata,
            IsMetadataLoading: false);

        var localizer = Localizer.Create(CultureInfo.GetCultureInfo("en-US"));
        var text = PhotoInfoFormatter.Format(state, CultureInfo.InvariantCulture, localizer.Get);

        Assert.Equal("SONY ILCE-7M4", text.Camera);
        Assert.Equal("85 mm", text.FocalLength);
        Assert.Equal("400", text.Iso);
        Assert.Null(text.Aperture);
        Assert.Null(text.Shutter);
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
            new PhotoInfoBase(1, "photo.jpg", ImageFormatId.Jpeg, new PixelSize(2, 2), 2048),
            metadata,
            IsMetadataLoading: false);

        var localizer = Localizer.Create(CultureInfo.GetCultureInfo("ru-RU"));
        var text = PhotoInfoFormatter.Format(state, CultureInfo.GetCultureInfo("ru-RU"), localizer.Get);

        Assert.Contains("18:42", text.CaptureDateTime);
        Assert.Equal(DateTimeKind.Unspecified, metadata.CaptureDateTime.Value.UnspecifiedClockTime.Kind);
    }

    [Fact]
    public void PhotographicFieldsFormatAsDistinctLocalizedValues()
    {
        var metadata = PhotoMetadataSummary.Empty with
        {
            CameraMake = "Nikon",
            CameraModel = "Nikon Z 5",
            LensModel = "NIKKOR Z 50mm f/1.8 S",
            FocalLengthMillimeters = 50,
            Aperture = 4,
            ExposureTime = new PhotoRational(10, 80000),
            Iso = 100,
            ExposureCompensationEv = -2d / 3,
            ExposureMode = PhotoExposureMode.AperturePriority,
            MeteringMode = PhotoMeteringMode.Matrix,
            WhiteBalanceMode = PhotoWhiteBalanceMode.Auto,
            FlashState = PhotoFlashState.DidNotFire,
        };
        var state = new PhotoInfoState(
            new PhotoInfoBase(1, "DSC_0001.JPG", ImageFormatId.Jpeg, new PixelSize(4016, 6016), 16_148_070),
            metadata,
            IsMetadataLoading: false);
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo("en-US"));

        var text = PhotoInfoFormatter.Format(state, CultureInfo.GetCultureInfo("en-US"), localizer.Get);

        Assert.Equal("Nikon Z 5", text.Camera);
        Assert.Equal("NIKKOR Z 50mm f/1.8 S", text.Lens);
        Assert.Equal("50 mm", text.FocalLength);
        Assert.Equal("ƒ/4", text.Aperture);
        Assert.Equal("1/8000 s", text.Shutter);
        Assert.Equal("100", text.Iso);
        Assert.Equal("-0.7 EV", text.ExposureCompensation);
        Assert.Equal("Aperture priority", text.ExposureMode);
        Assert.Equal("Matrix", text.MeteringMode);
        Assert.Equal("Auto", text.WhiteBalance);
        Assert.Equal("Did not fire", text.Flash);
    }
}
