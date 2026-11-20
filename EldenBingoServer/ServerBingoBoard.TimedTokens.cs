using EldenBingoCommon;
using Newtonsoft.Json;

namespace EldenBingoServer;

public partial class ServerBingoBoard
{
    // Server-only state is persisted, while clients receive ordinary square updates.
    [JsonProperty]
    public Dictionary<int, TimedSquareState> TimedSquares { get; set; } = new();

    public int[] AdvanceTimedSquares()
    {
        if (Room.Match.MatchStatus != MatchStatus.Running || Room.Match.Paused || TimedSquares.Count == 0)
            return Array.Empty<int>();
        var changed = new List<int>();
        var seconds = Math.Max(0, Room.Match.MatchSeconds);
        foreach (var (index, state) in TimedSquares)
        {
            lock (CheckStatus[index])
            {
                if (state.Frozen) continue;
                // Also protect claims restored from disk or made by older server code.
                if (CheckStatus[index].IsChecked()) state.Frozen = true;
                else state.ElapsedSeconds = Math.Max(state.ElapsedSeconds, seconds);
                if (renderTimedSquare(index, state)) changed.Add(index);
            }
        }
        return changed.ToArray();
    }

    private void freezeTimedSquare(int index)
    {
        if (!TimedSquares.TryGetValue(index, out var state) || state.Frozen) return;
        if (Room.Match.MatchStatus == MatchStatus.Running && !Room.Match.Paused)
            state.ElapsedSeconds = Math.Max(state.ElapsedSeconds, Math.Max(0, Room.Match.MatchSeconds));
        state.Frozen = true;
        renderTimedSquare(index, state);
    }

    private bool renderTimedSquare(int index, TimedSquareState state)
    {
        var text = state.TextAt(state.ElapsedSeconds);
        var tooltip = state.Tooltip();
        if (Squares[index].Text == text && Squares[index].Tooltip == tooltip) return false;
        Squares[index] = Squares[index] with { Text = text, Tooltip = tooltip };
        return true;
    }

    private void resumeTimedSquare(int index)
    {
        if (!TimedSquares.TryGetValue(index, out var state)) return;
        state.Frozen = false;
        if (Room.Match.MatchStatus == MatchStatus.Running)
            state.ElapsedSeconds = Math.Max(state.ElapsedSeconds, Math.Max(0, Room.Match.MatchSeconds));
        renderTimedSquare(index, state);
    }
}
