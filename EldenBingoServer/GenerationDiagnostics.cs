namespace EldenBingoServer;

public sealed record GenerationIssue(string Reason, string Detail);
public sealed class ObjectiveAttemptDiagnostic
{
    public bool Considered { get; internal set; }
    public bool CategoryRejected { get; internal set; }
    public bool RegionRejected { get; internal set; }
    public bool ForcedCenterExcluded { get; internal set; }
    public bool CenterRejected { get; internal set; }
    public bool Chosen { get; internal set; }
}

// One trace per generation attempt. Repeated checks during minimum selection
// set flags rather than inflating rejection counts.
public sealed class GenerationDiagnostics
{
    public Dictionary<int, ObjectiveAttemptDiagnostic> Objectives { get; } = new();
    public List<GenerationIssue> Failures { get; } = new();
    internal HashSet<string> CategoryBlockers { get; } = new();
    internal HashSet<string> RegionBlockers { get; } = new();
    internal ObjectiveAttemptDiagnostic For(int index)
    {
        if (!Objectives.TryGetValue(index, out var diagnostic)) Objectives[index] = diagnostic = new();
        return diagnostic;
    }
}
