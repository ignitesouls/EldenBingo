using EldenBingoCommon;
using System.Text.Json;

namespace EldenBingo.Settings
{
    internal sealed class BoardAppearance
    {
        public int[] TeamColors { get; set; } = BingoConstants.TeamColors.Select(c => c.Color.ToArgb()).ToArray();
        public int EmptyColor { get; set; } = Color.FromArgb(18, 20, 20).ToArgb();
        public int TextColor { get; set; } = Color.FromArgb(232, 230, 227).ToArgb();
        public int BorderColor { get; set; } = Color.FromArgb(118, 110, 97).ToArgb();
        public int BorderWidth { get; set; } = 1;
        public int Spacing { get; set; } = 2;
        public int HoverIntensity { get; set; } = 14;
        public bool AutoContrast { get; set; }

        public bool IsValid => TeamColors != null && TeamColors.Length == BingoConstants.TeamColors.Length &&
            TeamColors.All(c => Color.FromArgb(c).A == 255) && Color.FromArgb(EmptyColor).A == 255 &&
            Color.FromArgb(TextColor).A == 255 && Color.FromArgb(BorderColor).A == 255 &&
            BorderWidth >= 0 && BorderWidth <= 4 && Spacing >= 0 && Spacing <= 12 && HoverIntensity >= 0 && HoverIntensity <= 30;

        public BoardAppearance Clone() => new()
        {
            TeamColors = (int[])TeamColors.Clone(), EmptyColor = EmptyColor, TextColor = TextColor,
            BorderColor = BorderColor, BorderWidth = BorderWidth, Spacing = Spacing,
            HoverIntensity = HoverIntensity, AutoContrast = AutoContrast
        };

        public static BoardAppearance Parse(string json)
        {
            try
            {
                var value = JsonSerializer.Deserialize<BoardAppearance>(json);
                return value?.IsValid == true ? value : new();
            }
            catch (JsonException) { return new(); }
        }

        public Color ColorForTeam(int team) => team >= 0 && team < TeamColors.Length
            ? Color.FromArgb(TeamColors[team]) : BingoConstants.GetTeamColor(team);

        public Color ClaimedText(int team)
        {
            if (!AutoContrast) return Color.FromArgb(TextColor);
            return ContrastingText(ColorForTeam(team));
        }

        public static Color ContrastingText(Color color)
        {
            // Compare relative luminance against the midpoint for black/white contrast.
            double linear(byte c) => c / 255d <= .04045 ? c / 255d / 12.92 : Math.Pow((c / 255d + .055) / 1.055, 2.4);
            var luminance = .2126 * linear(color.R) + .7152 * linear(color.G) + .0722 * linear(color.B);
            return luminance > .179 ? Color.Black : Color.White;
        }
    }
}
