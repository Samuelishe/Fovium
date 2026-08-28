using Fovium.Imaging;
using Fovium.Loading;

namespace Fovium.Stage;

internal interface IStageImageRepository
{
    event EventHandler? AdjacentImageAvailable;

    bool TryAcquire(string path, out SharedResourceLease<DecodedImage>? image);

    bool RefreshRetainedCost(string path, DecodedImage image);

    IReadOnlyList<CachedResourceLease<DecodedImage>> AcquireAdjacent();
}

internal sealed class StageImageRepository(ViewerSession<DecodedImage> session)
    : IStageImageRepository
{
    public event EventHandler? AdjacentImageAvailable
    {
        add => session.AdjacentPreloadProgressed += value;
        remove => session.AdjacentPreloadProgressed -= value;
    }

    public bool TryAcquire(string path, out SharedResourceLease<DecodedImage>? image) =>
        session.TryAcquireCached(path, out image);

    public bool RefreshRetainedCost(string path, DecodedImage image) =>
        session.RefreshCachedCost(path, image);

    public IReadOnlyList<CachedResourceLease<DecodedImage>> AcquireAdjacent() =>
        session.AcquireCachedAdjacent();
}
