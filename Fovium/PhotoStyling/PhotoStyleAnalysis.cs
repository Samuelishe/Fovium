using System.Collections.Immutable;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.PhotoStyling;

internal readonly record struct PhotoPaletteEntry(StageColor Color, double Weight);

internal readonly record struct PhotoNotableColor(
    StageColor Color,
    double SupportFraction,
    double LargestComponentFraction,
    double CoherentSupportFraction,
    double Score);

internal sealed record PhotoColorField(
    int Columns,
    int Rows,
    ImmutableArray<StageColor> Colors)
{
    public StageColor this[int column, int row] => Colors[(row * Columns) + column];

    public long RetainedBytes => checked(32L + (Colors.Length * 3L));
}

internal sealed class PhotoStyleAnalysis
{
    public PhotoStyleAnalysis(
        StageColor averageColor,
        StageColor dominantColor,
        StageColor boundaryColor,
        ImmutableArray<PhotoPaletteEntry> palette,
        PhotoColorField spatialField,
        PixelSize analyzedSize,
        int visibleSampleCount,
        TimeSpan analysisDuration,
        ImmutableArray<PhotoNotableColor> notableColors = default)
    {
        AverageColor = averageColor;
        DominantColor = dominantColor;
        BoundaryColor = boundaryColor;
        Palette = palette;
        SpatialField = spatialField;
        AnalyzedSize = analyzedSize;
        VisibleSampleCount = visibleSampleCount;
        AnalysisDuration = analysisDuration;
        NotableColors = notableColors.IsDefault ? [] : notableColors;
    }

    public StageColor AverageColor { get; }

    public StageColor DominantColor { get; }

    public StageColor BoundaryColor { get; }

    public ImmutableArray<PhotoPaletteEntry> Palette { get; }

    public PhotoColorField SpatialField { get; }

    public PixelSize AnalyzedSize { get; }

    public int VisibleSampleCount { get; }

    public TimeSpan AnalysisDuration { get; }

    public ImmutableArray<PhotoNotableColor> NotableColors { get; }

    public long RetainedBytes => checked(
        104L +
        (Palette.Length * 32L) +
        (NotableColors.Length * 48L) +
        SpatialField.RetainedBytes);
}