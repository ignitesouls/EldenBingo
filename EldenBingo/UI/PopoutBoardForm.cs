using EldenBingoCommon;
using Neto.Shared;

namespace EldenBingo.UI
{
    internal class PopoutBoardForm : Form
    {
        private const int ResizeGripSize = 8;
        private const int BoardMarginX = 24;
        private const int BoardTop = 44;
        private const int BoardBottomPad = 104;

        private readonly BingoControl _bingoControl;
        private readonly ScoreboardControl _scoreboardControl;
        private readonly FlowLayoutPanel _playersPanel;
        private readonly Label _timerLabel;
        private readonly Label _statusLabel;
        private readonly Button _opacityToggleButton;
        private readonly Panel _boardFrame;
        private readonly Panel _toolbarPanel;
        private readonly Button _toolbarToggleButton;
        private readonly Button _transparentMarkedStyleButton;
        private readonly Dictionary<int, int> _teamScores;
        private readonly Dictionary<int, string> _teamNames;
        private readonly System.Windows.Forms.Timer _timer;
        private static readonly Color ChromaKey = Color.FromArgb(36, 31, 28);
        private static readonly Color BorderColor = Color.FromArgb(132, 124, 104);
        private static readonly Color TextColor = Color.FromArgb(248, 246, 242);

        private Client? _client;
        private bool _dragging;
        private bool _resizing;
        private bool _toolbarControlsVisible = true;
        private Point _dragStart;
        private Point _dragFormStart;
        private Point _resizeStart;
        private Size _resizeSizeStart;

        public PopoutBoardForm()
        {
            Text = "Bingo Board";
            Icon = Properties.Resources.icon;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            MinimumSize = new Size(260, 320);
            Size = initialFormSize();
            TopMost = true;
            BackColor = ChromaKey;
            TransparencyKey = ChromaKey;
            DoubleBuffered = true;
            _teamScores = new Dictionary<int, int>();
            _teamNames = new Dictionary<int, string>();

            _boardFrame = new Panel
            {
                BackColor = BorderColor,
                Padding = new Padding(1),
            };

            _bingoControl = new BingoControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                BoardBackgroundColor = ChromaKey,
            };
            _bingoControl.TransparentMarkedStyle = BingoControl.TransparentMarkedSquareStyle.Border;
            _boardFrame.Controls.Add(_bingoControl);

            _toolbarPanel = new FlowLayoutPanel
            {
                BackColor = ChromaKey,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 4, 4, 3),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
            };

            _opacityToggleButton = createToolButton("Transparent", "Toggle square backgrounds between solid and transparent");
            _opacityToggleButton.Width = 92;
            _opacityToggleButton.Click += opacityToggleButton_Click;

            _transparentMarkedStyleButton = createToolButton("Border", "Marked squares: colored border or filled background");
            _transparentMarkedStyleButton.Width = 62;
            _transparentMarkedStyleButton.Click += transparentMarkedStyleButton_Click;

            var moveButton = createToolButton("Move", "Move popout");
            moveButton.Width = 54;
            moveButton.Cursor = Cursors.SizeAll;
            enableDrag(moveButton);

            var resizeGrip = new ResizeGripControl
            {
                Dock = DockStyle.None,
                Size = new Size(64, 24),
                BackColor = Color.FromArgb(15, 31, 82),
                Cursor = Cursors.SizeNWSE,
                Margin = new Padding(0),
            };
            resizeGrip.MouseDown += resizeGrip_MouseDown;
            resizeGrip.MouseMove += resizeGrip_MouseMove;
            resizeGrip.MouseUp += resizeGrip_MouseUp;
            new ToolTip().SetToolTip(resizeGrip, "Drag to resize popout");

            var closeButton = createToolButton("Close", "Close popout");
            closeButton.Width = 56;
            closeButton.Click += (sender, args) => Close();

            _toolbarToggleButton = createToolButton("Hide", "Hide popout controls");
            _toolbarToggleButton.Width = 50;
            _toolbarToggleButton.Click += toolbarToggleButton_Click;

            _toolbarPanel.Controls.Add(_opacityToggleButton);
            _toolbarPanel.Controls.Add(_transparentMarkedStyleButton);
            _toolbarPanel.Controls.Add(moveButton);
            _toolbarPanel.Controls.Add(resizeGrip);
            _toolbarPanel.Controls.Add(closeButton);
            _toolbarPanel.Controls.Add(_toolbarToggleButton);
            enableDrag(_toolbarPanel);

