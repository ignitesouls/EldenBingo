using EldenBingo.Settings;
using System.Text.Json;

namespace EldenBingo.UI
{
    public partial class SettingsDialog
    {
        private List<LayoutProfile> _savedLayouts = new();
        private ComboBox _layoutList = null!;
        private TextBox _layoutName = null!;
        private Label _layoutFeedback = null!;
        private LayoutPreviewControl _layoutPreview = null!;
        private Label _previewInfo = null!;
        private Label _fontSample = null!;
        private NumericUpDown _layoutWidth = null!, _layoutHeight = null!, _sidebarWidth = null!;
        private LayoutProfile _geometry = new();
        private bool _loadingLayout;
        private Panel _previewPane = null!;
        internal LayoutProfile AppliedLayout => captureLayout("Current settings");

        private void initializeLayouts()
        {
            _savedLayouts = LayoutProfile.ReadSaved(Properties.Settings.Default.SavedLayoutsJson);
            var main = MainForm.Instance;
            _geometry.MainWidth = Math.Max(952, main?.RestoreBounds.Width ?? 1100);
            _geometry.MainHeight = Math.Max(566, main?.RestoreBounds.Height ?? 800);
            _geometry.MainX = main?.RestoreBounds.X ?? -9999;
            _geometry.MainY = main?.RestoreBounds.Y ?? -9999;
            main?.CaptureLayoutGeometry(_geometry);
            if (_geometry.PopoutCustom)
            {
                _popoutBoardCustomSizeRadioButton.Checked = true;
                _popoutBoardSizeXTextBox.Text = _geometry.PopoutWidth.ToString();
                _popoutBoardSizeYTextBox.Text = _geometry.PopoutHeight.ToString();
            }

            var page = new TabPage("Layouts") { Padding = new Padding(10), AutoScroll = true };
            var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(460, 0), Text = "Create your own layouts from your current appearance and window arrangement. No built-in layouts are included. Layouts change presentation only." });
            _layoutList = new ComboBox { Width = 285, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Saved layout" };
            rows.Controls.Add(makeRow(new Label { Text = "Saved layout:", AutoSize = true }, _layoutList));
            _layoutList.SelectedIndexChanged += (_, _) =>
            {
                if (_layoutList.SelectedItem is LayoutProfile p) loadLayout(p);
            };
            _layoutWidth = dimensionInput(952, 10000, _geometry.MainWidth, "Main window width");
            _layoutHeight = dimensionInput(566, 10000, _geometry.MainHeight, "Main window height");
            _sidebarWidth = dimensionInput(80, 800, main?.LayoutSidebarWidth ?? Properties.Settings.Default.LobbySidebarWidth, "Player sidebar width");
            rows.Controls.Add(makeRow(new Label { Text = "Main window (px):", AutoSize = true }, _layoutWidth, new Label { Text = "×", AutoSize = true }, _layoutHeight));
            rows.Controls.Add(makeRow(new Label { Text = "Player sidebar (px):", AutoSize = true }, _sidebarWidth));
            rows.Controls.Add(new Label { Text = "Save your current appearance and window arrangement:", AutoSize = true, Margin = new Padding(3, 15, 3, 3) });
            _layoutName = new TextBox { Width = 240, MaxLength = 60, PlaceholderText = "e.g. Friday stream", AccessibleName = "Saved layout name" };
            var save = new Button { Text = "Save named layout", AutoSize = true };
            save.Click += (_, _) => saveNamedLayout();
            rows.Controls.Add(makeRow(_layoutName, save));
            var delete = new Button { Text = "Delete selected saved layout", AutoSize = true, Enabled = false };
            _layoutList.SelectedIndexChanged += (_, _) => delete.Enabled = _layoutList.SelectedItem is LayoutProfile p && _savedLayouts.Contains(p);
            delete.Click += (_, _) =>
            {
                if (_layoutList.SelectedItem is LayoutProfile p && _savedLayouts.Remove(p))
                {
                    refreshLayoutList();
                    _layoutFeedback.Text = "Layout removed. Save settings to keep this change.";
                }
            };
            rows.Controls.Add(delete);
            _layoutFeedback = new Label { AutoSize = true, MaximumSize = new Size(460, 0), Text = "Changes, including named layouts, are kept when you click Save settings. Cancel discards them." };
            rows.Controls.Add(_layoutFeedback);
            page.Controls.Add(rows);
            tabControl1.TabPages.Add(page);

            var previewPane = _previewPane = new Panel { Dock = DockStyle.Right, Width = 330, Padding = new Padding(12) };
            var previewRows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            previewRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            previewRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            previewRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewRows.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            previewRows.Controls.Add(new Label { Text = "Live layout preview", AutoSize = true });
            _layoutPreview = new LayoutPreviewControl { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 8) };
            previewRows.Controls.Add(_layoutPreview);
            _previewInfo = new Label { Dock = DockStyle.Fill, AutoSize = true };
            previewRows.Controls.Add(_previewInfo);
            previewRows.Controls.Add(new Label { Text = "Text sample at chosen font size:", AutoSize = true });
            _fontSample = new Label { Text = "Collect 5 Golden Seeds", AutoSize = false, Height = 65, Dock = DockStyle.Fill };
            previewRows.Controls.Add(_fontSample);
            previewPane.Controls.Add(previewRows);
            // Only widen the content area. Keep the existing tabs and button row intact.
            var oldWidth = ClientSize.Width;
            ClientSize = new Size(oldWidth + previewPane.Width, ClientSize.Height);
            var body = new Panel { Dock = DockStyle.Fill };
            Controls.Remove(tabControl1);
            body.Controls.Add(tabControl1);
            body.Controls.Add(previewPane);
            Controls.Add(body);
            body.BringToFront();
            _okButton.Text = "Save settings";
            _okButton.AutoSize = true;
            _okButton.Left -= 25;

