using EldenBingo.Settings;
using EldenBingo.Sfx;

namespace EldenBingo.UI;

public partial class SettingsDialog
{
    private EventSoundPreferences _eventSounds = new();
    private GroupBox createEventSoundsCard()
    {
        _eventSounds = EventSoundPreferences.Parse(Properties.Settings.Default.EventSoundsJson);
        var card = new GroupBox { Text = "Per-event sounds", Width = 460, MinimumSize = new Size(460, 0), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var rows = new FlowLayoutPanel { Dock = DockStyle.Top, MinimumSize = new Size(440, 0), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8) };
        rows.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(430, 0), Text = "Enable events individually. Event volume is multiplied by the master volume. Choose WAV or MP3 files. Start, pause, and finish are silent until configured. Custom files stay at their selected paths." });
        foreach (var (kind, label, sound) in new[] {
            (MatchSoundEvent.Claim, "Claims / unclaims", SoundType.SquareClaimedOther), (MatchSoundEvent.Bingo, "Bingo", SoundType.Bingo),
            (MatchSoundEvent.Start, "Match start", SoundType.MatchStart), (MatchSoundEvent.Pause, "Pause / resume", SoundType.MatchPause), (MatchSoundEvent.Finish, "Match finish", SoundType.MatchFinish) })
        {
            var option = _eventSounds.Get(kind);
            var enabled = new CheckBox { Text = label, AutoSize = true, MinimumSize = new Size(172, 0), Checked = option.Enabled, AccessibleName = label + " sound enabled" };
            var volume = new NumericUpDown { Minimum = 0, Maximum = 100, Value = option.Volume, Width = 65, AccessibleName = label + " volume percent" };
            enabled.CheckedChanged += (_, _) => option.Enabled = enabled.Checked;
            volume.ValueChanged += (_, _) => option.Volume = (int)volume.Value;
            var test = new Button { Text = "Test", AutoSize = true };
            var filePath = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, AccessibleName = label + " sound file" };
            var browse = new Button { Text = "Browse...", AutoSize = true, AccessibleName = "Choose " + label + " sound file" };
            var reset = new Button { Text = SoundLibrary.HasBundledSound(sound) ? "Original" : "Clear", AutoSize = true, AccessibleName = "Reset " + label + " sound file" };
            void refresh()
            {
                var custom = !string.IsNullOrWhiteSpace(option.FilePath);
                filePath.Text = custom ? (File.Exists(option.FilePath) ? option.FilePath : "Missing: " + option.FilePath) :
                    SoundLibrary.HasBundledSound(sound) ? "Original application sound" : "None (silent)";
                test.Enabled = custom || SoundLibrary.HasBundledSound(sound);
                reset.Enabled = custom;
            }
            browse.Click += (_, _) =>
            {
                using var dialog = new OpenFileDialog { Title = "Choose " + label + " sound", Filter = "Audio files (*.wav;*.mp3)|*.wav;*.mp3|WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3", CheckFileExists = true };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SoundLibrary.ValidateCustomFile(dialog.FileName);
                    option.FilePath = dialog.FileName;
                    refresh();
                }
                catch (Exception ex) { MessageBox.Show(this, "Could not load this audio file.\n\n" + ex.Message, "Custom sound", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            reset.Click += (_, _) => { option.FilePath = ""; refresh(); };
            test.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(option.FilePath))
                {
                    try { SoundLibrary.ValidateCustomFile(option.FilePath); }
                    catch (Exception ex) { MessageBox.Show(this, "Could not play this audio file.\n\n" + ex.Message, "Custom sound", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                }
                var player = MainForm.Instance?.SoundPlayer;
                if (player == null) return;
                var previous = Properties.Settings.Default.OutputDevice;
                try
                {
                    player.SetAudioDevice((_soundOutputDeviceComboBox.SelectedItem as AudioDevice)?.Id ?? previous);
                    player.PlaySound(sound, Math.Clamp(_volumeTrackBar.Value * 10, 0, 100), option.Enabled ? option.Volume : 0, option.FilePath ?? "");
                }
                finally { player.SetAudioDevice(previous); }
            };
            rows.Controls.Add(makeRow(enabled, volume, new Label { Text = "%", AutoSize = true }, test));
            var fileRow = new TableLayoutPanel { Width = 430, MinimumSize = new Size(430, 0), AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 12) };
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fileRow.Controls.Add(filePath, 0, 0); fileRow.Controls.Add(browse, 1, 0); fileRow.Controls.Add(reset, 2, 0);
            rows.Controls.Add(fileRow);
            refresh();
        }
        card.Controls.Add(rows); return card;
    }
}
