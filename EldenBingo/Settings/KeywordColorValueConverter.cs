using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EldenBingo.Settings;

// Keeps the internal ARGB representation while making keyword JSON readable.
internal sealed class KeywordColorValueConverter : JsonConverter<int>
{
    public KeywordColorValueConverter() { }

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var legacy)) return legacy;
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString()!.Trim();
            var channels = text.Split(',');
            if (channels.Length == 3)
            {
                var rgb = new int[3];
                if (channels.Select((part, i) => int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out rgb[i]) && rgb[i] is >= 0 and <= 255).All(valid => valid))
                    return Color.FromArgb(rgb[0], rgb[1], rgb[2]).ToArgb();
            }
            var named = Color.FromName(text);
            if (named.IsKnownColor && named.A == 255) return named.ToArgb();
        }
        throw new JsonException("Keyword colors must be an opaque color name, an RGB string such as '255, 128, 192', or a legacy ARGB integer.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        var color = Color.FromArgb(value);
        // Keep invalid legacy alpha values numeric so normal profile validation
        // rejects them rather than silently changing their meaning.
        if (color.A != 255) { writer.WriteNumberValue(value); return; }
        foreach (var known in Enum.GetValues<KnownColor>())
        {
            var named = Color.FromKnownColor(known);
            if (!named.IsSystemColor && named.A == 255 && named.ToArgb() == value)
            {
                writer.WriteStringValue(named.Name);
                return;
            }
        }
        writer.WriteStringValue(FormattableString.Invariant($"{color.R}, {color.G}, {color.B}"));
    }
}
