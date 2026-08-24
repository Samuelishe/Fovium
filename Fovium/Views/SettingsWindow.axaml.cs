using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Fovium.Application;
using Fovium.Localization;
using Fovium.Settings;

namespace Fovium.Views;

internal sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly RadioButton _keepCurrentScaleOption;
    private readonly RadioButton _fitEachImageOption;
    private bool _initializing = true;

    public SettingsWindow(SettingsService settings, Localizer localizer)
    {
        _settings = settings;
        InitializeComponent();

        var viewingTab = FindRequired<TabItem>("ViewingTab");
        var aboutTab = FindRequired<TabItem>("AboutTab");
        var scaleHeading = FindRequired<TextBlock>("ScaleHeading");
        _keepCurrentScaleOption = FindRequired<RadioButton>("KeepCurrentScaleOption");
        _fitEachImageOption = FindRequired<RadioButton>("FitEachImageOption");
        var versionText = FindRequired<TextBlock>("VersionText");

        Title = localizer[UiStrings.SettingsTitle];
        viewingTab.Header = localizer[UiStrings.SettingsViewing];
        aboutTab.Header = localizer[UiStrings.SettingsAbout];
        scaleHeading.Text = localizer[UiStrings.SettingsScaleOnImageChange];
        _keepCurrentScaleOption.Content = localizer[UiStrings.SettingsKeepCurrentScale];
        _fitEachImageOption.Content = localizer[UiStrings.SettingsFitEachImage];
        versionText.Text = string.Format(
            System.Globalization.CultureInfo.CurrentUICulture,
            localizer[UiStrings.SettingsVersion],
            FoviumVersion.Display);

        _keepCurrentScaleOption.IsChecked =
            settings.Current.ImageChangeViewPolicy == ImageChangeViewPolicy.KeepCurrentScale;
        _fitEachImageOption.IsChecked =
            settings.Current.ImageChangeViewPolicy == ImageChangeViewPolicy.FitEachImage;
        _keepCurrentScaleOption.IsCheckedChanged += OnKeepCurrentScaleChanged;
        _fitEachImageOption.IsCheckedChanged += OnFitEachImageChanged;
        _initializing = false;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Settings control is missing: {name}.");

    private async void OnKeepCurrentScaleChanged(object? sender, RoutedEventArgs e)
    {
        if (!_initializing && _keepCurrentScaleOption.IsChecked == true)
        {
            await _settings.SetImageChangeViewPolicyAsync(ImageChangeViewPolicy.KeepCurrentScale);
        }
    }

    private async void OnFitEachImageChanged(object? sender, RoutedEventArgs e)
    {
        if (!_initializing && _fitEachImageOption.IsChecked == true)
        {
            await _settings.SetImageChangeViewPolicyAsync(ImageChangeViewPolicy.FitEachImage);
        }
    }
}
