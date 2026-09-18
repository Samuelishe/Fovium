using System.Globalization;
using Fovium.Localization;

namespace Fovium.ColorPicking;

internal sealed class PerceptualColorNameResolver(Localizer localizer)
{
    public string ResolveShort(PerceptualColorDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (description.IsTransparent)
        {
            return localizer[UiStrings.ColorPickerTransparent];
        }

        if (description.ProfessionalTerm is { } professionalTerm)
        {
            return ResolveProfessionalTerm(professionalTerm);
        }

        return description.Role!.Value switch
        {
            PerceptualColorRole.Neutral => ResolveNeutral(description.LightnessClass!.Value),
            PerceptualColorRole.NearBlack => ResolveNearBlack(description.Undertone!.Value),
            PerceptualColorRole.NearWhite => ResolveNearWhite(description.Undertone!.Value),
            _ => ResolveInformativeShort(description)
        };
    }

    public string ResolveDetailed(PerceptualColorDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (description.IsTransparent)
        {
            return localizer[UiStrings.ColorPickerTransparent];
        }

        if (description.ProfessionalTerm is { } professionalTerm)
        {
            return ResolveProfessionalTerm(professionalTerm);
        }

        if (description.Role is PerceptualColorRole.Neutral or
            PerceptualColorRole.NearBlack or
            PerceptualColorRole.NearWhite)
        {
            return ResolveShort(description);
        }

        var hue = ResolveHue(description.HueFamily!.Value);
        var includeLightness = description.LightnessClass != PerceptualLightnessClass.Medium;
        var includeChroma = description.Role == PerceptualColorRole.Chromatic &&
                            !IsEarthToneWithIntrinsicChroma(description.HueFamily!.Value) &&
                            description.ChromaClass is
                                PerceptualChromaClass.Muted or
                                PerceptualChromaClass.Saturated or
                                PerceptualChromaClass.Vivid;

        if (includeLightness && includeChroma)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                localizer[UiStrings.ColorPickerNameLightnessChromaHue],
                ResolveLightnessModifier(description.LightnessClass!.Value),
                LowercaseFirst(ResolveChromaModifier(description.ChromaClass!.Value)),
                LowercaseFirst(hue));
        }

        if (includeLightness)
        {
            return FormatLightnessHue(description.LightnessClass!.Value, hue);
        }

        if (includeChroma)
        {
            return FormatChromaHue(description.ChromaClass!.Value, hue);
        }

        return hue;
    }

    public string ResolveHue(PerceptualHueFamily hue) => localizer[hue switch
    {
        PerceptualHueFamily.Neutral => UiStrings.ColorPickerHueNeutral,
        PerceptualHueFamily.WarmGray => UiStrings.ColorPickerHueWarmGray,
        PerceptualHueFamily.CoolGray => UiStrings.ColorPickerHueCoolGray,
        PerceptualHueFamily.BlueGray => UiStrings.ColorPickerHueBlueGray,
        PerceptualHueFamily.GreenGray => UiStrings.ColorPickerHueGreenGray,
        PerceptualHueFamily.OliveGray => UiStrings.ColorPickerHueOliveGray,
        PerceptualHueFamily.RoseGray => UiStrings.ColorPickerHueRoseGray,
        PerceptualHueFamily.VioletGray => UiStrings.ColorPickerHueVioletGray,
        PerceptualHueFamily.LilacGray => UiStrings.ColorPickerHueLilacGray,
        PerceptualHueFamily.Greige => UiStrings.ColorPickerHueGreige,
        PerceptualHueFamily.Beige => UiStrings.ColorPickerHueBeige,
        PerceptualHueFamily.Sand => UiStrings.ColorPickerHueSand,
        PerceptualHueFamily.Cream => UiStrings.ColorPickerHueCream,
        PerceptualHueFamily.Peach => UiStrings.ColorPickerHuePeach,
        PerceptualHueFamily.Apricot => UiStrings.ColorPickerHueApricot,
        PerceptualHueFamily.Ochre => UiStrings.ColorPickerHueOchre,
        PerceptualHueFamily.Mustard => UiStrings.ColorPickerHueMustard,
        PerceptualHueFamily.Taupe => UiStrings.ColorPickerHueTaupe,
        PerceptualHueFamily.Terracotta => UiStrings.ColorPickerHueTerracotta,
        PerceptualHueFamily.Red => UiStrings.ColorPickerHueRed,
        PerceptualHueFamily.RedOrange => UiStrings.ColorPickerHueRedOrange,
        PerceptualHueFamily.Coral => UiStrings.ColorPickerHueCoral,
        PerceptualHueFamily.Orange => UiStrings.ColorPickerHueOrange,
        PerceptualHueFamily.Amber => UiStrings.ColorPickerHueAmber,
        PerceptualHueFamily.Yellow => UiStrings.ColorPickerHueYellow,
        PerceptualHueFamily.YellowGreen => UiStrings.ColorPickerHueYellowGreen,
        PerceptualHueFamily.OliveGreen => UiStrings.ColorPickerHueOliveGreen,
        PerceptualHueFamily.Green => UiStrings.ColorPickerHueGreen,
        PerceptualHueFamily.Mint => UiStrings.ColorPickerHueMint,
        PerceptualHueFamily.Turquoise => UiStrings.ColorPickerHueTurquoise,
        PerceptualHueFamily.TurquoiseCyan => UiStrings.ColorPickerHueTurquoiseCyan,
        PerceptualHueFamily.Cyan => UiStrings.ColorPickerHueCyan,
        PerceptualHueFamily.CyanBlue => UiStrings.ColorPickerHueCyanBlue,
        PerceptualHueFamily.Blue => UiStrings.ColorPickerHueBlue,
        PerceptualHueFamily.BlueViolet => UiStrings.ColorPickerHueBlueViolet,
        PerceptualHueFamily.Violet => UiStrings.ColorPickerHueViolet,
        PerceptualHueFamily.PinkLilac => UiStrings.ColorPickerHuePinkLilac,
        PerceptualHueFamily.Magenta => UiStrings.ColorPickerHueMagenta,
        PerceptualHueFamily.RedMagenta => UiStrings.ColorPickerHueRedMagenta,
        PerceptualHueFamily.Pink => UiStrings.ColorPickerHuePink,
        PerceptualHueFamily.Rose => UiStrings.ColorPickerHueRose,
        PerceptualHueFamily.Crimson => UiStrings.ColorPickerHueCrimson,
        PerceptualHueFamily.Burgundy => UiStrings.ColorPickerHueBurgundy,
        PerceptualHueFamily.Brown => UiStrings.ColorPickerHueBrown,
        PerceptualHueFamily.Olive => UiStrings.ColorPickerHueOlive,
        _ => throw new ArgumentOutOfRangeException(nameof(hue))
    }];

    public string ResolveProfessionalTerm(ProfessionalColorTerm term) =>
        localizer[ProfessionalShadeCatalog.Get(term).LocalizationKey];

    public string ResolveDetailToneLabel(PerceptualColorDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        return localizer[description.Role == PerceptualColorRole.Chromatic
            ? UiStrings.ColorPickerDetailHue
            : UiStrings.ColorPickerDetailUndertone];
    }

    public string ResolveDetailTone(PerceptualColorDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (description.IsTransparent)
        {
            throw new ArgumentException("Transparent colors have no perceptual tone.", nameof(description));
        }

        if (description.Role == PerceptualColorRole.Chromatic)
        {
            return ResolveHue(description.HueFamily!.Value);
        }

        return description.HueFamily!.Value switch
        {
            PerceptualHueFamily.Neutral => ResolveHue(PerceptualHueFamily.Neutral),
            PerceptualHueFamily.WarmGray => localizer[UiStrings.ColorPickerUndertoneWarm],
            PerceptualHueFamily.CoolGray => localizer[UiStrings.ColorPickerUndertoneCool],
            PerceptualHueFamily.BlueGray => ResolveHue(PerceptualHueFamily.Blue),
            PerceptualHueFamily.GreenGray => ResolveHue(PerceptualHueFamily.Green),
            PerceptualHueFamily.OliveGray => ResolveHue(PerceptualHueFamily.Olive),
            PerceptualHueFamily.RoseGray => ResolveHue(PerceptualHueFamily.Rose),
            PerceptualHueFamily.VioletGray => ResolveHue(PerceptualHueFamily.Violet),
            PerceptualHueFamily.LilacGray => localizer[UiStrings.ColorPickerUndertoneLilac],
            PerceptualHueFamily.Cream => ResolveHue(PerceptualHueFamily.Cream),
            PerceptualHueFamily.Greige => ResolveHue(PerceptualHueFamily.Beige),
            _ => ResolveHue(description.HueFamily.Value)
        };
    }

    public string ResolveLightness(PerceptualLightnessClass lightness) => localizer[lightness switch
    {
        PerceptualLightnessClass.VeryDark => UiStrings.ColorPickerLightnessVeryDark,
        PerceptualLightnessClass.Dark => UiStrings.ColorPickerLightnessDark,
        PerceptualLightnessClass.Medium => UiStrings.ColorPickerLightnessMedium,
        PerceptualLightnessClass.Light => UiStrings.ColorPickerLightnessLight,
        PerceptualLightnessClass.VeryLight => UiStrings.ColorPickerLightnessVeryLight,
        _ => throw new ArgumentOutOfRangeException(nameof(lightness))
    }];

    public string ResolveChroma(PerceptualChromaClass chroma) => localizer[chroma switch
    {
        PerceptualChromaClass.Neutral => UiStrings.ColorPickerChromaNeutral,
        PerceptualChromaClass.Muted => UiStrings.ColorPickerChromaMuted,
        PerceptualChromaClass.Moderate => UiStrings.ColorPickerChromaModerate,
        PerceptualChromaClass.Saturated => UiStrings.ColorPickerChromaSaturated,
        PerceptualChromaClass.Vivid => UiStrings.ColorPickerChromaVivid,
        _ => throw new ArgumentOutOfRangeException(nameof(chroma))
    }];

    public static string FormatOklch(OklchColor color) => string.Format(
        CultureInfo.InvariantCulture,
        "L {0:0.#}% · C {1:0.000} · h {2:0}°",
        color.L * 100,
        color.C,
        color.HueDegrees);

    private string ResolveInformativeShort(PerceptualColorDescription description)
    {
        var hue = ResolveHue(description.HueFamily!.Value);
        if (description.LightnessClass != PerceptualLightnessClass.Medium)
        {
            return FormatLightnessHue(description.LightnessClass!.Value, hue);
        }

        var hasUsefulChromaModifier = description.Role == PerceptualColorRole.Chromatic &&
                                      IsUsefulChromaModifier(description.ChromaClass) &&
                                      !IsEarthToneWithIntrinsicChroma(description.HueFamily!.Value);
        return hasUsefulChromaModifier
            ? FormatChromaHue(description.ChromaClass.GetValueOrDefault(), hue)
            : hue;
    }

    private string ResolveNeutral(PerceptualLightnessClass lightness) => localizer[lightness switch
    {
        PerceptualLightnessClass.VeryDark => UiStrings.ColorPickerNameBlack,
        PerceptualLightnessClass.Dark => UiStrings.ColorPickerNameDarkGray,
        PerceptualLightnessClass.Medium => UiStrings.ColorPickerNameGray,
        PerceptualLightnessClass.Light => UiStrings.ColorPickerNameLightGray,
        PerceptualLightnessClass.VeryLight => UiStrings.ColorPickerNameWhite,
        _ => throw new ArgumentOutOfRangeException(nameof(lightness))
    }];

    private string ResolveNearBlack(PerceptualUndertone undertone) => localizer[undertone switch
    {
        PerceptualUndertone.None => UiStrings.ColorPickerNameBlack,
        PerceptualUndertone.Red => UiStrings.ColorPickerNameRedBlack,
        PerceptualUndertone.Brown => UiStrings.ColorPickerNameBrownBlack,
        PerceptualUndertone.Olive => UiStrings.ColorPickerNameOliveBlack,
        PerceptualUndertone.Green => UiStrings.ColorPickerNameGreenBlack,
        PerceptualUndertone.Cyan => UiStrings.ColorPickerNameCyanBlack,
        PerceptualUndertone.Blue => UiStrings.ColorPickerNameBlueBlack,
        PerceptualUndertone.Violet => UiStrings.ColorPickerNameVioletBlack,
        PerceptualUndertone.Rose => UiStrings.ColorPickerNameRoseBlack,
        _ => throw new ArgumentOutOfRangeException(nameof(undertone))
    }];

    private string ResolveNearWhite(PerceptualUndertone undertone) => localizer[undertone switch
    {
        PerceptualUndertone.None => UiStrings.ColorPickerNameWhite,
        PerceptualUndertone.Red => UiStrings.ColorPickerNameRedWhite,
        PerceptualUndertone.Brown or PerceptualUndertone.Olive => UiStrings.ColorPickerNameCreamWhite,
        PerceptualUndertone.Green => UiStrings.ColorPickerNameGreenWhite,
        PerceptualUndertone.Cyan => UiStrings.ColorPickerNameCyanWhite,
        PerceptualUndertone.Blue => UiStrings.ColorPickerNameBlueWhite,
        PerceptualUndertone.Violet => UiStrings.ColorPickerNameVioletWhite,
        PerceptualUndertone.Rose => UiStrings.ColorPickerNamePinkWhite,
        _ => throw new ArgumentOutOfRangeException(nameof(undertone))
    }];

    private string FormatLightnessHue(PerceptualLightnessClass lightness, string hue) => string.Format(
        CultureInfo.CurrentCulture,
        localizer[UiStrings.ColorPickerNameLightnessHue],
        ResolveLightnessModifier(lightness),
        LowercaseFirst(hue));

    private string FormatChromaHue(PerceptualChromaClass chroma, string hue) => string.Format(
        CultureInfo.CurrentCulture,
        localizer[UiStrings.ColorPickerNameChromaHue],
        ResolveChromaModifier(chroma),
        LowercaseFirst(hue));

    private string ResolveLightnessModifier(PerceptualLightnessClass lightness) =>
        localizer[lightness switch
        {
            PerceptualLightnessClass.VeryDark => UiStrings.ColorPickerModifierVeryDark,
            PerceptualLightnessClass.Dark => UiStrings.ColorPickerModifierDark,
            PerceptualLightnessClass.Medium => UiStrings.ColorPickerModifierMedium,
            PerceptualLightnessClass.Light => UiStrings.ColorPickerModifierLight,
            PerceptualLightnessClass.VeryLight => UiStrings.ColorPickerModifierVeryLight,
            _ => throw new ArgumentOutOfRangeException(nameof(lightness))
        }];

    private string ResolveChromaModifier(PerceptualChromaClass chroma) => localizer[chroma switch
    {
        PerceptualChromaClass.Neutral => UiStrings.ColorPickerModifierNeutral,
        PerceptualChromaClass.Muted => UiStrings.ColorPickerModifierMuted,
        PerceptualChromaClass.Moderate => UiStrings.ColorPickerModifierModerate,
        PerceptualChromaClass.Saturated => UiStrings.ColorPickerModifierSaturated,
        PerceptualChromaClass.Vivid => UiStrings.ColorPickerModifierVivid,
        _ => throw new ArgumentOutOfRangeException(nameof(chroma))
    }];

    private static bool IsEarthToneWithIntrinsicChroma(PerceptualHueFamily hue) => hue is
        PerceptualHueFamily.Greige or
        PerceptualHueFamily.Beige or
        PerceptualHueFamily.Sand or
        PerceptualHueFamily.Cream or
        PerceptualHueFamily.Peach or
        PerceptualHueFamily.Apricot or
        PerceptualHueFamily.Ochre or
        PerceptualHueFamily.Mustard or
        PerceptualHueFamily.Taupe or
        PerceptualHueFamily.Terracotta;

    private static bool IsUsefulChromaModifier(PerceptualChromaClass? chroma) => chroma is
        PerceptualChromaClass.Muted or
        PerceptualChromaClass.Saturated or
        PerceptualChromaClass.Vivid;

    private static string LowercaseFirst(string value) => value.Length == 0
        ? value
        : char.ToLowerInvariant(value[0]) + value[1..];
}