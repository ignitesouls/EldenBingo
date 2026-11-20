using EldenBingo.Settings;
using EldenBingo.Sfx;
using EldenBingoCommon;
using Neto.Shared;

namespace EldenBingo;

public partial class MainForm
{
    private readonly MatchSoundTracker _matchSoundTracker = new();
    private void matchSoundUpdated(ClientModel? _, ServerMatchStatusUpdate packet)
    {
        var sound = _matchSoundTracker.Observe(_client.Room?.Name, packet.MatchStatus, packet.Paused);
        if (sound == null || !Properties.Settings.Default.PlaySounds) return;
        _sounds.PlaySound(sound switch { MatchSoundEvent.Start => SoundType.MatchStart, MatchSoundEvent.Pause => SoundType.MatchPause, _ => SoundType.MatchFinish });
    }
}
