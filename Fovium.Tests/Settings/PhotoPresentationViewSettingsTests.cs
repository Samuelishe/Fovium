using Fovium.Settings;

namespace Fovium.Tests.Settings;

public sealed class PhotoPresentationViewSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Fovium.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void ModeDefaultsOffAndMarginUsesCanonicalDefault()
    {
        var viewport = new Fovium.Viewer.PhotoViewportControl();

        Assert.False(viewport.PhotoPresentationViewEnabled);
        Assert.Equal(
            PhotoPresentationViewSettings.DefaultEdgeMarginPercent,
            FoviumSettings.Default.PhotoPresentationView.EdgeMarginPercent);
    }

    [Theory]
    [InlineData(-1, PhotoPresentationViewSettings.MinimumEdgeMarginPercent)]
    [InlineData(0, 0)]
    [InlineData(7.5, 7.5)]
    [InlineData(15, 15)]
    [InlineData(16, PhotoPresentationViewSettings.MaximumEdgeMarginPercent)]
    [InlineData(double.NaN, PhotoPresentationViewSettings.DefaultEdgeMarginPercent)]
    [InlineData(double.PositiveInfinity, PhotoPresentationViewSettings.DefaultEdgeMarginPercent)]
    [InlineData(double.NegativeInfinity, PhotoPresentationViewSettings.DefaultEdgeMarginPercent)]
    public void MarginNormalizationUsesDocumentedRange(double value, double expected)
    {
        var normalized = new PhotoPresentationViewSettings { EdgeMarginPercent = value }.Normalize();

        Assert.Equal(expected, normalized.EdgeMarginPercent);
    }

    [Fact]
    public async Task MarginPersistsButSessionEnabledStateDoesNot()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var expected = FoviumSettings.Default with
        {
            PhotoPresentationView = new PhotoPresentationViewSettings { EdgeMarginPercent = 9.25 },
        };
        var viewport = new Fovium.Viewer.PhotoViewportControl();
        viewport.SetPhotoPresentationViewEnabled(true);

        await store.SaveAsync(expected, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);
        var nextSession = new Fovium.Viewer.PhotoViewportControl();

        Assert.Equal(9.25, loaded.Settings.PhotoPresentationView.EdgeMarginPercent);
        Assert.Contains("\"photoPresentationView\"", json, StringComparison.Ordinal);
        Assert.Contains("\"edgeMarginPercent\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("photoPresentationViewEnabled", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("presentationViewEnabled", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewport.PhotoPresentationViewEnabled);
        Assert.False(nextSession.PhotoPresentationViewEnabled);
    }

    [Fact]
    public async Task OlderSettingsWithoutPresentationMarginLoadTheCanonicalDefault()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """
            {
              "schemaVersion": 2,
              "monitorColorManagementEnabled": true
            }
            """);

        var result = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

        Assert.Equal(
            PhotoPresentationViewSettings.Default,
            result.Settings.PhotoPresentationView);
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
