using System.Globalization;
using System.Runtime.InteropServices;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace Fovium.Metadata;

internal sealed class MetadataExtractorPhotoMetadataReader : IPhotoMetadataReader
{
    private const int TagOffsetTimeOriginal = 0x9011;

    public Task<PhotoMetadataReadResult> ReadAsync(
        ReadOnlyMemory<byte> encodedSource,
        CancellationToken cancellationToken)
    {
        if (encodedSource.IsEmpty)
        {
            return Task.FromResult(PhotoMetadataReadResult.FromSummary(PhotoMetadataSummary.Empty));
        }

        return Task.Run(
            () => Read(encodedSource, cancellationToken),
            cancellationToken);
    }

    private static PhotoMetadataReadResult Read(
        ReadOnlyMemory<byte> encodedSource,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!MemoryMarshal.TryGetArray(encodedSource, out var segment) || segment.Array is null)
            {
                return PhotoMetadataReadResult.Failed;
            }

            using var stream = new MemoryStream(
                segment.Array,
                segment.Offset,
                segment.Count,
                writable: false,
                publiclyVisible: true);
            var directories = ImageMetadataReader.ReadMetadata(stream);
            cancellationToken.ThrowIfCancellationRequested();
            return PhotoMetadataReadResult.FromSummary(Map(directories));
        }
        catch (ImageProcessingException)
        {
            return PhotoMetadataReadResult.Failed;
        }
        catch (IOException)
        {
            return PhotoMetadataReadResult.Failed;
        }
        catch (ArgumentException)
        {
            return PhotoMetadataReadResult.Failed;
        }
        catch (OverflowException)
        {
            return PhotoMetadataReadResult.Failed;
        }
    }

    private static PhotoMetadataSummary Map(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var make = Clean(ifd0?.GetString(ExifDirectoryBase.TagMake));
        var model = Clean(ifd0?.GetString(ExifDirectoryBase.TagModel));
        var lensMake = Clean(subIfd?.GetString(ExifDirectoryBase.TagLensMake));
        var lensModel = Clean(subIfd?.GetString(ExifDirectoryBase.TagLensModel));
        var focalLength = TryGetPositiveDouble(subIfd, ExifDirectoryBase.TagFocalLength);
        var aperture = TryGetPositiveDouble(subIfd, ExifDirectoryBase.TagFNumber);
        var exposure = TryGetRational(subIfd, ExifDirectoryBase.TagExposureTime);
        var iso = TryGetPositiveInt(subIfd, ExifDirectoryBase.TagIsoEquivalent);
        var exposureCompensation = TryGetFiniteDouble(subIfd, ExifDirectoryBase.TagExposureBias);
        var exposureMode = TryGetExposureMode(subIfd);
        var meteringMode = TryGetMeteringMode(subIfd);
        var whiteBalanceMode = TryGetWhiteBalanceMode(subIfd);
        var flashState = TryGetFlashState(subIfd);
        var capture = TryGetCaptureTime(subIfd) ?? TryGetCaptureTime(ifd0);

        return new PhotoMetadataSummary(
            make,
            model,
            lensMake,
            lensModel,
            focalLength,
            aperture,
            exposure,
            iso,
            exposureCompensation,
            exposureMode,
            meteringMode,
            whiteBalanceMode,
            flashState,
            capture);
    }

    private static double? TryGetFiniteDouble(MetadataExtractor.Directory? directory, int tag)
    {
        return directory is not null && directory.TryGetDouble(tag, out var value) && double.IsFinite(value)
            ? value
            : null;
    }

    private static double? TryGetPositiveDouble(MetadataExtractor.Directory? directory, int tag)
    {
        if (directory is null || !directory.TryGetDouble(tag, out var value) ||
            !double.IsFinite(value) || value <= 0)
        {
            return null;
        }

        return value;
    }

    private static int? TryGetPositiveInt(MetadataExtractor.Directory? directory, int tag)
    {
        return directory is not null && directory.TryGetInt32(tag, out var value) && value > 0
            ? value
            : null;
    }

    private static PhotoRational? TryGetRational(MetadataExtractor.Directory? directory, int tag)
    {
        if (directory is null || !directory.TryGetRational(tag, out var value) || value.Denominator == 0)
        {
            return null;
        }

        return new PhotoRational(value.Numerator, value.Denominator);
    }

    private static PhotoExposureMode? TryGetExposureMode(MetadataExtractor.Directory? directory)
    {
        if (directory is null)
        {
            return null;
        }

        if (directory.TryGetInt32(ExifDirectoryBase.TagExposureProgram, out var program))
        {
            var mappedProgram = program switch
            {
                1 => PhotoExposureMode.Manual,
                2 => PhotoExposureMode.Program,
                3 => PhotoExposureMode.AperturePriority,
                4 => PhotoExposureMode.ShutterPriority,
                5 => PhotoExposureMode.CreativeProgram,
                6 => PhotoExposureMode.ActionProgram,
                7 => PhotoExposureMode.Portrait,
                8 => PhotoExposureMode.Landscape,
                _ => (PhotoExposureMode?)null,
            };
            if (mappedProgram is not null)
            {
                return mappedProgram;
            }
        }

        return directory.TryGetInt32(ExifDirectoryBase.TagExposureMode, out var mode)
            ? mode switch
            {
                0 => PhotoExposureMode.Auto,
                1 => PhotoExposureMode.Manual,
                2 => PhotoExposureMode.AutoBracket,
                _ => null,
            }
            : null;
    }

    private static PhotoMeteringMode? TryGetMeteringMode(MetadataExtractor.Directory? directory)
    {
        return directory is not null && directory.TryGetInt32(ExifDirectoryBase.TagMeteringMode, out var value)
            ? value switch
            {
                1 => PhotoMeteringMode.Average,
                2 => PhotoMeteringMode.CenterWeightedAverage,
                3 => PhotoMeteringMode.Spot,
                4 => PhotoMeteringMode.MultiSpot,
                5 => PhotoMeteringMode.Matrix,
                6 => PhotoMeteringMode.Partial,
                255 => PhotoMeteringMode.Other,
                _ => null,
            }
            : null;
    }

    private static PhotoWhiteBalanceMode? TryGetWhiteBalanceMode(MetadataExtractor.Directory? directory)
    {
        return directory is not null && directory.TryGetInt32(
            ExifDirectoryBase.TagWhiteBalanceMode,
            out var value)
            ? value switch
            {
                0 => PhotoWhiteBalanceMode.Auto,
                1 => PhotoWhiteBalanceMode.Manual,
                _ => null,
            }
            : null;
    }

    private static PhotoFlashState? TryGetFlashState(MetadataExtractor.Directory? directory)
    {
        return directory is not null && directory.TryGetInt32(ExifDirectoryBase.TagFlash, out var value) && value >= 0
            ? (value & 1) == 1
                ? PhotoFlashState.Fired
                : PhotoFlashState.DidNotFire
            : null;
    }

    private static PhotoCaptureTime? TryGetCaptureTime(MetadataExtractor.Directory? directory)
    {
        if (directory is null)
        {
            return null;
        }

        var tag = directory.ContainsTag(ExifDirectoryBase.TagDateTimeOriginal)
            ? ExifDirectoryBase.TagDateTimeOriginal
            : ExifDirectoryBase.TagDateTime;
        if (!directory.TryGetDateTime(tag, out var dateTime))
        {
            return null;
        }

        var offset = TryParseOffset(directory.GetString(TagOffsetTimeOriginal));
        return new PhotoCaptureTime(DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified), offset);
    }

    private static TimeSpan? TryParseOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length != 6 || (text[0] != '+' && text[0] != '-') || text[3] != ':' ||
            !int.TryParse(text.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(text.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            hours > 14 || minutes > 59)
        {
            return null;
        }

        var offset = new TimeSpan(hours, minutes, 0);
        return text[0] == '-' ? -offset : offset;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('\0');
}
