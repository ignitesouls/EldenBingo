using EldenBingo.Net;
using EldenBingoCommon;
using Neto.Shared;
using System.Drawing.Drawing2D;
using System.Linq;

namespace EldenBingo.UI
{
    internal partial class LobbyControl : ClientUserControl
    {
        private static LobbyControl? _instance;
        private int _adminHeight = 0;
        private MatchStatus _lastMatchStatus;
        private bool _lastPaused;
        private System.Timers.Timer? _timer;
        private Panel _battleshipSpectatorLegendPanel = null!;
        private const bool EnableStatusDebugOverlay = false;
        private TransparentOverlayPanel? _statusOverlayPanel;
        private Panel? _adminHostPanel;
        private string _battleshipLegendTeamAName = "Team A";
        private string _battleshipLegendTeamBName = "Team B";
        private Color _battleshipLegendTeamAColor = Color.LightCyan;
        private Color _battleshipLegendTeamBColor = Color.LightCoral;

        public LobbyControl() : base()
        {
            InitializeComponent();

            // Move admin controls into the right-side roster panel so admin-only sizing
            // doesn't change the main bingo board layout. Do this at runtime to avoid
            // editing designer generated parent/anchor code.
            try
            {
                if (splitContainer1 != null && adminControl1 != null)
                {
                    if (splitContainer1.Panel1.Controls.Contains(adminControl1))
                        splitContainer1.Panel1.Controls.Remove(adminControl1);

                    // Create a stacked right-side layout: top = roster/log, bottom = admin host.
                    int adminHeight = Math.Max(140, adminControl1.Height);

                    _adminHostPanel = new Panel()
                    {
                        BackColor = Properties.Settings.Default.ControlBackColor,
                        Height = adminHeight,
                        Dock = DockStyle.Fill,
                    };

                    // Move existing Panel2 children (usually _clientList and _adminInfoLabel)
                    // into a new top panel, then create a TableLayoutPanel to stack top + admin.
                    var existing = splitContainer1.Panel2.Controls.Cast<Control>().ToList();
                    foreach (var c in existing)
                    {
                        try { splitContainer1.Panel2.Controls.Remove(c); } catch { }
                    }

                    // Create a simple stacked layout: top fills, bottom reserved for admin.
                    var rightTopPanel = new Panel() { Dock = DockStyle.Fill, BackColor = Properties.Settings.Default.ControlBackColor };
                    var rightBottomPanel = new Panel() { Dock = DockStyle.Bottom, Height = adminHeight, BackColor = Properties.Settings.Default.ControlBackColor };

                    // Move existing Panel2 children (usually _clientList and _adminInfoLabel)
                    // into the new top panel.
                    foreach (var c in existing)
                    {
                        try
                        {
                            if (c == _clientList)
                                c.Dock = DockStyle.Fill;
                            else if (c == _adminInfoLabel)
                                c.Dock = DockStyle.Bottom;
                            else
                                c.Dock = DockStyle.Top;
                            rightTopPanel.Controls.Add(c);
                        }
                        catch { }
                    }

                    // Use the bottom panel as the admin host so we preserve its reserved height
                    _adminHostPanel = rightBottomPanel;
                    _adminHostPanel.Controls.Add(adminControl1);
                    adminControl1.Dock = DockStyle.Fill;

                    // Add top then bottom so bottom stays docked to bottom
                    splitContainer1.Panel2.Controls.Add(rightTopPanel);
                    splitContainer1.Panel2.Controls.Add(rightBottomPanel);
                    try { rightTopPanel.BringToFront(); adminControl1.BringToFront(); } catch { }

                    // Adjust admin layout to fit the host panel width and compute a host height
                    try { adminControl1.AdjustLayoutForParentWidth(_adminHostPanel.ClientSize.Width); } catch { }

                    void recomputeAdminHostHeight()
                    {
                        try
                        {
                            var pref = adminControl1.GetPreferredSize(new Size(_adminHostPanel.ClientSize.Width > 0 ? _adminHostPanel.ClientSize.Width : 300, 0));
                            var target = Math.Min(Math.Max(pref.Height, 120), 400); // clamp height between 120 and 400
                            _adminHostPanel.Height = target;
                        }
                        catch { }
                    }

                    // Initial height calculation and on size changes
                    recomputeAdminHostHeight();
                    _adminHostPanel.SizeChanged += (s, e) => { try { adminControl1.AdjustLayoutForParentWidth(_adminHostPanel.ClientSize.Width); recomputeAdminHostHeight(); } catch { } };
                    splitContainer1.Panel2.SizeChanged += (s, e) => { try { adminControl1.AdjustLayoutForParentWidth(_adminHostPanel.ClientSize.Width); recomputeAdminHostHeight(); } catch { } };
                }
            }
            catch { }

            _instance = this;
            _adminHeight = adminControl1.Height;

            // Host battleship placement panel in the right-side status area
            _lobbyStatusPanel.Controls.Add(_battleshipControl.PlacementPanel);
            _battleshipControl.PlacementPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _battleshipControl.PlacementPanel.VisibleChanged += (s, e) => updateScoreboardControlLocationAndSize();

            // Use the app control background color for the status area to match the right-hand pane
            var statusBlue = Properties.Settings.Default.ControlBackColor;
            _lobbyStatusPanel.BackColor = statusBlue;
            _timerLabel.BackColor = statusBlue;
            _matchStatusLabel.BackColor = statusBlue;

            // Ensure the placement panel is opaque and matches the status background
            _battleshipControl.PlacementPanel.BackColor = statusBlue;

            _battleshipSpectatorLegendPanel = new Panel
            {
                Visible = false,
                BackColor = Properties.Settings.Default.ControlBackColor,
                BorderStyle = BorderStyle.None,
                Height = 120,
            };
            _battleshipSpectatorLegendPanel.Paint += drawBattleshipSpectatorLegend;
            _lobbyStatusPanel.Controls.Add(_battleshipSpectatorLegendPanel);

            // Temporary debug overlay: draws bounds for status-area controls when enabled and spectator
            if (EnableStatusDebugOverlay)
            {
                _statusOverlayPanel = new TransparentOverlayPanel
                {
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    Visible = true,
                };
                _statusOverlayPanel.Paint += StatusOverlay_Paint;
                _lobbyStatusPanel.Controls.Add(_statusOverlayPanel);
                _statusOverlayPanel.BringToFront();
            }

            listenToSettingsChanged();
            Load += lobbyControl_Load;

            splitContainer1.SplitterDistance = Width - Convert.ToInt32(200f * this.DefaultScaleFactors().Width);
            _adminInfoLabel.Height = Convert.ToInt32(_adminInfoLabel.Height * this.DefaultScaleFactors().Height);

            SizeChanged += lobbyControl_SizeChanged;
            splitContainer1.Panel1.SizeChanged += bingoPanel_SizeChanged;
        }

