using Fovium.Rendering;
using Fovium.Settings;

namespace Fovium.Stage;

internal readonly record struct PhotoPresentationLayoutResult(
    RectD PhotoDestination,
    RectD OuterPresentationBounds,
    RectD PresentationBounds,
    double MarginPhysicalPixels,
    double PhysicalScale,
    bool FitsRequestedBounds)
{
    public bool UsesExactPixelSampling =>
        Math.Abs(PhysicalScale - Math.Round(PhysicalScale)) <= 1e-9;
}

internal static class PhotoPresentationLayout
{
    private const double MinimumPhotoPhysicalPixels = 1;

    public static PhotoPresentationLayoutResult Calculate(
        LogicalSize viewport,
        double renderScaling,
        PixelSize orientedPhotoSize,
        StageSettings stage,
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

        ArgumentNullException.ThrowIfNull(stage);
        if (!double.IsFinite(edgeMarginPercent) ||
            edgeMarginPercent < PhotoPresentationViewSettings.MinimumEdgeMarginPercent ||
            edgeMarginPercent > PhotoPresentationViewSettings.MaximumEdgeMarginPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeMarginPercent));
        }

        var normalizedStage = stage.Normalize();
        var viewportPhysicalWidth = viewport.Width * renderScaling;
        var viewportPhysicalHeight = viewport.Height * renderScaling;
        var requestedMarginPhysical =
            Math.Min(viewportPhysicalWidth, viewportPhysicalHeight) * edgeMarginPercent / 100;
        var mattePhysical = normalizedStage.MatteEnabled
            ? normalizedStage.MatteWidthPhysicalPixels
            : 0;

        var maximumFeasibleMargin = Math.Max(
            0,
            (Math.Min(viewportPhysicalWidth, viewportPhysicalHeight) -
             (2 * mattePhysical) - MinimumPhotoPhysicalPixels) / 2);
        var marginPhysical = Math.Min(requestedMarginPhysical, maximumFeasibleMargin);
        var availablePhotoWidth = Math.Max(
            MinimumPhotoPhysicalPixels,
            viewportPhysicalWidth - (2 * (marginPhysical + mattePhysical)));
        var availablePhotoHeight = Math.Max(
            MinimumPhotoPhysicalPixels,
            viewportPhysicalHeight - (2 * (marginPhysical + mattePhysical)));
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
        var presentationBounds = new RectD(
            marginPhysical / renderScaling,
            marginPhysical / renderScaling,
            Math.Max(MinimumPhotoPhysicalPixels / renderScaling, viewport.Width - (2 * marginPhysical / renderScaling)),
            Math.Max(MinimumPhotoPhysicalPixels / renderScaling, viewport.Height - (2 * marginPhysical / renderScaling)));
        var outerBounds = normalizedStage.MatteEnabled
            ? StageGeometry.CalculateMatte(
                photoDestination,
                viewport,
                renderScaling,
                normalizedStage.MatteStyle,
                normalizedStage.MatteWidthPhysicalPixels).OuterBounds
            : photoDestination;
        var fitsRequestedBounds = Math.Abs(marginPhysical - requestedMarginPhysical) <= 1e-9 &&
            Contains(presentationBounds, outerBounds);

        return new PhotoPresentationLayoutResult(
            photoDestination,
            outerBounds,
            presentationBounds,
            marginPhysical,
            physicalScale,
            fitsRequestedBounds);
    }

    private static bool Contains(RectD outer, RectD inner) =>
        inner.X >= outer.X - 1e-9 &&
        inner.Y >= outer.Y - 1e-9 &&
        inner.X + inner.Width <= outer.X + outer.Width + 1e-9 &&
        inner.Y + inner.Height <= outer.Y + outer.Height + 1e-9;
}
