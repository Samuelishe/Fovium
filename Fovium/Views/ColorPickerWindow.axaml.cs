using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Fovium.ColorPicking;
using Fovium.Localization;
using Fovium.Stage;

namespace Fovium.Views;

internal sealed class StageColorChangedEventArgs(StageColor color) : EventArgs
{
    public StageColor Color { get; } = color;
}

internal enum ColorRepresentation
{
    Rgb,
    Hsv,
    Hex,
}

internal sealed partial class ColorPickerWindow : Window
{
    private readonly ColorSelectionModel _model;
    private readonly ColorWheelControl _wheel;
    private readonly ColorValueStripControl _valueStrip;
    private readonly Border _currentPreview;
    private readonly TextBlock _validationText;
    private readonly TextBox _redValue;
    private readonly TextBox _greenValue;
    private readonly TextBox _blueValue;
    private readonly TextBox _hueValue;
    private readonly TextBox _saturationValue;
    private readonly TextBox _brightnessValue;
    private readonly TextBox _hexValue;
    private readonly Grid _rgbPanel;
    private readonly Grid _hsvPanel;
    private readonly Grid _hexPanel;
    private readonly Button _rgbModeButton;
    private readonly Button _hsvModeButton;
    private readonly Button _hexModeButton;
    private readonly string _invalidColorText;
    private bool _updatingInputs;

    public ColorPickerWindow(StageColor initial, Localizer localizer, string title)
    {
        InitializeComponent();
        _model = new ColorSelectionModel(initial);
        _wheel = FindRequired<ColorWheelControl>("ColorWheel");
        _valueStrip = FindRequired<ColorValueStripControl>("ValueStrip");
        _currentPreview = FindRequired<Border>("CurrentPreview");
        _validationText = FindRequired<TextBlock>("ValidationText");
        _redValue = FindRequired<TextBox>("RedValue");
        _greenValue = FindRequired<TextBox>("GreenValue");
        _blueValue = FindRequired<TextBox>("BlueValue");
        _hueValue = FindRequired<TextBox>("HueValue");
        _saturationValue = FindRequired<TextBox>("SaturationValue");
        _brightnessValue = FindRequired<TextBox>("BrightnessValue");
        _hexValue = FindRequired<TextBox>("HexValue");
        _rgbPanel = FindRequired<Grid>("RgbPanel");
        _hsvPanel = FindRequired<Grid>("HsvPanel");
        _hexPanel = FindRequired<Grid>("HexPanel");
        _rgbModeButton = FindRequired<Button>("RgbModeButton");
        _hsvModeButton = FindRequired<Button>("HsvModeButton");
        _hexModeButton = FindRequired<Button>("HexModeButton");
        _invalidColorText = localizer[UiStrings.ColorInvalid];

        FindRequired<TextBlock>("PickerTitle").Text = title;
        FindRequired<TextBlock>("PickerDescription").Text = localizer[UiStrings.ColorPickerDialogDescription];
        FindRequired<TextBlock>("HexLabel").Text = localizer[UiStrings.ColorHex];
        FindRequired<TextBlock>("OriginalLabel").Text = localizer[UiStrings.ColorOriginal];
        FindRequired<TextBlock>("CurrentLabel").Text = localizer[UiStrings.ColorCurrent];
        FindRequired<Border>("OriginalPreview").Background = CreateBrush(initial);

        var close = FindRequired<Button>("CloseButton");
        AutomationProperties.SetName(close, localizer[UiStrings.CommonClose]);
        close.Click += (_, _) => Close(false);
        var cancel = FindRequired<Button>("CancelButton");
        cancel.Content = localizer[UiStrings.CommonCancel];
        cancel.Click += (_, _) => Close(false);
        var ok = FindRequired<Button>("OkButton");
        ok.Content = localizer[UiStrings.CommonOk];
        ok.Click += (_, _) =>
        {
            if (ApplyVisibleInputs())
            {
                Close(true);
            }
        };

        AutomationProperties.SetName(_wheel, localizer[UiStrings.ColorWheelAutomation]);
        AutomationProperties.SetName(_valueStrip, localizer[UiStrings.ColorValueAutomation]);
        _wheel.SelectionChanged += (_, hsv) => _model.SetHsv(hsv.HueDegrees, hsv.Saturation, hsv.Value);
        _valueStrip.ValueChanged += (_, value) =>
            _model.SetHsv(_model.Hsv.HueDegrees, _model.Hsv.Saturation, value);
        _model.Changed += (_, _) =>
        {
            ApplyModelToUi();
            ColorChanged?.Invoke(this, new StageColorChangedEventArgs(_model.CurrentColor));
        };

        _rgbModeButton.Click += (_, _) => SetRepresentation(ColorRepresentation.Rgb);
        _hsvModeButton.Click += (_, _) => SetRepresentation(ColorRepresentation.Hsv);
        _hexModeButton.Click += (_, _) => SetRepresentation(ColorRepresentation.Hex);
        AttachInputCommit(_redValue, ApplyRgbInputs);
        AttachInputCommit(_greenValue, ApplyRgbInputs);
        AttachInputCommit(_blueValue, ApplyRgbInputs);
        AttachInputCommit(_hueValue, ApplyHsvInputs);
        AttachInputCommit(_saturationValue, ApplyHsvInputs);
        AttachInputCommit(_brightnessValue, ApplyHsvInputs);
        AttachInputCommit(_hexValue, ApplyHexInput);
        KeyDown += OnWindowKeyDown;
        PointerPressed += OnWindowPointerPressed;

        ApplyModelToUi();
        SetRepresentation(ColorRepresentation.Hsv);
    }

