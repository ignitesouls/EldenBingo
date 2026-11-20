using System.Text.Json;

namespace EldenBingo.Settings
{
    internal sealed class ColorProfile
    {
        public string Name { get; set; } = "";
        public int[] TeamColors { get; set; } = new BoardAppearance().TeamColors;
        public int EmptyColor { get; set; }
        public int TextColor { get; set; }
        public int BorderColor { get; set; }
        public bool AutoContrast { get; set; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Trim().Length <= 60 &&
            new BoardAppearance { TeamColors = TeamColors, EmptyColor = EmptyColor, TextColor = TextColor, BorderColor = BorderColor }.IsValid;
        public override string ToString() => Name;

        public static ColorProfile Capture(string name, BoardAppearance style) => new()
        {
            Name = name.Trim(), TeamColors = (int[])style.TeamColors.Clone(), EmptyColor = style.EmptyColor,
            TextColor = style.TextColor, BorderColor = style.BorderColor, AutoContrast = style.AutoContrast
        };

        public void ApplyTo(BoardAppearance style)
        {
            style.TeamColors = (int[])TeamColors.Clone();
            style.EmptyColor = EmptyColor;
            style.TextColor = TextColor;
            style.BorderColor = BorderColor;
            style.AutoContrast = AutoContrast;
        }

        public bool Matches(BoardAppearance style) => TeamColors.SequenceEqual(style.TeamColors) &&
            EmptyColor == style.EmptyColor && TextColor == style.TextColor && BorderColor == style.BorderColor && AutoContrast == style.AutoContrast;

        public static List<ColorProfile> ReadSaved(string json)
        {
            try
            {
                return (JsonSerializer.Deserialize<List<ColorProfile?>>(json) ?? new())
                    .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name) && p.Name.Trim().Length <= 60 &&
                        new BoardAppearance { TeamColors = p.TeamColors, EmptyColor = p.EmptyColor, TextColor = p.TextColor, BorderColor = p.BorderColor }.IsValid)
                    .Select(p => { p!.Name = p.Name.Trim(); return p!; })
                    .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (JsonException) { return new(); }
        }
    }
}
