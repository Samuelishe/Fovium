using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fovium.Presentation;

[JsonConverter(typeof(PresentationColorJsonConverter))]
internal readonly record struct PresentationColor(byte Red, byte Green, byte Blue)
{
    public string ToHex() => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public static bool TryParse(string? value, out PresentationColor color)
    {
        color = default;
        if (value is not { Length: 7 } || value[0] != '#')
        {
            return false;
        }

        if (!byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var red) ||
            !byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var green) ||
            !byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue))
        {
            return false;
        }

        color = new PresentationColor(red, green, blue);
        return true;
    }
}

internal sealed class PresentationColorJsonConverter : JsonConverter<PresentationColor>
{
    public override PresentationColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return PresentationColor.TryParse(value, out var color)
            ? color
            : throw new JsonException("Presentation colors must use canonical #RRGGBB format.");
    }

    public override void Write(Utf8JsonWriter writer, PresentationColor value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToHex());
}

internal sealed record PresentationSettings
{
    public const double MinimumHighlightOpacity = 0.05;
    public const double MaximumHighlightOpacity = 0.90;
    public const double MinimumHighlightRadiusPhysicalPixels = 8;
    public const double MaximumHighlightRadiusPhysicalPixels = 256;
    public const double MinimumMarkupStrokePhysicalPixels = 1;
    public const double MaximumMarkupStrokePhysicalPixels = 128;
    public const double MinimumMarkupOpacity = 0.05;
    public const double MaximumMarkupOpacity = 1;

    public bool MarkupToolsEnabled { get; init; } = true;

    public PresentationColor HighlightColor { get; init; } = new(0xFF, 0xD5, 0x4F);

    public double HighlightOpacity { get; init; } = 0.30;

    public double HighlightRadiusPhysicalPixels { get; init; } = 42;

    public PresentationColor DefaultMarkupColor { get; init; } = new(0xFF, 0x45, 0x45);

    public double DefaultMarkupStrokePhysicalPixels { get; init; } = 4;

    public double DefaultMarkupOpacity { get; init; } = 1;

    public FloatingOverlayPlacement MarkupDockPlacement { get; init; } =
        FloatingOverlayPlacement.Default;

    public FloatingOverlayPlacement PhotoInfoPlacement { get; init; } =
        FloatingOverlayPlacement.BottomLeft;

    public FloatingOverlayPlacement HistogramPlacement { get; init; } =
        FloatingOverlayPlacement.BottomRight;

    public static PresentationSettings Default { get; } = new();

    public PresentationSettings AdjustHighlightRadius(double deltaPhysicalPixels)
    {
        if (!double.IsFinite(deltaPhysicalPixels) || deltaPhysicalPixels == 0)
        {
            return this;
        }

        return this with
        {
            HighlightRadiusPhysicalPixels = Math.Clamp(
                HighlightRadiusPhysicalPixels + deltaPhysicalPixels,
                MinimumHighlightRadiusPhysicalPixels,
                MaximumHighlightRadiusPhysicalPixels),
        };
    }

    public PresentationSettings Normalize() => this with
    {
        HighlightOpacity = NormalizeFinite(
            HighlightOpacity,
            Default.HighlightOpacity,
            MinimumHighlightOpacity,
            MaximumHighlightOpacity),
        HighlightRadiusPhysicalPixels = NormalizeFinite(
            HighlightRadiusPhysicalPixels,
            Default.HighlightRadiusPhysicalPixels,
            MinimumHighlightRadiusPhysicalPixels,
            MaximumHighlightRadiusPhysicalPixels),
        DefaultMarkupStrokePhysicalPixels = NormalizeFinite(
            DefaultMarkupStrokePhysicalPixels,
            Default.DefaultMarkupStrokePhysicalPixels,
            MinimumMarkupStrokePhysicalPixels,
            MaximumMarkupStrokePhysicalPixels),
        DefaultMarkupOpacity = NormalizeFinite(
            DefaultMarkupOpacity,
            Default.DefaultMarkupOpacity,
            MinimumMarkupOpacity,
            MaximumMarkupOpacity),
        MarkupDockPlacement = MarkupDockPlacement.Normalize(),
        PhotoInfoPlacement = PhotoInfoPlacement.Normalize(FloatingOverlayPlacement.BottomLeft),
        HistogramPlacement = HistogramPlacement.Normalize(FloatingOverlayPlacement.BottomRight),
    };

    private static double NormalizeFinite(double value, double fallback, double minimum, double maximum) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
