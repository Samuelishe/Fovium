using Fovium.Rendering;
using Fovium.PhotoStyling;
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
        PixelSize? ambientSize,
        long imageIdentity = 0,
        long? ambientIdentity = null,
        AmbientRenderFrameDiagnostics? frameDiagnostics = null,
        PhotoStyleAnalysis? photoStyleAnalysis = null,
        long? photoStyleIdentity = null,
        SKImage? colorWashImage = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(stage);
        var ambientPresent = ambientImage is not null && ambientSize is { IsValid: true };
        var matchingAmbient = ambientPresent && (imageIdentity == 0 || ambientIdentity == imageIdentity);
        var matchingPhotoStyle = photoStyleAnalysis is not null &&
            (imageIdentity == 0 || photoStyleIdentity == imageIdentity);
        frameDiagnostics?.Record(
            imageIdentity,
            stage.BackgroundMode,
            matchingAmbient ? ambientIdentity : null,
            matchingAmbient);
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
            Color = ToSkColor(ResolveBackgroundColor(
                stage,
                matchingPhotoStyle ? photoStyleAnalysis : null)),
        };
        canvas.DrawRect(ToSkRect(viewport), backgroundPaint);

        if (stage.BackgroundMode == StageBackgroundMode.ColorWash &&
            matchingPhotoStyle &&
            colorWashImage is { } colorWash)
        {
            canvas.DrawImage(
                colorWash,
                new SKRect(0, 0, colorWash.Width, colorWash.Height),
                ToSkRect(viewport),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
                backgroundPaint);
        }

        if (matchingAmbient &&
            ambientImage is not null &&
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
                Color = ToSkColor(PhotoDerivedStylePolicy.ResolveMatteColor(
                    stage,
                    matchingPhotoStyle ? photoStyleAnalysis : null)),
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

        var hairline = PhotoDerivedStylePolicy.ResolveHairline(
            stage,
            matchingPhotoStyle ? photoStyleAnalysis : null,
            renderScaling);
        if (hairline is { } separation)
        {
            using var hairlinePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)separation.WidthDip,
                Color = new SKColor(
                    separation.Color.Red,
                    separation.Color.Green,
                    separation.Color.Blue,
                    separation.Alpha),
            };
            var offset = separation.WidthDip / 2;
            var outline = new RectD(
                photoDestination.X - offset,
                photoDestination.Y - offset,
                photoDestination.Width + separation.WidthDip,
                photoDestination.Height + separation.WidthDip);
            canvas.Save();
            try
            {
                canvas.ClipRect(ToSkRect(viewport));
                canvas.DrawRect(ToSkRect(outline), hairlinePaint);
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

    private static StageColor ResolveBackgroundColor(
        StageSettings stage,
        PhotoStyleAnalysis? analysis) =>
        stage.BackgroundMode switch
        {
            StageBackgroundMode.Neutral => StageDefaults.NeutralColor,
            StageBackgroundMode.Custom => stage.CustomBackgroundColor,
            StageBackgroundMode.Average => analysis?.AverageColor ?? StageDefaults.BlackColor,
            StageBackgroundMode.Dominant => analysis?.DominantColor ?? StageDefaults.BlackColor,
            _ => StageDefaults.BlackColor,
        };

    private static SKColor ToSkColor(StageColor color) =>
        new(color.Red, color.Green, color.Blue, 255);
}
