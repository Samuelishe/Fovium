namespace Fovium.Settings;

internal sealed record SettingsWindowSizeSettings
{
    public const double DefaultWidthDip = 920;
    public const double DefaultHeightDip = 680;
    public const double MinimumWidthDip = 560;
    public const double MinimumHeightDip = 440;
    public const double MaximumWidthDip = 2400;
    public const double MaximumHeightDip = 1600;

    public double WidthDip { get; init; } = DefaultWidthDip;

    public double HeightDip { get; init; } = DefaultHeightDip;

    public static SettingsWindowSizeSettings Default { get; } = new();

    public SettingsWindowSizeSettings Normalize() => this with
    {
        WidthDip = IsValid(WidthDip, MinimumWidthDip, MaximumWidthDip)
            ? WidthDip
            : DefaultWidthDip,
        HeightDip = IsValid(HeightDip, MinimumHeightDip, MaximumHeightDip)
            ? HeightDip
            : DefaultHeightDip,
    };

    private static bool IsValid(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;
}