        public static UserInRoom? CurrentlyOnBehalfOfUser
        {
            get
            {
                if (_instance == null || _instance.Client == null || _instance.Client.LocalUser == null)
                    return null;

                if (_instance.Client.LocalUser.IsAdmin != true || _instance.Client.LocalUser.IsSpectator != true)
                    return _instance.Client.LocalUser;

                var selectedClient = _instance._clientList.SelectedItem as UserInRoom;
                return selectedClient ?? _instance.Client.LocalUser;
            }
        }

        public override Color BackColor
        {
            get => base.BackColor;
            set
            {
                base.BackColor = value;
                _clientList.BackColor = value;
                adminControl1.BackColor = value;
            }
        }

        protected override void AddClientListeners()
        {
            Client.OnRoomChanged += client_RoomChanged;
            Client.AddListener<ServerUserChecked>(userChecked);
            Client.AddListener<ServerUserJoinedRoom>(userJoined);
            Client.AddListener<ServerUserLeftRoom>(userLeft);
            Client.AddListener<ServerEntireBingoBoardUpdate>(gotBingoBoard);
            Client.AddListener<ServerUserChat>(userChat);
            Client.AddListener<ServerBingoAchievedUpdate>(bingoAchieved);
            Client.AddListener<ServerTeamNameChanged>(teamNameChanged);
            Client.AddListener<ServerUserChangedTeam>(userChangedTeam);
            Client.AddListener<ServerBroadcastMessage>(serverMessage);
            Client.OnBattleshipConfig += client_BattleshipConfig;
            Client.OnAllShipsPlaced += client_AllShipsPlaced;
            Client.OnBattleshipGameOver += client_BattleshipGameOver;
            Client.OnAttackResult += client_AttackResult;
        }