            _playersPanel = new FlowLayoutPanel
            {
                Size = new Size(320, 58),
                BackColor = ChromaKey,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0),
                Margin = new Padding(0),
                Cursor = Cursors.SizeAll,
            };
            enableDrag(_playersPanel);

            _timerLabel = new ShadowLabel
            {
                Size = new Size(190, 58),
                Font = new Font("Segoe UI", 26F, FontStyle.Bold, GraphicsUnit.Point),
                Text = "00:00:00",
                ForeColor = TextColor,
                BackColor = ChromaKey,
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.SizeAll,
            };
            enableDrag(_timerLabel);

            _statusLabel = new ShadowLabel
            {
                Size = new Size(190, 24),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
                Text = "Not Running",
                ForeColor = Color.CadetBlue,
                BackColor = ChromaKey,
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.SizeAll,
            };
            enableDrag(_statusLabel);

            _scoreboardControl = new ScoreboardControl
            {
                Visible = false,
                Size = new Size(1, 1),
            };

            Controls.Add(_boardFrame);
            Controls.Add(_toolbarPanel);
            Controls.Add(_playersPanel);
            Controls.Add(_timerLabel);
            Controls.Add(_statusLabel);
            Controls.Add(_scoreboardControl);

            SizeChanged += popoutBoardForm_SizeChanged;
            layoutControls();

            _timer = new System.Windows.Forms.Timer { Interval = 50 };
            _timer.Tick += timer_Tick;
            _timer.Start();
        }

        private static Size initialFormSize()
        {
            if (!Properties.Settings.Default.PopoutBoardCustomSize ||
                Properties.Settings.Default.PopoutBoardSizeX <= 0 ||
                Properties.Settings.Default.PopoutBoardSizeY <= 0)
            {
                return new Size(760, 760);
            }

            var width = Properties.Settings.Default.PopoutBoardSizeX + BoardMarginX * 2;
            var height = Properties.Settings.Default.PopoutBoardSizeY + BoardTop + BoardBottomPad;
            return new Size(width, height);
        }
        public Client? Client
        {
            get => _client;
            set
            {
                if (_client == value)
                    return;

                if (_client != null)
                {
                    _client.OnRoomChanged -= client_RoomChanged;
                    _client.OnUsersChanged -= client_UsersChanged;
                    _client.RemoveListener<ServerUserChangedTeam>(userChangedTeam);
                    _client.RemoveListener<ServerScoreboardUpdate>(scoreboardUpdated);
                    _client.RemoveListener<ServerMatchStatusUpdate>(matchStatusUpdated);
                }

                _client = value;
                _bingoControl.Client = value;
                _scoreboardControl.Client = value;

                if (_client != null)
                {
                    _client.OnRoomChanged += client_RoomChanged;
                    _client.OnUsersChanged += client_UsersChanged;
                    _client.AddListener<ServerUserChangedTeam>(userChangedTeam);
                    _client.AddListener<ServerScoreboardUpdate>(scoreboardUpdated);
                    _client.AddListener<ServerMatchStatusUpdate>(matchStatusUpdated);
                }

                updateTimerLabel();
                updateStatusLabel();
                _teamScores.Clear();
                _teamNames.Clear();
                updatePlayersLabel();
            }
        }

        private Button createToolButton(string text, string tooltip)
        {
            var button = new Button
            {
                Dock = DockStyle.None,
                Width = 28,
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(15, 31, 82),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0),
                TabStop = false,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(104, 126, 210);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 70, 148);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 54, 124);
            new ToolTip().SetToolTip(button, tooltip);
            return button;
        }

        private void enableDrag(Control control)
        {
            control.MouseDown += drag_MouseDown;
            control.MouseMove += drag_MouseMove;
            control.MouseUp += drag_MouseUp;
        }

        private void drag_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _dragging = true;
            _dragStart = Cursor.Position;
            _dragFormStart = Location;
        }

        private void drag_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging)
                return;

            var delta = new Size(Cursor.Position.X - _dragStart.X, Cursor.Position.Y - _dragStart.Y);
            Location = _dragFormStart + delta;
        }

        private void drag_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = false;
            }
        }

        private void opacityToggleButton_Click(object? sender, EventArgs e)
        {
            var makeTransparent = _bingoControl.SquareBackgroundOpacity > 0;
            _bingoControl.SquareBackgroundOpacity = makeTransparent ? 0 : 100;
            _opacityToggleButton.Text = makeTransparent ? "Solid" : "Transparent";
        }

        private void transparentMarkedStyleButton_Click(object? sender, EventArgs e)
        {
            if (_bingoControl.TransparentMarkedStyle == BingoControl.TransparentMarkedSquareStyle.Border)
            {
                _bingoControl.TransparentMarkedStyle = BingoControl.TransparentMarkedSquareStyle.Filled;
                _transparentMarkedStyleButton.Text = "Fill";
            }
            else
            {
                _bingoControl.TransparentMarkedStyle = BingoControl.TransparentMarkedSquareStyle.Border;
                _transparentMarkedStyleButton.Text = "Border";
            }
        }
        private void toolbarToggleButton_Click(object? sender, EventArgs e)
        {
            _toolbarControlsVisible = !_toolbarControlsVisible;
            foreach (Control control in _toolbarPanel.Controls)
            {
                if (control != _toolbarToggleButton)
                    control.Visible = _toolbarControlsVisible;
            }

            _toolbarToggleButton.Text = _toolbarControlsVisible ? "Hide" : "Show";
            _toolbarToggleButton.Width = _toolbarControlsVisible ? 50 : 54;
            _toolbarPanel.PerformLayout();
            layoutControls();
        }

        private void resizeGrip_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _resizing = true;
            _resizeStart = Cursor.Position;
            _resizeSizeStart = Size;
        }

        private void resizeGrip_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_resizing)
                return;

            var deltaX = Cursor.Position.X - _resizeStart.X;
            var deltaY = Cursor.Position.Y - _resizeStart.Y;
            var amount = Math.Abs(deltaX) > Math.Abs(deltaY) ? deltaX : deltaY;
            setSizeFromTopLeft(new Size(_resizeSizeStart.Width + amount, _resizeSizeStart.Height + amount));
        }

        private void resizeGrip_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _resizing = false;
            }
        }

        private void setSizeFromTopLeft(Size requestedSize)
        {
            Size = clampSize(requestedSize);
        }

        private Size clampSize(Size requestedSize)
        {
            return new Size(
                Math.Max(MinimumSize.Width, requestedSize.Width),
                Math.Max(MinimumSize.Height, requestedSize.Height));
        }

        private void timer_Tick(object? sender, EventArgs e)
        {
            updateTimerLabel();
            updateStatusLabel();
        }

        private void updateTimerLabel()
        {
            _timerLabel.Text = _client?.Room?.Match.TimerString ?? "00:00:00";
        }

        private void updateStatusLabel()
        {
            if (_client?.Room?.Match == null)
            {
                _statusLabel.Text = "Not Running";
                _statusLabel.ForeColor = Color.CadetBlue;
                return;
            }

            _statusLabel.Text = Match.MatchStatusToString(_client.Room.Match.MatchStatus, _client.Room.Match.Paused, out var color);
            _statusLabel.ForeColor = color;
        }

        private void matchStatusUpdated(ClientModel? model, ServerMatchStatusUpdate update)
        {
            void refresh()
            {
                updateTimerLabel();
                updateStatusLabel();
            }

            if (InvokeRequired)
            {
                BeginInvoke(refresh);
                return;
            }
            refresh();
        }

        private void client_RoomChanged(object? sender, object? e)
        {
            updateStatusLabel();
            updatePlayersLabel();
        }

        private void client_UsersChanged(object? sender, EventArgs e)
        {
            updatePlayersLabel();
        }

        private void userChangedTeam(ClientModel? model, ServerUserChangedTeam teamChanged)
        {
            if (_client?.Room != null)
            {
                var user = _client.Room.GetUser(teamChanged.UserGuid);
                if (user != null)
                    user.Team = teamChanged.Team;
            }
            updatePlayersLabel();
        }

        private void scoreboardUpdated(ClientModel? model, ServerScoreboardUpdate update)
        {
            void refresh()
            {
                _teamScores.Clear();
                _teamNames.Clear();
                foreach (var score in update.Scoreboard)
                {
                    _teamScores[score.Team] = score.Score;
                    _teamNames[score.Team] = score.Name;
                }
                updatePlayersLabel();
            }

            if (InvokeRequired)
            {
                BeginInvoke(refresh);
                return;
            }
            refresh();
        }

        private void updatePlayersLabel()
        {
            void update()
            {
                var teams = _client?.Room?.Users
                    .Where(u => !u.IsSpectator && u.Team >= 0)
                    .GroupBy(u => u.Team)
                    .OrderBy(team => team.Key)
                    .Select(team =>
                    {
                        var teamId = team.Key;
                        var teamName = _teamNames.TryGetValue(teamId, out var name) && !string.IsNullOrWhiteSpace(name)
                            ? name
                            : BingoConstants.GetTeamName(teamId);
                        var score = _teamScores.TryGetValue(teamId, out var teamScore) ? teamScore : 0;
                        return (Name: teamName, Team: teamId, Score: score);
                    })
                    .ToArray() ?? Array.Empty<(string Name, int Team, int Score)>();

                _playersPanel.SuspendLayout();
                _playersPanel.Controls.Clear();
                foreach (var team in teams)
                {
                    _playersPanel.Controls.Add(createPlayerScoreControl(team.Name, team.Team, team.Score));
                }
                _playersPanel.Visible = teams.Length > 0;
                _playersPanel.ResumeLayout();
                layoutControls();
            }

            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private Control createPlayerScoreControl(string name, int team, int score)
        {
            var font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            var squareSize = ScoreboardControl.ScoreboardRowControl.CompactSquareSize(font);
            var control = new ScoreboardControl.ScoreboardRowControl
            {
                Font = font,
                Width = 160,
                Height = squareSize.Height,
                Margin = new Padding(0, 0, 12, 0),
                Cursor = Cursors.SizeAll,
                CompactLayout = true,
                Team = team,
                NameText = name,
                CounterText = score.ToString(),
                Color = BingoConstants.GetTeamColor(team),
                TextColor = BingoConstants.GetTeamColorBright(team),
                BackColor = ChromaKey,
            };
            enableDrag(control);
            return control;
        }
        private void updatePlayerScoreControlLayout()
        {
            var rows = _playersPanel.Controls.OfType<ScoreboardControl.ScoreboardRowControl>().ToArray();
            if (rows.Length == 0)
                return;

            var gap = 12;
            var availableWidth = Math.Max(0, _playersPanel.ClientSize.Width - gap * (rows.Length - 1));
            var rowWidth = Math.Max(44, availableWidth / rows.Length);

            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var fontSize = bestPlayerScoreFontSize(row.NameText, rowWidth);
                if (Math.Abs(row.Font.Size - fontSize) > 0.05F)
                    row.Font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point);

                var squareSize = ScoreboardControl.ScoreboardRowControl.CompactSquareSize(row.Font);
                row.Width = rowWidth;
                row.Height = squareSize.Height;
                row.Margin = new Padding(0, 0, i == rows.Length - 1 ? 0 : gap, 0);
            }
        }

        private float bestPlayerScoreFontSize(string name, int rowWidth)
        {
            const float maxFontSize = 11F;
            const float minFontSize = 4.5F;

            for (var size = maxFontSize; size >= minFontSize; size -= 0.25F)
            {
                using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Point);
                var squareSize = ScoreboardControl.ScoreboardRowControl.CompactSquareSize(font);
                var nameWidth = TextRenderer.MeasureText(name, font).Width;
                var nameMargin = Math.Max(2, Convert.ToInt32(size / 3F));
                var neededWidth = squareSize.Width + nameWidth + nameMargin;
                if (neededWidth <= rowWidth)
                    return size;
            }

            return minFontSize;
        }
        private void popoutBoardForm_SizeChanged(object? sender, EventArgs e)
        {
            layoutControls();
        }

        private void layoutControls()
        {
            var availableWidth = Math.Max(1, ClientSize.Width - BoardMarginX * 2);
            var availableHeight = Math.Max(1, ClientSize.Height - BoardTop - BoardBottomPad);
            var boardWidth = availableWidth;
            var boardHeight = Convert.ToInt32(boardWidth / BingoControl.AspectRatio);

            if (boardHeight > availableHeight)
            {
                boardHeight = availableHeight;
                boardWidth = Convert.ToInt32(boardHeight * BingoControl.AspectRatio);
            }

            var boardLeft = Math.Max(0, (ClientSize.Width - boardWidth) / 2);
            _boardFrame.Bounds = new Rectangle(boardLeft, BoardTop, boardWidth, boardHeight);
            _toolbarPanel.PerformLayout();
            var toolbarWidth = _toolbarPanel.GetPreferredSize(Size.Empty).Width;
            var toolbarX = Math.Max(0, _boardFrame.Right - toolbarWidth + 4);
            _toolbarPanel.Location = new Point(toolbarX, 8);

            _statusLabel.Location = new Point(Math.Max(0, _boardFrame.Right - _statusLabel.Width), Math.Max(0, ClientSize.Height - _statusLabel.Height - 6));
            var timerY = Math.Max(0, _statusLabel.Top - _timerLabel.Height + 6);
            _timerLabel.Location = new Point(Math.Max(0, _boardFrame.Right - _timerLabel.Width), timerY);
            var playersWidth = Math.Max(0, _timerLabel.Left - _boardFrame.Left - 12);
            _playersPanel.SetBounds(_boardFrame.Left, timerY, playersWidth, _timerLabel.Height);
            updatePlayerScoreControlLayout();

            _toolbarPanel.BringToFront();
            _playersPanel.BringToFront();
            _timerLabel.BringToFront();
            _statusLabel.BringToFront();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            var owner = MainForm.Instance;
            if (owner != null)
            {
                Location = new Point(owner.Left + Math.Max(40, (owner.Width - Width) / 2), owner.Top + Math.Max(40, (owner.Height - Height) / 2));
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_client != null)
            {
                _client.OnRoomChanged -= client_RoomChanged;
                _client.OnUsersChanged -= client_UsersChanged;
                _client.RemoveListener<ServerUserChangedTeam>(userChangedTeam);
                _client.RemoveListener<ServerScoreboardUpdate>(scoreboardUpdated);
                _client.RemoveListener<ServerMatchStatusUpdate>(matchStatusUpdated);
            }
            _timer.Stop();
            _timer.Dispose();
            _bingoControl.DisconnectClickHotkey();
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            const int wmNcHitTest = 0x0084;
            const int htLeft = 10;
            const int htRight = 11;
            const int htTop = 12;
            const int htTopLeft = 13;
            const int htTopRight = 14;
            const int htBottom = 15;
            const int htBottomLeft = 16;
            const int htBottomRight = 17;

            base.WndProc(ref m);

            if (m.Msg != wmNcHitTest || WindowState == FormWindowState.Maximized)
                return;

            var cursor = PointToClient(Cursor.Position);
            var left = cursor.X <= ResizeGripSize;
            var right = cursor.X >= ClientSize.Width - ResizeGripSize;
            var top = cursor.Y <= ResizeGripSize;
            var bottom = cursor.Y >= ClientSize.Height - ResizeGripSize;

            if (left && top)
                m.Result = (IntPtr)htTopLeft;
            else if (right && top)
                m.Result = (IntPtr)htTopRight;
            else if (left && bottom)
                m.Result = (IntPtr)htBottomLeft;
            else if (right && bottom)
                m.Result = (IntPtr)htBottomRight;
            else if (left)
                m.Result = (IntPtr)htLeft;
            else if (right)
                m.Result = (IntPtr)htRight;
            else if (top)
                m.Result = (IntPtr)htTop;
            else if (bottom)
                m.Result = (IntPtr)htBottom;
        }

        private class ResizeGripControl : Control
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using var borderPen = new Pen(Color.FromArgb(104, 126, 210));
                e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
                TextRenderer.DrawText(e, "Resize", Font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private class ShadowLabel : Label
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                var horizontal = TextAlign == ContentAlignment.MiddleRight ? TextFormatFlags.Right : TextFormatFlags.Left;
                var flags = horizontal | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                var rect = ClientRectangle;
                var shadowRect = new Rectangle(rect.X + 2, rect.Y + 2, rect.Width, rect.Height);
                TextRenderer.DrawText(e, Text, Font, shadowRect, Color.FromArgb(160, 0, 0, 0), flags);
                TextRenderer.DrawText(e, Text, Font, rect, ForeColor, flags);
            }
        }
    }
}