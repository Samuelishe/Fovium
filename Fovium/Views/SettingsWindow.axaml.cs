using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Globalization;
using Fovium.Application;
using Fovium.Input;
using Fovium.Localization;
using Fovium.Presentation;
using Fovium.Settings;
using Fovium.Slideshow;
using Fovium.Stage;
using Fovium.Viewer;

namespace Fovium.Views;

internal sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly Localizer _localizer;
    private readonly PhotoPresentationViewSession _photoPresentationView;
    private readonly SlideshowSession _slideshow;
    private readonly ListBox _settingsNavigation;
    private readonly Button _closeButton;
    private readonly IReadOnlyDictionary<SettingsSection, Control> _sectionPages;
    private readonly ComboBox _languageOption;
    private readonly Border _languageRestartPanel;
    private readonly TextBlock _languageRestartHint;
    private readonly CheckBox _rememberRecentPhotosOption;
    private readonly RadioButton _keepCurrentScaleOption;
    private readonly RadioButton _fitEachImageOption;
    private readonly CheckBox _photoPresentationEnabledOption;
    private readonly Slider _photoPresentationMarginSlider;
    private readonly TextBlock _photoPresentationMarginValue;
    private readonly CheckBox _slideshowEnabledOption;
    private readonly Slider _slideshowDurationSlider;
    private readonly TextBlock _slideshowDurationValue;
    private readonly RadioButton _slideshowStopAtEndOption;
    private readonly RadioButton _slideshowLoopOption;
    private readonly CheckBox _monitorColorManagementOption;
    private readonly RadioButton _blackStageOption;
    private readonly RadioButton _neutralStageOption;
    private readonly RadioButton _customStageOption;
    private readonly RadioButton _ambientStageOption;
    private readonly RadioButton _averageStageOption;
    private readonly RadioButton _dominantStageOption;
    private readonly RadioButton _colorWashStageOption;
    private readonly RadioButton _colorGradientStageOption;
    private readonly RadioButton _softGlowStageOption;
    private readonly CheckBox _matteEnabledOption;
    private readonly ComboBox _matteStyleOption;
    private readonly ComboBox _matteColorSourceOption;
    private readonly ComboBox _photoSeparationOption;
    private readonly Grid _matteCustomColorPanel;
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
    private readonly Avalonia.Threading.DispatcherTimer _windowSizePersistenceTimer;
    private ViewerCommand? _capturingCommand;
    private Avalonia.Size? _pendingWindowSize;
    private bool _initializing = true;

    public SettingsWindow(
        SettingsService settings,
        Localizer localizer,
        PhotoPresentationViewSession photoPresentationView,
        SlideshowSession slideshow,
        Avalonia.Size initialSize)
    {
        _settings = settings;
        _localizer = localizer;
        _photoPresentationView = photoPresentationView;
        _slideshow = slideshow;
        InitializeComponent();
        Width = initialSize.Width;
        Height = initialSize.Height;
        _windowSizePersistenceTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _windowSizePersistenceTimer.Tick += OnWindowSizePersistenceTimerTick;

        _settingsNavigation = FindRequired<ListBox>("SettingsNavigation");
        _closeButton = FindRequired<Button>("SettingsCloseButton");
        _sectionPages = new Dictionary<SettingsSection, Control>
        {
            [SettingsSection.General] = FindRequired<ScrollViewer>("GeneralPage"),
            [SettingsSection.Viewing] = FindRequired<ScrollViewer>("ViewingPage"),
            [SettingsSection.Color] = FindRequired<ScrollViewer>("ColorPage"),
            [SettingsSection.Stage] = FindRequired<ScrollViewer>("StagePage"),
            [SettingsSection.Presentation] = FindRequired<ScrollViewer>("PresentationPage"),
            [SettingsSection.Controls] = FindRequired<ScrollViewer>("ControlsPage"),
            [SettingsSection.About] = FindRequired<ScrollViewer>("AboutPage"),
        };
        _languageOption = FindRequired<ComboBox>("LanguageOption");
        _languageRestartPanel = FindRequired<Border>("LanguageRestartPanel");
        _languageRestartHint = FindRequired<TextBlock>("LanguageRestartHint");
        _rememberRecentPhotosOption = FindRequired<CheckBox>("RememberRecentPhotosOption");
        _keepCurrentScaleOption = FindRequired<RadioButton>("KeepCurrentScaleOption");
        _fitEachImageOption = FindRequired<RadioButton>("FitEachImageOption");
        _photoPresentationEnabledOption =
            FindRequired<CheckBox>("PhotoPresentationEnabledOption");
        _photoPresentationMarginSlider = FindRequired<Slider>("PhotoPresentationMarginSlider");
        _photoPresentationMarginValue = FindRequired<TextBlock>("PhotoPresentationMarginValue");
        _slideshowEnabledOption = FindRequired<CheckBox>("SlideshowEnabledOption");
        _slideshowDurationSlider = FindRequired<Slider>("SlideshowDurationSlider");
        _slideshowDurationValue = FindRequired<TextBlock>("SlideshowDurationValue");
        _slideshowStopAtEndOption = FindRequired<RadioButton>("SlideshowStopAtEndOption");
        _slideshowLoopOption = FindRequired<RadioButton>("SlideshowLoopOption");
        _monitorColorManagementOption = FindRequired<CheckBox>("MonitorColorManagementOption");
        _blackStageOption = FindRequired<RadioButton>("BlackStageOption");
        _neutralStageOption = FindRequired<RadioButton>("NeutralStageOption");
        _customStageOption = FindRequired<RadioButton>("CustomStageOption");
        _ambientStageOption = FindRequired<RadioButton>("AmbientStageOption");
        _averageStageOption = FindRequired<RadioButton>("AverageStageOption");
        _dominantStageOption = FindRequired<RadioButton>("DominantStageOption");
        _colorWashStageOption = FindRequired<RadioButton>("ColorWashStageOption");
        _colorGradientStageOption = FindRequired<RadioButton>("ColorGradientStageOption");
        _softGlowStageOption = FindRequired<RadioButton>("SoftGlowStageOption");
        _matteEnabledOption = FindRequired<CheckBox>("MatteEnabledOption");
        _matteStyleOption = FindRequired<ComboBox>("MatteStyleOption");
        _matteColorSourceOption = FindRequired<ComboBox>("MatteColorSourceOption");
        _photoSeparationOption = FindRequired<ComboBox>("PhotoSeparationOption");
        _matteCustomColorPanel = FindRequired<Grid>("MatteCustomColorPanel");
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
        _closeButton.Content = FoviumIconCatalog.Create(FoviumIcon.Close, 14);
        ToolTip.SetTip(_closeButton, localizer[UiStrings.MenuClose]);
        AutomationProperties.SetName(_closeButton, localizer[UiStrings.MenuClose]);
        AttachResizeHandle("ResizeNorth", WindowEdge.North);
        AttachResizeHandle("ResizeSouth", WindowEdge.South);
        AttachResizeHandle("ResizeWest", WindowEdge.West);
        AttachResizeHandle("ResizeEast", WindowEdge.East);
        AttachResizeHandle("ResizeNorthWest", WindowEdge.NorthWest);
        AttachResizeHandle("ResizeNorthEast", WindowEdge.NorthEast);
        AttachResizeHandle("ResizeSouthWest", WindowEdge.SouthWest);
        AttachResizeHandle("ResizeSouthEast", WindowEdge.SouthEast);
        FindRequired<TextBlock>("SidebarTitle").Text = localizer[UiStrings.SettingsTitle];
        _settingsNavigation.ItemsSource = SettingsSectionCatalog.Ordered
            .Select(section =>
            {
                var item = new ListBoxItem
                {
                    Content = LocalizeSettingsSection(section),
                    Tag = section,
                };
                item.Classes.Add("navigation-item");
                return item;
            })
            .ToArray();
        _settingsNavigation.SelectedIndex = 0;
        FindRequired<TextBlock>("GeneralPageTitle").Text = localizer[UiStrings.SettingsGeneral];
        FindRequired<TextBlock>("GeneralPageDescription").Text =
            localizer[UiStrings.SettingsGeneralDescription];
        FindRequired<TextBlock>("ViewingPageTitle").Text = localizer[UiStrings.SettingsViewing];
        FindRequired<TextBlock>("ViewingPageDescription").Text =
            localizer[UiStrings.SettingsViewingDescription];
        FindRequired<TextBlock>("ColorPageTitle").Text = localizer[UiStrings.SettingsColor];
        FindRequired<TextBlock>("ColorPageDescription").Text =
            localizer[UiStrings.SettingsColorDescription];
        FindRequired<TextBlock>("StagePageTitle").Text = localizer[UiStrings.SettingsStage];
        FindRequired<TextBlock>("StagePageDescription").Text =
            localizer[UiStrings.SettingsStageDescription];
        FindRequired<TextBlock>("PresentationPageTitle").Text =
            localizer[UiStrings.SettingsPresentation];
        FindRequired<TextBlock>("PresentationPageDescription").Text =
            localizer[UiStrings.SettingsPresentationDescription];
        FindRequired<TextBlock>("ControlsPageTitle").Text = localizer[UiStrings.SettingsControls];
        FindRequired<TextBlock>("ControlsPageDescription").Text =
            localizer[UiStrings.SettingsControlsDescription];
        FindRequired<TextBlock>("AboutPageTitle").Text = localizer[UiStrings.SettingsAbout];
        FindRequired<TextBlock>("AboutPageDescription").Text =
            localizer[UiStrings.SettingsAboutDescription];
        FindRequired<TextBlock>("LanguageHeading").Text = localizer[UiStrings.SettingsLanguage];
        FindRequired<TextBlock>("LanguageDescription").Text =
            localizer[UiStrings.SettingsLanguageDescription];
        _languageRestartHint.Text = localizer[UiStrings.SettingsLanguageRestart];
        FindRequired<TextBlock>("RecentItemsHeading").Text =
            localizer[UiStrings.SettingsRememberRecentPhotos];
        FindRequired<TextBlock>("RecentItemsDescription").Text =
            localizer[UiStrings.SettingsRememberRecentPhotosDescription];
        _languageOption.ItemsSource = Enum.GetValues<UiLanguage>()
            .Select(language => new ComboBoxItem
            {
                Content = LocalizeLanguage(language),
                Tag = language,
            })
            .ToArray();
        FindRequired<TextBlock>("ScaleHeading").Text = localizer[UiStrings.SettingsScaleOnImageChange];
        FindRequired<TextBlock>("PhotoPresentationHeading").Text =
            localizer[UiStrings.SettingsPhotoPresentationView];
        _photoPresentationEnabledOption.Content =
            localizer[UiStrings.SettingsEnablePhotoPresentation];
        FindRequired<TextBlock>("PhotoPresentationMarginLabel").Text =
            localizer[UiStrings.SettingsPhotoPresentationEdgeMargin];
        FindRequired<TextBlock>("PhotoPresentationExplanation").Text =
            localizer[UiStrings.SettingsPhotoPresentationExplanation];
        FindRequired<TextBlock>("SlideshowHeading").Text = localizer[UiStrings.Slideshow];
        FindRequired<TextBlock>("SlideshowDurationLabel").Text =
            localizer[UiStrings.SlideshowSlideDuration];
        FindRequired<TextBlock>("SlideshowAtEndLabel").Text =
            localizer[UiStrings.SlideshowAtEnd];
        _slideshowStopAtEndOption.Content = localizer[UiStrings.SlideshowStopOnLast];
        _slideshowLoopOption.Content = localizer[UiStrings.SlideshowStartAgain];
        FindRequired<TextBlock>("MonitorColorManagementHeading").Text =
            localizer[UiStrings.ColorMonitorManagement];
        _monitorColorManagementOption.Content = localizer[UiStrings.ColorUseActiveMonitorProfile];
        FindRequired<TextBlock>("MonitorColorManagementExplanation").Text =
            localizer[UiStrings.ColorMonitorManagementExplanation];
        FindRequired<TextBlock>("BackgroundHeading").Text = localizer[UiStrings.StageBackground];
        FindRequired<TextBlock>("MatteHeading").Text = localizer[UiStrings.StageMatte];
        FindRequired<TextBlock>("ControlsHeading").Text =
            localizer[UiStrings.SettingsKeyboardShortcuts];
        FindRequired<TextBlock>("ControlsExplanation").Text =
            localizer[UiStrings.SettingsKeyboardShortcutsDescription];
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
        FindRequired<TextBlock>("MatteColorSourceLabel").Text =
            localizer[UiStrings.StageMatteColorSource];
        FindRequired<TextBlock>("PhotoSeparationLabel").Text =
            localizer[UiStrings.StagePhotoSeparation];
        _keepCurrentScaleOption.Content = localizer[UiStrings.SettingsKeepCurrentScale];
        _fitEachImageOption.Content = localizer[UiStrings.SettingsFitEachImage];
        _blackStageOption.Content = localizer[UiStrings.StageBlack];
        _neutralStageOption.Content = localizer[UiStrings.StageNeutral];
        _customStageOption.Content = localizer[UiStrings.StageCustom];
        _ambientStageOption.Content = localizer[UiStrings.StageAmbient];
        _averageStageOption.Content = localizer[UiStrings.StageAverage];
        _dominantStageOption.Content = localizer[UiStrings.StageDominant];
        _colorWashStageOption.Content = localizer[UiStrings.StageColorWash];
        _colorGradientStageOption.Content = localizer[UiStrings.StageColorGradient];
        _softGlowStageOption.Content = localizer[UiStrings.StageSoftGlow];
        _matteEnabledOption.Content = localizer[UiStrings.StageMatteEnabled];
        _ambientOptions.Header = localizer[UiStrings.StageAmbientOptions];
        _matteStyleOption.ItemsSource = Enum.GetValues<MatteStyle>()
            .Select(style => new ComboBoxItem { Content = LocalizeMatteStyle(style), Tag = style })
            .ToArray();
        _matteColorSourceOption.ItemsSource = Enum.GetValues<MatteColorSource>()
            .Select(source => new ComboBoxItem
            {
                Content = LocalizeMatteColorSource(source),
                Tag = source,
            })
            .ToArray();
        _photoSeparationOption.ItemsSource = Enum.GetValues<PhotoSeparationMode>()
            .Select(mode => new ComboBoxItem
            {
                Content = LocalizePhotoSeparation(mode),
                Tag = mode,
            })
            .ToArray();
        FindRequired<Button>("ResetShortcutsButton").Content = localizer[UiStrings.ShortcutReset];
        FindRequired<TextBlock>("VersionText").Text = string.Format(
            CultureInfo.CurrentUICulture,
            localizer[UiStrings.SettingsVersion],
            FoviumVersion.Display);
        FindRequired<TextBlock>("AboutProductDescription").Text =
            localizer[UiStrings.SettingsAboutProductDescription];

        CreateShortcutRows();
        ApplySettings(settings.Current);
        ApplyPhotoPresentationViewState();
        ApplySlideshowState();
        SubscribeEvents();
        _settings.SettingsChanged += OnSettingsChanged;
        _photoPresentationView.Changed += OnPhotoPresentationViewChanged;
        _slideshow.Changed += OnSlideshowChanged;
        Resized += OnWindowResized;
        Closed += OnClosed;
        KeyDown += OnShortcutCaptureKeyDown;
        AddHandler(
            InputElement.PointerPressedEvent,
            OnWindowPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        _initializing = false;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void SubscribeEvents()
    {
        _closeButton.Click += (_, _) => Close();
        _settingsNavigation.SelectionChanged += OnSettingsNavigationChanged;
        _languageOption.SelectionChanged += OnLanguageChanged;
        _rememberRecentPhotosOption.IsCheckedChanged += async (_, _) =>
        {
            if (!_initializing)
            {
                await _settings.SetRememberRecentPhotosAsync(
                    _rememberRecentPhotosOption.IsChecked == true);
            }
        };
        _keepCurrentScaleOption.IsCheckedChanged += OnKeepCurrentScaleChanged;
        _fitEachImageOption.IsCheckedChanged += OnFitEachImageChanged;
        _photoPresentationEnabledOption.IsCheckedChanged += (_, _) =>
        {
            if (!_initializing)
            {
                _photoPresentationView.SetEnabled(
                    _photoPresentationEnabledOption.IsChecked == true);
            }
        };
        _photoPresentationMarginSlider.ValueChanged += OnPhotoPresentationMarginChanged;
        _slideshowEnabledOption.IsCheckedChanged += (_, _) =>
        {
            if (!_initializing && (_slideshowEnabledOption.IsChecked == true) != _slideshow.IsRunning)
            {
                _slideshow.Toggle();
            }
        };
        _slideshowDurationSlider.ValueChanged += OnSlideshowDurationChanged;
        _slideshowStopAtEndOption.IsCheckedChanged += OnSlideshowEndBehaviorChanged;
        _slideshowLoopOption.IsCheckedChanged += OnSlideshowEndBehaviorChanged;
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
        _averageStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _averageStageOption,
            StageBackgroundMode.Average);
        _dominantStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _dominantStageOption,
            StageBackgroundMode.Dominant);
        _colorWashStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _colorWashStageOption,
            StageBackgroundMode.ColorWash);
        _colorGradientStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _colorGradientStageOption,
            StageBackgroundMode.ColorGradient);
        _softGlowStageOption.IsCheckedChanged += (_, _) => SetBackgroundIfChecked(
            _softGlowStageOption,
            StageBackgroundMode.SoftGlow);
        _matteEnabledOption.IsCheckedChanged += OnMatteEnabledChanged;
        _matteStyleOption.SelectionChanged += OnMatteStyleChanged;
        _matteColorSourceOption.SelectionChanged += OnMatteColorSourceChanged;
        _photoSeparationOption.SelectionChanged += OnPhotoSeparationChanged;
        _matteWidthSlider.ValueChanged += OnMatteWidthChanged;
        _brightnessSlider.ValueChanged += OnAmbientSliderChanged;
        _saturationSlider.ValueChanged += OnAmbientSliderChanged;
        _blurSlider.ValueChanged += OnAmbientSliderChanged;
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

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            SettingsWindowDragOrigin.MayInitiate(e.Source as Visual, this))
        {
            BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void AttachResizeHandle(string name, WindowEdge edge)
    {
        FindRequired<Border>(name).PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(edge, e);
                e.Handled = true;
            }
        };
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

            var groupContent = new StackPanel { Spacing = 9 };
            groupContent.Children.Add(new TextBlock
            {
                Text = _localizer[UiStrings.ForCommandGroup(group)],
                FontSize = 17,
                FontWeight = FontWeight.SemiBold,
            });

            var hintKey = group switch
            {
                ViewerCommandGroup.Presentation => UiStrings.CommandScopeHighlightHint,
                ViewerCommandGroup.Markup => UiStrings.CommandScopeMarkupHint,
                _ => null,
            };
            if (hintKey is not null)
            {
                groupContent.Children.Add(new TextBlock
                {
                    Text = _localizer[hintKey],
                    Foreground = new SolidColorBrush(Color.Parse("#AAA4B3")),
                    FontSize = 13,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                });
            }

            var rows = new StackPanel { Spacing = 5 };
            foreach (var definition in definitions)
            {
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 14,
                    MinHeight = 38,
                };
                row.Children.Add(new TextBlock
                {
                    Text = LocalizeCommand(definition.Command),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                });
                var button = new Button
                {
                    Tag = definition.Command,
                };
                button.Classes.Add("shortcut-key");
                Grid.SetColumn(button, 1);
                button.Click += OnShortcutButtonClick;
                row.Children.Add(button);
                rows.Children.Add(row);
                _shortcutButtons.Add(definition.Command, button);
            }

            groupContent.Children.Add(rows);
            var groupCard = new Border { Child = groupContent };
            groupCard.Classes.Add("settings-card");
            list.Children.Add(groupCard);
        }
    }

    private void OnSettingsNavigationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_settingsNavigation.SelectedItem is not ListBoxItem { Tag: SettingsSection selected })
        {
            return;
        }

        foreach (var (section, page) in _sectionPages)
        {
            page.IsVisible = section == selected;
            if (section == selected && page is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToHome();
            }
        }
    }

    private async void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && _languageOption.SelectedItem is ComboBoxItem { Tag: UiLanguage language })
        {
            await _settings.SetLanguageAsync(language);
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

    private async void OnPhotoPresentationMarginChanged(
        object? sender,
        RangeBaseValueChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var margin = Math.Round(_photoPresentationMarginSlider.Value * 2) / 2;
        UpdatePhotoPresentationMarginText(margin);
        await _settings.SetPhotoPresentationViewAsync(
            _settings.Current.PhotoPresentationView with { EdgeMarginPercent = margin });
    }

    private async void OnSlideshowDurationChanged(
        object? sender,
        RangeBaseValueChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var seconds = (int)Math.Round(_slideshowDurationSlider.Value);
        UpdateSlideshowDurationText(seconds);
        await _settings.SetSlideshowAsync(
            _settings.Current.Slideshow with { SlideDurationSeconds = seconds });
    }

    private async void OnSlideshowEndBehaviorChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        var behavior = _slideshowLoopOption.IsChecked == true
            ? SlideshowEndBehavior.Loop
            : SlideshowEndBehavior.StopAtEnd;
        await _settings.SetSlideshowAsync(
            _settings.Current.Slideshow with { EndBehavior = behavior });
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

    private async void OnMatteColorSourceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initializing &&
            _matteColorSourceOption.SelectedItem is ComboBoxItem { Tag: MatteColorSource source })
        {
            await _settings.SetStageAsync(_settings.Current.Stage with { MatteColorSource = source });
        }
    }

    private async void OnPhotoSeparationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initializing &&
            _photoSeparationOption.SelectedItem is ComboBoxItem { Tag: PhotoSeparationMode mode })
        {
            await _settings.SetStageAsync(_settings.Current.Stage with { PhotoSeparation = mode });
        }
    }

    private void OnPhotoPresentationViewChanged(object? sender, EventArgs e)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            ApplyPhotoPresentationViewState();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(ApplyPhotoPresentationViewState);
        }
    }

    private void ApplyPhotoPresentationViewState()
    {
        _initializing = true;
        _photoPresentationEnabledOption.IsChecked = _photoPresentationView.IsEnabled;
        _initializing = false;
    }

    private void OnSlideshowChanged(object? sender, EventArgs e)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            ApplySlideshowState();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(ApplySlideshowState);
        }
    }

    private void ApplySlideshowState()
    {
        _initializing = true;
        _slideshowEnabledOption.IsChecked = _slideshow.IsRunning;
        _slideshowEnabledOption.Content = _localizer[
            _slideshow.IsRunning ? UiStrings.SlideshowStop : UiStrings.SlideshowStart];
        _initializing = false;
    }

    private void ApplySettings(FoviumSettings settings)
    {
        _initializing = true;
        _languageOption.SelectedItem = _languageOption.ItemsSource?
            .OfType<ComboBoxItem>()
            .Single(item => item.Tag is UiLanguage language && language == settings.Language);
        _languageRestartPanel.IsVisible =
            LocaleResolver.Resolve(settings.Language, CultureInfo.CurrentUICulture) != _localizer.Locale;
        _rememberRecentPhotosOption.IsChecked = settings.Home.RememberRecentPhotos;
        _keepCurrentScaleOption.IsChecked =
            settings.ImageChangeViewPolicy == ImageChangeViewPolicy.KeepCurrentScale;
        _fitEachImageOption.IsChecked =
            settings.ImageChangeViewPolicy == ImageChangeViewPolicy.FitEachImage;
        _photoPresentationMarginSlider.Value = settings.PhotoPresentationView.EdgeMarginPercent;
        UpdatePhotoPresentationMarginText(settings.PhotoPresentationView.EdgeMarginPercent);
        _slideshowDurationSlider.Value = settings.Slideshow.SlideDurationSeconds;
        UpdateSlideshowDurationText(settings.Slideshow.SlideDurationSeconds);
        _slideshowStopAtEndOption.IsChecked =
            settings.Slideshow.EndBehavior == SlideshowEndBehavior.StopAtEnd;
        _slideshowLoopOption.IsChecked = settings.Slideshow.EndBehavior == SlideshowEndBehavior.Loop;
        _monitorColorManagementOption.IsChecked = settings.MonitorColorManagementEnabled;
        _blackStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Black;
        _neutralStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Neutral;
        _customStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Custom;
        _ambientStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Ambient;
        _averageStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Average;
        _dominantStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.Dominant;
        _colorWashStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.ColorWash;
        _colorGradientStageOption.IsChecked =
            settings.Stage.BackgroundMode == StageBackgroundMode.ColorGradient;
        _softGlowStageOption.IsChecked = settings.Stage.BackgroundMode == StageBackgroundMode.SoftGlow;
        _matteEnabledOption.IsChecked = settings.Stage.MatteEnabled;
        _matteStyleOption.SelectedItem = _matteStyleOption.ItemsSource?
            .OfType<ComboBoxItem>()
            .Single(item => item.Tag is MatteStyle style && style == settings.Stage.MatteStyle);
        _matteColorSourceOption.SelectedItem = _matteColorSourceOption.ItemsSource?
            .OfType<ComboBoxItem>()
            .Single(item => item.Tag is MatteColorSource source &&
                            source == settings.Stage.MatteColorSource);
        _photoSeparationOption.SelectedItem = _photoSeparationOption.ItemsSource?
            .OfType<ComboBoxItem>()
            .Single(item => item.Tag is PhotoSeparationMode mode &&
                            mode == settings.Stage.PhotoSeparation);
        _matteCustomColorPanel.IsEnabled =
            settings.Stage.MatteColorSource == MatteColorSource.Custom;
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

    private void UpdatePhotoPresentationMarginText(double margin) =>
        _photoPresentationMarginValue.Text = $"{margin:0.#}%";

    private void UpdateSlideshowDurationText(int seconds) =>
        _slideshowDurationValue.Text = $"{seconds} {_localizer[UiStrings.SlideshowSeconds]}";

    private string LocalizeMatteStyle(MatteStyle style) => _localizer[style switch
    {
        MatteStyle.Solid => UiStrings.StageMatteSolid,
        MatteStyle.Rounded => UiStrings.StageMatteRounded,
        MatteStyle.Soft => UiStrings.StageMatteSoft,
        MatteStyle.Angular => UiStrings.StageMatteAngular,
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    }];

    private string LocalizeMatteColorSource(MatteColorSource source) => _localizer[source switch
    {
        MatteColorSource.Custom => UiStrings.StageCustom,
        MatteColorSource.Average => UiStrings.StageAverage,
        MatteColorSource.Dominant => UiStrings.StageDominant,
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    }];

    private string LocalizePhotoSeparation(PhotoSeparationMode mode) => _localizer[mode switch
    {
        PhotoSeparationMode.None => UiStrings.StageSeparationNone,
        PhotoSeparationMode.HairlineAuto => UiStrings.StageHairlineAuto,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    }];

    private string LocalizeLanguage(UiLanguage language) => _localizer[language switch
    {
        UiLanguage.SystemDefault => UiStrings.SettingsLanguageSystemDefault,
        UiLanguage.English => UiStrings.SettingsLanguageEnglish,
        UiLanguage.Russian => UiStrings.SettingsLanguageRussian,
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    }];

    private string LocalizeSettingsSection(SettingsSection section) => _localizer[section switch
    {
        SettingsSection.General => UiStrings.SettingsGeneral,
        SettingsSection.Viewing => UiStrings.SettingsViewing,
        SettingsSection.Color => UiStrings.SettingsColor,
        SettingsSection.Stage => UiStrings.SettingsStage,
        SettingsSection.Presentation => UiStrings.SettingsPresentation,
        SettingsSection.Controls => UiStrings.SettingsControls,
        SettingsSection.About => UiStrings.SettingsAbout,
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
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
        PersistPendingWindowSize();
        _windowSizePersistenceTimer.Stop();
        _windowSizePersistenceTimer.Tick -= OnWindowSizePersistenceTimerTick;
        _settings.SettingsChanged -= OnSettingsChanged;
        _photoPresentationView.Changed -= OnPhotoPresentationViewChanged;
        _slideshow.Changed -= OnSlideshowChanged;
        Resized -= OnWindowResized;
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        if (e.Reason != WindowResizeReason.User || WindowState != WindowState.Normal)
        {
            return;
        }

        _pendingWindowSize = e.ClientSize;
        _windowSizePersistenceTimer.Stop();
        _windowSizePersistenceTimer.Start();
    }

    private void OnWindowSizePersistenceTimerTick(object? sender, EventArgs e)
    {
        _windowSizePersistenceTimer.Stop();
        PersistPendingWindowSize();
    }

    private void PersistPendingWindowSize()
    {
        if (_pendingWindowSize is not { } size)
        {
            return;
        }

        _pendingWindowSize = null;
        _ = _settings.SetSettingsWindowSizeAsync(new SettingsWindowSizeSettings
        {
            WidthDip = size.Width,
            HeightDip = size.Height,
        });
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

internal static class SettingsWindowDragOrigin
{
    public static bool MayInitiate(Visual? origin, Visual window)
    {
        ArgumentNullException.ThrowIfNull(window);

        for (var current = origin; current is not null; current = current.GetVisualParent())
        {
            if (current is Button or SelectingItemsControl or RangeBase or TextBox or Thumb ||
                current is Control control && control.Classes.Contains("resize-handle"))
            {
                return false;
            }

            if (ReferenceEquals(current, window))
            {
                return true;
            }
        }

        return false;
    }
}