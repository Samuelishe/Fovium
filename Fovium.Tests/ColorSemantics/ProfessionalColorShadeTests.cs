using System.Globalization;
using Fovium.ColorPicking;
using Fovium.Localization;

namespace Fovium.Tests.ColorSemantics;

public sealed class ProfessionalColorShadeTests
{
    [Theory]
    [InlineData("#C79FEF", ProfessionalColorTerm.Lavender, PerceptualHueFamily.BlueViolet)]
    [InlineData("#8E82FE", ProfessionalColorTerm.Periwinkle, PerceptualHueFamily.BlueViolet)]
    [InlineData("#01153E", ProfessionalColorTerm.Navy, PerceptualHueFamily.Blue)]
    [InlineData("#069AF3", ProfessionalColorTerm.Azure, PerceptualHueFamily.Blue)]
    [InlineData("#75BBFD", ProfessionalColorTerm.SkyBlue, PerceptualHueFamily.Blue)]
    [InlineData("#87AE73", ProfessionalColorTerm.Sage, PerceptualHueFamily.Green)]
    [InlineData("#01A049", ProfessionalColorTerm.Emerald, PerceptualHueFamily.Green)]
    [InlineData("#06470C", ProfessionalColorTerm.ForestGreen, PerceptualHueFamily.Green)]
    [InlineData("#04D8B2", ProfessionalColorTerm.Aquamarine, PerceptualHueFamily.Turquoise)]
    [InlineData("#029386", ProfessionalColorTerm.Teal, PerceptualHueFamily.Turquoise)]
    [InlineData("#FF796C", ProfessionalColorTerm.Salmon, PerceptualHueFamily.Coral)]
    [InlineData("#80013F", ProfessionalColorTerm.Wine, PerceptualHueFamily.Crimson)]
    [InlineData("#A83C09", ProfessionalColorTerm.Rust, PerceptualHueFamily.Brown)]
    [InlineData("#BE0119", ProfessionalColorTerm.Scarlet, PerceptualHueFamily.Red)]
    [InlineData("#FF9408", ProfessionalColorTerm.Tangerine, PerceptualHueFamily.Orange)]
    [InlineData("#FFFFCB", ProfessionalColorTerm.Ivory, PerceptualHueFamily.Cream)]
    [InlineData("#343837", ProfessionalColorTerm.Charcoal, PerceptualHueFamily.Neutral)]
    [InlineData("#516572", ProfessionalColorTerm.Slate, PerceptualHueFamily.BlueGray)]
    [InlineData("#4B0082", ProfessionalColorTerm.Indigo, PerceptualHueFamily.BlueViolet)]
    [InlineData("#B0E0E6", ProfessionalColorTerm.PowderBlue, PerceptualHueFamily.Cyan)]
    [InlineData("#B1D1FC", ProfessionalColorTerm.PowderBlue, PerceptualHueFamily.Blue)]
    [InlineData("#4682B4", ProfessionalColorTerm.SteelBlue, PerceptualHueFamily.Blue)]
    [InlineData("#6B8E23", ProfessionalColorTerm.OliveDrab, PerceptualHueFamily.OliveGreen)]
    [InlineData("#00FF00", ProfessionalColorTerm.Lime, PerceptualHueFamily.Green)]
    [InlineData("#C1F80A", ProfessionalColorTerm.Chartreuse, PerceptualHueFamily.YellowGreen)]
    [InlineData("#80F9AD", ProfessionalColorTerm.Seafoam, PerceptualHueFamily.Green)]
    [InlineData("#0047AB", ProfessionalColorTerm.Cobalt, PerceptualHueFamily.Blue)]
    [InlineData("#007BA7", ProfessionalColorTerm.Cerulean, PerceptualHueFamily.CyanBlue)]
    [InlineData("#FE4B03", ProfessionalColorTerm.BloodOrange, PerceptualHueFamily.Red)]
    [InlineData("#E17701", ProfessionalColorTerm.Pumpkin, PerceptualHueFamily.Ochre)]
    [InlineData("#F29E8E", ProfessionalColorTerm.Blush, PerceptualHueFamily.Rose)]
    [InlineData("#C0FA8B", ProfessionalColorTerm.Pistachio, PerceptualHueFamily.YellowGreen)]
    [InlineData("#FAF0E6", ProfessionalColorTerm.Linen, PerceptualHueFamily.Cream)]
    [InlineData("#C0C0C0", ProfessionalColorTerm.Silver, PerceptualHueFamily.Neutral)]
    public void IndependentReferenceAnchorsResolveSpecificTermsWithoutReplacingBaseFamilies(
        string hex,
        object expectedTerm,
        object expectedFamily)
    {
        var description = Describe(hex);

