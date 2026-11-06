using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SoundEffectsChecks
    {
        private static readonly string[] ActiveSoundtrack =
            { "AnotherJoe", "PotentialForAnything", "DiscoDescent", "GameplayMusic", "Skanska",
                "TheThirdKind", "DownToEarthPart1", "UntilICollapse", "WarOnActivism",
                "IntergalacticEmotionalBreakdown", "ShootingRobotsInSpace", "VertexStage1",
                "Contact", "ForWhomTheKnellTolls", "AColdSweat", "HeartOfTheCrypt", "StyxAndStones" };

        public static void ReimportSoundtrackAndCheckGameplay()
        {
            foreach (string name in new[] { "AnotherJoe.wav", "PotentialForAnything.wav" })
            {
                string path = "Assets/Resources/" + name;
                if (File.Exists(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            GameplayChecks.Run();
        }
        [MenuItem("Candy Cruisers/Run Sound Checks")]
        public static void Run()
        {
            using var songs = new SongSelectionCheckSettings();
            using var metronomes = new MetronomeCheckSettings();
            CheckClips();
            CheckTongue();
            foreach (bool magic in new[] { false, true }) CheckEarlyReturn(magic);
            CheckShieldReturn();
            CheckSlowReturn();
            CheckMenuMusicSelection();
            CheckSeamlessMenuMusic();
            CheckGameplayMusic();
            CheckDeathMusic();
            MusicLoudnessChecks.Run();
            Debug.Log("Sound effects checks passed: oscillator continuity, animation-driven pitch, early and slow returns, cancellation, music, volume, mute and pause.");
        }

        private static void CheckClips()
        {
            var root = new GameObject("Sound clip checks", typeof(SoundEffects));
            AudioClip replacement = null;
            AudioClip tone = null;
            try
            {
                var sounds = root.GetComponent<SoundEffects>();
                tone = sounds.GetClip(SoundEffect.TongueWhistle);
                Check(tone == sounds.GetClip(SoundEffect.TongueWhistle), "Generated oscillator is cached");
                ReadWhistle(tone);

                replacement = AudioClip.Create("Replacement sound", 4410, 1, ArcadeSoundClips.SampleRate, false);
                var serialized = new SerializedObject(sounds);
                serialized.FindProperty("cues").GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(sounds.GetClip(SoundEffect.TongueWhistle) == replacement, "Assigned tone replaces the synthesized default");
                UnityEngine.Object.DestroyImmediate(root);
                if (!Application.isPlaying) Check(tone == null, "Generated oscillator is released with its service");
                Check(replacement != null, "Service does not destroy externally supplied clips");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        private static float[] ReadWhistle(AudioClip clip)
        {
            Check(clip != null && clip.channels == 1 && clip.length < .005f, "Single-cycle mono oscillator, not a timed sweep");
            var data = new float[clip.samples];
            Check(clip.GetData(data, 0), "Whistle sample data is available");
            double energy = 0;
            float largestStep = 0;
            for (int i = 0; i < data.Length; i++)
            {
                Check(!float.IsNaN(data[i]) && !float.IsInfinity(data[i]) && Mathf.Abs(data[i]) < .9f, "Finite samples with clipping headroom");
                energy += data[i] * data[i];
                if (i > 0) largestStep = Mathf.Max(largestStep, Mathf.Abs(data[i] - data[i - 1]));
            }
            Check(energy / data.Length > .02 && largestStep < .2f, "Audible signal without abrupt sample jumps");
            Check(Crossings(data, 0, 1) == 1, "The wavetable contains one complete cycle");
            Check(Mathf.Abs(data[1] - data[0] - (data[0] - data[data.Length - 1])) < .0001f,
                "Loop boundary preserves the waveform slope");
            return data;
        }

        private static int Crossings(float[] data, float start, float end)
        {
            int count = 0;
            for (int i = Mathf.Max(1, (int)(data.Length * start)); i < data.Length * end; i++)
                if (data[i - 1] <= 0 && data[i] > 0) count++;
            return count;
        }

        private static void CheckTongue()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                session.Configure(player);
                var sounds = grid.GetComponent<SoundEffects>();
                var sources = tongue.GetComponentsInChildren<AudioSource>();
                Check(sources.Length == 1, "Repeated configuration creates one voice");
                var voice = sources[0];
                Check(!voice.playOnAwake && !voice.loop && voice.spatialBlend == 0 && voice.dopplerLevel == 0,
                    "Tongue has a nonspatial, nonlooping voice independent of camera distance");
                int extensions = 0, returns = 0;
                tongue.ExtensionStarted += () => extensions++;
                tongue.RetractionStarted += () => returns++;
                Check(!tongue.TryFire(EnemyColor.Red, 0) && extensions == 0 && voice.clip == null, "Invalid fire is silent");
                Check(tongue.TryFire(EnemyColor.Red, 10), "Accepted shot");
                var tone = voice.clip;
                float basePitch = voice.pitch;
                Check(tone == sounds.GetClip(SoundEffect.TongueWhistle) && voice.loop && extensions == 1,
                    "Accepted shot starts one sustained oscillator");
                Check(!tongue.TryFire(EnemyColor.Red, 10) && extensions == 1, "Blocked repeat does not restart sound");
                Check(voice.volume == 0, "Tone fades in from the mouth");
                tongue.Tick(.05f);
                sounds.Volume = .5f;
                Check(Mathf.Abs(voice.volume - .325f) < .0001f, "Shared volume updates a live voice");
                sounds.Muted = true;
                Check(voice.mute, "Mute applies to live voices");
                sounds.Muted = false;
                session.Pause();
                float pausedPitch = voice.pitch;
                tongue.Tick(0);
                Check(sounds.Paused && tongue.Active, "Pause preserves the shot and pauses its sound service");
                Check(voice.pitch == pausedPitch, "Frozen animation holds pitch");
                session.Resume();
                Check(!sounds.Paused && !voice.mute, "Resume and unmute restore playback settings");
                tongue.Tick(.25f);
                Check(voice.pitch > pausedPitch && voice.clip == tone && voice.loop,
                    "Pitch keeps rising beyond the old sound's duration");
                tongue.Tick((10 - tongue.Length) / 14);
                float peakPitch = voice.pitch;
                Check(returns == 1 && tongue.Retracting && voice.clip == tone,
                    "Full reach keeps the same oscillator through turnaround");
                tongue.Tick(.01f);
                Check(voice.pitch < peakPitch && returns == 1, "Pitch falls as the tongue retracts");
                tongue.Tick(1);
                Check(!tongue.Active && voice.clip == null && !voice.isPlaying, "Completed shot stops its sound");
                tongue.TryFire(EnemyColor.Blue, 10, true);
                Check(Mathf.Abs(voice.pitch - basePitch) < .0001f, "Magic starts at the same physical length and pitch");
                tongue.Tick(.1f);
                float magicPitch = voice.pitch;
                tongue.Cancel();
                Check(returns == 1 && voice.clip == null, "Cancellation is silent and stops extension");
                tongue.TryFire(EnemyColor.Red, 10);
                Check(voice.pitch == basePitch, "Next shot resets pitch");
                tongue.Tick(.4f);
                Check(Mathf.Abs(voice.pitch - magicPitch) < .0001f, "Equal tongue lengths sound alike regardless of animation speed");
                tongue.Tick(10);
                Check(returns == 2 && voice.clip == null, "Long frames deliver one return then completion");
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.GetComponent<TongueSoundEffects>().enabled = false;
                if (!Application.isPlaying) typeof(TongueSoundEffects).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(tongue.GetComponent<TongueSoundEffects>(), null);
                Check(voice.clip == null, "Disabling the sound binding stops its voice");
            });
        }

        private static void CheckEarlyReturn(bool magic)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                tongue.gameObject.AddComponent<TongueSoundEffects>().Configure(tongue, sounds);
                var target = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                player.transform.position = new Vector3(target.transform.position.x, -4.6f, 0);
                float distance = target.GetComponentInChildren<SpriteRenderer>().bounds.min.y - tongue.transform.position.y;
                int returns = 0;
                tongue.RetractionStarted += () => returns++;
                tongue.TryFire(EnemyColor.Red, 10, magic);
                tongue.Tick(distance / (magic ? 56 : 14) + .001f, grid);
                var voice = tongue.GetComponentInChildren<AudioSource>();
                Check(grid.Model.Count == 0 && tongue.Retracting && returns == 1 &&
                    voice.clip == sounds.GetClip(SoundEffect.TongueWhistle),
                    "Last-enemy hit preserves the live oscillator, magic=" + magic);
                float hitPitch = voice.pitch;
                float hitLength = tongue.Length;
                Check(hitPitch < 1.2f, "An early hit does not jump to a full-reach return pitch");
                tongue.Tick(.01f, grid);
                Check(voice.pitch <= hitPitch && tongue.Length < hitLength,
                    "An early return holds or lowers its scale note as the tongue retracts");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 10, magic);
                tongue.Tick(.01f, grid);
                Check(returns == 2 && !tongue.Active, "Empty field emits one return and completes");
            });
        }

        private static void CheckShieldReturn()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                tongue.gameObject.AddComponent<TongueSoundEffects>().Configure(tongue, sounds);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                var ability = blue.GetComponent<EnemyAbilities>();
                ability.Tick(1);
                ability.Tick(ability.CooldownRemaining);
                ability.Tick(.35f);
                Check(ability.ShieldActive, "Shield ready");
                player.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                int returns = 0;
                tongue.RetractionStarted += () => returns++;
                tongue.TryFire(EnemyColor.Red, 10);
                for (int i = 0; i < 100 && !tongue.Retracting; i++) tongue.Tick(.01f, grid);
                Check(returns == 1 && tongue.Retracting && grid.Model.Count == 1 &&
                    tongue.GetComponentInChildren<AudioSource>().clip == sounds.GetClip(SoundEffect.TongueWhistle),
                    "Shield turnaround preserves the oscillator without requiring an enemy clear");
            });
        }

        private static void CheckSlowReturn()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                session.Progress.Reset(19);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 3, 0);
                grid.RefreshSpecials();
                player.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.Tick(.34f, grid);
                Check(tongue.IsDeflected && tongue.Retracting, "Special shield triggers slow return");
                var voice = tongue.GetComponentInChildren<AudioSource>();
                var tone = voice.clip;
                for (int i = 0; i < 8; i++)
                {
                    float before = voice.pitch;
                    tongue.Tick(.25f, grid);
                    Check(tongue.Active && voice.loop && voice.clip == tone && voice.pitch <= before,
                        "Whistle sustains and keeps falling throughout a multi-second deflection");
                }
                tongue.Tick(100, grid);
                Check(!tongue.Active && voice.clip == null, "Slow return stops its tone only on completion");
            });
        }

        private static void CheckMenuMusicSelection()
        {
            for (int attempt = 0; attempt < 3; attempt++)
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player);
                    if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                    typeof(GameSession).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, null);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    if (!HasBundledSoundtrack()) return;
                    Check(music.MenuTrack != null && music.ContainsTrack(music.MenuTrack.name) &&
                        music.Source.clip == music.MenuTrack,
                        "Every fresh menu plays its selected soundtrack song");

                    var songs = new System.Collections.Generic.HashSet<string> { music.MenuTrack.name };
                    for (int i = 1; i < music.TrackCount; i++)
                    {
                        Check(music.SkipCurrentSong() && songs.Add(music.Source.clip.name),
                            "Menu skipping visits every other soundtrack song once before repeating");
                        var selected = music.Source.clip;
                        music.UpdatePlayback();
                        Check(session.State == GameSession.RunState.MainMenu && !session.MenuArriving &&
                            music.Source.clip == selected && music.MenuTrack == selected && music.Source.loop,
                            "Skipping stays on the menu and preserves the selected song");
                    }
                    var last = music.Source.clip;
                    Check(music.SkipCurrentSong() && music.Source.clip != last,
                        "Menu shuffle avoids immediate repeats at the bag boundary");
                    Check(!session.HandleMenuKey(KeyCode.F8) && session.State == GameSession.RunState.MainMenu,
                        "The music shortcut cannot activate the menu's start command");
                    var carried = music.Source.clip;
                    session.StartRun();
                    Check(music.Source.clip == carried && music.Track == carried && !music.Source.loop,
                        "Starting gameplay carries the song selected by the menu shortcut");
                });
            Debug.Log("Menu music checks passed: random soundtrack start, shuffle skips, menu state and gameplay carryover.");
        }

        private static void CheckSeamlessMenuMusic()
        {
            // Keep synchronous checks independent of background streaming; the runtime scene checks use the real recording.
            var menuClip = AudioClip.Create(GameplayMusicPlayer.MenuResourceName, ArcadeSoundClips.SampleRate * 15,
                1, ArcadeSoundClips.SampleRate, false);
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player);
                    if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    var settings = new SerializedObject(music);
                    settings.FindProperty("menuTrack").objectReferenceValue = menuClip;
                    settings.FindProperty("volume").floatValue = .23f;
                    settings.FindProperty("muted").boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    typeof(GameSession).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, null);
                    music.UpdatePlayback();
                    var source = music.Source;
                    var menu = source.clip;
                    source.timeSamples = menu.frequency * 12;
                    int position = source.timeSamples;
                    float beat = music.BeatPosition;
                    Check(menu == music.MenuTrack && music.CurrentTempo == 140 && music.CurrentBeatOffset == .1f &&
                        music.CurrentRelativeMajorTonic == 2, "Menu beat timing and key belong to the audible Another Joe recording");
                    session.StartRun();
                    Check(music.Source == source && source.clip == menu && music.Track == menu && !source.loop &&
                        source.timeSamples >= position && source.timeSamples - position < menu.frequency / 10,
                        "Starting gameplay keeps the same audio source, song and sample position without restarting");
                    Check(music.BeatPosition >= beat && music.BeatPosition - beat < .25f && music.CurrentTempo == 140 &&
                        music.CurrentRelativeMajorTonic == 2 && Mathf.Abs(source.volume - .23f * MusicLoudness.GainFor(menu)) < .0001f && source.mute,
                        "The menu-to-gameplay transition preserves beat phase, key, volume and mute");
                    if (Application.isPlaying) Check(source.isPlaying, "The continuing menu song remains playing in the real audio engine");
                    music.UpdatePlayback();
                    Check(source.clip == menu && source.timeSamples >= position, "Later playback updates do not replace the carried song");
                    session.Pause();
                    int pausedPosition = source.timeSamples;
                    session.Resume();
                    Check(source.clip == menu && source.timeSamples >= pausedPosition && source.timeSamples - pausedPosition < menu.frequency / 10,
                        "Pause/resume preserves the carried song's position");
                    if (HasBundledSoundtrack())
                    {
                        var songs = new System.Collections.Generic.HashSet<string> { menu.name };
                        for (int i = 1; i < music.TrackCount; i++)
                            Check(music.SkipCurrentSong() && songs.Add(music.Source.clip.name),
                                "The carried menu song counts as the first entry of the gameplay shuffle bag");
                        var last = music.Source.clip;
                        Check(songs.Count == music.TrackCount && music.SkipCurrentSong() && music.Source.clip != last,
                            "Normal shuffle continues without immediate repeats after the carried song");
                    }
                    else Check(!music.SkipCurrentSong(), "Local-only soundtrack skips are unavailable when no local music is installed");
                });
            }
            finally { UnityEngine.Object.DestroyImmediate(menuClip); }
            Debug.Log("Seamless menu music checks passed: same source, clip, sample position, beat phase, key, volume, mute, pause and shuffle continuation.");
        }

        private static void CheckGameplayMusic()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                var clip = AudioClip.Create("Gameplay music replacement", 4410, 1, ArcadeSoundClips.SampleRate, false);
                var menuClip = AudioClip.Create(GameplayMusicPlayer.MenuResourceName, ArcadeSoundClips.SampleRate * 15,
                    1, ArcadeSoundClips.SampleRate, false);
                try
                {
                    session.Configure(player);
                    if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                    typeof(GameSession).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, null);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    Check(music != null, "Game session creates a gameplay music player");
                    if (HasBundledSoundtrack())
                    {
                        var songs = new System.Collections.Generic.HashSet<string>();
                        for (int i = 0; i < music.TrackCount; i++)
                        {
                            var selected = music.Track;
                            Check(selected != null && selected.length > 30 && selected.loadState != AudioDataLoadState.Failed,
                                "Each local soundtrack resource imports as playable audio");
                            songs.Add(selected.name);
                            music.Source.clip = selected;
                            var beatMapAsset = Resources.Load<TextAsset>("BeatMaps/" + selected.name);
                            if (beatMapAsset != null)
                            {
                                Check(SongBeatMap.TryParse(beatMapAsset.text, selected.length, out var map) &&
                                    music.UsesBeatMap && Mathf.Abs(music.CurrentTempo -
                                        60f / map.DurationAtTime(music.PlaybackSeconds)) < .001f,
                                    "Each mapped track uses its own measured beat interval: " + selected.name);
                            }
                            else Check(music.CurrentTempo > 60 && music.CurrentTempo < 200,
                                "Each fixed-tempo track has independent measured beat metadata: " + selected.name);
                            if (i + 1 < music.TrackCount) music.SelectNextTrack();
                        }
                        Check(songs.Count == ActiveSoundtrack.Length &&
                            Array.TrueForAll(ActiveSoundtrack, songs.Contains) &&
                            !songs.Contains("We Are Not Anonymous") &&
                            !songs.Contains("PoisonWasTheCure") && !songs.Contains("DriveSlow"),
                            "Shuffle includes the updated local soundtrack set exactly once per bag");
                        var last = music.Track;
                        music.SelectNextTrack();
                        Check(music.Track != last, "Shuffle bag boundary avoids immediate repeats");
                        Check(music.MenuTrack != null && music.ContainsTrack(music.MenuTrack.name),
                            "Main menu selects one local soundtrack song");
                    }
                    else Check(music.Track == null && music.MenuTrack == null,
                        "Main stays music-free when no local soundtrack files are installed");
                    if (!HasBundledSoundtrack())
                        Check(!music.SkipCurrentSong(), "Menu skipping stays unavailable without local soundtrack resources");
                    var serialized = new SerializedObject(music);
                    serialized.FindProperty("track").objectReferenceValue = clip;
                    serialized.FindProperty("menuTrack").objectReferenceValue = menuClip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    music.UpdatePlayback();
                    Check(!music.InGameplayRun && music.ShouldPlayMusic && music.Source.clip == music.MenuTrack &&
                        music.MenuTrack != clip, "Main menu requests its selected soundtrack song, not the gameplay override");
                    Check(!music.SkipCurrentSong() && music.Source.clip == menuClip,
                        "Developer skip preserves an explicit menu-track override");
                    session.StartRun();
                    Check(session.State == GameSession.RunState.Refilling && music.InGameplayRun && music.ShouldPlayMusic,
                        "Starting a run requests gameplay music during the opening beat");
                    Check(music.Source.clip == music.MenuTrack && !music.Source.loop,
                        "Even an assigned gameplay override waits for the current menu song to finish");
                    if (Application.isPlaying)
                    {
                        music.Source.Stop();
                        music.UpdatePlayback(); music.UpdatePlayback();
                    }
                    else { music.SelectNextTrack(); music.UpdatePlayback(); }
                    Check(music.Source.clip == clip && music.Source.loop && !music.Source.playOnAwake &&
                        music.Source.spatialBlend == 0 && music.Source.dopplerLevel == 0,
                        "After the menu song ends, gameplay music uses the assigned nonspatial loop");
                    music.Source.timeSamples = 2205;
                    Check(Mathf.Abs(music.BeatPosition - ((.05f - .08f) / music.BeatDuration)) < .002f,
                        "Beat phase follows the audio sample clock, not frame time");
                    Check(Mathf.Abs(music.Source.volume - GameplayMusicPlayer.DefaultVolume) < .0001f &&
                        music.Source.volume < .35f, "Gameplay music defaults below sound-effect loudness");
                    session.Pause();
                    Check(music.InGameplayRun && !music.ShouldPlayMusic, "Pause keeps the run context but pauses music");
                    session.Resume();
                    Check(music.ShouldPlayMusic, "Resume requests music again");
                    serialized.Update();
                    serialized.FindProperty("track").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    music.UpdatePlayback();
                    if (HasBundledSoundtrack())
                    {
                        Check(music.ContainsTrack(music.Source.clip.name) && !music.Source.loop,
                            "Gameplay soundtrack advances instead of looping one song");
                        var beforeSkip = music.Track;
                        Check(music.SkipCurrentSong() && music.Track != beforeSkip && music.Source.clip == music.Track,
                            "Developer skip immediately selects the next gameplay song and its beat metadata");
                        session.Pause();
                        beforeSkip = music.Track;
                        Check(music.SkipCurrentSong() && music.Track != beforeSkip && session.IsPaused && !music.ShouldPlayMusic,
                            "Skipping while paused preserves the paused state");
                        session.Resume();
                    }
                    else
                    {
                        Check(!music.SkipCurrentSong(), "Developer skip stays unavailable without local soundtrack resources");
                        session.Pause(); session.Resume();
                    }
                    // Apply any pending shuffle transition before checking the active clip's
                    // loading state; an asynchronous replacement is not a finished song.
                    music.UpdatePlayback();
                    if (HasBundledSoundtrack() && Application.isPlaying && music.Source.clip.loadState == AudioDataLoadState.Loaded)
                    {
                        var finished = music.Source.clip;
                        music.Source.Stop();
                        music.UpdatePlayback(); music.UpdatePlayback();
                        Check(music.Source.clip != finished && music.Source.clip == music.Track,
                            "A finished gameplay song automatically selects and loads the next song");
                        var resumed = music.Track;
                        session.Pause(); session.Resume();
                        Check(music.Track == resumed, "Pause/resume does not advance the playlist");
                    }
                    typeof(GameSession).GetMethod("EndGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, null);
                    Check(!music.InGameplayRun && !music.ShouldPlayMusic && music.Source.volume == 0 && !music.Source.isPlaying,
                        "Game over immediately stops the music, including direct transitions without a death cue");
                    session.Tick(2);
                    music.UpdatePlayback();
                    Check(!music.ShouldPlayMusic && music.Source.volume == 0 && !music.Source.isPlaying && !music.SkipCurrentSong(),
                        "Game-over updates and developer skip cannot restart the soundtrack");
                }
                finally
                {
                    if (!Application.isPlaying) typeof(GameSession).GetMethod("OnDisable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                    UnityEngine.Object.DestroyImmediate(session);
                    UnityEngine.Object.DestroyImmediate(clip);
                    UnityEngine.Object.DestroyImmediate(menuClip);
                }
            });
        }

        private static void CheckDeathMusic()
        {
            var clip = AudioClip.Create("Death slowdown music", ArcadeSoundClips.SampleRate * 15,
                1, ArcadeSoundClips.SampleRate, false);
            try
            {
                foreach (bool muted in new[] { false, true })
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player);
                    if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    var settings = new SerializedObject(music);
                    settings.FindProperty("track").objectReferenceValue = clip;
                    settings.FindProperty("volume").floatValue = .23f;
                    settings.FindProperty("muted").boolValue = muted;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    music.UpdatePlayback();
                    var source = music.Source;
                    source.timeSamples = clip.frequency * 2;
                    int position = source.timeSamples;
                    typeof(GameSession).GetMethod("BeginPlayerDeath",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                    float duration = session.GameOverBlackoutSeconds;
                    Check(source.clip == clip && source.pitch == 1 && Mathf.Abs(source.volume - .23f) < .0001f &&
                        source.timeSamples >= position && source.timeSamples - position < clip.frequency / 10,
                        "Fatal slowdown continues the original recording without restarting, seeking or dropping its volume");
                    session.Tick(0); session.Tick(-1);
                    Check(source.pitch == 1 && session.GameOverMusicGain == 1,
                        "Nonpositive ticks do not advance the music collapse");
                    float previousPitch = source.pitch, previousVolume = source.volume;
                    for (int step = 1; step <= 8; step++)
                    {
                        session.Tick(duration / 8);
                        float fade = Mathf.SmoothStep(0, 1, step / 8f);
                        Check(Mathf.Abs(source.pitch - Mathf.Pow(.04f, fade)) < .0001f &&
                            Mathf.Abs(source.volume - .23f * (1 - fade)) < .0001f &&
                            source.pitch < previousPitch && source.volume < previousVolume,
                            "Every fade step continuously lowers both playback speed and normalized volume");
                        Check(source.clip == clip && source.mute == muted && !source.loop &&
                            session.GameOverBlackoutSeconds == duration && !music.SkipCurrentSong() && !music.PlayCountingMetronome(),
                            "Slowdown preserves the track, mute and captured duration without looping, shuffling or developer skips");
                        if (step < 8 && Application.isPlaying)
                            Check(source.isPlaying, "The real audio source keeps playing throughout the collapse");
                        if (step == 4) Check(Mathf.Abs(source.pitch - .2f) < .0001f,
                            "At the halfway point the recording is already five times slower");
                        if (step == 6) Check(source.pitch < .1f, "The final beat descends below one tenth speed");
                        previousPitch = source.pitch;
                        previousVolume = source.volume;
                    }
                    Check(!source.isPlaying && source.volume == 0 && !music.ShouldPlayMusic,
                        "Music is fully stopped at the blackout boundary before any shatter");
                    session.Tick(PlayerDeathBurst.ShatterSoundSeconds + .001f);
                    var shatter = grid.GetComponent<SoundEffects>().GetClip(SoundEffect.PlayerShatter);
                    Check(!Array.Exists(grid.GetComponentsInChildren<AudioSource>(), voice =>
                        voice.clip == shatter && voice != source),
                        "Final death melts quietly without an explosion voice");
                    session.Tick(PlayerMeltDeath.Duration);
                    music.UpdatePlayback(); music.UpdatePlayback();
                    Check(session.State == GameSession.RunState.GameOver && !source.isPlaying && source.volume == 0 && source.pitch == 1,
                        "Game over stays silent and clears the music-only pitch override");
                });
            }
            finally { UnityEngine.Object.DestroyImmediate(clip); }
            Debug.Log("Death music checks passed: continuous tape-stop pitch, fade, clip continuity, fixed duration, mute, no shuffle and independent shatter.");
        }

        private static bool HasBundledSoundtrack()
        {
            foreach (string name in ActiveSoundtrack)
                if (Resources.Load<AudioClip>(name) == null) return false;
            return true;
        }

        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Sound effects check failed: " + message); }
    }
}
