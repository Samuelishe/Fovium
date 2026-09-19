using System.Globalization;
using Fovium.Application;
using Fovium.Localization;
using Fovium.Settings;

namespace Fovium.Tests.Application;

public sealed class ApplicationStartupTests
{
    [Theory]
    [InlineData((int)UiLanguage.English, "ru-RU", "en", "Settings")]
    [InlineData((int)UiLanguage.Russian, "en-US", "ru", "Настройки")]
    [InlineData((int)UiLanguage.SystemDefault, "ru-RU", "ru", "Настройки")]
    public async Task StoredLanguageIsResolvedBeforeApplicationUiIsCreated(
        int storedLanguageValue,
        string systemCulture,
        string expectedLocale,
        string expectedSettingsTitle)
    {
        var storedLanguage = (UiLanguage)storedLanguageValue;
        var store = new StartupSettingsStore(
            FoviumSettings.Default with { Language = storedLanguage });

        var startup = await ApplicationStartup.LoadAsync(
            store,
            CultureInfo.GetCultureInfo(systemCulture),
            CancellationToken.None);

        using (startup.Settings)
        {
            Assert.Equal(storedLanguage, startup.Settings.Current.Language);
            Assert.Equal(expectedLocale, startup.Localizer.Locale);
            Assert.Equal(expectedSettingsTitle, startup.Localizer[UiStrings.SettingsTitle]);
            Assert.Equal(1, store.LoadCount);
        }
    }

    private sealed class StartupSettingsStore(FoviumSettings settings) : ISettingsStore
    {
        public int LoadCount { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            LoadCount++;
            return Task.FromResult(new SettingsLoadResult(settings, null));
        }

        public Task SaveAsync(FoviumSettings updatedSettings, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}