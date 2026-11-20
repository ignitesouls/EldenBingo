using EldenBingo.UI;

namespace EldenBingo;

public partial class MainForm
{
    private LobbyCodeCopyButton _copyLobbyButton = null!;
    private Panel _lobbyStatusGroup = null!;

    private void initializeLobbyStatus()
    {
        _clientStatusTextBox.Visible = true;
        _processMonitorStatusTextBox.Visible = false;
        panel1.Visible = true;
        // The form background is customizable (including pure black). The
        // native status text has a system surface, so give the whole footer
        // that same explicit surface instead of inheriting the form color.
        panel1.BackColor = SystemColors.Control;
        panel1.ForeColor = SystemColors.ControlText;
        panel1.Height = Math.Max(26, _clientStatusTextBox.PreferredHeight);
        _lobbyStatusGroup = new Panel { BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText };
        _copyLobbyButton = new LobbyCodeCopyButton { Width = 32, BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText, UseVisualStyleBackColor = false };
        _clientStatusTextBox.BackColor = SystemColors.Control;
        _clientStatusTextBox.ForeColor = SystemColors.ControlText;
        _clientStatusTextBox.Dock = DockStyle.None;
        _clientStatusTextBox.BorderStyle = BorderStyle.None;
        _clientStatusTextBox.AutoSize = false;
        _lobbyStatusGroup.Controls.Add(_clientStatusTextBox);
        _lobbyStatusGroup.Controls.Add(_copyLobbyButton);
        panel1.Controls.Add(_lobbyStatusGroup);
        panel1.SizeChanged += (_, _) => layoutLobbyStatus();
        _clientStatusTextBox.TextChanged += (_, _) => layoutLobbyStatus();
        Load += (_, _) => refreshLobbyStatus();
    }

    private void layoutLobbyStatus()
    {
        if (_lobbyStatusGroup == null) return;
        var iconWidth = _copyLobbyButton.Enabled ? 32 : 0;
        var textWidth = TextRenderer.MeasureText(_clientStatusTextBox.Text, _clientStatusTextBox.Font).Width + 8;
        var width = Math.Min(panel1.ClientSize.Width, textWidth + iconWidth);
        _lobbyStatusGroup.Bounds = new Rectangle(Math.Max(0, (panel1.ClientSize.Width - width) / 2), 0, width, panel1.ClientSize.Height);
        _clientStatusTextBox.Bounds = new Rectangle(0, Math.Max(0, (panel1.Height - _clientStatusTextBox.Font.Height) / 2), Math.Max(1, width - iconWidth), _clientStatusTextBox.Font.Height + 2);
        _copyLobbyButton.Bounds = new Rectangle(Math.Max(0, width - iconWidth), 0, iconWidth, panel1.ClientSize.Height);
    }

    private void refreshLobbyStatus()
    {
        if (_client == null || IsDisposed || Disposing) return;
        _clientStatusTextBox.Text = _client.GetConnectionStatusString();
        _copyLobbyButton.SetLobby(_client.IsConnected ? _client.Room?.Name : null);
        _copyLobbyButton.Visible = _copyLobbyButton.Enabled;
        layoutLobbyStatus();
        refreshQualityOfLifeButtons();
    }
}
