using EldenBingo.Settings;
using EldenBingoCommon;

namespace EldenBingo.UI
{
    public partial class SettingsDialog
    {
        private BoardAppearance _appearance = BoardAppearanceSettings.Current.Clone();
        private readonly List<Button> _teamColorButtons = new();
        private readonly List<Action> _refreshAppearanceControls = new();
        private bool _loadingAppearance;
        private TabControl _boardStyleTabs = null!;

        private void initializeBoardAppearance()
        {
            var page = new TabPage("Board style") { Padding = new Padding(8), AutoScroll = true };
            var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(465, 0),
                Text = "Customize how teams and squares look on your screen. Team names and other players' colors stay unchanged." });
            var colors = rows;
            var styling = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            var profiles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            initializeColorProfiles(profiles);
            var paletteRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            paletteRow.Controls.Add(new Label { Text = "Team palette:", AutoSize = true });
            void palette(string name, string[]? colors, bool accessible = false)
            {
                var button = new Button { Text = name, AutoSize = true, MinimumSize = new Size(65, 25) };
                button.Click += (_, _) =>
                {
                    _appearance.TeamColors = colors == null ? new BoardAppearance().TeamColors : colors.Select(c => ColorTranslator.FromHtml(c).ToArgb()).ToArray();
                    if (accessible) _appearance.AutoContrast = true;
                    refreshAppearanceControls();
                };
                paletteRow.Controls.Add(button);
            }
            palette("Default", null);
            palette("Vivid", new[] { "#FF3151", "#3985FF", "#20CF63", "#FF941F", "#A969FF", "#20D7E2", "#FF65B6", "#B88155", "#FFE34A" });
            palette("Pastel", new[] { "#FF9999", "#99BEFF", "#A5DEAB", "#FFCC99", "#C6A6EB", "#99DEDE", "#EDACD3", "#C7AF99", "#F2E3A1" });
            rows.Controls.Add(paletteRow);
            rows.Controls.Add(new Label { Text = "Color-blind palettes", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
            paletteRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(455, 0) };
            // Editable starting palettes, not a simulation or a guarantee that
            // nine simultaneous teams are distinguishable for every viewer.
            palette("Protanopia", new[] { "#E69F00", "#0072B2", "#009E73", "#F0E442", "#CC79A7", "#56B4E9", "#000000", "#999999", "#D55E00" }, true);
            // Paul Tol's nine-color muted qualitative palette, arranged for teams.
            // https://sronpersonalpages.nl/~pault/ (Muted qualitative scheme).
            palette("Deuteranopia", new[] { "#CC6677", "#332288", "#117733", "#DDCC77", "#882255", "#88CCEE", "#AA4499", "#999933", "#44AA99" }, true);
            palette("Tritanopia", new[] { "#D73027", "#4575B4", "#1B7837", "#F46D43", "#762A83", "#80CDC1", "#F4A582", "#404040", "#F7F7F7" }, true);
            rows.Controls.Add(paletteRow);
            rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(455, 0),
                Text = "Starting palettes for different color-vision needs. Choose what looks clearest to you; every team color remains editable. Automatic text contrast is enabled with these palettes." });
            var teams = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 3, Margin = new Padding(0, 3, 0, 8) };
            for (var i = 0; i < BingoConstants.TeamColors.Length; i++)
            {
                var team = i;
                var button = new Button { AutoSize = false, Size = new Size(145, 29), Text = BingoConstants.GetTeamName(i),
                    AccessibleName = $"Color for {BingoConstants.GetTeamName(i)}", UseVisualStyleBackColor = false };
                button.Click += (_, _) => chooseAppearanceColor(_appearance.ColorForTeam(team), color => _appearance.TeamColors[team] = color.ToArgb());
                _teamColorButtons.Add(button);
                teams.Controls.Add(button, i % 3, i / 3);
            }
            rows.Controls.Add(teams);
            addColor("Empty squares", () => _appearance.EmptyColor, value => _appearance.EmptyColor = value);
            addColor("Square text", () => _appearance.TextColor, value => _appearance.TextColor = value);
            addColor("Borders / gaps", () => _appearance.BorderColor, value => _appearance.BorderColor = value);
            rows = styling;
            addNumber("Border thickness (px)", 0, 4, () => _appearance.BorderWidth, value => _appearance.BorderWidth = value);
            addNumber("Square spacing (px)", 0, 12, () => _appearance.Spacing, value => _appearance.Spacing = value);
            addNumber("Hover brightness (%)", 0, 30, () => _appearance.HoverIntensity, value => _appearance.HoverIntensity = value);
            var contrast = new CheckBox { Text = "Automatically contrast text on single-team squares", AutoSize = true };
            contrast.CheckedChanged += (_, _) =>
            {
                if (_loadingAppearance) return;
                _appearance.AutoContrast = contrast.Checked;
                updateLayoutPreview();
            };
            _refreshAppearanceControls.Add(() => contrast.Checked = _appearance.AutoContrast);
            colors.Controls.Add(contrast);
            var reset = new Button { Text = "Reset board style", AutoSize = true };
            reset.Click += (_, _) => { _appearance = new BoardAppearance(); refreshAppearanceControls(); };
            rows.Controls.Add(reset);
            rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(455, 0), Text = "Click any team color to choose a custom color. Preview changes here, then Save settings to apply. Board styles are included in named layouts." });
            _boardStyleTabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Board appearance sections" };
            foreach (var (title, content) in new[] { ("Team colors", colors), ("Square styling", styling), ("Saved profiles", profiles) })
            {
                var tab = new TabPage(title) { Padding = new Padding(6) };
                tab.Controls.Add(content); _boardStyleTabs.TabPages.Add(tab);
            }
            page.Controls.Add(_boardStyleTabs);
            tabControl1.TabPages.Add(page);
            refreshAppearanceControls();

            void addColor(string label, Func<int> get, Action<int> set)
            {
                var button = new Button { Size = new Size(120, 26), UseVisualStyleBackColor = false, AccessibleName = label + " color" };
                button.Click += (_, _) => chooseAppearanceColor(Color.FromArgb(get()), color => set(color.ToArgb()));
                _refreshAppearanceControls.Add(() =>
                {
                    button.BackColor = Color.FromArgb(get());
                    button.ForeColor = readableText(button.BackColor);
                    button.Text = $"#{button.BackColor.R:X2}{button.BackColor.G:X2}{button.BackColor.B:X2}";
                });
                rows.Controls.Add(makeRow(new Label { Text = label, AutoSize = false, Width = 180, Height = 24 }, button));
            }

            void addNumber(string label, int min, int max, Func<int> get, Action<int> set)
            {
                var number = dimensionInput(min, max, get(), label);
                number.ValueChanged += (_, _) =>
                {
                    if (_loadingAppearance) return;
                    set((int)number.Value);
                    updateLayoutPreview();
                };
                _refreshAppearanceControls.Add(() => number.Value = get());
                rows.Controls.Add(makeRow(new Label { Text = label, AutoSize = false, Width = 180, Height = 24 }, number));
            }
        }

        private static Color readableText(Color color) => BoardAppearance.ContrastingText(color);

        private void chooseAppearanceColor(Color original, Action<Color> apply)
        {
            using var dialog = new ColorDialog { Color = original, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            apply(dialog.Color);
            refreshAppearanceControls();
        }

        private void refreshAppearanceControls()
        {
            _loadingAppearance = true;
            for (var i = 0; i < _teamColorButtons.Count; i++)
            {
                var button = _teamColorButtons[i];
                button.BackColor = _appearance.ColorForTeam(i);
                button.ForeColor = readableText(button.BackColor);
                button.Text = BingoConstants.GetTeamName(i);
            }
            foreach (var refresh in _refreshAppearanceControls) refresh();
            _loadingAppearance = false;
            updateLayoutPreview();
        }
    }
}
