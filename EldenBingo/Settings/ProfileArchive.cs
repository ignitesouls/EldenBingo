using System.Text;
using System.Text.Json;

namespace EldenBingo.Settings;

internal sealed class ProfileArchive
{
    public string Format { get; set; } = "EldenBingo.Profiles";
    public int Version { get; set; } = 1;
    public List<ColorProfile> ColorProfiles { get; set; } = new();
    public List<LayoutProfile> Layouts { get; set; } = new();
    public List<KeywordColorProfile> KeywordProfiles { get; set; } = new();
    public const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true, IgnoreReadOnlyProperties = true };

    public static ProfileArchive Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("Profile files must be smaller than 1 MB.");
        ProfileArchive? archive;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String || format.GetString() != "EldenBingo.Profiles" ||
                !document.RootElement.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
                throw new InvalidDataException("Use an Elden Bingo profile file with format version 1.");
            archive = JsonSerializer.Deserialize<ProfileArchive>(json, JsonOptions);
        }
        catch (JsonException) { throw new InvalidDataException("This is not a valid profile JSON file."); }
        if (archive == null || archive.Format != "EldenBingo.Profiles" || archive.Version != 1)
            throw new InvalidDataException("Use an Elden Bingo profile file with format version 1.");
        if (archive.ColorProfiles == null || archive.Layouts == null || archive.KeywordProfiles == null ||
            archive.ColorProfiles.Count + archive.Layouts.Count + archive.KeywordProfiles.Count is < 1 or > 200 ||
            archive.ColorProfiles.Any(p => p == null || !p.IsValid) || archive.Layouts.Any(p => p == null || !p.IsValid) || archive.KeywordProfiles.Any(p => p == null || !p.IsValid))
            throw new InvalidDataException("The file contains invalid profiles, or more than 200 profiles. Nothing was imported.");
        return archive;
    }

    public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    public static string AvailableName(string name, IEnumerable<string> used)
    {
        var names = used.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var original = name.Trim();
        var candidate = original;
        for (var i = 1; names.Contains(candidate); i++)
        {
            var suffix = i == 1 ? " (imported)" : $" (imported {i})";
            candidate = original[..Math.Min(original.Length, 60 - suffix.Length)] + suffix;
        }
        return candidate;
    }
}
