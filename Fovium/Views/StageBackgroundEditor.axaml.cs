using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Fovium.Localization;
using Fovium.Stage;

namespace Fovium.Views;

internal sealed class StageSettingsChangedEventArgs(StageSettings settings) : EventArgs
{
    public StageSettings Settings { get; } = settings;
}

internal sealed partial class StageBackgroundEditor : UserControl
{
    private readonly ListBox _modeSelector;
    private readonly Dictionary<StageBackgroundMode, ListBoxItem> _modeItems;
    private readonly TextBlock _selectedModeTitle;
    private readonly TextBlock _selectedModeDescription;
    private readonly TextBlock _noModeOptions;
    private readonly Grid _customModeOptions;
    private readonly StackPanel _adjustmentModeOptions;
    private readonly Grid _blurRow;
    private readonly Slider _brightnessSlider;
    private readonly Slider _saturationSlider;
    private readonly Slider _blurSlider;
    private readonly TextBlock _brightnessValue;
    private readonly TextBlock _saturationValue;
    private readonly TextBlock _blurValue;
    private readonly ColorSwatchButton _customColorButton;
    private Localizer? _localizer;
    private StageSettings _stage = StageSettings.Default;
    private bool _updating;

    public StageBackgroundEditor()
    {
        InitializeComponent();
        _modeSelector = FindRequired<ListBox>("ModeSelector");
        _modeItems = new Dictionary<StageBackgroundMode, ListBoxItem>
        {
            [StageBackgroundMode.Black] = FindRequired<ListBoxItem>("BlackModeItem"),
            [StageBackgroundMode.Neutral] = FindRequired<ListBoxItem>("NeutralModeItem"),
            [StageBackgroundMode.Average] = FindRequired<ListBoxItem>("AverageModeItem"),
            [StageBackgroundMode.Dominant] = FindRequired<ListBoxItem>("DominantModeItem"),
            [StageBackgroundMode.ColorWash] = FindRequired<ListBoxItem>("ColorWashModeItem"),
            [StageBackgroundMode.ColorGradient] = FindRequired<ListBoxItem>("ColorGradientModeItem"),
            [StageBackgroundMode.SoftGlow] = FindRequired<ListBoxItem>("SoftGlowModeItem"),
            [StageBackgroundMode.Custom] = FindRequired<ListBoxItem>("CustomModeItem"),
            [StageBackgroundMode.Ambient] = FindRequired<ListBoxItem>("AmbientModeItem"),
        };
        _selectedModeTitle = FindRequired<TextBlock>("SelectedModeTitle");
        _selectedModeDescription = FindRequired<TextBlock>("SelectedModeDescription");
        _noModeOptions = FindRequired<TextBlock>("NoModeOptions");
        _customModeOptions = FindRequired<Grid>("CustomModeOptions");
        _adjustmentModeOptions = FindRequired<StackPanel>("AdjustmentModeOptions");
        _blurRow = FindRequired<Grid>("BlurRow");
        _brightnessSlider = FindRequired<Slider>("BrightnessSlider");
        _saturationSlider = FindRequired<Slider>("SaturationSlider");
        _blurSlider = FindRequired<Slider>("BlurSlider");
        _brightnessValue = FindRequired<TextBlock>("BrightnessValue");
        _saturationValue = FindRequired<TextBlock>("SaturationValue");
        _blurValue = FindRequired<TextBlock>("BlurValue");
        _customColorButton = FindRequired<ColorSwatchButton>("CustomColorButton");

        foreach (var (mode, item) in _modeItems)
        {
            item.Tag = mode;
        }

        _modeSelector.SelectionChanged += OnModeSelectionChanged;
        _brightnessSlider.ValueChanged += OnAdjustmentChanged;
        _saturationSlider.ValueChanged += OnAdjustmentChanged;
        _blurSlider.ValueChanged += OnAdjustmentChanged;
        _customColorButton.Click += (_, _) => CustomColorRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<StageSettingsChangedEventArgs>? StageChanged;

    public event EventHandler? CustomColorRequested;

    public void Configure(Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
        foreach (var (mode, item) in _modeItems)
        {
            item.Content = LocalizeMode(mode);
        }

        FindRequired<TextBlock>("BrightnessLabel").Text = localizer[UiStrings.StageBrightness];
        FindRequired<TextBlock>("SaturationLabel").Text = localizer[UiStrings.StageSaturation];
        FindRequired<TextBlock>("BlurLabel").Text = localizer[UiStrings.StageBlur];
        FindRequired<TextBlock>("CustomColorLabel").Text = localizer[UiStrings.StageCustomColor];
        _noModeOptions.Text = localizer[UiStrings.StageNoAdjustments];
        var colorName = localizer[UiStrings.StageCustomColor];
        AutomationProperties.SetName(_customColorButton, colorName);
        ToolTip.SetTip(_customColorButton, colorName);
        UpdateContext();
    }

    public void Apply(StageSettings stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _stage = stage.Normalize();
        _updating = true;
        _modeSelector.SelectedItem = _modeItems[_stage.BackgroundMode];
        _customColorButton.SwatchBrush = CreateBrush(_stage.CustomBackgroundColor);
        UpdateContext();
        _updating = false;
        Dispatcher.UIThread.Post(
            () => _modeItems[_stage.BackgroundMode].BringIntoView(),
            DispatcherPriority.Loaded);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updating || _modeSelector.SelectedItem is not ListBoxItem { Tag: StageBackgroundMode mode })
        {
            return;
        }

        _stage = (_stage with { BackgroundMode = mode }).Normalize();
        UpdateContext();
        StageChanged?.Invoke(this, new StageSettingsChangedEventArgs(_stage));
    }

