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
}

internal static class StageBackgroundModeExtensions
{
    public static bool RequiresAmbient(this StageBackgroundMode mode) =>
        mode == StageBackgroundMode.Ambient;

    public static bool RequiresPhotoStyleAnalysis(this StageBackgroundMode mode) =>
        mode is StageBackgroundMode.Average or
            StageBackgroundMode.Dominant or
            StageBackgroundMode.ColorWash;
}
