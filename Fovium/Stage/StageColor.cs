using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fovium.Stage;

[JsonConverter(typeof(StageColorJsonConverter))]
internal readonly record struct StageColor(byte Red, byte Green, byte Blue)
{
    public string ToHex() => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public override string ToString() => ToHex();

    public static bool TryParse(string? value, out StageColor color)
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

        color = new StageColor(red, green, blue);
        return true;
    }
}

internal sealed class StageColorJsonConverter : JsonConverter<StageColor>
{
    public override StageColor Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return StageColor.TryParse(value, out var color)
            ? color
            : throw new JsonException("Stage colors must use canonical #RRGGBB format.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        StageColor value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToHex());
}
