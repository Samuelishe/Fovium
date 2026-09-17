using System.Globalization;
using Fovium.Imaging;
using Fovium.Localization;
using Fovium.Rendering;

namespace Fovium.Metadata;

internal sealed record PhotoInfoBase(
    long ImageIdentity,
    string SourcePath,
    ImageFormatId EncodedFormat,
    PixelSize OrientedSize,
    long EncodedBytes);

internal sealed record PhotoInfoState(
    PhotoInfoBase Base,
    PhotoMetadataSummary Metadata,
    bool IsMetadataLoading);

internal sealed record PhotoInfoText(
    string? Camera,
    string? Lens,
    string? FocalLength,
    string? Aperture,
    string? Shutter,
    string? Iso,
    string? ExposureCompensation,
    string? ExposureMode,
    string? MeteringMode,
    string? WhiteBalance,
    string? Flash,
    string Dimensions,
    string? CaptureDateTime,
    string File);

internal static class PhotoInfoFormatter
{
    public static PhotoInfoText Format(PhotoInfoState state, CultureInfo culture) =>
        Format(state, culture, static key => key);

    public static PhotoInfoText Format(
        PhotoInfoState state,
        CultureInfo culture,
        Func<string, string> localize)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(localize);
        var metadata = state.Metadata;
        return new PhotoInfoText(
            CombineDistinct(metadata.CameraMake, metadata.CameraModel),
            CombineDistinct(metadata.LensMake, metadata.LensModel, preferSecond: true),
            FormatFocalLength(metadata.FocalLengthMillimeters),
            FormatAperture(metadata.Aperture),
            FormatExposure(metadata.ExposureTime),
            FormatIso(metadata.Iso),
            FormatExposureCompensation(metadata.ExposureCompensationEv),
            FormatExposureMode(metadata.ExposureMode, localize),
            FormatMeteringMode(metadata.MeteringMode, localize),
            FormatWhiteBalance(metadata.WhiteBalanceMode, localize),
            FormatFlash(metadata.FlashState, localize),
            FormatDimensions(state.Base.OrientedSize, culture),
            metadata.CaptureDateTime is { } captured
                ? captured.UnspecifiedClockTime.ToString("d MMM yyyy · HH:mm", culture)
                : null,
            FormatFile(state.Base, culture));
    }

    public static string? FormatExposure(PhotoRational? exposure)
    {
        if (exposure is not { IsValid: true } value || value.Numerator <= 0 || value.Denominator <= 0)
        {
            return null;
        }

        var seconds = value.Value;
        if (seconds < 0.75)
        {
            var reciprocal = 1d / seconds;
            var roundedReciprocal = Math.Round(reciprocal);
            if (roundedReciprocal >= 2 &&
                Math.Abs(reciprocal - roundedReciprocal) <= reciprocal * 0.000_001)
            {
                return $"1/{roundedReciprocal.ToString("0", CultureInfo.InvariantCulture)} s";
            }
        }

        var format = seconds >= 0.1 ? "0.#" : "0.########";
        var formattedSeconds = seconds.ToString(format, CultureInfo.InvariantCulture);
        if (formattedSeconds == "0")
        {
            formattedSeconds = seconds.ToString("G6", CultureInfo.InvariantCulture);
        }

        return $"{formattedSeconds} s";
    }

    public static string? FormatAperture(double? aperture) =>
        aperture is > 0 and var value && double.IsFinite(value)
            ? $"ƒ/{value.ToString("0.#", CultureInfo.InvariantCulture)}"
            : null;

    public static string? FormatFocalLength(double? focalLength) =>
        focalLength is > 0 and var value && double.IsFinite(value)
            ? $"{value.ToString("0.#", CultureInfo.InvariantCulture)} mm"
            : null;

    public static string? FormatIso(int? iso) => iso is > 0
        ? iso.Value.ToString(CultureInfo.InvariantCulture)
        : null;

    public static string? FormatExposureCompensation(double? exposureCompensationEv)
    {
        if (exposureCompensationEv is not { } value || !double.IsFinite(value))
        {
            return null;
        }

        var normalized = Math.Abs(value) < 0.05 ? 0 : value;
        var prefix = normalized > 0 ? "+" : string.Empty;
        return $"{prefix}{normalized.ToString("0.#", CultureInfo.InvariantCulture)} EV";
    }

    private static string? FormatExposureMode(
        PhotoExposureMode? mode,
        Func<string, string> localize) => mode switch
    {
        PhotoExposureMode.Auto => localize(UiStrings.PhotoInfoExposureModeAuto),
        PhotoExposureMode.Manual => localize(UiStrings.PhotoInfoExposureModeManual),
        PhotoExposureMode.Program => localize(UiStrings.PhotoInfoExposureModeProgram),
        PhotoExposureMode.AperturePriority => localize(UiStrings.PhotoInfoExposureModeAperturePriority),
        PhotoExposureMode.ShutterPriority => localize(UiStrings.PhotoInfoExposureModeShutterPriority),
        PhotoExposureMode.CreativeProgram => localize(UiStrings.PhotoInfoExposureModeCreativeProgram),
        PhotoExposureMode.ActionProgram => localize(UiStrings.PhotoInfoExposureModeActionProgram),
        PhotoExposureMode.Portrait => localize(UiStrings.PhotoInfoExposureModePortrait),
        PhotoExposureMode.Landscape => localize(UiStrings.PhotoInfoExposureModeLandscape),
        PhotoExposureMode.AutoBracket => localize(UiStrings.PhotoInfoExposureModeAutoBracket),
        _ => null,
    };

    private static string? FormatMeteringMode(
        PhotoMeteringMode? mode,
        Func<string, string> localize) => mode switch
    {
        PhotoMeteringMode.Average => localize(UiStrings.PhotoInfoMeteringAverage),
        PhotoMeteringMode.CenterWeightedAverage => localize(UiStrings.PhotoInfoMeteringCenterWeighted),
        PhotoMeteringMode.Spot => localize(UiStrings.PhotoInfoMeteringSpot),
        PhotoMeteringMode.MultiSpot => localize(UiStrings.PhotoInfoMeteringMultiSpot),
        PhotoMeteringMode.Matrix => localize(UiStrings.PhotoInfoMeteringMatrix),
        PhotoMeteringMode.Partial => localize(UiStrings.PhotoInfoMeteringPartial),
        PhotoMeteringMode.Other => localize(UiStrings.PhotoInfoMeteringOther),
        _ => null,
    };

    private static string? FormatWhiteBalance(
        PhotoWhiteBalanceMode? mode,
        Func<string, string> localize) => mode switch
    {
        PhotoWhiteBalanceMode.Auto => localize(UiStrings.PhotoInfoWhiteBalanceAuto),
        PhotoWhiteBalanceMode.Manual => localize(UiStrings.PhotoInfoWhiteBalanceManual),
        _ => null,
    };

    private static string? FormatFlash(
        PhotoFlashState? state,
        Func<string, string> localize) => state switch
    {
        PhotoFlashState.DidNotFire => localize(UiStrings.PhotoInfoFlashDidNotFire),
        PhotoFlashState.Fired => localize(UiStrings.PhotoInfoFlashFired),
        _ => null,
    };

    private static string FormatDimensions(PixelSize size, CultureInfo culture)
    {
        var megapixels = size.Width * (double)size.Height / 1_000_000;
        return $"{size.Width} × {size.Height} · {megapixels.ToString("0.#", culture)} MP";
    }

    private static string FormatFile(PhotoInfoBase info, CultureInfo culture)
    {
        var size = info.EncodedBytes >= 1024 * 1024
            ? $"{(info.EncodedBytes / (1024d * 1024)).ToString("0.#", culture)} MB"
            : $"{Math.Max(1, Math.Round(info.EncodedBytes / 1024d)).ToString("0", culture)} KB";
        var format = ImageFormatCapabilities.Get(info.EncodedFormat).DisplayName;
        return $"{Path.GetFileName(info.SourcePath)} · {format} · {size}";
    }

    private static string? CombineDistinct(string? first, string? second, bool preferSecond = false)
    {
        if (second is null)
        {
            return first;
        }

        if (first is null || second.StartsWith(first, StringComparison.OrdinalIgnoreCase))
        {
            return second;
        }

        if (preferSecond && second.Contains(first, StringComparison.OrdinalIgnoreCase))
        {
            return second;
        }

        return $"{first} {second}";
    }
}
