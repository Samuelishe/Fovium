using Fovium.Settings;

namespace Fovium.Tests.Settings;

public sealed class SlideshowSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Fovium.Tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    [InlineData(61, 60)]
    public void DurationNormalizesToWholeSecondRange(int value, int expected)
    {
        var settings = new SlideshowSettings { SlideDurationSeconds = value }.Normalize();

        Assert.Equal(expected, settings.SlideDurationSeconds);
    }

    [Fact]
    public async Task ConfigurationPersistsButRunningStateDoesNotExistInJson()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var settings = FoviumSettings.Default with
        {
            Slideshow = new SlideshowSettings
            {
                SlideDurationSeconds = 9,
                EndBehavior = SlideshowEndBehavior.Loop,
            },
        };

        await store.SaveAsync(settings, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);

        Assert.Equal(9, loaded.Settings.Slideshow.SlideDurationSeconds);
        Assert.Equal(SlideshowEndBehavior.Loop, loaded.Settings.Slideshow.EndBehavior);
        Assert.Contains("\"slideshow\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isRunning", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentSlideshowIndex", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nextScheduled", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OlderSchemaTwoSettingsReceiveSlideshowDefaults()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """
            {
              "schemaVersion": 2
            }
            """);

        var result = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

        Assert.Equal(SlideshowSettings.Default, result.Settings.Slideshow);
        Assert.Null(result.Diagnostic);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
