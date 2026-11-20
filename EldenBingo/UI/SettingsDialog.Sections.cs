namespace EldenBingo.UI;

public partial class SettingsDialog
{
    private static GroupBox settingsSection(string title, params Control[] controls)
    {
        var group = new GroupBox { Text = title, Width = 460, MinimumSize = new Size(460, 0), AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10), Margin = new Padding(6) };
        var rows = new FlowLayoutPanel { Dock = DockStyle.Top, MinimumSize = new Size(430, 0), AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        foreach (var control in controls)
        {
            control.Dock = DockStyle.None;
            control.Margin = new Padding(0, 4, 0, 6);
            if (control is Label label) { label.AutoSize = true; label.MaximumSize = new Size(430, 0); }
            rows.Controls.Add(control);
        }
        group.Controls.Add(rows);
        return group;
    }

    private static TabPage settingsTabs(string title, params (string Title, Control[] Controls)[] sections)
    {
        var page = new TabPage { Padding = new Padding(8) };
        var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = title + " sections" };
        foreach (var section in sections)
        {
            var tab = new TabPage(section.Title) { Padding = new Padding(6) };
            var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.TopDown, WrapContents = false };
            foreach (var control in section.Controls)
            {
                control.Dock = DockStyle.None; control.Margin = new Padding(6);
                rows.Controls.Add(control);
            }
            tab.Controls.Add(rows); tabs.TabPages.Add(tab);
        }
        page.Controls.Add(tabs);
        page.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 40,
            Padding = new Padding(6, 8, 0, 0), Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12, FontStyle.Bold) });
        tabs.BringToFront();
        return page;
    }

    private TabPage createTextEffectsPage()
    {
        var font = settingsSection("Board font", new Label { Text = "Choose the font used by the board and its text." }, _fontLinkLabel);
        _shadowTrackBar.Width = 430;
        var effects = settingsSection("Shadows and claim highlights", _shadowLabel, _shadowTrackBar,
            _highlightMarkedCheckBox, _highlightBingoCheckBox);
        return settingsTabs("Text and effects", ("Font & highlights", new Control[] { font, effects }),
            ("Keyword colors", new Control[] { createKeywordProfilesCard() }));
    }

    private TabPage createAudioPage()
    {
        _volumeTrackBar.Width = 430;
        _soundOutputDeviceComboBox.Width = 390;
        var playback = settingsSection("Playback and output", _soundCheckBox, _volumeLabel, _volumeTrackBar,
            new Label { Text = "Output device" }, _soundOutputDeviceComboBox, _testSoundButton);
        return settingsTabs("Audio", ("Playback", new Control[] { playback }),
            ("Event sounds", new Control[] { createEventSoundsCard() }));
    }
}
