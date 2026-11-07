using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MissilePersistenceChecks
    {
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
                var flightClip = flight.Source.clip;
                Check(flightClip != null && !flight.Source.loop, "Both Red tiers beep on their first flash peak");
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(missile != null && missile.isActiveAndEnabled && !missile.Finished && !missile.Suspended,
                    "Destroying shooter preserves its fired missile");
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(session.State == GameSession.RunState.Refilling && missile.isActiveAndEnabled && !missile.Suspended,
                    "Clearing the final enemy also preserves active bullets");
                Check(flight.Source.clip == flightClip, "Destroying the shooter and clearing the fleet preserve its beep voice");
                var before = missile.transform.position;
                missile.Tick(.1f);
                Check(missile.transform.position.y < before.y, "Missile continues moving during refill");
                session.Pause();
                before = missile.transform.position;
                missile.Tick(1);
                Check(missile.transform.position == before, "Pause still freezes surviving missiles");
                Check(flight.Source.volume == 0 && !flight.Source.isPlaying && flight.Source.clip == null,
                    "Pause silences surviving missile beeps");
                session.Resume();
                Check(!missile.Suspended, "Resume during refill releases surviving missiles");
                // A surviving bullet must remain dangerous even with no enemies left.
                missile.transform.position = player.transform.position - missile.transform.rotation * Vector3.down;
                missile.Tick(1f);
                Check(missile.Finished && !player.Alive && player.Lives == PlayerMovement.MaxLives - 1,
                    "Surviving missile can hit the player during refill");
            });
            Debug.Log("Missile persistence checks passed: ordinary and aimed shooter death, fleet clear, continued travel, pause/resume and refill hits.");
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
