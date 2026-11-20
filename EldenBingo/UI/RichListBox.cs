using EldenBingoCommon;

namespace EldenBingo.UI
{
    internal class RichListBox : ListBox
    {
        public RichListBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DrawItem += new DrawItemEventHandler(listBox_DrawItem);
            Properties.Settings.Default.PropertyChanged += settingsChanged;
            Disposed += (_, _) => Properties.Settings.Default.PropertyChanged -= settingsChanged;
        }

        private void settingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.Settings.BoardAppearanceJson) && !IsDisposed) Invalidate();
        }

        private void listBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (sender is RichListBox list && e.Index >= 0 && e.Index < Items.Count)
            {
                using var brush = list.Items[e.Index] is UserInRoom item ? new SolidBrush(EldenBingo.Settings.BoardAppearanceSettings.UserColor(item)) : new SolidBrush(ForeColor);
                e.Graphics.DrawString(list.Items[e.Index].ToString(),
                      e.Font, brush, e.Bounds, StringFormat.GenericDefault);
            }
            e.DrawFocusRectangle();
        }
    }
}
