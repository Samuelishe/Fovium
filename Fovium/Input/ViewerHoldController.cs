namespace Fovium.Input;

internal interface IViewerHoldAction
{
    Task BeginAsync(ViewerCommand command, CancellationToken cancellationToken);

    void End();
}

internal sealed class ViewerHoldController(IViewerHoldAction action)
{
    private readonly object _sync = new();
    private readonly HashSet<string> _suppressedPrimaryKeys = new(StringComparer.OrdinalIgnoreCase);
    private ActiveHold? _active;

    public ViewerCommand? ActiveCommand
    {
        get
        {
            lock (_sync)
            {
                return _active?.Command;
            }
        }
    }

    public async Task<bool> TryBeginAsync(
        ViewerCommand command,
        string primaryKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryKey);
        if (ViewerCommands.GetDefinition(command).Trigger != ViewerCommandTrigger.Hold)
        {
            return false;
        }

        lock (_sync)
        {
            if (_suppressedPrimaryKeys.Contains(primaryKey))
            {
                return false;
            }

            if (_active is { } active)
            {
                if (!string.Equals(active.PrimaryKey, primaryKey, StringComparison.OrdinalIgnoreCase))
                {
                    _suppressedPrimaryKeys.Add(primaryKey);
                }

                return false;
            }

            _active = new ActiveHold(command, primaryKey);
        }

        try
        {
            await action.BeginAsync(command, cancellationToken);
        }
        catch
        {
            Cancel();
            throw;
        }

        return true;
    }

    public bool EndPrimaryKey(string primaryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryKey);
        lock (_sync)
        {
            if (_active is not { } active ||
                !string.Equals(active.PrimaryKey, primaryKey, StringComparison.OrdinalIgnoreCase))
            {
                _suppressedPrimaryKeys.Remove(primaryKey);
                return false;
            }

            _active = null;
            _suppressedPrimaryKeys.Remove(primaryKey);
        }

        action.End();
        return true;
    }

    public bool Cancel()
    {
        return Cancel(suppressPrimaryKeyUntilRelease: true, clearSuppressedKeys: false);
    }

    public bool CancelForFocusLoss()
    {
        return Cancel(suppressPrimaryKeyUntilRelease: false, clearSuppressedKeys: true);
    }

    private bool Cancel(bool suppressPrimaryKeyUntilRelease, bool clearSuppressedKeys)
    {
        lock (_sync)
        {
            if (clearSuppressedKeys)
            {
                _suppressedPrimaryKeys.Clear();
            }

            if (_active is null)
            {
                return false;
            }

            if (suppressPrimaryKeyUntilRelease)
            {
                _suppressedPrimaryKeys.Add(_active.PrimaryKey);
            }

            _active = null;
        }

        action.End();
        return true;
    }

    private sealed record ActiveHold(ViewerCommand Command, string PrimaryKey);
}
