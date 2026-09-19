using System.Globalization;
using Fovium.Localization;
using Fovium.Settings;

namespace Fovium.Application;

internal sealed record ApplicationStartupState(
    SettingsService Settings,
    Localizer Localizer);

internal static class ApplicationStartup
{
    public static async Task<ApplicationStartupState> LoadAsync(
        ISettingsStore store,
        CultureInfo systemCulture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(systemCulture);
        var settings = new SettingsService(store);
        try
        {
            await settings.InitializeAsync(cancellationToken).ConfigureAwait(false);
            var localizer = Localizer.Create(settings.Current.Language, systemCulture);
            return new ApplicationStartupState(settings, localizer);
        }
        catch
        {
            settings.Dispose();
            throw;
        }
    }
}