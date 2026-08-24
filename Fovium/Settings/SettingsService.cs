using System.Diagnostics;

namespace Fovium.Settings;

internal sealed class SettingsService(ISettingsStore store)
{
    private readonly object _stateSync = new();
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);
    private FoviumSettings _current = FoviumSettings.Default;
    private SettingsDiagnostic? _lastDiagnostic;

    public FoviumSettings Current
    {
        get
        {
            lock (_stateSync)
            {
                return _current;
            }
        }
    }

    public SettingsDiagnostic? LastDiagnostic
    {
        get
        {
            lock (_stateSync)
            {
                return _lastDiagnostic;
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var result = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        lock (_stateSync)
        {
            _current = result.Settings;
            _lastDiagnostic = result.Diagnostic;
        }

        TraceDiagnostic(result.Diagnostic);
    }

    public Task SetImageChangeViewPolicyAsync(
        ImageChangeViewPolicy policy,
        CancellationToken cancellationToken = default)
    {
        FoviumSettings snapshot;
        lock (_stateSync)
        {
            if (_current.ImageChangeViewPolicy == policy)
            {
                return Task.CompletedTask;
            }

            snapshot = _current with { ImageChangeViewPolicy = policy };
            _current = snapshot;
        }

        return PersistAsync(snapshot, cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _persistenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _persistenceGate.Release();
    }

    private async Task PersistAsync(FoviumSettings settings, CancellationToken cancellationToken)
    {
        await _persistenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await store.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
                lock (_stateSync)
                {
                    _lastDiagnostic = null;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                var diagnostic = new SettingsDiagnostic(
                    SettingsDiagnosticKind.WriteFailed,
                    "Settings could not be saved; the in-memory preference remains active.",
                    exception);
                lock (_stateSync)
                {
                    _lastDiagnostic = diagnostic;
                }

                TraceDiagnostic(diagnostic);
            }
        }
        finally
        {
            _persistenceGate.Release();
        }
    }

    private static void TraceDiagnostic(SettingsDiagnostic? diagnostic)
    {
        if (diagnostic is not null)
        {
            Debug.WriteLine($"Fovium settings {diagnostic.Kind}: {diagnostic.Message}");
        }
    }
}