        protected override void ClientChanged()
        {
            _bingoControl.Client = Client;
            _battleshipControl.Client = Client;
            _clientList.Client = Client;
            _scoreboardControl.Client = Client;
            if (adminControl1 != null)
            {
                adminControl1.Client = Client;
            }
            if (Client?.Room != null)
            {
                showHideAdminControls();
                updateMatchStatus(Client.Room.Match);
                setMatchTimerLabel(Client.Room.Match.TimerString);
                restartAndListenToTimer();
            }
        }

        protected override void RemoveClientListeners()
        {
            Client.OnRoomChanged -= client_RoomChanged;
            Client.RemoveListener<ServerUserChecked>(userChecked);
            Client.RemoveListener<ServerUserJoinedRoom>(userJoined);
            Client.RemoveListener<ServerUserLeftRoom>(userLeft);
            Client.RemoveListener<ServerEntireBingoBoardUpdate>(gotBingoBoard);
            Client.RemoveListener<ServerUserChat>(userChat);
            Client.RemoveListener<ServerBingoAchievedUpdate>(bingoAchieved);
            Client.OnBattleshipConfig -= client_BattleshipConfig;
            Client.OnAllShipsPlaced -= client_AllShipsPlaced;
            Client.OnBattleshipGameOver -= client_BattleshipGameOver;
            Client.OnAttackResult -= client_AttackResult;
        }

        private void userChecked(ClientModel? _, ServerUserChecked userCheckedArgs)
        {
            if (Client?.Room != null && Client.BingoBoard != null && userCheckedArgs.Index >= 0 && userCheckedArgs.Index < Client.BingoBoard.SquareCount)
            {
                var user = Client.Room.GetUser(userCheckedArgs.UserGuid);
                var playerName = user?.Nick ?? "Unknown";
                Color? playerColor = user?.ColorBright;
                Color? checkColor = userCheckedArgs.Team > -1 ? BingoConstants.GetTeamColorBright(userCheckedArgs.Team) : playerColor;

                var square = Client.BingoBoard.Squares[userCheckedArgs.Index];
                bool isChecked = square.IsChecked(userCheckedArgs.Team);
                updateMatchLog(new[] { playerName, isChecked ? "marked" : "unmarked", square.Text },
                               new Color?[] { playerColor, null, checkColor }, true);
            }
        }

        private void userJoined(ClientModel? _, ServerUserJoinedRoom userJoinedArgs)
        {
            if (Client?.Room != null)
            {
                updateMatchLog(new[] { userJoinedArgs.User.Nick, "joined the lobby" },
                        new Color?[] { userJoinedArgs.User.ColorBright, null }, true);
            }
        }

        private void userLeft(ClientModel? _, ServerUserLeftRoom userLeftArgs)
        {
            if (Client?.Room != null)
            {
                updateMatchLog(new[] { userLeftArgs.User.Nick, "left the lobby" },
                        new Color?[] { userLeftArgs.User.ColorBright, null }, true);
            }
        }

        private void gotBingoBoard(ClientModel? _, ServerEntireBingoBoardUpdate bingoBoardArgs)
        {
            if (bingoBoardArgs.AvailableClasses.Length <= 0)
                return;

            var prep = bingoBoardArgs.AvailableClasses.Length == 1 ? "Required class is:" : "Valid classes are:";
            var strings = new List<string>();
            var colors = new List<Color?>();
            foreach (var cl in bingoBoardArgs.AvailableClasses)
            {
                if (strings.Count == 0)
                    strings.Add(prep);
                else
                    strings.Add(",");
                strings.Add(cl.ToString());
                colors.Add(null);
                colors.Add(BingoConstants.ClassColors[(int)cl]);
            }
            colors.Add(null);
            updateMatchLog(strings.ToArray(), colors.ToArray(), false);
        }

        private void userChat(ClientModel? _, ServerUserChat chatArgs)
        {
            if (Client?.Room != null)
            {
                var user = Client.Room.GetUser(chatArgs.UserGuid);
                if (user != null)
                {
                    updateMatchLog(new[] { user.Nick, ":", chatArgs.Message },
                        new Color?[] { user.ColorBright, null, null }, true);
                }
            }
        }

        private void bingoAchieved(ClientModel? _, ServerBingoAchievedUpdate update)
        {
            string linename;
            switch (update.Bingo.Type)
            {
                case 0:
                    linename = $"column {update.Bingo.BingoIndex + 1}";
                    break;
                case 1:
                    linename = $"row {update.Bingo.BingoIndex + 1}";
                    break;
                case 2:
                    linename = $"diagonal TL->BR";
                    break;
                case 3:
                    linename = $"diagonal BL->TR";
                    break;
                default:
                    linename = "unknown";
                    break;
            }
            updateMatchLog(new string[] { update.Bingo.Name, $"BINGO on {linename}!" }, new Color?[] { BingoConstants.GetTeamColorBright(update.Bingo.Team), null }, true);
        }

