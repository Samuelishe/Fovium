using Fovium.Localization;

namespace Fovium.ColorPicking;

internal enum ProfessionalColorTerm
{
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