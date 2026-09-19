using Fovium.Settings;

namespace Fovium.Home;

internal enum RecentCaptureTrigger
{
    ExplicitOpen,
    ManualNavigation,
    SlideshowNavigation,
    Preload,
    FailedOrStalePublication,
}

internal static class RecentCapturePolicyResolver
{
    public static bool ShouldCapture(HomeSettings settings, RecentCaptureTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.RememberRecentPhotos)
        {
            return false;
        }

        return trigger switch
        {
            RecentCaptureTrigger.ExplicitOpen => true,
            RecentCaptureTrigger.ManualNavigation =>
                settings.CapturePolicy == RecentCapturePolicy.EveryViewedPhoto,
            RecentCaptureTrigger.SlideshowNavigation or
                RecentCaptureTrigger.Preload or
                RecentCaptureTrigger.FailedOrStalePublication => false,
            _ => false,
        };
    }
}