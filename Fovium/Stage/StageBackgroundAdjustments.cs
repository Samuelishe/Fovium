namespace Fovium.Stage;

internal sealed record StageColorAdjustment
{
    public double Brightness { get; init; } = StageDefaults.BackgroundBrightness;

    public double Saturation { get; init; } = StageDefaults.BackgroundSaturation;

    public static StageColorAdjustment Identity { get; } = new();

    public bool IsIdentity =>
        Brightness.Equals(StageDefaults.BackgroundBrightness) &&
        Saturation.Equals(StageDefaults.BackgroundSaturation);

    public StageColorAdjustment Normalize() => this with
    {
        Brightness = NormalizeFinite(
            Brightness,
            StageDefaults.BackgroundBrightness,
            StageDefaults.BackgroundBrightnessMinimum,
            StageDefaults.BackgroundBrightnessMaximum),
        Saturation = NormalizeFinite(
            Saturation,
            StageDefaults.BackgroundSaturation,
            StageDefaults.BackgroundSaturationMinimum,
            StageDefaults.BackgroundSaturationMaximum),
    };

    private static double NormalizeFinite(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}

internal sealed record AmbientStageAdjustment
{
    public double Brightness { get; init; } = StageDefaults.AmbientBrightness;

    public double Saturation { get; init; } = StageDefaults.AmbientSaturation;

    public double Blur { get; init; } = StageDefaults.AmbientBlurSigmaPixels;

    public static AmbientStageAdjustment Default { get; } = new();

    public StageColorAdjustment ColorAdjustment => new()
    {
        Brightness = Brightness,
        Saturation = Saturation,
    };

    public AmbientStageAdjustment Normalize() => this with
    {
        Brightness = NormalizeFinite(
            Brightness,
            StageDefaults.AmbientBrightness,
            StageDefaults.AmbientBrightnessMinimum,
            StageDefaults.AmbientBrightnessMaximum),
        Saturation = NormalizeFinite(
            Saturation,
            StageDefaults.AmbientSaturation,
            StageDefaults.AmbientSaturationMinimum,
            StageDefaults.AmbientSaturationMaximum),
        Blur = NormalizeFinite(
            Blur,
            StageDefaults.AmbientBlurSigmaPixels,
            StageDefaults.AmbientBlurMinimum,
            StageDefaults.AmbientBlurMaximum),
    };

    private static double NormalizeFinite(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}

internal sealed record StageBackgroundAdjustments
{
    public StageColorAdjustment Average { get; init; } = StageColorAdjustment.Identity;

    public StageColorAdjustment Dominant { get; init; } = StageColorAdjustment.Identity;

    public StageColorAdjustment ColorWash { get; init; } = StageColorAdjustment.Identity;

    public StageColorAdjustment ColorGradient { get; init; } = StageColorAdjustment.Identity;

    public StageColorAdjustment SoftGlow { get; init; } = StageColorAdjustment.Identity;

    public AmbientStageAdjustment Ambient { get; init; } = AmbientStageAdjustment.Default;

    public static StageBackgroundAdjustments Default { get; } = new();

    public StageBackgroundAdjustments Normalize() => this with
    {
        Average = (Average ?? StageColorAdjustment.Identity).Normalize(),
        Dominant = (Dominant ?? StageColorAdjustment.Identity).Normalize(),
        ColorWash = (ColorWash ?? StageColorAdjustment.Identity).Normalize(),
        ColorGradient = (ColorGradient ?? StageColorAdjustment.Identity).Normalize(),
        SoftGlow = (SoftGlow ?? StageColorAdjustment.Identity).Normalize(),
        Ambient = (Ambient ?? AmbientStageAdjustment.Default).Normalize(),
    };

    public StageColorAdjustment For(StageBackgroundMode mode) => mode switch
    {
        StageBackgroundMode.Average => Average,
        StageBackgroundMode.Dominant => Dominant,
        StageBackgroundMode.ColorWash => ColorWash,
        StageBackgroundMode.ColorGradient => ColorGradient,
        StageBackgroundMode.SoftGlow => SoftGlow,
        StageBackgroundMode.Ambient => Ambient.ColorAdjustment,
        _ => StageColorAdjustment.Identity,
    };

    public StageBackgroundAdjustments With(
        StageBackgroundMode mode,
        StageColorAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        if (mode == StageBackgroundMode.Ambient)
        {
            return this with
            {
                Ambient = (Ambient with
                {
                    Brightness = adjustment.Brightness,
                    Saturation = adjustment.Saturation,
                }).Normalize(),
            };
        }

        var normalized = adjustment.Normalize();
        return mode switch
        {
            StageBackgroundMode.Average => this with { Average = normalized },
            StageBackgroundMode.Dominant => this with { Dominant = normalized },
            StageBackgroundMode.ColorWash => this with { ColorWash = normalized },
            StageBackgroundMode.ColorGradient => this with { ColorGradient = normalized },
            StageBackgroundMode.SoftGlow => this with { SoftGlow = normalized },
            _ => this,
        };
    }
}