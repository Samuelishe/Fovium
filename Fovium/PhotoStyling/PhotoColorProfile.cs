using System.Collections.Immutable;
using Fovium.ColorSemantics;
using Fovium.Stage;

namespace Fovium.PhotoStyling;

internal sealed record PhotoColorProfileColor(
    StageColor Color,
    PerceptualColorDescription Description,
    ProfessionalShadeMatch? ProfessionalMatch,
    ColorNameMatch CreativeName)
{
    // Semantic records retain value data and references to shared localization/catalog strings.
    public const long EstimatedRetainedBytes = 160;
}

internal readonly record struct PhotoColorProfilePaletteEntry(
    PhotoColorProfileColor Color,
    double Weight);

internal sealed record PhotoColorProfile(
    PhotoColorProfileColor Dominant,
    PhotoColorProfileColor Average,
    ImmutableArray<PhotoColorProfilePaletteEntry> Palette,
    ImmutableArray<PhotoColorProfileColor> NotableColors)
{
    public long RetainedBytes => checked(
        104L +
        ((2L + Palette.Length + NotableColors.Length) * PhotoColorProfileColor.EstimatedRetainedBytes) +
        (Palette.Length * 16L));
}

internal interface IPhotoColorProfileProjector
{
    PhotoColorProfile? Create(PhotoStyleAnalysis analysis);
}

internal sealed class PhotoColorProfileProjector : IPhotoColorProfileProjector
{
    private readonly Lazy<ColorNameMatcher> _matcher;

    public PhotoColorProfileProjector(ColorNameMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        _matcher = new Lazy<ColorNameMatcher>(() => matcher);
    }

    public PhotoColorProfileProjector()
    {
        _matcher = new Lazy<ColorNameMatcher>(
            static () => new ColorNameMatcher(ColorNameCatalog.LoadEmbedded()),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public PhotoColorProfile? Create(PhotoStyleAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (analysis.VisibleSampleCount == 0 || analysis.Palette.IsDefaultOrEmpty)
        {
            return null;
        }

        var palette = analysis.Palette
            .Select(entry => new PhotoColorProfilePaletteEntry(
                Describe(entry.Color),
                entry.Weight))
            .ToImmutableArray();
        return new PhotoColorProfile(
            Describe(analysis.DominantColor),
            Describe(analysis.AverageColor),
            palette,
            analysis.NotableColors
                .Select(entry => Describe(entry.Color))
                .ToImmutableArray());
    }

    private PhotoColorProfileColor Describe(StageColor color)
    {
        var description = PerceptualColorClassifier.Describe(color.Red, color.Green, color.Blue);
        var professionalMatch = ProfessionalShadeClassifier.ClassifyMatch(
            description.Oklch!.Value,
            description.Role!.Value,
            description.HueFamily!.Value);
        return new PhotoColorProfileColor(
            color,
            description,
            professionalMatch,
            _matcher.Value.FindNearest(color.Red, color.Green, color.Blue));
    }
}