    private void UpdateContext()
    {
        var mode = _stage.BackgroundMode;
        _selectedModeTitle.Text = _localizer is null ? string.Empty : LocalizeMode(mode);
        var state = StageBackgroundEditorModeState.Resolve(mode);
        _noModeOptions.IsVisible = state.ShowNoAdjustments;
        _customModeOptions.IsVisible = state.ShowCustomColor;
        _adjustmentModeOptions.IsVisible = state.ShowColorAdjustment;
        _blurRow.IsVisible = state.ShowBlur;
        _selectedModeDescription.IsVisible = state.ShowCustomColor || state.ShowColorAdjustment;
        if (_localizer is not null)
        {
            _selectedModeDescription.Text = _localizer[state.ShowCustomColor
                ? UiStrings.StageCustomColorDescription
                : state.ShowColorAdjustment
                    ? UiStrings.StageAdjustmentDescription
                    : UiStrings.StageNoAdjustments];
        }

        if (!state.ShowColorAdjustment)
        {
            return;
        }

        _updating = true;
        var adjustment = _stage.BackgroundAdjustments.For(mode);
        var ambient = _stage.BackgroundAdjustments.Ambient;
        _brightnessSlider.Minimum = mode == StageBackgroundMode.Ambient
            ? StageDefaults.AmbientBrightnessMinimum * 100
            : StageDefaults.BackgroundBrightnessMinimum * 100;
        _brightnessSlider.Maximum = mode == StageBackgroundMode.Ambient
            ? StageDefaults.AmbientBrightnessMaximum * 100
            : StageDefaults.BackgroundBrightnessMaximum * 100;
        _saturationSlider.Minimum = mode == StageBackgroundMode.Ambient
            ? StageDefaults.AmbientSaturationMinimum * 100
            : StageDefaults.BackgroundSaturationMinimum * 100;
        _saturationSlider.Maximum = mode == StageBackgroundMode.Ambient
            ? StageDefaults.AmbientSaturationMaximum * 100
            : StageDefaults.BackgroundSaturationMaximum * 100;
        _brightnessSlider.Value = adjustment.Brightness * 100;
        _saturationSlider.Value = adjustment.Saturation * 100;
        _blurSlider.Value = ambient.Blur;
        UpdateValueText(adjustment, ambient.Blur);
        _updating = false;
    }

    private void OnAdjustmentChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        var mode = _stage.BackgroundMode;
        var adjustment = new StageColorAdjustment
        {
            Brightness = _brightnessSlider.Value / 100,
            Saturation = _saturationSlider.Value / 100,
        };
        var adjustments = _stage.BackgroundAdjustments.With(mode, adjustment);
        if (mode == StageBackgroundMode.Ambient)
        {
            adjustments = adjustments with
            {
                Ambient = adjustments.Ambient with { Blur = Math.Round(_blurSlider.Value) },
            };
        }

        _stage = (_stage with { BackgroundAdjustments = adjustments }).Normalize();
        var current = _stage.BackgroundAdjustments.For(mode);
        UpdateValueText(current, _stage.BackgroundAdjustments.Ambient.Blur);
        StageChanged?.Invoke(this, new StageSettingsChangedEventArgs(_stage));
    }

    private void UpdateValueText(StageColorAdjustment adjustment, double blur)
    {
        _brightnessValue.Text = adjustment.Brightness.ToString("P0", CultureInfo.CurrentUICulture);
        _saturationValue.Text = adjustment.Saturation.ToString("P0", CultureInfo.CurrentUICulture);
        _blurValue.Text = blur.ToString("0", CultureInfo.CurrentUICulture);
    }

    private string LocalizeMode(StageBackgroundMode mode) => _localizer![mode switch
    {
        StageBackgroundMode.Black => UiStrings.StageBlack,
        StageBackgroundMode.Neutral => UiStrings.StageNeutral,
        StageBackgroundMode.Custom => UiStrings.StageCustom,
        StageBackgroundMode.Ambient => UiStrings.StageAmbient,
        StageBackgroundMode.Average => UiStrings.StageAverage,
        StageBackgroundMode.Dominant => UiStrings.StageDominant,
        StageBackgroundMode.ColorWash => UiStrings.StageColorWash,
        StageBackgroundMode.ColorGradient => UiStrings.StageColorGradient,
        StageBackgroundMode.SoftGlow => UiStrings.StageSoftGlow,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    }];

    private static IBrush CreateBrush(StageColor color) =>
        new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Stage background control is missing: {name}.");
}