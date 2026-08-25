using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Fovium.Localization;
using Fovium.Stage;

namespace Fovium.Views;

internal sealed class StageColorChangedEventArgs(StageColor color) : EventArgs
{
    public StageColor Color { get; } = color;
}

internal sealed partial class ColorEditorWindow : Window
{
    private readonly Border _preview;
    private readonly TextBox _hexValue;
    private readonly TextBlock _redValue;
    private readonly TextBlock _greenValue;
    private readonly TextBlock _blueValue;
    private readonly TextBlock _validationText;
    private readonly Slider _redSlider;
    private readonly Slider _greenSlider;
    private readonly Slider _blueSlider;
    private readonly string _invalidColorText;
    private bool _initializing = true;

    public ColorEditorWindow(StageColor initial, Localizer localizer, string title)
    {
        InitializeComponent();
        Title = title;
        _preview = FindRequired<Border>("Preview");
        _hexValue = FindRequired<TextBox>("HexValue");
        _redValue = FindRequired<TextBlock>("RedValue");
        _greenValue = FindRequired<TextBlock>("GreenValue");
        _blueValue = FindRequired<TextBlock>("BlueValue");
        _validationText = FindRequired<TextBlock>("ValidationText");
        _redSlider = FindRequired<Slider>("RedSlider");
        _greenSlider = FindRequired<Slider>("GreenSlider");
        _blueSlider = FindRequired<Slider>("BlueSlider");
        _invalidColorText = localizer[UiStrings.ColorInvalid];
        FindRequired<TextBlock>("HexLabel").Text = localizer[UiStrings.ColorHex];
        var cancel = FindRequired<Button>("CancelButton");
        cancel.Content = localizer[UiStrings.CommonCancel];
        cancel.Click += (_, _) => Close(false);
        var ok = FindRequired<Button>("OkButton");
        ok.Content = localizer[UiStrings.CommonOk];
        ok.Click += (_, _) =>
        {
            if (ApplyHexValue())
            {
                Close(true);
            }
        };

        SetColor(initial, publish: false);
        _redSlider.ValueChanged += OnSliderValueChanged;
        _greenSlider.ValueChanged += OnSliderValueChanged;
        _blueSlider.ValueChanged += OnSliderValueChanged;
        _hexValue.KeyDown += OnHexKeyDown;
        _hexValue.LostFocus += (_, _) => ApplyHexValue();
        _initializing = false;
    }

    public event EventHandler<StageColorChangedEventArgs>? ColorChanged;

    public StageColor CurrentColor { get; private set; }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        SetColor(new StageColor(
            (byte)Math.Round(_redSlider.Value),
            (byte)Math.Round(_greenSlider.Value),
            (byte)Math.Round(_blueSlider.Value)), publish: true);
    }

    private void OnHexKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplyHexValue();
        }
    }

    private bool ApplyHexValue()
    {
        if (!StageColor.TryParse(_hexValue.Text, out var color))
        {
            _validationText.Text = _invalidColorText;
            return false;
        }

        _validationText.Text = string.Empty;
        SetColor(color, publish: true);
        return true;
    }

    private void SetColor(StageColor color, bool publish)
    {
        _initializing = true;
        CurrentColor = color;
        _redSlider.Value = color.Red;
        _greenSlider.Value = color.Green;
        _blueSlider.Value = color.Blue;
        _redValue.Text = color.Red.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _greenValue.Text = color.Green.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _blueValue.Text = color.Blue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _hexValue.Text = color.ToHex();
        _preview.Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        _initializing = false;
        if (publish)
        {
            ColorChanged?.Invoke(this, new StageColorChangedEventArgs(color));
        }
    }

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Color editor control is missing: {name}.");
}
