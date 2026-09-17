namespace Fovium.ColorPicking;

internal enum PerceptualHueFamily
{
    Neutral,
    WarmGray,
    CoolGray,
    Red,
    Coral,
    Orange,
    Amber,
    Yellow,
    YellowGreen,
    Green,
    Turquoise,
    Cyan,
    Blue,
    BlueViolet,
    Violet,
    Magenta,
    Pink,
    Burgundy,
    Brown,
    Olive,
}

internal enum PerceptualLightnessClass
{
    VeryDark,
    Dark,
    Medium,
    Light,
    VeryLight,
}

internal enum PerceptualChromaClass
{
    Neutral,
    Muted,
    Moderate,
    Saturated,
    Vivid,
}

internal readonly record struct OklchColor(double L, double C, double HueDegrees)
{
    public static OklchColor FromSrgb(byte red, byte green, byte blue)
    {
        var oklab = OklabColor.FromSrgb(red, green, blue);
        var chroma = Math.Sqrt((oklab.A * oklab.A) + (oklab.B * oklab.B));
        var hue = Math.Atan2(oklab.B, oklab.A) * (180d / Math.PI);
        if (hue < 0)
        {
            hue += 360d;
        }

        return new OklchColor(oklab.L, chroma, hue);
    }
}

internal sealed record PerceptualColorDescription(
    OklchColor? Oklch,
    PerceptualHueFamily? HueFamily,
    PerceptualLightnessClass? LightnessClass,
    PerceptualChromaClass? ChromaClass)
{
    public bool IsTransparent => Oklch is null;

    public static PerceptualColorDescription Transparent { get; } = new(null, null, null, null);
}

internal static class PerceptualColorClassifier
{
    // These thresholds are monotonic in OKLCH. The narrow cast-neutral band keeps
    // true grays neutral while allowing a restrained warm/cool-gray distinction.
    internal const double TrueNeutralChromaMaximum = 0.008;
    internal const double CastNeutralChromaMaximum = 0.035;
    internal const double CoolGrayChromaMaximum = 0.055;

    public static PerceptualColorDescription Describe(ColorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.IsTransparent)
        {
            return PerceptualColorDescription.Transparent;
        }

        var oklch = OklchColor.FromSrgb(sample.Red, sample.Green, sample.Blue);
        return new PerceptualColorDescription(
            oklch,
            ClassifyHue(oklch),
            ClassifyLightness(oklch.L),
            ClassifyChroma(oklch.C));
    }

    internal static PerceptualLightnessClass ClassifyLightness(double lightness) => lightness switch
    {
        < 0.25 => PerceptualLightnessClass.VeryDark,
        < 0.45 => PerceptualLightnessClass.Dark,
        < 0.72 => PerceptualLightnessClass.Medium,
        < 0.88 => PerceptualLightnessClass.Light,
        _ => PerceptualLightnessClass.VeryLight,
    };

    internal static PerceptualChromaClass ClassifyChroma(double chroma) => chroma switch
    {
        < 0.025 => PerceptualChromaClass.Neutral,
        < 0.07 => PerceptualChromaClass.Muted,
        < 0.14 => PerceptualChromaClass.Moderate,
        < 0.24 => PerceptualChromaClass.Saturated,
        _ => PerceptualChromaClass.Vivid,
    };

    private static PerceptualHueFamily ClassifyHue(OklchColor color)
    {
        if (color.C < TrueNeutralChromaMaximum)
        {
            return PerceptualHueFamily.Neutral;
        }

        if (color.C < CastNeutralChromaMaximum)
        {
            return IsWarmHue(color.HueDegrees)
                ? PerceptualHueFamily.WarmGray
                : PerceptualHueFamily.CoolGray;
        }

        if (color.C < CoolGrayChromaMaximum && color.HueDegrees is >= 220 and < 310)
        {
            return PerceptualHueFamily.CoolGray;
        }

        if (IsRedHue(color.HueDegrees) && color.L < 0.50 && color.C < 0.18)
        {
            return PerceptualHueFamily.Burgundy;
        }

        if (color.HueDegrees is >= 35 and < 85 && color.L < 0.62 && color.C < 0.18)
        {
            return PerceptualHueFamily.Brown;
        }

        if (color.HueDegrees is >= 85 and < 125 && color.L < 0.72 && color.C < 0.18)
        {
            return PerceptualHueFamily.Olive;
        }

        if (color.HueDegrees is >= 20 and < 45 &&
            color.L >= 0.52 &&
            color.C is >= 0.08 and < 0.22)
        {
            return PerceptualHueFamily.Coral;
        }

        if ((color.HueDegrees >= 345 || color.HueDegrees < 25) &&
            color.L >= 0.68 &&
            color.C < 0.23)
        {
            return PerceptualHueFamily.Pink;
        }

        return color.HueDegrees switch
        {
            < 40 => PerceptualHueFamily.Red,
            < 65 => PerceptualHueFamily.Orange,
            < 90 => PerceptualHueFamily.Amber,
            < 115 => PerceptualHueFamily.Yellow,
            < 140 => PerceptualHueFamily.YellowGreen,
            < 170 => PerceptualHueFamily.Green,
            < 195 => PerceptualHueFamily.Turquoise,
            < 225 => PerceptualHueFamily.Cyan,
            < 275 => PerceptualHueFamily.Blue,
            < 305 => PerceptualHueFamily.BlueViolet,
            < 325 => PerceptualHueFamily.Violet,
            < 345 => PerceptualHueFamily.Magenta,
            _ => PerceptualHueFamily.Red,
        };
    }

    private static bool IsRedHue(double hue) => hue < 35 || hue >= 345;

    private static bool IsWarmHue(double hue) => hue < 130 || hue >= 345;
}