using EldenBingoCommon;
using System.Text.Json;

namespace EldenBingo.Settings;

internal enum MatchSoundEvent { Claim, Bingo, Start, Pause, Finish }
internal sealed class EventSoundOption
{
    public bool Enabled { get; set; } = true;
    public int Volume { get; set; } = 100;
    public string FilePath { get; set; } = "";
}
internal sealed class EventSoundPreferences
{
    public Dictionary<MatchSoundEvent, EventSoundOption> Events { get; set; } = new();
    public bool IsValid => Events != null && Events.All(p => Enum.IsDefined(typeof(MatchSoundEvent), p.Key) && p.Value != null && p.Value.Volume is >= 0 and <= 100);
    public EventSoundOption Get(MatchSoundEvent kind)
    {
        if (!Events.TryGetValue(kind, out var value)) Events[kind] = value = new();
        return value;
    }
    public int VolumeFor(MatchSoundEvent kind) { var value = Get(kind); return value.Enabled ? value.Volume : 0; }
    public static int EffectiveVolume(int master, int level) => (int)Math.Round(Math.Clamp(master, 0, 100) * Math.Clamp(level, 0, 100) / 100d);
    public static EventSoundPreferences Parse(string json)
    {
        try { var value = JsonSerializer.Deserialize<EventSoundPreferences>(json); return value?.IsValid == true ? value : new(); }
        catch (JsonException) { return new(); }
    }
}

internal sealed class MatchSoundTracker
{
    private readonly object _gate = new();
    private string? _room;
    private MatchStatus _status;
    private bool _paused;
    public void Reset(string? room, MatchStatus status, bool paused) { lock (_gate) { _room = room; _status = status; _paused = paused; } }
    public MatchSoundEvent? Observe(string? room, MatchStatus status, bool paused)
    {
        lock (_gate)
        {
            MatchSoundEvent? sound = null;
            if (room != null && room == _room)
            {
                if (status == MatchStatus.Finished && _status != MatchStatus.Finished) sound = MatchSoundEvent.Finish;
                else if (status == MatchStatus.Running && _status != MatchStatus.Running) sound = MatchSoundEvent.Start;
                else if (status is > MatchStatus.NotRunning and < MatchStatus.Finished && paused != _paused) sound = MatchSoundEvent.Pause;
            }
            Reset(room, status, paused); return sound;
        }
    }
}
