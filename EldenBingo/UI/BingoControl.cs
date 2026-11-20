using EldenBingo.Net;
using EldenBingo.Properties;
using EldenBingo.Settings;
using EldenBingoCommon;
using Neto.Shared;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Timers;

namespace EldenBingo.UI
{
    internal partial class BingoControl : ClientUserControl
    {
        public const float AspectRatio = 1.1f;
        private const float CheckAnimationTimerMax = 5.0f;
        private const float BingoAnimationTimerMax = 3.0f;
        private const int AnimationFPS = 30;

        private static Color BgColor => Color.FromArgb(BoardAppearanceSettings.Current.EmptyColor);
        private static Color TextColor => Color.FromArgb(BoardAppearanceSettings.Current.TextColor);
        private bool _customBoardBackground;
        private BoardStatusEnum _boardStatus;
        private string[] _boardStatusStrings = { "Waiting for match to start...", "Click to reveal...", "Match Starting...", "" };
        private bool _revealed = false;
        private BingoSquareControl[] Squares;
        private System.Timers.Timer? _timer;
        private bool _clickHotkeyConnected;

        private int _size;
        private int _selectedSquareIndex = -1;
        private int _squareBackgroundOpacity = 100;
        private TransparentMarkedSquareStyle _transparentMarkedSquareStyle = TransparentMarkedSquareStyle.Transparent;
        private int _lastNavigationSelection = 0;
        private bool _selectionFromKeyboard;
        private bool _claimPromptOpen;

        public int[] ActiveTeams { get; private set; }

        public enum TransparentMarkedSquareStyle
        {
            Transparent,
            Filled,
            Border,
        }

        public BingoControl() : base()
        {
            InitializeComponent();
            initializeBoardSearch();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
            _boardStatusLabel.ForeColor = TextColor;
            _boardStatusLabel.BackColor = BgColor;
            _gridControl.SetAspectRatio(AspectRatio);
            _gridControl.MaintainAspectRatio = true;
            _size = 0;
            Squares = new BingoSquareControl[0];
            initSquareControls(_size);
            Load += bingoControl_Load;
            SizeChanged += bingoControl_SizeChanged;
            _gridControl.SizeChanged += _gridControl_SizeChanged;
            Properties.Settings.Default.PropertyChanged += default_PropertyChanged;
            Disposed += (_, _) =>
            {
                Properties.Settings.Default.PropertyChanged -= default_PropertyChanged;
                _timer?.Dispose();
                DisconnectClickHotkey();
                if (Client != null)
                {
                    Client.LocalDimmingChanged -= refreshLocalDimming;
                }
            };
            ActiveTeams = Array.Empty<int>();
            applyAppearance();
        }

        public Color BoardBackgroundColor
        {
            get => _gridControl.BackColor;
            set
            {
                _customBoardBackground = true;
                BackColor = value;
                _gridControl.BackColor = value;
                _boardStatusLabel.BackColor = value;
                Invalidate(true);
            }
        }

        public int SquareBackgroundOpacity
        {
            get => _squareBackgroundOpacity;
            set
            {
                var opacity = Math.Clamp(value, 0, 100);
                if (_squareBackgroundOpacity == opacity)
                    return;

                _squareBackgroundOpacity = opacity;
                foreach (var square in Squares)
                {
                    square.SquareBackgroundOpacity = opacity;
                }
                Invalidate();
            }
        }

        public TransparentMarkedSquareStyle TransparentMarkedStyle
        {
            get => _transparentMarkedSquareStyle;
            set
            {
                if (_transparentMarkedSquareStyle == value)
                    return;

                _transparentMarkedSquareStyle = value;
                foreach (var square in Squares)
                {
                    square.TransparentMarkedStyle = value;
                }
                Invalidate();
            }
        }

        public int SquareSpacing
        {
            get => _gridControl.PaddingX;
            set
            {
                var spacing = Math.Max(0, value);
                _gridControl.PaddingX = spacing;
                _gridControl.PaddingY = spacing;
            }
        }
        private enum BoardStatusEnum
        {
            NoBoard,
            BoardSetNotRevealed,
            MatchStarting,
            BoardRevealed,
        }

        private void initSquareControls(int size)
        {
            var targetSquares = size * size;
            _gridControl.GridWidth = size;
            _gridControl.GridHeight = size;
            if (_size > size)
            {
                while(_gridControl.Controls.Count > targetSquares)
                {
                    var lastIndex = _gridControl.Controls.Count - 1;
                    var squareControl = _gridControl.Controls[lastIndex];
                    squareControl.MouseDown -= square_MouseDown;
                    squareControl.MouseEnter -= square_MouseEntered;
                    squareControl.MouseLeave -= square_MouseLeave;
                    _gridControl.Controls.RemoveAt(lastIndex);
                }
            }
            else if(_size < size)
            {
                int i = _gridControl.Controls.Count;
                while (_gridControl.Controls.Count < targetSquares)
                {
                    var squareControl = new BingoSquareControl(i++, string.Empty, string.Empty);
                    squareControl.SquareBackgroundOpacity = _squareBackgroundOpacity;
                    squareControl.TransparentMarkedStyle = _transparentMarkedSquareStyle;
                    squareControl.MouseDown += square_MouseDown;
                    squareControl.MouseEnter += square_MouseEntered;
                    squareControl.MouseLeave += square_MouseLeave;
                    _gridControl.Controls.Add(squareControl);
                }
            }
            if (_size != size) 
            {
                var squareList = new List<BingoSquareControl>(_gridControl.Controls.OfType<BingoSquareControl>());
                Squares = squareList.ToArray();
                _size = size;
            }
        }


