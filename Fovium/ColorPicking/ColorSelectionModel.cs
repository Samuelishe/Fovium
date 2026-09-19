using System.Globalization;
using Fovium.Stage;

namespace Fovium.ColorPicking;

internal readonly record struct HsvColor(double HueDegrees, double Saturation, double Value)
{
    public HsvColor Normalize() => new(
        ColorSelectionModel.WrapHue(HueDegrees),
        Math.Clamp(double.IsFinite(Saturation) ? Saturation : 0, 0, 1),
        Math.Clamp(double.IsFinite(Value) ? Value : 0, 0, 1));
}

internal enum ColorEditCompletion
{
    Cancel,
    Accept,
}

internal sealed class ColorSelectionModel
{
    public ColorSelectionModel(StageColor initial)
    {
        OriginalColor = initial;
        SetColor(initial, publish: false);
    }

    public event EventHandler? Changed;

    public StageColor OriginalColor { get; }

    public StageColor CurrentColor { get; private set; }

    public HsvColor Hsv { get; private set; }

    public string Hex => CurrentColor.ToHex();

    public void SetHsv(double hueDegrees, double saturation, double value)
    {
        var hsv = new HsvColor(hueDegrees, saturation, value).Normalize();
        SetColor(FromHsv(hsv), hsv, publish: true);
    }

    public bool TrySetHex(string? text)
    {
        if (!StageColor.TryParse(text?.Trim(), out var color))
        {
            return false;
        }

        SetColor(color, publish: true);
        return true;
    }

    public bool TrySetRgb(string? red, string? green, string? blue) =>
        TryParseByte(red, out var r) &&
        TryParseByte(green, out var g) &&
        TryParseByte(blue, out var b) &&
        SetParsedColor(new StageColor(r, g, b));

    public bool TrySetHsv(string? hue, string? saturationPercent, string? valuePercent)
    {
        if (!double.TryParse(hue, NumberStyles.Float, CultureInfo.InvariantCulture, out var h) ||
            !double.TryParse(saturationPercent, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ||
            !double.TryParse(valuePercent, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ||
            !double.IsFinite(h) || s is < 0 or > 100 || v is < 0 or > 100)
        {
            return false;
        }

        SetHsv(h, s / 100, v / 100);
        return true;
    }

    public StageColor Resolve(ColorEditCompletion completion) =>
        completion == ColorEditCompletion.Accept ? CurrentColor : OriginalColor;

    public static HsvColor ToHsv(StageColor color)
    {
        var r = color.Red / 255d;
        var g = color.Green / 255d;
        var b = color.Blue / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var hue = delta == 0
            ? 0
            : max == r
                ? 60 * (((g - b) / delta) % 6)
                : max == g
                    ? 60 * (((b - r) / delta) + 2)
                    : 60 * (((r - g) / delta) + 4);
        return new HsvColor(
            WrapHue(hue),
            max == 0 ? 0 : delta / max,
            max);
    }

    public static StageColor FromHsv(HsvColor source)
    {
        var hsv = source.Normalize();
        var chroma = hsv.Value * hsv.Saturation;
        var sector = hsv.HueDegrees / 60;
        var x = chroma * (1 - Math.Abs((sector % 2) - 1));
        var (r1, g1, b1) = sector switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        var match = hsv.Value - chroma;
        return new StageColor(ToByte(r1 + match), ToByte(g1 + match), ToByte(b1 + match));
    }

    internal static double WrapHue(double hueDegrees)
    {
        if (!double.IsFinite(hueDegrees))
        {
            return 0;
        }

        var wrapped = hueDegrees % 360;
        return wrapped < 0 ? wrapped + 360 : wrapped;
    }

    private bool SetParsedColor(StageColor color)
    {
        SetColor(color, publish: true);
        return true;
    }

    private void SetColor(StageColor color, bool publish) =>
        SetColor(color, ToHsv(color), publish);

    private void SetColor(StageColor color, HsvColor hsv, bool publish)
    {
        CurrentColor = color;
        Hsv = hsv;
        if (publish)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool TryParseByte(string? text, out byte value) =>
        byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static byte ToByte(double channel) =>
        (byte)Math.Clamp(
            Math.Round(channel * 255, MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);
}