using Fovium.Input;
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
    public void DefaultsUseSchemaVersionTwoAndCanonicalValues()
    {
        Assert.Equal(2, FoviumSettings.Default.SchemaVersion);
        Assert.Equal(ImageChangeViewPolicy.KeepCurrentScale, FoviumSettings.Default.ImageChangeViewPolicy);
        Assert.Equal(StageBackgroundMode.Black, FoviumSettings.Default.Stage.BackgroundMode);
        Assert.False(FoviumSettings.Default.Stage.MatteEnabled);
        Assert.Equal(StageDefaults.CustomBackgroundColor, FoviumSettings.Default.Stage.CustomBackgroundColor);
        Assert.Equal(StageDefaults.MatteColor, FoviumSettings.Default.Stage.MatteColor);
        Assert.Equal(MatteStyle.Solid, FoviumSettings.Default.Stage.MatteStyle);
        Assert.Equal(24, FoviumSettings.Default.Stage.MatteWidthPhysicalPixels);
        Assert.Equal(StageDefaults.AmbientBrightness, FoviumSettings.Default.Stage.AmbientBrightness);
        Assert.Equal(StageDefaults.AmbientSaturation, FoviumSettings.Default.Stage.AmbientSaturation);
        Assert.Equal(StageDefaults.AmbientBlurSigmaPixels, FoviumSettings.Default.Stage.AmbientBlur);
    }

    [Fact]
    public async Task MissingFileUsesDefaults()
    {
        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(FoviumSettings.Default, result.Settings);
        Assert.Null(result.Diagnostic);
        Assert.False(result.RequiresSave);
    }

    [Fact]
    public Task V1BlackMigratesToBlackWithoutMatte() =>
        AssertV1MigrationAsync("Black", StageBackgroundMode.Black, expectedMatte: false);

    [Fact]
    public Task V1NeutralMigratesToNeutralWithoutMatte() =>
        AssertV1MigrationAsync("Neutral", StageBackgroundMode.Neutral, expectedMatte: false);

    [Fact]
    public Task V1AmbientMigratesToAmbientWithoutMatte() =>
        AssertV1MigrationAsync("Ambient", StageBackgroundMode.Ambient, expectedMatte: false);

    [Fact]
    public Task V1AmbientMatteMigratesToAmbientWithMatte() =>
        AssertV1MigrationAsync("AmbientMatte", StageBackgroundMode.Ambient, expectedMatte: true);

    [Fact]
    public async Task V1MigrationPreservesImageChangeViewPolicy()
    {
        await WriteAsync(
            """
            { "schemaVersion": 1, "imageChangeViewPolicy": "KeepCurrentScale", "stageMode": "AmbientMatte" }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.KeepCurrentScale, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(StageBackgroundMode.Ambient, result.Settings.Stage.BackgroundMode);
        Assert.True(result.Settings.Stage.MatteEnabled);
    }

    [Fact]
    public async Task V2RoundTripPreservesStageCustomizationAndShortcuts()
    {
        var store = CreateStore();
        var expected = FoviumSettings.Default with
        {
            ImageChangeViewPolicy = ImageChangeViewPolicy.FitEachImage,
            Stage = StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Custom,
                MatteEnabled = true,
                CustomBackgroundColor = new StageColor(0x12, 0x34, 0x56),
                MatteColor = new StageColor(0x65, 0x43, 0x21),
                MatteStyle = MatteStyle.Angular,
                MatteWidthPhysicalPixels = 96,
                AmbientBrightness = 0.72,
                AmbientSaturation = 1.1,
                AmbientBlur = 24,
            },
            Shortcuts = ShortcutSettings.Default.WithBinding(
                ViewerCommand.ToggleMatte,
                new ShortcutGesture("K", ShortcutModifiers.Control)),
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var serialized = await File.ReadAllTextAsync(Path.Combine(_directory, "settings.json"));
        var result = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected.SchemaVersion, result.Settings.SchemaVersion);
        Assert.Equal(expected.ImageChangeViewPolicy, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(expected.Stage, result.Settings.Stage);
        Assert.Equal("#123456", result.Settings.Stage.CustomBackgroundColor.ToHex());
        Assert.Equal("#654321", result.Settings.Stage.MatteColor.ToHex());
        Assert.Equal(
            new ShortcutGesture("K", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.DoesNotContain("isValid", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isReserved", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task ExistingSchemaV2WithoutMatteGeometryUsesSolidAnd24Pixels()
    {
        await WriteAsync(
            """
            {
              "schemaVersion": 2,
              "imageChangeViewPolicy": "FitEachImage",
              "stage": {
                "backgroundMode": "Custom",
                "matteEnabled": true,
                "customBackgroundColor": "#123456",
                "matteColor": "#654321",
                "ambientBrightness": 0.72,
                "ambientSaturation": 1.1,
                "ambientBlur": 24
              },
              "shortcuts": {
                "bindings": {
                  "viewer.toggleMatte": { "key": "K", "modifiers": "Control" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(2, result.Settings.SchemaVersion);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(StageBackgroundMode.Custom, result.Settings.Stage.BackgroundMode);
        Assert.True(result.Settings.Stage.MatteEnabled);
        Assert.Equal(new StageColor(0x12, 0x34, 0x56), result.Settings.Stage.CustomBackgroundColor);
        Assert.Equal(new StageColor(0x65, 0x43, 0x21), result.Settings.Stage.MatteColor);
        Assert.Equal(MatteStyle.Solid, result.Settings.Stage.MatteStyle);
        Assert.Equal(24, result.Settings.Stage.MatteWidthPhysicalPixels);
        Assert.Equal(new ShortcutGesture("K", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Null(result.Diagnostic);
        Assert.False(result.RequiresSave);
    }

    [Fact]
    public async Task InvalidMatteGeometryValuesNormalizeSafelyWithoutChangingOtherSettings()
    {
        await WriteAsync(
            """
            {
              "schemaVersion": 2,
              "imageChangeViewPolicy": "FitEachImage",
              "stage": {
                "backgroundMode": "Ambient",
                "matteEnabled": true,
                "matteStyle": 999,
                "matteWidthPhysicalPixels": 999
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(StageBackgroundMode.Ambient, result.Settings.Stage.BackgroundMode);
        Assert.True(result.Settings.Stage.MatteEnabled);
        Assert.Equal(MatteStyle.Solid, result.Settings.Stage.MatteStyle);
        Assert.Equal(192, result.Settings.Stage.MatteWidthPhysicalPixels);
    }

    [Fact]
    public async Task UnknownFutureSettingsAndShortcutEntriesAreTolerated()
    {
        await WriteAsync(
            """
            {
              "schemaVersion": 2,
              "imageChangeViewPolicy": "FitEachImage",
              "stage": { "backgroundMode": "Ambient", "futureStageValue": 42 },
              "shortcuts": {
                "bindings": {
                  "viewer.previous": { "key": "Left", "modifiers": "None" },
                  "viewer.future": { "key": "F12", "modifiers": "None" }
                },
                "futureShortcutValue": true
              },
              "futureRootValue": "ignored"
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(StageBackgroundMode.Ambient, result.Settings.Stage.BackgroundMode);
        Assert.Equal(new ShortcutGesture("Left"), result.Settings.Shortcuts.Get(ViewerCommand.PreviousImage));
        Assert.DoesNotContain("viewer.future", result.Settings.Shortcuts.Bindings.Keys);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task NullNestedObjectsAndUnknownEnumsNormalizeToSafeDefaults()
    {
        await WriteAsync(
            """
            {
              "schemaVersion": 2,
              "imageChangeViewPolicy": 999,
              "stage": null,
              "shortcuts": null
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(ImageChangeViewPolicy.KeepCurrentScale, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(StageSettings.Default, result.Settings.Stage);
        Assert.Equal(
            ShortcutSettings.Default.Get(ViewerCommand.ToggleMatte),
            result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task NullShortcutBindingMapRecoversToCanonicalDefaults()
    {
        await WriteAsync(
            """
            {
              "schemaVersion": 2,
              "shortcuts": { "bindings": null }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(
            new ShortcutGesture("Left"),
            result.Settings.Shortcuts.Get(ViewerCommand.PreviousImage));
        Assert.Equal(
            new ShortcutGesture("M"),
            result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task MalformedJsonRecoversToDefaults()
    {
        await WriteAsync("{not-json");

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(FoviumSettings.Default, result.Settings);
        Assert.Equal(SettingsDiagnosticKind.Malformed, result.Diagnostic?.Kind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private Task WriteAsync(string contents)
    {
        Directory.CreateDirectory(_directory);
        return File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), contents);
    }

    private JsonSettingsStore CreateStore() =>
        new(Path.Combine(_directory, "settings.json"));

    private async Task AssertV1MigrationAsync(
        string legacyMode,
        StageBackgroundMode expectedBackground,
        bool expectedMatte)
    {
        await WriteAsync($$"""
            { "schemaVersion": 1, "imageChangeViewPolicy": "FitEachImage", "stageMode": "{{legacyMode}}" }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(2, result.Settings.SchemaVersion);
        Assert.Equal(expectedBackground, result.Settings.Stage.BackgroundMode);
        Assert.Equal(expectedMatte, result.Settings.Stage.MatteEnabled);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, result.Settings.ImageChangeViewPolicy);
        Assert.True(result.RequiresSave);
        Assert.Null(result.Diagnostic);
    }
}
