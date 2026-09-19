using Fovium.Settings;

namespace Fovium.Tests.Settings;

public sealed class HomeSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Fovium.HomeSettings.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RecentLocationsAreMostRecentFirstDeduplicatedAndBounded()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var paths = Enumerable.Range(1, HomeSettings.MaximumRecentLocations + 2)
            .Select(index => Path.Combine(_directory, $"photo{index}.jpg"))
            .ToArray();

        foreach (var path in paths)
        {
            await service.AddRecentLocationAsync(new RecentLocation
            {
                Kind = RecentLocationKind.File,
                Path = path,
            });
        }

        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = paths[^2],
        });
        await service.FlushAsync();

        Assert.Equal(HomeSettings.MaximumRecentLocations, service.Current.Home.RecentLocations.Count);
        Assert.Equal(Path.GetFullPath(paths[^2]), service.Current.Home.RecentLocations[0].Path);
        Assert.Equal(1,
            service.Current.Home.RecentLocations.Count(item =>
                string.Equals(item.Path, Path.GetFullPath(paths[^2]), StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(service.Current.Home.RecentLocations,
            item => item.Path == Path.GetFullPath(paths[0]));
        Assert.Equal(service.Current.Home.RecentLocations, store.Saved?.Home.RecentLocations);
    }

    [Fact]
    public async Task ShowRecentPreferenceHidesPresentationWithoutClearingHistory()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var path = Path.Combine(_directory, "photo.jpg");
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = path,
        });

        await service.SetShowRecentItemsAsync(false);
        await service.FlushAsync();

        Assert.False(service.Current.Home.ShowRecentItems);
        Assert.Equal(Path.GetFullPath(path), Assert.Single(service.Current.Home.RecentLocations).Path);
        Assert.False(store.Saved?.Home.ShowRecentItems);
    }

    [Fact]
    public async Task ClearRecentRemovesHistoryAndPreservesVisibilityPreference()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        await service.SetShowRecentItemsAsync(false);
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.Folder,
            Path = _directory,
        });

        await service.ClearRecentLocationsAsync();
        await service.FlushAsync();

        Assert.Empty(service.Current.Home.RecentLocations);
        Assert.False(service.Current.Home.ShowRecentItems);
        Assert.Empty(store.Saved?.Home.RecentLocations ?? []);
    }

    [Fact]
    public async Task HomeSettingsRoundTripThroughSchemaTwoJson()
    {
        var path = Path.Combine(_directory, "settings.json");
        var locationPath = Path.Combine(_directory, "photos");
        var settings = FoviumSettings.Default with
        {
            Home = new HomeSettings
            {
                ShowRecentItems = false,
                RecentLocations =
                [
                    new RecentLocation
                    {
                        Kind = RecentLocationKind.Folder,
                        Path = locationPath,
                    },
                ],
            },
        };
        var store = new JsonSettingsStore(path);

        await store.SaveAsync(settings, CancellationToken.None);
        var result = await store.LoadAsync(CancellationToken.None);

        Assert.False(result.Settings.Home.ShowRecentItems);
        var recent = Assert.Single(result.Settings.Home.RecentLocations);
        Assert.Equal(RecentLocationKind.Folder, recent.Kind);
        Assert.Equal(Path.GetFullPath(locationPath), recent.Path);
        Assert.Null(result.Diagnostic);
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

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }
}