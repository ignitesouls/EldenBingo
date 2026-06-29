using EldenBingoCommon;
using Neto.Shared;
using System.ComponentModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace EldenBingo.UI
{
    internal class ScoreboardControl : ClientUserControl
    {
        private const int RowPaddingBottom = 3;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem changeTeamNameToolStripMenuItem;
        private ToolStripMenuItem showMyTeamToolStripMenuItem;
        private IList<ScoreboardRowControl> _rows;

        public ScoreboardControl()
        {
            _rows = new List<ScoreboardRowControl>();
            contextMenuStrip1 = new ContextMenuStrip();
            changeTeamNameToolStripMenuItem = new ToolStripMenuItem("Change team name");
            changeTeamNameToolStripMenuItem.Click += changeTeamNameToolStripMenuItem_Click;
            contextMenuStrip1.Items.Add(changeTeamNameToolStripMenuItem);
            showMyTeamToolStripMenuItem = new ToolStripMenuItem("Show icon indicating my team");
            showMyTeamToolStripMenuItem.Click += showMyTeamToolStripMenuItem_Click;
            showMyTeamToolStripMenuItem.Checked = Properties.Settings.Default.GS_ShowPlayerTeam;
            contextMenuStrip1.Items.Add(showMyTeamToolStripMenuItem);
            SizeChanged += scoreboardControl_SizeChanged;
            Properties.Settings.Default.PropertyChanged += default_PropertyChanged;
        }

        public override Font Font
        {
            get { return base.Font; }
            set
            {
                base.Font = value;
                foreach (var row in _rows)
                    row.Font = value;
                updateHeight();
            }
        }

        public void SetShowPlayerTeam(bool showPlayerTeam)
        {
            void update()
            {
                showMyTeamToolStripMenuItem.Checked = showPlayerTeam;
                foreach (var row in Controls.OfType<ScoreboardRowControl>())
                {
                    row.Invalidate();
                }
            }
            if(InvokeRequired)
            {
                Invoke(update);
                return;
            }
            Invoke(update);
        }


        protected override void AddClientListeners()
        {
            if (Client == null)
                return;

            Client.AddListener<ServerScoreboardUpdate>(scoreBoardUpdate);
        }

        protected override void RemoveClientListeners()
        {
            if (Client == null)
                return;

            Client.RemoveListener<ServerScoreboardUpdate>(scoreBoardUpdate);
        }

        private void scoreboardControl_SizeChanged(object? sender, EventArgs e)
        {
            foreach (var row in _rows)
            {
                row.Width = Width;
            }
        }

        private void scoreBoardUpdate(ClientModel? _, ServerScoreboardUpdate update)
        {
            updateRows(update.Scoreboard);
        }

        private void updateHeight()
        {
            void update()
            {
                var currentY = 0;
                int? squareHeight = null;
                if (_rows.Count == 0)
                {
                    squareHeight = ScoreboardRowControl.SquareSize(Font).Height;
                }
                else
                {
                    foreach (var row in _rows)
                    {
                        row.Location = new Point(0, currentY);
                        if (!squareHeight.HasValue)
                            squareHeight = row.SquareSize().Height;
                        currentY += squareHeight.Value + RowPaddingBottom;
                    }
                }
                Height = Math.Max(2, _rows.Count) * ((squareHeight ?? 0) + RowPaddingBottom);
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void updateRows(TeamScore[] scores)
        {
            void update()
            {
                Controls.Clear();
                _rows.Clear();

                var room = Client?.Room;
                if (room == null)
                    return;

                var userInfo = Client.LocalUser;
                var currentY = 0;

                int? squareHeight = null;
                foreach (var teamScore in scores)
                {
                    var control = new ScoreboardRowControl();

                    control.Color = BingoConstants.GetTeamColor(teamScore.Team);
                    control.CounterText = teamScore.Score.ToString();
                    control.TextColor = BingoConstants.GetTeamColorBright(teamScore.Team);
                    control.Team = teamScore.Team;
                    control.NameText = teamScore.Name;
                    control.Width = Width;
                    control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    control.Font = Font;
                    if (!squareHeight.HasValue)
                        squareHeight = control.SquareSize().Height;
                    control.Height = squareHeight.Value;
                    Controls.Add(control);
                    control.Location = new Point(0, currentY);
                    currentY += squareHeight.Value + RowPaddingBottom;
                    control.DoubleClick += onDoubleClick;
                    if(userInfo != null && userInfo.Team == teamScore.Team)
                    {
                        control.OwnTeam = true;
                    }
                    if (userInfo != null && (userInfo.IsAdmin || userInfo.Team == teamScore.Team))
                    {
                        control.ContextMenuStrip = contextMenuStrip1;
                    }
                    _rows.Add(control);
                }
                updateHeight();
            }
            if (InvokeRequired)
            {
                BeginInvoke(update);
                return;
            }
            update();
        }

        private void changeTeamNameToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (contextMenuStrip1.SourceControl is ScoreboardRowControl scoreboardRow)
            {
                initTeamNameChange(scoreboardRow.Team, scoreboardRow.NameText);
            }
        }

        private void showMyTeamToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            Properties.Settings.Default.GS_ShowPlayerTeam = !Properties.Settings.Default.GS_ShowPlayerTeam;
            Properties.Settings.Default.Save();
        }

        private void default_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.Default.GS_ShowPlayerTeam))
            {
                SetShowPlayerTeam(Properties.Settings.Default.GS_ShowPlayerTeam);
            }
        }

        private void onDoubleClick(object? sender, EventArgs e)
        {
            if (sender is ScoreboardRowControl scoreboardRow)
            {
                initTeamNameChange(scoreboardRow.Team, scoreboardRow.NameText);
            }
        }

        private void initTeamNameChange(int team, string text)
        {
            if (Client == null || Client.Room == null)
                return;

            var userInfo = Client.LocalUser;

            if (userInfo != null && (userInfo.IsAdmin || userInfo.Team == team))
            {
                MainForm? mainForm = MainForm.GetMainForm(this);
                try
                {
                    if (mainForm != null)
                    {
                        mainForm.TopMost = false;
                    }
                    var nameDialog = new SetTeamNameDialog();
                    nameDialog.TeamName = text;
                    var result = nameDialog.ShowDialog(this.Parent);
                    if (result == DialogResult.OK && Client != null)
                    {
                        _ = Client.SendPacketToServer(new Packet(new ClientSetTeamName(team, nameDialog.TeamName)));
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
        }


        internal class ScoreboardRowControl : Control
        {
            private const int PaddingInsideSquare = 4;
            private const int NameTextMarginLeft = 7;
            private const float SquareXOffset = 1f;

            public ScoreboardRowControl()
            {
                NameText = "";
                CounterText = "0";
                Color = Color.Empty;
            }

            public Color Color { get; set; }
            public string CounterText { get; set; }
            public Color TextColor { get; set; }
            public string NameText { get; set; }
            public int Team { get; set; }
            public bool OwnTeam { get; set; }
            public bool CompactLayout { get; set; }

            public void Update(int team, string name, string counter)
            {
                NameText = name;
                CounterText = counter;
                Color = BingoConstants.GetTeamColor(team);
                TextColor = BingoConstants.GetTeamColorBright(team);
            }

            public Size SquareSize()
            {
                return CompactLayout ? CompactSquareSize(Font) : SquareSize(Font);
            }

            public static Size SquareSize(Font font)
            {
                var measure = TextRenderer.MeasureText("00", font);
                return new Size(Math.Max(35, measure.Width + PaddingInsideSquare), Math.Max(22, measure.Height + PaddingInsideSquare));
            }


            public static Size CompactSquareSize(Font font)
            {
                var measure = TextRenderer.MeasureText("00", font);
                return new Size(Math.Max(22, measure.Width + 3), Math.Max(16, measure.Height + 2));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Font == null)
                    return;

                var squareSize = SquareSize();
                Height = squareSize.Height;
                var offsetX = CompactLayout ? 0 : Convert.ToInt32(Height * SquareXOffset);
                var nameMargin = CompactLayout ? Math.Max(2, Convert.ToInt32(Font.Size / 3F)) : NameTextMarginLeft;

                var rect = new Rectangle(offsetX, 0, squareSize.Width, squareSize.Height);
                var nameRect = new Rectangle(offsetX + rect.Width + nameMargin, 0, Math.Max(0, Width - offsetX - rect.Width - nameMargin), rect.Height);
                var nameFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

                var g = e.Graphics;
                using var fillBrush = new SolidBrush(Color);
                using var borderPen = new Pen(Color.FromArgb(96, 0, 0, 0));
                g.FillRectangle(fillBrush, rect);
                g.DrawRectangle(borderPen, new Rectangle(offsetX, 0, rect.Width - 1, rect.Height - 1));
                TextRenderer.DrawText(e, CounterText, Font, rect, Color.White, flags: TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(e, NameText, Font, nameRect, TextColor, flags: nameFlags);

                if (!CompactLayout && Properties.Settings.Default.GS_ShowPlayerTeam && OwnTeam)
                {
                    g.DrawImage(Properties.Resources.player_icon, 0, 0, Height, Height);
                }
            }
        }

    }
}