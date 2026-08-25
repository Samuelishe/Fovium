namespace Fovium.Input;

internal sealed class ViewerHoldActionRouter(
    IViewerHoldAction inspection,
    IViewerHoldAction markup) : IViewerHoldAction
{
    private IViewerHoldAction? _active;

    public async Task BeginAsync(ViewerCommand command, CancellationToken cancellationToken)
    {
        var action = command == ViewerCommand.TemporaryMarkupHand ? markup : inspection;
        _active = action;
        try
        {
            await action.BeginAsync(command, cancellationToken);
        }
        catch
        {
            _active = null;
            throw;
        }
    }

    public void End()
    {
        var action = _active;
        _active = null;
        action?.End();
    }
}
