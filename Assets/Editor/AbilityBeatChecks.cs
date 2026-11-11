using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class AbilityBeatChecks
    {
        public static void Run()
        {
            var clock = new AbilityBeatClock();
            clock.Advance(0, 12.37f, 1);
            float due = clock.AfterBeats(4);
            Check(due == 4, "Spawn phase aligns deadlines to whole soundtrack beats");
            clock.Advance(50, 15.99f, 1);
            Check(!clock.Reached(due), "Wall time cannot trigger an ability ahead of the soundtrack beat");
            clock.Advance(.001f, 16, 1);
            Check(clock.Reached(due), "Ability becomes due on the exact music beat");
            due = clock.AfterBeats(3);
            clock.Advance(100, 16, 1);
            Check(!clock.Reached(due), "A paused music clock cannot advance cooldowns");
            clock.Advance(0, .73f, 2);
            Check(!clock.Reached(due), "Song changes do not trigger an off-beat ability");
            clock.Advance(0, 2.99f, 2);
            Check(!clock.Reached(due), "Remaining beats survive song changes");
            clock.Advance(0, 3, 2);
            Check(clock.Reached(due), "Pending ability lands on the new song's beat");
            due = clock.AfterBeats(2);
            clock.Advance(0, .25f, 2);
            Check(!clock.Reached(due), "Backward seeks preserve pending intervals");
            clock.Advance(0, 2, 2);
            Check(clock.Reached(due), "Seeked track remains beat aligned");
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
            foreach (bool special in new[] { false, true })
            {
                var beats = CombatBalance.CooldownBeats(color, 1, special);
                Check(beats.x >= 1 && beats.y >= beats.x, "All ability ranges contain positive whole beats");
                Check(CombatBalance.CooldownBeats(color, 50, special).y <= beats.y,
                    "Later levels retain faster maximum cooldowns");
            }
            CheckSharedWarnings();
            CheckYellowBeats();
            CheckRetreatBeats();
            if (Application.isPlaying) CheckAudioDrivenRed();
            Debug.Log("Ability beat checks passed: phase alignment, pause, track changes, seeks and cooldown ranges.");
        }
        private static void CheckSharedWarnings()
        {
            foreach (float tempo in new[] { 60f, 180f })
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                music.PreviewTime = 0;
                music.PreviewTempo = tempo;
                var actor = ProgressionChecks.Add(grid, color, 2, 0);
                if (color == EnemyColor.Green)
                {
                    ProgressionChecks.Add(grid, color, 1, 0);
                    ProgressionChecks.Add(grid, color, 3, 0);
                    grid.RefreshSpecials();
                }
                else ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                var ability = actor.GetComponent<EnemyAbilities>();
                ability.Tick(0);
                float beatSeconds = 60 / tempo;
                bool cast = color == EnemyColor.Green || color == EnemyColor.Purple || color == EnemyColor.Orange;
                float trigger = ability.CooldownRemaining / beatSeconds + (cast ? 1 : 0);
                music.PreviewTime = (trigger - 1.01f) * beatSeconds;
                ability.Tick(0);
                Check(!ability.Warning, color + " does not warn more than one beat early at " + tempo);
                music.PreviewTime = (trigger - 1) * beatSeconds;
                ability.Tick(0);
                Check(ability.Warning, color + " warns on the preceding beat at " + tempo);
                ability.Tick(100);
                Check(ability.Warning, color + " warning follows paused soundtrack rather than wall time");
                music.PreviewTime = (trigger - .01f) * beatSeconds;
                ability.Tick(0);
                Check(ability.Warning, color + " warning lasts until activation");
                music.PreviewTime = trigger * beatSeconds;
                ability.Tick(0);
                Check(!ability.Warning, color + " warning ends on activation");
            });
        }
        private static void CheckYellowBeats()
        {
            foreach (bool special in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                music.PreviewTime = 0;
                music.PreviewTempo = 60;
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 2, 0);
                var target = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 1);
                if (special)
                {
                    ProgressionChecks.Add(grid, EnemyColor.Yellow, 1, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Yellow, 3, 0);
                    grid.RefreshSpecials();
                }
                var ability = yellow.GetComponent<EnemyAbilities>();
                Check(ability.BeginImitation(target), "Yellow starts its two-beat transformation");
                music.PreviewTime = 1;
                ability.Tick(100);
                Check(ability.IsTransforming, "One beat cannot complete Yellow despite a long frame");
                music.PreviewTempo = 180;
                music.PreviewTime = 1f / 3;
                ability.Tick(100);
                Check(ability.IsTransforming, "Tempo changes and a paused soundtrack preserve remaining beats");
                music.PreviewTime = 1.99f / 3;
                ability.Tick(0);
                Check(ability.IsTransforming, "Yellow remains transforming until its second beat");
                music.PreviewTime = 2f / 3;
                ability.Tick(0);
                Check(!ability.IsTransforming && (special ? ability.IsDisguised : yellow.Color == EnemyColor.Red),
                    "Regular conversion and special disguise finish exactly at two beats");
            });
        }
        private static void CheckRetreatBeats()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                music.PreviewTempo = 60;
                music.PreviewTime = .25f;
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 8);
                grid.TickRetreat(0);
                music.PreviewTime = .99f;
                grid.TickRetreat(100);
                Check(enemy.Row == 8, "Retreat waits for the next soundtrack beat");
                music.PreviewTime = 1;
                grid.TickRetreat(0);
                Check(enemy.Row == 7, "Retreat moves one space on a beat");
                grid.TickRetreat(100);
                Check(enemy.Row == 7, "Paused soundtrack cannot repeat a retreat step");
                music.PreviewTempo = 180;
                music.PreviewTime = 2f / 3;
                grid.TickRetreat(0);
                Check(enemy.Row == 6, "Retreat follows a faster tempo");
                music.PreviewTime = 4f / 3;
                grid.TickRetreat(0);
                Check(enemy.Row == 4, "A long frame catches up exactly one space per missed beat");
            });
        }
        private static void CheckAudioDrivenRed()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                var clip = AudioClip.Create("Ability beat fixture", 44100 * 30, 1, 44100, false);
                try
                {
                    music.Source.clip = clip;
                    music.Source.timeSamples = 44100;
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    var ability = red.GetComponent<EnemyAbilities>();
                    float due = music.PlaybackSeconds + ability.CooldownRemaining;
                    Check(Mathf.Abs(music.BeatPositionAtTime(due) - Mathf.Round(music.BeatPositionAtTime(due))) < .0001f,
                        "Real enemy deadline is on a soundtrack beat despite a fractional spawn time");
                    int before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length;
                    music.Source.timeSamples = Mathf.FloorToInt((due - .002f) * clip.frequency);
                    ability.Tick(100);
                    Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == before,
                        "Red cannot fire before its song beat even with a large frame delta");
                    music.Source.timeSamples = Mathf.CeilToInt(due * clip.frequency);
                    ability.Tick(.001f);
                    Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == before + 1,
                        "Red fires when audio playback crosses its scheduled beat");
                    ability.Tick(100);
                    Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == before + 1,
                        "Repeated ticks on the same audio beat never duplicate a shot");
                }
                finally { music.Source.clip = null; UnityEngine.Object.DestroyImmediate(clip); }
            });
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Ability beat check failed: " + message); }
    }
}
