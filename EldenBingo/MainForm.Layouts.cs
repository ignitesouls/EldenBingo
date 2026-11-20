using EldenBingo.Settings;

namespace EldenBingo
{
    public partial class MainForm
    {
        internal int LayoutSidebarWidth => _lobbyControl.LayoutSidebarWidth;

        internal void CaptureLayoutGeometry(LayoutProfile p)
        {
            _lobbyControl.CaptureLayoutGeometry(p);
        }

        private void applyLayout(LayoutProfile p)
        {
            var sizeChanged = RestoreBounds.Size != new Size(p.MainWidth, p.MainHeight);
            var positionChanged = p.MainX != -9999 && p.MainY != -9999 && RestoreBounds.Location != new Point(p.MainX, p.MainY);
            if (sizeChanged || positionChanged)
            {
                WindowState = FormWindowState.Normal;
                var location = p.MainX == -9999 || p.MainY == -9999 ? Location : new Point(p.MainX, p.MainY);
                Bounds = FitLayoutBounds(new Rectangle(location, new Size(p.MainWidth, p.MainHeight)));
            }
            _lobbyControl.ApplyLayout(p);
            Invalidate(true);
            Properties.Settings.Default.Save();
        }

        internal static Rectangle FitLayoutBounds(Rectangle bounds)
        {
            var screen = Screen.FromRectangle(bounds).WorkingArea;
            var width = Math.Min(bounds.Width, screen.Width);
            var height = Math.Min(bounds.Height, screen.Height);
            return new Rectangle(Math.Clamp(bounds.X, screen.Left, screen.Right - width),
                Math.Clamp(bounds.Y, screen.Top, screen.Bottom - height), width, height);
        }
    }
}
