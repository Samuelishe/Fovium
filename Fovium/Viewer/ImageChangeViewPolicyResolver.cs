using Fovium.Rendering;
using Fovium.Settings;

namespace Fovium.Viewer;

internal static class ImageChangeViewPolicyResolver
{
    public static ViewTransfer ForNavigation(
        ImageChangeViewPolicy policy,
        ViewTransfer currentView) =>
        policy switch
        {
            ImageChangeViewPolicy.KeepCurrentScale => currentView,
            ImageChangeViewPolicy.FitEachImage => ViewTransfer.Fit,
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    public static ViewTransfer ForNewSequence() => ViewTransfer.Fit;
}
