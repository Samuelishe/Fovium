using Avalonia;
using Fovium.Settings;

namespace Fovium.Views;

internal static class SettingsWindowSizePolicy
{
    private const double WorkAreaSafetyMarginDip = 24;

    public static Size Resolve(
        SettingsWindowSizeSettings settings,
        double workAreaWidthPhysicalPixels,
        double workAreaHeightPhysicalPixels,
        double renderScaling)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        if (!double.IsFinite(renderScaling) || renderScaling <= 0 ||
            !double.IsFinite(workAreaWidthPhysicalPixels) || workAreaWidthPhysicalPixels <= 0 ||
            !double.IsFinite(workAreaHeightPhysicalPixels) || workAreaHeightPhysicalPixels <= 0)
        {
            return new Size(normalized.WidthDip, normalized.HeightDip);
        }

        var maximumWidthDip = Math.Max(
            1,
            (workAreaWidthPhysicalPixels / renderScaling) - (WorkAreaSafetyMarginDip * 2));
        var maximumHeightDip = Math.Max(
            1,
            (workAreaHeightPhysicalPixels / renderScaling) - (WorkAreaSafetyMarginDip * 2));
        return new Size(
            Math.Min(normalized.WidthDip, maximumWidthDip),
            Math.Min(normalized.HeightDip, maximumHeightDip));
    }
}
