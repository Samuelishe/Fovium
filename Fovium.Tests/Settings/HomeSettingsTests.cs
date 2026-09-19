using Fovium.Home;
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
        Assert.Equal(20, HomeSettings.MaximumRecentLocations);
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
    public void ThumbnailCacheCapacityIsIndependentFromPersistedHistoryCapacity()
    {
        Assert.Equal(20, HomeSettings.MaximumRecentLocations);
        Assert.Equal(8, RecentThumbnailProvider.MaximumCachedItems);
        Assert.True(RecentThumbnailProvider.MaximumCachedItems < HomeSettings.MaximumRecentLocations);
    }

    [Fact]
    public async Task DisablingRememberRecentClearsHistoryAndSuppressesFutureWrites()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var path = Path.Combine(_directory, "photo.jpg");
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = path,
        });

        await service.SetRememberRecentPhotosAsync(false);
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = Path.Combine(_directory, "must-not-be-recorded.jpg"),
        });
        await service.FlushAsync();

        Assert.False(service.Current.Home.RememberRecentPhotos);
        Assert.Empty(service.Current.Home.RecentLocations);
        Assert.False(store.Saved?.Home.RememberRecentPhotos);
        Assert.Empty(store.Saved?.Home.RecentLocations ?? []);
    }

    [Fact]
    public async Task ReenablingRememberRecentStartsWithAnEmptyHistory()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.Folder,
            Path = _directory,
        });

        await service.SetRememberRecentPhotosAsync(false);
        await service.SetRememberRecentPhotosAsync(true);
        await service.FlushAsync();

        Assert.Empty(service.Current.Home.RecentLocations);
        Assert.True(service.Current.Home.RememberRecentPhotos);
        Assert.Empty(store.Saved?.Home.RecentLocations ?? []);
    }

    [Fact]
    public async Task RemoveRecentIsExplicitAndDoesNotDependOnPathAvailability()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var retained = Path.Combine(_directory, "retained.jpg");
        var removed = Path.Combine(_directory, "removed.jpg");
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = retained,
        });
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = removed,
        });

        await service.RemoveRecentLocationAsync(removed);
        await service.FlushAsync();

        var remaining = Assert.Single(service.Current.Home.RecentLocations);
        Assert.Equal(Path.GetFullPath(retained), remaining.Path);
        Assert.False(File.Exists(remaining.Path));
        Assert.Equal(service.Current.Home.RecentLocations, store.Saved?.Home.RecentLocations);
    }

    [Fact]
    public async Task FolderRecentPersistsStableLastPresentedPreview()
    {
        var path = Path.Combine(_directory, "settings.json");
        var locationPath = Path.Combine(_directory, "photos");
        var previewPath = Path.Combine(locationPath, "last-presented.jpg");
        var settings = FoviumSettings.Default with
        {
            Home = new HomeSettings
            {
                RecentLocations =
                [
                    new RecentLocation
                    {
                        Kind = RecentLocationKind.Folder,
                        Path = locationPath,
                        PreviewPath = previewPath,
                    },
                ],
            },
        };
        var store = new JsonSettingsStore(path);

        await store.SaveAsync(settings, CancellationToken.None);
        var result = await store.LoadAsync(CancellationToken.None);

        var recent = Assert.Single(result.Settings.Home.RecentLocations);
        Assert.Equal(Path.GetFullPath(locationPath), recent.Path);
        Assert.Equal(Path.GetFullPath(previewPath), recent.PreviewPath);
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
                RememberRecentPhotos = false,
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

        Assert.False(result.Settings.Home.RememberRecentPhotos);
        Assert.Empty(result.Settings.Home.RecentLocations);
        Assert.Null(result.Diagnostic);
    }

    [Fact]
    public async Task RevisitingFolderMovesItToFrontAndUpdatesItsPreview()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var folder = Path.Combine(_directory, "photos");
        var firstPreview = Path.Combine(folder, "first.jpg");
        var latestPreview = Path.Combine(folder, "latest.jpg");
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.Folder,
            Path = folder,
            PreviewPath = firstPreview,
        });
        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.File,
            Path = Path.Combine(_directory, "other.jpg"),
        });

        await service.AddRecentLocationAsync(new RecentLocation
        {
            Kind = RecentLocationKind.Folder,
            Path = folder,
            PreviewPath = latestPreview,
        });

        Assert.Equal(Path.GetFullPath(folder), service.Current.Home.RecentLocations[0].Path);
        Assert.Equal(Path.GetFullPath(latestPreview), service.Current.Home.RecentLocations[0].PreviewPath);
        Assert.Equal(2, service.Current.Home.RecentLocations.Count);
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