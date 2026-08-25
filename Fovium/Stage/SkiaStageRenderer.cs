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
        StageSettings stage,
        SKImage? ambientImage,
        PixelSize? ambientSize)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(stage);
        var logicalViewport = new LogicalSize(viewport.Width, viewport.Height);
        var geometry = StageGeometry.CalculateRenderGeometry(
            stage,
            photoDestination,
            ambientSize,
            logicalViewport,
            renderScaling);
        using var backgroundPaint = new SKPaint
        {
            IsAntialias = false,
            Color = ToSkColor(ResolveBackgroundColor(stage)),
        };
        canvas.DrawRect(ToSkRect(viewport), backgroundPaint);

        if (ambientImage is not null &&
            ambientSize is { IsValid: true } size &&
            geometry.AmbientDestination is { } cover)
        {
            using var ambientPaint = new SKPaint
            {
                IsAntialias = false,
                ColorFilter = SKColorFilter.CreateColorMatrix(CreateColorMatrix(
                    stage.AmbientBrightness,
                    stage.AmbientSaturation)),
            };
            canvas.DrawImage(
                ambientImage,
                new SKRect(0, 0, size.Width, size.Height),
                ToSkRect(cover),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                ambientPaint);
        }

        if (geometry.Matte is { } matte)
        {
            using var mattePaint = new SKPaint
            {
                IsAntialias = matte.Style != MatteStyle.Solid,
                Color = ToSkColor(stage.MatteColor),
            };
            canvas.Save();
            try
            {
                canvas.ClipRect(ToSkRect(viewport));
                DrawMatteOuterShape(canvas, matte, mattePaint);
                mattePaint.IsAntialias = false;
                mattePaint.MaskFilter = null;
                canvas.DrawRect(ToSkRect(matte.BackingDestination), mattePaint);
            }
            finally
            {
                canvas.Restore();
            }
        }
    }

    private static SKRect ToSkRect(RectD rectangle) => new(
        (float)rectangle.X,
        (float)rectangle.Y,
        (float)(rectangle.X + rectangle.Width),
        (float)(rectangle.Y + rectangle.Height));

    private static void DrawMatteOuterShape(
        SKCanvas canvas,
        MatteRenderGeometry matte,
        SKPaint paint)
    {
        var outer = ToSkRect(matte.OuterBounds);
        switch (matte.Style)
        {
            case MatteStyle.Solid:
                canvas.DrawRect(outer, paint);
                break;
            case MatteStyle.Rounded:
                canvas.DrawRoundRect(
                    outer,
                    (float)matte.OuterRadiusDip,
                    (float)matte.OuterRadiusDip,
                    paint);
                break;
            case MatteStyle.Soft:
                paint.MaskFilter = SKMaskFilter.CreateBlur(
                    SKBlurStyle.Normal,
                    (float)matte.SoftSigmaDip,
                    respectCTM: true);
                canvas.DrawRect(ToSkRect(matte.BackingDestination), paint);
                break;
            case MatteStyle.Angular:
                using (var path = CreateAngularPath(
                    StageGeometry.CalculateAngularPoints(matte.OuterBounds, matte.ChamferDip)))
                {
                    canvas.DrawPath(path, paint);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(matte));
        }
    }

    private static SKPath CreateAngularPath(IReadOnlyList<PointD> points)
    {
        var path = new SKPath();
        path.MoveTo((float)points[0].X, (float)points[0].Y);
        for (var index = 1; index < points.Count; index++)
        {
            path.LineTo((float)points[index].X, (float)points[index].Y);
        }

        path.Close();
        return path;
    }

    internal static float[] CreateColorMatrix(double brightness, double saturation)
    {
        const float redLuminance = 0.2126f;
        const float greenLuminance = 0.7152f;
        const float blueLuminance = 0.0722f;
        var brightnessValue = (float)brightness;
        var saturationValue = (float)saturation;
        var inverseSaturation = 1 - saturationValue;
        return
        [
            brightnessValue * (redLuminance * inverseSaturation + saturationValue),
            brightnessValue * greenLuminance * inverseSaturation,
            brightnessValue * blueLuminance * inverseSaturation,
            0,
            0,
            brightnessValue * redLuminance * inverseSaturation,
            brightnessValue * (greenLuminance * inverseSaturation + saturationValue),
            brightnessValue * blueLuminance * inverseSaturation,
            0,
            0,
            brightnessValue * redLuminance * inverseSaturation,
            brightnessValue * greenLuminance * inverseSaturation,
            brightnessValue * (blueLuminance * inverseSaturation + saturationValue),
            0,
            0,
            0,
            0,
            0,
            1,
            0,
        ];
    }

    private static StageColor ResolveBackgroundColor(StageSettings stage) =>
        stage.BackgroundMode switch
        {
            StageBackgroundMode.Neutral => StageDefaults.NeutralColor,
            StageBackgroundMode.Custom => stage.CustomBackgroundColor,
            _ => StageDefaults.BlackColor,
        };

    private static SKColor ToSkColor(StageColor color) =>
        new(color.Red, color.Green, color.Blue, 255);
}
