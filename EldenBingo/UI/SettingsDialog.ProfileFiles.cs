using EldenBingo.Settings;

namespace EldenBingo.UI;

public partial class SettingsDialog
{
    private void addProfileFileButtons(FlowLayoutPanel rows)
    {
        var import = new Button { Text = "Import profiles…", AutoSize = true };
        var export = new Button { Text = "Export profiles…", AutoSize = true };
        var menu = new ContextMenuStrip();
        Disposed += (_, _) => menu.Dispose();
        var selected = menu.Items.Add("Export selected color profile…", null, (_, _) => exportProfileFile(true));
        menu.Items.Add("Back up all profiles + current layout…", null, (_, _) => exportProfileFile(false));
        import.Click += (_, _) => importProfileFile();
        export.Click += (_, _) =>
        {
            selected.Enabled = _colorProfileList.SelectedItem is ColorProfile;
            menu.Show(export, new Point(0, export.Height));
        };
        rows.Controls.Add(makeRow(import, export));
    }

    private void importProfileFile()
    {
        using var dialog = new OpenFileDialog { Title = "Import color, keyword, and layout profiles", Filter = "Elden Bingo profiles (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > ProfileArchive.MaximumBytes) throw new InvalidDataException("Profile files must be smaller than 1 MB.");
            importProfileArchive(File.ReadAllText(dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { _colorProfileFeedback.Text = "Import failed: " + ex.Message; }
    }

    private void importProfileArchive(string json)
    {
        var archive = ProfileArchive.Parse(json);
        foreach (var profile in archive.ColorProfiles)
        {
            profile.Name = ProfileArchive.AvailableName(profile.Name, _colorProfiles.Select(p => p.Name));
            _colorProfiles.Add(profile);
        }
        foreach (var layout in archive.Layouts)
        {
            layout.Name = ProfileArchive.AvailableName(layout.Name, _savedLayouts.Select(p => p.Name));
            _savedLayouts.Add(layout);
        }
        foreach (var keyword in archive.KeywordProfiles)
        {
            keyword.Name = ProfileArchive.AvailableName(keyword.Name, _keywordProfiles.Select(p => p.Name));
            _keywordProfiles.Add(keyword);
        }
        refreshColorProfileList();
        refreshLayoutList();
        refreshKeywordProfileList();
        _colorProfileFeedback.Text = $"Imported {archive.ColorProfiles.Count} color, {archive.KeywordProfiles.Count} keyword, and {archive.Layouts.Count} layout profiles. Duplicate names keep both copies. Save settings to keep them.";
    }

    private ProfileArchive captureProfileArchive(bool selectedOnly)
    {
        if (selectedOnly)
        {
            if (_colorProfileList.SelectedItem is not ColorProfile selected) throw new InvalidOperationException("Select a saved color profile first.");
            return new ProfileArchive { ColorProfiles = new() { selected } };
        }
        var current = captureLayout(ProfileArchive.AvailableName("Current settings", _savedLayouts.Select(p => p.Name)));
        if (!current.IsValid) throw new InvalidDataException("Check the current layout sizes before backing up profiles.");
        return new ProfileArchive { ColorProfiles = _colorProfiles.ToList(), KeywordProfiles = _keywordProfiles.ToList(), Layouts = _savedLayouts.Append(current).ToList() };
    }

    private void exportProfileFile(bool selectedOnly, bool keywordOnly = false)
    {
        try
        {
            ProfileArchive archive;
            if (keywordOnly)
            {
                if (_keywordProfileList.SelectedItem is not KeywordColorProfile keyword) throw new InvalidDataException("Select a saved keyword profile first.");
                archive = new ProfileArchive { KeywordProfiles = new() { keyword } };
            }
            else archive = captureProfileArchive(selectedOnly);
            var json = archive.Serialize();
            ProfileArchive.Parse(json); // Apply the same bounds as import, so every backup can be restored.
            using var dialog = new SaveFileDialog { Title = "Export profiles", Filter = "Elden Bingo profiles (*.json)|*.json", DefaultExt = "json", AddExtension = true,
                FileName = keywordOnly ? "elden-bingo-keywords.json" : selectedOnly ? "elden-bingo-palette.json" : "elden-bingo-profiles.json" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dialog.FileName, json);
            _colorProfileFeedback.Text = "Profiles exported. Imports are available from this page; imported layouts appear under Layouts.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { _colorProfileFeedback.Text = "Export failed: " + ex.Message; }
    }
}
