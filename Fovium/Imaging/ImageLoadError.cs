namespace Fovium.Imaging;

internal enum ImageLoadErrorKind
{
    Missing,
    Unsupported,
    Corrupt,
    ResourceLimit,
    DecodeFailed,
}

internal sealed record ImageLoadError(
    ImageLoadErrorKind Kind,
    string TechnicalDetail,
    Exception? Exception = null);

internal readonly record struct ImageLoadAllowance(
    long MaximumWorkingBytes,
    long MaximumRetainedBytes,
    bool IsSpeculative);

internal sealed class ImageLoadResult<T> where T : class, IDisposable
{
    private ImageLoadResult(T? image, ImageLoadError? error)
    {
        Image = image;
        Error = error;
    }

    public T? Image { get; }

    public ImageLoadError? Error { get; }

    public bool IsSuccess => Image is not null;

    public static ImageLoadResult<T> Success(T image) => new(image, null);

    public static ImageLoadResult<T> Failure(ImageLoadError error) => new(null, error);
}

internal interface IImageLoader<T> where T : class, IDisposable
{
    Task<ImageLoadResult<T>> LoadAsync(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken);
}
