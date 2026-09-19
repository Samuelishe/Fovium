using Fovium.Settings;

namespace Fovium.Tests.Settings;

public sealed class RecentCapturePolicySettingsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("Fovium.RecentCapture.Tests.").FullName;

    [Fact]
    public void DefaultAndInvalidValuesNormalizeToOpenedItemsOnly()
    {
        Assert.Equal(RecentCapturePolicy.OpenedItemsOnly, HomeSettings.Default.CapturePolicy);
        Assert.Equal(
            RecentCapturePolicy.OpenedItemsOnly,
            (HomeSettings.Default with { CapturePolicy = (RecentCapturePolicy)99 }).Normalize().CapturePolicy);
    }

    [Fact]
    public async Task EveryViewedPolicyRoundTripsInSchemaTwoWithoutChangingHistoryCapacity()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var settings = FoviumSettings.Default with
        {
            Home = HomeSettings.Default with
            {
                CapturePolicy = RecentCapturePolicy.EveryViewedPhoto,
                RecentLocations =
                [
                    new RecentLocation
                    {
                        Kind = RecentLocationKind.File,
                        Path = Path.Combine(_directory, "viewed.jpg"),
                    },
                ],
            },
        };

        await store.SaveAsync(settings, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(2, loaded.Settings.SchemaVersion);
        Assert.Equal(RecentCapturePolicy.EveryViewedPhoto, loaded.Settings.Home.CapturePolicy);
        Assert.Single(loaded.Settings.Home.RecentLocations);
        Assert.Equal(20, HomeSettings.MaximumRecentLocations);
    }

    [Fact]
    public async Task OlderSchemaTwoHomeWithoutCapturePolicyUsesConservativeDefault()
    {
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """
                                           {
                                             "schemaVersion": 2,
                                             "home": {
                                               "rememberRecentPhotos": true,
                                               "recentLocations": []
                                             }
                                           }
                                           """);
        var loaded = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

        Assert.Equal(RecentCapturePolicy.OpenedItemsOnly, loaded.Settings.Home.CapturePolicy);
        Assert.Null(loaded.Diagnostic);
    }

    [Fact]
    public async Task SettingsServiceChangesPolicyWithoutTouchingExistingHistory()
    {
        var store = new RecordingStore();
        using var service = new SettingsService(store);
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = Path.Combine(_directory, "kept.jpg"),
        });

        await service.SetRecentCapturePolicyAsync(RecentCapturePolicy.EveryViewedPhoto);

        Assert.Equal(RecentCapturePolicy.EveryViewedPhoto, service.Current.Home.CapturePolicy);
        Assert.Single(service.Current.Home.RecentLocations);
        Assert.Equal(RecentCapturePolicy.EveryViewedPhoto, store.Saved!.Home.CapturePolicy);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class RecordingStore : ISettingsStore
    {
        public FoviumSettings? Saved { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }
}