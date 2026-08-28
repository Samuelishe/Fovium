using Fovium.Rendering;
using Fovium.Settings;

namespace Fovium.Stage;

internal readonly record struct PhotoPresentationLayoutResult(
    RectD PhotoDestination,
    RectD PhotoPresentationBounds,
    double MarginPhysicalPixels,
    double PhysicalScale,
    bool PhotoFitsPresentationBounds)
{
    public bool UsesExactPixelSampling =>
        Math.Abs(PhysicalScale - Math.Round(PhysicalScale)) <= 1e-9;
}

internal static class PhotoPresentationLayout
{
    private const double MinimumPhotoPhysicalPixels = 1;

    public static PhotoPresentationLayoutResult Calculate(LogicalSize viewport,
        double renderScaling,
        PixelSize orientedPhotoSize,
        double edgeMarginPercent)
    {
        if (!viewport.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(viewport));
        }

        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }

        if (!orientedPhotoSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(orientedPhotoSize));
        }

        if (!double.IsFinite(edgeMarginPercent) ||
            edgeMarginPercent < PhotoPresentationViewSettings.MinimumEdgeMarginPercent ||
            edgeMarginPercent > PhotoPresentationViewSettings.MaximumEdgeMarginPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeMarginPercent));
        }

        var viewportPhysicalWidth = viewport.Width * renderScaling;
        var viewportPhysicalHeight = viewport.Height * renderScaling;
        var requestedMarginPhysical =
            Math.Min(viewportPhysicalWidth, viewportPhysicalHeight) * edgeMarginPercent / 100;
        var maximumFeasibleMargin = Math.Max(
            0,
            (Math.Min(viewportPhysicalWidth, viewportPhysicalHeight) - MinimumPhotoPhysicalPixels) / 2);
        var marginPhysical = Math.Min(requestedMarginPhysical, maximumFeasibleMargin);
        var availablePhotoWidth = Math.Max(
            MinimumPhotoPhysicalPixels,
            viewportPhysicalWidth - (2 * marginPhysical));
        var availablePhotoHeight = Math.Max(
            MinimumPhotoPhysicalPixels,
            viewportPhysicalHeight - (2 * marginPhysical));
        var physicalScale = Math.Min(
            1,
            Math.Min(
                availablePhotoWidth / orientedPhotoSize.Width,
                availablePhotoHeight / orientedPhotoSize.Height));

        var photoWidthPhysical = orientedPhotoSize.Width * physicalScale;
        var photoHeightPhysical = orientedPhotoSize.Height * physicalScale;
        var photoXPhysical = (viewportPhysicalWidth - photoWidthPhysical) / 2;
        var photoYPhysical = (viewportPhysicalHeight - photoHeightPhysical) / 2;
        if (Math.Abs(physicalScale - Math.Round(physicalScale)) <= 1e-9)
        {
            photoXPhysical = Math.Round(photoXPhysical, MidpointRounding.AwayFromZero);
            photoYPhysical = Math.Round(photoYPhysical, MidpointRounding.AwayFromZero);
        }

        var photoWidthDip = photoWidthPhysical / renderScaling;
        var photoHeightDip = photoHeightPhysical / renderScaling;
        var photoDestination = new RectD(
            photoXPhysical / renderScaling,
            photoYPhysical / renderScaling,
            photoWidthDip,
            photoHeightDip);
        var photoPresentationBounds = new RectD(
            marginPhysical / renderScaling,
            marginPhysical / renderScaling,
            Math.Max(MinimumPhotoPhysicalPixels / renderScaling, viewport.Width - (2 * marginPhysical / renderScaling)),
            Math.Max(MinimumPhotoPhysicalPixels / renderScaling, viewport.Height - (2 * marginPhysical / renderScaling)));
        var photoFitsPresentationBounds = Math.Abs(marginPhysical - requestedMarginPhysical) <= 1e-9 &&
            Contains(photoPresentationBounds, photoDestination);

        return new PhotoPresentationLayoutResult(
            photoDestination,
            photoPresentationBounds,
            marginPhysical,
            physicalScale,
            photoFitsPresentationBounds);
    }

    private static bool Contains(RectD outer, RectD inner) =>
        inner.X >= outer.X - 1e-9 &&
        inner.Y >= outer.Y - 1e-9 &&
        inner.X + inner.Width <= outer.X + outer.Width + 1e-9 &&
        inner.Y + inner.Height <= outer.Y + outer.Height + 1e-9;
}