    public event EventHandler<StageColorChangedEventArgs>? ColorChanged;

    public StageColor CurrentColor => _model.CurrentColor;

    public StageColor Resolve(bool accepted) => _model.Resolve(
        accepted ? ColorEditCompletion.Accept : ColorEditCompletion.Cancel);

    private ColorRepresentation Representation { get; set; }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ApplyModelToUi()
    {
        _updatingInputs = true;
        var color = _model.CurrentColor;
        var hsv = _model.Hsv;
        _wheel.SetSelection(hsv);
        _valueStrip.SetSelection(hsv);
        _currentPreview.Background = CreateBrush(color);
        _redValue.Text = color.Red.ToString(CultureInfo.InvariantCulture);
        _greenValue.Text = color.Green.ToString(CultureInfo.InvariantCulture);
        _blueValue.Text = color.Blue.ToString(CultureInfo.InvariantCulture);
        _hueValue.Text = hsv.HueDegrees.ToString("0.##", CultureInfo.InvariantCulture);
        _saturationValue.Text = (hsv.Saturation * 100).ToString("0.##", CultureInfo.InvariantCulture);
        _brightnessValue.Text = (hsv.Value * 100).ToString("0.##", CultureInfo.InvariantCulture);
        _hexValue.Text = _model.Hex;
        _validationText.Text = string.Empty;
        _updatingInputs = false;
    }

    private void SetRepresentation(ColorRepresentation representation)
    {
        if (!ApplyVisibleInputs())
        {
            return;
        }

        Representation = representation;
        _rgbPanel.IsVisible = representation == ColorRepresentation.Rgb;
        _hsvPanel.IsVisible = representation == ColorRepresentation.Hsv;
        _hexPanel.IsVisible = representation == ColorRepresentation.Hex;
        _rgbModeButton.Classes.Set("selected", representation == ColorRepresentation.Rgb);
        _hsvModeButton.Classes.Set("selected", representation == ColorRepresentation.Hsv);
        _hexModeButton.Classes.Set("selected", representation == ColorRepresentation.Hex);
    }

    private bool ApplyVisibleInputs() => Representation switch
    {
        ColorRepresentation.Rgb => ApplyRgbInputs(),
        ColorRepresentation.Hsv => ApplyHsvInputs(),
        ColorRepresentation.Hex => ApplyHexInput(),
        _ => true,
    };

    private bool ApplyRgbInputs() => ApplyInput(() =>
        _model.TrySetRgb(_redValue.Text, _greenValue.Text, _blueValue.Text));

    private bool ApplyHsvInputs() => ApplyInput(() =>
        _model.TrySetHsv(_hueValue.Text, _saturationValue.Text, _brightnessValue.Text));

    private bool ApplyHexInput() => ApplyInput(() => _model.TrySetHex(_hexValue.Text));

    private bool ApplyInput(Func<bool> apply)
    {
        if (_updatingInputs)
        {
            return true;
        }

        if (apply())
        {
            _validationText.Text = string.Empty;
            return true;
        }

        _validationText.Text = _invalidColorText;
        return false;
    }

    private static void AttachInputCommit(TextBox textBox, Func<bool> apply)
    {
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                apply();
            }
        };
        textBox.LostFocus += (_, _) => apply();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close(false);
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            SettingsWindowDragOrigin.MayInitiate(e.Source as Avalonia.Visual, this))
        {
            BeginMoveDrag(e);
        }
    }

    private static IBrush CreateBrush(StageColor color) =>
        new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Color picker control is missing: {name}.");
}