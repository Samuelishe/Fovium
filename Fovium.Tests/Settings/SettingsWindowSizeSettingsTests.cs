using System.Text.Json;
using Fovium.Settings;
using Fovium.Viewer;

namespace Fovium.Tests.Settings;

public sealed class SettingsWindowSizeSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Fovium.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task JsonPersistsOnlyNormalWidthAndHeightNeverPositionOrWindowState()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var settings = FoviumSettings.Default with
        {
            SettingsWindowSize = new SettingsWindowSizeSettings
            {
                WidthDip = 934.5,
                HeightDip = 689.25,
            },
        };

        await store.SaveAsync(settings, CancellationToken.None);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var size = document.RootElement.GetProperty("settingsWindowSize");
        var names = size.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(["widthDip", "heightDip"], names);
        Assert.Equal(934.5, size.GetProperty("widthDip").GetDouble());
        Assert.Equal(689.25, size.GetProperty("heightDip").GetDouble());
        Assert.False(size.TryGetProperty("x", out _));
        Assert.False(size.TryGetProperty("y", out _));
        Assert.False(size.TryGetProperty("position", out _));
        Assert.False(size.TryGetProperty("windowState", out _));
        Assert.False(size.TryGetProperty("monitor", out _));
    }

    [Fact]
    public async Task ActivePresentationStateRemainsSessionOnlyWhileMarginAndWindowSizePersist()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var session = new PhotoPresentationViewSession();
        session.SetEnabled(true);
        var expected = FoviumSettings.Default with
        {
            PhotoPresentationView = new PhotoPresentationViewSettings { EdgeMarginPercent = 8.5 },
            SettingsWindowSize = new SettingsWindowSizeSettings
            {
                WidthDip = 940,
                HeightDip = 690,
            },
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);
        var nextSession = new PhotoPresentationViewSession();

        Assert.True(session.IsEnabled);
        Assert.False(nextSession.IsEnabled);
        Assert.Equal(8.5, loaded.Settings.PhotoPresentationView.EdgeMarginPercent);
        Assert.Equal(expected.SettingsWindowSize, loaded.Settings.SettingsWindowSize);
        Assert.DoesNotContain("photoPresentationViewEnabled", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("presentationViewEnabled", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"isEnabled\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1, 680)]
    [InlineData(920, 0)]
    [InlineData(100_000, 680)]
    [InlineData(920, 100_000)]
    public async Task SmallAndOversizedPersistedDimensionsNormalizeToDefaults(
        double width,
        double height)
    {
        await WriteAsync($$"""
            {
              "schemaVersion": 2,
              "settingsWindowSize": {
                "widthDip": {{width.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
                "heightDip": {{height.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(
            width is >= SettingsWindowSizeSettings.MinimumWidthDip and
                <= SettingsWindowSizeSettings.MaximumWidthDip
                ? width
                : SettingsWindowSizeSettings.DefaultWidthDip,
            result.Settings.SettingsWindowSize.WidthDip);
        Assert.Equal(
            height is >= SettingsWindowSizeSettings.MinimumHeightDip and
                <= SettingsWindowSizeSettings.MaximumHeightDip
                ? height
                : SettingsWindowSizeSettings.DefaultHeightDip,
            result.Settings.SettingsWindowSize.HeightDip);
        Assert.Null(result.Diagnostic);
    }

    [Theory]
    [InlineData(SettingsWindowSizeSettings.MinimumWidthDip, SettingsWindowSizeSettings.MinimumHeightDip)]
    [InlineData(SettingsWindowSizeSettings.MaximumWidthDip, SettingsWindowSizeSettings.MaximumHeightDip)]
    public async Task ExactPersistedSizeBoundariesRoundTrip(double width, double height)
    {
        await WriteAsync($$"""
            {
              "schemaVersion": 2,
              "settingsWindowSize": {
                "widthDip": {{width}},
                "heightDip": {{height}}
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(width, result.Settings.SettingsWindowSize.WidthDip);
        Assert.Equal(height, result.Settings.SettingsWindowSize.HeightDip);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task LegacySettingsWithoutWindowSizeUseCanonicalDefaults()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "photoPresentationView": { "edgeMarginPercent": 7 }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsWindowSizeSettings.Default, result.Settings.SettingsWindowSize);
        Assert.Equal(7, result.Settings.PhotoPresentationView.EdgeMarginPercent);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task MalformedWindowSizeJsonRecoversToCanonicalDefaults()
    {
        await WriteAsync("""
            {
              "schemaVersion": 2,
              "settingsWindowSize": {
                "widthDip": "not-a-number",
                "heightDip": 680
              }
            }
            """);

        var result = await CreateStore().LoadAsync(CancellationToken.None);

        Assert.Equal(SettingsWindowSizeSettings.Default, result.Settings.SettingsWindowSize);
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
}
