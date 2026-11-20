using EldenBingo.Settings;

namespace EldenBingo.UI;

public partial class SettingsDialog
{
    private List<KeywordColorProfile> _keywordProfiles = new();
    private ComboBox _keywordProfileList = null!;
    private TextBox _keywordProfileName = null!;
    private Label _keywordProfileFeedback = null!;
    private Button _updateKeywordProfile = null!, _deleteKeywordProfile = null!, _exportKeywordProfile = null!;

    private GroupBox createKeywordProfilesCard()
    {
        _keywordProfiles = KeywordColorProfile.ReadSaved(Properties.Settings.Default.SavedKeywordProfilesJson);
        var card = new GroupBox { Text = "Keyword rules and saved profiles", Width = 460, MinimumSize = new Size(460, 0), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var rows = new FlowLayoutPanel { Dock = DockStyle.Top, MinimumSize = new Size(440, 0), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8) };
            rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(435, 0), Text = "Save ordered keyword rules, their colors, and intensity. Switch quickly from Quick settings during a match." });
        _keywordProfileList = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Keyword color profile" };
        _deleteKeywordProfile = new Button { Text = "Delete selected", AutoSize = true, Enabled = false };
        _deleteKeywordProfile.Click += (_, _) => deleteKeywordProfile();
        rows.Controls.Add(makeRow(_keywordProfileList, _deleteKeywordProfile));
        _keywordProfileName = new TextBox { Width = 205, MaxLength = 60, PlaceholderText = "e.g. Bingo game highlights", AccessibleName = "Keyword profile name" };
        var save = new Button { Text = "Save as new", AutoSize = true };
        save.Click += (_, _) => saveKeywordProfile(false);
        _updateKeywordProfile = new Button { Text = "Update selected", AutoSize = true, Enabled = false };
        _updateKeywordProfile.Click += (_, _) => saveKeywordProfile(true);
        rows.Controls.Add(makeRow(_keywordProfileName, save, _updateKeywordProfile));
        var edit = new Button { Text = "Edit current rules…", AutoSize = true };
        edit.Click += _keywordColorsButton_Click;
        var import = new Button { Text = "Import profiles…", AutoSize = true };
        import.Click += (_, _) => { importProfileFile(); _keywordProfileFeedback.Text = _colorProfileFeedback.Text; };
        _exportKeywordProfile = new Button { Text = "Export selected…", AutoSize = true, Enabled = false };
        _exportKeywordProfile.Click += (_, _) => { exportProfileFile(true, true); _keywordProfileFeedback.Text = _colorProfileFeedback.Text; };
        rows.Controls.Add(makeRow(edit, import, _exportKeywordProfile));
        _keywordColorAlphaTrackBar.Width = 420;
        rows.Controls.Add(_numKeywordsLabel);
        rows.Controls.Add(_keywordColorAlphaLabel);
        rows.Controls.Add(_keywordColorAlphaTrackBar);
        _keywordProfileFeedback = new Label { AutoSize = true, MaximumSize = new Size(435, 0), Text = "Save settings keeps profile edits and applies colors. Cancel discards changes. An empty rule set can be saved to turn keyword highlights off." };
        rows.Controls.Add(_keywordProfileFeedback);
        _keywordProfileList.SelectedIndexChanged += (_, _) =>
        {
            var selected = _keywordProfileList.SelectedItem as KeywordColorProfile;
            _updateKeywordProfile.Enabled = _deleteKeywordProfile.Enabled = _exportKeywordProfile.Enabled = selected != null;
            if (selected == null) return;
            _keywordColors = selected.Rules.Select(r => new KeywordColor(r.Keyword, Color.FromArgb(r.Color))).ToList();
            _keywordColorAlphaTrackBar.Value = selected.Intensity;
            _keywordProfileName.Text = selected.Name;
            updateKeywordColorText();
            updateLayoutPreview();
        };
        refreshKeywordProfileList();
        card.Controls.Add(rows);
        return card;
    }

    internal void SelectKeywordProfiles()
    {
        _settingsSearch.Clear();
        tabControl1.SelectedTab = tabControl1.TabPages["settings-text-effects"];
        tabControl1.SelectedTab!.Controls.OfType<TabControl>().Single().SelectedIndex = 1;
        _keywordProfileList.Focus();
    }

    private void refreshKeywordProfileList(KeywordColorProfile? selected = null)
    {
        _keywordProfileList.Items.Clear();
        _keywordProfileList.Items.Add("Current keyword colors");
        _keywordProfileList.Items.AddRange(_keywordProfiles.ToArray());
        if (selected == null) _keywordProfileList.SelectedIndex = 0;
        else _keywordProfileList.SelectedItem = selected;
    }

    private void saveKeywordProfile(bool update)
    {
        var name = _keywordProfileName.Text.Trim();
        var selected = update ? _keywordProfileList.SelectedItem as KeywordColorProfile : null;
        var profile = new KeywordColorProfile { Name = name, Intensity = _keywordColorAlphaTrackBar.Value,
            Rules = _keywordColors.Select(c => new LayoutKeywordColor { Keyword = c.Keyword, Color = c.Color.ToArgb() }).ToList() };
        if (!profile.IsValid || update && selected == null)
        { _keywordProfileFeedback.Text = "Enter a name (1–60 characters) and valid keyword rules (up to 500)."; return; }
        if (_keywordProfiles.Any(p => p != selected && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        { _keywordProfileFeedback.Text = "That name is already used. Select it and choose Update selected, or use a different name."; return; }
        if (selected == null) _keywordProfiles.Add(profile);
        else _keywordProfiles[_keywordProfiles.IndexOf(selected)] = profile;
        refreshKeywordProfileList(profile);
        _keywordProfileFeedback.Text = $"'{name}' staged. Click Save settings to keep it.";
    }

    private void deleteKeywordProfile()
    {
        if (_keywordProfileList.SelectedItem is not KeywordColorProfile selected) return;
        _keywordProfiles.Remove(selected);
        refreshKeywordProfileList();
        _keywordProfileName.Clear();
        _keywordProfileFeedback.Text = "Profile removed; current rules kept. Save settings to keep this change.";
    }
}
