namespace Fovium.Stage;

internal enum StageBackgroundMode
{
    Black,
    Neutral,
    Custom,
    Ambient,
    Average,
    Dominant,
    ColorWash,
    ColorGradient,
    SoftGlow,
}

internal static class StageBackgroundModeExtensions
{
    public static bool RequiresAmbient(this StageBackgroundMode mode) =>
        mode == StageBackgroundMode.Ambient;

    public static bool RequiresPhotoStyleAnalysis(this StageBackgroundMode mode) =>
        mode is StageBackgroundMode.Average or
            StageBackgroundMode.Dominant or
            StageBackgroundMode.ColorWash or
            StageBackgroundMode.ColorGradient or
            StageBackgroundMode.SoftGlow;
}