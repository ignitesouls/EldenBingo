using EldenBingoCommon;

namespace EldenBingo;

public partial class Client
{
    // Private presentation state, shared only between this client's board windows.
    private readonly HashSet<int> _dimmedSquares = new();
    internal event Action? LocalDimmingChanged;

    internal bool HasLocalDim(int index)
    {
        lock (_dimmedSquares) return _dimmedSquares.Contains(index);
    }

    internal void ToggleLocalDim(int index)
    {
        if (Room?.Match is not { Board: { } board } match || match.MatchStatus < MatchStatus.Preparation ||
            index < 0 || index >= board.SquareCount) return;
        lock (_dimmedSquares)
            if (!_dimmedSquares.Add(index)) _dimmedSquares.Remove(index);
        LocalDimmingChanged?.Invoke();
    }

    private void ResetLocalDimming()
    {
        lock (_dimmedSquares) _dimmedSquares.Clear();
        LocalDimmingChanged?.Invoke();
    }
}
