using Fovium.Localization;
using Fovium.Settings;
using Fovium.Stage;

namespace Fovium.Tests.Settings;

public sealed class LanguageSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Fovium.LanguageSettings.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExplicitLanguageRoundTripsThroughSchemaTwoJson()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var expected = FoviumSettings.Default with { Language = UiLanguage.Russian };

        await store.SaveAsync(expected, CancellationToken.None);
        var result = await store.LoadAsync(CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);

        Assert.Equal(UiLanguage.Russian, result.Settings.Language);
        Assert.Contains("\"language\": \"Russian\"", json, StringComparison.Ordinal);
        Assert.Equal(StageSettings.Default, result.Settings.Stage);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task ExistingSchemaTwoDocumentWithoutLanguageUsesSystemDefault()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """
                                           {
                                             "schemaVersion": 2,
                                             "imageChangeViewPolicy": "FitEachImage"
                                           }
                                           """);

        var result = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

        Assert.Equal(UiLanguage.SystemDefault, result.Settings.Language);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, result.Settings.ImageChangeViewPolicy);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public void InvalidLanguageNormalizesToSystemDefaultWithoutChangingOtherPreferences()
    {
        var settings = FoviumSettings.Default with
        {
            Language = (UiLanguage)999,
            ImageChangeViewPolicy = ImageChangeViewPolicy.FitEachImage,
            MonitorColorManagementEnabled = false,
        };

        var normalized = settings.Normalize();

        Assert.Equal(UiLanguage.SystemDefault, normalized.Language);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, normalized.ImageChangeViewPolicy);
        Assert.False(normalized.MonitorColorManagementEnabled);
    }

    [Fact]
    public async Task LanguageChangeAutosavesWithoutChangingUnrelatedSettings()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);

        await service.SetLanguageAsync(UiLanguage.English);

        Assert.Equal(UiLanguage.English, service.Current.Language);
        Assert.Equal(UiLanguage.English, store.Saved?.Language);
        Assert.Equal(StageSettings.Default, service.Current.Stage);
        Assert.True(service.Current.MonitorColorManagementEnabled);
        Assert.Equal(1, store.SaveCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class RecordingSettingsStore : ISettingsStore
    {
        public FoviumSettings? Saved { get; private set; }

        public int SaveCount { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}