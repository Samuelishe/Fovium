using System.Diagnostics;
using Fovium.Input;
using Fovium.Presentation;
using Fovium.Stage;

namespace Fovium.Settings;

internal sealed class SettingsChangedEventArgs(FoviumSettings settings) : EventArgs
{
    public FoviumSettings Settings { get; } = settings;
}

internal sealed class SettingsService(ISettingsStore store) : IDisposable
{
    private static readonly TimeSpan PersistenceDebounce = TimeSpan.FromMilliseconds(150);
    private readonly object _stateSync = new();
    private readonly object _persistenceSync = new();
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);
    private FoviumSettings _current = FoviumSettings.Default;
    private SettingsDiagnostic? _lastDiagnostic;
    private CancellationTokenSource? _pendingPersistenceCancellation;
    private Task _pendingPersistence = Task.CompletedTask;
    private bool _disposed;

    public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

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
            _current = result.Settings.Normalize();
            _lastDiagnostic = result.Diagnostic;
        }

        TraceDiagnostic(result.Diagnostic);
        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(Current));
        if (result.RequiresSave)
        {
            await SchedulePersistence(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task SetImageChangeViewPolicyAsync(
        ImageChangeViewPolicy policy,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            settings => settings.ImageChangeViewPolicy == policy
                ? settings
                : settings with { ImageChangeViewPolicy = policy },
            cancellationToken);

    public Task SetMonitorColorManagementEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            settings => settings.MonitorColorManagementEnabled == enabled
                ? settings
                : settings with { MonitorColorManagementEnabled = enabled },
            cancellationToken);

    public Task SetPhotoPresentationViewAsync(
        PhotoPresentationViewSettings photoPresentationView,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photoPresentationView);
        var normalized = photoPresentationView.Normalize();
        return UpdateAsync(
            settings => settings.PhotoPresentationView == normalized
                ? settings
                : settings with { PhotoPresentationView = normalized },
            cancellationToken);
    }

    public Task SetSlideshowAsync(
        SlideshowSettings slideshow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slideshow);
        var normalized = slideshow.Normalize();
        return UpdateAsync(
            settings => settings.Slideshow == normalized
                ? settings
                : settings with { Slideshow = normalized },
            cancellationToken);
    }

    public Task SetSettingsWindowSizeAsync(
        SettingsWindowSizeSettings size,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(size);
        var normalized = size.Normalize();
        return UpdateAsync(
            settings => settings.SettingsWindowSize == normalized
                ? settings
                : settings with { SettingsWindowSize = normalized },
            cancellationToken);
    }

    public Task SetStageAsync(
        StageSettings stage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var normalized = stage.Normalize();
        return UpdateAsync(
            settings => settings.Stage == normalized
                ? settings
                : settings with { Stage = normalized },
            cancellationToken);
    }

    public Task ToggleMatteAsync(CancellationToken cancellationToken = default) =>
        UpdateAsync(
            settings => settings with
            {
                Stage = settings.Stage with { MatteEnabled = !settings.Stage.MatteEnabled },
            },
            cancellationToken);

    public Task SetShortcutsAsync(
        ShortcutSettings shortcuts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shortcuts);
        var normalized = shortcuts.Normalize();
        return UpdateAsync(
            settings => SettingsEqual(settings.Shortcuts, normalized)
                ? settings
                : settings with { Shortcuts = normalized },
            cancellationToken);
    }

    public Task SetPresentationAsync(
        PresentationSettings presentation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var normalized = presentation.Normalize();
        return UpdateAsync(
            settings => settings.Presentation == normalized
                ? settings
                : settings with { Presentation = normalized },
            cancellationToken);
    }

    public Task ResetShortcutsAsync(CancellationToken cancellationToken = default) =>
        SetShortcutsAsync(ShortcutSettings.Default, cancellationToken);

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task pending;
            lock (_persistenceSync)
            {
                pending = _pendingPersistence;
            }

            await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
            lock (_persistenceSync)
            {
                if (ReferenceEquals(pending, _pendingPersistence))
                {
                    return;
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_persistenceSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingPersistenceCancellation?.Cancel();
            _pendingPersistenceCancellation?.Dispose();
            _pendingPersistenceCancellation = null;
        }

        _persistenceGate.Dispose();
    }

    private Task UpdateAsync(
        Func<FoviumSettings, FoviumSettings> update,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FoviumSettings snapshot;
        lock (_stateSync)
        {
            var updated = update(_current).Normalize();
            if (AreEqual(updated, _current))
            {
                return Task.CompletedTask;
            }

            _current = updated;
            snapshot = updated;
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(snapshot));
        return SchedulePersistence(cancellationToken);
    }

    private Task SchedulePersistence(CancellationToken cancellationToken)
    {
        lock (_persistenceSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pendingPersistenceCancellation?.Cancel();
            _pendingPersistenceCancellation?.Dispose();
            _pendingPersistenceCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pendingPersistence = PersistAfterDelayAsync(_pendingPersistenceCancellation.Token);
            return _pendingPersistence;
        }
    }

    private async Task PersistAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PersistenceDebounce, cancellationToken).ConfigureAwait(false);
            await _persistenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await store.SaveAsync(Current, cancellationToken).ConfigureAwait(false);
                lock (_stateSync)
                {
                    _lastDiagnostic = null;
                }
            }
            finally
            {
                _persistenceGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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

    private static bool AreEqual(FoviumSettings left, FoviumSettings right) =>
        left.SchemaVersion == right.SchemaVersion &&
        left.ImageChangeViewPolicy == right.ImageChangeViewPolicy &&
        left.MonitorColorManagementEnabled == right.MonitorColorManagementEnabled &&
        left.PhotoPresentationView == right.PhotoPresentationView &&
        left.Slideshow == right.Slideshow &&
        left.SettingsWindowSize == right.SettingsWindowSize &&
        left.Stage == right.Stage &&
        left.Presentation == right.Presentation &&
        SettingsEqual(left.Shortcuts, right.Shortcuts);

    private static bool SettingsEqual(ShortcutSettings left, ShortcutSettings right) =>
        ViewerCommands.Definitions.All(
            definition => left.Get(definition.Command) == right.Get(definition.Command));

    private static void TraceDiagnostic(SettingsDiagnostic? diagnostic)
    {
        if (diagnostic is not null)
        {
            Debug.WriteLine($"Fovium settings {diagnostic.Kind}: {diagnostic.Message}");
        }
    }
}
