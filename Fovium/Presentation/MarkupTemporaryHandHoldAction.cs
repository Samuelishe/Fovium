using Fovium.Input;

namespace Fovium.Presentation;

internal sealed class MarkupTemporaryHandHoldAction(PresentationOverlaySession session)
    : IViewerHoldAction
{
    public Task BeginAsync(ViewerCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command != ViewerCommand.TemporaryMarkupHand)
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        session.BeginTemporaryHand();
        return Task.CompletedTask;
    }

    public void End() => session.EndTemporaryHand();
}
