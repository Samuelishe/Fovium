using System.Globalization;
using System.Text;
using Fovium.ColorPicking;
using Fovium.Localization;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorNameDisplayCatalogTests
{
    [Fact]
    public void EnglishUsesCanonicalName()
    {
        var resolver = CreateResolver("en", ColorNameDisplayCatalog.CreateForTests());
        var sample = CreateSample();

        Assert.Equal("Sky Fall", resolver.Resolve(sample));
    }

    [Fact]
    public void RussianUsesLocalizedName()
    {
        var resolver = CreateResolver(
            "ru",
            ColorNameDisplayCatalog.CreateForTests(
                new Dictionary<string, string> { ["rgb-89c6df"] = "Небопад" }));

        Assert.Equal("Небопад", resolver.Resolve(CreateSample()));
    }

    [Fact]
    public void MissingRussianEntryFallsBackToCanonicalEnglish()
    {
        var resolver = CreateResolver("ru", ColorNameDisplayCatalog.CreateForTests());

        Assert.Equal("Sky Fall", resolver.Resolve(CreateSample()));
    }

    [Fact]
    public void UnsupportedLocaleFallsBackToCanonicalEnglish()
    {
        var resolver = new ColorSampleNameResolver(
            Localizer.Create(CultureInfo.GetCultureInfo("de-DE")),
            ColorNameDisplayCatalog.ForLocale("de"));

        Assert.Equal("Sky Fall", resolver.Resolve(CreateSample()));
    }

    [Fact]
    public void LocaleChangeReusesStableSampleIdentityWithoutMatchingAgain()
    {
        var sample = CreateSample();
        var english = CreateResolver("en", ColorNameDisplayCatalog.CreateForTests());
        var russian = CreateResolver(
            "ru",
            ColorNameDisplayCatalog.CreateForTests(
                new Dictionary<string, string> { [sample.ColorNameStableId] = "Небопад" }));

        Assert.Equal("Sky Fall", english.Resolve(sample));
        Assert.Equal("Небопад", russian.Resolve(sample));
        Assert.Equal("rgb-89c6df", sample.ColorNameStableId);
        Assert.Equal("Sky Fall", sample.CanonicalName);
    }

    [Fact]
    public void CurrentSampleAndHistoryUseTheSameLocalizedIdentity()
    {
        var session = new ColorPickerSession();
        var sample = CreateSample();
        session.Commit(sample);
        var resolver = CreateResolver(
            "ru",
            ColorNameDisplayCatalog.CreateForTests(
                new Dictionary<string, string> { [sample.ColorNameStableId] = "Небопад" }));

        Assert.Equal("Небопад", resolver.Resolve(Assert.IsType<ColorSample>(session.CurrentSample)));
        Assert.Equal("Небопад", resolver.Resolve(Assert.Single(session.History).Sample));
    }

    [Fact]
    public void TransparentSemanticStillComesFromOrdinaryUiLocalization()
    {
        var transparent = new ColorSample(
            0,
            0,
            0,
            0,
            "transparent",
            null,
            ColorSampleAccuracy.Exact);
        var resolver = CreateResolver("ru", ColorNameDisplayCatalog.CreateForTests());

        Assert.Equal("Прозрачный", resolver.Resolve(transparent));
    }

    [Fact]
    public void EmbeddedRussianCatalogExactlyCoversCanonicalStableIds()
    {
        var canonicalIds = ColorNameCatalog.LoadEmbedded().Entries
            .Select(entry => entry.StableId)
            .ToHashSet(StringComparer.Ordinal);
        var russian = ColorNameDisplayCatalog.ForLocale("ru");

        Assert.Equal(ColorNameCatalog.ExpectedCount, canonicalIds.Count);
        Assert.Equal(ColorNameCatalog.ExpectedCount, russian.Count);
        Assert.Equal(russian.Count, russian.Names.Keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(russian.Names.Keys.Except(canonicalIds, StringComparer.Ordinal));
        Assert.Empty(canonicalIds.Except(russian.Names.Keys, StringComparer.Ordinal));
        Assert.DoesNotContain(
            russian.Names.Values,
            name => string.IsNullOrWhiteSpace(name) ||
                    name.Length > ColorNameDisplayCatalog.MaximumDisplayNameLength ||
                    !name.Any(character => character is >= 'А' and <= 'я' or 'Ё' or 'ё'));
        Assert.Equal("Небопад", russian.Names["rgb-89c6df"]);
        Assert.Equal("Желейный слизень", russian.Names["rgb-de6646"]);
        Assert.Equal("Папоротниковый", russian.Names["rgb-71ab62"]);
        Assert.Equal("Сланцевая гладь", russian.Names["rgb-577396"]);
        Assert.Equal("Сердитая паста", russian.Names["rgb-ffcc55"]);
    }

    [Fact]
    public void DuplicateStableIdsAreRejectedInsteadOfSilentlyOverwritten()
    {
        using var stream = JsonStream("""
                                      {
                                        "rgb-000000": "Чёрный",
                                        "rgb-000000": "Угольный"
                                      }
                                      """);

        Assert.Throws<InvalidDataException>(() => ColorNameDisplayCatalog.Load(stream));
    }

    [Fact]
    public void MalformedLocaleCatalogFallsBackToCanonicalEnglish()
    {
        var catalog = ColorNameDisplayCatalog.LoadOrFallback(() => JsonStream("{ broken"));

        Assert.Equal(0, catalog.Count);
        Assert.Equal("Sky Fall", catalog.Resolve("rgb-89c6df", "Sky Fall"));
    }

    private static ColorSampleNameResolver CreateResolver(
        string locale,
        ColorNameDisplayCatalog catalog) =>
        new(Localizer.Create(CultureInfo.GetCultureInfo(locale)), catalog);

    private static ColorSample CreateSample() => new(
        0x89,
        0xc6,
        0xdf,
        byte.MaxValue,
        "rgb-89c6df",
        "Sky Fall",
        ColorSampleAccuracy.Exact);

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));
}