using System.Globalization;
using Fovium.Imaging;
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
    string? Exposure,
    string Dimensions,
    string? CaptureDateTime,
    string File);

internal static class PhotoInfoFormatter
{
    public static PhotoInfoText Format(PhotoInfoState state, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(culture);
        var metadata = state.Metadata;
        return new PhotoInfoText(
            CombineDistinct(metadata.CameraMake, metadata.CameraModel),
            CombineDistinct(metadata.LensMake, metadata.LensModel, preferSecond: true),
            FormatExposureLine(metadata),
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
        if (seconds < 0.75 && value.Numerator == 1)
        {
            return $"1/{value.Denominator}";
        }

        return seconds < 1
            ? $"{seconds.ToString("0.#", CultureInfo.InvariantCulture)} s"
            : $"{seconds.ToString("0.#", CultureInfo.InvariantCulture)} s";
    }

    public static string? FormatAperture(double? aperture) =>
        aperture is > 0 and var value && double.IsFinite(value)
            ? $"ƒ/{value.ToString("0.#", CultureInfo.InvariantCulture)}"
            : null;

    public static string? FormatFocalLength(double? focalLength) =>
        focalLength is > 0 and var value && double.IsFinite(value)
            ? $"{value.ToString("0.#", CultureInfo.InvariantCulture)} mm"
            : null;

    public static string? FormatIso(int? iso) => iso is > 0 ? $"ISO {iso.Value}" : null;

    private static string? FormatExposureLine(PhotoMetadataSummary metadata)
    {
        var values = new[]
        {
            FormatFocalLength(metadata.FocalLengthMillimeters),
            FormatAperture(metadata.Aperture),
            FormatExposure(metadata.ExposureTime),
            FormatIso(metadata.Iso),
        }.Where(value => value is not null);
        var line = string.Join(" · ", values!);
        return line.Length == 0 ? null : line;
    }

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
