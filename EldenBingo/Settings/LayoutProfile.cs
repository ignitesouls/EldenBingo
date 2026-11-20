using System.Text.Json;

namespace EldenBingo.Settings
{
    internal sealed class LayoutProfile
    {
        public string Name { get; set; } = "";
        public string FontName { get; set; } = "Lucida Sans Unicode";
        public float FontSize { get; set; } = 12;
        public FontStyle FontStyle { get; set; }
        public int Background { get; set; } = Color.FromArgb(20, 20, 30).ToArgb();
        public int MainWidth { get; set; } = 1100;
        public int MainHeight { get; set; } = 800;
        public int MainX { get; set; } = -9999;
        public int MainY { get; set; } = -9999;
        public int SidebarWidth { get; set; } = 200;
        public bool BoardMaximum { get; set; }
        public int BoardWidth { get; set; } = 760;
        public int BoardHeight { get; set; } = 760;
        public bool PopoutCustom { get; set; }
        public int PopoutWidth { get; set; } = 760;
        public int PopoutHeight { get; set; } = 760;
        public int PopoutX { get; set; } = -9999;
        public int PopoutY { get; set; } = -9999;
        public int PopoutOpacity { get; set; } = 100;
        public int PopoutMarkedStyle { get; set; } = 2;
        public bool PopoutToolbar { get; set; } = true;
        // Retained for older profile files; map presentation is disabled in this release.
        public bool MapCustomSize { get; set; }
        public int MapWidth { get; set; } = 960;
        public int MapHeight { get; set; } = 720;
        public bool MapCustomPosition { get; set; }
        public int MapX { get; set; }
        public int MapY { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool ShowClasses { get; set; }
        public int Shadows { get; set; } = 40;
        public bool MarkHighlight { get; set; } = true;
        public bool BingoHighlight { get; set; } = true;
        public List<LayoutKeywordColor>? KeywordColors { get; set; }
        public int KeywordColorIntensity { get; set; } = 100;
        public BoardAppearance? BoardAppearance { get; set; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Length <= 60 &&
            !string.IsNullOrWhiteSpace(FontName) && float.IsFinite(FontSize) && FontSize >= 6 && FontSize <= 72 &&
            (int)FontStyle >= 0 && (int)FontStyle <= 15 && MainWidth >= 952 && MainWidth <= 10000 &&
            MainHeight >= 566 && MainHeight <= 10000 && SidebarWidth >= 80 && SidebarWidth <= 800 &&
            (!BoardMaximum || ValidSize(BoardWidth, BoardHeight)) &&
            (!PopoutCustom || ValidSize(PopoutWidth, PopoutHeight)) &&
            Shadows >= 0 && Shadows <= 100 &&
            PopoutOpacity >= 0 && PopoutOpacity <= 100 && PopoutMarkedStyle >= 0 && PopoutMarkedStyle <= 2 &&
            KeywordColorIntensity >= 0 && KeywordColorIntensity <= 100 &&
            (KeywordColors == null || KeywordColors.All(c => c != null && !string.IsNullOrWhiteSpace(c.Keyword))) &&
            (BoardAppearance == null || BoardAppearance.IsValid);

        private static bool ValidSize(int width, int height) => width >= 100 && width <= 10000 && height >= 100 && height <= 10000;

        public static List<LayoutProfile> ReadSaved(string json)
        {
            try
            {
                return (JsonSerializer.Deserialize<List<LayoutProfile>>(json) ?? new())
                    .Where(p => p != null && p.IsValid)
                    .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
            }
            catch (JsonException) { return new(); }
        }

        public override string ToString() => Name;
    }

    internal sealed class LayoutKeywordColor
    {
        [System.Text.Json.Serialization.JsonPropertyName("Keyword")]
        public string Keyword { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("Color")]
        [System.Text.Json.Serialization.JsonConverter(typeof(KeywordColorValueConverter))]
        public int Color { get; set; }
    }
}
