using Fovium.Input;
using Fovium.Presentation;
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
        Assert.Equal(PresentationSettings.Default, FoviumSettings.Default.Presentation);
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
            Presentation = PresentationSettings.Default with
            {
                MarkupToolsEnabled = false,
                HighlightColor = new PresentationColor(0x01, 0x23, 0x45),
                HighlightOpacity = 0.55,
                HighlightRadiusPhysicalPixels = 88,
                DefaultMarkupColor = new PresentationColor(0xAB, 0xCD, 0xEF),
                DefaultMarkupStrokePhysicalPixels = 9,
                DefaultMarkupOpacity = 0.65,
                MarkupDockPlacement = new FloatingOverlayPlacement(0.22, 0.84),
                PhotoInfoPlacement = new FloatingOverlayPlacement(0.17, 0.68),
            },
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var serialized = await File.ReadAllTextAsync(Path.Combine(_directory, "settings.json"));
        var result = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected.SchemaVersion, result.Settings.SchemaVersion);
        Assert.Equal(expected.ImageChangeViewPolicy, result.Settings.ImageChangeViewPolicy);
        Assert.Equal(expected.Stage, result.Settings.Stage);
        Assert.Equal(expected.Presentation, result.Settings.Presentation);
        Assert.Contains("#012345", serialized, StringComparison.Ordinal);
        Assert.Contains("#ABCDEF", serialized, StringComparison.Ordinal);
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
    public async Task ExistingV2SettingsReceivePeekAndBlinkDefaultsWithoutSchemaBump()
    {
        await WriteAsync(
            """
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.fit": { "key": "0", "modifiers": "None" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(2, result.Settings.SchemaVersion);
        Assert.Equal(new ShortcutGesture("Z"), result.Settings.Shortcuts.Get(ViewerCommand.Peek100));
        Assert.Equal(
            new ShortcutGesture("C", ShortcutModifiers.Shift),
            result.Settings.Shortcuts.Get(ViewerCommand.BlinkCompare));
        Assert.False(result.RequiresSave);
    }

    [Fact]
    public async Task ExistingV2CustomZAndCBindingsWinOverNewHoldDefaults()
    {
        await WriteAsync(
            """
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.fit": { "key": "Z", "modifiers": "None" },
                  "viewer.toggleMatte": { "key": "C", "modifiers": "None" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(new ShortcutGesture("Z"), result.Settings.Shortcuts.Get(ViewerCommand.Fit));
        Assert.Equal(new ShortcutGesture("C"), result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Null(result.Settings.Shortcuts.Get(ViewerCommand.Peek100));
        Assert.Equal(
            new ShortcutGesture("C", ShortcutModifiers.Shift),
            result.Settings.Shortcuts.Get(ViewerCommand.BlinkCompare));
        Assert.Equal(
            new ShortcutGesture("C"),
            result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
    }

    [Fact]
    public async Task ExistingV2ReceivesPresentationDefaultsAndFreeHighlightMarkupShortcuts()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": { "bindings": { "viewer.fit": { "key": "0", "modifiers": "None" } } }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(2, result.Settings.SchemaVersion);
        Assert.Equal(PresentationSettings.Default, result.Settings.Presentation);
        Assert.Equal(new ShortcutGesture("H"), result.Settings.Shortcuts.Get(ViewerCommand.ToggleHighlight));
        Assert.Equal(new ShortcutGesture("P"), result.Settings.Shortcuts.Get(ViewerCommand.ToggleMarkupTools));
    }

    [Fact]
    public async Task ExistingV2CustomHAndPBindingsWinOverNewPresentationDefaults()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.fit": { "key": "H", "modifiers": "None" },
                  "viewer.toggleMatte": { "key": "P", "modifiers": "None" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(new ShortcutGesture("H"), result.Settings.Shortcuts.Get(ViewerCommand.Fit));
        Assert.Equal(new ShortcutGesture("P"), result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Null(result.Settings.Shortcuts.Get(ViewerCommand.ToggleHighlight));
        Assert.Null(result.Settings.Shortcuts.Get(ViewerCommand.ToggleMarkupTools));
    }

    [Fact]
    public async Task ExistingV2ReceivesFreeMarkupHistoryDefaultsWithoutSchemaBump()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": { "bindings": { "viewer.fit": { "key": "0", "modifiers": "None" } } }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(2, result.Settings.SchemaVersion);
        Assert.Equal(
            new ShortcutGesture("Z", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.MarkupUndo));
        Assert.Equal(
            new ShortcutGesture("Y", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.MarkupRedo));
        Assert.Equal(
            new ShortcutGesture("C"),
            result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
    }

    [Fact]
    public async Task ExistingV2CustomizedHistoryGesturesAreNeverStolen()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.fit": { "key": "Z", "modifiers": "Control" },
                  "viewer.toggleMatte": { "key": "Y", "modifiers": "Control" },
                  "viewer.toggleHighlight": { "key": "Delete", "modifiers": "Control" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(
            new ShortcutGesture("Z", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.Fit));
        Assert.Equal(
            new ShortcutGesture("Y", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Equal(
            new ShortcutGesture("Delete", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.ToggleHighlight));
        Assert.Null(result.Settings.Shortcuts.Get(ViewerCommand.MarkupUndo));
        Assert.Null(result.Settings.Shortcuts.Get(ViewerCommand.MarkupRedo));
        Assert.Equal(new ShortcutGesture("C"), result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
    }

    [Fact]
    public async Task ExistingV2WithoutMarkupOpacityDefaultsToOpaqueWithoutLosingPresentation()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "presentation": {
                "markupToolsEnabled": false,
                "highlightColor": "#010203",
                "highlightOpacity": 0.4,
                "highlightRadiusPhysicalPixels": 70,
                "defaultMarkupColor": "#AABBCC",
                "defaultMarkupStrokePhysicalPixels": 11
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.False(result.Settings.Presentation.MarkupToolsEnabled);
        Assert.Equal(new PresentationColor(1, 2, 3), result.Settings.Presentation.HighlightColor);
        Assert.Equal(new PresentationColor(0xAA, 0xBB, 0xCC), result.Settings.Presentation.DefaultMarkupColor);
        Assert.Equal(11, result.Settings.Presentation.DefaultMarkupStrokePhysicalPixels);
        Assert.Equal(1, result.Settings.Presentation.DefaultMarkupOpacity);
        Assert.Equal(
            FloatingOverlayPlacement.Default,
            result.Settings.Presentation.MarkupDockPlacement);
        Assert.Equal(
            FloatingOverlayPlacement.BottomLeft,
            result.Settings.Presentation.PhotoInfoPlacement);
        Assert.False(result.RequiresSave);
    }

    [Fact]
    public async Task PreviousDefaultBlinkAndClearPairMigrates()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.blinkCompare": { "key": "C", "modifiers": "None" },
                  "viewer.clearMarkup": { "key": "Delete", "modifiers": "Control" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(
            new ShortcutGesture("C", ShortcutModifiers.Shift),
            result.Settings.Shortcuts.Get(ViewerCommand.BlinkCompare));
        Assert.Equal(new ShortcutGesture("C"), result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
        Assert.True(result.RequiresSave);
    }

    [Fact]
    public async Task EvolvedBlinkAndClearDefaultsAreIdempotent()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.blinkCompare": { "key": "C", "modifiers": "Shift" },
                  "viewer.clearMarkup": { "key": "C", "modifiers": "None" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(
            new ShortcutGesture("C", ShortcutModifiers.Shift),
            result.Settings.Shortcuts.Get(ViewerCommand.BlinkCompare));
        Assert.Equal(new ShortcutGesture("C"), result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
        Assert.False(result.RequiresSave);
    }

    [Fact]
    public async Task CustomizedBlinkIsPreservedWithoutEvolvingClear()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.blinkCompare": { "key": "B", "modifiers": "None" },
                  "viewer.clearMarkup": { "key": "Delete", "modifiers": "Control" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(new ShortcutGesture("B"), result.Settings.Shortcuts.Get(ViewerCommand.BlinkCompare));
        Assert.Equal(
            new ShortcutGesture("Delete", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
        Assert.False(result.RequiresSave);
    }

    [Fact]
    public async Task CustomizedClearIsPreservedWithoutEvolvingBlink()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.blinkCompare": { "key": "C", "modifiers": "None" },
                  "viewer.clearMarkup": { "key": "X", "modifiers": "None" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(new ShortcutGesture("C"), result.Settings.Shortcuts.Get(ViewerCommand.BlinkCompare));
        Assert.Equal(new ShortcutGesture("X"), result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
        Assert.False(result.RequiresSave);
    }

    [Fact]
    public async Task NewBracketDefaultsAreAssignedWhenFree()
    {
        await WriteAsync("""{ "schemaVersion": 2, "shortcuts": { "bindings": {} } }""");

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(
            new ShortcutGesture("OpenBracket"),
            result.Settings.Shortcuts.Get(ViewerCommand.DecreaseMarkupThickness));
        Assert.Equal(
            new ShortcutGesture("CloseBracket"),
            result.Settings.Shortcuts.Get(ViewerCommand.IncreaseMarkupThickness));
        Assert.Equal(
            new ShortcutGesture("OpenBracket", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.DecreaseMarkupOpacity));
        Assert.Equal(
            new ShortcutGesture("CloseBracket", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.IncreaseMarkupOpacity));
    }

    [Fact]
    public async Task GlobalBracketBindingsCoexistWithContextualDefaults()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.fit": { "key": "OpenBracket", "modifiers": "None" },
                  "viewer.toggleMatte": { "key": "CloseBracket", "modifiers": "None" },
                  "viewer.toggleHighlight": { "key": "OpenBracket", "modifiers": "Control" },
                  "viewer.toggleMarkupTools": { "key": "CloseBracket", "modifiers": "Control" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(new ShortcutGesture("OpenBracket"), result.Settings.Shortcuts.Get(ViewerCommand.Fit));
        Assert.Equal(new ShortcutGesture("CloseBracket"), result.Settings.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Equal(
            new ShortcutGesture("OpenBracket"),
            result.Settings.Shortcuts.Get(ViewerCommand.DecreaseMarkupThickness));
        Assert.Equal(
            new ShortcutGesture("CloseBracket"),
            result.Settings.Shortcuts.Get(ViewerCommand.IncreaseMarkupThickness));
        Assert.Equal(
            new ShortcutGesture("OpenBracket", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.DecreaseMarkupOpacity));
        Assert.Equal(
            new ShortcutGesture("CloseBracket", ShortcutModifiers.Control),
            result.Settings.Shortcuts.Get(ViewerCommand.IncreaseMarkupOpacity));
        Assert.Equal(
            new ShortcutGesture("OpenBracket"),
            result.Settings.Shortcuts.Get(ViewerCommand.DecreaseHighlightRadius));
    }

    [Fact]
    public async Task ExistingSameScopeCustomizationWinsOverNewToolDefault()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "shortcuts": {
                "bindings": {
                  "viewer.clearMarkup": { "key": "B", "modifiers": "None" }
                }
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(new ShortcutGesture("B"), result.Settings.Shortcuts.Get(ViewerCommand.ClearMarkup));
        Assert.Null(result.Settings.Shortcuts.Get(ViewerCommand.SelectBrushTool));
        Assert.Equal(new ShortcutGesture("V"), result.Settings.Shortcuts.Get(ViewerCommand.SelectHandTool));
        Assert.Equal(new ShortcutGesture("Space"), result.Settings.Shortcuts.Get(ViewerCommand.TemporaryMarkupHand));
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
