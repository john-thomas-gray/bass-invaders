using UnityEngine;

namespace CandyCruisers
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class GameplayMusicPlayer : MonoBehaviour
    {
        public const string DefaultResourceName = "DiscoDescent";
        public const string MenuResourceName = "AnotherJoe";
        public const string CountingResourceName = "CountingMetronome";
        public const int BeatsPerMeasure = 4;
        // Measure alignment is separate from the individual-beat clock.
        public const int DownbeatOffsetBeats = 1;
        [System.Serializable]
        public sealed class Song
        {
            public string resourceName;
            [Min(1)] public float beatsPerMinute;
            public float beatOffsetSeconds;
            [Range(0, 11), Tooltip("Relative-major tonic: C=0, C#=1, ... B=11. For minor songs use their relative major.")]
            public int relativeMajorTonic;
            [Range(0, 3)] public int downbeatOffsetBeats = DownbeatOffsetBeats;
            public Song(string name, float tempo, float offset, int tonic = 0, int downbeat = DownbeatOffsetBeats)
            { resourceName = name; beatsPerMinute = tempo; beatOffsetSeconds = offset; relativeMajorTonic = tonic;
                downbeatOffsetBeats = downbeat; }
        }
        [SerializeField] private Song[] soundtrack = CreateDefaultSoundtrack();
        public static string[] DefaultSoundtrackNames => System.Array.ConvertAll(CreateDefaultSoundtrack(), song => song.resourceName);
        private static Song[] CreateDefaultSoundtrack() => new[] {
            new Song("DiscoDescent", 115.03f, .08f, 0), new Song("GameplayMusic", 142f, .09f, 8),
            new Song("AnotherJoe", 140f, .1f, 2), new Song("PotentialForAnything", 125f, 0f, 5),
            new Song("Skanska", 128f, 0f, 0),
            new Song("TheThirdKind", 139.67f, 0f, 0),
            new Song("DownToEarthPart1", 143.55f, .10449f, 0),
            new Song("UntilICollapse", 139.67f, .09288f, 0),
            new Song("WarOnActivism", 143.55f, .19737f, 0),
            new Song("IntergalacticEmotionalBreakdown", 143.55f, .26703f, 0),
            new Song("ShootingRobotsInSpace", 136f, .62694f, 0),
            new Song("VertexStage1", 143.55f, .22059f, 0),
            new Song("Contact", 123.05f, .05805f, 2, 0),
            new Song("ForWhomTheKnellTolls", 140f, .09288f, 11),
            new Song("AColdSweat", 155f, .09288f, 0),
            new Song("HeartOfTheCrypt", 145f, .09288f, 3),
            new Song("StyxAndStones", 130f, .09288f, 4)
        };
        private static readonly Song CountingSong = new Song(CountingResourceName, DefaultBeatsPerMinute, .08f, 0, 0);
        private AudioClip countingTrack;
        private AudioClip CountingTrack => countingTrack != null ? countingTrack :
            countingTrack = Resources.Load<AudioClip>(CountingResourceName);
        private readonly System.Collections.Generic.List<int> remainingSongs = new System.Collections.Generic.List<int>();
        private readonly System.Random shuffle = new System.Random();
        private readonly System.Collections.Generic.Dictionary<AudioClip, SongBeatMap> beatMaps = new();
        private int songIndex = -1;
        private bool pausedSource;
        private bool wasInMainMenu;
        private bool menuShuffleStarted;
        private AudioClip carriedMenuTrack;
        private LevelDefinition appliedLevelDefinition;
        private bool levelSoundtrackApplied;
        private System.Collections.Generic.HashSet<string> levelTracks;
        private bool hasSelectedLevelTracks;
        private bool TrackAllowed(string name) => name != CountingResourceName &&
            SongSelectionSettings.IsSelected(name) &&
            (levelTracks == null || !hasSelectedLevelTracks || levelTracks.Contains(name));
        private bool UsesCustomTrack => track != null && TrackAllowed(track.name);
        public int TrackCount => soundtrack.Length;
        [SerializeField, Range(0, 11)] private int customTrackRelativeMajorTonic;
        private Song CurrentSong
        {
            get
            {
                // Beat timing and key follow the clip actually playing, including the menu carryover.
                var clip = Source.clip;
                if (clip != null && clip.name == CountingResourceName) return CountingSong;
                if (clip != null)
                    foreach (var song in soundtrack)
                        if (song.resourceName == clip.name) return song;
                return null;
            }
        }
        public int CurrentRelativeMajorTonic => CurrentSong?.relativeMajorTonic ?? customTrackRelativeMajorTonic;
        public int CurrentDownbeatOffsetBeats => PreviewTime.HasValue ? DownbeatOffsetBeats : CurrentSong?.downbeatOffsetBeats ?? DownbeatOffsetBeats;
        public float? PreviewTime { get; set; }
        public float PreviewTempo { get; set; } = 120;
        private float FixedBeatDuration => 60f / Mathf.Max(1, PreviewTime.HasValue ? PreviewTempo : CurrentSong?.beatsPerMinute ?? beatsPerMinute);
        public float CurrentTempo => 60f / BeatDuration * (session != null ? session.MusicTempoMultiplier : 1);
        public float CurrentBeatOffset => PreviewTime.HasValue ? 0 : CurrentBeatMap?.FirstBeatTime ?? CurrentSong?.beatOffsetSeconds ?? beatOffsetSeconds;
        public bool UsesBeatMap => CurrentBeatMap != null;
        private SongBeatMap CurrentBeatMap
        {
            get
            {
                if (PreviewTime.HasValue) return null;
                var clip = Source.clip;
                if (clip == null) return null;
                var song = CurrentSong;
                if (song == null) return null;
                if (beatMaps.TryGetValue(clip, out var cached)) return cached;
                SongBeatMap map = null;
                var asset = Resources.Load<TextAsset>("BeatMaps/" + song.resourceName);
                if (asset != null && !SongBeatMap.TryParse(asset.text, clip.length, out map))
                    Debug.LogWarning("Invalid beat map for " + clip.name + "; using its fixed tempo.", this);
                beatMaps[clip] = map;
                return map;
            }
        }
        public const float DefaultBeatsPerMinute = 115.03f;
        [SerializeField, Min(1)] private float beatsPerMinute = DefaultBeatsPerMinute;
        [SerializeField] private float beatOffsetSeconds = .08f;
        public const float DefaultVolume = .10f;
        public const float DeathMinimumPitch = .04f;
        [SerializeField] private GameSession session;
        [SerializeField] private AudioClip track;
        [SerializeField] private AudioClip menuTrack;
        [SerializeField, Range(0, 1)] private float volume = DefaultVolume;
        [SerializeField] private bool muted;
        private AudioSource source;
        private AudioClip resourceTrack;
        private AudioClip resourceMenuTrack;
        private bool wasInRun;
        private bool warnedFailedLoad;

        public AudioSource Source { get { EnsureSource(); return source; } }
        public bool BeatClockRunning => PreviewTime.HasValue || Source.isPlaying;
        public float PlaybackSeconds => PreviewTime ?? (Source.clip != null ?
            Source.timeSamples / (float)Mathf.Max(1, Source.clip.frequency) : 0);
        // These durations use song/game seconds: both playback and game time receive
        // the same tempo multiplier, so beat-driven actions must not apply it twice.
        public float BeatDuration => CurrentBeatMap?.DurationAtTime(PlaybackSeconds) ?? FixedBeatDuration;
        public static float BeatPulse(float beatPosition, bool offbeat = false) =>
            Mathf.Pow(Mathf.Max(0, Mathf.Cos((beatPosition - (offbeat ? .5f : 0)) * Mathf.PI * 2)), 4);
        public float BeatPosition => BeatPositionAtTime(PlaybackSeconds);
        public int CurrentBeatInMeasure => BeatInMeasure(BeatPosition, CurrentDownbeatOffsetBeats);
        public static int BeatInMeasure(float beat, int downbeatOffset) =>
            ((Mathf.FloorToInt(beat) - downbeatOffset) % BeatsPerMeasure + BeatsPerMeasure) % BeatsPerMeasure + 1;
        public bool TryAdvancePlayback(AudioClip clip, ref float previousSeconds, out float elapsed)
        {
            elapsed = 0;
            float now = PlaybackSeconds;
            // Audio buffers can report a small backwards step. Only a real seek releases the clock.
            if (clip == null || Source.clip != clip || !BeatClockRunning || now < previousSeconds - .05f) return false;
            now = Mathf.Max(previousSeconds, now);
            elapsed = now - previousSeconds;
            previousSeconds = now;
            return true;
        }
        public float BeatPositionAtTime(float seconds) => CurrentBeatMap?.BeatAtTime(seconds) ??
            (seconds - CurrentBeatOffset) / FixedBeatDuration;
        public float SecondsAtBeat(float beat) => CurrentBeatMap?.TimeAtBeat(beat) ??
            CurrentBeatOffset + beat * FixedBeatDuration;
        public float CompletedBeatDuration(int beat) => SecondsAtBeat(beat) - SecondsAtBeat(beat - 1);
        public float SecondsForBeats(float count)
        {
            float time = PlaybackSeconds;
            return Mathf.Max(0, SecondsAtBeat(BeatPositionAtTime(time) + Mathf.Max(0, count)) - time);
        }
        public float SecondsToNextBeat => Mathf.Max(0, SecondsAtBeat(Mathf.Floor(BeatPosition) + 1) - PlaybackSeconds);
        public float[] DurationsForBeats(int count)
        {
            var durations = new float[Mathf.Max(0, count)];
            float time = PlaybackSeconds;
            float beat = BeatPositionAtTime(time);
            for (int i = 0; i < durations.Length; i++)
            {
                float next = SecondsAtBeat(beat + i + 1);
                durations[i] = next - time;
                time = next;
            }
            return durations;
        }
        public AudioClip MenuTrack => MetronomeSettings.VoiceEnabled ? CountingTrack :
            menuTrack != null && SongSelectionSettings.IsSelected(menuTrack.name) ? menuTrack :
            resourceMenuTrack != null ? resourceMenuTrack : SelectMenuTrack();
        public AudioClip Track
        {
            get
            {
                if (MetronomeSettings.VoiceEnabled) return CountingTrack;
                if (carriedMenuTrack != null) return carriedMenuTrack;
                if (UsesCustomTrack) return track;
                if (resourceTrack == null) SelectNextTrack();
                return resourceTrack;
            }
        }
        public bool ContainsTrack(string name) => name != CountingResourceName &&
            System.Array.Exists(soundtrack, song => song.resourceName == name);
        public string[] SoundtrackNames => System.Array.ConvertAll(soundtrack, song => song.resourceName);
        public bool IsTrackAllowedForLevel(string name) => TrackAllowed(name);
        public bool SetSongSelected(string name, bool selected)
        {
            if (!SongSelectionSettings.TrySetSelected(name, selected, SoundtrackNames)) return false;
            RefreshSongSelection();
            UpdatePlayback();
            return true;
        }

        private void RefreshSongSelection()
        {
            hasSelectedLevelTracks = levelTracks != null && System.Array.Exists(soundtrack,
                song => levelTracks.Contains(song.resourceName) && SongSelectionSettings.IsSelected(song.resourceName));
            remainingSongs.Clear();
            if (carriedMenuTrack != null && !TrackAllowed(carriedMenuTrack.name)) carriedMenuTrack = null;
            if (resourceTrack != null && !TrackAllowed(resourceTrack.name)) resourceTrack = null;
            if (resourceMenuTrack != null && !SongSelectionSettings.IsSelected(resourceMenuTrack.name)) resourceMenuTrack = null;
        }
        public void SelectNextTrack()
        {
            carriedMenuTrack = null;
            TrySelectNextResourceTrack();
        }

        private bool TrySelectNextResourceTrack()
        {
            resourceTrack = null;
            if (soundtrack.Length == 0) return false;
            for (int attempts = 0; attempts < soundtrack.Length; attempts++)
            {
                if (remainingSongs.Count == 0) RefillShuffleBag();
                if (remainingSongs.Count == 0) return false;
                int index = remainingSongs[0]; remainingSongs.RemoveAt(0);
                var clip = Resources.Load<AudioClip>(soundtrack[index].resourceName);
                if (clip == null) continue;
                songIndex = index;
                resourceTrack = clip;
                return true;
            }
            return false;
        }

        private void RefillShuffleBag()
        {
            remainingSongs.Clear();
            for (int i = 0; i < soundtrack.Length; i++)
                if (TrackAllowed(soundtrack[i].resourceName)) remainingSongs.Add(i);
            for (int i = remainingSongs.Count - 1; i > 0; i--)
            {
                int j = shuffle.Next(i + 1);
                int swap = remainingSongs[i]; remainingSongs[i] = remainingSongs[j]; remainingSongs[j] = swap;
            }
            if (remainingSongs.Count > 1 && remainingSongs[0] == songIndex)
            { int swap = remainingSongs[0]; remainingSongs[0] = remainingSongs[1]; remainingSongs[1] = swap; }
        }

        private AudioClip SelectMenuTrack()
        {
            if (soundtrack.Length > 0)
            {
                int start = shuffle.Next(soundtrack.Length);
                for (int offset = 0; offset < soundtrack.Length; offset++)
                {
                    int index = (start + offset) % soundtrack.Length;
                    if (soundtrack[index].resourceName == CountingResourceName ||
                        !SongSelectionSettings.IsSelected(soundtrack[index].resourceName)) continue;
                    resourceMenuTrack = Resources.Load<AudioClip>(soundtrack[index].resourceName);
                    if (resourceMenuTrack == null) continue;
                    songIndex = index;
                    return resourceMenuTrack;
                }
            }
            songIndex = System.Array.FindIndex(soundtrack, song => song.resourceName == MenuResourceName);
            return resourceMenuTrack = SongSelectionSettings.IsSelected(MenuResourceName) ?
                Resources.Load<AudioClip>(MenuResourceName) : null;
        }
        public bool InGameplayRun => session != null &&
            (session.State == GameSession.RunState.Playing || session.State == GameSession.RunState.Refilling);
        public bool ShouldPlayMusic => session != null && !session.IsPaused &&
            !session.OpeningWarningActive &&
            (InGameplayRun || session.State == GameSession.RunState.MainMenu ||
                session.State == GameSession.RunState.Dying && session.GameOverMusicGain > 0);

        public void Configure(GameSession gameSession)
        {
            session = gameSession;
            UpdatePlayback();
        }

        private void Awake() => EnsureSource();
        private void OnEnable()
        {
            SongSelectionSettings.EnsureSelection(SoundtrackNames);
            RefreshSongSelection();
            if (session == null) session = GetComponent<GameSession>() ?? FindFirstObjectByType<GameSession>();
            UpdatePlayback();
        }
        private void Update()
        {
            if ((Application.isEditor || Debug.isDebugBuild) && Input.GetKeyDown(KeyCode.F8)) SkipCurrentSong();
            UpdatePlayback();
        }
        public bool PlayCountingMetronome() => SetVoiceMetronome(true);
        public bool SetVoiceMetronome(bool enabled)
        {
            if (session == null || session.OpeningWarningActive ||
                session.State != GameSession.RunState.MainMenu && !InGameplayRun) return false;
            if (enabled && CountingTrack == null) return false;
            MetronomeSettings.VoiceEnabled = enabled;
            UpdatePlayback();
            return true;
        }
        public bool SkipCurrentSong()
        {
            if (MetronomeSettings.VoiceEnabled || !(Application.isEditor || Debug.isDebugBuild) || session == null ||
                session.OpeningWarningActive || soundtrack.Length < 2) return false;
            bool inMenu = session.State == GameSession.RunState.MainMenu;
            if (inMenu ? menuTrack != null : !InGameplayRun || UsesCustomTrack) return false;
            if (inMenu && !menuShuffleStarted)
            {
                songIndex = System.Array.FindIndex(soundtrack, song => song.resourceName == MenuTrack?.name);
                RefillShuffleBag();
                remainingSongs.Remove(songIndex);
                menuShuffleStarted = true;
            }
            carriedMenuTrack = null;
            if (!TrySelectNextResourceTrack()) return false;
            if (inMenu) resourceMenuTrack = resourceTrack;
            UpdatePlayback();
            return true;
        }
        private void OnDisable()
        {
            if (source != null) source.Pause();
            wasInRun = false;
        }
        private void OnValidate()
        {
            beatMaps.Clear();
            EnsureSource();
            ApplySourceSettings();
        }

        public void UpdatePlayback()
        {
            EnsureSource();
            if (PreviewTime.HasValue) return;
            if (session == null) session = GetComponent<GameSession>();
            ApplyLevelSoundtrack();
            if (session != null && session.OpeningWarningActive)
            {
                source.Stop();
                source.clip = null;
                wasInRun = false;
                wasInMainMenu = false;
                pausedSource = false;
                return;
            }
            if (session != null && (session.State == GameSession.RunState.Dying || session.State == GameSession.RunState.GameOver))
            {
                // Keep the current recording in place; the captured fade clock drives the tape stop.
                ApplySourceSettings();
                if (session.GameOverMusicGain <= 0)
                {
                    StopPlayback();
                    pausedSource = false;
                }
                return;
            }
            bool inMenu = session != null && session.State == GameSession.RunState.MainMenu;
            if (wasInMainMenu && InGameplayRun && source.clip != null && TrackAllowed(source.clip.name))
            {
                // Adopt the menu song without stopping, seeking or replaying its AudioSource.
                carriedMenuTrack = source.clip;
                resourceTrack = null;
                songIndex = System.Array.FindIndex(soundtrack, song => song.resourceName == carriedMenuTrack.name);
                RefillShuffleBag();
                remainingSongs.Remove(songIndex);
            }
            wasInMainMenu = inMenu;
            var clip = inMenu ? MenuTrack : Track;
            if (clip == null)
            {
                source.Stop();
                source.clip = null;
                wasInRun = false;
                return;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Failed)
            {
                if (Application.isPlaying && !warnedFailedLoad)
                {
                    Debug.LogWarning("Gameplay music track failed to load audio data: " + clip.name, this);
                    warnedFailedLoad = true;
                }
                source.Stop();
                wasInRun = false;
                return;
            }
            if (source.clip != clip)
            {
                source.Stop();
                source.clip = clip;
                wasInRun = false;
                pausedSource = false;
            }
            ApplySourceSettings();

            bool inRun = InGameplayRun || session != null && session.State == GameSession.RunState.MainMenu;
            bool shouldPlay = ShouldPlayMusic;
            if (!Application.isPlaying)
            {
                wasInRun = inRun;
                return;
            }
            if (clip.loadState == AudioDataLoadState.Loading) return;

            if (!inRun)
            {
                source.Stop();
                wasInRun = false;
                return;
            }

            if (!wasInRun)
            {
                source.Stop();
                source.time = 0;
                source.Play();
                if (!shouldPlay) { source.Pause(); pausedSource = true; }
            }
            else if (shouldPlay)
            {
                if (pausedSource) { source.UnPause(); pausedSource = false; }
                else if (!source.isPlaying && InGameplayRun && (!UsesCustomTrack || carriedMenuTrack != null))
                {
                    if (UsesCustomTrack) carriedMenuTrack = null;
                    else SelectNextTrack();
                    wasInRun = false;
                    // Configure the next clip on the next frame, without restarting the run or its beat effects.
                }
            }
            else if (source.isPlaying)
            {
                source.Pause();
                pausedSource = true;
            }
            if (source.clip == (session != null && session.State == GameSession.RunState.MainMenu ? MenuTrack : Track)) wasInRun = inRun;
        }

        public void StopPlayback()
        {
            EnsureSource();
            source.Stop();
            wasInRun = false;
        }

        private void ApplyLevelSoundtrack()
        {
            if (!InGameplayRun)
            {
                if (session != null && session.State == GameSession.RunState.MainMenu && levelSoundtrackApplied)
                {
                    levelTracks = null;
                    levelSoundtrackApplied = false;
                    appliedLevelDefinition = null;
                    remainingSongs.Clear();
                }
                return;
            }
            var definition = session.CurrentLevelDefinition;
            if (!levelSoundtrackApplied && definition == null) return;
            if (levelSoundtrackApplied && appliedLevelDefinition == definition) return;
            appliedLevelDefinition = definition;
            levelSoundtrackApplied = true;
            var names = definition != null ? definition.possibleSoundtrackTracks : null;
            levelTracks = names != null && names.Length > 0 ? new System.Collections.Generic.HashSet<string>(names) : null;
            // Prefer the level's tracks when they overlap the player's selection. Otherwise
            // use the selected soundtrack so an exclusive selection can play on every level.
            RefreshSongSelection();
            RefillShuffleBag();
            var retained = carriedMenuTrack != null ? carriedMenuTrack : resourceTrack;
            if (retained != null)
                remainingSongs.Remove(System.Array.FindIndex(soundtrack, song => song.resourceName == retained.name));
        }

        private void EnsureSource()
        {
            if (source != null) return;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            ApplySourceSettings();
        }

        private void ApplySourceSettings()
        {
            if (source == null) return;
            source.playOnAwake = false;
            bool dying = session != null && session.State == GameSession.RunState.Dying;
            source.loop = !dying && (MetronomeSettings.VoiceEnabled || !InGameplayRun ||
                UsesCustomTrack && (carriedMenuTrack == null || carriedMenuTrack == track));
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.pitch = (session != null ? session.MusicTempoMultiplier : 1) *
                (dying ? Mathf.Pow(DeathMinimumPitch, session.GameOverBlackoutOpacity) : 1);
            source.volume = volume * MusicLoudness.GainFor(source.clip) * (session != null ? session.GameOverMusicGain : 1);
            source.mute = muted;
        }
    }
}
