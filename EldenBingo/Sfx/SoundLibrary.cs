using EldenBingo.Util;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using EldenBingo.Settings;

namespace EldenBingo.Sfx
{
    public enum SoundType
    {
        SquareClaimedOther,
        SquareClaimedOwn,
        SquareUnclaimedOther,
        SquareUnclaimedOwn,
        Bingo,
        MatchStart,
        MatchPause,
        MatchFinish,
    }

    public class AudioDevice
    {
        public string Name { get; private set; }
        public string Id { get; private set; }

        public AudioDevice(string Name, string Id)
        {
            this.Name = Name;
            this.Id = Id;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class SoundLibrary : IDisposable, IMMNotificationClient
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;

        private static string SfxPath = Path.Combine(AppContext.BaseDirectory, "Sfx");
        private static readonly string?[] AudioFiles = new string?[]
        {
            "square_claimed_other.wav",
            "square_claimed_own.wav",
            "square_unclaimed_other.wav",
            "square_unclaimed_own.wav",
            "bingo.wav",
            null, // Start, pause, and finish are silent until a custom file is chosen.
            null,
            null
        };

        private readonly CachedSoundSampleProvider?[] _sounds;
        private readonly WasapiOut?[] _players;
        private readonly CachedSoundSampleProvider?[] _customSounds = new CachedSoundSampleProvider?[AudioFiles.Length];
        private readonly string?[] _customPaths = new string?[AudioFiles.Length];
        private readonly DateTime[] _customModified = new DateTime[AudioFiles.Length];

        internal static bool HasBundledSound(SoundType type) => (int)type >= 0 && (int)type < AudioFiles.Length && AudioFiles[(int)type] != null;

        internal static void ValidateCustomFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The sound file could not be found.", path);
            using var reader = new AudioFileReader(path);
            if (reader.TotalTime <= TimeSpan.Zero || reader.Read(new float[1024], 0, 1024) == 0)
                throw new InvalidDataException("Choose an audio file containing playable sound.");
        }

        private MMDevice? _currentDevice = null;
        private string _forceDeviceId = string.Empty;

        public SoundLibrary()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            _deviceEnumerator.RegisterEndpointNotificationCallback(this);
            _sounds = new CachedSoundSampleProvider[AudioFiles.Length];
            _players = new WasapiOut[AudioFiles.Length];
            for (int i = 0; i < AudioFiles.Length; i++)
            {
                try
                {
                    if (AudioFiles[i] == null) continue;
                    var path = Path.Combine(SfxPath, AudioFiles[i]!);
                    if (File.Exists(path))
                    {
                        _sounds[i] = new CachedSoundSampleProvider(new CachedSound(path));
                    }

                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    _sounds[i] = null;
                }
            }
            initAudioDevice();
        }

        public IList<AudioDevice> GetAudioDevices()
        {
            var list = new List<AudioDevice>
            {
                new AudioDevice("System Default Output", string.Empty)
            };
            foreach (var device in _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add(new AudioDevice(device.FriendlyName, device.ID));
            }
            return list;
        }

        public void SetAudioDevice(string id)
        {
            _forceDeviceId = id;
            _currentDevice = null;
        }