        Assert.Equal((PerceptualHueFamily)expectedFamily, description.HueFamily);
        Assert.Equal((ProfessionalColorTerm)expectedTerm, description.ProfessionalTerm);
    }

    [Theory]
    [InlineData("#FFD700", ProfessionalColorTerm.Gold)]
    [InlineData("#DBB40C", ProfessionalColorTerm.Gold)]
    [InlineData("#F0E68C", ProfessionalColorTerm.Khaki)]
    [InlineData("#C3B091", ProfessionalColorTerm.Khaki)]
    [InlineData("#B87333", ProfessionalColorTerm.Copper)]
    [InlineData("#4A0100", ProfessionalColorTerm.Mahogany)]
    [InlineData("#AF6F09", ProfessionalColorTerm.Caramel)]
    [InlineData("#FFF700", ProfessionalColorTerm.Lemon)]
    [InlineData("#00A86B", ProfessionalColorTerm.Jade)]
    [InlineData("#4169E1", ProfessionalColorTerm.RoyalBlue)]
    public void ThirdWaveIndependentReferenceAnchorsResolveConventionalTerms(
        string hex,
        object expectedTerm)
    {
        Assert.Equal((ProfessionalColorTerm)expectedTerm, Describe(hex).ProfessionalTerm);
    }

    [Theory]
    [InlineData("#C8A2C8", "Lilac")]
    [InlineData("#6C5472", "Mauve")]
    [InlineData("#A98691", "Mauve")]
    [InlineData("#612246", "Plum")]
    [InlineData("#DA70D6", "Orchid")]
    [InlineData("#9966CC", "Amethyst")]
    [InlineData("#3D0734", "Aubergine")]
    [InlineData("#6495ED", "CornflowerBlue")]
    [InlineData("#191970", "MidnightBlue")]
    [InlineData("#003153", "PrussianBlue")]
    [InlineData("#120A8F", "Ultramarine")]
    [InlineData("#89CFF0", "BabyBlue")]
    [InlineData("#005F6A", "PetrolBlue")]
    [InlineData("#8A9A5B", "Moss")]
    [InlineData("#355E3B", "HunterGreen")]
    [InlineData("#4F7942", "Fern")]
    [InlineData("#568203", "Avocado")]
    [InlineData("#2E8B57", "SeaGreen")]
    [InlineData("#F4C430", "Saffron")]
    [InlineData("#EAA221", "Marigold")]
    [InlineData("#7B3F00", "Chocolate")]
    [InlineData("#A85624", "Cinnamon")]
    [InlineData("#A0522D", "Sienna")]
    [InlineData("#E97451", "BurntSienna")]
    [InlineData("#704214", "Sepia")]
    [InlineData("#954535", "Chestnut")]
    [InlineData("#E34234", "Vermilion")]
    [InlineData("#960018", "Carmine")]
    [InlineData("#FF6347", "Tomato")]
    [InlineData("#F0EAD6", "Eggshell")]
    [InlineData("#BDACA3", "Mushroom")]
    [InlineData("#2A3439", "Gunmetal")]
    [InlineData("#E0115F", "Ruby")]
    [InlineData("#9E003A", "Cranberry")]
    [InlineData("#40826D", "Viridian")]
    [InlineData("#ACE1AF", "Celadon")]
    [InlineData("#FAEBD7", "AntiqueWhite")]
    [InlineData("#F3E5AB", "Vanilla")]
    [InlineData("#FADA5F", "NaplesYellow")]
    [InlineData("#4E312D", "Espresso")]
    public void MultiWaveIndependentReferenceAnchorsResolveConventionalTerms(string hex, string expectedTerm)
    {
        Assert.Equal(expectedTerm, Describe(hex).ProfessionalTerm?.ToString());
    }