        private void teamNameChanged(ClientModel? model, ServerTeamNameChanged teamNameChanged)
        {
            if (Client?.Room != null)
            {
                var user = Client.Room.GetUser(teamNameChanged.UserGuid);
                if (user != null)
                {
                    var teamColor = BingoConstants.GetTeamColorBright(teamNameChanged.Team);
                    updateMatchLog(
                        new string[] { user.Nick, $"changed name of", teamNameChanged.TeamColorName, "to", teamNameChanged.Name },
                        new Color?[] { BingoConstants.GetTeamColorBright(user.Team), null, teamColor, null, teamColor },
                        true);
                    updateBattleshipSpectatorLegend();
                }
            }
        }

        private void userChangedTeam(ClientModel? model, ServerUserChangedTeam teamChanged)
        {
            if (Client?.Room != null)
            {
                var user = Client.Room.GetUser(teamChanged.UserGuid);
                if (user != null)
                {
                    var oldTeamColor = BingoConstants.GetTeamColorBright(user.Team);
                    var newTeamColor = BingoConstants.GetTeamColorBright(teamChanged.Team);
                    user.Team = teamChanged.Team;
                    updateMatchLog(
                        new string[] { user.Nick, $"changed team to", teamChanged.TeamColorName },
                        new Color?[] { oldTeamColor, null, newTeamColor },
                        true);
                    updateBattleshipSpectatorLegend();
                }
            }
        }

        private void serverMessage(ClientModel? model, ServerBroadcastMessage message)
        {
            updateMatchLog(new[] { $"Server: {message.Message}" }, new Color?[] { Color.Orange }, true);
        }

