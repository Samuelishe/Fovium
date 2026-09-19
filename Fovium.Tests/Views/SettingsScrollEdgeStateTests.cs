using Fovium.Views;

namespace Fovium.Tests.Views;

public sealed class SettingsScrollEdgeStateTests
{
    [Fact]
    public void NoOverflowShowsNeitherFade()
    {
        Assert.Equal(
            new SettingsScrollEdgeState(false, false),
            SettingsScrollEdgeState.Resolve(0, 500, 600));
    }

    [Fact]
    public void ExactTopShowsOnlyBottomFade()
    {
        Assert.Equal(
            new SettingsScrollEdgeState(false, true),
            SettingsScrollEdgeState.Resolve(0, 1000, 500));
    }

    [Fact]
    public void MidScrollShowsBothFades()
    {
        Assert.Equal(
            new SettingsScrollEdgeState(true, true),
            SettingsScrollEdgeState.Resolve(250.25, 1000.5, 500));
    }

    [Fact]
    public void ExactBottomShowsOnlyTopFade()
    {
        Assert.Equal(
            new SettingsScrollEdgeState(true, false),
            SettingsScrollEdgeState.Resolve(500, 1000, 500));
    }

    [Fact]
    public void FractionalThresholdAvoidsFadeFlickerAtBothEdges()
    {
        Assert.False(SettingsScrollEdgeState.Resolve(0.5, 1000, 500).ShowTopFade);
        Assert.True(SettingsScrollEdgeState.Resolve(0.51, 1000, 500).ShowTopFade);
        Assert.False(SettingsScrollEdgeState.Resolve(499.5, 1000, 500).ShowBottomFade);
        Assert.True(SettingsScrollEdgeState.Resolve(499.49, 1000, 500).ShowBottomFade);
    }
}