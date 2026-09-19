using Fovium.Home;

namespace Fovium.Tests.Home;

public sealed class HomeViewerStateTests
{
    [Fact]
    public void StartsAtHomeAndSupportsRepeatedViewerCycles()
    {
        var state = new HomeViewerState();

        Assert.Equal(ViewerContentMode.Home, state.Mode);
        Assert.True(state.TryPublish(state.BeginOpen()));
        Assert.Equal(ViewerContentMode.Viewer, state.Mode);

        Assert.True(state.TryReturnHome());

        Assert.Equal(ViewerContentMode.Home, state.Mode);
        Assert.True(state.TryPublish(state.BeginOpen()));
        Assert.Equal(ViewerContentMode.Viewer, state.Mode);
    }

    [Fact]
    public void ReturningHomeRejectsAStalePublication()
    {
        var state = new HomeViewerState();
        var ticket = state.BeginOpen();
        Assert.True(state.TryPublish(state.BeginOpen()));

        Assert.True(state.TryReturnHome());

        Assert.False(state.TryPublish(ticket));
        Assert.Equal(ViewerContentMode.Home, state.Mode);
    }

    [Fact]
    public void ClosePhotoWhileHomeIsANoOpAndDoesNotCancelPendingOpen()
    {
        var state = new HomeViewerState();
        var ticket = state.BeginOpen();

        Assert.False(state.TryReturnHome());
        Assert.True(state.TryPublish(ticket));
        Assert.Equal(ViewerContentMode.Viewer, state.Mode);
    }

    [Fact]
    public void NewerOpenRejectsAnOlderPublication()
    {
        var state = new HomeViewerState();
        var older = state.BeginOpen();
        var newer = state.BeginOpen();

        Assert.False(state.TryPublish(older));
        Assert.True(state.TryPublish(newer));
        Assert.Equal(ViewerContentMode.Viewer, state.Mode);
    }
}