        private void client_BattleshipConfig(object? sender, BattleshipConfigEventArgs e)
        {
            void update()
            {
                // Switch to battleship view
                _bingoControl.Visible = false;
                _battleshipControl.Visible = true;
                updateBattleshipSpectatorLegend();
                updateBingoPanelSize();
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
            updateMatchLog(new[] { "Battleship mode! Place your ships." }, new Color?[] { Color.Cyan }, true);
        }

        private void client_AllShipsPlaced(object? sender, EventArgs e)
        {
            updateMatchLog(new[] { "All ships placed! The battle begins!" }, new Color?[] { Color.Lime }, true);
        }

        private void client_BattleshipGameOver(object? sender, BattleshipGameOverEventArgs e)
        {
            updateMatchLog(
                new[] { "GAME OVER!", e.WinningTeamName, "wins!" },
                new Color?[] { Color.Gold, BingoConstants.GetTeamColorBright(e.WinningTeam), Color.Gold },
                true);
        }

        private void client_AttackResult(object? sender, AttackResultEventArgs e)
        {
            string resultStr = e.Result switch
            {
                AttackResult.Miss => "Miss!",
                AttackResult.Hit => "Hit!",
                AttackResult.Sunk => "SUNK!",
                _ => ""
            };
            Color resultColor = e.Result switch
            {
                AttackResult.Miss => Color.LightBlue,
                AttackResult.Hit => Color.Orange,
                AttackResult.Sunk => Color.Red,
                _ => Color.White
            };
            string attacker = BingoConstants.GetTeamName(e.AttackingTeam);
            string defender = BingoConstants.GetTeamName(e.DefendingTeam);
            string squareName = Client?.BingoBoard?.Squares.ElementAtOrDefault(e.Index).Text ?? "";
            string squarePart = string.IsNullOrWhiteSpace(squareName) ? "" : $" ({squareName})";
            updateMatchLog(
                new[] { attacker, "attacks", defender + squarePart + ":", resultStr },
                new Color?[] { BingoConstants.GetTeamColorBright(e.AttackingTeam), null, BingoConstants.GetTeamColorBright(e.DefendingTeam), resultColor },
                true);
            if (e.SunkShip != null)
            {
                updateMatchLog(
                    new[] { $"  {defender}'s {e.SunkShip.Value.ShipName} has been sunk!" },
                    new Color?[] { Color.Red },
                    false);
            }
        }

        private void _scoreboardControl_SizeChanged(object sender, EventArgs e)
        {
            updateScoreboardControlLocationAndSize();
        }

        private void updateScoreboardControlLocationAndSize()
        {
            void update()
            {
                var startPosY = _scoreboardControl.Bottom + 3;

                if (_battleshipSpectatorLegendPanel.Visible)
                {
                    _battleshipSpectatorLegendPanel.Location = new Point(5, startPosY);
                    _battleshipSpectatorLegendPanel.Width = _lobbyStatusPanel.Width - 10;
                    startPosY = _battleshipSpectatorLegendPanel.Bottom + 3;
                }

                // If battleship placement panel is visible, position it between scoreboard and log
                var placementPanel = _battleshipControl.PlacementPanel;
                if (placementPanel.Visible)
                {
                    placementPanel.Location = new Point(_logBoxBorderPanel.Left, startPosY);
                    placementPanel.Width = _logBoxBorderPanel.Width;
                    placementPanel.Height = 180;
                    startPosY = placementPanel.Bottom + 3;
                }

                _logBoxBorderPanel.Location = new Point(_logBoxBorderPanel.Location.X, startPosY);
                _logBoxBorderPanel.Height = _lobbyStatusPanel.Height - _logBoxBorderPanel.Location.Y - 3;
                _statusOverlayPanel?.Invalidate();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void StatusOverlay_Paint(object? sender, PaintEventArgs e)
        {
            if (!EnableStatusDebugOverlay)
                return;
            if (Client?.LocalUser == null)
                return;
            // Allow overlay for spectators and admins so we can debug admin-only artifacts
            if (Client.LocalUser.IsSpectator != true && Client.LocalUser.IsAdmin != true)
                return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            using var penLegend = new Pen(Color.FromArgb(200, Color.Red), 2);
            using var penPlacement = new Pen(Color.FromArgb(200, Color.Lime), 2);
            using var penLog = new Pen(Color.FromArgb(200, Color.Cyan), 2);
            using var penScore = new Pen(Color.FromArgb(200, Color.Magenta), 2);
            using var font = new Font("Segoe UI", 8, FontStyle.Bold);

            try
            {
                if (_battleshipSpectatorLegendPanel != null && _battleshipSpectatorLegendPanel.Visible)
                {
                    var r = _battleshipSpectatorLegendPanel.Bounds;
                    g.DrawRectangle(penLegend, r.X, r.Y, r.Width - 1, r.Height - 1);
                    g.DrawString("Legend", font, Brushes.White, r.X + 4, r.Y + 4);
                }

                var placement = _battleshipControl.PlacementPanel;
                if (placement != null && placement.Visible)
                {
                    var r = placement.Bounds;
                    g.DrawRectangle(penPlacement, r.X, r.Y, r.Width - 1, r.Height - 1);
                    g.DrawString("PlacementPanel", font, Brushes.White, r.X + 4, r.Y + 4);
                }

                var rLog = _logBoxBorderPanel.Bounds;
                g.DrawRectangle(penLog, rLog.X, rLog.Y, rLog.Width - 1, rLog.Height - 1);
                g.DrawString("Log", font, Brushes.White, rLog.X + 4, rLog.Y + 4);

                var rScore = _scoreboardControl.Bounds;
                g.DrawRectangle(penScore, rScore.X, rScore.Y, rScore.Width - 1, rScore.Height - 1);
                g.DrawString("Scoreboard", font, Brushes.White, rScore.X + 4, rScore.Y + 4);
            }
            catch
            {
                // Swallow any painting exceptions during debug overlay
            }
        }

        private void updateBattleshipSpectatorLegend()
        {
            void update()
            {
                bool show = _battleshipControl.Visible && Client?.LocalUser?.IsSpectator == true;
                _battleshipSpectatorLegendPanel.Visible = show;
                if (!show)
                    return;

                var teams = Client?.Room?.Users?
                    .Where(u => u.Team >= 0)
                    .Select(u => u.Team)
                    .Distinct()
                    .OrderBy(t => t)
                    .Take(2)
                    .ToList() ?? new List<int>();

                string teamA = teams.Count > 0 ? BingoConstants.GetTeamName(teams[0]) : "Team A";
                string teamB = teams.Count > 1 ? BingoConstants.GetTeamName(teams[1]) : "Team B";

                Color teamAColor = teams.Count > 0 ? BingoConstants.GetTeamColorBright(teams[0]) : Color.LightCyan;
                Color teamBColor = teams.Count > 1 ? BingoConstants.GetTeamColorBright(teams[1]) : Color.LightCoral;

                _battleshipLegendTeamAName = teamA;
                _battleshipLegendTeamBName = teamB;
                _battleshipLegendTeamAColor = teamAColor;
                _battleshipLegendTeamBColor = teamBColor;
                _battleshipSpectatorLegendPanel.Invalidate();

                updateScoreboardControlLocationAndSize();
            }

            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void drawBattleshipSpectatorLegend(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = _battleshipSpectatorLegendPanel.ClientRectangle;

            // Draw a filled background (no framed border to avoid visible boxed artifact)
            using (var bg = new SolidBrush(_battleshipSpectatorLegendPanel.BackColor))
            {
                g.FillRectangle(bg, bounds);
            }

            int x = 8;
            int y = 6;
            int iconSize = 12;
            int lineHeight = 22;

            using var headerFont = new Font("Segoe UI", 8.75f, FontStyle.Bold);
            using var itemFont = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            using var whiteBrush = new SolidBrush(Color.White);
            using var dimBrush = new SolidBrush(Color.FromArgb(210, 210, 210));
            using var sunkPen = new Pen(Color.White, 2f);
            using var shipBrush = new SolidBrush(Color.FromArgb(70, 120, 120, 120));

            // Header
            g.DrawString("Spectator Legend", headerFont, whiteBrush, x, y);
            y += 20;

            // Team A hit/miss markers
            using var teamABrush = new SolidBrush(_battleshipLegendTeamAColor);
            using var teamAPen = new Pen(_battleshipLegendTeamAColor, 2f);
            g.DrawEllipse(teamAPen, x, y + 1, iconSize, iconSize);
            g.FillEllipse(teamABrush, x + 19, y + 4, 6, 6);
            g.DrawString($"O / dot = {_battleshipLegendTeamAName}", itemFont, teamABrush, x + 32, y);
            y += lineHeight;

            // Team B hit/miss markers
            using var teamBBrush = new SolidBrush(_battleshipLegendTeamBColor);
            using var teamBPen = new Pen(_battleshipLegendTeamBColor, 2f);
            g.DrawRectangle(teamBPen, x, y + 1, iconSize, iconSize);
            g.FillRectangle(teamBBrush, x + 19, y + 4, 6, 6);
            g.DrawString($"[] / square = {_battleshipLegendTeamBName}", itemFont, teamBBrush, x + 32, y);
            y += lineHeight;

            // Ship tint and sunk markers
            g.FillRectangle(shipBrush, x, y + 1, iconSize, iconSize);
            g.DrawRectangle(Pens.Gray, x, y + 1, iconSize, iconSize);
            g.DrawRectangle(sunkPen, x + 19, y + 1, iconSize, iconSize);
            g.DrawString("Tint = ship, white border = sunk", itemFont, dimBrush, x + 36, y);

            // Bottom subtle separator removed to avoid visible framed line when legend is transparent
        }

        private void _timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (Client?.Room?.Match != null)
                setMatchTimerLabel(Client.Room.Match.TimerString);
        }

        private void appendText(string text, Color color)
        {
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.SelectionLength = 0;

            _logTextBox.SelectionColor = color;
            _logTextBox.AppendText(text);
            _logTextBox.SelectionColor = _logTextBox.ForeColor;
        }

        private void client_RoomChanged(object? sender, RoomChangedEventArgs e)
        {
            if (e.PreviousRoom != null)
            {
                e.PreviousRoom.Match.MatchStatusChanged -= match_MatchStatusChanged;
            }
            clearMatchLog();
            showHideAdminControls();
            if (e.NewRoom != null)
            {
                //Set this so it doesn't print the new value
                _lastMatchStatus = e.NewRoom.Match.MatchStatus;
                _lastPaused = e.NewRoom.Match.Paused;
                updateMatchStatus(e.NewRoom.Match);
                setMatchTimerLabel(e.NewRoom.Match.TimerString);
                restartAndListenToTimer();
                e.NewRoom.Match.MatchStatusChanged += match_MatchStatusChanged;
            }
            updateBattleshipSpectatorLegend();
            updateScoreboardControlLocationAndSize();
        }

        private void default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.Default.BingoMaxSizeX) ||
                e.PropertyName == nameof(Properties.Settings.Default.BingoMaxSizeY) ||
                e.PropertyName == nameof(Properties.Settings.Default.BingoBoardMaximumSize))
            {
                updateBingoMaximumSize();
                updateBingoPanelSize();
            }
            if (e.PropertyName == nameof(Properties.Settings.Default.BingoFont) ||
                e.PropertyName == nameof(Properties.Settings.Default.BingoFontSize) ||
                e.PropertyName == nameof(Properties.Settings.Default.BingoFontStyle))
            {
                updateScoreboardFont();
            }
        }

