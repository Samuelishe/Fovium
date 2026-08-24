using Fovium.Imaging;
using Fovium.Loading;

namespace Fovium.Stage;

internal interface IAmbientImageRepository
{
    bool TryAcquire(string path, out SharedResourceLease<DecodedImage>? image);

    bool RefreshRetainedCost(string path, DecodedImage image);

    Task WaitForAdjacentPreloadAsync(CancellationToken cancellationToken);

    IReadOnlyList<CachedResourceLease<DecodedImage>> AcquireAdjacent();
}

internal sealed class AmbientImageRepository(ViewerSession<DecodedImage> session)
    : IAmbientImageRepository
{
    public bool TryAcquire(string path, out SharedResourceLease<DecodedImage>? image) =>
        session.TryAcquireCached(path, out image);

    public bool RefreshRetainedCost(string path, DecodedImage image) =>
        session.RefreshCachedCost(path, image);

    public Task WaitForAdjacentPreloadAsync(CancellationToken cancellationToken) =>
        session.WaitForAdjacentPreloadAsync(cancellationToken);

    public IReadOnlyList<CachedResourceLease<DecodedImage>> AcquireAdjacent() =>
        session.AcquireCachedAdjacent();
}
