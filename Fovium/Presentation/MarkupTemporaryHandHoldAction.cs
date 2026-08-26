using Fovium.Input;

namespace Fovium.Presentation;

internal sealed class MarkupTemporaryHandHoldAction(
    PresentationOverlaySession session,
    Func<bool>? allowWithoutMarkupTools = null)
    : IViewerHoldAction
{
    public Task BeginAsync(ViewerCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command != ViewerCommand.TemporaryMarkupHand)
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        session.BeginTemporaryHand(allowWithoutMarkupTools?.Invoke() == true);
        return Task.CompletedTask;
    }

    public void End() => session.EndTemporaryHand();
}