        public void FlashBingo(BingoLine bingo)
        {
            void flashSquares(int startx, int starty, int dx, int dy)
            {
                int index(int x, int y) { return y * _size + x; }
                var x = startx;
                var y = starty;
                for (int i = 0; i < _size; ++i)
                {
                    Squares[index(x, y)].BingoAnimationTimer = BingoAnimationTimerMax;
                    x += dx;
                    y += dy;
                }
            }
            switch (bingo.Type)
            {
                case 0:
                    flashSquares(bingo.BingoIndex, 0, 0, 1);
                    break;

                case 1:
                    flashSquares(0, bingo.BingoIndex, 1, 0);
                    break;

                case 2:
                    flashSquares(0, 0, 1, 1);
                    break;

                case 3:
                    flashSquares(0, _size - 1, 1, -1);
                    break;
            }
            startTimer();
        }

        public void UpdateBoard()
        {
            if (Client?.Room?.Match != null)
            {
                setBoard(Client.Room.Match.Board);
            }
        }

        protected override void AddClientListeners()
        {
            if (Client == null)
                return;

            Client.OnRoomChanged += onRoomChanged;
            Client.LocalDimmingChanged += refreshLocalDimming;
            Client.AddListener<ServerSquareUpdate>(squareUpdate);
            Client.AddListener<ServerMatchStatusUpdate>(matchStatusUpdate);
            Client.AddListener<ServerEntireBingoBoardUpdate>(entireBingoBoardUpdate);
            Client.AddListener<ServerBingoAchievedUpdate>(bingoUpdate);
            Client.AddListener<ServerScoreboardUpdate>(scoreBoardUpdate);
        }

        private void scoreBoardUpdate(ClientModel? model, ServerScoreboardUpdate update)
        {
            var activeTeams = update.Scoreboard.Select(t => t.Team).ToArray();
            var oldSet = new HashSet<int>(ActiveTeams);
            var newSet = new HashSet<int>(activeTeams);
            var changed = !oldSet.SetEquals(newSet);

            if (changed)
            {
                //Redraw all squares if the scoreboard was changed
                foreach (var square in Squares)
                {
                    square.ActiveTeams = activeTeams;
                }
            }
        }

        protected override void ClientChanged()
        {
            if (Client?.Room == null)
            {
                clearBoard();
                return;
            }

            setBoard(Client.Room.Match.Board);
            updateBoardStatus(Client.Room.Match);
        }

        protected override void RemoveClientListeners()
        {
            if (Client == null)
                return;

            Client.OnRoomChanged -= onRoomChanged;
            Client.LocalDimmingChanged -= refreshLocalDimming;
            Client.RemoveListener<ServerSquareUpdate>(squareUpdate);
            Client.RemoveListener<ServerMatchStatusUpdate>(matchStatusUpdate);
            Client.RemoveListener<ServerEntireBingoBoardUpdate>(entireBingoBoardUpdate);
            Client.RemoveListener<ServerBingoAchievedUpdate>(bingoUpdate);
        }

        private void startTimer()
        {
            if (_timer == null)
            {
                _timer = new System.Timers.Timer();
                _timer.Interval = 1000d / AnimationFPS;
                _timer.Elapsed += onTimerTick;
                _timer.Start();
            }
        }

