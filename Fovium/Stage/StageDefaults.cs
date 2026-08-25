namespace Fovium.Stage;

internal static class StageDefaults
{
    // Presentation defaults selected through R3-F2; these are not calibrated reference values.
    public static StageColor BlackColor { get; } = new(0x00, 0x00, 0x00);

    public static StageColor NeutralColor { get; } = new(0x50, 0x50, 0x50);

    public static StageColor CustomBackgroundColor { get; } = new(0x20, 0x20, 0x20);

    public static StageColor MatteColor { get; } = new(0x20, 0x20, 0x20);

    public const int AmbientLongEdgePixels = 384;

    public const double AmbientBrightness = 0.65;

    public const double AmbientBrightnessMinimum = 0.30;

    public const double AmbientBrightnessMaximum = 1.00;

    public const double AmbientSaturation = 0.85;

    public const double AmbientSaturationMinimum = 0.00;

    public const double AmbientSaturationMaximum = 1.25;

    public const double AmbientBlurSigmaPixels = 18;

    public const double AmbientBlurMinimum = 8;

    public const double AmbientBlurMaximum = 32;

    public const MatteStyle MatteStyle = global::Fovium.Stage.MatteStyle.Solid;

    public const double MatteWidthPhysicalPixels = 24;

    public const double MatteWidthMinimumPhysicalPixels = 4;

    public const double MatteWidthMaximumPhysicalPixels = 192;

    public const double MatteOuterShapeRatio = 1.5;

    public const double MatteSoftSigmaRatio = 1d / 3d;
}
