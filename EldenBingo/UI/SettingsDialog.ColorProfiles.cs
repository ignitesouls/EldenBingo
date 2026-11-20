using EldenBingo.Settings;

namespace EldenBingo.UI
{
    public partial class SettingsDialog
    {
        internal void SelectColorProfiles()
        {
            _settingsSearch.Clear();
            tabControl1.SelectedTab = tabControl1.TabPages["settings-board-style"];
            _boardStyleTabs.SelectedIndex = 2;
            _colorProfileList.Focus();
        }

        private List<ColorProfile> _colorProfiles = new();
        private ComboBox _colorProfileList = null!;
        private TextBox _colorProfileName = null!;
        private Label _colorProfileFeedback = null!;
        private Button _updateColorProfileButton = null!, _deleteColorProfileButton = null!;

        private void initializeColorProfiles(FlowLayoutPanel rows)
        {
            _colorProfiles = ColorProfile.ReadSaved(Properties.Settings.Default.SavedColorProfilesJson);
            rows.Controls.Add(new Label { Text = "Saved color profiles", AutoSize = true, Margin = new Padding(3, 12, 3, 3) });
            _colorProfileList = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Color profile" };
            _deleteColorProfileButton = new Button { Text = "Delete selected", AutoSize = true, Enabled = false };
            _deleteColorProfileButton.Click += (_, _) => deleteColorProfile();
            rows.Controls.Add(makeRow(_colorProfileList, _deleteColorProfileButton));
            _colorProfileName = new TextBox { Width = 205, MaxLength = 60, PlaceholderText = "e.g. Tournament colors", AccessibleName = "Color profile name" };
            var save = new Button { Text = "Save as new", AutoSize = true };
            save.Click += (_, _) => saveColorProfile(false);
            _updateColorProfileButton = new Button { Text = "Update selected", AutoSize = true, Enabled = false };
            _updateColorProfileButton.Click += (_, _) => saveColorProfile(true);
            rows.Controls.Add(makeRow(_colorProfileName, save, _updateColorProfileButton));
            _colorProfileFeedback = new Label { AutoSize = true, MaximumSize = new Size(455, 0), Text = "Profiles save team, square, text, and border colors plus automatic contrast. Save settings to keep changes; Cancel discards them." };
            rows.Controls.Add(_colorProfileFeedback);
            addProfileFileButtons(rows);
            _colorProfileList.SelectedIndexChanged += (_, _) =>
            {
                var selected = _colorProfileList.SelectedItem as ColorProfile;
                _deleteColorProfileButton.Enabled = _updateColorProfileButton.Enabled = selected != null;
                if (selected == null) return;
                selected.ApplyTo(_appearance);
                _colorProfileName.Text = selected.Name;
                refreshAppearanceControls();
            };
            refreshColorProfileList();
        }

        private void refreshColorProfileList(ColorProfile? selected = null)
        {
            _colorProfileList.Items.Clear();
            _colorProfileList.Items.Add("Current colors");
            _colorProfileList.Items.AddRange(_colorProfiles.ToArray());
            if (selected != null) _colorProfileList.SelectedItem = selected;
            else _colorProfileList.SelectedIndex = 0;
        }

        private void saveColorProfile(bool update)
        {
            var name = _colorProfileName.Text.Trim();
            var selected = update ? _colorProfileList.SelectedItem as ColorProfile : null;
            if (name.Length == 0 || name.Length > 60 || update && selected == null)
            {
                _colorProfileFeedback.Text = "Enter a profile name (1–60 characters).";
                return;
            }
            if (_colorProfiles.Any(p => p != selected && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _colorProfileFeedback.Text = "That name is already used. Select it and choose Update selected, or use a different name.";
                return;
            }
            var profile = ColorProfile.Capture(name, _appearance);
            if (selected != null) _colorProfiles[_colorProfiles.IndexOf(selected)] = profile;
            else _colorProfiles.Add(profile);
            refreshColorProfileList(profile);
            _colorProfileFeedback.Text = $"'{name}' staged. Click Save settings to keep it.";
        }

        private void deleteColorProfile()
        {
            if (_colorProfileList.SelectedItem is not ColorProfile selected) return;
            _colorProfiles.Remove(selected);
            refreshColorProfileList();
            _colorProfileName.Clear();
            _colorProfileFeedback.Text = "Profile removed; current colors kept. Save settings to keep this change.";
        }
    }
}
