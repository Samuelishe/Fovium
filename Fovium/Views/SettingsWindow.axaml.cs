using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Fovium.Application;
using Fovium.Input;
using Fovium.Localization;
using Fovium.Presentation;
using Fovium.Settings;
using Fovium.Stage;

namespace Fovium.Views;

internal sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly Localizer _localizer;
    private readonly RadioButton _keepCurrentScaleOption;
    private readonly RadioButton _fitEachImageOption;
    private readonly CheckBox _monitorColorManagementOption;
    private readonly RadioButton _blackStageOption;
    private readonly RadioButton _neutralStageOption;
    private readonly RadioButton _customStageOption;
    private readonly RadioButton _ambientStageOption;
    private readonly CheckBox _matteEnabledOption;
    private readonly ComboBox _matteStyleOption;
    private readonly Slider _matteWidthSlider;
    private readonly TextBlock _matteWidthValue;
    private readonly Border _customColorSwatch;
    private readonly Border _matteColorSwatch;
    private readonly Slider _brightnessSlider;
    private readonly Slider _saturationSlider;
    private readonly Slider _blurSlider;
    private readonly TextBlock _brightnessValue;
    private readonly TextBlock _saturationValue;
    private readonly TextBlock _blurValue;
    private readonly TextBlock _shortcutValidationText;
    private readonly Expander _ambientOptions;
    private readonly CheckBox _enableMarkupOption;
    private readonly Border _highlightColorSwatch;
    private readonly Border _defaultMarkupColorSwatch;
    private readonly Slider _highlightOpacitySlider;
    private readonly Slider _highlightRadiusSlider;
    private readonly Slider _defaultStrokeSlider;
    private readonly Slider _defaultMarkupOpacitySlider;
    private readonly TextBlock _highlightOpacityValue;
    private readonly TextBlock _highlightRadiusValue;
    private readonly TextBlock _defaultStrokeValue;
    private readonly TextBlock _defaultMarkupOpacityValue;
    private readonly Dictionary<ViewerCommand, Button> _shortcutButtons = [];
    private ViewerCommand? _capturingCommand;
    private bool _initializing = true;

    public SettingsWindow(SettingsService settings, Localizer localizer)
    {
        _settings = settings;
        _localizer = localizer;
        InitializeComponent();

        var viewingTab = FindRequired<TabItem>("ViewingTab");
        var colorTab = FindRequired<TabItem>("ColorTab");
        var stageTab = FindRequired<TabItem>("StageTab");
        var presentationTab = FindRequired<TabItem>("PresentationTab");
        var controlsTab = FindRequired<TabItem>("ControlsTab");
        var aboutTab = FindRequired<TabItem>("AboutTab");
        _keepCurrentScaleOption = FindRequired<RadioButton>("KeepCurrentScaleOption");
        _fitEachImageOption = FindRequired<RadioButton>("FitEachImageOption");
        _monitorColorManagementOption = FindRequired<CheckBox>("MonitorColorManagementOption");
        _blackStageOption = FindRequired<RadioButton>("BlackStageOption");
        _neutralStageOption = FindRequired<RadioButton>("NeutralStageOption");
        _customStageOption = FindRequired<RadioButton>("CustomStageOption");
        _ambientStageOption = FindRequired<RadioButton>("AmbientStageOption");
        _matteEnabledOption = FindRequired<CheckBox>("MatteEnabledOption");
        _matteStyleOption = FindRequired<ComboBox>("MatteStyleOption");
        _matteWidthSlider = FindRequired<Slider>("MatteWidthSlider");
        _matteWidthValue = FindRequired<TextBlock>("MatteWidthValue");
        _customColorSwatch = FindRequired<Border>("CustomColorSwatch");
        _matteColorSwatch = FindRequired<Border>("MatteColorSwatch");
        _brightnessSlider = FindRequired<Slider>("BrightnessSlider");
        _saturationSlider = FindRequired<Slider>("SaturationSlider");
        _blurSlider = FindRequired<Slider>("BlurSlider");
        _brightnessValue = FindRequired<TextBlock>("BrightnessValue");
        _saturationValue = FindRequired<TextBlock>("SaturationValue");
        _blurValue = FindRequired<TextBlock>("BlurValue");
        _shortcutValidationText = FindRequired<TextBlock>("ShortcutValidationText");
        _ambientOptions = FindRequired<Expander>("AmbientOptions");
        _enableMarkupOption = FindRequired<CheckBox>("EnableMarkupOption");
        _highlightColorSwatch = FindRequired<Border>("HighlightColorSwatch");
        _defaultMarkupColorSwatch = FindRequired<Border>("DefaultMarkupColorSwatch");
        _highlightOpacitySlider = FindRequired<Slider>("HighlightOpacitySlider");
        _highlightRadiusSlider = FindRequired<Slider>("HighlightRadiusSlider");
        _defaultStrokeSlider = FindRequired<Slider>("DefaultStrokeSlider");
        _defaultMarkupOpacitySlider = FindRequired<Slider>("DefaultMarkupOpacitySlider");
        _highlightOpacityValue = FindRequired<TextBlock>("HighlightOpacityValue");
        _highlightRadiusValue = FindRequired<TextBlock>("HighlightRadiusValue");
        _defaultStrokeValue = FindRequired<TextBlock>("DefaultStrokeValue");
        _defaultMarkupOpacityValue = FindRequired<TextBlock>("DefaultMarkupOpacityValue");

        Title = localizer[UiStrings.SettingsTitle];
        viewingTab.Header = localizer[UiStrings.SettingsViewing];
        colorTab.Header = localizer[UiStrings.SettingsColor];
        stageTab.Header = localizer[UiStrings.SettingsStage];
        presentationTab.Header = localizer[UiStrings.SettingsPresentation];
        controlsTab.Header = localizer[UiStrings.SettingsControls];
        aboutTab.Header = localizer[UiStrings.SettingsAbout];
        FindRequired<TextBlock>("ScaleHeading").Text = localizer[UiStrings.SettingsScaleOnImageChange];
        FindRequired<TextBlock>("MonitorColorManagementHeading").Text =
            localizer[UiStrings.ColorMonitorManagement];
        _monitorColorManagementOption.Content = localizer[UiStrings.ColorUseActiveMonitorProfile];
        FindRequired<TextBlock>("MonitorColorManagementExplanation").Text =
            localizer[UiStrings.ColorMonitorManagementExplanation];
        FindRequired<TextBlock>("BackgroundHeading").Text = localizer[UiStrings.StageBackground];
        FindRequired<TextBlock>("MatteHeading").Text = localizer[UiStrings.StageMatte];
        FindRequired<TextBlock>("ControlsHeading").Text = localizer[UiStrings.SettingsControls];
        _enableMarkupOption.Content = localizer[UiStrings.PresentationEnableMarkup];
        FindRequired<TextBlock>("HighlightHeading").Text = localizer[UiStrings.PresentationHighlight];
        FindRequired<TextBlock>("HighlightColorLabel").Text = localizer[UiStrings.PresentationHighlightColor];
        FindRequired<TextBlock>("HighlightOpacityLabel").Text = localizer[UiStrings.PresentationHighlightOpacity];
        FindRequired<TextBlock>("HighlightRadiusLabel").Text = localizer[UiStrings.PresentationHighlightRadius];
        FindRequired<TextBlock>("MarkupDefaultsHeading").Text = localizer[UiStrings.PresentationMarkupDefaults];
        FindRequired<TextBlock>("MarkupColorLabel").Text = localizer[UiStrings.PresentationMarkupColor];
        FindRequired<TextBlock>("DefaultStrokeLabel").Text = localizer[UiStrings.PresentationStroke];
        FindRequired<TextBlock>("DefaultMarkupOpacityLabel").Text =
            localizer[UiStrings.PresentationOpacity];
        FindRequired<TextBlock>("BrightnessLabel").Text = localizer[UiStrings.StageAmbientBrightness];
        FindRequired<TextBlock>("SaturationLabel").Text = localizer[UiStrings.StageAmbientSaturation];
        FindRequired<TextBlock>("BlurLabel").Text = localizer[UiStrings.StageAmbientBlur];
        FindRequired<TextBlock>("MatteStyleLabel").Text = localizer[UiStrings.StageMatteStyle];
        FindRequired<TextBlock>("MatteSizeLabel").Text = localizer[UiStrings.StageMatteSize];
        FindRequired<TextBlock>("MatteColorLabel").Text = localizer[UiStrings.StageMatteColor];
        _keepCurrentScaleOption.Content = localizer[UiStrings.SettingsKeepCurrentScale];
        _fitEachImageOption.Content = localizer[UiStrings.SettingsFitEachImage];
        _blackStageOption.Content = localizer[UiStrings.StageBlack];
        _neutralStageOption.Content = localizer[UiStrings.StageNeutral];
        _customStageOption.Content = localizer[UiStrings.StageCustom];
        _ambientStageOption.Content = localizer[UiStrings.StageAmbient];
        _matteEnabledOption.Content = localizer[UiStrings.StageMatteEnabled];
        _ambientOptions.Header = localizer[UiStrings.StageAmbientOptions];
        _matteStyleOption.ItemsSource = Enum.GetValues<MatteStyle>()
            .Select(style => new ComboBoxItem { Content = LocalizeMatteStyle(style), Tag = style })
            .ToArray();
        FindRequired<Button>("ResetShortcutsButton").Content = localizer[UiStrings.ShortcutReset];
        FindRequired<TextBlock>("VersionText").Text = string.Format(
            System.Globalization.CultureInfo.CurrentUICulture,
            localizer[UiStrings.SettingsVersion],
            FoviumVersion.Display);

        CreateShortcutRows();
        ApplySettings(settings.Current);
        SubscribeEvents();
        _settings.SettingsChanged += OnSettingsChanged;
        Closed += OnClosed;
        KeyDown += OnShortcutCaptureKeyDown;
        _initializing = false;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void SubscribeEvents()
    {
        _keepCurrentScaleOption.IsCheckedChanged += OnKeepCurrentScaleChanged;
        _fitEachImageOption.IsCheckedChanged += OnFitEachImageChanged;
        _monitorColorManagementOption.IsCheckedChanged += async (_, _) =>
        {
            if (!_initializing)
            {
                await _settings.SetMonitorColorManagementEnabledAsync(
                    _monitorColorManagementOption.IsChecked == true);
            }
        };
        _blackStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _blackStageOption,
            StageBackgroundMode.Black);
        _neutralStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _neutralStageOption,
            StageBackgroundMode.Neutral);
        _customStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _customStageOption,
            StageBackgroundMode.Custom);
        _ambientStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _ambientStageOption,
            StageBackgroundMode.Ambient);
        _matteEnabledOption.IsCheckedChanged += OnMatteEnabledChanged;
        _matteStyleOption.SelectionChanged += OnMatteStyleChanged;
        _matteWidthSlider.ValueChanged += OnMatteWidthChanged;
        _brightnessSlider.ValueChanged += OnAmbientSliderChanged;
        _saturationSlider.ValueChanged += OnAmbientSliderChanged;
        _blurSlider.ValueChanged += OnAmbientSliderChanged;
        FindRequired<Button>("EditAmbientButton").Click += (_, _) =>
            _ambientOptions.IsExpanded = !_ambientOptions.IsExpanded;
        FindRequired<Button>("EditCustomColorButton").Click += async (_, _) =>
            await EditColorAsync(customBackground: true);
        FindRequired<Button>("EditMatteColorButton").Click += async (_, _) =>
            await EditColorAsync(customBackground: false);
        FindRequired<Button>("ResetShortcutsButton").Click += async (_, _) =>
            await _settings.ResetShortcutsAsync();
        _enableMarkupOption.IsCheckedChanged += async (_, _) =>
        {
            if (!_initializing)
            {
                await _settings.SetPresentationAsync(_settings.Current.Presentation with
                {
                    MarkupToolsEnabled = _enableMarkupOption.IsChecked == true,
                });
            }
        };
        _highlightOpacitySlider.ValueChanged += OnPresentationSliderChanged;
        _highlightRadiusSlider.ValueChanged += OnPresentationSliderChanged;
        _defaultStrokeSlider.ValueChanged += OnPresentationSliderChanged;
        _defaultMarkupOpacitySlider.ValueChanged += OnPresentationSliderChanged;
        FindRequired<Button>("EditHighlightColorButton").Click += async (_, _) =>
            await EditPresentationColorAsync(highlight: true);
        FindRequired<Button>("EditDefaultMarkupColorButton").Click += async (_, _) =>
            await EditPresentationColorAsync(highlight: false);
    }

    private void CreateShortcutRows()
    {
        var list = FindRequired<StackPanel>("ControlsList");
        foreach (var group in Enum.GetValues<ViewerCommandGroup>())
        {
            var definitions = ViewerCommands.Definitions
                .Where(definition => definition.Group == group)
                .ToArray();
            if (definitions.Length == 0)
            {
                continue;
            }

            list.Children.Add(new TextBlock
            {
                Text = _localizer[UiStrings.ForCommandGroup(group)],
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Margin = new Avalonia.Thickness(0, 8, 0, 0),
            });

            var hintKey = group switch
            {
                ViewerCommandGroup.Presentation => UiStrings.CommandScopeHighlightHint,
                ViewerCommandGroup.Markup => UiStrings.CommandScopeMarkupHint,
                _ => null,
            };
            if (hintKey is not null)
            {
                list.Children.Add(new TextBlock
                {
                    Text = _localizer[hintKey],
                    Opacity = 0.65,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                });
            }

            foreach (var definition in definitions)
            {
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 14,
                };
                row.Children.Add(new TextBlock
                {
                    Text = LocalizeCommand(definition.Command),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                });
                var button = new Button
                {
                    MinWidth = 128,
                    HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Tag = definition.Command,
                };
                Grid.SetColumn(button, 1);
                button.Click += OnShortcutButtonClick;
                row.Children.Add(button);
                list.Children.Add(row);
                _shortcutButtons.Add(definition.Command, button);
            }
        }
    }

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

    private async void SetBackgroundIfChecked(RadioButton option, StageBackgroundMode mode)
    {
        if (!_initializing && option.IsChecked == true)
        {
            await _settings.SetStageAsync(_settings.Current.Stage with { BackgroundMode = mode });
        }
    }

    private async void OnMatteEnabledChanged(object? sender, RoutedEventArgs e)
    {
        if (!_initializing)
        {
            await _settings.SetStageAsync(_settings.Current.Stage with
            {
                MatteEnabled = _matteEnabledOption.IsChecked == true,
            });
        }
    }

    private async void OnMatteStyleChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && _matteStyleOption.SelectedItem is ComboBoxItem { Tag: MatteStyle style })
        {
            await _settings.SetStageAsync(_settings.Current.Stage with { MatteStyle = style });
        }
    }

    private async void OnMatteWidthChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var width = Math.Round(_matteWidthSlider.Value);
        UpdateMatteWidthText(width);
        await _settings.SetStageAsync(_settings.Current.Stage with
        {
            MatteWidthPhysicalPixels = width,
        });
    }

    private async void OnAmbientSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var stage = _settings.Current.Stage with
        {
            AmbientBrightness = _brightnessSlider.Value / 100,
            AmbientSaturation = _saturationSlider.Value / 100,
            AmbientBlur = Math.Round(_blurSlider.Value),
        };
        UpdateAmbientValueText(stage);
        await _settings.SetStageAsync(stage);
    }

    private async Task EditColorAsync(bool customBackground)
    {
        var original = customBackground
            ? _settings.Current.Stage.CustomBackgroundColor
            : _settings.Current.Stage.MatteColor;
        var title = _localizer[customBackground
            ? UiStrings.StageCustomColor
            : UiStrings.StageMatteColor];
        var editor = new ColorEditorWindow(original, _localizer, title);
        editor.ColorChanged += async (_, e) =>
        {
            var stage = _settings.Current.Stage;
            await _settings.SetStageAsync(customBackground
                ? stage with { CustomBackgroundColor = e.Color }
                : stage with { MatteColor = e.Color });
        };
        var accepted = await editor.ShowDialog<bool>(this);
        if (!accepted)
        {
            var stage = _settings.Current.Stage;
            await _settings.SetStageAsync(customBackground
                ? stage with { CustomBackgroundColor = original }
                : stage with { MatteColor = original });
        }
    }

    private void OnShortcutButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewerCommand command })
        {
            return;
        }

        _capturingCommand = command;
        _shortcutValidationText.Text = string.Empty;
        UpdateShortcutButtons(_settings.Current.Shortcuts);
    }

    private async void OnShortcutCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (_capturingCommand is not { } command)
        {
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            CancelShortcutCapture();
            return;
        }

        if (!AvaloniaShortcutGestureAdapter.TryCreate(e, out var gesture))
        {
            _shortcutValidationText.Text = _localizer[UiStrings.ShortcutInvalid];
            return;
        }

        var current = _settings.Current.Shortcuts;
        var assignment = ShortcutResolver.Assign(current, command, gesture, replaceConflict: false);
        if (assignment.Status == ShortcutAssignmentStatus.Invalid)
        {
            _shortcutValidationText.Text = _localizer[UiStrings.ShortcutInvalid];
            return;
        }

        _capturingCommand = null;
        if (assignment.Status == ShortcutAssignmentStatus.Conflict &&
            assignment.ConflictingCommand is { } conflict)
        {
            UpdateShortcutButtons(current);
            var dialog = new ShortcutConflictWindow(_localizer, LocalizeCommand(conflict));
            if (!await dialog.ShowDialog<bool>(this))
            {
                return;
            }

            assignment = ShortcutResolver.Assign(current, command, gesture, replaceConflict: true);
        }

        _shortcutValidationText.Text = string.Empty;
        await _settings.SetShortcutsAsync(assignment.Settings);
    }

    private void CancelShortcutCapture()
    {
        _capturingCommand = null;
        _shortcutValidationText.Text = string.Empty;
        UpdateShortcutButtons(_settings.Current.Shortcuts);
    }

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
        _keepCurrentScaleOption.IsChecked =
            settings.ImageChangeViewPolicy == ImageChangeViewPolicy.KeepCurrentScale;
        _fitEachImageOption.IsChecked =
            settings.ImageChangeViewPolicy == ImageChangeViewPolicy.FitEachImage;
        _monitorColorManagementOption.IsChecked = settings.MonitorColorManagementEnabled;
        _blackStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Black;
        _neutralStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Neutral;
        _customStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Custom;
        _ambientStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Ambient;
        _matteEnabledOption.IsChecked = settings.Stage.MatteEnabled;
        _matteStyleOption.SelectedItem = _matteStyleOption.ItemsSource?
            .OfType<ComboBoxItem>()
            .Single(item => item.Tag is MatteStyle style && style == settings.Stage.MatteStyle);
        _matteWidthSlider.Value = settings.Stage.MatteWidthPhysicalPixels;
        _brightnessSlider.Value = settings.Stage.AmbientBrightness * 100;
        _saturationSlider.Value = settings.Stage.AmbientSaturation * 100;
        _blurSlider.Value = settings.Stage.AmbientBlur;
        SetSwatch(_customColorSwatch, settings.Stage.CustomBackgroundColor);
        SetSwatch(_matteColorSwatch, settings.Stage.MatteColor);
        UpdateAmbientValueText(settings.Stage);
        UpdateMatteWidthText(settings.Stage.MatteWidthPhysicalPixels);
        UpdateShortcutButtons(settings.Shortcuts);
        _enableMarkupOption.IsChecked = settings.Presentation.MarkupToolsEnabled;
        _highlightOpacitySlider.Value = settings.Presentation.HighlightOpacity * 100;
        _highlightRadiusSlider.Value = settings.Presentation.HighlightRadiusPhysicalPixels;
        _defaultStrokeSlider.Value = settings.Presentation.DefaultMarkupStrokePhysicalPixels;
        _defaultMarkupOpacitySlider.Value = settings.Presentation.DefaultMarkupOpacity * 100;
        SetSwatch(_highlightColorSwatch, settings.Presentation.HighlightColor);
        SetSwatch(_defaultMarkupColorSwatch, settings.Presentation.DefaultMarkupColor);
        UpdatePresentationValueText(settings.Presentation);
        _initializing = false;
    }

    private void UpdateAmbientValueText(StageSettings stage)
    {
        _brightnessValue.Text = $"{stage.AmbientBrightness:P0}";
        _saturationValue.Text = $"{stage.AmbientSaturation:P0}";
        _blurValue.Text = stage.AmbientBlur.ToString("0", System.Globalization.CultureInfo.CurrentUICulture);
    }

    private void UpdateMatteWidthText(double width) =>
        _matteWidthValue.Text = $"{width:0} px";

    private string LocalizeMatteStyle(MatteStyle style) => _localizer[style switch
    {
        MatteStyle.Solid => UiStrings.StageMatteSolid,
        MatteStyle.Rounded => UiStrings.StageMatteRounded,
        MatteStyle.Soft => UiStrings.StageMatteSoft,
        MatteStyle.Angular => UiStrings.StageMatteAngular,
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    }];

    private void UpdateShortcutButtons(ShortcutSettings shortcuts)
    {
        foreach (var (command, button) in _shortcutButtons)
        {
            button.Content = _capturingCommand == command
                ? _localizer[UiStrings.ShortcutPressKey]
                : ShortcutGestureFormatter.Format(
                    shortcuts.Get(command),
                    _localizer[UiStrings.ShortcutUnassigned]);
        }
    }

    private string LocalizeCommand(ViewerCommand command) =>
        _localizer[UiStrings.ForCommand(command)];

    private void OnClosed(object? sender, EventArgs e)
    {
        _capturingCommand = null;
        _settings.SettingsChanged -= OnSettingsChanged;
    }

    private static void SetSwatch(Border border, StageColor color) =>
        border.Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));

    private static void SetSwatch(Border border, PresentationColor color) =>
        border.Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));

    private async void OnPresentationSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var presentation = _settings.Current.Presentation with
        {
            HighlightOpacity = _highlightOpacitySlider.Value / 100,
            HighlightRadiusPhysicalPixels = Math.Round(_highlightRadiusSlider.Value),
            DefaultMarkupStrokePhysicalPixels = Math.Round(_defaultStrokeSlider.Value),
            DefaultMarkupOpacity = _defaultMarkupOpacitySlider.Value / 100,
        };
        UpdatePresentationValueText(presentation);
        await _settings.SetPresentationAsync(presentation);
    }

    private async Task EditPresentationColorAsync(bool highlight)
    {
        var presentation = _settings.Current.Presentation;
        var original = highlight ? presentation.HighlightColor : presentation.DefaultMarkupColor;
        var editor = new ColorEditorWindow(
            new StageColor(original.Red, original.Green, original.Blue),
            _localizer,
            _localizer[highlight
                ? UiStrings.PresentationHighlightColor
                : UiStrings.PresentationMarkupColor]);
        editor.ColorChanged += async (_, args) =>
        {
            var color = new PresentationColor(args.Color.Red, args.Color.Green, args.Color.Blue);
            var current = _settings.Current.Presentation;
            await _settings.SetPresentationAsync(highlight
                ? current with { HighlightColor = color }
                : current with { DefaultMarkupColor = color });
        };
        var accepted = await editor.ShowDialog<bool>(this);
        if (!accepted)
        {
            var current = _settings.Current.Presentation;
            await _settings.SetPresentationAsync(highlight
                ? current with { HighlightColor = original }
                : current with { DefaultMarkupColor = original });
        }
    }

    private void UpdatePresentationValueText(PresentationSettings presentation)
    {
        _highlightOpacityValue.Text = $"{presentation.HighlightOpacity:P0}";
        _highlightRadiusValue.Text = $"{presentation.HighlightRadiusPhysicalPixels:0} px";
        _defaultStrokeValue.Text = $"{presentation.DefaultMarkupStrokePhysicalPixels:0} px";
        _defaultMarkupOpacityValue.Text = $"{presentation.DefaultMarkupOpacity:P0}";
    }

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Settings control is missing: {name}.");
}
