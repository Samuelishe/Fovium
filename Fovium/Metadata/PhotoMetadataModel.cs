namespace Fovium.Metadata;

internal readonly record struct PhotoRational(long Numerator, long Denominator)
{
    public bool IsValid => Denominator != 0;

    public double Value => IsValid ? (double)Numerator / Denominator : double.NaN;
}

internal readonly record struct PhotoCaptureTime(DateTime RecordedTime, TimeSpan? Offset)
{
    public DateTime UnspecifiedClockTime => DateTime.SpecifyKind(RecordedTime, DateTimeKind.Unspecified);
}

internal sealed record PhotoMetadataSummary(
    string? CameraMake,
    string? CameraModel,
    string? LensMake,
    string? LensModel,
    double? FocalLengthMillimeters,
    double? Aperture,
    PhotoRational? ExposureTime,
    int? Iso,
    PhotoCaptureTime? CaptureDateTime)
{
    public static PhotoMetadataSummary Empty { get; } = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public bool HasUsefulMetadata =>
        CameraMake is not null ||
        CameraModel is not null ||
        LensMake is not null ||
        LensModel is not null ||
        FocalLengthMillimeters is not null ||
        Aperture is not null ||
        ExposureTime is not null ||
        Iso is not null ||
        CaptureDateTime is not null;
}

internal enum PhotoMetadataReadStatus
{
    Success,
    NoMetadata,
    Failed,
}

internal sealed record PhotoMetadataReadResult(
    PhotoMetadataReadStatus Status,
    PhotoMetadataSummary Summary)
{
    public static PhotoMetadataReadResult FromSummary(PhotoMetadataSummary summary) =>
        new(
            summary.HasUsefulMetadata ? PhotoMetadataReadStatus.Success : PhotoMetadataReadStatus.NoMetadata,
            summary);

    public static PhotoMetadataReadResult Failed { get; } =
        new(PhotoMetadataReadStatus.Failed, PhotoMetadataSummary.Empty);
}

internal interface IPhotoMetadataReader
{
    Task<PhotoMetadataReadResult> ReadAsync(
        ReadOnlyMemory<byte> encodedSource,
        CancellationToken cancellationToken);
}
