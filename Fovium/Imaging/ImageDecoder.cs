namespace Fovium.Imaging;

using System.Diagnostics;
using Fovium.PhotoStyling;

internal sealed class ImageDecoder : IImageLoader<DecodedImage>, IDisposable
{
    public const int DefaultMaximumConcurrentDecodes = 2;

    private readonly IReadOnlyList<IImageDecodeBackend> _backends;
    private readonly SemaphoreSlim _decodeSlots;
    private readonly IPhotoStyleAnalyzer _photoStyleAnalyzer;

    internal ImageDecoder(
        IEnumerable<IImageDecodeBackend> backends,
        int maximumConcurrentDecodes = DefaultMaximumConcurrentDecodes,
        IPhotoStyleAnalyzer? photoStyleAnalyzer = null)
    {
        ArgumentNullException.ThrowIfNull(backends);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentDecodes);
        _backends = backends.ToArray();
        if (_backends.Count == 0)
        {
            throw new ArgumentException("At least one image decode backend is required.", nameof(backends));
        }

        _decodeSlots = new SemaphoreSlim(maximumConcurrentDecodes, maximumConcurrentDecodes);
        _photoStyleAnalyzer = photoStyleAnalyzer ?? new PhotoStyleAnalyzer();
    }

    public static ImageDecoder CreateDefault() =>
        new([new HeifImageDecodeBackend(), new TiffImageDecodeBackend(), new SkiaImageDecodeBackend()]);

    public async Task<ImageLoadResult<DecodedImage>> LoadAsync(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        await _decodeSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => Load(path, allowance, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _decodeSlots.Release();
        }
    }

    public void Dispose()
    {
        foreach (var backend in _backends.OfType<IDisposable>())
        {
            backend.Dispose();
        }

        _decodeSlots.Dispose();
    }

    private ImageLoadResult<DecodedImage> Load(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                return Failure(ImageLoadErrorKind.Missing, "The source file does not exist.");
            }

            foreach (var backend in _backends)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = backend.Decode(path, allowance, cancellationToken);
                if (result.Kind == ImageDecodeBackendResultKind.NotMyFormat)
                {
                    continue;
                }

                if (result.Kind == ImageDecodeBackendResultKind.Success)
                {
                    var image = result.Image
                        ?? throw new InvalidOperationException("A successful backend returned no image.");
                    try
                    {
                        var analysis = _photoStyleAnalyzer.Analyze(image, cancellationToken);
                        _ = image.TryAttachPhotoStyleAnalysis(analysis);
                    }
                    catch (OperationCanceledException)
                    {
                        image.Dispose();
                        throw;
                    }
                    catch (Exception exception)
                    {
                        Debug.WriteLine($"Fovium photo styling analysis failed: {exception}");
                    }

                    return ImageLoadResult<DecodedImage>.Success(
                        image);
                }

                return Failure(MapError(result.Kind), result.TechnicalDetail ?? "Image decode failed.", result.Exception);
            }

            return Failure(ImageLoadErrorKind.Unsupported, "Fovium does not support the detected image content.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            return Failure(ImageLoadErrorKind.Missing, exception.Message, exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            return Failure(ImageLoadErrorKind.Missing, exception.Message, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ImageLoadErrorKind.DecodeFailed, "Access to the source was denied.", exception);
        }
        catch (OverflowException exception)
        {
            return Failure(ImageLoadErrorKind.ResourceLimit, "Decoded resource estimation overflowed.", exception);
        }
        catch (IOException exception)
        {
            return Failure(ImageLoadErrorKind.DecodeFailed, exception.Message, exception);
        }
        catch (InvalidOperationException exception)
        {
            return Failure(ImageLoadErrorKind.DecodeFailed, exception.Message, exception);
        }
    }

    private static ImageLoadErrorKind MapError(ImageDecodeBackendResultKind kind) =>
        kind switch
        {
            ImageDecodeBackendResultKind.UnsupportedVariant => ImageLoadErrorKind.Unsupported,
            ImageDecodeBackendResultKind.Corrupt => ImageLoadErrorKind.Corrupt,
            ImageDecodeBackendResultKind.ResourceLimit => ImageLoadErrorKind.ResourceLimit,
            ImageDecodeBackendResultKind.BackendUnavailable => ImageLoadErrorKind.DecodeFailed,
            ImageDecodeBackendResultKind.DecodeFailed => ImageLoadErrorKind.DecodeFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static ImageLoadResult<DecodedImage> Failure(
        ImageLoadErrorKind kind,
        string detail,
        Exception? exception = null) =>
        ImageLoadResult<DecodedImage>.Failure(new ImageLoadError(kind, detail, exception));
}