            foreach (var control in new Control[] { _colorPanel, _fontLinkLabel })
            {
                control.BackColorChanged += (_, _) => updateLayoutPreview();
                control.FontChanged += (_, _) => updateLayoutPreview();
            }
            foreach (var input in new TextBox[] { _bingoMaxXTextBox, _bingoMaxYTextBox, _popoutBoardSizeXTextBox, _popoutBoardSizeYTextBox,
                _mapSizeCustomXTextBox, _mapSizeCustomYTextBox, _mapPositionXTextBox, _mapPositionYTextBox })
                input.TextChanged += (_, _) => updateLayoutPreview();
            foreach (var input in new RadioButton[] { _bingoCustomMaxSizeRadioButton, _popoutBoardCustomSizeRadioButton, _mapSizeCustomRadioButton, _mapPositionCustomRadioButton })
                input.CheckedChanged += (_, _) => updateLayoutPreview();
            foreach (var input in new CheckBox[] { _alwaysOnTopCheckbox, _showClassesCheckBox, _highlightMarkedCheckBox, _highlightBingoCheckBox })
                input.CheckedChanged += (_, _) => updateLayoutPreview();
            _shadowTrackBar.ValueChanged += (_, _) => updateLayoutPreview();
            _keywordColorAlphaTrackBar.ValueChanged += (_, _) => updateLayoutPreview();
            _layoutWidth.ValueChanged += (_, _) => updateLayoutPreview();
            _layoutHeight.ValueChanged += (_, _) => updateLayoutPreview();
            _sidebarWidth.ValueChanged += (_, _) => updateLayoutPreview();
            refreshLayoutList();
            updateLayoutPreview();
        }

        private static FlowLayoutPanel makeRow(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 5, 0, 0) };
            row.Controls.AddRange(controls);
            return row;
        }

        private static NumericUpDown dimensionInput(int min, int max, int value, string name) => new()
        {
            Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Width = 80, AccessibleName = name
        };

        private void refreshLayoutList()
        {
            _layoutList.Items.Clear();
            _layoutList.Items.Add("None (current settings)");
            _layoutList.Items.AddRange(_savedLayouts.ToArray());
            _layoutList.SelectedIndex = 0;
        }

        private LayoutProfile captureLayout(string name)
        {
            int number(TextBox input) => int.TryParse(input.Text, out var n) ? n : 0;
            return new LayoutProfile
            {
                Name = name, FontName = _fontLinkLabel.Font.Name, FontSize = _fontLinkLabel.Font.Size, FontStyle = _fontLinkLabel.Font.Style,
                Background = _colorPanel.BackColor.ToArgb(), MainWidth = (int)_layoutWidth.Value, MainHeight = (int)_layoutHeight.Value,
                MainX = _geometry.MainX, MainY = _geometry.MainY, SidebarWidth = (int)_sidebarWidth.Value,
                BoardMaximum = _bingoCustomMaxSizeRadioButton.Checked, BoardWidth = number(_bingoMaxXTextBox), BoardHeight = number(_bingoMaxYTextBox),
                PopoutCustom = _popoutBoardCustomSizeRadioButton.Checked, PopoutWidth = number(_popoutBoardSizeXTextBox), PopoutHeight = number(_popoutBoardSizeYTextBox),
                PopoutX = _geometry.PopoutX, PopoutY = _geometry.PopoutY,
                PopoutOpacity = _geometry.PopoutOpacity, PopoutMarkedStyle = _geometry.PopoutMarkedStyle, PopoutToolbar = _geometry.PopoutToolbar,
                MapCustomSize = _mapSizeCustomRadioButton.Checked, MapWidth = number(_mapSizeCustomXTextBox), MapHeight = number(_mapSizeCustomYTextBox),
                MapCustomPosition = _mapPositionCustomRadioButton.Checked, MapX = number(_mapPositionXTextBox), MapY = number(_mapPositionYTextBox),
                AlwaysOnTop = _alwaysOnTopCheckbox.Checked, ShowClasses = _showClassesCheckBox.Checked, Shadows = _shadowTrackBar.Value * 10,
                MarkHighlight = _highlightMarkedCheckBox.Checked, BingoHighlight = _highlightBingoCheckBox.Checked,
                KeywordColors = _keywordColors.Select(c => new LayoutKeywordColor { Keyword = c.Keyword, Color = c.Color.ToArgb() }).ToList(),
                KeywordColorIntensity = _keywordColorAlphaTrackBar.Value,
                BoardAppearance = _appearance.Clone()
            };
        }

        private void loadLayout(LayoutProfile p)
        {
            Font font;
            try { font = new Font(p.FontName, p.FontSize, p.FontStyle); }
            catch (ArgumentException) { font = new Font("Segoe UI", p.FontSize); }
            _loadingLayout = true;
            _geometry = new LayoutProfile
            {
                MainX = p.MainX == -9999 ? _geometry.MainX : p.MainX,
                MainY = p.MainY == -9999 ? _geometry.MainY : p.MainY,
                PopoutX = p.PopoutX == -9999 ? _geometry.PopoutX : p.PopoutX,
                PopoutY = p.PopoutY == -9999 ? _geometry.PopoutY : p.PopoutY,
                PopoutOpacity = p.PopoutOpacity, PopoutMarkedStyle = p.PopoutMarkedStyle, PopoutToolbar = p.PopoutToolbar
            };
            _fontLinkLabel.Font = font;
            _fontLinkLabel.Text = font.Name;
            _colorPanel.BackColor = Color.FromArgb(p.Background);
            _layoutWidth.Value = p.MainWidth;
            _layoutHeight.Value = p.MainHeight;
            _sidebarWidth.Value = p.SidebarWidth;
            _bingoCustomMaxSizeRadioButton.Checked = p.BoardMaximum;
            _bingoNoMaxSizeRadioButton.Checked = !p.BoardMaximum;
            _bingoMaxXTextBox.Text = p.BoardWidth.ToString();
            _bingoMaxYTextBox.Text = p.BoardHeight.ToString();
            _popoutBoardCustomSizeRadioButton.Checked = p.PopoutCustom;
            _popoutBoardDefaultSizeRadioButton.Checked = !p.PopoutCustom;
            _popoutBoardSizeXTextBox.Text = p.PopoutWidth.ToString();
            _popoutBoardSizeYTextBox.Text = p.PopoutHeight.ToString();
            _mapSizeCustomRadioButton.Checked = p.MapCustomSize;
            _mapSizeRememberLastRadioButton.Checked = !p.MapCustomSize;
            _mapSizeCustomXTextBox.Text = p.MapWidth.ToString();
            _mapSizeCustomYTextBox.Text = p.MapHeight.ToString();
            _mapPositionCustomRadioButton.Checked = p.MapCustomPosition;
            _mapPositionRelativeRadioButton.Checked = !p.MapCustomPosition;
            _mapPositionXTextBox.Text = p.MapX.ToString();
            _mapPositionYTextBox.Text = p.MapY.ToString();
            _alwaysOnTopCheckbox.Checked = p.AlwaysOnTop;
            _showClassesCheckBox.Checked = p.ShowClasses;
            _shadowTrackBar.Value = p.Shadows / 10;
            _highlightMarkedCheckBox.Checked = p.MarkHighlight;
            _highlightBingoCheckBox.Checked = p.BingoHighlight;
            if (p.BoardAppearance != null)
            {
                _appearance = p.BoardAppearance.Clone();
                refreshAppearanceControls();
            }
            if (p.KeywordColors != null)
            {
                _keywordColors = p.KeywordColors.Select(c => new KeywordColor(c.Keyword, Color.FromArgb(c.Color))).ToList();
                _keywordColorAlphaTrackBar.Value = p.KeywordColorIntensity;
                updateKeywordColorText();
            }
            _layoutName.Text = _savedLayouts.Contains(p) ? p.Name : "";
            _layoutFeedback.Text = $"{p.Name} loaded for preview. Save settings to apply.";
            _loadingLayout = false;
            updateLayoutPreview();
        }

        private void saveNamedLayout()
        {
            var name = _layoutName.Text.Trim();
            var profile = captureLayout(name);
            if (!profile.IsValid)
            {
                _layoutFeedback.Text = "Enter a name and valid sizes (100–10000 px) and font size (6–72 pt).";
                return;
            }
            var previous = _savedLayouts.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (previous != null && MessageBox.Show(this, $"Replace saved layout '{name}'?", "Save named layout", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            _savedLayouts.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            _savedLayouts.Add(profile);
            refreshLayoutList();
            _layoutList.SelectedItem = profile;
            _layoutFeedback.Text = $"'{name}' saved in this dialog. Click Save settings to keep it.";
        }

        private void updateLayoutPreview()
        {
            if (_loadingLayout || _layoutPreview == null) return;
            var p = captureLayout("Preview");
            _layoutPreview.Profile = p;
            _layoutPreview.Invalidate();
            _fontSample.Font = _fontLinkLabel.Font;
            _fontSample.BackColor = _colorPanel.BackColor;
            _fontSample.ForeColor = Color.FromArgb(_appearance.TextColor);
            _previewInfo.Text = $"Window: {p.MainWidth} × {p.MainHeight} px\nBoard limit: {(p.BoardMaximum ? $"{p.BoardWidth} × {p.BoardHeight} px" : "Fit window")}\nPop-out: {(p.PopoutCustom ? $"{p.PopoutWidth} × {p.PopoutHeight} px" : "Default")}";
        }

        private bool validateLayout()
        {
            if (!AppliedLayout.IsValid)
            {
                MessageBox.Show(this, "Use sizes between 100 and 10000 pixels for enabled custom sizes, and a font size between 6 and 72 points.", "Check layout settings");
                return false;
            }
            return true;
        }

        private void saveLayouts()
        {
            Properties.Settings.Default.LastSettingsCategory = _categories.FirstOrDefault(c => c.Page == tabControl1.SelectedTab)?.Key ?? "layouts";
            Properties.Settings.Default.BoardAppearanceJson = JsonSerializer.Serialize(_appearance);
            Properties.Settings.Default.SavedLayoutsJson = JsonSerializer.Serialize(_savedLayouts);
            Properties.Settings.Default.SavedColorProfilesJson = JsonSerializer.Serialize(_colorProfiles);
            Properties.Settings.Default.SavedKeywordProfilesJson = JsonSerializer.Serialize(_keywordProfiles);
            Properties.Settings.Default.LobbySidebarWidth = (int)_sidebarWidth.Value;
            Properties.Settings.Default.PopoutLayoutJson = JsonSerializer.Serialize(AppliedLayout);
        }
    }
}
