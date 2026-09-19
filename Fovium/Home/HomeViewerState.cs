namespace Fovium.Home;

internal enum ViewerContentMode
{
    Home,
    Viewer,
}

internal readonly record struct ViewerOpenTicket(long Revision);

internal sealed class HomeViewerState
{
    private long _revision;

    public ViewerContentMode Mode { get; private set; } = ViewerContentMode.Home;

    public ViewerOpenTicket BeginOpen() => new(++_revision);

    public bool TryPublish(ViewerOpenTicket ticket)
    {
        if (ticket.Revision != _revision)
        {
            return false;
        }

        Mode = ViewerContentMode.Viewer;
        return true;
    }

    public bool TryReturnHome()
    {
        if (Mode == ViewerContentMode.Home)
        {
            return false;
        }

        _revision++;
        Mode = ViewerContentMode.Home;
        return true;
    }
}