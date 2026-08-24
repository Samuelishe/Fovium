using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Fovium.Application;
using Fovium.Localization;
using Fovium.Settings;
using Fovium.Stage;

namespace Fovium.Views;

internal sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly RadioButton _keepCurrentScaleOption;
    private readonly RadioButton _fitEachImageOption;
    private readonly RadioButton _blackStageOption;
    private readonly RadioButton _neutralStageOption;
    private readonly RadioButton _ambientStageOption;
    private readonly RadioButton _ambientMatteStageOption;
    private bool _initializing = true;

    public SettingsWindow(SettingsService settings, Localizer localizer)
    {
        _settings = settings;
        InitializeComponent();

        var viewingTab = FindRequired<TabItem>("ViewingTab");
        var aboutTab = FindRequired<TabItem>("AboutTab");
        var stageTab = FindRequired<TabItem>("StageTab");
        var scaleHeading = FindRequired<TextBlock>("ScaleHeading");
        _keepCurrentScaleOption = FindRequired<RadioButton>("KeepCurrentScaleOption");
        _fitEachImageOption = FindRequired<RadioButton>("FitEachImageOption");
        _blackStageOption = FindRequired<RadioButton>("BlackStageOption");
        _neutralStageOption = FindRequired<RadioButton>("NeutralStageOption");
        _ambientStageOption = FindRequired<RadioButton>("AmbientStageOption");
        _ambientMatteStageOption = FindRequired<RadioButton>("AmbientMatteStageOption");
        var stageHeading = FindRequired<TextBlock>("StageHeading");
        var versionText = FindRequired<TextBlock>("VersionText");

        Title = localizer[UiStrings.SettingsTitle];
        viewingTab.Header = localizer[UiStrings.SettingsViewing];
        aboutTab.Header = localizer[UiStrings.SettingsAbout];
        stageTab.Header = localizer[UiStrings.SettingsStage];
        scaleHeading.Text = localizer[UiStrings.SettingsScaleOnImageChange];
        _keepCurrentScaleOption.Content = localizer[UiStrings.SettingsKeepCurrentScale];
        _fitEachImageOption.Content = localizer[UiStrings.SettingsFitEachImage];
        stageHeading.Text = localizer[UiStrings.SettingsStageMode];
        _blackStageOption.Content = localizer[UiStrings.StageBlack];
        _neutralStageOption.Content = localizer[UiStrings.StageNeutral];
        _ambientStageOption.Content = localizer[UiStrings.StageAmbient];
        _ambientMatteStageOption.Content = localizer[UiStrings.StageAmbientMatte];
        versionText.Text = string.Format(
            System.Globalization.CultureInfo.CurrentUICulture,
            localizer[UiStrings.SettingsVersion],
            FoviumVersion.Display);

        _keepCurrentScaleOption.IsChecked =
            settings.Current.ImageChangeViewPolicy == ImageChangeViewPolicy.KeepCurrentScale;
        _fitEachImageOption.IsChecked =
            settings.Current.ImageChangeViewPolicy == ImageChangeViewPolicy.FitEachImage;
        ApplySettings(settings.Current);
        _keepCurrentScaleOption.IsCheckedChanged += OnKeepCurrentScaleChanged;
        _fitEachImageOption.IsCheckedChanged += OnFitEachImageChanged;
        _blackStageOption.IsCheckedChanged += OnBlackStageChanged;
        _neutralStageOption.IsCheckedChanged += OnNeutralStageChanged;
        _ambientStageOption.IsCheckedChanged += OnAmbientStageChanged;
        _ambientMatteStageOption.IsCheckedChanged += OnAmbientMatteStageChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        Closed += (_, _) => _settings.SettingsChanged -= OnSettingsChanged;
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

    private async void OnBlackStageChanged(object? sender, RoutedEventArgs e) =>
        await SetStageIfCheckedAsync(_blackStageOption, StageMode.Black);

    private async void OnNeutralStageChanged(object? sender, RoutedEventArgs e) =>
        await SetStageIfCheckedAsync(_neutralStageOption, StageMode.Neutral);

    private async void OnAmbientStageChanged(object? sender, RoutedEventArgs e) =>
        await SetStageIfCheckedAsync(_ambientStageOption, StageMode.Ambient);

    private async void OnAmbientMatteStageChanged(object? sender, RoutedEventArgs e) =>
        await SetStageIfCheckedAsync(_ambientMatteStageOption, StageMode.AmbientMatte);

    private Task SetStageIfCheckedAsync(RadioButton option, StageMode mode) =>
        !_initializing && option.IsChecked == true
            ? _settings.SetStageModeAsync(mode)
            : Task.CompletedTask;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            ApplySettings(e.Settings);
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplySettings(e.Settings));
        }
    }

    private void ApplySettings(FoviumSettings settings)
    {
        _initializing = true;
        _blackStageOption.IsChecked = settings.StageMode == StageMode.Black;
        _neutralStageOption.IsChecked = settings.StageMode == StageMode.Neutral;
        _ambientStageOption.IsChecked = settings.StageMode == StageMode.Ambient;
        _ambientMatteStageOption.IsChecked = settings.StageMode == StageMode.AmbientMatte;
        _initializing = false;
    }
}
