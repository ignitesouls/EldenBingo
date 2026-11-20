using EldenBingoCommon;
using System.Text.Json;

namespace EldenBingo.Settings;

internal sealed class MatchSettingsPreset
{
    public string Name { get; set; } = "";
    public BingoGameSettings Settings { get; set; }
    public override string ToString() => Name;
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Trim().Length <= 60 && Settings.BoardSize >= BingoConstants.BoardSizeMin &&
        Settings.BoardSize <= BingoConstants.BoardSizeMax && Settings.NumberOfClasses is >= 1 and <= 10 && Settings.CategoryLimit is >= 0 and <= 25 &&
        Settings.PreparationTime >= 0 && Settings.PointsPerBingoLine >= 0 && Settings.ValidClasses != null &&
        Settings.ValidClasses.All(c => Enum.IsDefined(typeof(EldenRingClasses), c)) && (!Settings.RandomClasses || Settings.ValidClasses.Count >= Settings.NumberOfClasses);
    public static BingoGameSettings Copy(BingoGameSettings settings) => settings with { ValidClasses = new HashSet<EldenRingClasses>(settings.ValidClasses) };
    public static List<MatchSettingsPreset> ReadSaved(string json)
    {
        try { return (JsonSerializer.Deserialize<List<MatchSettingsPreset>>(json) ?? new()).Where(p => p != null && p.IsValid).DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(); }
        catch (JsonException) { return new(); }
    }
    public static string Describe(BingoGameSettings settings) => $"{settings.BoardSize} × {settings.BoardSize} · {(settings.Lockout ? "Lockout" : "Non-lockout")}\n" +
        $"Preparation: {settings.PreparationTime}s · Seed: {(settings.RandomSeed == 0 ? "Random" : settings.RandomSeed)}\n" +
        $"Classes: {(settings.RandomClasses ? $"Pick {settings.NumberOfClasses} from {settings.ValidClasses.Count}" : "Unrestricted")} · Category limit: {settings.CategoryLimit}\n" +
        $"Bonus per bingo: {settings.PointsPerBingoLine}";
}
