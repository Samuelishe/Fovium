using Fovium.Home;

namespace Fovium.Tests.Home;

public sealed class HomeCursorPolicyTests
{
    [Theory]
    [InlineData((int)ViewerContentMode.Home, false, false, false)]
    [InlineData((int)ViewerContentMode.Viewer, false, false, true)]
    [InlineData((int)ViewerContentMode.Viewer, true, false, false)]
    [InlineData((int)ViewerContentMode.Viewer, false, true, false)]
    public void IdleTimerRunsOnlyForAnActiveUnblockedViewer(
        int modeValue,
        bool canceled,
        bool menuOpen,
        bool expected)
    {
        Assert.Equal(expected, HomeCursorPolicy.ShouldRunViewerIdleTimer(
            (ViewerContentMode)modeValue,
            canceled,
            menuOpen));
    }

    [Theory]
    [InlineData((int)ViewerContentMode.Home, false, true, 10, false)]
    [InlineData((int)ViewerContentMode.Viewer, true, true, 10, false)]
    [InlineData((int)ViewerContentMode.Viewer, false, false, 10, false)]
    [InlineData((int)ViewerContentMode.Viewer, false, true, 1, false)]
    [InlineData((int)ViewerContentMode.Viewer, false, true, 2, true)]
    public void CursorHideRequiresViewerAuthorityAndFullIdleDelay(
        int modeValue,
        bool menuOpen,
        bool hasActivity,
        double idleSeconds,
        bool expected)
    {
        Assert.Equal(expected, HomeCursorPolicy.ShouldHideViewerCursor(
            (ViewerContentMode)modeValue,
            menuOpen,
            hasActivity,
            TimeSpan.FromSeconds(idleSeconds),
            TimeSpan.FromSeconds(1.75)));
    }
}