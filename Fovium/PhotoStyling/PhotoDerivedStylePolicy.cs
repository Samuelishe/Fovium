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
    internal const double WashMinimumLightness = 0.18;
    internal const double WashMaximumLightness = 0.72;
    internal const double WashMaximumChroma = 0.12;

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
                .Select(color => NormalizeTone(
                    color,
                    WashMinimumLightness,
                    WashMaximumLightness,
                    WashMaximumChroma))
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
        var topTone = Oklab.Lerp(
            Oklab.FromSrgb(field[left, top]),
            Oklab.FromSrgb(field[right, top]),
            horizontal);
        var bottomTone = Oklab.Lerp(
            Oklab.FromSrgb(field[left, bottom]),
            Oklab.FromSrgb(field[right, bottom]),
            horizontal);
        return Oklab.Lerp(topTone, bottomTone, vertical).ToSrgb();
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
        var lab = Oklab.FromSrgb(source);
        var chroma = Math.Sqrt((lab.A * lab.A) + (lab.B * lab.B));
        var scale = chroma > maximumChroma ? maximumChroma / chroma : 1;
        return new Oklab(
            Math.Clamp(lab.L, minimumLightness, maximumLightness),
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

    private static double ToSrgb(double channel) =>
        channel <= 0.0031308
            ? 12.92 * channel
            : (1.055 * Math.Pow(channel, 1d / 2.4)) - 0.055;

    private readonly record struct Oklab(double L, double A, double B)
    {
        public static Oklab Lerp(Oklab first, Oklab second, double amount) => new(
            first.L + ((second.L - first.L) * amount),
            first.A + ((second.A - first.A) * amount),
            first.B + ((second.B - first.B) * amount));

        public static Oklab FromSrgb(StageColor color)
        {
            var red = ToLinear(color.Red / 255d);
            var green = ToLinear(color.Green / 255d);
            var blue = ToLinear(color.Blue / 255d);
            var l = (0.4122214708 * red) + (0.5363325363 * green) + (0.0514459929 * blue);
            var m = (0.2119034982 * red) + (0.6806995451 * green) + (0.1073969566 * blue);
            var s = (0.0883024619 * red) + (0.2817188376 * green) + (0.6299787005 * blue);
            var lRoot = Math.Cbrt(l);
            var mRoot = Math.Cbrt(m);
            var sRoot = Math.Cbrt(s);
            return new Oklab(
                (0.2104542553 * lRoot) + (0.7936177850 * mRoot) - (0.0040720468 * sRoot),
                (1.9779984951 * lRoot) - (2.4285922050 * mRoot) + (0.4505937099 * sRoot),
                (0.0259040371 * lRoot) + (0.7827717662 * mRoot) - (0.8086757660 * sRoot));
        }

        public StageColor ToSrgb()
        {
            var lRoot = L + (0.3963377774 * A) + (0.2158037573 * B);
            var mRoot = L - (0.1055613458 * A) - (0.0638541728 * B);
            var sRoot = L - (0.0894841775 * A) - (1.2914855480 * B);
            var l = lRoot * lRoot * lRoot;
            var m = mRoot * mRoot * mRoot;
            var s = sRoot * sRoot * sRoot;
            var red = (+4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
            var green = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
            var blue = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);
            return new StageColor(ToByte(red), ToByte(green), ToByte(blue));
        }

        private static byte ToByte(double linear) =>
            (byte)Math.Clamp(
                (int)Math.Round(
                    PhotoDerivedStylePolicy.ToSrgb(Math.Clamp(linear, 0, 1)) * 255),
                0,
                255);
    }
}
