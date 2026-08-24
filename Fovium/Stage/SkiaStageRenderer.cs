using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Stage;

internal static class SkiaStageRenderer
{
    public static void Draw(
        SKCanvas canvas,
        RectD viewport,
        RectD photoDestination,
        double renderScaling,
        StageMode mode,
        SKImage? ambientImage,
        PixelSize? ambientSize)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var logicalViewport = new LogicalSize(viewport.Width, viewport.Height);
        var geometry = StageGeometry.CalculateRenderGeometry(
            mode,
            photoDestination,
            ambientSize,
            logicalViewport,
            renderScaling);
        using var backgroundPaint = new SKPaint
        {
            IsAntialias = false,
            Color = ToSkColor(mode == StageMode.Neutral
                ? StageDefaults.NeutralColor
                : StageDefaults.BlackColor),
        };
        canvas.DrawRect(ToSkRect(viewport), backgroundPaint);

        if (ambientImage is not null &&
            ambientSize is { IsValid: true } size &&
            geometry.AmbientDestination is { } cover)
        {
            using var ambientPaint = new SKPaint { IsAntialias = false };
            canvas.DrawImage(
                ambientImage,
                new SKRect(0, 0, size.Width, size.Height),
                ToSkRect(cover),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                ambientPaint);
        }

        if (geometry.MatteDestination is { } matte)
        {
            using var mattePaint = new SKPaint
            {
                IsAntialias = false,
                Color = ToSkColor(StageDefaults.MatteColor),
            };
            canvas.DrawRect(ToSkRect(matte), mattePaint);
        }
    }

    private static SKRect ToSkRect(RectD rectangle) => new(
        (float)rectangle.X,
        (float)rectangle.Y,
        (float)(rectangle.X + rectangle.Width),
        (float)(rectangle.Y + rectangle.Height));

    private static SKColor ToSkColor(StageColor color) =>
        new(color.Red, color.Green, color.Blue, color.Alpha);
}
