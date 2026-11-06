using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ColorClearCelebrationChecks
    {
        public static void Run()
        {
            CheckOverlappingWave();
            CheckBarUnlockTransition();
            CheckLostBars();
            var random = UnityEngine.Random.state;
            var sizes = Enumerable.Range(0, 4096).Select(_ => EnemyPlaceholderArt.RandomDustSize(.55f)).ToArray();
            Check(sizes.All(size => size >= .055f && size <= .16f) && sizes.Count(size => size >= .11f) > sizes.Length * .6f,
                "Most stream particles use the larger half of the dust size range");
            Check(sizes.Any(size => size < .07f) && UnityEngine.Random.state.Equals(random),
                "Larger dust retains fine glints without consuming gameplay randomness");
            var animation = new ColorClearBarAnimation();
            var slot = new Rect(20, 500, 80, 5);
            animation.Begin(EnemyColor.Blue);
            var first = animation.Present(EnemyColor.Blue, slot);
            Check(first.width > slot.width && first.height > slot.height && first.center.y < slot.center.y,
                "Earned bar starts oversized above its slot");
            animation.Tick(.3f);
            var middle = animation.Present(EnemyColor.Blue, slot);
            Check(middle.width < first.width && middle.center.y > first.center.y, "Bar shrinks and slides toward its slot");
            animation.Tick(1);
            Check(animation.Present(EnemyColor.Blue, slot) == slot, "Bar settles exactly into the resting slot");
            for (int i = 1; i < 6; i++)
                Check(ColorClearBarAnimation.TonePitch(i + 1) > ColorClearBarAnimation.TonePitch(i), "Every additional bar raises tone pitch");
            int[] majorScale = { 0, 2, 4, 5, 7, 9 };
            for (int i = 0; i < majorScale.Length; i++)
                Check(Mathf.Abs(ColorClearBarAnimation.TonePitch(i + 1) - Mathf.Pow(2, majorScale[i] / 12f)) < .00001f,
                    "Bar notes follow the first six degrees of the major scale");
            var dim = GameSession.ColorClearBarTint(EnemyColor.Red, false);
            var powered = GameSession.ColorClearBarTint(EnemyColor.Red, true);
            Check(dim.a < powered.a && dim.r < powered.r, "Powered-down bars are dimmer and more transparent than lit bars");
            var high = GameSession.ColorClearBarLevelRect(slot, 0, 0);
            var low = GameSession.ColorClearBarLevelRect(slot, .25f, 0);
            var offset = GameSession.ColorClearBarLevelRect(slot, 0, 1);
            Check(high.height > low.height && Mathf.Abs(high.height - offset.height) > .01f,
                "Powered bars visualize the beat with staggered level heights");

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                Check(GameSession.ColorClearBarColors(spawner, grid).Count == 0,
                    "An empty unplanned fleet has no invented unlocked-color slots");
                ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                Check(GameSession.ColorClearBarColors(spawner, grid).SequenceEqual(new[] { EnemyColor.Green }),
                    "Actual enemy colors create slots regardless of legacy unlock gates");
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                session.Progress.Reset(6);
                Check(GameSession.ColorClearBarColors(spawner, grid)
                    .SequenceEqual(new[] { EnemyColor.Green }),
                    "Changing the level cannot invent colors in the active fleet");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                var pitches = new List<float>();
                grid.GetComponent<SoundEffects>().CuePlayed += (cue, pitch) => { if (cue == SoundEffect.ColorClear) pitches.Add(pitch); };
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var redLast = ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 1);
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 4, 2);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(pitches.Count == 0, "Partial color clear has no reward tone");
                grid.ClearMatchingChain(redLast.Id, EnemyColor.Red);
                Check(pitches.Count == 1 && pitches[0] == 1, "First bar sounds at base pitch");
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(pitches.Count == 2 && pitches[1] > pitches[0], "Second color-clear event sounds higher");
                if (Application.isPlaying)
                {
                    var bursts = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None);
                    Check(bursts.Count(b => b.ColorClear) >= 2, "Actual final-color kills create spacedust bursts");
                }
                grid.ResetColorClearStreak();
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 0, 3);
                grid.SetColor(yellow.Id, EnemyColor.Purple);
                Check(pitches.Count == 2, "Transformation alone never awards a celebration");
                grid.ClearMatchingChain(green.Id, EnemyColor.Green);
                Check(pitches.Last() == 1, "Lost bar sequence resets pitch");
                var source = yellow.Visuals.Body;
                var dust = EnemyDeathBurst.Create(source, EnemyColor.Purple, 2, 4, true);
                try
                {
                    var grains = dust.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "Color-clear spacedust").ToArray();
                    Check(grains.Length == 32 && grains.All(s => !s.enabled && s.sortingOrder < source.sortingOrder),
                        "Spacedust waits for cascade and renders behind enemies");
                    Check(grains.All(s => s.sprite == EnemyPlaceholderArt.SpaceDust), "Dust uses soft glints instead of confetti triangles");
                    dust.Tick(EnemyDeathBurst.RingDelay + .2f);
                    Check(grains.All(s => s.enabled) && grains.Select(s => new Vector3(s.color.r, s.color.g, s.color.b)).Distinct().Count() == 6,
                        "Scattering dust contains all six enemy colors");
                    var before = grains[0].transform.localPosition.magnitude;
                    dust.Tick(.2f);
                    Check(grains[0].transform.localPosition.magnitude > before, "Dust scatters outward");
                    Check(dust.GetComponentInChildren<TextMesh>().text == "4", "Dust preserves the combo multiplier label");
                    dust.Tick(2);
                    Check(dust.Finished, "Dust cleans up after its individual arrivals");
                }
                finally { UnityEngine.Object.DestroyImmediate(dust.gameObject); }

                tongue.Cancel();
                Check(tongue.TryFire(EnemyColor.Purple, 4), "Fixture starts a timed return shot");
                tongue.Tick(4f / 14f);
                Check(tongue.Retracting && Mathf.Abs(tongue.RemainingReturnSeconds - .2f) < .001f,
                    "Tongue exposes its remaining return time");
                player.transform.position = new Vector3(0, -4.6f, 0);
                source.transform.position = new Vector3(0, 3, 0);
                var timedDust = EnemyDeathBurst.Create(source, EnemyColor.Purple, 1, 1, true);
                try
                {
                    var grains = Grains(timedDust);
                    tongue.Tick(1);
                    timedDust.Tick(EnemyDeathBurst.ColorClearScatterSeconds);
                    Check(!tongue.Active && !timedDust.Finished, "Tongue arrival does not finish dust return");
                    timedDust.Tick(EnemyDeathBurst.ColorClearStreamStaggerSeconds);
                    var before = grains.Select(grain => grain.transform.position).ToArray();
                    timedDust.Tick(0);
                    timedDust.Tick(-1);
                    Check(grains.Select(grain => grain.transform.position).SequenceEqual(before), "Paused or negative-time dust does not advance");
                    timedDust.Tick(.1f);
                    Check(grains.Select((grain, i) => Mathf.Abs(Vector3.Distance(grain.transform.position, before[i]) -
                        EnemyDeathBurst.ColorClearReturnSpeed * .1f)).All(error => error < .0001f),
                        "Every returning particle travels at the same constant world speed");
                    Check(grains.Select(s => new Vector3(s.color.r, s.color.g, s.color.b)).Distinct().Count() == 6,
                        "Returning dust retains all six colors");
                    before = grains.Select(grain => grain.transform.position).ToArray();
                    player.transform.position += Vector3.right * .3f;
                    timedDust.Tick(.1f);
                    Check(grains.Select((grain, i) => Mathf.Abs(Vector3.Distance(grain.transform.position, before[i]) -
                        EnemyDeathBurst.ColorClearReturnSpeed * .1f)).All(error => error < .0001f),
                        "Moving the player redirects the stream without accelerating or teleporting particles");
                    Check(grains.All(s => s.enabled && s.color.a >= .7f && s.transform.localScale.x >= .049f &&
                        s.transform.localScale.x == s.transform.localScale.y),
                        "Round dust glints retain their size and shimmer in flight");
                    var arrivals = new List<float>();
                    int remaining = grains.Length;
                    const float step = 1f / 120;
                    for (float time = 0; !timedDust.Finished && time < EnemyDeathBurst.ColorClearMaxSeconds; time += step)
                    {
                        timedDust.Tick(step);
                        int visible = grains.Count(grain => grain.enabled);
                        Check(visible <= remaining, "Absorbed particles never reappear");
                        for (int i = visible; i < remaining; i++) arrivals.Add(time);
                        remaining = visible;
                        var target = player.MagicAbsorptionTarget.position;
                        Check(grains.All(grain => grain.enabled || Vector3.Distance(grain.transform.position, target) < .001f),
                            "Each particle disappears at the player's current position");
                        Check(grains.Count(grain => grain.enabled && Vector3.Distance(grain.transform.position, target) < .18f) <= 8,
                            "Particles do not accumulate into a ball at the player");
                    }
                    Check(timedDust.Finished && remaining == 0 && arrivals.Count == grains.Length && arrivals.Last() - arrivals.First() > .25f,
                        "Particles arrive and vanish over a stream, not at one synchronized deadline");
                }
                finally
                {
                    tongue.Cancel();
                    UnityEngine.Object.DestroyImmediate(timedDust.gameObject);
                }
                CheckStreamTiming(source, player);
            });
            Debug.Log("Color-clear celebration checks passed: constant-speed dust streams, staggered absorption, larger grains, frame timing, final-color triggers, bars and tones.");
        }

        private static void CheckLostBars()
        {
            foreach (bool damage in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                int cues = 0;
                grid.GetComponent<SoundEffects>().CuePlayed += (cue, pitch) =>
                { if (cue == SoundEffect.BarPowerDown) cues++; };
                bool invincible = DeveloperOptions.PlayerInvincible;
                try
                {
                    DeveloperOptions.PlayerInvincible = false;
                    if (damage) Check(player.Hit(true), "Damage fixture accepts a player hit");
                    else
                    {
                        player.transform.position = new Vector3(-2.9f, -4.6f, 0);
                        Check(player.Fire() && tongue.IsMagic && cues == 0,
                            "Spending magic waits for the shot outcome before powering down bars");
                        tongue.Tick(10, grid);
                    }
                    Check(cues == 1 && session.LostBarAnimation.SimultaneousPowerDown && !grid.HasColorClearBar(EnemyColor.Red),
                        "A missed magic shot or player hit powers down the lost bars exactly once");
                }
                finally { DeveloperOptions.PlayerInvincible = invincible; }
            });
            foreach (float step in new[] { 1f / 30, 1f / 60, 1f / 120, .5f })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                grid.GetComponent<GameplayMusicPlayer>().StopPlayback();
                int powerDownCues = 0;
                grid.GetComponent<SoundEffects>().CuePlayed += (cue, pitch) =>
                { if (cue == SoundEffect.BarPowerDown) powerDownCues++; };
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 2, 0);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                grid.ClearMatchingChain(green.Id, EnemyColor.Green);
                grid.ResetColorClearStreak();
                Check(powerDownCues == 1 && session.LostBarAnimation.SimultaneousPowerDown &&
                    session.LostBarAnimation.Visible(EnemyColor.Red) && session.LostBarAnimation.Visible(EnemyColor.Green) &&
                    !session.LostBarAnimation.Visible(EnemyColor.Blue),
                    "Losing the streak powers down exactly the earned bars with the partial-stage sound");
                Check(grid.Fleet.ClearedColorCount == 0 && session.State == GameSession.RunState.Playing && !session.ClearCelebration.Active,
                    "Power-down releases spawn locks immediately and keeps gameplay running");
                grid.ResetColorClearStreak();
                Check(powerDownCues == 1, "Resetting an empty streak cannot repeat its sound");
                var slot = new Rect(20, 500, 80, 5);
                session.Tick(step);
                Check(session.DisplayedColorClearBarRect(EnemyColor.Red, slot, 0, 0).height > slot.height,
                    "Lost bars retain the shared partial-stage rise and fall while no longer logically powered");
                var progress = session.LostBarAnimation.PowerDownProgress;
                session.Pause(); session.Tick(10);
                Check(session.LostBarAnimation.PowerDownProgress == progress, "Pause freezes lost-bar power-down");
                session.Resume();
                // Re-earning a bar during the fade takes precedence over the old loss snapshot.
                var returningRed = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 1);
                grid.ClearMatchingChain(returningRed.Id, EnemyColor.Red);
                Check(grid.HasColorClearBar(EnemyColor.Red) &&
                    session.DisplayedColorClearBarRect(EnemyColor.Red, slot, 0, 0).width > slot.width,
                    "A newly earned bar immediately replaces its previous power-down");
                for (float time = 0; time < 3; time += step) session.Tick(step);
                Check(!session.LostBarAnimation.Active && grid.HasColorClearBar(EnemyColor.Red),
                    "Power-down finishes at every frame rate without removing newly earned progress");
            });
        }

        private static void CheckOverlappingWave()
        {
            var wave = new FullSetCelebration();
            var removed = new List<EnemyColor>();
            wave.ColorRemoved += removed.Add;
            wave.Begin(new List<EnemyColor> { EnemyColor.Blue, EnemyColor.Red, EnemyColor.Green }, duration: 1.5f);
            var resting = new Rect(0, 100, 30, 5);
            wave.Tick(.4f);
            Check(wave.Present(EnemyColor.Blue, resting).height > resting.height &&
                wave.Present(EnemyColor.Red, resting).height > resting.height && removed.Count == 0,
                "The next bar rises while the preceding bar is still falling");
            Check(wave.Present(EnemyColor.Green, resting) == resting,
                "The overlap remains local to neighboring wave bars");
            wave.Tick(.1f);
            Check(removed.SequenceEqual(new[] { EnemyColor.Red }) && !wave.Visible(EnemyColor.Red),
                "Overlapping motion preserves the exact beat-counted removal");
            wave.Tick(1);
            Check(wave.Finished && removed.Count == 3 && Mathf.Abs(wave.PlaybackDuration - 1.5f) < .0001f,
                "Overlap does not add delay to the next fleet");
        }

        private static void CheckBarUnlockTransition()
        {
            var row = new Rect(20, 500, 320, 5);
            foreach (int level in new[] { 1, 3, 5, 8 })
            foreach (bool partial in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                var music = grid.GetComponent<GameplayMusicPlayer>(); music.StopPlayback();
                session.Progress.Reset(level);
                session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated - 2, false);
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                var before = session.DisplayedColorClearBarColors;
                var original = session.DisplayedColorClearBarSlotRect(row, 0);
                int arrivals = 0; session.WaveSpawned += () => arrivals++;
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                if (partial) grid.ResetColorClearStreak();
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(session.Progress.Level == level + 1 && session.DisplayedColorClearBarColors.SequenceEqual(before),
                    "Level changes and reservations keep the outgoing fleet's exact bars");
                session.Tick(session.ClearCelebration.PlaybackDuration - .001f);
                Check(!session.ClearCelebration.Finished && session.DisplayedColorClearBarColors.SequenceEqual(before) &&
                    session.DisplayedColorClearBarSlotRect(row, 0) == original,
                    "Old colors and widths stay fixed through the last celebration frame");
                session.Pause(); session.Tick(10);
                Check(session.DisplayedColorClearBarSlotRect(row, 0) == original, "Pause preserves the current fleet slots");
                session.Resume(); music.StopPlayback();
                session.Tick(.002f); session.Tick(10); session.Tick(10);
                var target = grid.Fleet.PresentColors();
                Check(arrivals == 1 && session.State == GameSession.RunState.Playing &&
                    session.DisplayedColorClearBarColors.SequenceEqual(target),
                    "Bars switch once to actual spawned colors, without another layout timer");
                for (int i = 0; i < target.Count; i++)
                    Check(session.DisplayedColorClearBarSlotRect(row, i) ==
                        GameSession.ColorClearBarSlotRect(row.x, row.y, row.width, target.Count, i),
                        "The incoming fleet immediately uses equal-width slots");
            });
        }

        private static SpriteRenderer[] Grains(EnemyDeathBurst dust) => dust.GetComponentsInChildren<SpriteRenderer>(true)
            .Where(sprite => sprite.name == "Color-clear spacedust").ToArray();

        private static void CheckStreamTiming(SpriteRenderer source, PlayerMovement player)
        {
            var coarse = EnemyDeathBurst.Create(source, EnemyColor.Purple, 3, 3, true);
            var fine = EnemyDeathBurst.Create(source, EnemyColor.Purple, 3, 3, true);
            try
            {
                coarse.Tick(1.1f);
                for (int i = 0; i < 110; i++) fine.Tick(.01f);
                var a = Grains(coarse);
                var b = Grains(fine);
                Check(a.Select((grain, i) => Vector3.Distance(grain.transform.position, b[i].transform.position)).All(error => error < .001f),
                    "Large frames and small frames agree across cascade, scatter and staggered launch boundaries");
                coarse.Tick(10);
                Check(coarse.Finished && a.All(grain => !grain.enabled), "A long frame absorbs all remaining particles exactly once");
                coarse.Tick(10);
                fine.Tick(EnemyDeathBurst.ColorClearMaxSeconds);
                Check(fine.Finished, "Fine-step stream also finishes cleanly");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coarse.gameObject);
                UnityEngine.Object.DestroyImmediate(fine.gameObject);
            }

            var far = EnemyDeathBurst.Create(source, EnemyColor.Purple, 1, 1, true);
            var saved = source.transform.position;
            source.transform.position = player.MagicAbsorptionTarget.position + Vector3.up * 2;
            var near = EnemyDeathBurst.Create(source, EnemyColor.Purple, 1, 1, true);
            source.transform.position = saved;
            try
            {
                float nearFinish = 0, farFinish = 0;
                for (float time = .01f; time < EnemyDeathBurst.ColorClearMaxSeconds; time += .01f)
                {
                    if (!near.Finished) { near.Tick(.01f); if (near.Finished) nearFinish = time; }
                    if (!far.Finished) { far.Tick(.01f); if (far.Finished) farFinish = time; }
                    if (near.Finished && far.Finished) break;
                }
                Check(nearFinish > 0 && farFinish > nearFinish + .5f,
                    "Distant enemies produce longer streams instead of faster particles with the same arrival time");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(near.gameObject);
                UnityEngine.Object.DestroyImmediate(far.gameObject);
            }
        }

        public static void CapturePreview()
        {
            Run();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 2);
            ProgressionChecks.Add(grid, EnemyColor.Red, 1, 2);
            ProgressionChecks.Add(grid, EnemyColor.Red, 3, 2);
            var dust = EnemyDeathBurst.Create(blue.Visuals.Body, EnemyColor.Blue, 1, 3, true);
            blue.Visuals.Body.enabled = false;
            dust.Tick(.4f);
            GameplayChecks.Capture(540, 960, "color-clear-spacedust");
            GameplayChecks.Capture(960, 540, "color-clear-spacedust-landscape");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Color-clear celebration check failed: " + message); }
    }
}
