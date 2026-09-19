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
                ImageDecoder.CreateDefault(),
                cache,
                memoryPolicy);
            var activation = new ActivationService(new DirectorySequenceBuilder());
            var startup = ApplicationStartup.LoadAsync(
                    new JsonSettingsStore(SettingsPathResolver.ResolveCurrent()),
                    System.Globalization.CultureInfo.CurrentUICulture,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            desktop.MainWindow = new ViewerWindow(
                activation,
                session,
                startup.Localizer,
                startup.Settings,
                desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }
}