using System.Collections.Immutable;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.PhotoStyling;

internal readonly record struct HairlinePresentation(
    StageColor Color,
    byte Alpha,
    double WidthDip);

internal static class PhotoDerivedStylePolicy
{
    internal const double MatteMinimumLightness = 0.30;
    internal const double MatteMaximumLightness = 0.88;
    internal const double MatteMaximumChroma = 0.10;
    internal const double WashMinimumLightness = 0.20;
    internal const double WashMaximumLightness = 0.76;
    internal const double WashChromaGain = 1.18;
    internal const double WashMaximumChroma = 0.16;

    public static StageColor ResolveMatteColor(
        StageSettings stage,
        PhotoStyleAnalysis? analysis)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var source = stage.MatteColorSource switch
        {
            MatteColorSource.Custom => stage.MatteColor,
            MatteColorSource.Average => analysis?.AverageColor ?? StageDefaults.MatteColor,
            MatteColorSource.Dominant => analysis?.DominantColor ?? StageDefaults.MatteColor,
            _ => StageDefaults.MatteColor,
        };
        return stage.MatteColorSource == MatteColorSource.Custom
            ? source
            : NormalizeTone(
                source,
                MatteMinimumLightness,
                MatteMaximumLightness,
                MatteMaximumChroma);
    }

    public static PhotoColorField ResolveWashField(PhotoStyleAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        return analysis.SpatialField with
        {
            Colors = analysis.SpatialField.Colors
                .Select(NormalizeWashTone)
                .ToImmutableArray(),
        };
    }

    public static SKImage CreateColorWashImage(PhotoStyleAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var field = ResolveWashField(analysis);
        using var colorSpace = SKColorSpace.CreateSrgb();
        using var bitmap = new SKBitmap(new SKImageInfo(
            StageDefaults.PhotoStyleWashRasterPixels,
            StageDefaults.PhotoStyleWashRasterPixels,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque,
            colorSpace));
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = SampleSmoothField(field, x, y, bitmap.Width, bitmap.Height);
                bitmap.SetPixel(x, y, new SKColor(color.Red, color.Green, color.Blue));
            }
        }

        return SKImage.FromBitmap(bitmap);
    }

    private static StageColor SampleSmoothField(
        PhotoColorField field,
        int x,
        int y,
        int width,
        int height)
    {
        var fieldX = Math.Clamp(((x + 0.5) * field.Columns / width) - 0.5, 0, field.Columns - 1);
        var fieldY = Math.Clamp(((y + 0.5) * field.Rows / height) - 0.5, 0, field.Rows - 1);
        var left = (int)Math.Floor(fieldX);
        var top = (int)Math.Floor(fieldY);
        var right = Math.Min(field.Columns - 1, left + 1);
        var bottom = Math.Min(field.Rows - 1, top + 1);
        var horizontal = SmoothStep(fieldX - left);
        var vertical = SmoothStep(fieldY - top);
        var topTone = PhotoStylingOklab.Lerp(
            PhotoStylingOklab.FromSrgb(field[left, top]),
            PhotoStylingOklab.FromSrgb(field[right, top]),
            horizontal);
        var bottomTone = PhotoStylingOklab.Lerp(
            PhotoStylingOklab.FromSrgb(field[left, bottom]),
            PhotoStylingOklab.FromSrgb(field[right, bottom]),
            horizontal);
        return PhotoStylingOklab.Lerp(topTone, bottomTone, vertical).ToSrgb();
    }

    private static double SmoothStep(double value) => value * value * (3 - (2 * value));

    public static HairlinePresentation? ResolveHairline(
        StageSettings stage,
        PhotoStyleAnalysis? analysis,
        double renderScaling)
    {
        ArgumentNullException.ThrowIfNull(stage);
        if (!stage.MatteEnabled ||
            stage.PhotoSeparation != PhotoSeparationMode.HairlineAuto ||
            analysis is null)
        {
            return null;
        }

        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }

        var matte = ResolveMatteColor(stage, analysis);
        StageColor[] candidates =
        [
            new StageColor(0, 0, 0),
            new StageColor(128, 128, 128),
            new StageColor(255, 255, 255),
        ];
        var selected = candidates
            .Select((color, index) => new
            {
                Color = color,
                Index = index,
                Score = Math.Min(
                    ContrastRatio(color, matte),
                    ContrastRatio(color, analysis.BoundaryColor)),
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .First();
        return new HairlinePresentation(
            selected.Color,
            StageDefaults.HairlineOpacity,
            1d / renderScaling);
    }

    internal static StageColor NormalizeTone(
        StageColor source,
        double minimumLightness,
        double maximumLightness,
        double maximumChroma)
    {
        var lab = PhotoStylingOklab.FromSrgb(source);
        var chroma = lab.Chroma;
        var scale = chroma > maximumChroma ? maximumChroma / chroma : 1;
        return new PhotoStylingOklab(
            Math.Clamp(lab.L, minimumLightness, maximumLightness),
            lab.A * scale,
            lab.B * scale).ToSrgb();
    }

    internal static StageColor NormalizeWashTone(StageColor source)
    {
        var lab = PhotoStylingOklab.FromSrgb(source);
        var chroma = lab.Chroma;
        var targetChroma = Math.Min(chroma * WashChromaGain, WashMaximumChroma);
        var scale = chroma > 0 ? targetChroma / chroma : 1;
        return new PhotoStylingOklab(
            Math.Clamp(lab.L, WashMinimumLightness, WashMaximumLightness),
            lab.A * scale,
            lab.B * scale).ToSrgb();
    }

    internal static double ContrastRatio(StageColor first, StageColor second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(StageColor color) =>
        (0.2126 * ToLinear(color.Red / 255d)) +
        (0.7152 * ToLinear(color.Green / 255d)) +
        (0.0722 * ToLinear(color.Blue / 255d));

    private static double ToLinear(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);

}
