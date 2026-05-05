using EldenBingo.Net;
using EldenBingoCommon;
using Neto.Shared;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;

namespace EldenBingo.UI
{
    internal class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }
    }

    internal class BattleshipControl : ClientUserControl
    {
        private const int CellSize = 72;
        private const int GridPadding = 10;
        private const int SpectatorGridGap = 30;

        private static readonly Color BgColor = Color.FromArgb(18, 20, 20);
        private static readonly Color GridLineColor = Color.FromArgb(50, 50, 50);
        private static readonly Color GridBgColor = Color.FromArgb(20, 20, 25);
        private static readonly Color ShipColor = Color.FromArgb(100, 110, 120);
        private static readonly Color HitColor = Color.FromArgb(200, 40, 40);
        private static readonly Color MissColor = Color.FromArgb(80, 80, 120);
        private static readonly Color SunkColor = Color.FromArgb(160, 30, 30);
        private static readonly Color SelectedShipColor = Color.FromArgb(80, 200, 80);
        private static readonly Color InvalidPlacementColor = Color.FromArgb(220, 60, 60);
        private static readonly Color TextColor = Color.FromArgb(232, 230, 227);

        // Outgoing attack markers (your attacks on enemy grid)
        private static readonly Color OutgoingHitColor = Color.FromArgb(255, 160, 40);
        private static readonly Color OutgoingMissColor = Color.FromArgb(220, 220, 220);
        private static readonly Font MultiHitBadgeFont = new Font("Segoe UI", 8, FontStyle.Bold);

        // Goal text
        private static readonly Color GoalTextColor = Color.FromArgb(220, 240, 240, 240);
        private static readonly Color GoalTextShadowColor = Color.FromArgb(60, 0, 0, 0);
        private static readonly Color CheckedByMyTeamColor = Color.FromArgb(80, 40, 180, 40);
        private static readonly Color CheckedByEnemyColor = Color.FromArgb(80, 180, 40, 40);

        // State
        private int _boardSize;
        private ShipDefinition[] _shipDefs = Array.Empty<ShipDefinition>();
        private ShipPlacement[] _currentPlacements = Array.Empty<ShipPlacement>();
        private bool[] _placedShipGrid = Array.Empty<bool>();
        private int _selectedShipIndex = -1;
        private bool _selectedHorizontal = true;
        private int _hoverRow = -1;
        private int _hoverCol = -1;
        private bool _placementPhase = false;
        private bool _placementConfirmed = false;
        private int _selectedSquareIndex = -1;
        private int _lastNavigationSelection = 0;

        // Client-local marks: only visible to this player, never sent to server
        private readonly HashSet<int> _localMarkedCells = new();

        // Battleship grids received from server (keyed by team for spectator support)
        private readonly Dictionary<int, BattleshipTeamView> _teamViews = new();
        private readonly Dictionary<(int Attacker, int Index), List<(int Defender, AttackResult Result)>> _attackResultBuffer = new();
        private readonly object _attackBufferLock = new object();
        // Temporary overlays for outgoing attacks that hit multiple teams: index -> (count, expiry)
        private readonly Dictionary<int, (int Count, DateTime Expiry)> _multiHitOverlays = new();
        private readonly object _multiHitLock = new object();

        // Controls
        private Panel _gridPanel = null!;
        private Label _messageLabel = null!;
        private ListBox _shipListBox = null!;
        private FlowLayoutPanel _teamLegendPanel = null!;
        private List<int> _targetTeamList = new();
        private RadioButton _horizontalRadio = null!;
        private RadioButton _verticalRadio = null!;
        private Button _confirmButton = null!;
        private Label _shipPlacementLabel = null!;
        private ToolTip _cellToolTip = null!;
        // attacks now apply to all teams; no per-target selection

        /// <summary>Panel with ship list, orientation radios, and confirm button. Hosted externally by LobbyControl.</summary>
        public Panel PlacementPanel { get; private set; } = null!;

        public int BoardSize => _boardSize;

        /// <summary>The pixel width from the control's left edge to the grid's right edge, plus a small margin.</summary>
        public int NeededGridWidth => _boardSize > 0 ? _boardSize * EffectiveCellSize + 1 + GridPadding + 2 : Width;

        public BattleshipControl()
        {
            InitComponents();
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        private void LayoutPlacementPanel()
        {
            int w = PlacementPanel.Width;
            const int labelH = 22;
            const int listboxH = 90;
            const int radioH = 26;
            const int confirmH = 34;
            const int gap = 4;
            _shipPlacementLabel.Location = new Point(0, 0);
            _shipListBox.Location = new Point(0, labelH);
            _shipListBox.Height = listboxH;
            int radiosTop = labelH + listboxH + gap;
            _horizontalRadio.Location = new Point(0, radiosTop);
            _verticalRadio.Location = new Point(w / 2, radiosTop);
            _confirmButton.Location = new Point(0, radiosTop + radioH + gap);
            _confirmButton.Height = confirmH;
        }

        private void InitComponents()
        {
            _gridPanel = new DoubleBufferedPanel
            {
                BackColor = Color.Transparent,
                Location = new Point(GridPadding, 5),
                BorderStyle = BorderStyle.FixedSingle,
            };
            _gridPanel.Paint += GridPanel_Paint;
            _gridPanel.MouseMove += GridPanel_MouseMove;
            _gridPanel.MouseLeave += GridPanel_MouseLeave;
            _gridPanel.MouseClick += GridPanel_MouseClick;

            _messageLabel = new Label
            {
                ForeColor = TextColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 28,
            };

            // Ship placement controls
            _shipPlacementLabel = new Label
            {
                Text = "Place your ships:",
                ForeColor = TextColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = true,
            };

            _shipListBox = new ListBox
            {
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 9),
                Height = 110,
                Width = 180,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            };
            _shipListBox.SelectedIndexChanged += ShipListBox_SelectedIndexChanged;

            _horizontalRadio = new RadioButton
            {
                Text = "Horizontal",
                ForeColor = TextColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9),
                Checked = true,
                AutoSize = true,
            };
            _horizontalRadio.CheckedChanged += (s, e) =>
            {
                if (_horizontalRadio.Checked)
                {
                    _selectedHorizontal = true;
                    UpdateShipListLabels();
                    _gridPanel.Invalidate();
                }
            };

            _verticalRadio = new RadioButton
            {
                Text = "Vertical",
                ForeColor = TextColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9),
                AutoSize = true,
            };
            _verticalRadio.CheckedChanged += (s, e) =>
            {
                if (_verticalRadio.Checked)
                {
                    _selectedHorizontal = false;
                    UpdateShipListLabels();
                    _gridPanel.Invalidate();
                }
            };

            _confirmButton = new Button
            {
                Text = "Confirm Placement",
                ForeColor = TextColor,
                BackColor = Color.FromArgb(30, 100, 30),
                FlatStyle = FlatStyle.Flat,
                Width = 180,
                Height = 32,
                Enabled = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            };
            _confirmButton.Click += ConfirmButton_Click;

            PlacementPanel = new Panel
            {
                BackColor = Color.Transparent,
                Visible = false,
                Width = 190,
            };
            PlacementPanel.Controls.Add(_shipPlacementLabel);
            PlacementPanel.Controls.Add(_shipListBox);
            PlacementPanel.Controls.Add(_horizontalRadio);
            PlacementPanel.Controls.Add(_verticalRadio);
            PlacementPanel.Controls.Add(_confirmButton);
            PlacementPanel.Height = 200;
            PlacementPanel.SizeChanged += (s, e) => LayoutPlacementPanel();
            LayoutPlacementPanel();

            _cellToolTip = new ToolTip
            {
                InitialDelay = 200,
                ReshowDelay = 100,
                AutoPopDelay = 10000,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = TextColor,
            };

            // Team legend panel (non-interactive)
            _teamLegendPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0),
            };
            // Ensure the message label is part of the control so match status/clock messages are visible
            Controls.Add(_messageLabel);
            Controls.Add(_teamLegendPanel);

            Controls.Add(_gridPanel);

            SizeChanged += (s, e) => LayoutPanels();

            KeyDown += BattleshipControl_KeyDown;
            Load += (s, e) => SetupRawInput();
        }

        private void SetupRawInput()
        {
            var mainForm = MainForm.GetMainForm(this);
            if (mainForm != null)
                mainForm.RawInput.KeyPressed += RawInput_KeyPressed;
        }

        private bool IsBattleshipActive =>
            Visible && _boardSize > 0 && Client?.Room != null;

        private void RawInput_KeyPressed(object? sender, KeyEventArgs e)
        {
            if (!IsBattleshipActive)
                return;

            // Click hotkey - check square
            var key = Properties.Settings.Default.ClickHotkey;
            if (key != 0 && e.KeyValue == key)
            {
                // Use keyboard navigation position (independent of mouse hover)
                int targetIndex = _lastNavigationSelection;
                if (targetIndex >= 0 && targetIndex < _boardSize * _boardSize)
                {
                    if (_placementPhase && !_placementConfirmed && _selectedShipIndex >= 0)
                    {
                        int row = targetIndex / _boardSize;
                        int col = targetIndex % _boardSize;
                        if (CanPlaceShip(_selectedShipIndex, row, col, _selectedHorizontal))
                        {
                            PlaceShip(_selectedShipIndex, row, col, _selectedHorizontal);
                            AdvanceToNextShip();
                            _gridPanel.Invalidate();
                        }
                    }
                    else if (!_placementPhase)
                    {
                        _ = TryCheckSquare(targetIndex);
                    }
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            // Navigation
            if (!(Properties.Settings.Default.NumpadNavigation || Properties.Settings.Default.ArrowNavigation))
                return;

            int size = _boardSize;
            bool handled = false;
            bool dontMoveCursor = false;
            int selected = _selectedSquareIndex;
            if (selected < 0)
            {
                dontMoveCursor = true;
                selected = _lastNavigationSelection;
            }
            int row2 = selected / size;
            int col2 = selected % size;

            void moveLeft() { col2 = Math.Clamp(col2 - 1, 0, size - 1); handled = true; }
            void moveRight() { col2 = Math.Clamp(col2 + 1, 0, size - 1); handled = true; }
            void moveUp() { row2 = Math.Clamp(row2 - 1, 0, size - 1); handled = true; }
            void moveDown() { row2 = Math.Clamp(row2 + 1, 0, size - 1); handled = true; }

            if (Properties.Settings.Default.NumpadNavigation)
            {
                switch (e.KeyCode)
                {
                    case Keys.NumPad4: moveLeft(); break;
                    case Keys.NumPad6: moveRight(); break;
                    case Keys.NumPad8: moveUp(); break;
                    case Keys.NumPad2: moveDown(); break;
                    case Keys.NumPad7: moveLeft(); moveUp(); break;
                    case Keys.NumPad9: moveRight(); moveUp(); break;
                    case Keys.NumPad1: moveLeft(); moveDown(); break;
                    case Keys.NumPad3: moveRight(); moveDown(); break;
                }
            }
            if (Properties.Settings.Default.ArrowNavigation)
            {
                switch (e.KeyCode)
                {
                    case Keys.Left: moveLeft(); break;
                    case Keys.Right: moveRight(); break;
                    case Keys.Up: moveUp(); break;
                    case Keys.Down: moveDown(); break;
                }
            }
            if (handled)
            {
                if (dontMoveCursor)
                {
                    row2 = selected / size;
                    col2 = selected % size;
                }
                int newIndex = row2 * size + col2;
                _lastNavigationSelection = newIndex;
                SetSelectedSquare(newIndex);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void SetSelectedSquare(int newIndex)
        {
            if (_selectedSquareIndex == newIndex) return;
            _selectedSquareIndex = newIndex;
            // Update hover row/col to match
            if (newIndex >= 0 && newIndex < _boardSize * _boardSize)
            {
                _hoverRow = newIndex / _boardSize;
                _hoverCol = newIndex % _boardSize;
            }
            _gridPanel.Invalidate();
        }

        private void AdvanceToNextShip()
        {
            int next = -1;
            for (int i = 0; i < _currentPlacements.Length; i++)
            {
                if (_currentPlacements[i].StartRow < 0)
                {
                    next = i;
                    break;
                }
            }
            if (next >= 0)
                _shipListBox.SelectedIndex = next;
            else
            {
                _confirmButton.Enabled = true;
                UpdateMessage("All ships placed! Click Confirm when ready.", Color.Lime);
            }
        }

        private void BattleshipControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.R && _placementPhase && !_placementConfirmed)
            {
                ToggleOrientation();
                e.Handled = true;
            }
        }

        private void ToggleOrientation()
        {
            if (_selectedHorizontal)
                _verticalRadio.Checked = true;
            else
                _horizontalRadio.Checked = true;
        }

        private bool IsSpectator => Client?.LocalUser?.IsSpectator == true;

        private int EffectiveCellSize
        {
            get
            {
                if (_boardSize <= 0) return CellSize;
                int availableHeight = Height - 10; // 5 top + 5 bottom padding
                int availableWidth = Width - GridPadding; // only left padding; grid is left-aligned

                int fromHeight = availableHeight / _boardSize;
                int fromWidth = availableWidth / _boardSize;
                int dynamicSize = Math.Min(fromHeight, fromWidth);
                return Math.Max(32, dynamicSize);
            }
        }

        private void LayoutPanels()
        {
            if (_boardSize <= 0)
                return;

            int cellSz = EffectiveCellSize;
            int gridPixelSize = _boardSize * cellSz + 1;
            _gridPanel.Size = new Size(gridPixelSize, gridPixelSize);
            _gridPanel.Location = new Point(GridPadding, 5);

            // Position message label and team legend to the right of the grid
            if (_messageLabel != null)
            {
                _messageLabel.Location = new Point(_gridPanel.Right + 8, _gridPanel.Top + 2);
                _messageLabel.Width = 160;
            }
            if (_teamLegendPanel != null)
            {
                int legendY = _messageLabel != null ? _messageLabel.Bottom + 6 : _gridPanel.Top + 2;
                _teamLegendPanel.Location = new Point(_gridPanel.Right + 8, legendY);
            }
        }

        public void SetBattleshipConfig(ShipDefinition[] ships, int boardSize)
        {
            _boardSize = boardSize;
            _shipDefs = ships;
            _currentPlacements = new ShipPlacement[ships.Length];
            _placedShipGrid = new bool[boardSize * boardSize];
            _teamViews.Clear();

            for (int i = 0; i < ships.Length; i++)
            {
                _currentPlacements[i] = new ShipPlacement(i, -1, -1, true);
            }

            _horizontalRadio.Checked = true;
            UpdateShipListLabels();

            LayoutPanels();
            _gridPanel.Invalidate();
        }

        private void UpdateShipListLabels()
        {
            int selectedIdx = _shipListBox.SelectedIndex;
            _shipListBox.Items.Clear();
            string orient = _selectedHorizontal ? "H" : "V";
            foreach (var ship in _shipDefs)
            {
                _shipListBox.Items.Add($"{ship.Name} ({ship.Size}) \u2014 {orient}");
            }
            if (selectedIdx >= 0 && selectedIdx < _shipListBox.Items.Count)
                _shipListBox.SelectedIndex = selectedIdx;
        }

        public void EnterPlacementPhase()
        {
            _placementPhase = true;
            _placementConfirmed = false;
            _confirmButton.Enabled = false;
            _confirmButton.Text = "Confirm Placement";
            PlacementPanel.Visible = true;
            _horizontalRadio.Checked = true;
            if (_shipListBox.Items.Count > 0)
                _shipListBox.SelectedIndex = 0;
            UpdateMessage("Place your ships on the grid, then confirm.", Color.Cyan);
            LayoutPanels();
        }

        private void UpdateTargetTeamList()
        {
            _targetTeamList.Clear();
            _teamLegendPanel.Controls.Clear();
            if (Client?.Room == null)
            {
                return;
            }
            var myTeam = Client.LocalUser?.Team ?? -1;
            var opponents = Client.Room.Users.Where(u => u.Team >= 0 && u.Team != myTeam).Select(u => u.Team).Distinct().OrderBy(t => t).ToList();
            foreach (var t in opponents)
            {
                _targetTeamList.Add(t);
                var lbl = new Label
                {
                    AutoSize = true,
                    Text = BingoConstants.GetTeamName(t),
                    ForeColor = BingoConstants.GetTeamColorBright(t),
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                };
                _teamLegendPanel.Controls.Add(lbl);
            }
        }

        public void ExitPlacementPhase()
        {
            _placementPhase = false;
            PlacementPanel.Visible = false;
            LayoutPanels();
            _gridPanel.Invalidate();
        }

        public void UpdateTeamView(BattleshipTeamView view)
        {
            _teamViews[view.Team] = view;
            _gridPanel.Invalidate();
        }

        public void ShowAttackResult(int index, AttackResult result, int attackingTeam, int defendingTeam)
        {
            // Buffer per-(attacker,index) and aggregate shortly to avoid rapid overwrites
            var key = (Attacker: attackingTeam, Index: index);
            lock (_attackBufferLock)
            {
                if (!_attackResultBuffer.TryGetValue(key, out var list))
                {
                    list = new List<(int Defender, AttackResult Result)>();
                    _attackResultBuffer[key] = list;
                }
                list.Add((defendingTeam, result));
            }

            // Schedule a short delay flush when first added
            _ = Task.Run(async () =>
            {
                await Task.Delay(140).ConfigureAwait(false);
                List<(int Defender, AttackResult Result)> toShow;
                lock (_attackBufferLock)
                {
                    if (!_attackResultBuffer.TryGetValue(key, out var l))
                        return;
                    toShow = new List<(int Defender, AttackResult Result)>(l);
                    _attackResultBuffer.Remove(key);
                }

                // Build deduplicated defender/result map (keep first result per defender)
                var dedup = new Dictionary<int, AttackResult>();
                foreach (var e in toShow)
                {
                    if (!dedup.ContainsKey(e.Defender))
                        dedup[e.Defender] = e.Result;
                }

                // Determine overall color / sound severity
                var anySunk = dedup.Values.Any(v => v == AttackResult.Sunk);
                var anyHit = dedup.Values.Any(v => v == AttackResult.Hit);
                Color resultColor = anySunk ? Color.Red : (anyHit ? Color.Orange : Color.LightBlue);

                // Build message text
                var attackerName = BingoConstants.GetTeamName(attackingTeam);
                var parts = dedup.Select(kv => $"{BingoConstants.GetTeamName(kv.Key)}: {(kv.Value == AttackResult.Sunk ? "SUNK" : kv.Value == AttackResult.Hit ? "Hit" : "Miss")}").ToArray();
                var msg = parts.Length == 1
                    ? $"{attackerName} attacks {parts[0]} at cell {index}" 
                    : $"{attackerName} attacks cell {index}: {string.Join(", ", parts)}";

                // Show message on UI thread (UpdateMessage marshals to UI thread)
                UpdateMessage(msg, resultColor);

                // Play sound once based on highest severity
                if (Properties.Settings.Default.PlaySounds)
                {
                    Sfx.SoundType? soundType = anySunk ? Sfx.SoundType.BattleshipSunk : anyHit ? Sfx.SoundType.BattleshipHit : Sfx.SoundType.BattleshipMiss;
                    if (soundType.HasValue)
                    {
                        try
                        {
                            if (MainForm.Instance != null)
                            {
                                if (MainForm.Instance.InvokeRequired)
                                    MainForm.Instance.BeginInvoke(new Action(() => MainForm.Instance.SoundPlayer?.PlaySound(soundType.Value, 15)));
                                else
                                    MainForm.Instance.SoundPlayer?.PlaySound(soundType.Value, 15);
                            }
                        }
                        catch { }
                    }
                }

                // If this client is the attacker and multiple defenders were hit, show a short badge overlay on the attacked cell.
                try
                {
                    var myTeam = Client?.LocalUser?.Team ?? -1;
                    var hitCount = dedup.Values.Count(v => v != AttackResult.Miss);
                    if (attackingTeam == myTeam && hitCount > 1)
                    {
                        var expiry = DateTime.UtcNow.AddSeconds(2);
                        if (InvokeRequired)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                lock (_multiHitLock)
                                {
                                    _multiHitOverlays[index] = (hitCount, expiry);
                                }
                                _gridPanel.Invalidate();
                            }));
                        }
                        else
                        {
                            lock (_multiHitLock)
                            {
                                _multiHitOverlays[index] = (hitCount, expiry);
                            }
                            _gridPanel.Invalidate();
                        }
                    }
                }
                catch { }
            });
        }

        public void ShowGameOver(int winningTeam, string winningTeamName)
        {
            UpdateMessage($"GAME OVER! {winningTeamName} wins!", Color.Gold);
        }

        public void UpdateMessage(string msg, Color color)
        {
            void update()
            {
                _messageLabel.Text = msg;
                _messageLabel.ForeColor = color;
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void ShipListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _selectedShipIndex = _shipListBox.SelectedIndex;
            _gridPanel.Invalidate();
        }

        private async void ConfirmButton_Click(object? sender, EventArgs e)
        {
            if (Client == null || _placementConfirmed)
                return;

            for (int i = 0; i < _currentPlacements.Length; i++)
            {
                if (_currentPlacements[i].StartRow < 0)
                {
                    UpdateMessage($"Place all ships before confirming!", Color.Red);
                    return;
                }
            }

            _confirmButton.Enabled = false;

            await Client.PlaceShips(_currentPlacements);

            await Task.Delay(200);

            await Client.ConfirmShipPlacement();
            _placementConfirmed = true;
            _confirmButton.Text = "Confirmed!";
            UpdateMessage("Waiting for opponent to place ships...", Color.Yellow);
        }

        private void GridPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            int cellSz = EffectiveCellSize;
            int row = e.Y / cellSz;
            int col = e.X / cellSz;
            if (row != _hoverRow || col != _hoverCol)
            {
                _hoverRow = row;
                _hoverCol = col;
                _gridPanel.Invalidate();

                // Update tooltip with goal text
                UpdateCellTooltip(row, col);
            }
        }

        private void GridPanel_MouseLeave(object? sender, EventArgs e)
        {
            _hoverRow = -1;
            _hoverCol = -1;
            _gridPanel.Invalidate();
            _cellToolTip.Hide(_gridPanel);
        }

        private void UpdateCellTooltip(int row, int col)
        {
            if (row < 0 || col < 0 || row >= _boardSize || col >= _boardSize)
            {
                _cellToolTip.Hide(_gridPanel);
                return;
            }

            var board = Client?.BingoBoard;
            if (board == null)
            {
                _cellToolTip.Hide(_gridPanel);
                return;
            }

            int index = row * _boardSize + col;
            if (index >= board.SquareCount)
            {
                _cellToolTip.Hide(_gridPanel);
                return;
            }

            var square = board.Squares[index];
            string tooltip = square.Tooltip ?? "";
            string display = string.IsNullOrWhiteSpace(tooltip) ? (square.Text ?? "") : tooltip;

            if (string.IsNullOrWhiteSpace(display))
            {
                _cellToolTip.Hide(_gridPanel);
                return;
            }

            int cellSz = EffectiveCellSize;
            _cellToolTip.Show(display, _gridPanel, col * cellSz + cellSz, row * cellSz);
        }

        private void GridPanel_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Prevent any spectator from interacting with the grid
                if (IsSpectator)
                    return;
                // During placement phase: place ships
                if (_placementPhase && !_placementConfirmed && _selectedShipIndex >= 0 && _selectedShipIndex < _shipDefs.Length)
                {
                    int cellSz = EffectiveCellSize;
                    int row = e.Y / cellSz;
                    int col = e.X / cellSz;

                    if (CanPlaceShip(_selectedShipIndex, row, col, _selectedHorizontal))
                    {
                        PlaceShip(_selectedShipIndex, row, col, _selectedHorizontal);
                        AdvanceToNextShip();
                        _gridPanel.Invalidate();
                    }
                    return;
                }

                // During running phase: click to complete a goal (triggers battleship attack)
                if (!_placementPhase)
                {
                    int cellSz = EffectiveCellSize;
                    int row = e.Y / cellSz;
                    int col = e.X / cellSz;
                    if (row >= 0 && row < _boardSize && col >= 0 && col < _boardSize)
                    {
                        int index = row * _boardSize + col;
                        _ = TryCheckSquare(index);
                    }
                }
            }
            else if (e.Button == MouseButtons.Right && !_placementPhase)
            {
                int cellSz = EffectiveCellSize;
                int row = e.Y / cellSz;
                int col = e.X / cellSz;
                if (row >= 0 && row < _boardSize && col >= 0 && col < _boardSize)
                {
                    int index = row * _boardSize + col;
                    ToggleLocalMark(index);
                }
            }
        }

        private bool CanPlaceShip(int shipIndex, int startRow, int startCol, bool horizontal)
        {
            if (shipIndex < 0 || shipIndex >= _shipDefs.Length)
                return false;

            var size = _shipDefs[shipIndex].Size;
            int dr = horizontal ? 0 : 1;
            int dc = horizontal ? 1 : 0;

            for (int i = 0; i < size; i++)
            {
                int r = startRow + dr * i;
                int c = startCol + dc * i;
                if (r < 0 || r >= _boardSize || c < 0 || c >= _boardSize)
                    return false;

                int idx = r * _boardSize + c;
                if (_placedShipGrid[idx])
                {
                    bool occupiedByOther = false;
                    for (int s = 0; s < _currentPlacements.Length; s++)
                    {
                        if (s == shipIndex || _currentPlacements[s].StartRow < 0)
                            continue;
                        if (IsShipAtCell(s, r, c))
                        {
                            occupiedByOther = true;
                            break;
                        }
                    }
                    if (occupiedByOther)
                        return false;
                }
            }
            return true;
        }

        private bool IsShipAtCell(int shipIndex, int row, int col)
        {
            if (shipIndex < 0 || shipIndex >= _currentPlacements.Length || _currentPlacements[shipIndex].StartRow < 0)
                return false;
            var p = _currentPlacements[shipIndex];
            var size = _shipDefs[shipIndex].Size;
            int dr = p.IsHorizontal ? 0 : 1;
            int dc = p.IsHorizontal ? 1 : 0;
            for (int i = 0; i < size; i++)
            {
                if (p.StartRow + dr * i == row && p.StartCol + dc * i == col)
                    return true;
            }
            return false;
        }

        private void PlaceShip(int shipIndex, int startRow, int startCol, bool horizontal)
        {
            if (_currentPlacements[shipIndex].StartRow >= 0)
            {
                ClearShipFromGrid(shipIndex);
            }

            _currentPlacements[shipIndex] = new ShipPlacement(shipIndex, startRow, startCol, horizontal);

            var size = _shipDefs[shipIndex].Size;
            int dr = horizontal ? 0 : 1;
            int dc = horizontal ? 1 : 0;
            for (int i = 0; i < size; i++)
            {
                int r = startRow + dr * i;
                int c = startCol + dc * i;
                _placedShipGrid[r * _boardSize + c] = true;
            }
        }

        private void ClearShipFromGrid(int shipIndex)
        {
            var p = _currentPlacements[shipIndex];
            if (p.StartRow < 0)
                return;
            var size = _shipDefs[shipIndex].Size;
            int dr = p.IsHorizontal ? 0 : 1;
            int dc = p.IsHorizontal ? 1 : 0;
            for (int i = 0; i < size; i++)
            {
                int r = p.StartRow + dr * i;
                int c = p.StartCol + dc * i;
                _placedShipGrid[r * _boardSize + c] = false;
            }
        }

        private async Task TryCheckSquare(int index)
        {
            if (Client == null)
                return;

            var userToSetFor = LobbyControl.CurrentlyOnBehalfOfUser;
            if (userToSetFor == null)
                return;

            // Send battleship attack packet. Target team is ignored server-side; send -1 for clarity.
            var attackPacket = new Packet(new ClientBattleshipAttack(index, -1, userToSetFor.Guid));
            await Client.SendPacketToServer(attackPacket);
        }

        private void ToggleLocalMark(int index)
        {
            if (!_localMarkedCells.Remove(index))
                _localMarkedCells.Add(index);
            _gridPanel.Invalidate();
        }

        #region Painting

        private void GridPanel_Paint(object? sender, PaintEventArgs e)
        {
            if (_boardSize <= 0)
                return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (IsSpectator)
            {
                // Spectator: draw both teams side by side
                DrawSpectatorView(g);
            }
            else
            {
                int cellSz = EffectiveCellSize;
                // No solid background fill here; parent/backing color should show through

                // During placement phase, draw placed ships and preview
                if (_placementPhase)
                {
                    DrawPlacedShips(g);
                    DrawShipPreview(g);
                }

                // Player: draw own combined view
                var myTeam = Client?.LocalUser?.Team ?? 0;
                if (_teamViews.TryGetValue(myTeam, out var myView))
                {
                    DrawCombinedGrid(g, myView, 0);
                }

                DrawGoalText(g, 0);
                DrawMarkedCells(g);
                DrawGridLines(g, 0);

                // Hover highlight
                if (_hoverRow >= 0 && _hoverRow < _boardSize && _hoverCol >= 0 && _hoverCol < _boardSize)
                {
                    using var hoverPen = new Pen(Color.FromArgb(180, 255, 255, 255), 2f);
                    var hoverRect = new Rectangle(_hoverCol * cellSz + 1, _hoverRow * cellSz + 1, cellSz - 2, cellSz - 2);
                    g.DrawRectangle(hoverPen, hoverRect);
                }
            }
        }

        private void DrawSpectatorView(Graphics g)
        {
            int cellSz = EffectiveCellSize;
            int gridPixelSize = _boardSize * cellSz;
            var sortedTeams = _teamViews.Keys.OrderBy(t => t).ToList();

            // No solid background fill here; parent/backing color should show through

            // Collect team colors and ship data
            var teamColors = new Color[sortedTeams.Count];
            var teamViews = new BattleshipTeamView[sortedTeams.Count];
            for (int t = 0; t < sortedTeams.Count; t++)
            {
                teamColors[t] = BingoConstants.GetTeamColorBright(sortedTeams[t]);
                teamViews[t] = _teamViews[sortedTeams[t]];
            }

            // Draw ships - support any number of teams occupying the same cell.
            // For a single team, draw a solid tint. For multiple teams, draw pie slices like the bingo board.
            for (int i = 0; i < _boardSize * _boardSize; i++)
            {
                int r = i / _boardSize;
                int c = i % _boardSize;
                var rect = new Rectangle(c * cellSz + 1, r * cellSz + 1, cellSz - 2, cellSz - 2);

                // Collect present teams (indexes into sortedTeams/teamViews)
                var present = new List<int>();
                for (int t = 0; t < sortedTeams.Count; t++)
                {
                    if (teamViews[t].ShipCells[i])
                        present.Add(t);
                }

                if (present.Count == 0)
                    continue;

                if (present.Count == 1)
                {
                    using var brush = new SolidBrush(Color.FromArgb(60, teamColors[present[0]]));
                    g.FillRectangle(brush, rect);
                }
                else
                {
                    // Use the same pie-slice approach as BingoSquareControl, but clip to the cell
                    var angleAdd = 360f / present.Count;
                    var angleStart = present.Count % 2 == 0 ? 270f - (present.Count == 2 ? 45f : angleAdd / 2f) : 270f;
                    var bigRect = new Rectangle(rect.X - rect.Width, rect.Y - rect.Height, rect.Width * 3, rect.Height * 3);

                    // Save graphics state and clip to the cell rect so the pie doesn't bleed into neighbors
                    var gs = g.Save();
                    try
                    {
                        g.SetClip(rect);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        for (int p = 0; p < present.Count; p++)
                        {
                            int teamIdx = present[p];
                            using var brush = new SolidBrush(Color.FromArgb(70, teamColors[teamIdx]));
                            g.FillPie(brush, bigRect, angleStart + angleAdd * p, angleAdd);
                        }

                        // Draw separator lines from center to pie edges for clarity
                        using var sepPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f);
                        var cx = rect.Left + rect.Width / 2f;
                        var cy = rect.Top + rect.Height / 2f;
                        var radius = Math.Max(rect.Width, rect.Height) * 0.6f;
                        for (int p = 0; p < present.Count; p++)
                        {
                            var angle = (angleStart + angleAdd * p) * (Math.PI / 180.0);
                            var px = cx + radius * Math.Cos(angle);
                            var py = cy + radius * Math.Sin(angle);
                            g.DrawLine(sepPen, cx, cy, (float)px, (float)py);
                        }
                    }
                    finally
                    {
                        g.Restore(gs);
                    }
                }
            }

            // Draw sunk ship outlines and attacks per team
            for (int t = 0; t < sortedTeams.Count; t++)
            {
                var view = teamViews[t];
                var teamColor = teamColors[t];

                // Draw sunk ship outlines in team color
                using var sunkPen = new Pen(teamColor, 2.5f);
                foreach (var sunk in view.SunkShips)
                {
                    int dr = sunk.IsHorizontal ? 0 : 1;
                    int dc = sunk.IsHorizontal ? 1 : 0;
                    for (int j = 0; j < sunk.Size; j++)
                    {
                        int sr = sunk.StartRow + dr * j;
                        int sc = sunk.StartCol + dc * j;
                        var rect = new Rectangle(sc * cellSz + 3, sr * cellSz + 3, cellSz - 6, cellSz - 6);
                        g.DrawRectangle(sunkPen, rect);
                    }
                }

                // Draw attacks from this team (what they shot at their opponent)
                // Use circle for team 0, square for team 1 to differentiate
                for (int i = 0; i < _boardSize * _boardSize; i++)
                {
                    int r = i / _boardSize;
                    int c = i % _boardSize;
                    var rect = new Rectangle(c * cellSz + 1, r * cellSz + 1, cellSz - 2, cellSz - 2);
                    int margin = cellSz / 6;

                    if (view.AttackGrid[i] == CellState.Hit)
                    {
                        using var hitPen = new Pen(teamColor, 2.5f);
                        if (t == 0)
                            g.DrawEllipse(hitPen, rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2);
                        else
                            g.DrawRectangle(hitPen, rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2);
                    }
                    else if (view.AttackGrid[i] == CellState.Miss)
                    {
                        using var missBrush = new SolidBrush(Color.FromArgb(120, teamColor));
                        int dotSize = cellSz / 7;
                        int cx = rect.Left + rect.Width / 2 - dotSize / 2;
                        if (t == 0)
                        {
                            // Team 0: dot in upper third
                            int cy = rect.Top + rect.Height / 4 - dotSize / 2;
                            g.FillEllipse(missBrush, cx, cy, dotSize, dotSize);
                        }
                        else
                        {
                            // Team 1: square in lower third
                            int cy = rect.Top + rect.Height * 3 / 4 - dotSize / 2;
                            g.FillRectangle(missBrush, cx, cy, dotSize, dotSize);
                        }
                    }
                }
            }

            DrawGoalText(g, 0, 0);
            DrawMarkedCells(g);
            DrawGridLines(g, 0, 0);
        }

        private void DrawGridLines(Graphics g, int offsetX = 0, int offsetY = 0)
        {
            using var pen = new Pen(GridLineColor);
            int cellSz = EffectiveCellSize;
            for (int i = 0; i <= _boardSize; i++)
            {
                g.DrawLine(pen, offsetX + i * cellSz, offsetY, offsetX + i * cellSz, offsetY + _boardSize * cellSz);
                g.DrawLine(pen, offsetX, offsetY + i * cellSz, offsetX + _boardSize * cellSz, offsetY + i * cellSz);
            }
        }

        private void DrawPlacedShips(Graphics g)
        {
            using var shipBrush = new SolidBrush(ShipColor);
            int cellSz = EffectiveCellSize;
            for (int s = 0; s < _currentPlacements.Length; s++)
            {
                var p = _currentPlacements[s];
                if (p.StartRow < 0)
                    continue;

                var size = _shipDefs[s].Size;
                int dr = p.IsHorizontal ? 0 : 1;
                int dc = p.IsHorizontal ? 1 : 0;
                for (int i = 0; i < size; i++)
                {
                    int r = p.StartRow + dr * i;
                    int c = p.StartCol + dc * i;
                    g.FillRectangle(shipBrush, c * cellSz + 1, r * cellSz + 1, cellSz - 2, cellSz - 2);
                }
            }
        }

        private void DrawShipPreview(Graphics g)
        {
            if (_selectedShipIndex < 0 || _hoverRow < 0 || _hoverCol < 0 || _placementConfirmed)
                return;

            bool canPlace = CanPlaceShip(_selectedShipIndex, _hoverRow, _hoverCol, _selectedHorizontal);
            using var previewBrush = new SolidBrush(Color.FromArgb(100, canPlace ? SelectedShipColor : InvalidPlacementColor));
            int cellSz = EffectiveCellSize;

            var size = _shipDefs[_selectedShipIndex].Size;
            int dr = _selectedHorizontal ? 0 : 1;
            int dc = _selectedHorizontal ? 1 : 0;
            for (int i = 0; i < size; i++)
            {
                int r = _hoverRow + dr * i;
                int c = _hoverCol + dc * i;
                if (r >= 0 && r < _boardSize && c >= 0 && c < _boardSize)
                    g.FillRectangle(previewBrush, c * cellSz + 1, r * cellSz + 1, cellSz - 2, cellSz - 2);
            }
        }

        private void DrawCombinedGrid(Graphics g, BattleshipTeamView view, int offsetX)
        {
            using var shipBrush = new SolidBrush(ShipColor);
            using var hitBrush = new SolidBrush(HitColor);
            using var sunkBrush = new SolidBrush(SunkColor);

            // Build sunk ship cell set (enemy ships you sunk)
            var sunkCells = new HashSet<int>();
            foreach (var sunk in view.SunkShips)
            {
                int dr = sunk.IsHorizontal ? 0 : 1;
                int dc = sunk.IsHorizontal ? 1 : 0;
                for (int j = 0; j < sunk.Size; j++)
                {
                    int cellIdx = (sunk.StartRow + dr * j) * _boardSize + (sunk.StartCol + dc * j);
                    sunkCells.Add(cellIdx);
                }
            }

            int cellSz = EffectiveCellSize;

            for (int i = 0; i < _boardSize * _boardSize; i++)
            {
                int r = i / _boardSize;
                int c = i % _boardSize;
                var rect = new Rectangle(offsetX + c * cellSz + 1, r * cellSz + 1, cellSz - 2, cellSz - 2);

                // Your ships (background)
                if (view.ShipCells[i])
                    g.FillRectangle(shipBrush, rect);

                // Your outgoing attacks on enemy grid
                if (view.AttackGrid[i] == CellState.Hit)
                {
                    if (sunkCells.Contains(i))
                    {
                        // Sunk: thick orange border + X
                        using var sunkPen = new Pen(OutgoingHitColor, 3);
                        g.DrawRectangle(sunkPen, rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
                        DrawX(g, rect, OutgoingHitColor, 1.5f);
                    }
                    else
                    {
                        // Hit: orange circle
                        using var hitPen = new Pen(OutgoingHitColor, 2.5f);
                        int margin = 8;
                        g.DrawEllipse(hitPen, rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2);
                    }
                }
                else if (view.AttackGrid[i] == CellState.Miss)
                {
                    // Small filled white dot
                    int dotSize = cellSz / 5;
                    using var missBrush = new SolidBrush(OutgoingMissColor);
                    int cx = rect.Left + rect.Width / 2 - dotSize / 2;
                    int cy = rect.Top + rect.Height / 2 - dotSize / 2;
                    g.FillEllipse(missBrush, cx, cy, dotSize, dotSize);
                }

                // Draw attacker identity on this cell (who attacked this defense cell)
                if (view.AttackedBy != null && view.AttackedBy.Length > i && view.AttackedBy[i] >= 0)
                {
                    int attacker = view.AttackedBy[i];
                    Color attackerColor = BingoConstants.GetTeamColorBright(attacker);
                    int dotSize = Math.Max(4, cellSz / 7);

                    // Determine top/bottom placement using match teams order when available
                    var teams = Client?.Room?.Users?.Where(u => u.Team >= 0).Select(u => u.Team).Distinct().OrderBy(t => t).Take(2).ToList();
                    int cyAtt;
                    if (teams != null && teams.Count >= 2)
                    {
                        if (attacker == teams[0])
                            cyAtt = rect.Top + rect.Height / 4 - dotSize / 2; // top third
                        else if (attacker == teams[1])
                            cyAtt = rect.Top + rect.Height * 3 / 4 - dotSize / 2; // bottom third
                        else
                            cyAtt = rect.Top + rect.Height / 2 - dotSize / 2; // middle fallback
                    }
                    else
                    {
                        // Fallback: even team -> top, odd -> bottom
                        if ((attacker % 2) == 0)
                            cyAtt = rect.Top + rect.Height / 4 - dotSize / 2;
                        else
                            cyAtt = rect.Top + rect.Height * 3 / 4 - dotSize / 2;
                    }

                    int cxAtt = rect.Left + rect.Width / 2 - dotSize / 2;
                    using var brush = new SolidBrush(attackerColor);
                    g.FillEllipse(brush, cxAtt, cyAtt, dotSize, dotSize);
                }

                // Draw multi-hit badge if present (your outgoing attack hit multiple teams)
                (int Count, DateTime Expiry) overlay;
                lock (_multiHitLock)
                {
                    if (_multiHitOverlays.TryGetValue(i, out var o))
                    {
                        overlay = o;
                        if (o.Expiry <= DateTime.UtcNow)
                        {
                            _multiHitOverlays.Remove(i);
                            overlay = (0, DateTime.MinValue);
                        }
                    }
                    else
                    {
                        overlay = (0, DateTime.MinValue);
                    }
                }

                if (overlay.Count > 1)
                {
                    int badgeSize = Math.Max(14, rect.Width / 5);
                    // Sanity check sizes to avoid invalid GDI+ parameters during transient layout changes
                    if (badgeSize > 0 && rect.Width >= badgeSize + 4 && rect.Height >= badgeSize + 4)
                    {
                        var badgeRect = new Rectangle(rect.Right - badgeSize - 4, rect.Top + 4, badgeSize, badgeSize);
                        try
                        {
                            using var badgeBrush = new SolidBrush(OutgoingHitColor);
                            using var badgePen = new Pen(Color.Black, 1f);
                            g.FillEllipse(badgeBrush, badgeRect);
                            g.DrawEllipse(badgePen, badgeRect);
                            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            using var txtBrush = new SolidBrush(Color.White);
                            g.DrawString(overlay.Count.ToString(), MultiHitBadgeFont, txtBrush, new RectangleF(badgeRect.X, badgeRect.Y + 1, badgeRect.Width, badgeRect.Height), sf);
                        }
                        catch (ArgumentException)
                        {
                            // Swallow GDI+ parameter errors to avoid crashing the UI during transient layout races
                        }
                        catch
                        {
                            // Non-fatal: don't let drawing errors crash the control
                        }
                    }
                }
            }
        }

        private void DrawMarkedCells(Graphics g, int offsetX = 0, int offsetY = 0)
        {
            if (_localMarkedCells.Count == 0)
                return;

            var starImage = Properties.Resources.tinystar;
            int cellSz = EffectiveCellSize;
            float scale = cellSz / 96f;
            float starW = starImage.Width * scale * 0.7f;
            float starH = starImage.Height * scale * 0.7f;
            float starX = 3f * scale;
            float starY = 3f * scale;

            foreach (int i in _localMarkedCells)
            {
                if (i < 0 || i >= _boardSize * _boardSize)
                    continue;
                int r = i / _boardSize;
                int c = i % _boardSize;
                g.DrawImage(starImage, offsetX + c * cellSz + starX, offsetY + r * cellSz + starY, starW, starH);
            }
        }

        private void DrawGoalText(Graphics g, int offsetX, int offsetY = 0)
        {
            var board = Client?.BingoBoard;
            if (board == null)
                return;

            int cellSz = EffectiveCellSize;
            float fontSize = 7.5f * cellSz / CellSize;
            using var font = new Font("Segoe UI", fontSize);
            using var textBrush = new SolidBrush(GoalTextColor);
            using var shadowBrush = new SolidBrush(GoalTextShadowColor);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.LineLimit
            };

            int totalSquares = Math.Min(board.SquareCount, _boardSize * _boardSize);
            for (int i = 0; i < totalSquares; i++)
            {
                int r = i / _boardSize;
                int c = i % _boardSize;
                int padding = 4;
                var textRect = new RectangleF(
                    offsetX + c * cellSz + padding,
                    offsetY + r * cellSz + padding,
                    cellSz - padding * 2,
                    cellSz - padding * 2
                );

                string text = board.Squares[i].Text ?? "";
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Shadow for readability
                var shadowRect = textRect;
                shadowRect.Offset(1, 1);
                g.DrawString(text, font, shadowBrush, shadowRect, sf);
                g.DrawString(text, font, textBrush, textRect, sf);
            }
        }

        private void DrawX(Graphics g, Rectangle rect, Color color, float penWidth)
        {
            using var pen = new Pen(color, penWidth);
            int margin = 8;
            g.DrawLine(pen, rect.Left + margin, rect.Top + margin, rect.Right - margin, rect.Bottom - margin);
            g.DrawLine(pen, rect.Right - margin, rect.Top + margin, rect.Left + margin, rect.Bottom - margin);
        }

        private void DrawDot(Graphics g, Rectangle rect, Color color, int dotSize, int offsetX, int offsetY)
        {
            using var brush = new SolidBrush(color);
            int cx = rect.Left + rect.Width / 2 - dotSize / 2 + offsetX;
            int cy = rect.Top + rect.Height / 2 - dotSize / 2 + offsetY;
            g.FillEllipse(brush, cx, cy, dotSize, dotSize);
        }

        private void DrawHollowDot(Graphics g, Rectangle rect, Color color, int dotSize, int offsetX, int offsetY)
        {
            using var pen = new Pen(color, 1.5f);
            int cx = rect.Left + rect.Width / 2 - dotSize / 2 + offsetX;
            int cy = rect.Top + rect.Height / 2 - dotSize / 2 + offsetY;
            g.DrawEllipse(pen, cx, cy, dotSize, dotSize);
        }

        private void DrawCheckedSquares(Graphics g, int offsetX, int offsetY)
        {
            var board = Client?.BingoBoard;
            if (board == null)
                return;

            int myTeam = Client?.LocalUser?.Team ?? -1;
            using var myTeamBrush = new SolidBrush(CheckedByMyTeamColor);
            using var enemyBrush = new SolidBrush(CheckedByEnemyColor);
            int cellSz = EffectiveCellSize;
            using var checkFont = new Font("Segoe UI", 9, FontStyle.Bold);

            int totalSquares = Math.Min(board.SquareCount, _boardSize * _boardSize);
            for (int i = 0; i < totalSquares; i++)
            {
                var square = board.Squares[i];
                bool checkedByMe = myTeam >= 0 && square.IsChecked(myTeam);
                bool checkedByAny = square.Team != null && square.Team.Length > 0;
                bool checkedByEnemy = checkedByAny && !checkedByMe;

                if (!checkedByMe && !checkedByEnemy)
                    continue;

                int r = i / _boardSize;
                int c = i % _boardSize;
                var rect = new Rectangle(offsetX + c * cellSz + 1, offsetY + r * cellSz + 1, cellSz - 2, cellSz - 2);

                if (checkedByMe)
                    g.FillRectangle(myTeamBrush, rect);
                else if (checkedByEnemy)
                    g.FillRectangle(enemyBrush, rect);

                // Checkmark indicator in corner
                string mark = checkedByMe ? "\u2713" : "\u2717";
                var markColor = checkedByMe ? Color.Lime : Color.Red;
                using var markBrush = new SolidBrush(markColor);
                g.DrawString(mark, checkFont, markBrush, rect.Right - 16, rect.Top + 2);
            }
        }

        #endregion

        #region Client event wiring

        protected override void AddClientListeners()
        {
            if (Client == null)
                return;
            Client.OnBattleshipConfig += Client_OnBattleshipConfig;
            Client.OnBattleshipTeamView += Client_OnBattleshipTeamView;
            Client.OnAttackResult += Client_OnAttackResult;
            Client.OnAllShipsPlaced += Client_OnAllShipsPlaced;
            Client.OnBattleshipGameOver += Client_OnBattleshipGameOver;
            Client.OnRoomChanged += Client_OnRoomChanged;
            Client.AddListener<ServerMatchStatusUpdate>(Client_MatchStatusUpdate);
            Client.AddListener<ServerSquareUpdate>(Client_SquareUpdate);
            Client.AddListener<ServerEntireBingoBoardUpdate>(Client_BoardUpdate);
        }

        protected override void RemoveClientListeners()
        {
            if (Client == null)
                return;
            Client.OnBattleshipConfig -= Client_OnBattleshipConfig;
            Client.OnBattleshipTeamView -= Client_OnBattleshipTeamView;
            Client.OnAttackResult -= Client_OnAttackResult;
            Client.OnAllShipsPlaced -= Client_OnAllShipsPlaced;
            Client.OnBattleshipGameOver -= Client_OnBattleshipGameOver;
            Client.OnRoomChanged -= Client_OnRoomChanged;
            Client.RemoveListener<ServerMatchStatusUpdate>(Client_MatchStatusUpdate);
            Client.RemoveListener<ServerSquareUpdate>(Client_SquareUpdate);
            Client.RemoveListener<ServerEntireBingoBoardUpdate>(Client_BoardUpdate);
        }

        protected override void ClientChanged()
        {
        }

        private void Client_OnBattleshipConfig(object? sender, BattleshipConfigEventArgs e)
        {
            void update()
            {
                SetBattleshipConfig(e.Ships, e.BoardSize);
                if (!IsSpectator)
                    EnterPlacementPhase();
                UpdateTargetTeamList();
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_OnBattleshipTeamView(object? sender, BattleshipTeamViewEventArgs e)
        {
            void update()
            {
                UpdateTeamView(e.TeamView);
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_OnAttackResult(object? sender, AttackResultEventArgs e)
        {
            void update()
            {
                try
                {
                    var myTeam = Client?.LocalUser?.Team ?? -1;
                    if (myTeam >= 0 && _teamViews.TryGetValue(myTeam, out var myView))
                    {
                        // If this attack hit (or missed) a cell that also contains one of our ships,
                        // reflect the result immediately on our local defense grid so the player sees it.
                        int idx = e.Index;
                        if (idx >= 0 && myView.ShipCells != null && idx < myView.ShipCells.Length && myView.ShipCells[idx])
                        {
                            if (myView.DefenseGrid != null && idx < myView.DefenseGrid.Length)
                            {
                                var newState = e.Result == AttackResult.Hit || e.Result == AttackResult.Sunk ? CellState.Hit : CellState.Miss;
                                if (myView.DefenseGrid[idx] != newState)
                                {
                                    myView.DefenseGrid[idx] = newState;
                                    if (myView.AttackedBy != null && idx < myView.AttackedBy.Length)
                                        myView.AttackedBy[idx] = e.AttackingTeam;
                                    _teamViews[myTeam] = myView;
                                }
                            }
                        }
                    }
                }
                catch { }

                ShowAttackResult(e.Index, e.Result, e.AttackingTeam, e.DefendingTeam);
                _gridPanel.Invalidate();
            }

            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_OnAllShipsPlaced(object? sender, EventArgs e)
        {
            void update()
            {
                ExitPlacementPhase();
                UpdateMessage("All ships placed! Match starting...", Color.Lime);
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_OnBattleshipGameOver(object? sender, BattleshipGameOverEventArgs e)
        {
            void update()
            {
                ShowGameOver(e.WinningTeam, e.WinningTeamName);
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_OnRoomChanged(object? sender, RoomChangedEventArgs e)
        {
            void update()
            {
                _teamViews.Clear();
                _localMarkedCells.Clear();
                _placementPhase = false;
                _placementConfirmed = false;
                PlacementPanel.Visible = false;
                _gridPanel.Invalidate();
                _messageLabel.Text = "";
                UpdateTargetTeamList();
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_MatchStatusUpdate(ClientModel? _, ServerMatchStatusUpdate statusUpdate)
        {
            void update()
            {
                if (statusUpdate.MatchStatus == MatchStatus.Running)
                {
                    ExitPlacementPhase();
                    UpdateMessage("Match is live! Click goals to complete them and attack!", Color.Lime);
                }
                else if (statusUpdate.MatchStatus == MatchStatus.Preparation)
                {
                    ExitPlacementPhase();
                    UpdateMessage("Preparation phase — study the board!", Color.Cyan);
                    _gridPanel.Invalidate();
                }
                else if (statusUpdate.MatchStatus == MatchStatus.Finished)
                {
                    ExitPlacementPhase();
                }
                else if (statusUpdate.MatchStatus == MatchStatus.NotRunning)
                {
                    _teamViews.Clear();
                    _gridPanel.Invalidate();
                    _messageLabel.Text = "";
                }
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_SquareUpdate(ClientModel? _, ServerSquareUpdate squareUpdate)
        {
            void update() { _gridPanel.Invalidate(); }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        private void Client_BoardUpdate(ClientModel? _, ServerEntireBingoBoardUpdate boardUpdate)
        {
            void update()
            {
                _localMarkedCells.Clear();
                _gridPanel.Invalidate();
            }
            if (InvokeRequired)
                BeginInvoke(update);
            else
                update();
        }

        #endregion
    }
}
