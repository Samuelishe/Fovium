using Fovium.Localization;

namespace Fovium.ColorPicking;

internal enum ProfessionalColorTerm
{
    Heliotrope,
    SlateBlue,
    SpringGreen,
    PineGreen,
    BrickRed,
    RawSienna,
    RawUmber,
    Canary,
    Gamboge,
    Ecru,
    Buff,
    Goldenrod,
    Russet,
    Heather,
    Ruby,
    Cranberry,
    Viridian,
    Celadon,
    AntiqueWhite,
    Vanilla,
    NaplesYellow,
    Espresso,
    Lilac,
    Mauve,
    Plum,
    Orchid,
    Amethyst,
    Aubergine,
    CornflowerBlue,
    MidnightBlue,
    PrussianBlue,
    Ultramarine,
    BabyBlue,
    PetrolBlue,
    Moss,
    HunterGreen,
    Fern,
    Avocado,
    SeaGreen,
    Saffron,
    Marigold,
    Chocolate,
    Cinnamon,
    Sienna,
    BurntSienna,
    Sepia,
    Chestnut,
    Vermilion,
    Carmine,
    Tomato,
    Eggshell,
    Mushroom,
    Gunmetal,
    Gold,
    Khaki,
    Copper,
    Mahogany,
    Caramel,
    Lemon,
    Jade,
    RoyalBlue,
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
    Slate,
    Indigo,
    PowderBlue,
    SteelBlue,
    OliveDrab,
    Lime,
    Chartreuse,
    Seafoam,
    Cobalt,
    Cerulean,
    BloodOrange,
    Pumpkin,
    Blush,
    Pistachio,
    Linen,
    Silver
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
    IReadOnlyList<ProfessionalShadeRegionDefinition> Regions);

internal sealed record ProfessionalShadeRegionDefinition(
    string StableId,
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
        PerceptualHueFamily family) => FailureReason(color, role, family) is null;

    public string? FailureReason(
        OklchColor color,
        PerceptualColorRole role,
        PerceptualHueFamily family)
    {
        if (!ParentFamilies.Contains(family))
        {
            return "parent-family";
        }

        if (!Roles.HasFlag(ToRoleSet(role)))
        {
            return "role";
        }

        if (color.L < MinimumLightness || color.L >= MaximumLightness)
        {
            return "lightness";
        }

        if (color.C < MinimumChroma || color.C >= MaximumChroma)
        {
            return "chroma";
        }

        return ContainsHue(color.HueDegrees) ? null : "hue";
    }

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

internal sealed record ProfessionalShadeMatch(
    ProfessionalColorTerm Term,
    string TermStableId,
    string RegionStableId,
    int Priority);

internal sealed record ProfessionalShadeRegionEvaluation(
    ProfessionalColorTerm Term,
    string TermStableId,
    string RegionStableId,
    int Priority,
    bool Matched,
    string? FailureReason);

internal sealed record ProfessionalShadeExplanation(
    ProfessionalShadeMatch? Winner,
    IReadOnlyList<ProfessionalShadeRegionEvaluation> Candidates);