        private void onTimerTick(object? sender, ElapsedEventArgs e)
        {
            var delta = 1.0f / AnimationFPS;
            var maxTimer = 0f;
            foreach (var square in Squares)
            {
                if (square.CheckAnimationTimer > 0)
                {
                    square.CheckAnimationTimer -= delta;
                    square.Invalidate();
                    maxTimer = Math.Max(maxTimer, square.CheckAnimationTimer);
                }
                if (square.BingoAnimationTimer > 0)
                {
                    square.BingoAnimationTimer -= delta;
                    square.Invalidate();
                    maxTimer = Math.Max(maxTimer, square.BingoAnimationTimer);
                }
            }
            if (maxTimer <= 0 && _timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }

        private void onRoomChanged(object? sender, RoomChangedEventArgs e)
        {
            _revealed = false;
            if (Client?.BingoBoard == null)
            {
                clearBoard();
            }
            if (Client?.Room?.Match != null)
            {
                updateBoardStatus(Client.Room.Match);
            }
        }

        private void squareUpdate(ClientModel? _, ServerSquareUpdate update)
        {
            void apply()
            {
                if (Client?.BingoBoard == null || update.Index < 0 || update.Index >= Squares.Length) return;
                Client.BingoBoard.Squares[update.Index] = update.Square;
                updateSquare(Client.BingoBoard, update.Index, Properties.Settings.Default.MarkHighlight);
                updateSearchMatches();
            }
            if (InvokeRequired) BeginInvoke((Action)apply); else apply();
        }

        private void matchStatusUpdate(ClientModel? _, ServerMatchStatusUpdate matchStatus)
        {
            if (Client?.Room?.Match != null)
            {
                updateBoardStatus(Client.Room.Match);
            }
        }

        private void entireBingoBoardUpdate(ClientModel? _, ServerEntireBingoBoardUpdate boardUpdate)
        {
            if (Client?.Room == null)
                return;

            setBoard(Client.Room.Match.Board);
            updateBoardStatus(Client.Room.Match);
        }

        private void bingoUpdate(ClientModel? _, ServerBingoAchievedUpdate update)
        {
            if(Properties.Settings.Default.BingoHighlight)
                FlashBingo(update.Bingo);
        }

        private void _boardStatusLabel_Click(object sender, EventArgs e)
        {
            if (_boardStatus == BoardStatusEnum.BoardSetNotRevealed)
            {
                _revealed = true;
                _boardStatus = BoardStatusEnum.BoardRevealed;
                _boardStatusLabel.Text = _boardStatusStrings[(int)_boardStatus];
                _boardStatusLabel.Visible = false;
                refreshSearchAvailability();
            }
        }

        private void _gridControl_SizeChanged(object? sender, EventArgs e)
        {
            _boardStatusLabel.Location = new Point(_gridControl.Location.X + _gridControl.BorderX, _gridControl.Location.Y + _gridControl.BorderY);
            _boardStatusLabel.Width = _gridControl.Width - _gridControl.BorderX * 2;
            _boardStatusLabel.Height = _gridControl.Height - _gridControl.BorderY * 2;
        }

        private void bingoControl_Load(object? sender, EventArgs e)
        {
            _gridControl.UpdateGrid();
            recalculateFontSizeForSquares();
            ConnectClickHotkey();
        }

        private BingoSquareControl? getSelectedSquare()
        {
            if (_selectedSquareIndex >= 0 && _selectedSquareIndex < Squares.Length)
                return Squares[_selectedSquareIndex];
            return null;
        }

        private void setSelectedSquare(int newIndex)
        {
            _selectionFromKeyboard = false;
            if (_selectedSquareIndex == newIndex) return;
            // Deselect old square
            if (_selectedSquareIndex >= 0 && _selectedSquareIndex < Squares.Length)
                Squares[_selectedSquareIndex].MouseOver = false;
            _selectedSquareIndex = newIndex;
            // Select new square
            if (_selectedSquareIndex >= 0 && _selectedSquareIndex < Squares.Length)
                Squares[_selectedSquareIndex].MouseOver = true;
        }

        private void selectKeyboardSquare(int index)
        {
            setSelectedSquare(index);
            _selectionFromKeyboard = true;
        }

        private async void mouseWheel(object? sender, MouseEventArgs e)
        {
            if (Squares == null || e.Delta == 0 || !canUseBoardHotkeys())
                return;

            var square = getWheelTarget(Cursor.Position);
            if (square != null)
            {
                await changeSquareCounter(square, Math.Clamp(e.Delta, -1, 1));
            }
        }

        private BingoSquareControl? getWheelTarget(Point screenPosition) =>
            getMouseHoveredSquare(screenPosition) ??
            (Properties.Settings.Default.ScrollKeyboardSelectedSquare && _selectionFromKeyboard ? getSelectedSquare() : null);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);

        private BingoSquareControl? getMouseHoveredSquare(Point screenPosition)
        {
            // Keyboard selection uses the same visual highlight as mouse hover.
            // Hit-test the actual window so scrolling over chat, a gap, or a
            // covering application never changes the keyboard-selected square.
            var square = Control.FromChildHandle(WindowFromPoint(screenPosition)) as BingoSquareControl;
            return square != null && square.Visible && square.Enabled && Squares.Contains(square) &&
                square.ClientRectangle.Contains(square.PointToClient(screenPosition)) ? square : null;
        }

        private void bingoControl_SizeChanged(object? sender, EventArgs e)
        {
            recalculateFontSizeForSquares();
        }

        private void clearBoard()
        {
            void update()
            {
                initSquareControls(0);
                _boardStatus = BoardStatusEnum.NoBoard;
                refreshSearchAvailability();
                Invalidate();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void default_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.BoardAppearanceJson))
                applyAppearance();
            if (e.PropertyName == nameof(Properties.Settings.Default.BingoFont) ||
                e.PropertyName == nameof(Properties.Settings.Default.BingoFontStyle) ||
                e.PropertyName == nameof(Properties.Settings.Default.BingoFontSize))
            {
                recalculateFontSizeForSquares();
            }
            if(e.PropertyName == nameof(Properties.Settings.SquareShadows))
            {
                redrawAllSquares();
            }
            if (e.PropertyName == nameof(Properties.Settings.KeywordColorsJson) || e.PropertyName == nameof(Properties.Settings.KeywordColorsAlpha))
            {
                foreach (var square in Squares)
                {
                    square.UpdateKeywordColor();
                }
                redrawAllSquares();
            }
        }

        private void applyAppearance()
        {
            SquareSpacing = BoardAppearanceSettings.Current.Spacing;
            recalculateFontSizeForSquares();
            _boardStatusLabel.ForeColor = TextColor;
            if (!_customBoardBackground)
            {
                _gridControl.BackColor = Color.FromArgb(BoardAppearanceSettings.Current.BorderColor);
                _boardStatusLabel.BackColor = BgColor;
            }
            redrawAllSquares();
        }

        private void redrawAllSquares()
        {
            Invalidate();
            foreach (var square in Squares)
            {
                square.Invalidate();
            }
        }

