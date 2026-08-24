namespace Fovium.Stage;

internal readonly record struct StageColor(byte Red, byte Green, byte Blue, byte Alpha = 255);

internal static class StageDefaults
{
    // Presentation defaults selected for R3; these are not calibrated reference values.
    public static StageColor BlackColor { get; } = new(0x00, 0x00, 0x00);

    public static StageColor NeutralColor { get; } = new(0x50, 0x50, 0x50);

    public static StageColor MatteColor { get; } = new(0x20, 0x20, 0x20);

    public const int AmbientLongEdgePixels = 384;

    public const float AmbientBlurSigmaPixels = 18;

    public const float AmbientSaturation = 0.55f;

    public const float AmbientBrightness = 0.45f;

    public const double MatteWidthPhysicalPixels = 24;
}
