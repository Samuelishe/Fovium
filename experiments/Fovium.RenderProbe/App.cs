using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Fovium.RenderProbe;

internal sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var initialPath = desktop.Args?.FirstOrDefault(File.Exists);
            desktop.MainWindow = new RenderProbeWindow(initialPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
