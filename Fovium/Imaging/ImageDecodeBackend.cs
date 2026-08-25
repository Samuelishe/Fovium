namespace Fovium.Imaging;

internal enum ImageDecodeBackendResultKind
{
    NotMyFormat,
    Success,
    UnsupportedVariant,
    Corrupt,
    ResourceLimit,
    DecodeFailed,
}

internal sealed class ImageDecodeBackendResult
{
    private ImageDecodeBackendResult(
        ImageDecodeBackendResultKind kind,
        DecodedImage? image,
        string? technicalDetail,
        Exception? exception)
    {
        Kind = kind;
        Image = image;
        TechnicalDetail = technicalDetail;
        Exception = exception;
    }

    public ImageDecodeBackendResultKind Kind { get; }

    public DecodedImage? Image { get; }

    public string? TechnicalDetail { get; }

    public Exception? Exception { get; }

    public static ImageDecodeBackendResult NotMyFormat() =>
        new(ImageDecodeBackendResultKind.NotMyFormat, null, null, null);

    public static ImageDecodeBackendResult Success(DecodedImage image) =>
        new(ImageDecodeBackendResultKind.Success, image, null, null);

    public static ImageDecodeBackendResult Failure(
        ImageDecodeBackendResultKind kind,
        string technicalDetail,
        Exception? exception = null)
    {
        if (kind is ImageDecodeBackendResultKind.NotMyFormat or ImageDecodeBackendResultKind.Success)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new ImageDecodeBackendResult(kind, null, technicalDetail, exception);
    }
}

internal interface IImageDecodeBackend
{
    ImageDecodeBackendResult Decode(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken);
}
