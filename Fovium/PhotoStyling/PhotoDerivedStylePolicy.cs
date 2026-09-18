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
    internal const double GradientMinimumLightness = 0.18;
    internal const double GradientMaximumLightness = 0.78;
    internal const double GradientChromaGain = 1.06;
    internal const double GradientMaximumChroma = 0.14;

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

    public static SKImage CreateColorGradientImage(PhotoStyleAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var gradient = ResolveLinearGradient(analysis);
        return CreateGradientImage((x, y) =>
        {
            var amount = gradient.Axis == PhotoGradientAxis.Horizontal ? x : y;
            return InterpolateGradientStops(
                gradient.Start,
                gradient.Middle,
                gradient.End,
                amount,
                0.5);
        });
    }

    public static SKImage CreateSoftGlowImage(PhotoStyleAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var glow = ResolveRadialGlow(analysis);
        return CreateGradientImage((x, y) =>
        {
            var horizontal = x - 0.5;
            var vertical = y - 0.5;
            var amount = Math.Min(1, Math.Sqrt((horizontal * horizontal) + (vertical * vertical)) / Math.Sqrt(0.5));
            return InterpolateGradientStops(
                glow.Center,
                glow.Middle,
                glow.Edge,
                amount,
                0.56);
        });
    }

    public static PhotoLinearGradient ResolveLinearGradient(PhotoStyleAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var field = analysis.SpatialField;
        var left = AverageFieldBand(field, 0, Math.Min(2, field.Columns), 0, field.Rows);
        var right = AverageFieldBand(
            field,
            Math.Max(0, field.Columns - 2),
            field.Columns,
            0,
            field.Rows);
        var top = AverageFieldBand(field, 0, field.Columns, 0, Math.Min(2, field.Rows));
        var bottom = AverageFieldBand(
            field,
            0,
            field.Columns,
            Math.Max(0, field.Rows - 2),
            field.Rows);
        var horizontalDistance = ColorDistanceSquared(left, right);
        var verticalDistance = ColorDistanceSquared(top, bottom);
        var horizontal = horizontalDistance >= verticalDistance;
        var boundary = PhotoStylingOklab.FromSrgb(analysis.BoundaryColor);
        var start = horizontal ? left : top;
        var end = horizontal ? right : bottom;
        return new PhotoLinearGradient(
            horizontal ? PhotoGradientAxis.Horizontal : PhotoGradientAxis.Vertical,
            NormalizeGradientTone(PhotoStylingOklab.Lerp(boundary, start, 0.72).ToSrgb()),
            NormalizeGradientTone(analysis.AverageColor),
            NormalizeGradientTone(PhotoStylingOklab.Lerp(boundary, end, 0.72).ToSrgb()));
    }

    public static PhotoRadialGlow ResolveRadialGlow(PhotoStyleAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var average = PhotoStylingOklab.FromSrgb(analysis.AverageColor);
        var dominant = PhotoStylingOklab.FromSrgb(analysis.DominantColor);
        var boundary = PhotoStylingOklab.FromSrgb(analysis.BoundaryColor);
        var middle = PhotoStylingOklab.Lerp(average, dominant, 0.18);
        var center = PhotoStylingOklab.Lerp(average, dominant, 0.50);
        var edge = PhotoStylingOklab.Lerp(boundary, average, 0.10);
        return new PhotoRadialGlow(
            NormalizeGradientTone(new PhotoStylingOklab(
                center.L + 0.06,
                center.A,
                center.B).ToSrgb()),
            NormalizeGradientTone(middle.ToSrgb()),
            NormalizeGradientTone(new PhotoStylingOklab(
                Math.Min(edge.L, middle.L - 0.08),
                edge.A,
                edge.B).ToSrgb()));
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

    private static SKImage CreateGradientImage(Func<double, double, StageColor> sample)
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        using var bitmap = new SKBitmap(new SKImageInfo(
            StageDefaults.PhotoStyleGradientRasterPixels,
            StageDefaults.PhotoStyleGradientRasterPixels,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque,
            colorSpace));
        var denominator = bitmap.Width - 1d;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = sample(x / denominator, y / denominator);
                bitmap.SetPixel(x, y, new SKColor(color.Red, color.Green, color.Blue));
            }
        }

        return SKImage.FromBitmap(bitmap);
    }

    private static StageColor InterpolateGradientStops(
        StageColor start,
        StageColor middle,
        StageColor end,
        double amount,
        double middlePosition)
    {
        var first = amount <= middlePosition;
        var localAmount = first
            ? amount / middlePosition
            : (amount - middlePosition) / (1 - middlePosition);
        return PhotoStylingOklab.Lerp(
            PhotoStylingOklab.FromSrgb(first ? start : middle),
            PhotoStylingOklab.FromSrgb(first ? middle : end),
            Math.Clamp(localAmount, 0, 1)).ToSrgb();
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

    internal static StageColor NormalizeGradientTone(StageColor source)
    {
        var lab = PhotoStylingOklab.FromSrgb(source);
        var chroma = lab.Chroma;
        var targetChroma = Math.Min(chroma * GradientChromaGain, GradientMaximumChroma);
        var scale = chroma > 0 ? targetChroma / chroma : 1;
        return new PhotoStylingOklab(
            Math.Clamp(lab.L, GradientMinimumLightness, GradientMaximumLightness),
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

    private static PhotoStylingOklab AverageFieldBand(
        PhotoColorField field,
        int firstColumn,
        int endColumn,
        int firstRow,
        int endRow)
    {
        var lightness = 0d;
        var a = 0d;
        var b = 0d;
        var count = 0;
        for (var row = firstRow; row < endRow; row++)
        {
            for (var column = firstColumn; column < endColumn; column++)
            {
                var color = PhotoStylingOklab.FromSrgb(field[column, row]);
                lightness += color.L;
                a += color.A;
                b += color.B;
                count++;
            }
        }

        return count > 0
            ? new PhotoStylingOklab(lightness / count, a / count, b / count)
            : PhotoStylingOklab.FromSrgb(StageDefaults.BlackColor);
    }

    private static double ColorDistanceSquared(
        PhotoStylingOklab first,
        PhotoStylingOklab second)
    {
        var lightness = first.L - second.L;
        var a = first.A - second.A;
        var b = first.B - second.B;
        return (lightness * lightness) + (a * a) + (b * b);
    }
}