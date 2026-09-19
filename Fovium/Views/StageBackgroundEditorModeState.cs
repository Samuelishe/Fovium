using Fovium.Stage;

namespace Fovium.Views;

internal readonly record struct StageBackgroundEditorModeState(
    bool ShowNoAdjustments,
    bool ShowCustomColor,
    bool ShowColorAdjustment,
    bool ShowBlur)
{
    public static StageBackgroundEditorModeState Resolve(StageBackgroundMode mode)
    {
        var showCustom = mode == StageBackgroundMode.Custom;
        var showAdjustment = mode is StageBackgroundMode.Average or
            StageBackgroundMode.Dominant or
            StageBackgroundMode.ColorWash or
            StageBackgroundMode.ColorGradient or
            StageBackgroundMode.SoftGlow or
            StageBackgroundMode.Ambient;
        return new StageBackgroundEditorModeState(
            !showCustom && !showAdjustment,
            showCustom,
            showAdjustment,
            mode == StageBackgroundMode.Ambient);
    }
}