    [Theory]
    [InlineData("#D94FF5", "Heliotrope", "Гелиотроповый")]
    [InlineData("#7B68EE", "Slate blue", "Сланцево-синий")]
    [InlineData("#00FF7F", "Spring green", "Весенний зелёный")]
    [InlineData("#01796F", "Pine green", "Сосновый зелёный")]
    [InlineData("#8F1402", "Brick red", "Кирпично-красный")]
    [InlineData("#CB4154", "Brick red", "Кирпично-красный")]
    [InlineData("#9A6200", "Raw sienna", "Натуральная сиена")]
    [InlineData("#A75E09", "Raw umber", "Натуральная умбра")]
    [InlineData("#FDFF63", "Canary yellow", "Канареечный жёлтый")]
    [InlineData("#E49B0F", "Gamboge", "Гуммигут")]
    [InlineData("#C2B280", "Ecru", "Экрю")]
    [InlineData("#DBC7A8", "Buff", "Палевый")]
    [InlineData("#DAA520", "Goldenrod", "Золотисто-жёлтый")]
    [InlineData("#794029", "Russet", "Рыжевато-коричневый")]
    [InlineData("#988E94", "Heather", "Вересковый")]
    public void ExpandedCorpusAnchorsResolveReviewedEnglishAndRussian(
        string hex,
        string englishName,
        string russianName)
    {
        var description = Describe(hex);
        var english = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("en-US")));
        var russian = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("ru-RU")));

        Assert.NotNull(description.ProfessionalTerm);
        Assert.Equal(englishName, english.ResolveShort(description));
        Assert.Equal(englishName, english.ResolveDetailed(description));
        Assert.Equal(russianName, russian.ResolveShort(description));
        Assert.Equal(russianName, russian.ResolveDetailed(description));
    }

    [Theory]
    [InlineData("#341D6D")]
    [InlineData("#FF0000")]
    [InlineData("#FF00FF")]
    [InlineData("#C95E3A")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#0080FF")]
    [InlineData("#77C081")]
    [InlineData("#D2D3D8")]
    [InlineData("#ADF0D1")]
    [InlineData("#378050")]
    [InlineData("#6BC59A")]
    [InlineData("#B09E8A")]
    public void SpecificTermsDoNotConsumeAcceptedGenericOrAdjacentControls(string hex)
    {
        Assert.Null(Describe(hex).ProfessionalTerm);
    }

    [Fact]
    public void DeclarativeCatalogHasStableUniqueReachableLocalizedDefinitions()
    {
        var definitions = ProfessionalShadeCatalog.Definitions;
        var regions = definitions.SelectMany(definition => definition.Regions).ToArray();

        Assert.Equal(94, definitions.Count);
        Assert.Equal(definitions.Count, definitions.Select(item => item.StableId).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(item => item.Term).Distinct().Count());
        Assert.Equal(99, regions.Length);
        Assert.Equal(regions.Length, regions.Select(item => item.StableId).Distinct().Count());
        Assert.Equal(regions.Length, regions.Select(item => item.Priority).Distinct().Count());
        Assert.All(definitions, definition =>
        {
            Assert.StartsWith("professional-", definition.StableId, StringComparison.Ordinal);
            Assert.NotEmpty(definition.LocalizationKey);
            Assert.NotEmpty(definition.Regions);

            var english = Localizer.Create(CultureInfo.GetCultureInfo("en-US"))[definition.LocalizationKey];
            var russian = Localizer.Create(CultureInfo.GetCultureInfo("ru-RU"))[definition.LocalizationKey];
            Assert.NotEqual(definition.LocalizationKey, english);
            Assert.NotEqual(definition.LocalizationKey, russian);
            Assert.NotEqual(english, russian);
        });
        Assert.All(regions, region =>
        {
            Assert.StartsWith("professional-", region.StableId, StringComparison.Ordinal);
            Assert.NotEmpty(region.ParentFamilies);
            Assert.InRange(region.MinimumLightness, 0, 1);
            Assert.InRange(region.MaximumLightness, 0, 1.001);
            Assert.True(region.MinimumLightness < region.MaximumLightness);
            Assert.True(region.MinimumChroma < region.MaximumChroma);
            Assert.InRange(region.MinimumHue, 0, 360);
            Assert.InRange(region.MaximumHue, 0, 360);
        });
    }

    [Fact]
    public void OneProfessionalTermCanOwnMultipleBoundedRegionsWithoutDuplicatingItsIdentity()
    {
        var powderBlue = ProfessionalShadeCatalog.Get(ProfessionalColorTerm.PowderBlue);

        Assert.Equal("professional-powder-blue", powderBlue.StableId);
        Assert.Equal(2, powderBlue.Regions.Count);
        Assert.Equal(
            ["professional-powder-blue-blue", "professional-powder-blue-cyan"],
            powderBlue.Regions.Select(region => region.StableId).Order().ToArray());
    }

    [Theory]
    [InlineData(ProfessionalColorTerm.Gold, "professional-gold", 2)]
    [InlineData(ProfessionalColorTerm.Khaki, "professional-khaki", 2)]
    [InlineData(ProfessionalColorTerm.Mauve, "professional-mauve", 2)]
    [InlineData(ProfessionalColorTerm.BrickRed, "professional-brick-red", 2)]
    public void ThirdWaveCompositeTermsKeepOneIdentityAcrossEvidenceLobes(
        object term,
        string stableId,
        int expectedRegions)
    {
        var definition = ProfessionalShadeCatalog.Get((ProfessionalColorTerm)term);

        Assert.Equal(stableId, definition.StableId);
        Assert.Equal(expectedRegions, definition.Regions.Count);
        Assert.All(
            definition.Regions,
            region => Assert.StartsWith(stableId, region.StableId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("#C1F80A", ProfessionalColorTerm.Chartreuse)]
    [InlineData("#6B8E23", ProfessionalColorTerm.OliveDrab)]
    [InlineData("#E17701", ProfessionalColorTerm.Pumpkin)]
    [InlineData("#A83C09", ProfessionalColorTerm.Rust)]
    [InlineData("#87AE73", ProfessionalColorTerm.Sage)]
    [InlineData("#4682B4", ProfessionalColorTerm.SteelBlue)]
    [InlineData("#0047AB", ProfessionalColorTerm.Cobalt)]
    [InlineData("#C79FEF", ProfessionalColorTerm.Lavender)]
    [InlineData("#5363D9", ProfessionalColorTerm.Periwinkle)]
    [InlineData("#A47333", ProfessionalColorTerm.Pumpkin)]
    [InlineData("#AE7845", ProfessionalColorTerm.Pumpkin)]
    public void ThirdWaveRegionsPreserveAcceptedNeighboringTerms(string hex, object expectedTerm)
    {
        Assert.Equal((ProfessionalColorTerm)expectedTerm, Describe(hex).ProfessionalTerm);
    }

    [Fact]
    public void EveryDeclaredRegionHasAReachableWinningPoint()
    {
        foreach (var definition in ProfessionalShadeCatalog.Definitions)
        {
            foreach (var region in definition.Regions)
            {
                Assert.True(
                    CanWin(definition.Term, region),
                    $"Region '{region.StableId}' is unreachable because it never wins inside its declared bounds.");
            }
        }
    }

    [Fact]
    public void ExplanationNamesWinningRegionAndWhyCompetingRegionsFail()
    {
        var color = OklchColor.FromSrgb(199, 159, 239);

        var explanation = ProfessionalShadeClassifier.Explain(
            color,
            PerceptualColorRole.Chromatic,
            PerceptualHueFamily.BlueViolet);

        Assert.NotNull(explanation.Winner);
        Assert.Equal(ProfessionalColorTerm.Lavender, explanation.Winner.Term);
        Assert.Equal("professional-lavender-core", explanation.Winner.RegionStableId);
        Assert.Contains(explanation.Candidates, item =>
            item.Term == ProfessionalColorTerm.Periwinkle &&
            !item.Matched &&
            item.FailureReason == "hue");
    }

    [Theory]
    [InlineData(0.68, 0.12, 291.999, ProfessionalColorTerm.Periwinkle)]
    [InlineData(0.68, 0.12, 292.000, ProfessionalColorTerm.Lavender)]
    [InlineData(0.719999, 0.15, 245, ProfessionalColorTerm.Azure)]
    [InlineData(0.720000, 0.15, 245, ProfessionalColorTerm.SkyBlue)]
    [InlineData(0.479999, 0.15, 150, ProfessionalColorTerm.ForestGreen)]
    [InlineData(0.480000, 0.15, 150, ProfessionalColorTerm.Emerald)]
    public void AdjacentSpecificRegionsUseExplicitNonOverlappingBoundaries(
        double lightness,
        double chroma,
        double hue,
        object expected)
    {
        var family = hue switch
        {
            < 200 => PerceptualHueFamily.Green,
            < 270 => PerceptualHueFamily.Blue,
            _ => PerceptualHueFamily.BlueViolet
        };

        Assert.Equal(
            (ProfessionalColorTerm)expected,
            ProfessionalShadeClassifier.Classify(
                new OklchColor(lightness, chroma, hue),
                PerceptualColorRole.Chromatic,
                family));
    }

    [Theory]
    [InlineData(0.86, 0.22, 137.999, PerceptualHueFamily.YellowGreen, ProfessionalColorTerm.Chartreuse)]
    [InlineData(0.86, 0.22, 138.000, PerceptualHueFamily.Green, ProfessionalColorTerm.Lime)]
    [InlineData(0.45, 0.17, 269.999, PerceptualHueFamily.Blue, ProfessionalColorTerm.Cobalt)]
    [InlineData(0.45, 0.17, 270.000, PerceptualHueFamily.BlueViolet, null)]
    [InlineData(0.60, 0.104999, 245, PerceptualHueFamily.CyanBlue, ProfessionalColorTerm.SteelBlue)]
    [InlineData(0.60, 0.105000, 245, PerceptualHueFamily.CyanBlue, ProfessionalColorTerm.Cerulean)]
    [InlineData(0.60, 0.159999, 245, PerceptualHueFamily.Blue, ProfessionalColorTerm.Cerulean)]
    [InlineData(0.60, 0.160000, 245, PerceptualHueFamily.Blue, ProfessionalColorTerm.Azure)]
    public void SecondWaveBoundariesHaveExplicitAdjacentBehavior(
        double lightness,
        double chroma,
        double hue,
        object family,
        object? expected)
    {
        Assert.Equal(
            (ProfessionalColorTerm?)expected,
            ProfessionalShadeClassifier.Classify(
                new OklchColor(lightness, chroma, hue),
                PerceptualColorRole.Chromatic,
                (PerceptualHueFamily)family));
    }

    [Theory]
    [InlineData(0.90, 0.15, 99.999, PerceptualHueFamily.Yellow, ProfessionalColorTerm.Gold)]
    [InlineData(0.90, 0.15, 100.000, PerceptualHueFamily.Yellow, ProfessionalColorTerm.Lemon)]
    [InlineData(0.70, 0.13, 69.999, PerceptualHueFamily.Ochre, ProfessionalColorTerm.Caramel)]
    [InlineData(0.70, 0.12, 78.000, PerceptualHueFamily.Ochre, ProfessionalColorTerm.Gold)]
    [InlineData(0.60, 0.121999, 64, PerceptualHueFamily.Ochre, ProfessionalColorTerm.Copper)]
    [InlineData(0.60, 0.122000, 64, PerceptualHueFamily.Ochre, ProfessionalColorTerm.Caramel)]
    [InlineData(0.60, 0.14, 154.999, PerceptualHueFamily.Green, ProfessionalColorTerm.Emerald)]
    [InlineData(0.60, 0.14, 155.000, PerceptualHueFamily.Green, ProfessionalColorTerm.Jade)]
    [InlineData(0.519999, 0.19, 266, PerceptualHueFamily.Blue, ProfessionalColorTerm.Cobalt)]
    [InlineData(0.520000, 0.19, 266, PerceptualHueFamily.Blue, ProfessionalColorTerm.RoyalBlue)]
    public void ThirdWaveBoundariesHaveExplicitSiblingBehavior(
        double lightness,
        double chroma,
        double hue,
        object family,
        object expected)
    {
        Assert.Equal(
            (ProfessionalColorTerm)expected,
            ProfessionalShadeClassifier.Classify(
                new OklchColor(lightness, chroma, hue),
                PerceptualColorRole.Chromatic,
                (PerceptualHueFamily)family));
    }

    [Theory]
    [InlineData(0.58, 0.22, 4.999, PerceptualHueFamily.Crimson, PerceptualColorRole.Chromatic, null)]
    [InlineData(0.58, 0.22, 5.000, PerceptualHueFamily.Crimson, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Ruby)]
    [InlineData(0.419999, 0.18, 12, PerceptualHueFamily.Crimson, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Wine)]
    [InlineData(0.420000, 0.18, 12, PerceptualHueFamily.Crimson, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Cranberry)]
    [InlineData(0.449999, 0.10, 166, PerceptualHueFamily.Turquoise, PerceptualColorRole.Chromatic, null)]
    [InlineData(0.450000, 0.10, 166, PerceptualHueFamily.Turquoise, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Viridian)]
    [InlineData(0.799999, 0.08, 146, PerceptualHueFamily.Green, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Sage)]
    [InlineData(0.800000, 0.08, 146, PerceptualHueFamily.Green, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Celadon)]
    [InlineData(0.934999, 0.03, 75, PerceptualHueFamily.Cream, PerceptualColorRole.NearWhite,
        ProfessionalColorTerm.Eggshell)]
    [InlineData(0.935000, 0.03, 75, PerceptualHueFamily.Cream, PerceptualColorRole.NearWhite,
        ProfessionalColorTerm.AntiqueWhite)]
    [InlineData(0.879999, 0.08, 96, PerceptualHueFamily.Yellow, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Khaki)]
    [InlineData(0.880000, 0.08, 96, PerceptualHueFamily.Yellow, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Vanilla)]
    [InlineData(0.90, 0.15, 97.999, PerceptualHueFamily.Yellow, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.NaplesYellow)]
    [InlineData(0.90, 0.15, 98.000, PerceptualHueFamily.Yellow, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Gold)]
    [InlineData(0.399999, 0.05, 40, PerceptualHueFamily.Brown, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Espresso)]
    [InlineData(0.400000, 0.05, 40, PerceptualHueFamily.Brown, PerceptualColorRole.Chromatic,
        ProfessionalColorTerm.Chocolate)]
    public void FourthWaveBoundariesPreserveExplicitSiblingFallbacks(
        double lightness,
        double chroma,
        double hue,
        object family,
        object role,
        object? expected)
    {
        Assert.Equal(
            (ProfessionalColorTerm?)expected,
            ProfessionalShadeClassifier.Classify(
                new OklchColor(lightness, chroma, hue),
                (PerceptualColorRole)role,
                (PerceptualHueFamily)family));
    }

    [Theory]
    [InlineData("#FFD700", "Gold", "Золотистый")]
    [InlineData("#C3B091", "Khaki", "Хаки")]
    [InlineData("#B87333", "Copper", "Медный")]
    [InlineData("#4A0100", "Mahogany", "Махагоновый")]
    [InlineData("#AF6F09", "Caramel", "Карамельный")]
    [InlineData("#FFF700", "Lemon", "Лимонный")]
    [InlineData("#00A86B", "Jade", "Нефритовый")]
    [InlineData("#4169E1", "Royal blue", "Королевский синий")]
    public void ThirdWavePrimaryNamesAreConciseReviewedEnglishAndRussian(
        string hex,
        string englishName,
        string russianName)
    {
        var description = Describe(hex);
        var english = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("en-US")));
        var russian = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("ru-RU")));

        Assert.Equal(englishName, english.ResolveShort(description));
        Assert.Equal(englishName, english.ResolveDetailed(description));
        Assert.Equal(russianName, russian.ResolveShort(description));
        Assert.Equal(russianName, russian.ResolveDetailed(description));
    }

    [Theory]
    [InlineData("#C8A2C8", "Lilac", "Лиловый")]
    [InlineData("#6C5472", "Mauve", "Розовато-лиловый")]
    [InlineData("#6495ED", "Cornflower blue", "Васильковый")]
    [InlineData("#4F7942", "Fern green", "Папоротниковый")]
    [InlineData("#F4C430", "Saffron", "Шафрановый")]
    [InlineData("#E34234", "Vermilion", "Киноварь")]
    [InlineData("#F0EAD6", "Eggshell", "Яичная скорлупа")]
    [InlineData("#2A3439", "Gunmetal gray", "Оружейно-серый")]
    [InlineData("#E0115F", "Ruby", "Рубиновый")]
    [InlineData("#9E003A", "Cranberry", "Клюквенный")]
    [InlineData("#40826D", "Viridian", "Виридиановый")]
    [InlineData("#ACE1AF", "Celadon", "Селадоновый")]
    [InlineData("#FAEBD7", "Antique white", "Античный белый")]
    [InlineData("#F3E5AB", "Vanilla", "Ванильный")]
    [InlineData("#FADA5F", "Naples yellow", "Неаполитанский жёлтый")]
    [InlineData("#4E312D", "Espresso", "Эспрессо")]
    public void MultiWavePrimaryNamesAreConciseReviewedEnglishAndRussian(
        string hex,
        string englishName,
        string russianName)
    {
        var description = Describe(hex);
        var english = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("en-US")));
        var russian = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("ru-RU")));

        Assert.Equal(englishName, english.ResolveShort(description));
        Assert.Equal(englishName, english.ResolveDetailed(description));
        Assert.Equal(russianName, russian.ResolveShort(description));
        Assert.Equal(russianName, russian.ResolveDetailed(description));
    }

    [Theory]
    [InlineData("#C79FEF", "Lavender", "Лавандовый", "Blue-violet", "Сине-фиолетовый")]
    [InlineData("#87AE73", "Sage", "Шалфейный", "Green", "Зелёный")]
    [InlineData("#80013F", "Wine", "Винный", "Crimson", "Малиновый")]
    [InlineData("#343837", "Charcoal", "Угольный", "Neutral", "Нейтральный")]
    public void SpecificPrimaryNameAndGenericDetailToneRemainSeparate(
        string hex,
        string englishName,
        string russianName,
        string englishTone,
        string russianTone)
    {
        var description = Describe(hex);
        var english = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("en-US")));
        var russian = new PerceptualColorNameResolver(Localizer.Create(CultureInfo.GetCultureInfo("ru-RU")));

        Assert.Equal(englishName, english.ResolveShort(description));
        Assert.Equal(englishName, english.ResolveDetailed(description));
        Assert.Equal(russianName, russian.ResolveShort(description));
        Assert.Equal(russianName, russian.ResolveDetailed(description));
        Assert.Equal(englishTone, english.ResolveDetailTone(description));
        Assert.Equal(russianTone, russian.ResolveDetailTone(description));
    }

    private static PerceptualColorDescription Describe(string hex) =>
        ColorSampleSemantics.Describe(new ColorSample(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16),
            byte.MaxValue,
            $"rgb-{hex[1..].ToLowerInvariant()}",
            null,
            ColorSampleAccuracy.Exact));

    private static bool CanWin(
        ProfessionalColorTerm term,
        ProfessionalShadeRegionDefinition region)
    {
        var lightnesses = InteriorSamples(region.MinimumLightness, region.MaximumLightness);
        var chromas = InteriorSamples(region.MinimumChroma, region.MaximumChroma);
        var hues = HueInteriorSamples(region.MinimumHue, region.MaximumHue);

        foreach (var role in Enum.GetValues<PerceptualColorRole>())
        {
            foreach (var family in region.ParentFamilies)
            {
                foreach (var lightness in lightnesses)
                {
                    foreach (var chroma in chromas)
                    {
                        foreach (var hue in hues)
                        {
                            var explanation = ProfessionalShadeClassifier.Explain(
                                new OklchColor(lightness, chroma, hue), role, family);
                            if (explanation.Winner?.Term == term &&
                                explanation.Winner.RegionStableId == region.StableId)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    private static double[] InteriorSamples(double minimum, double maximum)
    {
        var epsilon = Math.Min(0.000001, (maximum - minimum) / 4);
        return [minimum + epsilon, (minimum + maximum) / 2, maximum - epsilon];
    }

    private static double[] HueInteriorSamples(double minimum, double maximum)
    {
        if (minimum <= maximum)
        {
            return InteriorSamples(minimum, maximum);
        }

        var span = 360 - minimum + maximum;
        var epsilon = Math.Min(0.000001, span / 4);
        return
        [
            (minimum + epsilon) % 360,
            (minimum + span / 2) % 360,
            (maximum - epsilon + 360) % 360
        ];
    }
}