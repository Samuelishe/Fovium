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

        var hue = description.HueFamily!.Value;
        if (hue == PerceptualHueFamily.Neutral)
        {
            return localizer[description.LightnessClass!.Value switch
            {
                PerceptualLightnessClass.VeryDark => UiStrings.ColorPickerNameBlack,
                PerceptualLightnessClass.Dark => UiStrings.ColorPickerNameDarkGray,
                PerceptualLightnessClass.Medium => UiStrings.ColorPickerNameGray,
                PerceptualLightnessClass.Light => UiStrings.ColorPickerNameLightGray,
                PerceptualLightnessClass.VeryLight => UiStrings.ColorPickerNameWhite,
                _ => throw new ArgumentOutOfRangeException(nameof(description))
            }];
        }

        return ResolveHue(hue);
    }

    public string ResolveDetailed(PerceptualColorDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (description.IsTransparent)
        {
            return localizer[UiStrings.ColorPickerTransparent];
        }

        var shortName = ResolveShort(description);
        var hue = description.HueFamily!.Value;
        if (hue == PerceptualHueFamily.Neutral)
        {
            return shortName;
        }

        var includeLightness = description.LightnessClass != PerceptualLightnessClass.Medium;
        var includeChroma = description.ChromaClass is
            PerceptualChromaClass.Muted or
            PerceptualChromaClass.Saturated or
            PerceptualChromaClass.Vivid;
        if (hue is PerceptualHueFamily.WarmGray or PerceptualHueFamily.CoolGray)
        {
            includeChroma = false;
        }

        if (includeLightness && includeChroma)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                localizer[UiStrings.ColorPickerNameLightnessChromaHue],
                ResolveLightnessModifier(description.LightnessClass!.Value),
                LowercaseFirst(ResolveChromaModifier(description.ChromaClass!.Value)),
                LowercaseFirst(shortName));
        }

        if (includeLightness)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                localizer[UiStrings.ColorPickerNameLightnessHue],
                ResolveLightnessModifier(description.LightnessClass!.Value),
                LowercaseFirst(shortName));
        }

        if (includeChroma)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                localizer[UiStrings.ColorPickerNameChromaHue],
                ResolveChromaModifier(description.ChromaClass!.Value),
                LowercaseFirst(shortName));
        }

        return shortName;
    }

    public string ResolveHue(PerceptualHueFamily hue) => localizer[hue switch
    {
        PerceptualHueFamily.Neutral => UiStrings.ColorPickerHueNeutral,
        PerceptualHueFamily.WarmGray => UiStrings.ColorPickerHueWarmGray,
        PerceptualHueFamily.CoolGray => UiStrings.ColorPickerHueCoolGray,
        PerceptualHueFamily.Red => UiStrings.ColorPickerHueRed,
        PerceptualHueFamily.Coral => UiStrings.ColorPickerHueCoral,
        PerceptualHueFamily.Orange => UiStrings.ColorPickerHueOrange,
        PerceptualHueFamily.Amber => UiStrings.ColorPickerHueAmber,
        PerceptualHueFamily.Yellow => UiStrings.ColorPickerHueYellow,
        PerceptualHueFamily.YellowGreen => UiStrings.ColorPickerHueYellowGreen,
        PerceptualHueFamily.Green => UiStrings.ColorPickerHueGreen,
        PerceptualHueFamily.Turquoise => UiStrings.ColorPickerHueTurquoise,
        PerceptualHueFamily.Cyan => UiStrings.ColorPickerHueCyan,
        PerceptualHueFamily.Blue => UiStrings.ColorPickerHueBlue,
        PerceptualHueFamily.BlueViolet => UiStrings.ColorPickerHueBlueViolet,
        PerceptualHueFamily.Violet => UiStrings.ColorPickerHueViolet,
        PerceptualHueFamily.Magenta => UiStrings.ColorPickerHueMagenta,
        PerceptualHueFamily.Pink => UiStrings.ColorPickerHuePink,
        PerceptualHueFamily.Burgundy => UiStrings.ColorPickerHueBurgundy,
        PerceptualHueFamily.Brown => UiStrings.ColorPickerHueBrown,
        PerceptualHueFamily.Olive => UiStrings.ColorPickerHueOlive,
        _ => throw new ArgumentOutOfRangeException(nameof(hue))
    }];

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
        "{0:0.#}% · {1:0.000} · {2:0}°",
        color.L * 100,
        color.C,
        color.HueDegrees);

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

    private static string LowercaseFirst(string value) => value.Length == 0
        ? value
        : char.ToLowerInvariant(value[0]) + value[1..];
}