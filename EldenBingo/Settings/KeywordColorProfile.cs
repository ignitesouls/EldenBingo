using System.Text.Json;

namespace EldenBingo.Settings;

internal sealed class KeywordColorProfile
{
    public string Name { get; set; } = "";
    public List<LayoutKeywordColor> Rules { get; set; } = new();
    public int Intensity { get; set; } = 100;
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Trim().Length <= 60 && Intensity is >= 0 and <= 100 &&
        Rules != null && Rules.Count <= 500 && Rules.All(r => r != null && !string.IsNullOrWhiteSpace(r.Keyword) && Color.FromArgb(r.Color).A == 255);
    public override string ToString() => Name;
    public List<LayoutKeywordColor> CloneRules() => Rules.Select(r => new LayoutKeywordColor { Keyword = r.Keyword, Color = r.Color }).ToList();
    public bool Matches(IEnumerable<LayoutKeywordColor> rules, int intensity) => Intensity == intensity &&
        Rules.Select(r => (r.Keyword, r.Color)).SequenceEqual(rules.Select(r => (r.Keyword, r.Color)));

    public static List<KeywordColorProfile> ReadSaved(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<KeywordColorProfile>>(json) ?? new())
                .Where(p => p != null && p.IsValid).GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        }
        catch (JsonException) { return new(); }
    }
}
