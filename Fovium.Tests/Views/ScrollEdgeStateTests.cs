using Fovium.Views;

namespace Fovium.Tests.Views;

public sealed class ScrollEdgeStateTests
{
    [Fact]
    public void NoOverflowShowsNeitherFade()
    {
        Assert.Equal(
            new ScrollEdgeState(false, false),
            ScrollEdgeState.Resolve(0, 500, 600));
    }

    [Fact]
    public void ExactTopShowsOnlyBottomFade()
    {
        Assert.Equal(
            new ScrollEdgeState(false, true),
            ScrollEdgeState.Resolve(0, 1000, 500));
    }

    [Fact]
    public void MidScrollShowsBothFades()
    {
        Assert.Equal(
            new ScrollEdgeState(true, true),
            ScrollEdgeState.Resolve(250.25, 1000.5, 500));
    }

    [Fact]
    public void ExactBottomShowsOnlyTopFade()
    {
        Assert.Equal(
            new ScrollEdgeState(true, false),
            ScrollEdgeState.Resolve(500, 1000, 500));
    }

    [Fact]
    public void FractionalThresholdAvoidsFadeFlickerAtBothEdges()
    {
        Assert.False(ScrollEdgeState.Resolve(0.5, 1000, 500).ShowTopFade);
        Assert.True(ScrollEdgeState.Resolve(0.51, 1000, 500).ShowTopFade);
        Assert.False(ScrollEdgeState.Resolve(499.5, 1000, 500).ShowBottomFade);
        Assert.True(ScrollEdgeState.Resolve(499.49, 1000, 500).ShowBottomFade);
    }
}