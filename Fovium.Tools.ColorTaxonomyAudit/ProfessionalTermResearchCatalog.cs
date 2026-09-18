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

[JsonConverter(typeof(JsonStringEnumConverter<CandidateResearchStatus>))]
internal enum CandidateResearchStatus
{
    Unreviewed,
    Accepted,
    Deferred,
    Rejected,
    Synonym
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
    bool IsShipped)
{
    public CandidateResearchStatus Status { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string RussianCandidate { get; init; } = string.Empty;
}

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
                ShippedTerms.Contains(item.Term))
            {
                Status = GetStatus(item.Term),
                Reason = GetReason(item.Term),
                RussianCandidate = GetRussianCandidate(item.Term)
            })
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
            "Heather" or "RoseQuartz" or "Amaranth" or "BabyPink" or "Cerise" or "Heliotrope" or
            "HotPink" or "Puce" or "RoyalPurple" or "ShockingPink" or "TyrianPurple" =>
            ProfessionalTermDomain.PurplePink,
        "CornflowerBlue" or "MidnightBlue" or "PrussianBlue" or "Ultramarine" or "BabyBlue" or
            "PowderBlue" or "SteelBlue" or "Cobalt" or "Cerulean" or "RoyalBlue" or "Indigo" or
            "Navy" or "Azure" or "SkyBlue" or "Sapphire" or "Denim" or "ElectricBlue" or
            "OxfordBlue" or "IceBlue" or "DustyBlue" or "AliceBlue" or "CadetBlue" or
            "CambridgeBlue" or "DuckEggBlue" or "EgyptianBlue" or "GentianBlue" or "LapisLazuli" or
            "NileBlue" or "PeacockBlue" or "PhthaloBlue" or "RobinsEggBlue" or "SaxeBlue" or
            "SlateBlue" or "WedgwoodBlue" or "Zaffre" => ProfessionalTermDomain.Blue,
        "PetrolBlue" or "Moss" or "HunterGreen" or "Fern" or "Avocado" or "SeaGreen" or "Jade" or
            "OliveDrab" or "Lime" or "Chartreuse" or "Seafoam" or "Sage" or "Emerald" or
            "ForestGreen" or "Aquamarine" or "Teal" or "Pistachio" or "Viridian" or "Celadon" or
            "BottleGreen" or "PineGreen" or "KellyGreen" or "AppleGreen" or "Petrol" or
            "Artichoke" or "Asparagus" or "BritishRacingGreen" or "EauDeNil" or "Eucalyptus" or
            "Glaucous" or "GreenEarth" or "JungleGreen" or "LaurelGreen" or "LincolnGreen" or
            "Loden" or "Malachite" or "Myrtle" or "ParisGreen" or "PeaGreen" or "PhthaloGreen" or
            "RifleGreen" or "SpringGreen" or "Verdigris" =>
            ProfessionalTermDomain.GreenCyan,
        "Saffron" or "Marigold" or "Gold" or "Khaki" or "Lemon" or "Canary" or "Honey" or
            "NaplesYellow" or "CadmiumYellow" or "Citrine" or "Citron" or "Daffodil" or
            "Dandelion" or "Gamboge" or "Goldenrod" or "HarvestGold" or "IndianYellow" or
            "Jonquil" or "OldGold" or "Primrose" or "Straw" or "Sunflower" =>
            ProfessionalTermDomain.YellowEarth,
        "Chocolate" or "Cinnamon" or "Sienna" or "BurntSienna" or "Sepia" or "Chestnut" or
            "Copper" or "Mahogany" or "Caramel" or "Pumpkin" or "Rust" or "Coffee" or "Umber" or
            "RawUmber" or "BurntUmber" or "Bronze" or "Brick" or "Espresso" or "Auburn" or
            "Beaver" or "Biscuit" or "Bisque" or "Buckskin" or "Buff" or "Camel" or "Cappuccino" or
            "Chamois" or "Cocoa" or "Fawn" or "Ginger" or "Hazel" or "Henna" or "Mocha" or
            "MummyBrown" or "RawSienna" or "Russet" or "Sable" or "SaddleBrown" or "Tawny" or
            "Teak" or "VandykeBrown" or "Walnut" or "Wenge" =>
            ProfessionalTermDomain.BrownEarth,
        "Vermilion" or "Carmine" or "Tomato" or "BloodOrange" or "Salmon" or "Wine" or "Scarlet" or
            "Tangerine" or "Ruby" or "Cherry" or "Cranberry" or "AlizarinCrimson" or "BloodRed" or
            "BrickRed" or "BurntOrange" or "CadmiumRed" or "Cardinal" or "Carnelian" or "Carrot" or
            "ChineseRed" or "Cinnabar" or "Claret" or "Cochineal" or "Firebrick" or "Flame" or
            "Garnet" or "Geranium" or "IndianRed" or "Madder" or "Oxblood" or "Paprika" or
            "Pomegranate" or "PompeianRed" or "Poppy" or "Sangria" or "Strawberry" or "VenetianRed" =>
            ProfessionalTermDomain.RedOrange,
        "Eggshell" or "Mushroom" or "Gunmetal" or "Linen" or "Silver" or "Ivory" or "Charcoal" or
            "Slate" or "Ecru" or "Champagne" or "Stone" or "Pewter" or "Parchment" or "DoveGray" or
            "Vanilla" or "Pearl" or "Graphite" or "Ash" or "Smoke" or "AntiqueWhite" or
            "PaynesGray" or "Alabaster" or "Almond" or "AshGray" or "BattleshipGray" or "Bone" or
            "CoolGray" or "DesertSand" or "Ebony" or "Fuscous" or "Gainsboro" or "Greige" or
            "IronGray" or "Jet" or "Licorice" or "Magnolia" or "Manatee" or "Oatmeal" or "Onyx" or
            "Oyster" or "PearlGray" or "Platinum" or "Putty" or "Quartz" or "Seashell" or
            "Timberwolf" => ProfessionalTermDomain.NeutralOffWhite,
        _ => ProfessionalTermDomain.Other
    };

    private static CandidateResearchStatus GetStatus(string term)
    {
        if (ShippedTerms.Contains(term))
        {
            return CandidateResearchStatus.Accepted;
        }

        return term switch
        {
            "Fuchsia" or "RoyalPurple" or "IndianRed" or "CadetBlue" => CandidateResearchStatus.Synonym,
            "Sapphire" or "BottleGreen" => CandidateResearchStatus.Rejected,
            "Cherry" or "Raspberry" or "Ecru" or "Parchment" or "RawUmber" or "BurntUmber" or
                "PaynesGray" or "Coffee" or "Stone" or "Pearl" or "Champagne" or "Pewter" or
                "Graphite" or "Auburn" => CandidateResearchStatus.Deferred,
            _ => CandidateResearchStatus.Unreviewed
        };
    }

    private static string GetReason(string term)
    {
        if (ShippedTerms.Contains(term))
        {
            return "Accepted into the bounded production taxonomy with deterministic core and boundary evidence.";
        }

        return term switch
        {
            "Fuchsia" => "Near-synonym of shipped Magenta; retained as a research alias.",
            "RoyalPurple" => "Expanded-corpus core collides with shipped Indigo.",
            "IndianRed" => "Expanded-corpus core is indistinguishable from accepted Brick Red controls.",
            "CadetBlue" => "CSS Cadet Blue is perceptually indistinguishable from a shipped Teal control.",
            "Auburn" => "Expanded-corpus core overlaps accepted Brick Red and Carmine controls.",
            "Sapphire" => "F11 evidence displaced accepted Cobalt controls without a distinct robust core.",
            "BottleGreen" => "F11 evidence displaced accepted Forest Green controls without a distinct robust core.",
            "Cherry" or "Raspberry" => "Useful conventional berry term; expanded-corpus component review pending.",
            "Ecru" or "Parchment" or "Stone" or "Pearl" or "Champagne" or "Pewter" or "Graphite" =>
                "Material-origin neutral requires a compact RGB core distinct from shipped neighbors.",
            "RawUmber" or "BurntUmber" or "PaynesGray" =>
                "Historical pigment name requires stable screen-color semantics rather than chemical identity.",
            "Coffee" => "Food-origin brown remains ambiguous across lightness and product usage.",
            _ => "Unreviewed conventional candidate retained for ranked whole-corpus research."
        };
    }

    private static string GetRussianCandidate(string term) => term switch
    {
        "Alabaster" => "Алебастровый",
        "AlizarinCrimson" => "Ализариновый кармин",
        "Bone" => "Костяной",
        "BurntUmber" => "Жжёная умбра",
        "CadmiumRed" => "Кадмиевый красный",
        "CadmiumYellow" => "Кадмиевый жёлтый",
        "Champagne" => "Шампань",
        "DoveGray" => "Голубиный серый",
        "Ecru" => "Экрю",
        "EgyptianBlue" => "Египетский синий",
        "Graphite" => "Графитовый",
        "GreenEarth" => "Зелёная земля",
        "IndianRed" => "Индийский красный",
        "IndianYellow" => "Индийский жёлтый",
        "Parchment" => "Пергаментный",
        "PaynesGray" => "Серая Пейна",
        "Pearl" => "Жемчужный",
        "Pewter" => "Оловянный",
        "PhthaloBlue" => "Фталоцианиновый синий",
        "PhthaloGreen" => "Фталоцианиновый зелёный",
        "Putty" => "Шпаклёвочный",
        "RawSienna" => "Натуральная сиена",
        "RawUmber" => "Натуральная умбра",
        "VenetianRed" => "Венецианский красный",
        _ => string.Empty
    };
}