using EldenBingo.Export;

namespace EldenBingo.UI
{
    internal partial class LobbyControl
    {
        private LocalJsonPublisher? _livePublisher;
        private Button _shareJsonButton = null!;
        private Label _shareJsonStatus = null!;
        private bool _shareSetup;
        private ExportHealth _webHealth = ExportHealth.Off;
        internal ExportHealth WebExportHealth => Volatile.Read(ref _webHealth) with { Active = Volatile.Read(ref _livePublisher) != null };

        private void initializeLiveShare(TableLayoutPanel panel)
        {
            panel.Height = 88;
            panel.RowCount = 3;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            _shareJsonButton = new Button { Text = "Local JSON URL…", Dock = DockStyle.Fill, Margin = Padding.Empty, Enabled = false, UseVisualStyleBackColor = true };
            _shareJsonStatus = new Label { Text = "Local URL off", Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(3),
                BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText, Font = new Font("Segoe UI", 8) };
            panel.Controls.Add(_shareJsonButton);
            panel.Controls.Add(_shareJsonStatus);
            _shareJsonButton.Click += configureLiveSharing;
            _exportTracker.Changed += json => Volatile.Read(ref _livePublisher)?.Publish(json);
        }

        private async void configureLiveSharing(object? sender, EventArgs e)
        {
            if (_shareSetup || Client?.Room == null) return;
            _shareSetup = true;
            var room = Client.Room;
            var generation = Interlocked.Read(ref _exportRoomGeneration);
            try
            {
                using var dialog = new LiveShareForm(_livePublisher?.Slot ?? Math.Clamp(Properties.Settings.Default.LocalJsonSlot, 1, 99),
                    _livePublisher?.Port ?? Math.Clamp(Properties.Settings.Default.LocalJsonPort, 1024, 65535), _livePublisher?.ReadUrl.AbsoluteUri)
                    { TopMost = MainForm.Instance?.TopMost ?? false };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!dialog.SharingEnabled) { await stopLiveSharing(); return; }
                if (_livePublisher != null) return;
                if (Client?.Room != room) return;
                _shareJsonButton.Enabled = false;
                shareStatus(false, "Starting local JSON service…");
                Properties.Settings.Default.LocalJsonSlot = dialog.Slot;
                Properties.Settings.Default.LocalJsonPort = dialog.Port;
                Properties.Settings.Default.Save();
                var publisher = await LocalJsonPublisher.StartAsync(dialog.Slot, dialog.Port, _exportTracker.Serialize());
                publisher.StatusChanged += (ok, text) => shareStatus(!ok, text, publisher);
                lock (_exportSessionGate)
                {
                    if (generation == _exportRoomGeneration && Client?.Room == room) Volatile.Write(ref _livePublisher, publisher);
                }
                if (!ReferenceEquals(_livePublisher, publisher)) { await publisher.DisposeAsync(); return; }
                publisher.Publish(_exportTracker.Serialize());
                shareStatus(false, $"Local lobby {publisher.Slot} — open to copy URL", publisher);
                using var ready = new LiveShareForm(publisher.Slot, publisher.Port, publisher.ReadUrl.AbsoluteUri) { TopMost = MainForm.Instance?.TopMost ?? false };
                if (ready.ShowDialog(this) == DialogResult.OK && !ready.SharingEnabled) await stopLiveSharing();
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or ArgumentException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or System.Configuration.ConfigurationErrorsException)
            {
                shareStatus(true, "Could not start local URL: " + ex.Message);
                if (!IsDisposed) MessageBox.Show(this, "Could not start local URL.\n\n" + ex.Message, "Local JSON URL", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                _shareSetup = false;
                if (!IsDisposed) _shareJsonButton.Enabled = Client?.Room != null;
            }
        }

        private async Task stopLiveSharing()
        {
            var publisher = Interlocked.Exchange(ref _livePublisher, null);
            shareStatus(false, "Local URL off");
            if (publisher != null) await publisher.DisposeAsync();
        }

        private void stopLiveSharingForRoomChange()
        {
            var publisher = Interlocked.Exchange(ref _livePublisher, null);
            if (publisher != null) _ = publisher.DisposeAsync();
            shareStatus(false, "Local URL off");
        }

        private void shareStatus(bool error, string text, LocalJsonPublisher? expected = null)
        {
            lock (_exportSessionGate)
            {
                if (expected != null && !ReferenceEquals(Volatile.Read(ref _livePublisher), expected)) return;
                var active = Volatile.Read(ref _livePublisher) != null;
                Volatile.Write(ref _webHealth, new(active, error, text,
                    !active ? null : !error && expected != null ? DateTimeOffset.Now : _webHealth.LastSuccess));
            }
            void update()
            {
                if (IsDisposed || Disposing || expected != null && !ReferenceEquals(_livePublisher, expected)) return;
                _shareJsonStatus.Text = text;
                _shareJsonStatus.ForeColor = error ? Color.Maroon : SystemColors.ControlText;
                _shareJsonButton.Text = _livePublisher == null ? "Local JSON URL…" : $"Local lobby {_livePublisher.Slot}: On…";
                _shareJsonButton.Enabled = !_shareSetup && Client?.Room != null;
                _liveExportTip.SetToolTip(_shareJsonStatus, text);
                _liveExportTip.SetToolTip(_shareJsonButton, _livePublisher?.ReadUrl.AbsoluteUri ?? "Serve match JSON on localhost with a stable lobby slot");
            }
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try { if (InvokeRequired) BeginInvoke((Action)update); else update(); }
            catch (InvalidOperationException) { }
        }
    }
}
