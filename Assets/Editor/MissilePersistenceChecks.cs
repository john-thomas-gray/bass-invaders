using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MissilePersistenceChecks
    {
        public static void RunInPlayMode()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            SessionState.SetBool("CandyCruisers.MissileClearChecks", true);
            RestorePlayModeChecks();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void RestorePlayModeChecks()
        {
            if (SessionState.GetBool("CandyCruisers.MissileClearChecks", false))
                EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            SessionState.SetBool("CandyCruisers.MissileClearChecks", false);
            EditorApplication.playModeStateChanged -= OnPlayMode;
            try { RunRelatedChecks(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void RunRelatedChecks()
        {
            using var songs = new SongSelectionCheckSettings();
            using var metronomes = new MetronomeCheckSettings();
            Run();
            MissileBoundaryChecks.Run();
            MissileFlightSoundChecks.Run();
            if (Application.isPlaying) WaveTransitionChecks.RunRefillDelayEnvelope();
            else WaveTransitionChecks.Run();
        }

        public static void Run()
        {
            foreach (bool special in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                // This synchronous lifetime check drives the silent fallback clock, not streamed playback.
                var music = grid.GetComponent<GameplayMusicPlayer>();
                music.Source.Stop();
                music.Source.clip = null;
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                if (special)
                {
                    ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                    grid.RefreshSpecials();
                }
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                var previous = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                red.GetComponent<EnemyAbilities>().Tick(100);
                var missile = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None)
                    .Single(candidate => !previous.Contains(candidate));
                Check(missile.Aimed == special && !missile.Homing && missile.transform.parent == null, "Fired missile is independent of shooter");
                if (special) missile.LaunchAimed(player, missile.transform.rotation * Vector3.down);
                else missile.SetTarget(player);
                var flight = missile.GetComponent<MissileFlightSound>();
                flight.Refresh();
                Check(flight.Source != null && !flight.Source.loop, "Both Red tiers launch a beat-beep voice on the missile");
                missile.transform.position = player.transform.position + Vector3.up * 1.5f;
                flight.Refresh();
                TickUntilBeep(missile, flight);
                Check(flight.Source.clip != null && !flight.Source.loop, "Both Red tiers beep on their first flash peak");
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(missile != null && missile.isActiveAndEnabled && !missile.Finished && !missile.Suspended,
                    "Destroying shooter preserves its fired missile");
                var beforeBursts = UnityEngine.Object.FindObjectsByType<MissileClearBurst>(FindObjectsSortMode.None);
                var position = missile.transform.position;
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(session.State == GameSession.RunState.Refilling && missile.Finished && !missile.gameObject.activeSelf,
                    "Clearing the final enemy immediately retires airborne missiles");
                Check(!flight.Source.isPlaying && flight.Source.clip == null && flight.Source.volume == 0,
                    "Wave clear immediately silences the missile voice");
                var burst = UnityEngine.Object.FindObjectsByType<MissileClearBurst>(FindObjectsSortMode.None)
                    .Single(candidate => !beforeBursts.Contains(candidate));
                try
                {
                    Check(burst.transform.position == position && burst.transform.parent == null,
                        "A detached explosion appears at the missile's final position");
                    missile.ClearWithBurst();
                    Check(UnityEngine.Object.FindObjectsByType<MissileClearBurst>(FindObjectsSortMode.None).Length == beforeBursts.Length + 1,
                        "Repeated clearing cannot duplicate the explosion");
                    missile.Tick(1);
                    Check(missile.transform.position == position && player.Alive && player.Lives == PlayerMovement.MaxLives,
                        "Cleared missiles cannot move or hurt the player during refill");
                    var spark = burst.GetComponentInChildren<SpriteRenderer>();
                    var initial = spark.transform.localPosition;
                    burst.Tick(MissileClearBurst.Duration / 2);
                    Check(!burst.Finished && spark.color.a > 0 && spark.color.a < 1 && spark.transform.localPosition != initial,
                        "Explosion sparks expand and fade during their brief lifetime");
                    burst.Tick(MissileClearBurst.Duration);
                    Check(burst.Finished && !burst.gameObject.activeSelf, "Explosion cleans up after its animation");
                }
                finally { UnityEngine.Object.DestroyImmediate(burst.gameObject); }
            });
            Debug.Log("Missile persistence checks passed: ordinary and aimed shooter death, immediate wave-clear retirement, silent flight voices and brief detached explosions.");
        }
        private static void TickUntilBeep(EnemyMissile missile, MissileFlightSound flight)
        {
            int before = flight.PlayedBeeps;
            for (int i = 0; i < 90 && !missile.Finished; i++)
            {
                missile.Tick(1f / 120);
                if (flight.PlayedBeeps > before) return;
            }
            throw new Exception("Missile persistence check failed: Flash beat did not produce a beep");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Missile persistence check failed: " + message); }
    }
}
