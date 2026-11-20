using EldenBingo.Export;

namespace EldenBingo.UI;

internal partial class LobbyControl
{
    private readonly BoardExportTracker _exportTracker = new();
    private readonly ToolTip _liveExportTip = new();
    private readonly object _exportSessionGate = new();
    private long _exportRoomGeneration;

    private void initializeExports()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 28, RowCount = 1, ColumnCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _exportJsonButton.Dock = DockStyle.Fill;
        _exportJsonButton.Margin = Padding.Empty;
        panel.Controls.Add(_exportJsonButton);
        initializeLiveShare(panel);
        _clientList.Controls.Add(panel);
        panel.SendToBack();
        HandleCreated += (_, _) => shareStatus(false, _livePublisher == null ? "Local URL off" : "Sharing live JSON");
        Disposed += (_, _) =>
        {
            stopExportsForRoomChange();
            _timer?.Dispose();
            Properties.Settings.Default.PropertyChanged -= default_PropertyChanged;
            if (Client != null) RemoveClientListeners();
            _liveExportTip.Dispose();
        };
    }

    private void stopExportsForRoomChange()
    {
        lock (_exportSessionGate) _exportRoomGeneration++;
        stopLiveSharingForRoomChange();
    }

    private void exportSquareUpdated(Neto.Shared.ClientModel? _, ServerSquareUpdate packet)
    {
        if (Client?.Room != null) _exportTracker.SetSquare(packet);
    }
}
