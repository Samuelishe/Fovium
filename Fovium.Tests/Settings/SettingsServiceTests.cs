using Fovium.Settings;

namespace Fovium.Tests.Settings;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task PreferenceChangeTakesEffectAndPersists()
    {
        var store = new RecordingSettingsStore();
        var service = new SettingsService(store);

        await service.SetImageChangeViewPolicyAsync(
            ImageChangeViewPolicy.FitEachImage,
            CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, service.Current.ImageChangeViewPolicy);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, store.Saved?.ImageChangeViewPolicy);
    }

    [Fact]
    public async Task SaveFailureKeepsInMemoryPreferenceAndDiagnostic()
    {
        var service = new SettingsService(new RecordingSettingsStore(failSave: true));

        await service.SetImageChangeViewPolicyAsync(
            ImageChangeViewPolicy.FitEachImage,
            CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, service.Current.ImageChangeViewPolicy);
        Assert.Equal(SettingsDiagnosticKind.WriteFailed, service.LastDiagnostic?.Kind);
    }

    [Fact]
    public async Task FlushWaitsForPendingAutosave()
    {
        var store = new DelayedSettingsStore();
        var service = new SettingsService(store);
        var change = service.SetImageChangeViewPolicyAsync(ImageChangeViewPolicy.FitEachImage);

        var flush = service.FlushAsync();

        Assert.False(flush.IsCompleted);
        store.Complete();
        await Task.WhenAll(change, flush);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, store.Saved?.ImageChangeViewPolicy);
    }

    private sealed class RecordingSettingsStore(bool failSave = false) : ISettingsStore
    {
        public FoviumSettings? Saved { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
        {
            if (failSave)
            {
                throw new IOException("Synthetic settings failure.");
            }

            Saved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedSettingsStore : ISettingsStore
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public FoviumSettings? Saved { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public async Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
        {
            await _completion.Task.WaitAsync(cancellationToken);
            Saved = settings;
        }

        public void Complete() => _completion.TrySetResult();
    }
}
