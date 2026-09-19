using Fovium.Home;

namespace Fovium.Tests.Home;

public sealed class RecentThumbnailLoadingPolicyTests
{
    [Fact]
    public void InitialWindowRequestsOnlyVisibleCardsAndOneLookAhead()
    {
        var window = RecentThumbnailLoadingPolicy.Resolve(0, 824, 206, 20);

        Assert.Equal(new RecentThumbnailWindow(0, 4), window);
        Assert.True(window.Contains(0));
        Assert.True(window.Contains(4));
        Assert.False(window.Contains(5));
    }

    [Fact]
    public void ScrolledWindowMovesLookAheadWithoutGrowingToHistoryCapacity()
    {
        var window = RecentThumbnailLoadingPolicy.Resolve(1_030, 824, 206, 20);

        Assert.Equal(new RecentThumbnailWindow(4, 9), window);
        Assert.Equal(6, window.LastIndex - window.FirstIndex + 1);
    }

    [Fact]
    public void EndWindowClampsToLastHistoryItem()
    {
        var window = RecentThumbnailLoadingPolicy.Resolve(3_500, 824, 206, 20);

        Assert.Equal(15, window.FirstIndex);
        Assert.Equal(19, window.LastIndex);
    }

    [Fact]
    public void PreLayoutWindowUsesBoundedInitialVisibleEstimate()
    {
        var window = RecentThumbnailLoadingPolicy.Resolve(0, 0, 206, 20);

        Assert.Equal(new RecentThumbnailWindow(0, 5), window);
    }
}