        public void ConnectClickHotkey()
        {
            if (_clickHotkeyConnected)
                return;

            var mainForm = MainForm.GetMainForm(this) ?? MainForm.Instance;
            if (mainForm != null)
            {
                mainForm.RawInput.KeyPressed += keyPressed;
                mainForm.RawInput.MouseWheel += mouseWheel;
                _clickHotkeyConnected = true;
            }
        }

        public void DisconnectClickHotkey()
        {
            if (!_clickHotkeyConnected)
                return;

            var mainForm = MainForm.GetMainForm(this) ?? MainForm.Instance;
            if (mainForm != null)
            {
                mainForm.RawInput.KeyPressed -= keyPressed;
                mainForm.RawInput.MouseWheel -= mouseWheel;
            }
            _clickHotkeyConnected = false;
        }

        private UserInRoom? getUserToSetFor()
        {
            return LobbyControl.CurrentlyOnBehalfOfUser;
        }

        private void recalculateFontSizeForSquares()
        {
            var scale = this.DefaultScaleFactors();

            var labelFontSize = Height / 30f;
            _boardStatusLabel.Font = MainForm.GetFontFromSettings(Font, labelFontSize / scale.Height);

            if (Squares == null || Squares.Length == 0)
                return;
            var squareHeight = Squares[0].Height;

            var bingoFontSize = squareHeight / 10f;
            var font = MainForm.GetFontFromSettings(Font, bingoFontSize / scale.Height);
            
            foreach(var square in Squares)
            {
                square.Font = font;
            }
        }

        internal void PreviewBoard(BingoBoard board)
        {
            setBoard(board);
            _boardStatus = BoardStatusEnum.BoardRevealed;
            _boardStatusLabel.Visible = false;
            refreshSearchAvailability();
        }

