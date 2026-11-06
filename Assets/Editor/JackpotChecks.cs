using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class JackpotChecks
    {
        public static void Run()
        {
            var celebration = new FullSetCelebration();
            var colors = ((EnemyColor[])Enum.GetValues(typeof(EnemyColor))).ToList();
            celebration.Begin(colors);
            var resting = new Rect(0, 100, 40, 5);
            var visited = new System.Collections.Generic.HashSet<EnemyColor>();
            for (int i = 0; i < colors.Count; i++)
            {
                Check(celebration.Color == Color.white, "Every earned-bar beat starts white");
                celebration.Tick(FullSetCelebration.StepDuration * .75f);
                var flashed = colors.First(c => EnemyPalette.Get(c) == celebration.Color);
                visited.Add(flashed);
                Check(celebration.Visible(flashed), "Bar stays visible through its color half-beat");
                if (i == colors.Count - 1) Check(celebration.LastColor == flashed, "Final flash reserves the player's new color");
                else Check(celebration.Visible(celebration.LastColor.Value), "Final bar remains until the last flash");
                celebration.Tick(FullSetCelebration.StepDuration * .25f + .00001f);
                Check(!celebration.Visible(flashed), "Each beat removes exactly its displayed bar");
            }
            Check(visited.Count == colors.Count && colors.All(c => !celebration.Visible(c)), "All earned colors cycle and all bars are removed");
            Check(celebration.Finished && celebration.Color == EnemyPalette.Get(celebration.LastColor.Value), "Transition finishes on the reserved color, never white");
            int simultaneousRemovals = 0;
            celebration.ColorRemoved += _ => simultaneousRemovals++;
            celebration.Begin(colors.Take(3).ToList(), duration: 2, simultaneousPowerDown: true);
            var early = celebration.Present(colors[0], resting);
            Check(celebration.SimultaneousPowerDown && celebration.PowerDownProgress == 0,
                "Partial power-down exposes a non-pulsing shared progress value");
            Check(SameColor(GameSession.ColorClearBarPowerDownTint(colors[0], 0), GameSession.ColorClearBarTint(colors[0], true)) &&
                SameColor(GameSession.ColorClearBarPowerDownTint(colors[0], 1), GameSession.ColorClearBarTint(colors[0], false)),
                "Partial power-down fades back to the default unpowered bar color instead of disappearing");
            celebration.Tick(.3f);
            var redSpike = celebration.Present(colors[0], resting);
            var blueSpike = celebration.Present(colors[1], resting);
            Check(celebration.Color == Color.white && redSpike.height > early.height && redSpike == blueSpike &&
                redSpike.x == resting.x && redSpike.width == resting.width && celebration.PowerDownProgress > 0,
                "Partial wave clears spike every earned bar vertically while the player flash stays white");
            celebration.Tick(1.1f);
            Check(celebration.Present(colors[0], resting).height < redSpike.height && colors.Take(3).All(celebration.Visible),
                "Simultaneous partial bars power down together before disappearing");
            celebration.Tick(.61f);
            Check(simultaneousRemovals == 3 && colors.Take(3).All(c => !celebration.Visible(c)),
                "Simultaneous partial bars all disappear on the shared downbeat");
            for (int count = 1; count <= colors.Count; count++)
            {
                celebration.Begin(colors.Take(count).ToList());
                celebration.Tick(FullSetCelebration.StepDuration * count - .001f);
                Check(!celebration.Finished, "Standalone celebration defaults use one fallback beat per supplied bar");
                celebration.Tick(.002f);
                Check(celebration.Finished, "Every bar count completes at its beat-count deadline");
            }
            celebration.Reset();
            Check(!celebration.Active && colors.All(celebration.Visible), "Reset restores ordinary bar presentation");
            celebration.Begin(colors, new[] { EnemyColor.Green });
            Check(celebration.LastColor == colors.OrderBy(color => color).Last(),
                "Full-set wave finishes on the rightmost bar; the session reserves that color in the next fleet");
            foreach (bool magic in new[] { false, true })
            foreach (bool fullLives in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                var sounds = grid.GetComponent<SoundEffects>();
                int jackpots = 0;
                sounds.CuePlayed += (effect, pitch) => { if (effect == SoundEffect.Jackpot) jackpots++; };
                var clip = sounds.GetClip(SoundEffect.Jackpot);
                var samples = new float[clip.samples]; clip.GetData(samples, 0);
                Check(clip.length > 1.5f && clip.length < 2 && samples.All(s => Mathf.Abs(s) < 1) &&
                    samples.Take(samples.Length / 2).Any(s => Mathf.Abs(s) > .1f) &&
                    samples.Skip(samples.Length / 2).Any(s => Mathf.Abs(s) > .05f), "Jackpot has an unclipped bell run and sustained finale");
                if (!fullLives) { player.Hit(); player.TickSurvival(4); }
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                if (magic) grid.ClearMagicChain(red.Id, 0); else grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(jackpots == 0, "Partial bar set is not a jackpot");
                if (magic) grid.ClearMagicChain(blue.Id, 0); else grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(jackpots == 1 && grid.AllColorClearBarsFilled && player.Lives == PlayerMovement.MaxLives,
                    "Completing the bars sounds once, including at maximum lives");
                Check(session.ClearCelebration.Active && player.CelebrationColor == session.ClearCelebration.Color,
                    "Full fleet clear begins the synchronized player celebration");
                session.Pause();
                var heldColor = player.DisplayColor;
                session.Tick(10);
                Check(player.DisplayColor == heldColor && !session.ClearCelebration.Finished, "Pause freezes the celebration");
                session.Resume();
                session.Tick(.05f); session.Tick(.05f);
                Check(jackpots == 1, "The full set does not retrigger on later frames");
            });
            Debug.Log("Jackpot checks passed: full sets, partial sets, ordinary/magic kills, full lives, exactly-once sound and bell samples.");
            CheckRemovalTones(true);
            CheckRemovalTones(false);
        }
        private static void CheckRemovalTones(bool partial)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                var sounds = grid.GetComponent<SoundEffects>();
                var pitches = new System.Collections.Generic.List<float>();
                var expectedEffect = partial ? SoundEffect.BarPowerDown : SoundEffect.BarPowerUp;
                int wrongTones = 0;
                int jackpots = 0;
                sounds.CuePlayed += (effect, pitch) =>
                {
                    if (effect == expectedEffect) pitches.Add(pitch);
                    else if (effect == SoundEffect.BarPowerDown || effect == SoundEffect.BarPowerUp) wrongTones++;
                    if (effect == SoundEffect.Jackpot) jackpots++;
                };
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 4, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                if (partial)
                {
                    grid.ResetColorClearStreak();
                    Check(pitches.Count == 1, "Losing the earned bar plays its own power-down cue");
                    pitches.Clear();
                }
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                grid.ClearMatchingChain(green.Id, EnemyColor.Green);
                Check(session.ClearCelebration.Active && grid.AllColorClearBarsFilled == !partial && jackpots == (partial ? 0 : 1),
                    "Both complete and partial sets transition, with a jackpot only for complete sets");
                var earned = partial ? new[] { EnemyColor.Blue, EnemyColor.Green } : new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green };
                var powerDownVoice = partial ? grid.GetComponentsInChildren<AudioSource>()
                    .LastOrDefault(source => source.clip == sounds.GetClip(SoundEffect.BarPowerDown)) : null;
                int hidden = 0;
                float transitionDuration = session.ClearCelebration.PlaybackDuration;
                if (partial)
                    Check(pitches.Count == 1 && powerDownVoice != null &&
                        Mathf.Abs(powerDownVoice.clip.length / powerDownVoice.pitch - transitionDuration) < .001f,
                        "Partial sets play one power-down tone stretched across the whole transition");
                for (int i = 0; i < 120; i++)
                {
                    session.ClearCelebration.Tick(transitionDuration / 120 + .000001f);
                    hidden = earned.Count(c => !session.ClearCelebration.Visible(c));
                    int started = session.ClearCelebration.Waiting ? 0 : Mathf.Min(hidden + 1, earned.Length);
                    Check(partial ? hidden == 0 || hidden == earned.Length : pitches.Count == started,
                        "Each complete-set cue begins while its bar is visible, before that bar disappears");
                }
                Check(pitches.Count == (partial ? 1 : earned.Length) && wrongTones == 0,
                    "Partial sets use one directed tone; complete sets keep one tone per bar");
                for (int i = 1; i < pitches.Count; i++)
                    Check(pitches[i] > pitches[i - 1], "Complete sets ascend");
                session.ClearCelebration.Tick(10);
                Check(pitches.Count == (partial ? 1 : earned.Length), "No repeated removal tones after completion");
                Check(sounds.GetClip(expectedEffect).length > .2f, "Replaceable removal cue has an audio clip");
            });
        }
        private static bool SameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .0001f && Mathf.Abs(a.g - b.g) < .0001f &&
            Mathf.Abs(a.b - b.b) < .0001f && Mathf.Abs(a.a - b.a) < .0001f;
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Jackpot check failed: " + message); }
    }
}
