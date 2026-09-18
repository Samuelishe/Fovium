namespace Fovium.ColorPicking;

internal enum PerceptualColorRole
{
    Chromatic,
    Neutral,
    NearNeutral,
    TintedNeutral,
    NearBlack,
    NearWhite
}

internal enum PerceptualUndertone
{
    None,
    Red,
    Brown,
    Olive,
    Green,
    Cyan,
    Blue,
    Violet,
    Rose
}

internal enum PerceptualHueFamily
{
    Neutral,
    WarmGray,
    CoolGray,
    BlueGray,
    GreenGray,
    OliveGray,
    RoseGray,
    VioletGray,
    LilacGray,
    Greige,
    Beige,
    Sand,
    Cream,
    Peach,
    Apricot,
    Ochre,
    Mustard,
    Taupe,
    Terracotta,
    Red,
    RedOrange,
    Coral,
    Orange,
    Amber,
    Yellow,
    YellowGreen,
    OliveGreen,
    Green,
    Mint,
    Turquoise,
    TurquoiseCyan,
    Cyan,
    CyanBlue,
    Blue,
    BlueViolet,
    Violet,
    PinkLilac,
    Magenta,
    RedMagenta,
    Pink,
    Rose,
    Crimson,
    Burgundy,
    Brown,
    Olive
}

internal enum PerceptualLightnessClass
{
    VeryDark,
    Dark,
    Medium,
    Light,
    VeryLight
}

internal enum PerceptualChromaClass
{
    Neutral,
    Muted,
    Moderate,
    Saturated,
    Vivid
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
    PerceptualColorRole? Role,
    PerceptualUndertone? Undertone,
    PerceptualHueFamily? HueFamily,
    PerceptualLightnessClass? LightnessClass,
    PerceptualChromaClass? ChromaClass)
{
    public bool IsTransparent => Oklch is null;

    public static PerceptualColorDescription Transparent { get; } =
        new(null, null, null, null, null, null);
}

internal static class PerceptualColorClassifier
{
    // Absolute OKLCH chroma alone does not describe photographic neutral roles.
    // Dedicated black/white gates own the extremes; these symmetric triangular
    // curves keep gray-cast bands narrow there and bounded around mid-gray.
    internal const double TrueNeutralChromaMaximum = 0.008;
    internal const double NearBlackLightnessMaximum = 0.20;
    internal const double NearBlackChromaMaximum = 0.055;
    internal const double NearWhiteLightnessMinimum = 0.90;
    internal const double NearWhiteChromaMaximum = 0.070;
    private const double SubtleUndertoneBase = 0.014;
    private const double SubtleUndertoneMidtoneGain = 0.008;
    private const double TintedNeutralBase = 0.030;
    private const double TintedNeutralMidtoneGain = 0.022;
    private const double BlueVioletNeutralAllowance = 0.010;
    private const double NearBlackUndertoneFloor = 0.010;
    private const double NearBlackUndertoneShadowGain = 0.060;
    private const double NearWhiteUndertoneFloor = 0.012;

    // Warm earth names are useful only in the bounded low/moderate-chroma region.
    // The ordered bands prevent nearby beige materials from falling through to
    // orange/olive while leaving darker olive and saturated warm colors intact.
    internal const double WarmNeutralChromaMinimum = 0.015;
    internal const double GreigeChromaMaximum = 0.038;
    internal const double BeigeChromaMaximum = 0.055;
    internal const double SandChromaMaximum = 0.090;
    internal const double MustardChromaMinimum = 0.100;
    internal const double OchreHueMinimum = 55;
    internal const double OchreDarkHueMinimum = 82;
    internal const double OchreHueMaximum = 95;
    internal const double OchreLightnessMinimum = 0.30;
    internal const double OchreChromaMinimum = 0.035;
    internal const double PeachLightnessMinimum = 0.760;
    internal const double ApricotLightnessMinimum = 0.860;
    internal const double ApricotChromaMinimum = 0.065;
    internal const double MintLightnessMinimum = 0.860;
    internal const double MintChromaMinimum = 0.070;
    internal const double MintChromaMaximum = 0.130;
    internal const double MintHueMinimum = 140;
    internal const double MintHueMaximum = 180;
    internal const double TerracottaLightnessMinimum = 0.550;
    internal const double CoralHueMaximum = 43;
    internal const double BurgundyHueMinimum = 350;
    internal const double RoseHueMaximum = 32;
    internal const double VioletHueMaximum = 330;
    internal const double VividMagentaChromaMinimum = 0.240;
    internal const double VioletGrayHueMinimum = 285;
    internal const double LilacGrayHueMinimum = 300;

