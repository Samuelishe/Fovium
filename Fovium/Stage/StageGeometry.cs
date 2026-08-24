using Fovium.Rendering;

namespace Fovium.Stage;

internal readonly record struct StageLayout(RectD ImageDestination, RectD? MatteDestination);

internal readonly record struct StageRenderGeometry(
    RectD PhotoDestination,
    RectD? AmbientDestination,
    RectD? MatteDestination);

internal static class StageGeometry
{
    public static StageRenderGeometry CalculateRenderGeometry(
        StageMode mode,
        RectD photoDestination,
        PixelSize? ambientSize,
        LogicalSize viewport,
        double renderScaling)
    {
        RectD? ambient = mode.RequiresAmbient() && ambientSize is { IsValid: true } size
            ? CalculateCover(size, viewport)
            : null;
        RectD? matte = mode == StageMode.AmbientMatte
            ? CalculateMatte(photoDestination, viewport, renderScaling).MatteDestination
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

    public static StageLayout CalculateMatte(
        RectD imageDestination,
        LogicalSize viewport,
        double renderScaling,
        double physicalWidth = StageDefaults.MatteWidthPhysicalPixels)
    {
        if (!viewport.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(viewport));
        }

        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }

        if (!double.IsFinite(physicalWidth) || physicalWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalWidth));
        }

        var inset = physicalWidth / renderScaling;
        var left = Math.Max(0, imageDestination.X - inset);
        var top = Math.Max(0, imageDestination.Y - inset);
        var right = Math.Min(viewport.Width, imageDestination.X + imageDestination.Width + inset);
        var bottom = Math.Min(viewport.Height, imageDestination.Y + imageDestination.Height + inset);
        var matte = new RectD(
            left,
            top,
            Math.Max(0, right - left),
            Math.Max(0, bottom - top));
        return new StageLayout(imageDestination, matte);
    }
}
