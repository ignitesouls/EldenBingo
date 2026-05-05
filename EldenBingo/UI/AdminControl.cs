using EldenBingo.Net;
using EldenBingo.Settings;
using EldenBingoCommon;
using Neto.Shared;
using System.Collections.Generic;
using System.Drawing;

namespace EldenBingo.UI
{
    internal partial class AdminControl : ClientUserControl
    {
        private System.Windows.Forms.Timer? _hideAdminMessageTimer;
        private Dictionary<string, Rectangle>? _originalBounds;

        public AdminControl()
        {
            InitializeComponent();
        }

        private void AdminControl_Load(object sender, EventArgs e)
        {
            if (!DesignMode)
            {
                _bingoJsonTextBox.Text = Properties.Settings.Default.LastBingoFile;
            }
            setFeatureToAllControls(this);

            // Capture original control bounds so we can restore when resizing
            try
            {
                if (_originalBounds == null)
                {
                    _originalBounds = new Dictionary<string, Rectangle>();
                    foreach (Control c in Controls)
                    {
                        if (!string.IsNullOrEmpty(c.Name) && !_originalBounds.ContainsKey(c.Name))
                            _originalBounds[c.Name] = c.Bounds;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Adjusts the admin control layout to fit within a narrow parent.
        /// If <paramref name="parentWidth"/> is below a threshold, stack action
        /// controls vertically to avoid horizontal overflow.
        /// </summary>
        public void AdjustLayoutForParentWidth(int parentWidth)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AdjustLayoutForParentWidth(parentWidth)));
                return;
            }

            try
            {
                const int threshold = 380; // when narrower than this, switch to vertical layout
                if (parentWidth <= 0)
                    return;

                if (parentWidth < threshold)
                {
                    int margin = 8;
                    int x = margin;
                    int y = margin;
                    int w = Math.Max(100, parentWidth - margin * 2);

                    // Title
                    label3.Location = new Point(x, y);
                    y += label3.Height + 6;

                    // Hide the legacy inline 'Upload Bingo JSON' label when in narrow stacked layout
                    try { label1.Visible = false; } catch { }

                    // JSON input
                    _bingoJsonTextBox.SetBounds(x, y, w, _bingoJsonTextBox.Height);
                    y += _bingoJsonTextBox.Height + 6;

                    // Browse / Upload as full-width buttons stacked
                    _browseJsonButton.SetBounds(x, y, w, _browseJsonButton.Height);
                    y += _browseJsonButton.Height + 6;
                    _uploadJsonButton.SetBounds(x, y, w, _uploadJsonButton.Height);
                    y += _uploadJsonButton.Height + 8;

                    // Main actions: stack the action buttons full-width (no bottom-right anchoring)
                    var actionButtons = new List<Control>() { _lobbySettingsButton, _generateNewBoardButton, _startMatchButton, _pauseMatchButton, _stopMatchButton };
                    int spacing = 6;
                    foreach (var b in actionButtons)
                    {
                        b.SetBounds(x, y, w, b.Height);
                        b.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        y += b.Height + spacing;
                    }

                    // Status label below the buttons
                    _adminStatusLabel.SetBounds(x, y, w, _adminStatusLabel.Height);
                    y += _adminStatusLabel.Height + margin;

                    // Resize control height to fit stacked content
                    this.Width = parentWidth;
                    this.Height = Math.Min(Math.Max(y, 120), 1000);
                }
                else
                {
                    // restore original bounds if available
                    if (_originalBounds != null)
                    {
                        foreach (Control c in Controls)
                        {
                            if (!string.IsNullOrEmpty(c.Name) && _originalBounds.ContainsKey(c.Name))
                                c.Bounds = _originalBounds[c.Name];
                        }
                        // restore size
                        if (_originalBounds.ContainsKey(this.Name))
                        {
                            this.Bounds = _originalBounds[this.Name];
                        }
                        // Ensure the upload label is visible again in restored layout
                        try { label1.Visible = true; } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// This method will disable arrow inputs for this control and all its child controls
        /// </summary>
        /// <param name="cc"></param>
        private void setFeatureToAllControls(Control cc)
        {
            if (cc != null)
            {
                foreach (Control control in cc.Controls)
                {
                    control.PreviewKeyDown += OnPreviewKeyDown;
                    setFeatureToAllControls(control);
                }
            }
        }

        private void OnPreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                e.IsInputKey = true;
            }
        }

        public override Color BackColor
        {
            get => base.BackColor;
            set
            {
                base.BackColor = value;
                //_consoleTextBox.BackColor = value;
            }
        }

        protected override void AddClientListeners()
        {
            if (Client == null)
                return;
            Client.Connected += client_Connected;
            Client.Disconnected += client_Disconnected;
            Client.OnRoomChanged += client_RoomChanged;
            Client.AddListener<ServerAdminStatusMessage>(adminStatusMessage);
            Client.AddListener<ServerCurrentGameSettings>(receivedGameSettings);
        }

        protected override void ClientChanged()
        {
            updateButtonsStatus();
        }

        protected override void RemoveClientListeners()
        {
            if (Client == null)
                return;
            Client.Connected -= client_Connected;
            Client.Disconnected -= client_Disconnected;
            Client.OnRoomChanged -= client_RoomChanged;
            Client.RemoveListener<ServerAdminStatusMessage>(adminStatusMessage);
            Client.RemoveListener<ServerCurrentGameSettings>(receivedGameSettings);
        }

        private void _browseJsonButton_Click(object sender, EventArgs e)
        {
            clearFocus();
            var file = _bingoJsonTextBox.Text;
            var dir = Path.GetDirectoryName(file);
            var dialog = new OpenFileDialog()
            {
                Filter = ".Json Files (*.json)|*.json|All Files (*.*)|*.*",
                InitialDirectory = string.IsNullOrWhiteSpace(dir) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : dir,
                FileName = string.IsNullOrWhiteSpace(file) || !File.Exists(file) ? string.Empty : _bingoJsonTextBox.Text,
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _bingoJsonTextBox.Text = dialog.FileName;
                Properties.Settings.Default.LastBingoFile = _bingoJsonTextBox.Text;
                Properties.Settings.Default.Save();
            }
        }

        private async void _generateNewBoardButton_Click(object sender, EventArgs e)
        {
            clearFocus();
            await randomizeNewBoard();
        }

        private async void _pauseMatchButton_Click(object sender, EventArgs e)
        {
            clearFocus();
            await tryTogglePause();
        }

        private async void _startMatchButton_Click(object sender, EventArgs e)
        {
            clearFocus();
            await tryChangeMatchStatus(MatchStatus.Starting);
        }


        private async void _stopMatchButton_Click(object sender, EventArgs e)
        {
            clearFocus();
            if (MessageBox.Show("Stop match? The match will end immediately", "Stop match", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                await tryChangeMatchStatus(MatchStatus.Finished);
        }

        private async void _uploadJsonButton_Click(object sender, EventArgs e)
        {
            clearFocus();
            await uploadJsonData(_bingoJsonTextBox.Text);
        }

        private void clearFocus()
        {
            label3.Focus();
        }


        private void client_Connected(object? sender, EventArgs e)
        {
            updateButtonsStatus();
        }

        private void client_Disconnected(object? sender, StringEventArgs e)
        {
            updateButtonsStatus();
        }

        private void adminStatusMessage(ClientModel? _, ServerAdminStatusMessage message)
        {
            updateAdminStatusText(message.Message, Color.FromArgb(message.Color));
        }

        private void client_RoomChanged(object? sender, RoomChangedEventArgs e)
        {
            updateButtonsStatus();
            if (e.PreviousRoom != null)
                e.PreviousRoom.Match.MatchStatusChanged -= match_MatchStatusChanged;
            if (e.NewRoom != null)
                e.NewRoom.Match.MatchStatusChanged += match_MatchStatusChanged;
        }

        private void match_MatchStatusChanged(object? sender, EventArgs e)
        {
            updateButtonsStatus();
        }

        private async Task randomizeNewBoard()
        {
            if (Client?.Room == null)
            {
                errorProvider1.SetError(_generateNewBoardButton, "Not in a room");
                return;
            }
            if (Client?.LocalUser?.IsAdmin != true)
            {
                errorProvider1.SetError(_generateNewBoardButton, "Not admin");
                return;
            }
            errorProvider1.SetError(_bingoJsonTextBox, null);
            var p = new Packet(new ClientRandomizeBoard());
            await Client.SendPacketToServer(p);
        }

        private async Task tryTogglePause()
        {
            if (Client == null)
                return;

            var p = new Packet(new ClientTogglePause());
            await Client.SendPacketToServer(p);
        }

        private async Task tryChangeMatchStatus(MatchStatus status)
        {
            if (Client == null)
                return;

            var p = new Packet(new ClientChangeMatchStatus(status));
            await Client.SendPacketToServer(p);
        }

        private void updateAdminStatusText(string text, Color color)
        {
            void update()
            {
                if (_hideAdminMessageTimer != null)
                    _hideAdminMessageTimer.Tick -= _hideAdminMessageTimer_Tick;
                _adminStatusLabel.Text = text;
                _adminStatusLabel.ForeColor = color;
                _hideAdminMessageTimer = new System.Windows.Forms.Timer();
                _hideAdminMessageTimer.Interval = 6000;
                _hideAdminMessageTimer.Tick += _hideAdminMessageTimer_Tick;
                _hideAdminMessageTimer.Start();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void _hideAdminMessageTimer_Tick(object? sender, EventArgs e)
        {
            hideText();
            _hideAdminMessageTimer.Stop();
        }

        private void hideText()
        {
            void update()
            {
                _adminStatusLabel.Text = string.Empty;
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void updateButtonsStatus()
        {
            void update()
            {
                var inRoom = Client?.Room != null;
                var admin = inRoom && Client?.LocalUser?.IsAdmin == true;
                var matchStarted = Client?.Room != null && (Client.Room.Match.Running || Client.Room.Match.Paused);
                _browseJsonButton.Enabled = admin;
                _uploadJsonButton.Enabled = admin;
                _lobbySettingsButton.Enabled = admin;
                _generateNewBoardButton.Enabled = admin;
                _startMatchButton.Enabled = admin && !matchStarted;
                _pauseMatchButton.Enabled = admin && matchStarted;
                if (admin)
                    _pauseMatchButton.Text = Client?.Room != null && Client.Room.Match.Paused ? "Unpause Match" : "Pause Match";
                _stopMatchButton.Enabled = admin && matchStarted;
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private async Task uploadJsonData(string file)
        {
            if (!File.Exists(file))
            {
                errorProvider1.SetError(_bingoJsonTextBox, "File not found");
                return;
            }
            if (Client?.Room == null)
            {
                errorProvider1.SetError(_uploadJsonButton, "Not in a room");
                return;
            }
            if (Client?.LocalUser?.IsAdmin != true)
            {
                errorProvider1.SetError(_uploadJsonButton, "Not admin");
                return;
            }
            try
            {
                string json = File.ReadAllText(file);
                errorProvider1.SetError(_bingoJsonTextBox, null);
                errorProvider1.SetError(_uploadJsonButton, null);

                var p = new Packet(new ClientBingoJson(json));
                await Client.SendPacketToServer(p);
            }
            catch (IOException ex)
            {
                errorProvider1.SetError(_bingoJsonTextBox, $"Could not read file: {ex.Message}");
            }
        }

        private async void _lobbySettingsButton_Click(object sender, EventArgs e)
        {
            if (Client?.Room == null)
            {
                return; //Not in a room
            }
            var request = new ClientRequestCurrentGameSettings();
            await Client.SendPacketToServer(new Packet(request));
        }

        private void receivedGameSettings(ClientModel? _, ServerCurrentGameSettings gameSettingsArgs)
        {
            //Open the settings window without locking the receiver thread
            Task.Run(() => openSettingsWindow(gameSettingsArgs.GameSettings));
        }

        private void openSettingsWindow(BingoGameSettings settings)
        {
            if (Client?.Room == null)
            {
                return; //Not in a room
            }

            async void openWindow()
            {
                var mainForm = MainForm.GetMainForm(this);
                try
                {
                    if (mainForm != null)
                    {
                        mainForm.TopMost = false;
                    }
                    var form = new GameSettingsForm();
                    form.Settings = settings;

                    if (form.ShowDialog(mainForm) == DialogResult.OK)
                    {
                        GameSettingsHelper.SaveToSettings(form.Settings, Properties.Settings.Default);
                        var request = new ClientSetGameSettings(form.Settings);
                        await Client.SendPacketToServer(new Packet(request));
                    }
                }
                finally
                {
                    if (mainForm != null)
                    {
                        mainForm.TopMost = Properties.Settings.Default.AlwaysOnTop;
                    }
                }
            }
            if (InvokeRequired)
            {
                BeginInvoke(openWindow);
                return;
            }
            openWindow();
        }
    }
}