        private void setBoard(BingoBoard? board)
        {
            void update()
            {
                if (board == null)
                {
                    clearBoard();
                    return;
                }
                initSquareControls(board.Size);
                recalculateFontSizeForSquares();
                for (int i = 0; i < board.SquareCount; ++i)
                {
                    updateSquare(board, i, false);
                }
                _selectedSquareIndex = -1;
                _lastNavigationSelection = 0;
                _selectionFromKeyboard = false;
                updateSearchMatches();
                redrawAllSquares();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private string escapeText(string text)
        {
            return text.Replace("&", "&&");
        }

        private void updateSquare(BingoBoard board, int index, bool highlightNewSquares)
        {
            var s = board.Squares[index];
            Squares[index].Text = escapeText(s.Text);
            Squares[index].ToolTip = s.Tooltip;
            Squares[index].PointValue = s.PointValue;
            Squares[index].Lockout = board.Lockout;
            updateSquareStatus(board, index, highlightNewSquares);
        }

        private void updateSquareStatus(BingoBoard board, int index, bool highlightNewSquares)
        {
            var s = board.Squares[index];
            var teamsBefore = Squares[index].Teams;
            Squares[index].Teams = s.Team;
            Squares[index].Marked = s.Marked;
            Squares[index].Counters = s.Counters;
            Squares[index].Dimmed = Client?.HasLocalDim(index) == true;
            if (highlightNewSquares)
            {
                //If a new team checked the square (more teams present than before)
                if (s.Team.Length > teamsBefore.Length)
                {
                    //Start check animation
                    Squares[index].CheckAnimationTimer = CheckAnimationTimerMax;
                    startTimer();
                }
                //If a team check was removed (less teams present than before)
                else if (s.Team.Length < teamsBefore.Length)
                {
                    //Stop check animation
                    Squares[index].CheckAnimationTimer = 0f;
                }
            }
            Squares[index].Invalidate();
        }

        private async void square_MouseDown(object? sender, MouseEventArgs e)
        {
            if (Client == null || sender is not BingoSquareControl c)
                return;

            //No room or no board set in room
            if (Client.Room?.Match?.Board == null)
                return;

            BingoBoard board = Client.Room.Match.Board;

            if (e.Button == MouseButtons.Middle && CanSearchBoard && Properties.Settings.Default.MiddleMouseDimsSquares)
            {
                Client.ToggleLocalDim(c.Index);
                return;
            }

            if (e.Button == MouseButtons.Left && Client.Room.Match.MatchStatus >= MatchStatus.Running)
            {
                await clickSquare(c);
            }
            if (e.Button == MouseButtons.Right && Client.Room.Match.MatchStatus >= MatchStatus.Preparation)
            {
                await markSquare(c);
            }
        }

        private void square_MouseEntered(object? sender, EventArgs e)
        {
            // When a square is entered, deselect all other squares
            if(sender is BingoSquareControl square)
            {
                setSelectedSquare(square.Index);
            }
        }

        private void refreshLocalDimming()
        {
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { BeginInvoke((Action)refreshLocalDimming); return; }
            foreach (var square in Squares)
            {
                square.Dimmed = Client?.HasLocalDim(square.Index) == true;
                square.Invalidate();
            }
        }

        private void square_MouseLeave(object? sender, EventArgs e)
        {
            // When a square is entered, deselect all other squares
            if (sender is BingoSquareControl square)
            {
                if (_selectedSquareIndex == square.Index)
                    setSelectedSquare(-1);
            }
        }

        private async Task clickSquare(BingoSquareControl c)
        {
            //No room or no board set in room
            if (Client?.Room?.Match?.Board == null || _claimPromptOpen)
                return;

            var userToSetFor = getUserToSetFor();
            if (userToSetFor == null)
                return;

            var square = Client.Room.Match.Board.Squares[c.Index];
            var room = Client.Room;
            var board = room.Match.Board;
            var resume = false;
            var unclaiming = square.Team.Contains(userToSetFor.Team) || Client.LocalUser is { IsAdmin: true, IsSpectator: true } && square.Team.Length == 1;
            if (unclaiming && square.CanChooseTimedResume)
            {
                _claimPromptOpen = true;
                try
                {
                    using var dialog = new TimedUnclaimForm(square.Text);
                    Form? owner = MainForm.Instance is { IsDisposed: false } main ? main : FindForm();
                    dialog.TopMost = owner?.TopMost ?? false;
                    var choice = dialog.ShowDialog(owner);
                    if (choice is not (DialogResult.Yes or DialogResult.No)) return;
                    if (Client?.Room != room || Client.BingoBoard != board || room.Match.MatchStatus != MatchStatus.Running ||
                        !board.Squares[c.Index].CanChooseTimedResume || !board.Squares[c.Index].Team.SequenceEqual(square.Team)) return;
                    resume = choice == DialogResult.Yes;
                }
                finally { _claimPromptOpen = false; }
            }
            var p = new Packet(new ClientTryCheck(c.Index, userToSetFor.Guid, resume));
            await Client.SendPacketToServer(p);
        }

        private async Task markSquare(BingoSquareControl c)
        {
            //No room or no board set in room
            if (Client?.Room?.Match?.Board == null)
                return;

            var p = new Packet(new ClientTryMark(c.Index));
            await Client.SendPacketToServer(p);
        }

        private async Task changeSquareCounter(BingoSquareControl c, int change)
        {
            //No room or no board set in room
            if (Client?.Room?.Match?.Board == null)
                return;

            var userToSetFor = getUserToSetFor();
            if (userToSetFor == null || userToSetFor.IsSpectator)
                return;

            if (Client.Room?.Match?.Board != null && Client.Room.Match.MatchStatus >= MatchStatus.Running)
            {
                var p = new Packet(new ClientTrySetCounter(c.Index, change, userToSetFor.Guid));
                await Client.SendPacketToServer(p);
            }
        }

        private void updateBoardStatus(Match match)
        {
            void update()
            {
                if (match.Board == null)
                {
                    _boardStatus = match.MatchStatus == MatchStatus.Starting ? BoardStatusEnum.MatchStarting : BoardStatusEnum.NoBoard;
                }
                else
                {
                    if (match.MatchStatus < MatchStatus.Preparation)
                    {
                        _boardStatus = BoardStatusEnum.BoardSetNotRevealed;
                    }
                    else
                    {
                        _boardStatus = BoardStatusEnum.BoardRevealed;
                        _revealed = false; //Reset revealed status, so the board is not automatically revealed next time
                    }
                }
                _boardStatusLabel.Text = _boardStatusStrings[(int)_boardStatus];
                _boardStatusLabel.Visible = !_revealed && _boardStatus < BoardStatusEnum.BoardRevealed;
                refreshSearchAvailability();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private class BingoSquareControl : Control
        {
            public int PointValue { get; set; } = 1;
            public bool SearchMatched { get; set; }
            public bool Dimmed { get; set; }
            private bool _mouseOver;
            public bool MouseOver
            {
                get { return _mouseOver; }
                set
                {
                    if (_mouseOver != value)
                    {
                        _mouseOver = value;
                        Invalidate();
                    }
                }
            }
            public float CheckAnimationTimer;
            public float BingoAnimationTimer;
            private readonly ToolTip _toolTip;
            private int[] _teams;
            private int[] _activeTeams;
            private TransparentMarkedSquareStyle _transparentMarkedSquareStyle;
            private SquareCounter[] _counters;
            private bool _marked;
            private SolidBrush _brush;
            private LinearGradientBrush _gradientBrush;
            private ImageAttributes _imageAttributes;
            private ColorMatrix _colorMatrix;

            private static Bitmap _starImage;
            private static Bitmap _squareGradient;

            private Color? _keywordColor;
            private int _squareBackgroundOpacity = 100;

            static BingoSquareControl()
            {
                _starImage = Resources.tinystar;
                _squareGradient = Resources.square_gradient;
            }

            public BingoSquareControl(int index, string text, string tooltip)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                TextChanged += onTextChanged;
                BackColor = Color.Transparent;
                Index = index;
                Text = text;
                _toolTip = new ToolTip();
                ToolTip = tooltip;
                Teams = Array.Empty<int>();
                _activeTeams = Array.Empty<int>();
                _transparentMarkedSquareStyle = TransparentMarkedSquareStyle.Transparent;
                _counters = new SquareCounter[0];
                _brush = new SolidBrush(Color.White);
                _imageAttributes = new ImageAttributes();
                _colorMatrix = new ColorMatrix();
                _imageAttributes.SetColorMatrix(_colorMatrix);
                if (Height > 0)
                {
                    _gradientBrush = new LinearGradientBrush(new Point(0, 0), new Point(0, Height), Color.Transparent, Color.Transparent);
                }
                SizeChanged += (o, e) =>
                {
                    if (Height > 0)
                    {
                        _gradientBrush?.Dispose();
                        _gradientBrush = new LinearGradientBrush(new Point(0, 0), new Point(0, Height), Color.Transparent, Color.Transparent);
                    }
                };
            }

            private void onTextChanged(object? sender, EventArgs e)
            {
                UpdateKeywordColor();
            }

            public void UpdateKeywordColor()
            {
                _keywordColor = KeywordColorsJsonHelper.GetColor(Text)?.Color ?? null;
                Invalidate();
            }

            private float keywordColorAlpha()
            {
                return Math.Clamp(Properties.Settings.Default.KeywordColorsAlpha * 0.01f, 0f, 1f);
            }

            public int SquareBackgroundOpacity
            {
                get => _squareBackgroundOpacity;
                set
                {
                    var opacity = Math.Clamp(value, 0, 100);
                    if (_squareBackgroundOpacity == opacity)
                        return;

                    _squareBackgroundOpacity = opacity;
                    Invalidate();
                }
            }

            public TransparentMarkedSquareStyle TransparentMarkedStyle
            {
                get => _transparentMarkedSquareStyle;
                set
                {
                    if (_transparentMarkedSquareStyle == value)
                        return;

                    _transparentMarkedSquareStyle = value;
                    Invalidate();
                }
            }

            public int[] Teams
            {
                get { return _teams; }
                set
                {
                    if (_teams != value)
                    {
                        _teams = value;
                        AccessibleDescription = _teams.Length == 0 ? "Unclaimed square" : "Claimed by teams " + string.Join(", ", _teams.Select(t => t + 1));
                        Invalidate();
                    }
                }
            }

            public bool Lockout { get; set; }

            public int[] ActiveTeams
            {
                get { return _activeTeams; }
                set
                {
                    var oldSet = new HashSet<int>(_activeTeams);
                    var newSet = new HashSet<int>(value);
                    _activeTeams = value;
                    if (!oldSet.SetEquals(newSet))
                    {
                        Invalidate();
                    }
                }
            }

            public SquareCounter[] Counters
            {
                get { return _counters; }
                set
                {
                    if (_counters.Length != value.Length)
                    {
                        _counters = value;
                        Invalidate();
                    }
                    else
                    {
                        var changed = false;
                        for (int i = 0; i < value.Length; ++i)
                        {
                            if (!_counters[i].Equals(value[i]))
                            {
                                _counters[i] = value[i];
                                changed = true;
                            }
                        }
                        if (changed)
                            Invalidate();
                    }
                }
            }

            public int Index { get; init; }

            public bool Marked
            {
                get { return _marked; }
                set
                {
                    if (_marked != value)
                    {
                        _marked = value;
                        Invalidate();
                    }
                }
            }

            public string ToolTip
            {
                get { return _toolTip.GetToolTip(this); }
                set
                {
                    _toolTip.AutoPopDelay = 32766;
                    _toolTip.SetToolTip(this, string.IsNullOrWhiteSpace(value) ? null : value);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                drawRectangle(e);
                drawBorder(e);
                drawTransparentMarkedBorder(e);

                drawBingoText(e);
                if (PointValue != 1 && Width > 8 && Height > 8)
                {
                    var label = $"{PointValue} pts";
                    using var font = new Font(Font.FontFamily, Math.Clamp(Font.Size * 0.55f, 6f, 12f), FontStyle.Bold);
                    var size = TextRenderer.MeasureText(label, font);
                    var rect = new Rectangle(Math.Max(2, Width - size.Width - 4), 4, Math.Min(size.Width, Width - 4), size.Height);
                    using var background = new SolidBrush(Color.FromArgb(210, 20, 20, 20));
                    e.Graphics.FillRectangle(background, rect);
                    TextRenderer.DrawText(e.Graphics, label, font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
                if (SearchMatched && Width > 8 && Height > 8)
                {
                    using var shadow = new Pen(Color.Black, 5);
                    using var highlight = new Pen(Color.White, 2) { DashStyle = DashStyle.Dash };
                    var matchRect = new Rectangle(3, 3, Width - 7, Height - 7);
                    e.Graphics.DrawRectangle(shadow, matchRect); e.Graphics.DrawRectangle(highlight, matchRect);
                }

                if (Marked)
                    drawMarkedStar(e);

                //Don't draw counters in lockout mode if a square is claimed
                if (!Lockout || Teams.Length == 0)
                    drawCounters(e);

                if (Dimmed)
                {
                    using var shade = new SolidBrush(Color.FromArgb(165, Color.Black));
                    e.Graphics.FillRectangle(shade, ClientRectangle);
                }

            }

            private static string? findLongestWord(Graphics g, string text, Font font)
            {
                string? longest = null;
                float longestLength = 0f;
                foreach (var word in text.Split(" "))
                {
                    var len = g.MeasureString(word, font);
                    if (len.Width > longestLength)
                    {
                        longest = word;
                        longestLength = len.Width;
                    }
                }
                return longest;
            }

            private void drawBingoText(PaintEventArgs e)
            {
                if (Width < 4 || Height < 4) return;
                const int textUp = 2;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
                var f = Font;
                var pointInset = PointValue == 1 ? 0 : Math.Min(Height / 3, (int)Math.Ceiling(Math.Clamp(Font.Size * 0.55f, 6f, 12f) * 96 / 72) + 8);
                var available = new Size(ClientRectangle.Width, Math.Max(1, ClientRectangle.Height - pointInset));
                Size size = TextRenderer.MeasureText(Text, f, available);
                while (f.Size > 1 && (size.Width > available.Width || size.Height > available.Height + textUp))
                {
                    var next = new Font(Font.FontFamily, Math.Max(1, f.Size - .2f), Font.Style);
                    if (!ReferenceEquals(f, Font)) f.Dispose();
                    f = next;
                    size = TextRenderer.MeasureText(Text, f, available, flags);
                }
                var textRect = new Rectangle(0, pointInset - textUp, available.Width, available.Height + textUp);
                var shadowRect = new Rectangle(1, pointInset + 1 - textUp, available.Width, available.Height + textUp);
                var shadowColor = Color.FromArgb(96, 0, 0, 0);
                TextRenderer.DrawText(e, Text, f, shadowRect, shadowColor, flags);
                //If a keyword color is set for this square (meaning a matching keyword rule was found)
                //and no team has claimed the square yet:
                //Color the text with the keyword color
                var tc = _teams.Length == 1 ? BoardAppearanceSettings.Current.ClaimedText(_teams[0]) : TextColor;
                if (_keywordColor != null && _teams.Length == 0)
                {
                    tc = blend(_keywordColor.Value, TextColor, keywordColorAlpha());
                }
                TextRenderer.DrawText(e, Text, f, textRect, tc, flags);
                if (!ReferenceEquals(f, Font)) f.Dispose();
            }

            private static Color blend(Color color, Color backColor, double amount)
            {
                byte r = (byte)(color.R * amount + backColor.R * (1 - amount));
                byte g = (byte)(color.G * amount + backColor.G * (1 - amount));
                byte b = (byte)(color.B * amount + backColor.B * (1 - amount));
                return Color.FromArgb(r, g, b);
            }


            private void drawCounters(PaintEventArgs e)
            {
                var counterFont = new Font(Font.FontFamily, Font.Size * 1.2f);
                var counterFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                var shadowColor = Color.FromArgb(96, 0, 0, 0);
                for (int i = 0; i < _counters.Length; ++i)
                {
                    var c = _counters[i];
                    if (c.Counter == 0)
                        continue;
                    if (Teams.Contains(c.Team)) //Don't draw counter for teams that have claimed the square
                        continue;
                    var size = TextRenderer.MeasureText(c.Counter.ToString(), counterFont);
                    int leftXPos;
                    if (i == 0)
                        leftXPos = 0;
                    else if (i == _counters.Length - 1)
                        leftXPos = Width - size.Width;
                    else
                    {
                        var widthBetweenLeftAndRight = Width - size.Width;
                        leftXPos = Convert.ToInt32(i * (widthBetweenLeftAndRight / (_counters.Length - 1f)));
                    }
                    int yPos = Height - size.Height;
                    var color = EldenBingo.Settings.BoardAppearanceSettings.TeamColorBright(c.Team);
                    TextRenderer.DrawText(e, c.Counter.ToString(), counterFont, new Rectangle(leftXPos + 1, yPos + 1, size.Width, size.Height), shadowColor, flags: counterFlags);
                    TextRenderer.DrawText(e, c.Counter.ToString(), counterFont, new Rectangle(leftXPos, yPos, size.Width, size.Height), color, flags: counterFlags);
                }
            }

            private void drawMarkedStar(PaintEventArgs e)
            {
                var scale = Width / 96f;
                var x = 3f * scale;
                var y = 3f * scale;
                var width = _starImage.Width * scale * 0.7f;
                var height = _starImage.Height * scale * 0.7f;
                e.Graphics.DrawImage(_starImage, x, y, width, height);
            }

            private void drawBorder(PaintEventArgs e)
            {
                var style = BoardAppearanceSettings.Current;
                if (style.BorderWidth == 0 || Width <= style.BorderWidth || Height <= style.BorderWidth) return;
                using var pen = new Pen(Color.FromArgb(style.BorderColor), style.BorderWidth);
                var inset = style.BorderWidth / 2f;
                e.Graphics.DrawRectangle(pen, inset, inset, Width - style.BorderWidth, Height - style.BorderWidth);
            }

            private void drawTransparentMarkedBorder(PaintEventArgs e)
            {
                if (_transparentMarkedSquareStyle != TransparentMarkedSquareStyle.Border || _teams.Length == 0)
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.None;
                for (var i = 0; i < _teams.Length; i++)
                {
                    var inset = 2 + i * 4;
                    if (Width <= inset * 2 || Height <= inset * 2)
                        break;

                    using var pen = new Pen(EldenBingo.Settings.BoardAppearanceSettings.TeamColor(_teams[i]), 3);
                    e.Graphics.DrawRectangle(pen, inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);
                }
            }
            private Color withSquareOpacity(Color color)
            {
                if (_squareBackgroundOpacity >= 100)
                    return color;

                if (_teams.Length > 0 && _transparentMarkedSquareStyle == TransparentMarkedSquareStyle.Filled)
                    return color;

                return Color.FromArgb(Convert.ToInt32(255 * (_squareBackgroundOpacity / 100f)), color);
            }
            private void drawRectangle(PaintEventArgs e)
            {
                var g = e.Graphics;
                bool isChecked = _teams.Length > 0;
                //Draw empty background
                if(_teams.Length == 0)
                {
                    Color color = BgColor;
                    if (MouseOver)
                    {
                        color = color.Brighten(BoardAppearanceSettings.Current.HoverIntensity / 100f);
                    }
                    _brush.Color = withSquareOpacity(color);
                    g.FillRectangle(_brush, new Rectangle(0, 0, Width, Height));
                }
                else
                {
                    Dictionary<int, int> teamIndex = new Dictionary<int, int>();
                    //In lockout, or if no active teams are set, draw the teams in the order they appear in the "selected" list
                    if (Lockout || _activeTeams.Length == 0)
                    {
                        for (int i = 0; i < _teams.Length; ++i)
                        {
                            teamIndex[_teams[i]] = i;
                        }
                    }
                    //In non-lockout, order the teams according to the active teams list, so we get gaps for teams that have not checked the square
                    else
                    {
                        for (int i = 0; i < _activeTeams.Length; ++i)
                        {
                            teamIndex[_activeTeams[i]] = i;
                        }
                    }
                    var numTeams = teamIndex.Count;
                    if (numTeams > 0)
                    {
                        if(numTeams > 1)
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                        foreach (var team in _teams)
                        {
                            //Find the index of the team, if not present (should never happen) continue to next team
                            if (!teamIndex.TryGetValue(team, out var i))
                                continue;

                            Color color = EldenBingo.Settings.BoardAppearanceSettings.TeamColor(team);
                            if (MouseOver)
                            {
                                color = color.Brighten(BoardAppearanceSettings.Current.HoverIntensity / 100f);
                            }
                            _brush.Color = withSquareOpacity(color);

                            if (numTeams == 1)
                            {
                                g.FillRectangle(_brush, new Rectangle(0, 0, Width, Height));
                            }
                            else
                            {
                                var angleAdd = 360f / numTeams;
                                var angleStart = numTeams % 2 == 0 ? 270f - (numTeams == 2 ? 45f : angleAdd / 2f) : 270f;
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.FillPie(_brush, new Rectangle(-Width, -Height, Width * 3, Height * 3), angleStart + angleAdd * i, angleAdd);
                            }
                        }
                        g.SmoothingMode = SmoothingMode.None;
                    }
                }
                var shadows = Properties.Settings.Default.SquareShadows * 0.01f;
                var backgroundOpacity = _squareBackgroundOpacity / 100f;
                var gradientColor = isChecked ? Color.FromArgb(Convert.ToInt32(120 * shadows * backgroundOpacity), 0, 0, 0) : Color.FromArgb(Convert.ToInt32(150 * shadows * backgroundOpacity), 0, 0, 0);
                _gradientBrush.LinearColors = new[] { gradientColor, Color.Transparent };
                g.FillRectangle(_gradientBrush, new Rectangle(0, 0, Width, Height));

                //Draw subtle dark shadow around the edges
                if (Properties.Settings.Default.SquareShadows > 0)
                {
                    _colorMatrix.Matrix00 = -1f;
                    _colorMatrix.Matrix11 = -1f;
                    _colorMatrix.Matrix22 = -1f;
                    _colorMatrix.Matrix33 = 0.6f * shadows * backgroundOpacity;
                    _imageAttributes.SetColorMatrix(_colorMatrix);
                    g.DrawImage(_squareGradient, new Rectangle(0, 0, Width, Height), 0, 0, _squareGradient.Width, _squareGradient.Height, GraphicsUnit.Pixel, _imageAttributes);
                }

                if (Properties.Settings.Default.MarkHighlight && CheckAnimationTimer > 0)
                {
                    var alpha = (1.0f - MathF.Sin(CheckAnimationTimer * 8f) * 0.2f) * invLerp(0.0f, 0.8f, CheckAnimationTimer);
                    _colorMatrix.Matrix00 = 1f;
                    _colorMatrix.Matrix11 = 1f;
                    _colorMatrix.Matrix22 = 1f;
                    _colorMatrix.Matrix33 = alpha;
                    _imageAttributes.SetColorMatrix(_colorMatrix);
                    g.DrawImage(_squareGradient, new Rectangle(0, 0, Width, Height), 0, 0, _squareGradient.Width, _squareGradient.Height, GraphicsUnit.Pixel, _imageAttributes);
                }

                //White out the entire tile
                if (BingoAnimationTimer > 0 || CheckAnimationTimer > 0)
                {
                    //Slow fading flash when square is involved in a bingo (but only if enabled in settings)
                    var frac = Properties.Settings.Default.BingoHighlight ? BingoAnimationTimer / BingoAnimationTimerMax : 0f;
                    //Quick flash if just checked (but only if enabled in settings)
                    var frac2 = Properties.Settings.Default.MarkHighlight ? invLerp(CheckAnimationTimerMax - 0.3f, CheckAnimationTimerMax, CheckAnimationTimer) * 0.4f : 0f;
                    _brush.Color = Color.FromArgb(Convert.ToInt32(255 * Math.Max(frac, frac2)), 255, 255, 230);
                    g.FillRectangle(_brush, new Rectangle(0, 0, Width, Height));
                }
            }

            private int round(float f)
            {
                return (int)MathF.Round(f);
            }

            private float mod(float x, float m)
            {
                return x > 0 ? x % m : (x % m + m) % m;
            }

            private float invLerp(float a, float b, float v)
            {
                return Math.Clamp((v - a) / (b - a), 0f, 1f);
            }
        }
    }
}
