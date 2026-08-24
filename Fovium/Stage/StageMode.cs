namespace Fovium.Stage;

internal enum StageMode
{
    Black,
    Neutral,
    Ambient,
    AmbientMatte,
}

internal static class StageModeExtensions
{
    public static bool RequiresAmbient(this StageMode mode) =>
        mode is StageMode.Ambient or StageMode.AmbientMatte;
}
