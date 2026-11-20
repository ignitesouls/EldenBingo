using EldenBingo.Settings;
using EldenBingoCommon;
using System.Text.Json;

namespace EldenBingo.UI;

internal sealed class MatchPresetsForm : Form
{
    private readonly List<MatchSettingsPreset> _presets;
    private readonly BingoGameSettings _current;
    private readonly ComboBox _list = new() { Width = 395, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Match preset" };
    private readonly TextBox _name = new() { Width = 220, MaxLength = 60, PlaceholderText = "Preset name", AccessibleName = "Match preset name" };
    private readonly Label _feedback = new() { AutoSize = true, MaximumSize = new Size(410, 0) };
    public BingoGameSettings? SelectedSettings { get; private set; }

    public MatchPresetsForm(BingoGameSettings current)
    {
        _current = MatchSettingsPreset.Copy(current);
        _presets = MatchSettingsPreset.ReadSaved(Properties.Settings.Default.MatchPresetsJson);
        Text = "Match settings presets"; ClientSize = new Size(450, 385); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
        rows.Controls.Add(new Label { Text = "Current settings to save:", AutoSize = true });
        rows.Controls.Add(new Label { Text = MatchSettingsPreset.Describe(current), AutoSize = true, MaximumSize = new Size(410, 0) });
        rows.Controls.Add(_list);
        var summary = new Label { AutoSize = true, MaximumSize = new Size(410, 0), MinimumSize = new Size(400, 56) };
        rows.Controls.Add(summary);
        var save = new Button { Text = "Save as new", AutoSize = true };
        save.Click += (_, _) => savePreset(false);
        var editRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        editRow.Controls.AddRange(new Control[] { _name, save }); rows.Controls.Add(editRow);
        var update = new Button { Text = "Update selected", AutoSize = true };
        var delete = new Button { Text = "Delete selected", AutoSize = true };
        update.Click += (_, _) => savePreset(true);
        delete.Click += (_, _) => { if (_list.SelectedItem is MatchSettingsPreset p) { _presets.Remove(p); refresh(); _feedback.Text = "Preset removed. Save presets to keep the change."; } };
        var manage = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        manage.Controls.AddRange(new Control[] { update, delete }); rows.Controls.Add(manage);
        rows.Controls.Add(_feedback);
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(3, 12, 3, 3) };
        var use = new Button { Text = "Use preset", AutoSize = true };
        var keep = new Button { Text = "Save presets", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        use.Click += (_, _) => { if (_list.SelectedItem is MatchSettingsPreset p) { SelectedSettings = MatchSettingsPreset.Copy(p.Settings); commit(); } };
        keep.Click += (_, _) => commit();
        actions.Controls.AddRange(new Control[] { use, keep, cancel }); rows.Controls.Add(actions);
        _list.SelectedIndexChanged += (_, _) =>
        {
            var selected = _list.SelectedItem as MatchSettingsPreset;
            use.Enabled = update.Enabled = delete.Enabled = selected != null;
            summary.Text = selected == null ? "Choose a saved preset, or name the current settings." : MatchSettingsPreset.Describe(selected.Settings);
            _name.Text = selected?.Name ?? "";
        };
        Controls.Add(rows); CancelButton = cancel; refresh();
        _feedback.Text = "Changes stay local until you save. Applying a preset fills the settings form; confirm that form to change the lobby.";
    }

    private void refresh(MatchSettingsPreset? selected = null)
    {
        _list.Items.Clear(); _list.Items.Add("Select a preset…"); _list.Items.AddRange(_presets.ToArray());
        if (selected == null) _list.SelectedIndex = 0; else _list.SelectedItem = selected;
    }
    private void savePreset(bool update)
    {
        var selected = update ? _list.SelectedItem as MatchSettingsPreset : null;
        var preset = new MatchSettingsPreset { Name = _name.Text.Trim(), Settings = MatchSettingsPreset.Copy(_current) };
        if (!preset.IsValid || update && selected == null) { _feedback.Text = "Enter a name and valid bingo game options. Random classes need enough allowed classes."; return; }
        if (_presets.Any(p => p != selected && p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase))) { _feedback.Text = "That name already exists. Update it or use another name."; return; }
        if (selected == null) _presets.Add(preset); else _presets[_presets.IndexOf(selected)] = preset;
        refresh(preset); _feedback.Text = "Preset staged. Save presets to keep it.";
    }
    private void commit()
    {
        Properties.Settings.Default.MatchPresetsJson = JsonSerializer.Serialize(_presets);
        Properties.Settings.Default.Save(); DialogResult = DialogResult.OK; Close();
    }
}
