using Fovium.Settings;
using Fovium.Stage;

namespace Fovium.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Fovium.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void DefaultsUseSchemaVersionOneKeepCurrentScaleAndBlackStage()
    {
        Assert.Equal(1, FoviumSettings.Default.SchemaVersion);
        Assert.Equal(ImageChangeViewPolicy.KeepCurrentScale, FoviumSettings.Default.ImageChangeViewPolicy);
        Assert.Equal(StageMode.Black, FoviumSettings.Default.StageMode);
    }

    [Fact]
    public async Task MissingFileUsesDefaults()
    {
        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(FoviumSettings.Default, result.Settings);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task SaveLoadRoundTripPersistsPreference()
    {
        var store = CreateStore();
        var expected = FoviumSettings.Default with
        {
            ImageChangeViewPolicy = ImageChangeViewPolicy.FitEachImage,
            StageMode = StageMode.AmbientMatte,
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var result = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, result.Settings);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task ExistingR2SettingsWithoutStageLoadAsBlack()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """
            { "schemaVersion": 1, "imageChangeViewPolicy": "FitEachImage" }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(StageMode.Black, result.Settings.StageMode);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task MalformedJsonRecoversToDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "{not-json");

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(FoviumSettings.Default, result.Settings);
        Assert.Equal(SettingsDiagnosticKind.Malformed, result.Diagnostic?.Kind);
    }

    [Fact]
    public async Task UnknownPropertiesAreTolerated()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """
            { "schemaVersion": 1, "imageChangeViewPolicy": "FitEachImage", "stageMode": "Ambient", "futureValue": 42 }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(StageMode.Ambient, result.Settings.StageMode);
        Assert.Null(result.Diagnostic);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private JsonSettingsStore CreateStore() =>
        new(Path.Combine(_directory, "settings.json"));
}
