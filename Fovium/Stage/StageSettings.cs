namespace Fovium.Stage;

internal sealed record StageSettings
{
    public StageBackgroundMode BackgroundMode { get; init; } = StageBackgroundMode.Black;

    public bool MatteEnabled { get; init; }

    public StageColor CustomBackgroundColor { get; init; } = StageDefaults.CustomBackgroundColor;

    public StageColor MatteColor { get; init; } = StageDefaults.MatteColor;

    public MatteStyle MatteStyle { get; init; } = StageDefaults.MatteStyle;

    public double MatteWidthPhysicalPixels { get; init; } = StageDefaults.MatteWidthPhysicalPixels;

    public double AmbientBrightness { get; init; } = StageDefaults.AmbientBrightness;

    public double AmbientSaturation { get; init; } = StageDefaults.AmbientSaturation;

    public double AmbientBlur { get; init; } = StageDefaults.AmbientBlurSigmaPixels;

    public static StageSettings Default { get; } = new();

    public StageSettings Normalize() => this with
    {
        BackgroundMode = Enum.IsDefined(BackgroundMode)
            ? BackgroundMode
            : StageBackgroundMode.Black,
        MatteStyle = Enum.IsDefined(MatteStyle)
            ? MatteStyle
            : StageDefaults.MatteStyle,
        MatteWidthPhysicalPixels = NormalizeFinite(
            MatteWidthPhysicalPixels,
            StageDefaults.MatteWidthPhysicalPixels,
            StageDefaults.MatteWidthMinimumPhysicalPixels,
            StageDefaults.MatteWidthMaximumPhysicalPixels),
        AmbientBrightness = NormalizeFinite(
            AmbientBrightness,
            StageDefaults.AmbientBrightness,
            StageDefaults.AmbientBrightnessMinimum,
            StageDefaults.AmbientBrightnessMaximum),
        AmbientSaturation = NormalizeFinite(
            AmbientSaturation,
            StageDefaults.AmbientSaturation,
            StageDefaults.AmbientSaturationMinimum,
            StageDefaults.AmbientSaturationMaximum),
        AmbientBlur = NormalizeFinite(
            AmbientBlur,
            StageDefaults.AmbientBlurSigmaPixels,
            StageDefaults.AmbientBlurMinimum,
            StageDefaults.AmbientBlurMaximum),
    };

    private static double NormalizeFinite(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
