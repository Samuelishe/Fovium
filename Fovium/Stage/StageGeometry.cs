using Fovium.Rendering;

namespace Fovium.Stage;

internal readonly record struct MatteRenderGeometry(
    RectD BackingDestination,
    RectD OuterBounds,
    RectD VisibleBounds,
    MatteStyle Style,
    double WidthDip,
    double OuterRadiusDip,
    double ChamferDip,
    double SoftSigmaDip);

internal readonly record struct StageRenderGeometry(
    RectD PhotoDestination,
    RectD? AmbientDestination,
    MatteRenderGeometry? Matte);

internal static class StageGeometry
{
    public static StageRenderGeometry CalculateRenderGeometry(
        StageSettings stage,
        RectD photoDestination,
        PixelSize? ambientSize,
        LogicalSize viewport,
        double renderScaling)
    {
        ArgumentNullException.ThrowIfNull(stage);
        RectD? ambient = stage.BackgroundMode.RequiresAmbient() && ambientSize is { IsValid: true } size
            ? CalculateCover(size, viewport)
            : null;
        MatteRenderGeometry? matte = stage.MatteEnabled
            ? CalculateMatte(
                photoDestination,
                viewport,
                renderScaling,
                stage.MatteStyle,
                stage.MatteWidthPhysicalPixels)
            : null;
        return new StageRenderGeometry(photoDestination, ambient, matte);
    }

    public static RectD CalculateCover(PixelSize source, LogicalSize viewport)
    {
        if (!source.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (!viewport.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(viewport));
        }

        var scale = Math.Max(viewport.Width / source.Width, viewport.Height / source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;
        return new RectD(
            (viewport.Width - width) / 2,
            (viewport.Height - height) / 2,
            width,
            height);
    }

    public static MatteRenderGeometry CalculateMatte(
        RectD imageDestination,
        LogicalSize viewport,
        double renderScaling,
        MatteStyle style,
        double physicalWidth)
    {
        if (!IsValidImageDestination(imageDestination))
        {
            throw new ArgumentOutOfRangeException(nameof(imageDestination));
        }

        if (!viewport.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(viewport));
        }

        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }

        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style));
        }

        if (!double.IsFinite(physicalWidth) ||
            physicalWidth < StageDefaults.MatteWidthMinimumPhysicalPixels ||
            physicalWidth > StageDefaults.MatteWidthMaximumPhysicalPixels)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalWidth));
        }

        var widthDip = physicalWidth / renderScaling;
        var outer = Inflate(imageDestination, widthDip);
        var visible = Intersect(outer, new RectD(0, 0, viewport.Width, viewport.Height));
        var derivedShapeSize = Math.Min(
            widthDip * StageDefaults.MatteOuterShapeRatio,
            Math.Min(outer.Width, outer.Height) / 2);
        var softSigma = widthDip * StageDefaults.MatteSoftSigmaRatio;
        return new MatteRenderGeometry(
            imageDestination,
            outer,
            visible,
            style,
            widthDip,
            derivedShapeSize,
            derivedShapeSize,
            softSigma);
    }

    public static IReadOnlyList<PointD> CalculateAngularPoints(RectD bounds, double chamfer)
    {
        if (!IsValidImageDestination(bounds) ||
            !double.IsFinite(chamfer) ||
            chamfer < 0 ||
            chamfer > Math.Min(bounds.Width, bounds.Height) / 2)
        {
            throw new ArgumentOutOfRangeException(nameof(chamfer));
        }

        var right = bounds.X + bounds.Width;
        var bottom = bounds.Y + bounds.Height;
        return
        [
            new PointD(bounds.X + chamfer, bounds.Y),
            new PointD(right - chamfer, bounds.Y),
            new PointD(right, bounds.Y + chamfer),
            new PointD(right, bottom - chamfer),
            new PointD(right - chamfer, bottom),
            new PointD(bounds.X + chamfer, bottom),
            new PointD(bounds.X, bottom - chamfer),
            new PointD(bounds.X, bounds.Y + chamfer),
        ];
    }

    private static bool IsValidImageDestination(RectD value) =>
        double.IsFinite(value.X) &&
        double.IsFinite(value.Y) &&
        double.IsFinite(value.Width) &&
        double.IsFinite(value.Height) &&
        value.Width > 0 &&
        value.Height > 0;

    private static RectD Inflate(RectD value, double inset) => new(
        value.X - inset,
        value.Y - inset,
        value.Width + (2 * inset),
        value.Height + (2 * inset));

    private static RectD Intersect(RectD first, RectD second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);
        return new RectD(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
