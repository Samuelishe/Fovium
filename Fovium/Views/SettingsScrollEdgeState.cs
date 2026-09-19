namespace Fovium.Views;

internal readonly record struct SettingsScrollEdgeState(bool ShowTopFade, bool ShowBottomFade)
{
    public static SettingsScrollEdgeState Resolve(
        double offset,
        double extent,
        double viewport,
        double threshold = 0.5)
    {
        var safeOffset = double.IsFinite(offset) ? Math.Max(0, offset) : 0;
        var safeExtent = double.IsFinite(extent) ? Math.Max(0, extent) : 0;
        var safeViewport = double.IsFinite(viewport) ? Math.Max(0, viewport) : 0;
        var safeThreshold = double.IsFinite(threshold) ? Math.Max(0, threshold) : 0.5;
        var overflow = safeExtent > safeViewport + safeThreshold;
        return new SettingsScrollEdgeState(
            overflow && safeOffset > safeThreshold,
            overflow && safeExtent - safeViewport - safeOffset > safeThreshold);
    }
}