internal static class ProfessionalShadeCatalog
{
    // These compact OKLCH regions sit above the stable broad-family geometry.
    // Each term is supported by recurring public reference anchors; the table
    // deliberately leaves ambiguous boundary colors to their broad fallback.
    public static IReadOnlyList<ProfessionalShadeDefinition> Definitions { get; } =
    [
        Define("professional-heliotrope", ProfessionalColorTerm.Heliotrope,
            UiStrings.ColorPickerProfessionalHeliotrope,
            [PerceptualHueFamily.Violet, PerceptualHueFamily.Magenta], PerceptualRoleSet.Chromatic,
            0.62, 0.76, 0.20, 0.31, 310, 330, 420),
        Define("professional-slate-blue", ProfessionalColorTerm.SlateBlue,
            UiStrings.ColorPickerProfessionalSlateBlue,
            [PerceptualHueFamily.BlueViolet], PerceptualRoleSet.Chromatic,
            0.56, 0.64, 0.15, 0.23, 280, 292, 418),
        Define("professional-spring-green", ProfessionalColorTerm.SpringGreen,
            UiStrings.ColorPickerProfessionalSpringGreen,
            [PerceptualHueFamily.YellowGreen, PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.82, 0.94, 0.19, 0.29, 143, 160, 416),
        Define("professional-pine-green", ProfessionalColorTerm.PineGreen,
            UiStrings.ColorPickerProfessionalPineGreen,
            [PerceptualHueFamily.Green, PerceptualHueFamily.Turquoise], PerceptualRoleSet.Chromatic,
            0.45, 0.58, 0.07, 0.12, 175, 194, 415),
        DefineComposite("professional-brick-red", ProfessionalColorTerm.BrickRed,
            UiStrings.ColorPickerProfessionalBrickRed,
            Region("professional-brick-red-deep",
                [PerceptualHueFamily.Red, PerceptualHueFamily.Brown], PerceptualRoleSet.Chromatic,
                0.36, 0.48, 0.13, 0.20, 25, 38, 412),
            Region("professional-brick-red-crimson",
                [PerceptualHueFamily.Crimson, PerceptualHueFamily.Red], PerceptualRoleSet.Chromatic,
                0.52, 0.63, 0.14, 0.21, 10, 24, 411)),
        Define("professional-raw-sienna", ProfessionalColorTerm.RawSienna,
            UiStrings.ColorPickerProfessionalRawSienna,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Ochre], PerceptualRoleSet.Chromatic,
            0.51, 0.57, 0.105, 0.135, 68, 75, 410),
        Define("professional-raw-umber", ProfessionalColorTerm.RawUmber,
            UiStrings.ColorPickerProfessionalRawUmber,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Ochre], PerceptualRoleSet.Chromatic,
            0.51, 0.59, 0.105, 0.15, 56, 63, 409),
        Define("professional-canary", ProfessionalColorTerm.Canary,
            UiStrings.ColorPickerProfessionalCanary,
            [PerceptualHueFamily.Yellow], PerceptualRoleSet.Chromatic,
            0.96, 0.995, 0.14, 0.195, 106, 114, 408),
        Define("professional-gamboge", ProfessionalColorTerm.Gamboge,
            UiStrings.ColorPickerProfessionalGamboge,
            [PerceptualHueFamily.Ochre, PerceptualHueFamily.Amber], PerceptualRoleSet.Chromatic,
            0.72, 0.75, 0.14, 0.18, 74, 80, 407),
        Define("professional-ecru", ProfessionalColorTerm.Ecru, UiStrings.ColorPickerProfessionalEcru,
            [PerceptualHueFamily.Sand, PerceptualHueFamily.Beige], PerceptualRoleSet.Chromatic,
            0.72, 0.81, 0.05, 0.09, 86, 99, 406),
        Define("professional-buff", ProfessionalColorTerm.Buff, UiStrings.ColorPickerProfessionalBuff,
            [PerceptualHueFamily.Beige, PerceptualHueFamily.Sand], PerceptualRoleSet.Chromatic,
            0.79, 0.88, 0.03, 0.065, 70, 87, 405),
        Define("professional-goldenrod", ProfessionalColorTerm.Goldenrod,
            UiStrings.ColorPickerProfessionalGoldenrod,
            [PerceptualHueFamily.Mustard, PerceptualHueFamily.Amber], PerceptualRoleSet.Chromatic,
            0.71, 0.79, 0.13, 0.18, 80, 88, 403),
        Define("professional-russet", ProfessionalColorTerm.Russet, UiStrings.ColorPickerProfessionalRusset,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Terracotta], PerceptualRoleSet.Chromatic,
            0.39, 0.49, 0.06, 0.115, 35, 49, 402),
        Define("professional-heather", ProfessionalColorTerm.Heather, UiStrings.ColorPickerProfessionalHeather,
            [PerceptualHueFamily.RoseGray, PerceptualHueFamily.LilacGray],
            PerceptualRoleSet.NearNeutral | PerceptualRoleSet.TintedNeutral,
            0.61, 0.70, 0.008, 0.025, 325, 355, 401),
        Define("professional-ruby", ProfessionalColorTerm.Ruby, UiStrings.ColorPickerProfessionalRuby,
            [PerceptualHueFamily.Red, PerceptualHueFamily.Crimson, PerceptualHueFamily.Coral],
            PerceptualRoleSet.Chromatic, 0.46, 0.63, 0.17, 0.27, 5, 23, 344),
        Define("professional-cranberry", ProfessionalColorTerm.Cranberry,
            UiStrings.ColorPickerProfessionalCranberry,
            [PerceptualHueFamily.Red, PerceptualHueFamily.Crimson, PerceptualHueFamily.Burgundy],
            PerceptualRoleSet.Chromatic, 0.42, 0.50, 0.135, 0.22, 355, 19, 343),
        Define("professional-viridian", ProfessionalColorTerm.Viridian,
            UiStrings.ColorPickerProfessionalViridian,
            [PerceptualHueFamily.Green, PerceptualHueFamily.Turquoise], PerceptualRoleSet.Chromatic,
            0.45, 0.62, 0.07, 0.13, 155, 178, 342),
        Define("professional-celadon", ProfessionalColorTerm.Celadon, UiStrings.ColorPickerProfessionalCeladon,
            [PerceptualHueFamily.YellowGreen, PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.80, 0.95, 0.045, 0.125, 132, 158, 341),
        Define("professional-antique-white", ProfessionalColorTerm.AntiqueWhite,
            UiStrings.ColorPickerProfessionalAntiqueWhite,
            [PerceptualHueFamily.Cream], PerceptualRoleSet.NearWhite,
            0.935, 0.985, 0.018, 0.052, 52, 86, 338),
        Define("professional-vanilla", ProfessionalColorTerm.Vanilla, UiStrings.ColorPickerProfessionalVanilla,
            [PerceptualHueFamily.Cream, PerceptualHueFamily.Yellow],
            PerceptualRoleSet.Chromatic | PerceptualRoleSet.NearWhite,
            0.88, 0.965, 0.05, 0.11, 86, 109, 337),
        Define("professional-naples-yellow", ProfessionalColorTerm.NaplesYellow,
            UiStrings.ColorPickerProfessionalNaplesYellow,
            [PerceptualHueFamily.Amber, PerceptualHueFamily.Yellow], PerceptualRoleSet.Chromatic,
            0.85, 0.94, 0.105, 0.18, 88, 98, 336),
        Define("professional-espresso", ProfessionalColorTerm.Espresso, UiStrings.ColorPickerProfessionalEspresso,
            [PerceptualHueFamily.WarmGray, PerceptualHueFamily.Taupe, PerceptualHueFamily.Brown],
            PerceptualRoleSet.TintedNeutral | PerceptualRoleSet.Chromatic,
            0.21, 0.40, 0.018, 0.07, 15, 61, 335),
        Define("professional-lilac", ProfessionalColorTerm.Lilac, UiStrings.ColorPickerProfessionalLilac,
            [PerceptualHueFamily.Violet, PerceptualHueFamily.PinkLilac], PerceptualRoleSet.Chromatic,
            0.70, 0.89, 0.035, 0.116, 310, 340, 330),
        DefineComposite("professional-mauve", ProfessionalColorTerm.Mauve, UiStrings.ColorPickerProfessionalMauve,
            Region("professional-mauve-violet",
                [
                    PerceptualHueFamily.Violet, PerceptualHueFamily.PinkLilac, PerceptualHueFamily.VioletGray,
                    PerceptualHueFamily.LilacGray, PerceptualHueFamily.RoseGray
                ],
                PerceptualRoleSet.Chromatic | PerceptualRoleSet.TintedNeutral,
                0.38, 0.66, 0.030, 0.106, 305, 345, 329),
            Region("professional-mauve-rose",
                [PerceptualHueFamily.Rose, PerceptualHueFamily.RedMagenta, PerceptualHueFamily.RoseGray],
                PerceptualRoleSet.Chromatic | PerceptualRoleSet.TintedNeutral,
                0.55, 0.76, 0.025, 0.071, 340, 15, 328)),
        Define("professional-plum", ProfessionalColorTerm.Plum, UiStrings.ColorPickerProfessionalPlum,
            [
                PerceptualHueFamily.Violet, PerceptualHueFamily.Magenta, PerceptualHueFamily.RedMagenta,
                PerceptualHueFamily.Burgundy
            ], PerceptualRoleSet.Chromatic,
            0.22, 0.49, 0.070, 0.165, 318, 358, 327),
        Define("professional-orchid", ProfessionalColorTerm.Orchid, UiStrings.ColorPickerProfessionalOrchid,
            [PerceptualHueFamily.Violet, PerceptualHueFamily.PinkLilac, PerceptualHueFamily.Magenta],
            PerceptualRoleSet.Chromatic, 0.58, 0.81, 0.13, 0.25, 318, 340, 326),
        Define("professional-amethyst", ProfessionalColorTerm.Amethyst, UiStrings.ColorPickerProfessionalAmethyst,
            [PerceptualHueFamily.BlueViolet, PerceptualHueFamily.Violet], PerceptualRoleSet.Chromatic,
            0.50, 0.69, 0.11, 0.22, 294, 316, 325),
        Define("professional-aubergine", ProfessionalColorTerm.Aubergine,
            UiStrings.ColorPickerProfessionalAubergine,
            [PerceptualHueFamily.Violet, PerceptualHueFamily.Magenta, PerceptualHueFamily.RedMagenta],
            PerceptualRoleSet.Chromatic, 0.14, 0.39, 0.065, 0.17, 315, 347, 331),
        Define("professional-cornflower-blue", ProfessionalColorTerm.CornflowerBlue,
            UiStrings.ColorPickerProfessionalCornflowerBlue,
            [PerceptualHueFamily.Blue, PerceptualHueFamily.BlueViolet], PerceptualRoleSet.Chromatic,
            0.61, 0.76, 0.105, 0.19, 255, 273, 322),
        Define("professional-midnight-blue", ProfessionalColorTerm.MidnightBlue,
            UiStrings.ColorPickerProfessionalMidnightBlue,
            [PerceptualHueFamily.Blue, PerceptualHueFamily.BlueViolet], PerceptualRoleSet.Chromatic,
            0.12, 0.34, 0.12, 0.19, 250, 286, 321),
        Define("professional-prussian-blue", ProfessionalColorTerm.PrussianBlue,
            UiStrings.ColorPickerProfessionalPrussianBlue,
            [PerceptualHueFamily.CyanBlue, PerceptualHueFamily.Blue], PerceptualRoleSet.Chromatic,
            0.22, 0.41, 0.05, 0.116, 225, 256, 320),
        Define("professional-ultramarine", ProfessionalColorTerm.Ultramarine,
            UiStrings.ColorPickerProfessionalUltramarine,
            [PerceptualHueFamily.Blue, PerceptualHueFamily.BlueViolet], PerceptualRoleSet.Chromatic,
            0.25, 0.49, 0.18, 0.33, 260, 281, 319),
        Define("professional-baby-blue", ProfessionalColorTerm.BabyBlue, UiStrings.ColorPickerProfessionalBabyBlue,
            [PerceptualHueFamily.Cyan, PerceptualHueFamily.CyanBlue, PerceptualHueFamily.Blue],
            PerceptualRoleSet.Chromatic, 0.78, 0.93, 0.075, 0.145, 220, 251, 318),
        Define("professional-petrol-blue", ProfessionalColorTerm.PetrolBlue,
            UiStrings.ColorPickerProfessionalPetrolBlue,
            [PerceptualHueFamily.Cyan, PerceptualHueFamily.TurquoiseCyan], PerceptualRoleSet.Chromatic,
            0.30, 0.56, 0.04, 0.115, 195, 221, 317),
        Define("professional-moss", ProfessionalColorTerm.Moss, UiStrings.ColorPickerProfessionalMoss,
            [PerceptualHueFamily.Olive, PerceptualHueFamily.OliveGreen, PerceptualHueFamily.YellowGreen],
            PerceptualRoleSet.Chromatic, 0.40, 0.71, 0.04, 0.105, 105, 136, 316),
        Define("professional-hunter-green", ProfessionalColorTerm.HunterGreen,
            UiStrings.ColorPickerProfessionalHunterGreen,
            [PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.27, 0.49, 0.04, 0.105, 134, 162, 315),
        Define("professional-fern", ProfessionalColorTerm.Fern, UiStrings.ColorPickerProfessionalFern,
            [PerceptualHueFamily.OliveGreen, PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.48, 0.69, 0.07, 0.145, 130, 151, 314),
        Define("professional-avocado", ProfessionalColorTerm.Avocado, UiStrings.ColorPickerProfessionalAvocado,
            [PerceptualHueFamily.OliveGreen, PerceptualHueFamily.YellowGreen], PerceptualRoleSet.Chromatic,
            0.48, 0.71, 0.14, 0.19, 128, 135, 313),
        Define("professional-sea-green", ProfessionalColorTerm.SeaGreen, UiStrings.ColorPickerProfessionalSeaGreen,
            [PerceptualHueFamily.Green, PerceptualHueFamily.Turquoise], PerceptualRoleSet.Chromatic,
            0.45, 0.69, 0.11, 0.13, 150, 173, 312),
        Define("professional-saffron", ProfessionalColorTerm.Saffron, UiStrings.ColorPickerProfessionalSaffron,
            [PerceptualHueFamily.Ochre, PerceptualHueFamily.Amber, PerceptualHueFamily.Yellow],
            PerceptualRoleSet.Chromatic, 0.75, 0.91, 0.13, 0.22, 84, 92.5, 311),
        Define("professional-marigold", ProfessionalColorTerm.Marigold, UiStrings.ColorPickerProfessionalMarigold,
            [PerceptualHueFamily.Orange, PerceptualHueFamily.Amber, PerceptualHueFamily.Ochre],
            PerceptualRoleSet.Chromatic, 0.68, 0.87, 0.145, 0.23, 65, 84, 310),
        Define("professional-chocolate", ProfessionalColorTerm.Chocolate,
            UiStrings.ColorPickerProfessionalChocolate,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Taupe], PerceptualRoleSet.Chromatic,
            0.25, 0.51, 0.05, 0.14, 35, 66, 309),
        Define("professional-cinnamon", ProfessionalColorTerm.Cinnamon, UiStrings.ColorPickerProfessionalCinnamon,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Terracotta, PerceptualHueFamily.Ochre],
            PerceptualRoleSet.Chromatic, 0.44, 0.60, 0.105, 0.18, 48, 63, 308),
        Define("professional-sienna", ProfessionalColorTerm.Sienna, UiStrings.ColorPickerProfessionalSienna,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Terracotta], PerceptualRoleSet.Chromatic,
            0.41, 0.58, 0.075, 0.15, 35, 50, 307),
        Define("professional-burnt-sienna", ProfessionalColorTerm.BurntSienna,
            UiStrings.ColorPickerProfessionalBurntSienna,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Terracotta, PerceptualHueFamily.Coral],
            PerceptualRoleSet.Chromatic, 0.63, 0.76, 0.12, 0.21, 30, 46, 306),
        Define("professional-sepia", ProfessionalColorTerm.Sepia, UiStrings.ColorPickerProfessionalSepia,
            [PerceptualHueFamily.Brown], PerceptualRoleSet.Chromatic,
            0.29, 0.51, 0.035, 0.105, 50, 76, 332),
        Define("professional-chestnut", ProfessionalColorTerm.Chestnut, UiStrings.ColorPickerProfessionalChestnut,
            [PerceptualHueFamily.Burgundy, PerceptualHueFamily.Brown], PerceptualRoleSet.Chromatic,
            0.32, 0.57, 0.07, 0.155, 24, 38, 304),
        Define("professional-vermilion", ProfessionalColorTerm.Vermilion,
            UiStrings.ColorPickerProfessionalVermilion,
            [PerceptualHueFamily.Red, PerceptualHueFamily.RedOrange, PerceptualHueFamily.Coral],
            PerceptualRoleSet.Chromatic, 0.52, 0.69, 0.17, 0.235, 25, 35, 303),
        Define("professional-carmine", ProfessionalColorTerm.Carmine, UiStrings.ColorPickerProfessionalCarmine,
            [PerceptualHueFamily.Red, PerceptualHueFamily.Crimson], PerceptualRoleSet.Chromatic,
            0.32, 0.56, 0.135, 0.19, 14, 29, 302),
        Define("professional-tomato", ProfessionalColorTerm.Tomato, UiStrings.ColorPickerProfessionalTomato,
            [PerceptualHueFamily.Coral, PerceptualHueFamily.RedOrange], PerceptualRoleSet.Chromatic,
            0.69, 0.79, 0.15, 0.25, 30, 43, 333),
        Define("professional-eggshell", ProfessionalColorTerm.Eggshell, UiStrings.ColorPickerProfessionalEggshell,
            [PerceptualHueFamily.Cream], PerceptualRoleSet.NearWhite,
            0.91, 0.95, 0.014, 0.041, 70, 111, 300),
        Define("professional-mushroom", ProfessionalColorTerm.Mushroom,
            UiStrings.ColorPickerProfessionalMushroom,
            [PerceptualHueFamily.Greige, PerceptualHueFamily.Taupe, PerceptualHueFamily.WarmGray],
            PerceptualRoleSet.NearNeutral | PerceptualRoleSet.TintedNeutral | PerceptualRoleSet.Chromatic,
            0.58, 0.81, 0.011, 0.046, 20, 65, 299),
        Define("professional-gunmetal", ProfessionalColorTerm.Gunmetal, UiStrings.ColorPickerProfessionalGunmetal,
            [PerceptualHueFamily.BlueGray, PerceptualHueFamily.CoolGray],
            PerceptualRoleSet.NearNeutral | PerceptualRoleSet.TintedNeutral,
            0.22, 0.43, 0.008, 0.041, 190, 261, 298),
        Define("professional-lemon", ProfessionalColorTerm.Lemon, UiStrings.ColorPickerProfessionalLemon,
            [PerceptualHueFamily.Yellow, PerceptualHueFamily.YellowGreen], PerceptualRoleSet.Chromatic,
            0.86, 0.99, 0.12, 0.25, 100, 115, 230),
        DefineComposite("professional-gold", ProfessionalColorTerm.Gold, UiStrings.ColorPickerProfessionalGold,
            Region("professional-gold-yellow",
                [PerceptualHueFamily.Yellow, PerceptualHueFamily.Amber], PerceptualRoleSet.Chromatic,
                0.78, 0.94, 0.11, 0.22, 76, 100, 229),
            Region("professional-gold-ochre",
                [PerceptualHueFamily.Amber, PerceptualHueFamily.Ochre], PerceptualRoleSet.Chromatic,
                0.64, 0.82, 0.09, 0.16, 78, 96, 228)),
        DefineComposite("professional-khaki", ProfessionalColorTerm.Khaki, UiStrings.ColorPickerProfessionalKhaki,
            Region("professional-khaki-yellow",
                [PerceptualHueFamily.Yellow, PerceptualHueFamily.YellowGreen], PerceptualRoleSet.Chromatic,
                0.82, 0.95, 0.07, 0.15, 94, 114, 227),
            Region("professional-khaki-earth",
                [
                    PerceptualHueFamily.Beige, PerceptualHueFamily.Olive, PerceptualHueFamily.OliveGray
                ],
                PerceptualRoleSet.Chromatic | PerceptualRoleSet.TintedNeutral,
                0.68, 0.81, 0.04, 0.075, 75, 102, 226)),
        Define("professional-copper", ProfessionalColorTerm.Copper, UiStrings.ColorPickerProfessionalCopper,
            [PerceptualHueFamily.Terracotta, PerceptualHueFamily.Brown, PerceptualHueFamily.Ochre],
            PerceptualRoleSet.Chromatic, 0.58, 0.70, 0.105, 0.122, 45, 66, 225),
        Define("professional-mahogany", ProfessionalColorTerm.Mahogany, UiStrings.ColorPickerProfessionalMahogany,
            [PerceptualHueFamily.Burgundy, PerceptualHueFamily.Brown, PerceptualHueFamily.Terracotta],
            PerceptualRoleSet.Chromatic, 0.20, 0.46, 0.055, 0.17, 18, 45, 224),
        Define("professional-caramel", ProfessionalColorTerm.Caramel, UiStrings.ColorPickerProfessionalCaramel,
            [PerceptualHueFamily.Brown, PerceptualHueFamily.Ochre, PerceptualHueFamily.Sand],
            PerceptualRoleSet.Chromatic, 0.54, 0.72, 0.122, 0.14, 58, 70, 223),
        Define("professional-jade", ProfessionalColorTerm.Jade, UiStrings.ColorPickerProfessionalJade,
            [PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.52, 0.72, 0.09, 0.17, 155, 172, 222),
        Define("professional-royal-blue", ProfessionalColorTerm.RoyalBlue,
            UiStrings.ColorPickerProfessionalRoyalBlue,
            [PerceptualHueFamily.Blue], PerceptualRoleSet.Chromatic,
            0.52, 0.66, 0.15, 0.24, 260, 270, 221),
        Define("professional-indigo", ProfessionalColorTerm.Indigo, UiStrings.ColorPickerProfessionalIndigo,
            [PerceptualHueFamily.BlueViolet, PerceptualHueFamily.Violet], PerceptualRoleSet.Chromatic,
            0.22, 0.48, 0.15, 0.26, 278, 308, 210),
        DefineComposite("professional-powder-blue", ProfessionalColorTerm.PowderBlue,
            UiStrings.ColorPickerProfessionalPowderBlue,
            Region("professional-powder-blue-cyan",
                [PerceptualHueFamily.Cyan, PerceptualHueFamily.CyanBlue, PerceptualHueFamily.BlueGray],
                PerceptualRoleSet.Chromatic | PerceptualRoleSet.TintedNeutral,
                0.82, 0.93, 0.025, 0.076, 195, 232, 209),
            Region("professional-powder-blue-blue",
                [PerceptualHueFamily.CyanBlue, PerceptualHueFamily.Blue, PerceptualHueFamily.BlueGray],
                PerceptualRoleSet.Chromatic | PerceptualRoleSet.TintedNeutral,
                0.80, 0.93, 0.030, 0.086, 232, 266, 208)),
        Define("professional-steel-blue", ProfessionalColorTerm.SteelBlue,
            UiStrings.ColorPickerProfessionalSteelBlue,
            [PerceptualHueFamily.BlueGray, PerceptualHueFamily.CyanBlue, PerceptualHueFamily.Blue],
            PerceptualRoleSet.TintedNeutral | PerceptualRoleSet.Chromatic,
            0.48, 0.70, 0.050, 0.105, 230, 262, 207),
        Define("professional-olive-drab", ProfessionalColorTerm.OliveDrab,
            UiStrings.ColorPickerProfessionalOliveDrab,
            [PerceptualHueFamily.Olive, PerceptualHueFamily.OliveGreen, PerceptualHueFamily.YellowGreen],
            PerceptualRoleSet.Chromatic, 0.42, 0.66, 0.070, 0.165, 108, 131, 206),
        Define("professional-lime", ProfessionalColorTerm.Lime, UiStrings.ColorPickerProfessionalLime,
            [PerceptualHueFamily.YellowGreen, PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.76, 0.97, 0.18, 0.36, 138, 153, 205),
        Define("professional-chartreuse", ProfessionalColorTerm.Chartreuse,
            UiStrings.ColorPickerProfessionalChartreuse,
            [PerceptualHueFamily.YellowGreen, PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.72, 0.96, 0.18, 0.34, 112, 138, 204),
        Define("professional-seafoam", ProfessionalColorTerm.Seafoam, UiStrings.ColorPickerProfessionalSeafoam,
            [PerceptualHueFamily.Green, PerceptualHueFamily.Mint, PerceptualHueFamily.Turquoise],
            PerceptualRoleSet.Chromatic, 0.76, 0.94, 0.10, 0.18, 148, 166, 203),
        Define("professional-cobalt", ProfessionalColorTerm.Cobalt, UiStrings.ColorPickerProfessionalCobalt,
            [PerceptualHueFamily.Blue, PerceptualHueFamily.BlueViolet], PerceptualRoleSet.Chromatic,
            0.34, 0.58, 0.11, 0.23, 250, 270, 202),
        Define("professional-cerulean", ProfessionalColorTerm.Cerulean, UiStrings.ColorPickerProfessionalCerulean,
            [PerceptualHueFamily.Cyan, PerceptualHueFamily.CyanBlue, PerceptualHueFamily.Blue],
            PerceptualRoleSet.Chromatic, 0.48, 0.70, 0.08, 0.16, 222, 247, 201),
        Define("professional-blood-orange", ProfessionalColorTerm.BloodOrange,
            UiStrings.ColorPickerProfessionalBloodOrange,
            [PerceptualHueFamily.Red, PerceptualHueFamily.RedOrange, PerceptualHueFamily.Coral],
            PerceptualRoleSet.Chromatic, 0.55, 0.76, 0.18, 0.29, 30, 47, 200),
        Define("professional-pumpkin", ProfessionalColorTerm.Pumpkin, UiStrings.ColorPickerProfessionalPumpkin,
            [PerceptualHueFamily.Orange, PerceptualHueFamily.Amber, PerceptualHueFamily.Ochre],
            PerceptualRoleSet.Chromatic, 0.55, 0.76, 0.09, 0.19, 48, 72, 199),
        Define("professional-blush", ProfessionalColorTerm.Blush, UiStrings.ColorPickerProfessionalBlush,
            [PerceptualHueFamily.Rose, PerceptualHueFamily.Pink, PerceptualHueFamily.Coral],
            PerceptualRoleSet.Chromatic, 0.72, 0.90, 0.025, 0.12, 350, 36, 198),
        Define("professional-pistachio", ProfessionalColorTerm.Pistachio,
            UiStrings.ColorPickerProfessionalPistachio,
            [PerceptualHueFamily.YellowGreen, PerceptualHueFamily.Green], PerceptualRoleSet.Chromatic,
            0.78, 0.95, 0.055, 0.18, 112, 138, 197),
        Define("professional-linen", ProfessionalColorTerm.Linen, UiStrings.ColorPickerProfessionalLinen,
            [PerceptualHueFamily.Cream], PerceptualRoleSet.NearWhite,
            0.93, 1.001, 0.010, 0.036, 45, 80, 196),
        Define("professional-silver", ProfessionalColorTerm.Silver, UiStrings.ColorPickerProfessionalSilver,
            [
                PerceptualHueFamily.Neutral, PerceptualHueFamily.WarmGray, PerceptualHueFamily.CoolGray,
                PerceptualHueFamily.BlueGray, PerceptualHueFamily.GreenGray, PerceptualHueFamily.RoseGray,
                PerceptualHueFamily.VioletGray, PerceptualHueFamily.LilacGray
            ],
            PerceptualRoleSet.Neutral | PerceptualRoleSet.NearNeutral | PerceptualRoleSet.TintedNeutral,
            0.76, 0.83, 0, 0.014, 0, 360, 195),
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
        [
            new ProfessionalShadeRegionDefinition(
                stableId + "-core",
                parentFamilies,
                roles,
                minimumLightness,
                maximumLightness,
                minimumChroma,
                maximumChroma,
                minimumHue,
                maximumHue,
                priority)
        ]);

    private static ProfessionalShadeDefinition DefineComposite(
        string stableId,
        ProfessionalColorTerm term,
        string localizationKey,
        params ProfessionalShadeRegionDefinition[] regions) => new(stableId, term, localizationKey, regions);

    private static ProfessionalShadeRegionDefinition Region(
        string stableId,
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
    private sealed record IndexedRegion(
        ProfessionalShadeDefinition Definition,
        ProfessionalShadeRegionDefinition Region);

    private static readonly IndexedRegion[] Regions = ProfessionalShadeCatalog.Definitions
        .SelectMany(definition => definition.Regions.Select(region => new IndexedRegion(definition, region)))
        .OrderByDescending(item => item.Region.Priority)
        .ThenBy(item => item.Region.StableId, StringComparer.Ordinal)
        .ToArray();

    private static readonly IReadOnlyDictionary<PerceptualHueFamily, IndexedRegion[]> ByFamily =
        ProfessionalShadeCatalog.Definitions
            .SelectMany(definition => definition.Regions.SelectMany(region =>
                region.ParentFamilies.Select(family => (family, item: new IndexedRegion(definition, region)))))
            .GroupBy(item => item.family)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.item)
                    .OrderByDescending(item => item.Region.Priority)
                    .ThenBy(item => item.Region.StableId, StringComparer.Ordinal)
                    .ToArray());

    public static ProfessionalColorTerm? Classify(
        OklchColor color,
        PerceptualColorRole role,
        PerceptualHueFamily family) => ClassifyMatch(color, role, family)?.Term;

    public static ProfessionalShadeMatch? ClassifyMatch(
        OklchColor color,
        PerceptualColorRole role,
        PerceptualHueFamily family)
    {
        var winner = ByFamily.GetValueOrDefault(family, [])
            .FirstOrDefault(item => item.Region.Matches(color, role, family));
        return winner is null
            ? null
            : new ProfessionalShadeMatch(
                winner.Definition.Term,
                winner.Definition.StableId,
                winner.Region.StableId,
                winner.Region.Priority);
    }

    public static ProfessionalShadeExplanation Explain(
        OklchColor color,
        PerceptualColorRole role,
        PerceptualHueFamily family)
    {
        var evaluations = Regions.Select(item =>
        {
            var failure = item.Region.FailureReason(color, role, family);
            return new ProfessionalShadeRegionEvaluation(
                item.Definition.Term,
                item.Definition.StableId,
                item.Region.StableId,
                item.Region.Priority,
                failure is null,
                failure);
        }).ToArray();
        return new ProfessionalShadeExplanation(
            ClassifyMatch(color, role, family),
            evaluations);
    }
}