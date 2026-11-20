using EldenBingo.Settings;
using System.Text.Json;

namespace EldenBingo
{
    public partial class MainForm
    {
        private void initializeColorProfileMenu()
        {
            _settingsButton.ToolTipText = "Open Settings";
            _quickSettingsButton.ToolTipText = "Switch color profiles, test squaresets, or open guides";
            _quickSettingsButton.DropDownOpening += (_, _) =>
            {
                foreach (var item in _quickSettingsButton.DropDownItems.Cast<ToolStripItem>().ToArray()) item.Dispose();
                _quickSettingsButton.DropDownItems.Clear();
                _quickSettingsButton.DropDownItems.Add(new ToolStripMenuItem("Color profiles") { Enabled = false });
                var profiles = ColorProfile.ReadSaved(Properties.Settings.Default.SavedColorProfilesJson);
                if (profiles.Count == 0)
                    _quickSettingsButton.DropDownItems.Add(new ToolStripMenuItem("No saved color profiles") { Enabled = false });
                foreach (var profile in profiles)
                {
                    var item = new ToolStripMenuItem(profile.Name) { Checked = profile.Matches(BoardAppearanceSettings.Current) };
                    item.Click += (_, _) =>
                    {
                        var appearance = BoardAppearanceSettings.Current.Clone();
                        profile.ApplyTo(appearance);
                        Properties.Settings.Default.BoardAppearanceJson = JsonSerializer.Serialize(appearance);
                        Properties.Settings.Default.Save();
                    };
                    _quickSettingsButton.DropDownItems.Add(item);
                }
                _quickSettingsButton.DropDownItems.Add(new ToolStripSeparator());
                _quickSettingsButton.DropDownItems.Add("Manage color profiles…", null, (_, _) =>
                {
                    using var dialog = new UI.SettingsDialog();
                    dialog.TopMost = Properties.Settings.Default.AlwaysOnTop;
                    dialog.SelectColorProfiles();
                    if (dialog.ShowDialog(this) == DialogResult.OK) applyLayout(dialog.AppliedLayout);
                });
                _quickSettingsButton.DropDownItems.Add(new ToolStripSeparator());
                _quickSettingsButton.DropDownItems.Add(new ToolStripMenuItem("Keyword color profiles") { Enabled = false });
                var keywords = KeywordColorProfile.ReadSaved(Properties.Settings.Default.SavedKeywordProfilesJson);
                if (keywords.Count == 0) _quickSettingsButton.DropDownItems.Add(new ToolStripMenuItem("No saved keyword profiles") { Enabled = false });
                foreach (var profile in keywords)
                {
                    var item = new ToolStripMenuItem(profile.Name) { Checked = profile.Matches(
                        KeywordColorsJsonHelper.Colors.Select(c => new LayoutKeywordColor { Keyword = c.Keyword, Color = c.Color.ToArgb() }), Properties.Settings.Default.KeywordColorsAlpha) };
                    item.Click += (_, _) =>
                    {
                        KeywordColorsJsonHelper.Colors = profile.Rules.Select(r => new KeywordColor(r.Keyword, Color.FromArgb(r.Color))).ToArray();
                        Properties.Settings.Default.KeywordColorsAlpha = profile.Intensity;
                        Properties.Settings.Default.Save();
                    };
                    _quickSettingsButton.DropDownItems.Add(item);
                }
                _quickSettingsButton.DropDownItems.Add("Manage keyword profiles…", null, (_, _) =>
                {
                    using var dialog = new UI.SettingsDialog { TopMost = Properties.Settings.Default.AlwaysOnTop };
                    dialog.SelectKeywordProfiles();
                    if (dialog.ShowDialog(this) == DialogResult.OK) applyLayout(dialog.AppliedLayout);
                });
                _quickSettingsButton.DropDownItems.Add(new ToolStripSeparator());
                _quickSettingsButton.DropDownItems.Add("Test squareset randomization…", null, (_, _) =>
                {
                    using var tester = new UI.BoardDistributionForm(GameSettingsHelper.ReadFromSettings(Properties.Settings.Default));
                    tester.ShowDialog(this);
                });
                _quickSettingsButton.DropDownItems.Add("Help and guides…", null, (_, _) =>
                {
                    using var dialog = new UI.SettingsDialog { TopMost = Properties.Settings.Default.AlwaysOnTop };
                    dialog.SelectHelpGuides();
                    if (dialog.ShowDialog(this) == DialogResult.OK) applyLayout(dialog.AppliedLayout);
                });
            };
        }
    }
}
