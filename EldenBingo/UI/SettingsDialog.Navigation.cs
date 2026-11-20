namespace EldenBingo.UI
{
    public partial class SettingsDialog
    {
        private sealed record SettingsCategory(string Key, string Title, string Keywords, TabPage Page, bool Preview)
        {
            public override string ToString() => Title;
        }

        private readonly List<SettingsCategory> _categories = new();
        private ListBox _settingsNavigation = null!;
        private TextBox _settingsSearch = null!;
        private Label _noSettingsResults = null!;
        private bool _updatingNavigation;

        private void initializeNavigation()
        {
            var layouts = tabControl1.TabPages[3];
            var boardStyle = tabControl1.TabPages[4];
            tabControl1.TabPages.Clear();
            add("layouts", "Layouts", "custom saved named layout window arrangement streaming", layouts, true);
            add("board-style", "Team colors & squares", "palette profiles import export backup saved named switch vivid pastel border gap spacing contrast hover brightness", boardStyle, true);
            add("text-effects", "Text & effects", "font size keyword profiles saved switch import export shadows highlight flashing bingo", createTextEffectsPage(), true);
            add("board-size", "Board & pop-out size", "width height maximum dimensions overlay capture stream", cards("Board and pop-out sizing", groupBox2, _popoutBoardSizeGroupBox), true);
            add("keyboard", "Keyboard", "hotkey shortcut arrow numpad diagonal mark star count modifiers shift control alt out of focus click navigation mouse middle dim scroll presets disable", createKeyboardPage(), false);
            add("audio", "Audio", "sound volume mute claim bingo start pause resume finish alert output device sfx test", createAudioPage(), false);
            add("squareset-testing", "Squareset testing", "json randomization distribution generator weights categories regions center points simulation probability", cards("Squareset randomization", createSquaresetTestingCard()), false);
            groupBox9.Text = "Stream / spectator delay";
            add("spectator", "Spectating", "stream delay timer milliseconds match events", cards("Spectator timing", groupBox9), false);
            add("connection", "Server hosting", "connect server port hosting lobby room code", cards("Server hosting", groupBox5), false);
            groupBox3.Text = "Window appearance";
            add("application", "Application", "background color always on top updates startup", cards("Application preferences", groupBox3, groupBox10), true);
            add("guides", "Help & guides", "help how to instructions squareset json categories weights regions center point-value timed tokens freezing unclaim colors palettes keyword profiles shortcuts focus search local url export overlay troubleshooting", createGuidePage(), false);

            tabControl1.Appearance = TabAppearance.FlatButtons;
            tabControl1.ItemSize = new Size(0, 1);
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.Multiline = false;
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 182, Padding = new Padding(8) };
            _settingsSearch = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Search settings…", AccessibleName = "Search settings" };
            _settingsNavigation = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                IntegralHeight = false, ItemHeight = 34, DrawMode = DrawMode.OwnerDrawFixed, AccessibleName = "Settings categories" };
            _settingsNavigation.DrawItem += (_, e) =>
            {
                if (e.Index < 0 || e.Index >= _settingsNavigation.Items.Count) return;
                e.DrawBackground();
                var color = (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : SystemColors.ControlText;
                TextRenderer.DrawText(e.Graphics, _settingsNavigation.Items[e.Index].ToString(), e.Font,
                    new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                e.DrawFocusRectangle();
            };
            var spacer = new Panel { Dock = DockStyle.Top, Height = 10 };
            sidebar.Controls.Add(_settingsNavigation);
            sidebar.Controls.Add(spacer);
            sidebar.Controls.Add(_settingsSearch);
            var body = tabControl1.Parent!;
            _noSettingsResults = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Text = "No settings match your search.\nTry a different word or clear the search.", Visible = false };
            body.Controls.Add(_noSettingsResults);
            body.Controls.Add(sidebar);
            tabControl1.BringToFront();
            _settingsNavigation.SelectedIndexChanged += (_, _) =>
            {
                if (!_updatingNavigation && _settingsNavigation.SelectedItem is SettingsCategory category)
                    tabControl1.SelectedTab = category.Page;
            };
            tabControl1.SelectedIndexChanged += (_, _) => syncNavigation();
            _settingsSearch.TextChanged += (_, _) => filterSettings();
            SizeChanged += (_, _) => updatePreviewVisibility();
            FormClosing += (_, _) => Properties.Settings.Default.LastSettingsCategory =
                _categories.FirstOrDefault(c => c.Page == tabControl1.SelectedTab)?.Key ?? "layouts";
            KeyPreview = true;
            KeyDown += (_, e) =>
            {
                if (e.Handled) return;
                if (e.Control && e.KeyCode == Keys.F)
                {
                    var search = tabControl1.SelectedTab?.Name == "settings-guides" ? _guideSearch ?? _settingsSearch : _settingsSearch;
                    search.Focus(); search.SelectAll();
                    e.Handled = e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape && _settingsSearch.TextLength > 0)
                {
                    _settingsSearch.Clear();
                    e.Handled = e.SuppressKeyPress = true;
                }
            };
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(860, 550);
            var available = Screen.FromControl(this).WorkingArea;
            ClientSize = new Size(Math.Min(1040, available.Width - 40), Math.Min(640, available.Height - 80));
            filterSettings();
            tabControl1.SelectedTab = (_categories.FirstOrDefault(c => c.Key == Properties.Settings.Default.LastSettingsCategory) ?? _categories[0]).Page;
            syncNavigation();
        }

        private void add(string key, string title, string keywords, TabPage page, bool preview)
        {
            page.Name = "settings-" + key;
            page.Text = title;
            tabControl1.TabPages.Add(page);
            _categories.Add(new SettingsCategory(key, title, keywords, page, preview));
        }

        private Control createSquaresetTestingCard()
        {
            var card = new GroupBox { Text = "Test your JSON squareset", Size = new Size(520, 185) };
            card.Controls.Add(new Label
            {
                Text = "Generate boards on this computer using a local JSON file. No server requests, uploads, or lobby changes.\n\nReview objective frequencies, category/region limits, center rules, and failures. Preview a board or export results as CSV.",
                Location = new Point(14, 25), Size = new Size(490, 98)
            });
            var open = new Button { Text = "Test squareset randomization…", Name = "openSquaresetTester", Location = new Point(14, 136), Size = new Size(250, 30), UseVisualStyleBackColor = true };
            open.Click += (_, _) =>
            {
                using var tester = new BoardDistributionForm(Settings.GameSettingsHelper.ReadFromSettings(Properties.Settings.Default));
                tester.ShowDialog(this);
            };
            card.Controls.Add(open);
            return card;
        }

        private static TabPage cards(string title, params Control[] controls)
        {
            var page = new TabPage { Padding = new Padding(8) };
            var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown };
            foreach (var control in controls)
            {
                control.Dock = DockStyle.None;
                control.Margin = new Padding(6);
                rows.Controls.Add(control);
            }
            page.Controls.Add(rows);
            page.Controls.Add(new Label { Dock = DockStyle.Top, Height = 40, Text = title, Padding = new Padding(6, 8, 0, 0),
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12, FontStyle.Bold) });
            rows.BringToFront();
            return page;
        }

        private static IEnumerable<string> settingText(Control control)
        {
            yield return control.Text;
            yield return control.AccessibleName ?? "";
            foreach (Control child in control.Controls)
                foreach (var text in settingText(child)) yield return text;
        }

        private void filterSettings()
        {
            var terms = _settingsSearch.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var selected = tabControl1.SelectedTab;
            var matches = _categories.Where(c =>
            {
                var text = c.Title + " " + c.Keywords + " " + string.Join(" ", settingText(c.Page));
                return terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
            }).ToArray();
            _updatingNavigation = true;
            _settingsNavigation.BeginUpdate();
            _settingsNavigation.Items.Clear();
            _settingsNavigation.Items.AddRange(matches);
            _settingsNavigation.EndUpdate();
            _updatingNavigation = false;
            var hasResults = matches.Length > 0;
            tabControl1.Visible = hasResults;
            _noSettingsResults.Visible = !hasResults;
            if (hasResults)
            {
                var next = matches.FirstOrDefault(c => c.Page == selected) ?? matches[0];
                _settingsNavigation.SelectedItem = next;
                tabControl1.SelectedTab = next.Page;
            }
            updatePreviewVisibility();
        }

        private void syncNavigation()
        {
            if (_settingsNavigation == null) return;
            var category = _categories.FirstOrDefault(c => c.Page == tabControl1.SelectedTab);
            if (category != null && _settingsNavigation.Items.Contains(category))
                _settingsNavigation.SelectedItem = category;
            updatePreviewVisibility();
        }

        private void updatePreviewVisibility()
        {
            if (_previewPane == null || _settingsSearch == null) return;
            _previewPane.Visible = _settingsNavigation.Items.Count > 0 && ClientSize.Width >= 1000 &&
                _categories.FirstOrDefault(c => c.Page == tabControl1.SelectedTab)?.Preview == true;
        }
    }
}
