using System.Collections.Immutable;
using System.Globalization;
using Fovium.ColorSemantics;
using Fovium.PhotoStyling;
using Fovium.Stage;

namespace Fovium.Metadata;

internal sealed record PhotoColorProfileDisplayColor(
    StageColor Color,
    string StructuralName,
    string CreativeName,
    string Hex,
    string Oklch);

internal sealed record PhotoColorProfileDisplayEntry(
    PhotoColorProfileDisplayColor Color,
    string Share);

internal sealed record PhotoColorProfilePresentation(
    PhotoColorProfileDisplayColor Dominant,
    PhotoColorProfileDisplayColor Average,
    ImmutableArray<PhotoColorProfileDisplayEntry> Palette,
    ImmutableArray<PhotoColorProfileDisplayColor> NotableColors);

internal static class PhotoColorProfileLayout
{
    internal const int MaximumNotableColors = 10;
    internal const int MaximumNotableColorsPerRow = 5;

    public static ImmutableArray<ImmutableArray<PhotoColorProfileDisplayColor>> ArrangeNotableColors(
        ImmutableArray<PhotoColorProfileDisplayColor> colors)
    {
        if (colors.IsDefaultOrEmpty)
        {
            return [];
        }

        var bounded = colors.Take(MaximumNotableColors).ToArray();
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<PhotoColorProfileDisplayColor>>(
            (bounded.Length + MaximumNotableColorsPerRow - 1) / MaximumNotableColorsPerRow);
        for (var offset = 0; offset < bounded.Length; offset += MaximumNotableColorsPerRow)
        {
            rows.Add(bounded
                .Skip(offset)
                .Take(MaximumNotableColorsPerRow)
                .ToImmutableArray());
        }

        return rows.MoveToImmutable();
    }
}

internal static class PhotoColorProfilePresenter
{
    public static PhotoColorProfilePresentation Format(
        PhotoColorProfile profile,
        CultureInfo culture,
        PerceptualColorNameResolver structuralNames,
        ColorNameDisplayCatalog creativeNames)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(structuralNames);
        ArgumentNullException.ThrowIfNull(creativeNames);

        return new PhotoColorProfilePresentation(
            FormatColor(profile.Dominant, structuralNames, creativeNames),
            FormatColor(profile.Average, structuralNames, creativeNames),
            profile.Palette.Select(entry => new PhotoColorProfileDisplayEntry(
                    FormatColor(entry.Color, structuralNames, creativeNames),
                    FormatShare(entry.Weight, culture)))
                .ToImmutableArray(),
            profile.NotableColors
                .Select(color => FormatColor(color, structuralNames, creativeNames))
                .ToImmutableArray());
    }

    private static string FormatShare(double weight, CultureInfo culture) =>
        weight is > 0 and < 0.01
            ? weight.ToString("P1", culture)
            : weight.ToString("P0", culture);

    private static PhotoColorProfileDisplayColor FormatColor(
        PhotoColorProfileColor color,
        PerceptualColorNameResolver structuralNames,
        ColorNameDisplayCatalog creativeNames) => new(
        color.Color,
        structuralNames.ResolveShort(color.Description),
        creativeNames.Resolve(color.CreativeName.StableId, color.CreativeName.CanonicalName),
        color.Color.ToHex(),
        PerceptualColorNameResolver.FormatOklch(color.Description.Oklch!.Value));
}