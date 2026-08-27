using Fovium.ColorManagement;

namespace Fovium.Tests.ColorManagement;

public sealed class ManagedPhotoPublicationPolicyTests
{
    [Fact]
    public void SameSourceSameDestinationGeometryChangesNeverEnterBlankPresentationState()
    {
        var acquiredProxy = ManagedPhotoPublicationPolicy.Resolve(
            presentationAcquired: true,
            ManagedPhotoPendingReason.GeometryRefinementPending);

        Assert.False(acquiredProxy.SuppressLegacyPhoto);
        Assert.True(acquiredProxy.PhotoPresentationVisible);
        Assert.False(acquiredProxy.GeometryOnlyBlackFallback);

        var geometryCoverageMiss = ManagedPhotoPublicationPolicy.Resolve(
            presentationAcquired: false,
            ManagedPhotoPendingReason.GeometryRefinementPending);

        Assert.True(geometryCoverageMiss.SuppressLegacyPhoto);
        Assert.False(geometryCoverageMiss.PhotoPresentationVisible);
        Assert.True(geometryCoverageMiss.GeometryOnlyBlackFallback);

        AssertStrictFallback(ManagedPhotoPendingReason.NoPresentationYet);
        AssertStrictFallback(ManagedPhotoPendingReason.SourceChanged);
        AssertStrictFallback(ManagedPhotoPendingReason.DestinationChanged);
    }

    private static void AssertStrictFallback(ManagedPhotoPendingReason reason)
    {
        var decision = ManagedPhotoPublicationPolicy.Resolve(
            presentationAcquired: false,
            reason);

        Assert.True(decision.SuppressLegacyPhoto);
        Assert.False(decision.PhotoPresentationVisible);
        Assert.False(decision.GeometryOnlyBlackFallback);
    }
}