    public static PerceptualColorDescription Describe(ColorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.IsTransparent)
        {
            return PerceptualColorDescription.Transparent;
        }

        var oklch = OklchColor.FromSrgb(sample.Red, sample.Green, sample.Blue);
        var role = ClassifyRole(oklch);
        var undertone = ClassifyUndertone(oklch, role);
        return new PerceptualColorDescription(
            oklch,
            role,
            undertone,
            ClassifyHue(oklch, role, undertone),
            ClassifyLightness(oklch.L),
            ClassifyChroma(oklch.C));
    }

    internal static PerceptualHueFamily ClassifyHue(OklchColor color)
    {
        var role = ClassifyRole(color);
        return ClassifyHue(color, role, ClassifyUndertone(color, role));
    }

    internal static PerceptualColorRole ClassifyRole(OklchColor color)
    {
        if (color.L < NearBlackLightnessMaximum && color.C < NearBlackChromaMaximum)
        {
            return PerceptualColorRole.NearBlack;
        }

        if (color.L >= NearWhiteLightnessMinimum && color.C < NearWhiteChromaMaximum)
        {
            return PerceptualColorRole.NearWhite;
        }

        if (color.C < TrueNeutralChromaMaximum)
        {
            return PerceptualColorRole.Neutral;
        }

        if (color.C < SubtleUndertoneLimit(color.L))
        {
            return PerceptualColorRole.NearNeutral;
        }

        return color.C < TintedNeutralLimit(color.L, color.HueDegrees)
            ? PerceptualColorRole.TintedNeutral
            : PerceptualColorRole.Chromatic;
    }

    internal static double SubtleUndertoneLimit(double lightness) =>
        SubtleUndertoneBase + SubtleUndertoneMidtoneGain * LightnessEnvelope(lightness);

    internal static double TintedNeutralLimit(double lightness, double hueDegrees)
    {
        var limit = TintedNeutralBase + TintedNeutralMidtoneGain * LightnessEnvelope(lightness);
        if (hueDegrees is >= 205 and < 310)
        {
            limit += BlueVioletNeutralAllowance;
        }

        return limit;
    }

    internal static double OchreHueMinimumAt(double lightness)
    {
        var transition = Math.Clamp((lightness - 0.42) / 0.18, 0, 1);
        return OchreDarkHueMinimum - transition * (OchreDarkHueMinimum - OchreHueMinimum);
    }

    internal static PerceptualLightnessClass ClassifyLightness(double lightness) => lightness switch
    {
        < 0.25 => PerceptualLightnessClass.VeryDark,
        < 0.45 => PerceptualLightnessClass.Dark,
        < 0.72 => PerceptualLightnessClass.Medium,
        < 0.88 => PerceptualLightnessClass.Light,
        _ => PerceptualLightnessClass.VeryLight
    };

    internal static PerceptualChromaClass ClassifyChroma(double chroma) => chroma switch
    {
        < 0.025 => PerceptualChromaClass.Neutral,
        < 0.07 => PerceptualChromaClass.Muted,
        < 0.14 => PerceptualChromaClass.Moderate,
        < 0.24 => PerceptualChromaClass.Saturated,
        _ => PerceptualChromaClass.Vivid
    };

    private static PerceptualUndertone ClassifyUndertone(
        OklchColor color,
        PerceptualColorRole role)
    {
        if (role is PerceptualColorRole.Neutral or PerceptualColorRole.Chromatic)
        {
            return PerceptualUndertone.None;
        }

        if ((role == PerceptualColorRole.NearBlack &&
             color.C < NearBlackUndertoneFloor +
             Math.Max(0, 0.18 - color.L) * NearBlackUndertoneShadowGain) ||
            (role == PerceptualColorRole.NearWhite && color.C < NearWhiteUndertoneFloor))
        {
            return PerceptualUndertone.None;
        }

        return color.HueDegrees switch
        {
            < 20 => PerceptualUndertone.Red,
            < 65 => PerceptualUndertone.Brown,
            < 125 => PerceptualUndertone.Olive,
            < 170 => PerceptualUndertone.Green,
            < 210 => PerceptualUndertone.Cyan,
            < 275 => PerceptualUndertone.Blue,
            < 325 => PerceptualUndertone.Violet,
            < 345 => PerceptualUndertone.Rose,
            _ => PerceptualUndertone.Red
        };
    }

    private static PerceptualHueFamily ClassifyHue(
        OklchColor color,
        PerceptualColorRole role,
        PerceptualUndertone undertone)
    {
        if (role == PerceptualColorRole.Neutral ||
            (undertone == PerceptualUndertone.None &&
             role is PerceptualColorRole.NearBlack or PerceptualColorRole.NearWhite))
        {
            return PerceptualHueFamily.Neutral;
        }

        if (role is PerceptualColorRole.NearNeutral or PerceptualColorRole.TintedNeutral)
        {
            if (ClassifyWarmEarthTone(color, role) is { } earthTone)
            {
                return earthTone;
            }

            return undertone switch
            {
                PerceptualUndertone.Red or PerceptualUndertone.Rose => PerceptualHueFamily.RoseGray,
                PerceptualUndertone.Brown => PerceptualHueFamily.WarmGray,
                PerceptualUndertone.Olive => PerceptualHueFamily.OliveGray,
                PerceptualUndertone.Green => PerceptualHueFamily.GreenGray,
                PerceptualUndertone.Blue => PerceptualHueFamily.BlueGray,
                PerceptualUndertone.Violet when color.HueDegrees < VioletGrayHueMinimum =>
                    PerceptualHueFamily.BlueGray,
                PerceptualUndertone.Violet when color.HueDegrees < LilacGrayHueMinimum =>
                    PerceptualHueFamily.VioletGray,
                PerceptualUndertone.Violet => PerceptualHueFamily.LilacGray,
                _ => PerceptualHueFamily.CoolGray
            };
        }

        if (role is PerceptualColorRole.NearBlack or PerceptualColorRole.NearWhite)
        {
            return undertone switch
            {
                PerceptualUndertone.Red => PerceptualHueFamily.Red,
                PerceptualUndertone.Brown => role == PerceptualColorRole.NearWhite
                    ? PerceptualHueFamily.Cream
                    : PerceptualHueFamily.Brown,
                PerceptualUndertone.Olive => role == PerceptualColorRole.NearWhite
                    ? PerceptualHueFamily.Cream
                    : PerceptualHueFamily.Olive,
                PerceptualUndertone.Green => PerceptualHueFamily.Green,
                PerceptualUndertone.Cyan => PerceptualHueFamily.Cyan,
                PerceptualUndertone.Blue => PerceptualHueFamily.Blue,
                PerceptualUndertone.Violet => PerceptualHueFamily.Violet,
                PerceptualUndertone.Rose => PerceptualHueFamily.Rose,
                _ => PerceptualHueFamily.Neutral
            };
        }

        if (ClassifyWarmEarthTone(color, role) is { } chromaticEarthTone)
        {
            return chromaticEarthTone;
        }

        // Mint is a light chromatic green-to-blue-green role. The lower chroma
        // bound deliberately meets the near-white gate, while the upper bound
        // leaves stronger aquamarine/turquoise samples in their spectral family.
        if (role == PerceptualColorRole.Chromatic &&
            color.HueDegrees is >= MintHueMinimum and < MintHueMaximum &&
            color.L >= MintLightnessMinimum &&
            color.C is >= MintChromaMinimum and < MintChromaMaximum)
        {
            return PerceptualHueFamily.Mint;
        }

        if ((color.HueDegrees >= BurgundyHueMinimum || color.HueDegrees < 35) &&
            color.L < 0.50 &&
            color.C < 0.14)
        {
            return PerceptualHueFamily.Burgundy;
        }

        if ((color.HueDegrees >= 345 || color.HueDegrees < 20) &&
            color.L < 0.62 &&
            color.C >= 0.11)
        {
            return PerceptualHueFamily.Crimson;
        }

        if ((color.HueDegrees >= 345 || color.HueDegrees < RoseHueMaximum) &&
            color.L >= 0.52 &&
            color.C < 0.12)
        {
            return PerceptualHueFamily.Rose;
        }

        if (color.HueDegrees is >= 35 and < 85 && color.L < 0.62 && color.C < 0.18)
        {
            return PerceptualHueFamily.Brown;
        }

        if (color.HueDegrees is >= OchreHueMaximum and < 120 && color.L < 0.72 && color.C < 0.18)
        {
            return PerceptualHueFamily.Olive;
        }

        if (color.HueDegrees is >= 120 and < 135 && color.L < 0.72 && color.C < 0.17)
        {
            return PerceptualHueFamily.OliveGreen;
        }

        if (color.HueDegrees is >= 20 and < CoralHueMaximum &&
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

        if (color.HueDegrees is >= 322 and < 340 &&
            color.L >= 0.70 &&
            color.C < 0.18)
        {
            return PerceptualHueFamily.PinkLilac;
        }

        if (color.HueDegrees is >= 322 and < VioletHueMaximum &&
            color.C >= VividMagentaChromaMinimum)
        {
            return PerceptualHueFamily.Magenta;
        }

        return color.HueDegrees switch
        {
            < 38 => PerceptualHueFamily.Red,
            < 48 => PerceptualHueFamily.RedOrange,
            < 70 => PerceptualHueFamily.Orange,
            < 92 => PerceptualHueFamily.Amber,
            < 112 => PerceptualHueFamily.Yellow,
            < 135 => PerceptualHueFamily.YellowGreen,
            < 165 => PerceptualHueFamily.Green,
            < 190 => PerceptualHueFamily.Turquoise,
            < 205 => PerceptualHueFamily.TurquoiseCyan,
            < 230 => PerceptualHueFamily.Cyan,
            < 245 => PerceptualHueFamily.CyanBlue,
            < 270 => PerceptualHueFamily.Blue,
            < 307 => PerceptualHueFamily.BlueViolet,
            < VioletHueMaximum => PerceptualHueFamily.Violet,
            < 340 => PerceptualHueFamily.Magenta,
            < 355 => PerceptualHueFamily.RedMagenta,
            _ => PerceptualHueFamily.Red
        };
    }

    private static double LightnessEnvelope(double lightness) =>
        1 - Math.Abs((2 * Math.Clamp(lightness, 0, 1)) - 1);

    private static PerceptualHueFamily? ClassifyWarmEarthTone(
        OklchColor color,
        PerceptualColorRole role)
    {
        var hue = color.HueDegrees;

        if (role is PerceptualColorRole.NearNeutral or PerceptualColorRole.TintedNeutral)
        {
            if (hue is >= 35 and < 65 &&
                color.L is >= 0.35 and < 0.62 &&
                color.C is >= 0.025 and < BeigeChromaMaximum)
            {
                return PerceptualHueFamily.Taupe;
            }

            if (hue is >= 45 and < 100 &&
                color.L is >= 0.55 and < 0.88 &&
                color.C is >= WarmNeutralChromaMinimum and < GreigeChromaMaximum)
            {
                return PerceptualHueFamily.Greige;
            }

            if (hue is >= 45 and < 95 &&
                color.L is >= 0.62 and < 0.90 &&
                color.C is >= GreigeChromaMaximum and < BeigeChromaMaximum)
            {
                return PerceptualHueFamily.Beige;
            }
        }

        if (role == PerceptualColorRole.Chromatic)
        {
            if (hue is >= 35 and < 58 &&
                color.L >= PeachLightnessMinimum &&
                color.C is >= 0.035 and < 0.15)
            {
                return PerceptualHueFamily.Peach;
            }

            if (hue is >= 58 and < 82 &&
                color.L >= ApricotLightnessMinimum &&
                color.C is >= ApricotChromaMinimum and < 0.12)
            {
                return PerceptualHueFamily.Apricot;
            }

            if (hue is >= 35 and < 55 &&
                color.L is >= TerracottaLightnessMinimum and < PeachLightnessMinimum &&
                color.C is >= 0.06 and < 0.16)
            {
                return PerceptualHueFamily.Terracotta;
            }

            if (hue is >= 45 and < OchreHueMaximum && color.L is >= 0.62 and < 0.90)
            {
                if (color.C < GreigeChromaMaximum)
                {
                    return PerceptualHueFamily.Greige;
                }

                if (color.C < BeigeChromaMaximum)
                {
                    return PerceptualHueFamily.Beige;
                }

                if (color.C < SandChromaMaximum)
                {
                    return PerceptualHueFamily.Sand;
                }
            }

            if (hue is >= 82 and < 90 &&
                color.L is >= 0.58 and < 0.78 &&
                color.C is >= MustardChromaMinimum and < 0.18)
            {
                return PerceptualHueFamily.Mustard;
            }
        }

        if ((role is PerceptualColorRole.Chromatic or PerceptualColorRole.NearNeutral or
                PerceptualColorRole.TintedNeutral) &&
            hue >= OchreHueMinimumAt(color.L) && hue < OchreHueMaximum &&
            color.L is >= OchreLightnessMinimum and < 0.78 &&
            color.C is >= OchreChromaMinimum and < 0.17)
        {
            return PerceptualHueFamily.Ochre;
        }

        return null;
    }
}