        private MMDevice defaultAudioDevice()
        {
            return _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        public void PlaySound(SoundType type, int? volume = null, int? eventVolume = null, string? customFile = null)
        {
            var kind = type switch { SoundType.Bingo => MatchSoundEvent.Bingo, SoundType.MatchStart => MatchSoundEvent.Start,
                SoundType.MatchPause => MatchSoundEvent.Pause, SoundType.MatchFinish => MatchSoundEvent.Finish, _ => MatchSoundEvent.Claim };
            var preferences = EventSoundPreferences.Parse(Properties.Settings.Default.EventSoundsJson);
            var effectiveVolume = EventSoundPreferences.EffectiveVolume(volume ?? Properties.Settings.Default.SoundVolume, eventVolume ?? preferences.VolumeFor(kind));
            if (effectiveVolume == 0 || volume == null && !Properties.Settings.Default.PlaySounds) return;
            int i = (int)type;
            try
            {
                if (i >= 0 && i < _sounds.Length)
                {
                    var s = _sounds[i];
                    var path = customFile ?? preferences.Get(kind).FilePath;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        if (!File.Exists(path)) throw new FileNotFoundException("Custom sound file is missing.", path);
                        var modified = File.GetLastWriteTimeUtc(path);
                        if (_customSounds[i] == null || _customPaths[i] != path || _customModified[i] != modified)
                        {
                            _customSounds[i] = new CachedSoundSampleProvider(new CachedSound(path));
                            _customPaths[i] = path; _customModified[i] = modified;
                        }
                        s = _customSounds[i];
                    }
                    if (s != null)
                    {
                        if (_currentDevice == null) initAudioDevice();
                        s.ResetPosition();
                        s.SetVolume(effectiveVolume * 0.01f);

                        WasapiOut? p = _players[i];
                        p?.Stop();
                        p?.Dispose();
                        p = new WasapiOut(_currentDevice, AudioClientShareMode.Shared, false, 50);
                        p.Init(s);
                        p.Play();
                        _players[i] = p;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public void Dispose()
        {
            foreach (var p in _players)
            {
                p?.Dispose();
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            //throw new NotImplementedException();
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            //throw new NotImplementedException();
        }

        public void OnDeviceRemoved(string deviceId)
        {
            //throw new NotImplementedException();
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                MainForm.Instance?.PrintToConsole($"Default audio output device changed to: {defaultDeviceId}", Color.BlanchedAlmond, true);
                _currentDevice = null;
            }
        }

        private void initAudioDevice()
        {
            if (_currentDevice == null && _forceDeviceId != string.Empty)
            {
                _currentDevice = _deviceEnumerator.GetDevice(_forceDeviceId);
            }
            //If still null then the device wasn't found so use the default device instead
            if (_currentDevice == null)
            {
                _currentDevice = defaultAudioDevice();
            }
            for (int i = 0; i < _players.Length; ++i)
            {
                _players[i]?.Stop();
                _players[i]?.Dispose();
                _players[i] = null;
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            //throw new NotImplementedException();
        }

        private class CachedSound
        {
            public byte[] AudioData { get; private set; }
            public WaveFormat WaveFormat { get; private set; }
            public CachedSound(string audioFileName)
            {
                using (var audioFileReader = new AudioFileReader(audioFileName))
                {
                    // TODO: could add resampling in here if required
                    WaveFormat = audioFileReader.WaveFormat;
                    var wholeFile = new List<byte>((int)(audioFileReader.Length));
                    var readBuffer = new byte[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels * 4];
                    int bytesRead;
                    while ((bytesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                    {
                        wholeFile.AddRange(readBuffer.Take(bytesRead));
                    }
                    AudioData = wholeFile.ToArray();
                }
            }
        }

        private class CachedSoundSampleProvider : IWaveProvider
        {
            private readonly CachedSound cachedSound;
            private long position;

            private float _volume;
            public CachedSoundSampleProvider(CachedSound cachedSound)
            {
                this.cachedSound = cachedSound;
            }

            public void ResetPosition()
            {
                position = 0;
            }

            public void SetVolume(float volume)
            {
                _volume = Math.Clamp(volume, 0f, 1f);
            }

            public WaveFormat WaveFormat { get { return cachedSound.WaveFormat; } }

            public int Read(byte[] buffer, int offset, int count)
            {
                var availableSamples = cachedSound.AudioData.Length - position;
                var samplesToCopy = Math.Min(availableSamples, count);

                int destOffset = offset;
                for (int sourceSample = 0; sourceSample < samplesToCopy; sourceSample += 4)
                {
                    var span = new ReadOnlySpan<byte>(cachedSound.AudioData, (int)position + sourceSample, 4);
                    float sample = BitConverter.ToSingle(span) * _volume;
                    var floatBytes = BitConverter.GetBytes(sample);
                    buffer[destOffset + 0] = floatBytes[0];
                    buffer[destOffset + 1] = floatBytes[1];
                    buffer[destOffset + 2] = floatBytes[2];
                    buffer[destOffset + 3] = floatBytes[3];
                    destOffset += 4;
                }
                //Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
                position += samplesToCopy;
                return (int)samplesToCopy;
            }
        }
    }
}
