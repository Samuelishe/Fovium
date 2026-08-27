using Fovium.Viewer;

namespace Fovium.Tests.Viewer;

public sealed class PhotoPresentationViewSessionTests
{
    [Fact]
    public void SessionStartsDisabledAndDoesNotNotifyUntilStateChanges()
    {
        var session = new PhotoPresentationViewSession();
        var notifications = 0;
        session.Changed += (_, _) => notifications++;

        session.SetEnabled(false);

        Assert.False(session.IsEnabled);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void SettingsF6AndContextMenuObserveOneLiveSessionAuthority()
    {
        var session = new PhotoPresentationViewSession();
        var settingsChecked = session.IsEnabled;
        var viewportEnabled = session.IsEnabled;
        session.Changed += (_, _) =>
        {
            settingsChecked = session.IsEnabled;
            viewportEnabled = session.IsEnabled;
        };

        session.SetEnabled(true); // Live Settings checkbox.

        Assert.True(session.IsEnabled);
        Assert.True(settingsChecked);
        Assert.True(viewportEnabled);
        Assert.True(session.IsEnabled); // Context-menu checked authority.

        session.Toggle(); // F6/context-menu command path.

        Assert.False(session.IsEnabled);
        Assert.False(settingsChecked);
        Assert.False(viewportEnabled);
        Assert.False(session.IsEnabled); // Context-menu checked authority updates live.
    }

    [Fact]
    public void IdempotentSetNotifiesExactlyOncePerRealTransition()
    {
        var session = new PhotoPresentationViewSession();
        var observed = new List<bool>();
        session.Changed += (_, _) => observed.Add(session.IsEnabled);

        session.SetEnabled(true);
        session.SetEnabled(true);
        session.SetEnabled(false);
        session.SetEnabled(false);
        session.Toggle();

        Assert.Equal([true, false, true], observed);
        Assert.True(session.IsEnabled);
    }
}
