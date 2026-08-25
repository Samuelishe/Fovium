using Fovium.Imaging;
using Fovium.Loading;

namespace Fovium.Tests.Stage;

public sealed class AmbientLifetimeTests
{
    [Fact]
    public void AmbientCostEvictsSpeculativeNeighborBeforeProtectedCurrent()
    {
        using var cache = new ByteBudgetCache<string, DecodedImage>(800, StringComparer.Ordinal);
        var current = StageTestImages.CreateDecoded("current.png", retainedBytes: 256);
        var neighbor = StageTestImages.CreateDecoded("neighbor.png", retainedBytes: 400);
        Assert.True(cache.Add("current", current, protect: true));
        Assert.True(cache.Add("neighbor", neighbor, protect: false));
        var ambient = StageTestImages.CreateAmbient(16, 8);
        Assert.True(current.TryAttachAmbient(ambient));

        var accepted = cache.RefreshCost("current", current);

        Assert.True(accepted);
        Assert.Equal(768, cache.RetainedBytes);
        Assert.False(cache.TryAcquire("neighbor", out _));
        Assert.True(cache.TryAcquire("current", out var currentLease));
        currentLease!.Dispose();
    }

    [Fact]
    public void RejectedAmbientCanBeDetachedWithoutCorruptingCacheAccounting()
    {
        using var cache = new ByteBudgetCache<string, DecodedImage>(800, StringComparer.Ordinal);
        var current = StageTestImages.CreateDecoded(retainedBytes: 256);
        Assert.True(cache.Add("current", current, protect: true));
        var ambient = StageTestImages.CreateAmbient(32, 32);
        Assert.True(current.TryAttachAmbient(ambient));

        Assert.False(cache.RefreshCost("current", current));
        Assert.True(current.RemoveAmbient(ambient));

        Assert.False(current.HasAmbient);
        Assert.Equal(256, cache.RetainedBytes);
        Assert.True(cache.RefreshCost("current", current));
    }

    [Fact]
    public void RetainedDrawLeaseKeepsAmbientAliveAfterCacheOwnerRelease()
    {
        var cache = new ByteBudgetCache<string, DecodedImage>(2_000, StringComparer.Ordinal);
        var decoded = StageTestImages.CreateDecoded(retainedBytes: 256);
        Assert.True(cache.Add("photo", decoded, protect: true));
        var ambient = StageTestImages.CreateAmbient();
        Assert.True(decoded.TryAttachAmbient(ambient));
        Assert.True(cache.RefreshCost("photo", decoded));
        var drawLease = decoded.TryAcquireAmbient();
        var width = drawLease!.Image.Width;

        cache.Dispose();

        Assert.Equal(width, drawLease.Image.Width);
        drawLease.Dispose();
        Assert.Throws<ObjectDisposedException>(() => drawLease.Image.Width);
    }

    [Fact]
    public void ReplacingAmbientOwnerDefersDisposalUntilOldDrawLeaseEnds()
    {
        using var decoded = StageTestImages.CreateDecoded();
        var ambient = StageTestImages.CreateAmbient();
        Assert.True(decoded.TryAttachAmbient(ambient));
        var retainedDraw = decoded.TryAcquireAmbient();

        Assert.True(decoded.RemoveAmbient(ambient));

        Assert.NotNull(retainedDraw);
        Assert.True(retainedDraw!.Image.Width > 0);
        retainedDraw.Dispose();
        Assert.Throws<ObjectDisposedException>(() => retainedDraw.Image.Width);
    }

    [Fact]
    public void ReplacingBlurVariantKeepsOldDrawLeaseAliveAndPublishesNewOwner()
    {
        using var decoded = StageTestImages.CreateDecoded();
        var oldAmbient = StageTestImages.CreateAmbient(blur: 18);
        Assert.True(decoded.TryAttachAmbient(oldAmbient));
        var oldDrawLease = decoded.TryAcquireAmbient();
        var replacement = StageTestImages.CreateAmbient(blur: 24);

        Assert.True(decoded.TrySetAmbient(replacement));
        using var replacementLease = decoded.TryAcquireAmbient();

        Assert.Equal(18, oldDrawLease!.Blur);
        Assert.True(oldDrawLease.Image.Width > 0);
        Assert.Equal(24, replacementLease!.Blur);
        oldDrawLease.Dispose();
        Assert.Throws<ObjectDisposedException>(() => oldDrawLease.Image.Width);
    }
}
