using Fovium.Localization;

namespace Fovium.ColorPicking;

internal enum ProfessionalColorTerm
{
    Lavender,
    Periwinkle,
    Navy,
    Azure,
    SkyBlue,
    Sage,
    Emerald,
    ForestGreen,
    Aquamarine,
    Teal,
    Salmon,
    Wine,
    Rust,
    Scarlet,
    Tangerine,
    Ivory,
    Charcoal,
    Slate
}

[Flags]
internal enum PerceptualRoleSet
{
    None = 0,
    Chromatic = 1,
    Neutral = 2,
    NearNeutral = 4,
    TintedNeutral = 8,
    NearBlack = 16,
    NearWhite = 32
}

internal sealed record ProfessionalShadeDefinition(
    string StableId,
    ProfessionalColorTerm Term,
    string LocalizationKey,
    IReadOnlyList<PerceptualHueFamily> ParentFamilies,
    PerceptualRoleSet Roles,
    double MinimumLightness,
    double MaximumLightness,
    double MinimumChroma,
    double MaximumChroma,
    double MinimumHue,
    double MaximumHue,
    int Priority)
{
    public bool Matches(
        OklchColor color,
        PerceptualColorRole role,
        PerceptualHueFamily family) =>
        ParentFamilies.Contains(family) &&
        Roles.HasFlag(ToRoleSet(role)) &&
        color.L >= MinimumLightness && color.L < MaximumLightness &&
        color.C >= MinimumChroma && color.C < MaximumChroma &&
        ContainsHue(color.HueDegrees);

    internal bool ContainsHue(double hue) => MinimumHue <= MaximumHue
        ? hue >= MinimumHue && hue < MaximumHue
        : hue >= MinimumHue || hue < MaximumHue;

    private static PerceptualRoleSet ToRoleSet(PerceptualColorRole role) => role switch
    {
        PerceptualColorRole.Chromatic => PerceptualRoleSet.Chromatic,
        PerceptualColorRole.Neutral => PerceptualRoleSet.Neutral,
        PerceptualColorRole.NearNeutral => PerceptualRoleSet.NearNeutral,
        PerceptualColorRole.TintedNeutral => PerceptualRoleSet.TintedNeutral,
        PerceptualColorRole.NearBlack => PerceptualRoleSet.NearBlack,
        PerceptualColorRole.NearWhite => PerceptualRoleSet.NearWhite,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}

internal static class ProfessionalShadeCatalog
{
    // These compact OKLCH regions sit above the stable broad-family geometry.
    // Each term is supported by recurring public reference anchors; the table
    // deliberately leaves ambiguous boundary colors to their broad fallback.
    public static IReadOnlyList<ProfessionalShadeDefinition> Definitions { get; } =
    [
        Define("professional-lavender", ProfessionalColorTerm.Lavender, UiStrings.ColorPickerProfessionalLavender,
            [PerceptualHueFamily.BlueViolet, PerceptualHueFamily.Violet, PerceptualHueFamily.PinkLilac],
            PerceptualRoleSet.Chromatic, 0.68, 0.88, 0.06, 0.18, 292, 320, 180),
        Define("professional-periwinkle", ProfessionalColorTerm.Periwinkle,
            UiStrings.ColorPickerProfessionalPeriwinkle, [PerceptualHueFamily.BlueViolet],
            PerceptualRoleSet.Chromatic, 0.55, 0.80, 0.07, 0.21, 272, 292, 179),
        Define("professional-navy", ProfessionalColorTerm.Navy, UiStrings.ColorPickerProfessionalNavy,
            [PerceptualHueFamily.Blue, PerceptualHueFamily.BlueViolet], PerceptualRoleSet.Chromatic,
            0.12, 0.36, 0.04, 0.14, 245, 282, 178),
        Define("professional-azure", ProfessionalColorTerm.Azure, UiStrings.ColorPickerProfessionalAzure,
            [PerceptualHueFamily.CyanBlue, PerceptualHueFamily.Blue], PerceptualRoleSet.Chromatic,
            0.55, 0.72, 0.12, 0.23, 235, 255, 177),
        Define("professional-sky-blue", ProfessionalColorTerm.SkyBlue, UiStrings.ColorPickerProfessionalSkyBlue,
            [PerceptualHueFamily.CyanBlue, PerceptualHueFamily.Blue], PerceptualRoleSet.Chromatic,
            0.72, 0.91, 0.055, 0.17, 235, 260, 176),
        Define("professional-sage", ProfessionalColorTerm.Sage, UiStrings.ColorPickerProfessionalSage,
            [PerceptualHueFamily.OliveGreen, PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.55, 0.82, 0.04, 0.11, 122, 150, 175),
        Define("professional-emerald", ProfessionalColorTerm.Emerald, UiStrings.ColorPickerProfessionalEmerald,
            [PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.48, 0.73, 0.14, 0.26, 138, 166, 174),
        Define("professional-forest-green", ProfessionalColorTerm.ForestGreen,
            UiStrings.ColorPickerProfessionalForestGreen, [PerceptualHueFamily.Green],
            PerceptualRoleSet.Chromatic, 0.25, 0.48, 0.055, 0.17, 125, 166, 173),
        Define("professional-aquamarine", ProfessionalColorTerm.Aquamarine,
            UiStrings.ColorPickerProfessionalAquamarine, [PerceptualHueFamily.Turquoise],
            PerceptualRoleSet.Chromatic, 0.68, 0.91, 0.10, 0.20, 160, 182, 172),
        Define("professional-teal", ProfessionalColorTerm.Teal, UiStrings.ColorPickerProfessionalTeal,
            [PerceptualHueFamily.Turquoise, PerceptualHueFamily.TurquoiseCyan], PerceptualRoleSet.Chromatic,
            0.38, 0.68, 0.055, 0.16, 174, 205, 171),
        Define("professional-salmon", ProfessionalColorTerm.Salmon, UiStrings.ColorPickerProfessionalSalmon,
            [PerceptualHueFamily.Rose, PerceptualHueFamily.Coral, PerceptualHueFamily.RedOrange],
            PerceptualRoleSet.Chromatic, 0.62, 0.85, 0.09, 0.21, 18, 36, 170),
        Define("professional-wine", ProfessionalColorTerm.Wine, UiStrings.ColorPickerProfessionalWine,
            [PerceptualHueFamily.Burgundy, PerceptualHueFamily.Crimson, PerceptualHueFamily.RedMagenta],
            PerceptualRoleSet.Chromatic, 0.25, 0.47, 0.07, 0.19, 345, 18, 169),
        Define("professional-rust", ProfessionalColorTerm.Rust, UiStrings.ColorPickerProfessionalRust,
            [PerceptualHueFamily.Brown], PerceptualRoleSet.Chromatic,
            0.38, 0.56, 0.10, 0.19, 30, 48, 168),
        Define("professional-scarlet", ProfessionalColorTerm.Scarlet, UiStrings.ColorPickerProfessionalScarlet,
            [PerceptualHueFamily.Red, PerceptualHueFamily.Crimson], PerceptualRoleSet.Chromatic,
            0.45, 0.66, 0.18, 0.26, 18, 28.5, 167),
        Define("professional-tangerine", ProfessionalColorTerm.Tangerine,
            UiStrings.ColorPickerProfessionalTangerine,
            [PerceptualHueFamily.Orange, PerceptualHueFamily.Amber], PerceptualRoleSet.Chromatic,
            0.68, 0.86, 0.14, 0.24, 52, 72, 166),
        Define("professional-ivory", ProfessionalColorTerm.Ivory, UiStrings.ColorPickerProfessionalIvory,
            [PerceptualHueFamily.Cream], PerceptualRoleSet.NearWhite,
            0.94, 1.001, 0.015, 0.071, 80, 120, 165),
        Define("professional-slate", ProfessionalColorTerm.Slate, UiStrings.ColorPickerProfessionalSlate,
            [PerceptualHueFamily.BlueGray], PerceptualRoleSet.NearNeutral | PerceptualRoleSet.TintedNeutral,
            0.36, 0.66, 0.018, 0.066, 210, 265, 164),
        Define("professional-charcoal", ProfessionalColorTerm.Charcoal, UiStrings.ColorPickerProfessionalCharcoal,
            [
                PerceptualHueFamily.Neutral, PerceptualHueFamily.WarmGray, PerceptualHueFamily.CoolGray,
                PerceptualHueFamily.BlueGray
            ],
            PerceptualRoleSet.Neutral | PerceptualRoleSet.NearNeutral | PerceptualRoleSet.TintedNeutral,
            0.22, 0.42, 0, 0.022, 0, 360, 163)
    ];

    private static readonly IReadOnlyDictionary<ProfessionalColorTerm, ProfessionalShadeDefinition> ByTerm =
        Definitions.ToDictionary(definition => definition.Term);

    public static ProfessionalShadeDefinition Get(ProfessionalColorTerm term) => ByTerm[term];

    private static ProfessionalShadeDefinition Define(
        string stableId,
        ProfessionalColorTerm term,
        string localizationKey,
        IReadOnlyList<PerceptualHueFamily> parentFamilies,
        PerceptualRoleSet roles,
        double minimumLightness,
        double maximumLightness,
        double minimumChroma,
        double maximumChroma,
        double minimumHue,
        double maximumHue,
        int priority) => new(
        stableId,
        term,
        localizationKey,
        parentFamilies,
        roles,
        minimumLightness,
        maximumLightness,
        minimumChroma,
        maximumChroma,
        minimumHue,
        maximumHue,
        priority);
}

internal static class ProfessionalShadeClassifier
{
    private static readonly IReadOnlyDictionary<PerceptualHueFamily, ProfessionalShadeDefinition[]> ByFamily =
        ProfessionalShadeCatalog.Definitions
            .SelectMany(definition => definition.ParentFamilies.Select(family => (family, definition)))
            .GroupBy(item => item.family)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.definition)
                    .OrderByDescending(definition => definition.Priority)
                    .ThenBy(definition => definition.StableId, StringComparer.Ordinal)
                    .ToArray());

    public static ProfessionalColorTerm? Classify(
        OklchColor color,
        PerceptualColorRole role,
        PerceptualHueFamily family) => ByFamily.GetValueOrDefault(family, [])
        .Where(definition => definition.Matches(color, role, family))
        .Select(definition => (ProfessionalColorTerm?)definition.Term)
        .FirstOrDefault();
}