        private void updateScoreboardFont()
        {
            void update()
            {
                var font = MainForm.GetFontFromSettings(_scoreboardControl.Font, 12f);
                if (font != _scoreboardControl.Font)
                {
                    _scoreboardControl.Font = font;
                }
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void initHideLabel()
        {
            var ll = new LinkLabel() { Text = "(Hide)", AutoSize = true, Font = _adminInfoLabel.Font };
            _adminInfoLabel.Controls.Add(ll);
            ll.Location = new Point(_adminInfoLabel.Width - ll.Width, _adminInfoLabel.Height - ll.Height);
            ll.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ll.Click += (o, e) =>
            {
                _adminInfoLabel.Hide();
            };
        }

        private void listenToSettingsChanged()
        {
            Properties.Settings.Default.PropertyChanged += default_PropertyChanged;
        }

        private void lobbyControl_Load(object? sender, EventArgs e)
        {
            initHideLabel();
            updateBingoMaximumSize();
            updateBingoPanelSize();
            updateScoreboardFont();
            updateScoreboardControlLocationAndSize();
        }

        private void lobbyControl_SizeChanged(object? sender, EventArgs e)
        {
            updateBingoPanelSize();
        }

        private void bingoPanel_SizeChanged(object? sender, EventArgs e)
        {
            updateBingoPanelSize();
        }

        private void match_MatchStatusChanged(object? sender, EventArgs e)
        {
            if (Client?.Room != null)
            {
                updateMatchStatus(Client.Room.Match);
                restartAndListenToTimer();
            }
        }

        private void match_MatchTimerChanged(object? sender, EventArgs e)
        {
            if (Client?.Room != null)
            {
                setMatchTimerLabel(Client.Room.Match.TimerString);
            }
        }

        private void restartAndListenToTimer()
        {
            if (Client?.Room?.Match != null)
                setMatchTimerLabel(Client.Room.Match.TimerString);

            if (_timer != null)
            {
                _timer.Elapsed -= _timer_Elapsed;
                _timer.Stop();
            }

            _timer = new System.Timers.Timer();
            _timer.Interval = 50;
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
        }

        private void setMatchTimerLabel(string text)
        {
            void update()
            {
                _timerLabel.Text = text;
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void showHideAdminControls()
        {
            void showHide()
            {
                var isAdmin = Client?.LocalUser?.IsAdmin == true;
                // If we hosted the admin control in a right-side panel, keep the host visible
                // so the bottom area reserves its height, but only show the admin content
                // when the local user is an admin.
                if (_adminHostPanel != null)
                {
                    try
                    {
                        _adminHostPanel.Visible = true; // reserve the bottom area height
                        adminControl1.Visible = isAdmin; // show content only for admins
                        adminControl1.Enabled = isAdmin;
                        _adminHostPanel.BringToFront();
                        adminControl1.BringToFront();
                    }
                    catch { }
                }
                else
                {
                    adminControl1.Visible = isAdmin;
                    adminControl1.Height = isAdmin ? _adminHeight : 0;
                }
                _adminInfoLabel.Visible = isAdmin && Client?.LocalUser?.IsSpectator == true;
                updateBingoPanelSize();
            }
            if (InvokeRequired)
            {
                BeginInvoke(showHide);
                return;
            }
            showHide();
        }

        private void updateBingoMaximumSize()
        {
            if (Properties.Settings.Default.BingoBoardMaximumSize && Properties.Settings.Default.BingoMaxSizeX > 0 && Properties.Settings.Default.BingoMaxSizeY > 0)
            {
                _bingoControl.MaximumSize = new Size(Properties.Settings.Default.BingoMaxSizeX, Properties.Settings.Default.BingoMaxSizeY);
            }
            else
            {
                _bingoControl.MaximumSize = new Size();
            }
        }

        private void updateBingoPanelSize()
        {
            int statusPanelWidth = Convert.ToInt32(270f * this.DefaultScaleFactors().Width);
            var maxWidth = splitContainer1.Panel1.Width - statusPanelWidth;
            // Only subtract admin height from Panel1 if the admin control is still docked in Panel1
            var adminInLeftPanel = adminControl1.Parent == splitContainer1.Panel1;
            var maxHeight = splitContainer1.Panel1.Height - (adminInLeftPanel && adminControl1.Visible ? _adminHeight : 0);
            splitContainer1.SplitterWidth = 2;
            
            if (maxWidth < 120)
                maxWidth = 120;

            if (_battleshipControl.Visible)
            {
                // Battleship board is square, constrained by height.
                _bingoBoardPanel.Width = Math.Min(maxWidth, maxHeight + 10);
                updateBingoSize();
                return;
            }

            if (Properties.Settings.Default.BingoBoardMaximumSize)
            {
                maxWidth = Math.Min(maxWidth, Properties.Settings.Default.BingoMaxSizeX + _bingoControl.Location.X + 3);
                maxHeight = Math.Min(maxHeight, Properties.Settings.Default.BingoMaxSizeY + _bingoControl.Location.Y + 3);
            }
            if (maxWidth > maxHeight * 1.1f)
            {
                maxWidth = (int)(maxHeight * 1.1f);
            }
            else if (maxHeight > maxHeight / 1.1f)
            {
                maxHeight = (int)(maxHeight / 1.1f);
            }
            _bingoBoardPanel.Width = maxWidth;
            updateBingoSize();
        }

        private void updateBingoSize()
        {
            var maxSize = _bingoBoardPanel.Size - new Size(_bingoControl.Location) - new Size(3, 3);
            _bingoControl.Size = maxSize;
        }

        private void clearMatchLog()
        {
            void update()
            {
                _logTextBox.Clear();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void updateMatchLog(string text, Color color, bool timestamp)
        {
            updateMatchLog(new[] { text }, new Color?[] { color }, timestamp);
        }

        private void updateMatchLog(string[] text, Color?[] color, bool timestamp)
        {
            void update()
            {
                if (timestamp && Client?.Room?.Match != null)
                    appendText($"[{Client.Room.Match.TimerString}] ", Color.Gray);
                for (int i = 0; i < text.Length; i++)
                {
                    Color? col = i < color.Length ? color[i] : null;
                    appendText(text[i], col ?? _logTextBox.ForeColor);
                    if (i < text.Length - 1)
                        _logTextBox.AppendText(" ");
                }
                _logTextBox.AppendText(Environment.NewLine);
                _logTextBox.ScrollToCaret();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void updateMatchStatus(Match match)
        {
            void update()
            {
                _matchStatusLabel.Text = Match.MatchStatusToString(match.MatchStatus, match.Paused, out var color);
                _matchStatusLabel.ForeColor = color;
                if (_lastMatchStatus != match.MatchStatus || _lastPaused != match.Paused)
                {
                    updateMatchLog(new[] { "Match status changed to", Match.MatchStatusToString(match.MatchStatus, match.Paused, out var color2) }, new Color?[] { null, color2 }, true);
                }
                // Reset battleship/bingo visibility when match fully resets (not on Finished — keep battleship view up)
                if (match.MatchStatus == MatchStatus.NotRunning)
                {
                    _battleshipControl.Visible = false;
                    _bingoControl.Visible = true;
                    updateBattleshipSpectatorLegend();
                    updateBingoPanelSize();
                }
                _lastMatchStatus = match.MatchStatus;
                _lastPaused = match.Paused;
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private async void _chatTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                await sendChat();
            }
        }

        private async Task sendChat()
        {
            async Task send()
            {
                var text = _chatTextBox.Text;
                if (Client?.Room == null)
                    updateMatchLog("Not in a room", Color.Red, false);
                if (Client?.Room != null && !string.IsNullOrWhiteSpace(text))
                {
                    var message = new ClientChat(text);
                    await Client.SendPacketToServer(new Packet(message));
                }
                _chatTextBox.Clear();
            }
            if (InvokeRequired)
            {
                BeginInvoke(send, null);
                return;
            }
            await send();
        }

        private void _logTextBox_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            openUrl(e.LinkText);
        }

        private void openUrl(string? url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                updateMatchLog(ex.Message, Color.Red, false);
            }
        }
    }

    internal class TransparentOverlayPanel : Panel
    {
        public TransparentOverlayPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Color.Transparent;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                const int WS_EX_TRANSPARENT = 0x20;
                cp.ExStyle |= WS_EX_TRANSPARENT;
                return cp;
            }
        }

        // Make the overlay ignore mouse hit tests so clicks pass through to underlying controls
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST)
            {
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }
    }
}