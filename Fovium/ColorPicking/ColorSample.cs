namespace Fovium.ColorPicking;

internal enum ColorSampleAccuracy
{
    Exact,
    Approximate,
}

internal sealed record ColorSample(
    byte Red,
    byte Green,
    byte Blue,
    byte Alpha,
    string ColorNameStableId,
    string? CanonicalName,
    ColorSampleAccuracy Accuracy)
{
    public bool IsTransparent => Alpha == 0;

    public string Hex => Alpha == byte.MaxValue
        ? $"#{Red:X2}{Green:X2}{Blue:X2}"
        : $"#{Red:X2}{Green:X2}{Blue:X2}{Alpha:X2}";

    public string Components => Alpha == byte.MaxValue
        ? $"RGB {Red}, {Green}, {Blue}"
        : $"RGBA {Red}, {Green}, {Blue}, {Alpha}";
}
