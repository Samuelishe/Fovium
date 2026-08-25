namespace Fovium.Stage;

internal enum StageBackgroundMode
{
    Black,
    Neutral,
    Custom,
    Ambient,
}

internal static class StageBackgroundModeExtensions
{
    public static bool RequiresAmbient(this StageBackgroundMode mode) =>
        mode == StageBackgroundMode.Ambient;
}
