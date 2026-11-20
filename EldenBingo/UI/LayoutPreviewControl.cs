using EldenBingo.Settings;
using System.Drawing.Drawing2D;

namespace EldenBingo.UI
{
    internal sealed class LayoutPreviewControl : Control
    {
        public LayoutProfile? Profile { get; set; }

        public LayoutPreviewControl()
        {
            DoubleBuffered = true;
            AccessibleName = "Layout preview";
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var p = Profile;
            if (p == null || Width < 20 || Height < 20) return;
            var style = p.BoardAppearance ?? new BoardAppearance();
            var g = e.Graphics;
            g.Clear(Color.FromArgb(p.Background));
            var scale = Math.Min((Width - 12f) / p.MainWidth, (Height - 12f) / p.MainHeight);
            var left = (Width - p.MainWidth * scale) / 2;
            var top = (Height - p.MainHeight * scale) / 2;
            var sidebar = p.SidebarWidth * scale;
            var availableWidth = (p.MainWidth - p.SidebarWidth - 40) * scale;
            var boardWidth = Math.Min(availableWidth, (p.MainHeight - 150) * scale * BingoControl.AspectRatio);
            if (p.BoardMaximum && p.BoardWidth > 0 && p.BoardHeight > 0)
                boardWidth = Math.Max(20, Math.Min(boardWidth, Math.Min(p.BoardWidth, p.BoardHeight * BingoControl.AspectRatio) * scale));
            var boardHeight = boardWidth / BingoControl.AspectRatio;
            var x = left + 8 + (availableWidth - boardWidth) / 2;
            var y = top + 25 * scale;
            using var sampleFont = new Font(p.FontName, Math.Max(1, p.FontSize * scale), p.FontStyle);
            using var brush = new SolidBrush(Color.FromArgb(18, 20, 20));
            using var text = new SolidBrush(Color.FromArgb(232, 230, 227));
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord };
            string[] samples = { "Defeat Margit", "Collect 5 Golden Seeds", "Reach Altus Plateau", "Upgrade a weapon", "Defeat a dragon" };
            brush.Color = Color.FromArgb(style.BorderColor);
            g.FillRectangle(brush, x, y, boardWidth, boardHeight);
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 5; col++)
            {
                var claimed = (row * 5 + col) % 3 == 0;
                var team = (row * 5 + col) / 3 % style.TeamColors.Length;
                var gap = style.Spacing * scale;
                var rect = new RectangleF(x + col * boardWidth / 5, y + row * boardHeight / 5,
                    Math.Max(1, boardWidth / 5 - gap), Math.Max(1, boardHeight / 5 - gap));
                brush.Color = claimed ? style.ColorForTeam(team) : Color.FromArgb(style.EmptyColor);
                g.FillRectangle(brush, rect);
                using var shadow = new LinearGradientBrush(rect, Color.FromArgb(p.Shadows * 150 / 100, Color.Black), Color.Transparent, 90);
                g.FillRectangle(shadow, rect);
                var sample = samples[(row + col) % samples.Length];
                if (style.BorderWidth > 0)
                {
                    using var border = new Pen(Color.FromArgb(style.BorderColor), Math.Max(.5f, style.BorderWidth * scale));
                    g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
                }
                text.Color = claimed ? style.ClaimedText(team) : Color.FromArgb(style.TextColor);
                var rule = p.KeywordColors?.FirstOrDefault(c => sample.Contains(c.Keyword, StringComparison.OrdinalIgnoreCase));
                if (!claimed && rule != null)
                {
                    var color = Color.FromArgb(rule.Color);
                    var alpha = p.KeywordColorIntensity / 100f;
                    text.Color = Color.FromArgb((int)(color.R * alpha + text.Color.R * (1 - alpha)),
                        (int)(color.G * alpha + text.Color.G * (1 - alpha)), (int)(color.B * alpha + text.Color.B * (1 - alpha)));
                }
                g.DrawString(sample, sampleFont, text, rect, format);
            }
            brush.Color = Color.FromArgb(40, 44, 50);
            text.Color = Color.FromArgb(232, 230, 227);
            g.FillRectangle(brush, left + p.MainWidth * scale - sidebar, y, sidebar - 4, boardHeight);
            using var captionFont = new Font("Segoe UI", 8);
            g.DrawString("Players", captionFont, text, new RectangleF(left + p.MainWidth * scale - sidebar, y + 4, sidebar - 4, 30), format);
            g.DrawRectangle(Pens.Gray, left, top, p.MainWidth * scale, p.MainHeight * scale);
        }
    }
}
