using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class TempoMapChecks
    {
        public static void RunWithSoundChecks() { Run(); MusicKeyChecks.RunWithSoundChecks(); }

        public static void Run()
        {
            CheckClock();
            CheckPlaybackAdvance();
            CheckValidation();
            CheckCelebration();
            CheckSoundtrack();
            if (Application.isPlaying) CheckMappedMovement();
            Debug.Log("Tempo map checks passed: continuous phase, tempo changes, inverse timing, validation, variable bar durations and soundtrack maps.");
        }

        private static SongBeatMap Fixture() => new SongBeatMap
        {
            durationSeconds = 4,
            beatTimes = new[] { .1f, .6f, 1.1f, 1.35f, 1.6f, 2.35f, 3.1f }
        };

        private static void CheckPlaybackAdvance()
        {
            var root = new GameObject("Playback clock jitter fixture", typeof(GameplayMusicPlayer));
            var clip = AudioClip.Create("Playback clock", 44100, 1, 44100, false);
            var replacement = AudioClip.Create("Replacement playback clock", 44100, 1, 44100, false);
            try
            {
                var music = root.GetComponent<GameplayMusicPlayer>();
                music.Source.clip = clip;
                float previous = .5f;
                music.PreviewTime = .49f;
                Check(music.TryAdvancePlayback(clip, ref previous, out float elapsed) && elapsed == 0 && previous == .5f,
                    "Small audio-buffer regressions hold the musical clock instead of releasing it");
                music.PreviewTime = .55f;
                Check(music.TryAdvancePlayback(clip, ref previous, out elapsed), "The clock continues after buffer jitter");
                Near(elapsed, .05f, "Recovering from jitter advances only the new musical time");
                Check(music.TryAdvancePlayback(clip, ref previous, out elapsed) && elapsed == 0,
                    "Repeated reads cannot count a frame twice");
                music.PreviewTime = .1f;
                Check(!music.TryAdvancePlayback(clip, ref previous, out elapsed), "A real backwards seek releases the previous clock");
                music.PreviewTime = .6f; music.Source.clip = replacement;
                Check(!music.TryAdvancePlayback(clip, ref previous, out elapsed), "Track replacement releases the previous clock");
                music.Source.clip = clip; music.PreviewTime = null; music.StopPlayback();
                Check(!music.TryAdvancePlayback(clip, ref previous, out elapsed), "Stopped music uses the bounded fallback");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        private static void CheckClock()
        {
            var map = Fixture();
            Check(map.IsValid(4), "A map can contain both abrupt acceleration and slowdown");
            Near(map.BeatAtTime(.35f), .5f, "Opening half-beat");
            Near(map.BeatAtTime(1.225f), 2.5f, "Half-beat after acceleration");
            Near(map.BeatAtTime(1.975f), 4.5f, "Half-beat after slowdown");
            Near(map.DurationAtTime(.8f), .5f, "Opening beat length");
            Near(map.DurationAtTime(1.2f), .25f, "Accelerated beat length");
            Near(map.DurationAtTime(2), .75f, "Slowed beat length");
            for (int i = 0; i < map.beatTimes.Length; i++)
            {
                Near(map.BeatAtTime(map.beatTimes[i]), i, "Every detected timestamp is an integer beat");
                Near(map.TimeAtBeat(i), map.beatTimes[i], "Beat-to-time lookup preserves timestamps");
            }
            float previous = float.NegativeInfinity;
            for (float time = -.5f; time < 5; time += .007f)
            {
                float beat = map.BeatAtTime(time);
                Check(beat > previous, "Changing tempo never rewinds or stalls the beat clock");
                Near(map.TimeAtBeat(beat), time, "Seeking and extrapolation round-trip through the same map");
                previous = beat;
            }
            Near(map.TimeAtBeat(map.BeatAtTime(.85f) + 2) - .85f, .625f,
                "A two-beat timer integrates a tempo change instead of multiplying by the old duration");
        }

        private static void CheckValidation()
        {
            Check(SongBeatMap.TryParse(JsonUtility.ToJson(Fixture()), 4, out var parsed) && parsed.IsValid(4),
                "Serialized maps load through the runtime parser");
            Check(!SongBeatMap.TryParse("{invalid", 4, out _) && !SongBeatMap.TryParse("{}", 4, out _),
                "Malformed and empty maps fail safely");
            Check(!Fixture().IsValid(8), "Maps for a different recording length are rejected");
            foreach (var beats in new[] { new[] { 0f }, new[] { 0f, 0f }, new[] { .5f, .1f },
                new[] { -.1f, .5f }, new[] { 0f, float.NaN }, new[] { 0f, float.PositiveInfinity }, new[] { 0f, 5f } })
            {
                var map = Fixture(); map.beatTimes = beats;
                Check(!map.IsValid(4), "Invalid beat sequences cannot reach playback");
            }
        }

        private static void CheckCelebration()
        {
            var animation = new FullSetCelebration();
            var colors = new List<EnemyColor> { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green };
            int removals = 0, starts = 0;
            animation.ColorRemoved += color => removals++;
            animation.BarStarted += color => starts++;
            animation.Begin(colors, beatDurations: new[] { .5f, .25f, .75f });
            Near(animation.PlaybackDuration, 1.5f, "Celebration duration sums the actual upcoming beats");
            animation.Tick(.49f);
            Check(starts == 1 && removals == 0 && animation.Color != Color.white, "First bar starts once before its mapped removal");
            animation.Tick(.02f);
            Check(starts == 2 && removals == 1 && animation.Color == Color.white, "Second bar and its cue begin at the faster beat boundary");
            animation.Tick(.24f);
            Check(starts == 3 && removals == 2 && !animation.Finished, "Faster second bar is removed and the third starts on schedule");
            animation.Tick(.74f);
            Check(removals == 2 && !animation.Finished, "Slower third bar retains its full interval");
            animation.Tick(.02f);
            Check(removals == 3 && animation.Finished, "All mapped removals occur exactly once");
            animation.Tick(10);
            Check(starts == 3 && removals == 3, "Long frames cannot repeat bar starts, cues or completed removals");

            starts = removals = 0;
            animation.Begin(colors, duration: 1.9f, beatDurations: new[] { .5f, .25f, .75f }, holdDuration: .4f);
            Near(animation.PlaybackDuration, 1.9f, "Celebration duration includes the lead-in hold");
            animation.Tick(.39f);
            Check(starts == 0 && removals == 0 && animation.Color == Color.white && colors.TrueForAll(animation.Visible),
                "Lead-in hold keeps every earned bar visible before removals begin");
            animation.Tick(.27f);
            Check(removals == 0 && animation.Color != Color.white, "First bar still waits its beat after the hold");
            animation.Tick(1.25f);
            Check(removals == 3 && animation.Finished, "Held celebrations still remove every mapped bar once");
        }

        private static void CheckSoundtrack()
        {
            var root = new GameObject("Tempo map playback fixture", typeof(GameplayMusicPlayer));
            var anotherJoe = AudioClip.Create("AnotherJoe", 44100, 1, 44100, false);
            var replacement = AudioClip.Create("Unmapped tempo fixture", 44100, 1, 44100, false);
            try
            {
                var music = root.GetComponent<GameplayMusicPlayer>();
                Check(Resources.Load<AudioClip>("PoisonWasTheCure") == null && Resources.Load<AudioClip>("DriveSlow") == null,
                    "Removed soundtrack recordings are no longer packaged as resources");
                Check(!music.ContainsTrack("We Are Not Anonymous") &&
                    !music.ContainsTrack("PoisonWasTheCure") && !music.ContainsTrack("DriveSlow"),
                    "Removed soundtrack entries stay out of the playlist");
                Check(music.ContainsTrack("Contact"), "Trimmed Contact recording is registered in the playlist");
                var contactAsset = Resources.Load<TextAsset>("BeatMaps/Contact");
                var contactMap = contactAsset != null ? JsonUtility.FromJson<SongBeatMap>(contactAsset.text) : null;
                Check(contactMap != null && contactMap.IsValid(contactMap.durationSeconds) &&
                    Mathf.Abs(contactMap.durationSeconds - 232.826485f) < .001f,
                    "Contact map matches the requested 55.380-288.206-second edit");
                var contactClip = Resources.Load<AudioClip>("Contact");
                if (contactClip != null)
                {
                    music.Source.clip = contactClip;
                    Check(music.UsesBeatMap && music.CurrentDownbeatOffsetBeats == 0 &&
                        music.CurrentRelativeMajorTonic == 2,
                        "Contact uses its analyzed beats, opening downbeat and D-major tonal metadata");
                }
                foreach (string name in new[] { "ForWhomTheKnellTolls", "AColdSweat", "HeartOfTheCrypt", "StyxAndStones" })
                {
                    Check(music.ContainsTrack(name), "Added soundtrack recording is registered: " + name);
                    var asset = Resources.Load<TextAsset>("BeatMaps/" + name);
                    var clip = Resources.Load<AudioClip>(name);
                    var map = asset != null ? JsonUtility.FromJson<SongBeatMap>(asset.text) : null;
                    Check(map != null && map.IsValid(clip != null ? clip.length : map.durationSeconds),
                        "Added soundtrack map matches its recording: " + name);
                    if (clip == null) continue;
                    music.Source.clip = clip;
                    Check(music.UsesBeatMap && Mathf.Abs(music.SecondsAtBeat(0) - map.FirstBeatTime) < .001f &&
                        Mathf.Abs(music.BeatPositionAtTime(map.beatTimes[map.beatTimes.Length / 2]) - map.beatTimes.Length / 2) < .001f,
                        "Added soundtrack follows its measured beat clock: " + name);
                }
                foreach (string name in new[] { "We Are Not Anonymous", "PoisonWasTheCure", "DriveSlow" })
                {
                    var saved = Resources.Load<TextAsset>("BeatMaps/" + name);
                    Check(saved != null, "Analyzed maps remain available without their recordings in the playlist");
                    var savedMap = JsonUtility.FromJson<SongBeatMap>(saved.text);
                    Check(savedMap.IsValid(savedMap.durationSeconds), "Analyzed maps remain valid without their recordings installed");
                    if (name == "We Are Not Anonymous") continue;
                    float early = (savedMap.BeatAtTime(25) - savedMap.BeatAtTime(5)) * 3;
                    float later = name == "PoisonWasTheCure" ?
                        (savedMap.BeatAtTime(100) - savedMap.BeatAtTime(70)) * 2 :
                        (savedMap.BeatAtTime(265) - savedMap.BeatAtTime(245)) * 3;
                    Check(name == "PoisonWasTheCure" ? later > early + 12 : later < early - 10,
                        "The analyzed recording captures its sustained tempo change");
                }
                music.Source.clip = anotherJoe;
                Check(!music.UsesBeatMap, "Existing constant-tempo songs retain their authored timing");
                Near(music.BeatPositionAtTime(.1f + 60f / 140 * 5), 5, "Fixed-tempo phase stays unchanged");
                music.Source.clip = replacement;
                Check(!music.UsesBeatMap, "An unknown replacement clip never inherits another song's map");
                music.Source.clip = null;
                Near(music.PlaybackSeconds, 0, "Silent fallback never queries sample position without an audio clip");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(anotherJoe);
                UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        private static void CheckMappedMovement()
        {
            foreach (string track in new[] { "DiscoDescent", GameplayMusicPlayer.CountingResourceName })
            foreach (int frames in new[] { 1, 60 })
            {
                var root = new GameObject("Mapped movement fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(GameplayMusicPlayer));
                var clip = AudioClip.Create(track, 44100 * 4, 1, 44100, false);
                try
                {
                    var grid = root.GetComponent<EnemyGrid>();
                    var movement = root.GetComponent<EnemyGridMovement>();
                    var music = root.GetComponent<GameplayMusicPlayer>();
                    music.Source.clip = clip;
                    var cache = (Dictionary<AudioClip, SongBeatMap>)typeof(GameplayMusicPlayer).GetField("beatMaps",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(music);
                    cache[clip] = Fixture();
                    for (int i = 0; i < 3; i++) grid.Model.TryAdd(i + 1, EnemyColor.Green, i, 0);
                    int pulses = 0;
                    movement.GreenPulsed += id => pulses++;
                    music.Source.Play();
                    music.Source.timeSamples = Mathf.RoundToInt(.35f * clip.frequency);
                    movement.Tick(.001);
                    for (int i = 1; i <= frames; i++)
                    {
                        music.Source.timeSamples = Mathf.RoundToInt(Mathf.Lerp(.35f, 3.11f, i / (float)frames) * clip.frequency);
                        movement.Tick(2.76 / frames);
                    }
                    Check(pulses == 3, "Mapped counts two and four produce one movement pulse each, including long frames and shifted downbeats");
                    Near(root.transform.position.x, movement.CurrentSpeed * 1.5f,
                        "Movement spends only the selected single-beat intervals across acceleration and slowdown");
                    music.Source.Pause();
                    float pausedBeat = music.BeatPosition;
                    Near(music.BeatPosition, pausedBeat, "Pausing leaves mapped phase unchanged");
                    music.Source.UnPause();
                    Near(music.BeatPosition, pausedBeat, "Resume does not restart mapped phase");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
        }

        private static void Near(float actual, float expected, string message) =>
            Check(Mathf.Abs(actual - expected) < .002f, message + ": " + actual + " expected " + expected);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Tempo map check failed: " + message); }
    }
}
