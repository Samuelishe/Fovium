using Fovium.ColorPicking;
using System.Text.Json.Serialization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal enum ProfessionalTermDomain
{
    PurplePink,
    Blue,
    GreenCyan,
    YellowEarth,
    BrownEarth,
    RedOrange,
    NeutralOffWhite,
    Other
}

[JsonConverter(typeof(JsonStringEnumConverter<ProfessionalOverlapSeverity>))]
internal enum ProfessionalOverlapSeverity
{
    Unknown,
    BenignSibling,
    SuspiciousSibling,
    DistantFamily,
    ExcessiveVolume
}

internal sealed record ProfessionalTermResearchDescriptor(
    string CanonicalTerm,
    ProfessionalTermDomain Domain,
    IReadOnlyList<string> Aliases,
    bool IsShipped);

internal static class ProfessionalTermResearchCatalog
{
    private static readonly IReadOnlySet<string> ShippedTerms = ProfessionalShadeCatalog.Definitions
        .Select(definition => definition.Term.ToString())
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<ProfessionalTermResearchDescriptor> Terms { get; } =
        SpecificColorTermNormalizer.Vocabulary
            .Select(item => new ProfessionalTermResearchDescriptor(
                item.Term,
                GetDomain(item.Term),
                item.Aliases,
                ShippedTerms.Contains(item.Term)))
            .OrderBy(item => item.CanonicalTerm, StringComparer.Ordinal)
            .ToArray();

    public static ProfessionalOverlapSeverity ClassifyOverlap(string winner, string competitor)
    {
        var winnerDomain = GetDomain(winner);
        var competitorDomain = GetDomain(competitor);
        if (winnerDomain == ProfessionalTermDomain.Other || competitorDomain == ProfessionalTermDomain.Other)
        {
            return ProfessionalOverlapSeverity.Unknown;
        }

        if (winnerDomain == competitorDomain)
        {
            return ProfessionalOverlapSeverity.BenignSibling;
        }

        return AreAdjacent(winnerDomain, competitorDomain)
            ? ProfessionalOverlapSeverity.SuspiciousSibling
            : ProfessionalOverlapSeverity.DistantFamily;
    }

    public static ProfessionalTermDomain DescribeDomain(string term) => GetDomain(term);

    private static bool AreAdjacent(ProfessionalTermDomain left, ProfessionalTermDomain right) =>
        (left, right) switch
        {
            (ProfessionalTermDomain.PurplePink, ProfessionalTermDomain.RedOrange) or
                (ProfessionalTermDomain.RedOrange, ProfessionalTermDomain.PurplePink) or
                (ProfessionalTermDomain.PurplePink, ProfessionalTermDomain.Blue) or
                (ProfessionalTermDomain.Blue, ProfessionalTermDomain.PurplePink) or
                (ProfessionalTermDomain.Blue, ProfessionalTermDomain.GreenCyan) or
                (ProfessionalTermDomain.GreenCyan, ProfessionalTermDomain.Blue) or
                (ProfessionalTermDomain.GreenCyan, ProfessionalTermDomain.YellowEarth) or
                (ProfessionalTermDomain.YellowEarth, ProfessionalTermDomain.GreenCyan) or
                (ProfessionalTermDomain.YellowEarth, ProfessionalTermDomain.BrownEarth) or
                (ProfessionalTermDomain.BrownEarth, ProfessionalTermDomain.YellowEarth) or
                (ProfessionalTermDomain.YellowEarth, ProfessionalTermDomain.RedOrange) or
                (ProfessionalTermDomain.RedOrange, ProfessionalTermDomain.YellowEarth) or
                (ProfessionalTermDomain.BrownEarth, ProfessionalTermDomain.RedOrange) or
                (ProfessionalTermDomain.RedOrange, ProfessionalTermDomain.BrownEarth) or
                (ProfessionalTermDomain.NeutralOffWhite, ProfessionalTermDomain.Blue) or
                (ProfessionalTermDomain.Blue, ProfessionalTermDomain.NeutralOffWhite) or
                (ProfessionalTermDomain.NeutralOffWhite, ProfessionalTermDomain.GreenCyan) or
                (ProfessionalTermDomain.GreenCyan, ProfessionalTermDomain.NeutralOffWhite) or
                (ProfessionalTermDomain.NeutralOffWhite, ProfessionalTermDomain.YellowEarth) or
                (ProfessionalTermDomain.YellowEarth, ProfessionalTermDomain.NeutralOffWhite) or
                (ProfessionalTermDomain.NeutralOffWhite, ProfessionalTermDomain.BrownEarth) or
                (ProfessionalTermDomain.BrownEarth, ProfessionalTermDomain.NeutralOffWhite) => true,
            _ => false
        };

    private static ProfessionalTermDomain GetDomain(string term) => term switch
    {
        "Lilac" or "Mauve" or "Plum" or "Orchid" or "Amethyst" or "Aubergine" or "Lavender" or
            "Periwinkle" or "Blush" or "Fuchsia" or "Raspberry" or "Mulberry" or "Wisteria" or
            "Heather" or "RoseQuartz" => ProfessionalTermDomain.PurplePink,
        "CornflowerBlue" or "MidnightBlue" or "PrussianBlue" or "Ultramarine" or "BabyBlue" or
            "PowderBlue" or "SteelBlue" or "Cobalt" or "Cerulean" or "RoyalBlue" or "Indigo" or
            "Navy" or "Azure" or "SkyBlue" or "Sapphire" or "Denim" or "ElectricBlue" or
            "OxfordBlue" or "IceBlue" or "DustyBlue" => ProfessionalTermDomain.Blue,
        "PetrolBlue" or "Moss" or "HunterGreen" or "Fern" or "Avocado" or "SeaGreen" or "Jade" or
            "OliveDrab" or "Lime" or "Chartreuse" or "Seafoam" or "Sage" or "Emerald" or
            "ForestGreen" or "Aquamarine" or "Teal" or "Pistachio" or "Viridian" or "Celadon" or
            "BottleGreen" or "PineGreen" or "KellyGreen" or "AppleGreen" or "Petrol" =>
            ProfessionalTermDomain.GreenCyan,
        "Saffron" or "Marigold" or "Gold" or "Khaki" or "Lemon" or "Canary" or "Honey" or
            "NaplesYellow" => ProfessionalTermDomain.YellowEarth,
        "Chocolate" or "Cinnamon" or "Sienna" or "BurntSienna" or "Sepia" or "Chestnut" or
            "Copper" or "Mahogany" or "Caramel" or "Pumpkin" or "Rust" or "Coffee" or "Umber" or
            "RawUmber" or "BurntUmber" or "Bronze" or "Brick" or "Espresso" =>
            ProfessionalTermDomain.BrownEarth,
        "Vermilion" or "Carmine" or "Tomato" or "BloodOrange" or "Salmon" or "Wine" or "Scarlet" or
            "Tangerine" or "Ruby" or "Cherry" or "Cranberry" => ProfessionalTermDomain.RedOrange,
        "Eggshell" or "Mushroom" or "Gunmetal" or "Linen" or "Silver" or "Ivory" or "Charcoal" or
            "Slate" or "Ecru" or "Champagne" or "Stone" or "Pewter" or "Parchment" or "DoveGray" or
            "Vanilla" or "Pearl" or "Graphite" or "Ash" or "Smoke" or "AntiqueWhite" or
            "PaynesGray" => ProfessionalTermDomain.NeutralOffWhite,
        _ => ProfessionalTermDomain.Other
    };
}