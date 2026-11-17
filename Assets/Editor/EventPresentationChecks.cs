using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace CandyCruisers.Editor
{
    public static class EventPresentationChecks
    {
        public static void Run()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                foreach (SoundEffect effect in Enum.GetValues(typeof(SoundEffect)))
                {
                    if (!SoundEffects.IsPlayableEffect(effect))
                    {
                        Check(sounds.GetClip(effect) == null && !sounds.PlayCue(effect),
                            "Retired movement, Red launch and wave-arrival cues stay unavailable");
                        continue;
                    }
                    var clip = sounds.GetClip(effect);
                    Check(clip != null, "Every cue has a replaceable fallback clip");
                    var samples = new float[clip.samples]; clip.GetData(samples, 0);
                    Check(samples.Any(value => Mathf.Abs(value) > .01f) && samples.All(value => Mathf.Abs(value) <= 1),
                        "Every cue contains audible, unclipped samples");
                    if (effect == SoundEffect.EnemyDefeat)
                    {
                        int firstHalf = 0, secondHalf = 0;
                        for (int i = 1; i < samples.Length; i++)
                            if (samples[i - 1] <= 0 && samples[i] > 0)
                            { if (i < samples.Length / 2) firstHalf++; else secondHalf++; }
                        Check(Math.Abs(firstHalf - secondHalf) <= 1 && firstHalf + secondHalf >= 33 && firstHalf + secondHalf <= 35,
                            "Death sound holds concert C instead of sweeping downward");
                    }
                }
                int[] arpeggio = { 0, 4, 7, 12, 16, 19 };
                for (int multiplier = 1; multiplier <= 36; multiplier++)
                    Check(Mathf.Abs(SoundEffects.DefeatPitch(multiplier) - Mathf.Pow(2, arpeggio[(multiplier - 1) % 6] / 12f)) < .00001f &&
                        SoundEffects.DefeatPitch(multiplier) <= 3, "Death notes repeat a major arpeggio within playback pitch limits");
                var played = new List<SoundEffect>(); sounds.CuePlayed += (cue, pitch) => played.Add(cue);
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                red.GetComponent<EnemyAbilities>().Tick(30);
                Check(!played.Contains(SoundEffect.RedFire), "Red launches have no firing cue");
                Check(UnityEngine.Object.FindObjectsByType<MissileFlightSound>(FindObjectsSortMode.None)
                    .Any(flight => flight.Source != null && !flight.Source.loop),
                    "Red launch gives the projectile its own flash-beep voice");
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 1, 0);
                int greenCueCount = played.Count;
                grid.GetComponent<EnemyGridMovement>().Tick(5);
                Check(!played.Skip(greenCueCount).Contains(SoundEffect.GreenStep), "Fleet tick keeps Green movement silent");
                ProgressionChecks.Add(grid, EnemyColor.Green, 1, 1);
                ProgressionChecks.Add(grid, EnemyColor.Green, 2, 1);
                grid.RefreshSpecials();
                greenCueCount = played.Count;
                green.GetComponent<EnemyAbilities>().Tick(30); green.GetComponent<EnemyAbilities>().Tick(.61f);
                Check(!played.Skip(greenCueCount).Contains(SoundEffect.GreenDash), "Special Green dash keeps movement silent");
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                blue.GetComponent<EnemyAbilities>().Tick(30);
                Check(played.Contains(SoundEffect.ShieldPower), "Shield power-up emits cue");
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 3, 0);
                var copy = yellow.GetComponent<EnemyAbilities>();
                Check(copy.BeginImitation(blue) && played.Contains(SoundEffect.YellowTransform), "Ordinary Yellow transformation cue");
                grid.SetColor(yellow.Id, EnemyColor.Red); copy.Tick(0);
                grid.SetColor(yellow.Id, EnemyColor.Yellow); copy.Tick(0);
                ProgressionChecks.Add(grid, EnemyColor.Yellow, 3, 1);
                ProgressionChecks.Add(grid, EnemyColor.Yellow, 4, 1);
                grid.RefreshSpecials();
                Check(copy.BeginImitation(blue) && played.Contains(SoundEffect.YellowHide), "Special Yellow hide cue");
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(yellow.Color == EnemyColor.Yellow && played.Contains(SoundEffect.YellowReveal),
                    "Host death emits the special Yellow reversion cue");
                var purple = ProgressionChecks.Add(grid, EnemyColor.Purple, 0, 2);
                Check(grid.GetComponent<EnemyRowSpawner>().TrySummon(purple) != null && played.Contains(SoundEffect.PurpleWarp), "Purple warp cue");
                player.Hit();
                Check(player.GrantLife() && played.Contains(SoundEffect.OneUp), "One-up cue only on a granted life");
                sounds.SetPaused(true);
                int count = played.Count;
                Check(!sounds.PlayCue(SoundEffect.ShieldPower) && played.Count == count, "Paused event sounds do not play");
                sounds.SetPaused(false);
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 2);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 3, 2);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 3);
                grid.RefreshSpecials();
                var mesh = grid.GetComponentInChildren<MeshFilter>().sharedMesh;
                Check(mesh.colors.Any(c => c.a == 1) && mesh.colors.Any(c => c.a == 0) &&
                    mesh.colors.Any(c => c.a > 0 && c.a < 1), "Group shield has a solid rim and transparent inward taper");
                Check(grid.GroupShield.EdgeCount == 8, "Group shield retains the original collision outline");
                Check(grid.GroupShield.ContourCount == 1 && mesh.vertexCount == grid.GroupShield.EdgeCount * 9,
                    "Corner turns share miter vertices so the shield perimeter has no open wedges");
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var presentation = red.GetComponent<EnemyPresentation>();
                var original = red.Visuals.Body.transform.localScale;
                Bounds bounds = red.HitBounds;
                red.GetComponent<EnemyAbilities>().BeginSpawnEffect();
                Check(red.Visuals.Body.transform.localScale == Vector3.zero && red.HitBounds == bounds, "Growth begins at zero without shrinking hitbox");
                presentation.Tick(.5f, EnemyColor.Red, 0);
                Check(red.Visuals.Body.transform.localScale.x > original.x && !presentation.IsWarping, "Regular arrival overshoots without a portal");
                presentation.Tick(.3f, EnemyColor.Red, 0);
                Check(red.Visuals.Body.transform.localScale == original && !presentation.IsPhasing, "Growth settles to original scale");
                red.GetComponent<EnemyAbilities>().BeginSpawnEffect(EnemyColor.Purple, true);
                Check(presentation.IsWarping && red.Visuals.Body.transform.localScale == original, "Purple warp remains a distinct full-sized phase effect");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                var sounds = grid.GetComponent<SoundEffects>();
                var played = new List<SoundEffect>();
                var pitches = new List<float>();
                sounds.CuePlayed += (effect, pitch) => { played.Add(effect); pitches.Add(pitch); };
                var plan = new[] { EnemyColor.Red, EnemyColor.Red, EnemyColor.Red, EnemyColor.Blue, EnemyColor.Blue };
                Check(grid.GetComponent<EnemyRowSpawner>().SpawnBatch(plan) && !played.Contains(SoundEffect.WaveSpawn),
                    "A new batch spawns without a wave-arrival sound cue");
                session.Progress.RegisterClear(session.Progress.NextThreshold - 1, false);
                grid.ClearMatchingChain(grid.Model.At(0, 0).Id, EnemyColor.Red);
                Check(session.Progress.Level == 1 && session.Progress.LevelUpPending &&
                    played.Count(e => e == SoundEffect.LevelUp) == 0, "Mid-wave threshold crossing banks the level without a cue");
                var body = new GameObject("Defeat cue source", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                body.transform.SetParent(grid.transform, false);
                body.sprite = grid.View(grid.Model.At(3, 0).Id).Visuals.Body.sprite;
                grid.ClearMatchingChain(grid.Model.At(3, 0).Id, EnemyColor.Blue);
                Check(session.Progress.Level == 2 && played.Count(e => e == SoundEffect.LevelUp) == 1,
                    "Level transition plays one cue when the fleet is cleared");
                int count = played.Count;
                var cue = PresentationCue.Spawn(null, PresentationCue.Kind.Defeat, body, EnemyColor.Blue, 3, 8);
                try
                {
                    Check(played.Count == count, "Defeat sound waits for its cascade delay");
                    cue.Tick(EnemyDeathBurst.RingDelay * 2);
                    Check(played.Last() == SoundEffect.EnemyDefeat && Mathf.Abs(pitches.Last() - SoundEffects.DefeatPitch(8)) < .001f,
                        "Delayed defeat uses the displayed multiplier for pitch");
                    count = played.Count; cue.Tick(.01f);
                    Check(played.Count == count, "Defeat cue sounds once");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cue.gameObject);
                    UnityEngine.Object.DestroyImmediate(body.gameObject);
                }
            });
            Debug.Log("Event presentation checks passed: clip samples, gameplay cues, pitch, pause, group shield fade and growth versus warp arrivals.");
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 2);
            ProgressionChecks.Add(grid, EnemyColor.Blue, 3, 2);
            ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 3);
            grid.RefreshSpecials();
            var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 2);
            red.GetComponent<EnemyAbilities>().BeginSpawnEffect();
            red.GetComponent<EnemyPresentation>().Tick(.45f, EnemyColor.Red, 0);
            var purple = ProgressionChecks.Add(grid, EnemyColor.Purple, 4, 3);
            purple.GetComponent<EnemyAbilities>().BeginSpawnEffect(EnemyColor.Purple, true);
            purple.GetComponent<EnemyPresentation>().Tick(.3f, EnemyColor.Purple, 0);
            GameplayChecks.Capture(540, 960, "group-shield-growth-warp");
            GameplayChecks.Capture(960, 540, "group-shield-growth-warp-landscape");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Event presentation check failed: " + message); }
    }
}
