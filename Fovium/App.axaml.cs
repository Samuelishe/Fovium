using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Fovium.Application;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Localization;
using Fovium.Navigation;
using Fovium.Settings;
using Fovium.Views;

namespace Fovium;

internal sealed partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var memoryPolicy = AutomaticMemoryPolicy.Detect();
            var pathComparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var cache = new ByteBudgetCache<string, DecodedImage>(
                memoryPolicy.CacheBudgetBytes,
                pathComparer);
            var session = new ViewerSession<DecodedImage>(
                new SkiaImageDecoder(),
                cache,
                memoryPolicy);
            var activation = new ActivationService(new DirectorySequenceBuilder());
            var localizer = Localizer.CreateForCurrentCulture();
            var settings = new SettingsService(new JsonSettingsStore(SettingsPathResolver.ResolveCurrent()));
            desktop.MainWindow = new ViewerWindow(
                activation,
                session,
                localizer,
                settings,
                desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
