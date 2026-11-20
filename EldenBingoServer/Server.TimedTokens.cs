using Neto.Shared;

namespace EldenBingoServer;

public partial class Server
{
    private readonly System.Timers.Timer _timedTokenTimer = new(250);
    private readonly SemaphoreSlim _timedTickGate = new(1, 1);
    private bool _timedTimerWired;

    public new void Host()
    {
        base.Host();
        if (!_timedTimerWired)
        {
            _timedTokenTimer.Elapsed += async (_, _) => await tickTimedSquares();
            _timedTimerWired = true;
        }
        _timedTokenTimer.Start();
    }

    private async Task mutateRoomBoard(BingoClientModel? sender, Func<Task> action)
    {
        var room = sender?.Room;
        if (room == null) return;
        await room.BoardMutationGate.WaitAsync();
        try
        {
            if (sender!.Room != room) return;
            await publishTimedSquareUpdates(room);
            if (sender.Room == room) await action();
        }
        finally { room.BoardMutationGate.Release(); }
    }

    private async Task tickTimedSquares()
    {
        if (!Hosting || !await _timedTickGate.WaitAsync(0)) return;
        try
        {
            foreach (var room in _rooms.Values)
            {
                if (room.Match.Board is not ServerBingoBoard { TimedSquares.Count: > 0 } ||
                    !await room.BoardMutationGate.WaitAsync(0)) continue;
                try { await publishTimedSquareUpdates(room); }
                finally { room.BoardMutationGate.Release(); }
            }
        }
        catch (Exception ex) { FireOnError("Could not update timed objectives: " + ex.Message); }
        finally { _timedTickGate.Release(); }
    }

    private async Task publishTimedSquareUpdates(ServerRoom room)
    {
        if (room.Match.Board is not ServerBingoBoard board) return;
        var changed = board.AdvanceTimedSquares();
        if (changed.Length == 0) return;
        var tasks = new List<Task>();
        foreach (var recipient in room.Users.ToArray())
        {
            if (!ReferenceEquals(room.Match.Board, board)) return;
            var packet = new Packet();
            foreach (var index in changed)
                packet.AddObject(new ServerSquareUpdate(board.GetSquareDataForUser(recipient, index), index));
            tasks.Add(SendPacketToClient(packet, recipient.Client));
        }
        await Task.WhenAll(tasks);
    }
}
