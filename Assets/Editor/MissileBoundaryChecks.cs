using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MissileBoundaryChecks
    {
        public static void Run()
        {
            foreach (float side in new[] { -1f, 1f }) CheckEdge(side);
            foreach (bool aimed in new[] { false, true }) CheckDespawn(aimed);
            CheckCollision();
            CheckExpiry();
            Debug.Log("Missile boundary checks passed: no wrapping at either edge, pointed-only positional audio, swept collision, silent despawn and expiry grace.");
        }

        private static EnemyMissile Create(PlayerMovement player, SoundEffects sounds, Vector3 position, Vector3? heading = null)
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"));
            root.transform.position = position;
            var missile = root.GetComponent<EnemyMissile>();
            SpecialEnemyChecks.StartAtCruiseSpeed(missile);
            if (heading.HasValue) missile.LaunchAimed(player, heading.Value);
            else missile.SetTarget(player);
            var flight = root.GetComponent<MissileFlightSound>();
            var settings = new SerializedObject(flight);
            settings.FindProperty("audibleDistance").floatValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            flight.Configure(missile, sounds);
            return missile;
        }

        private static void CheckEdge(float side)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                player.transform.position = new Vector3(side * 2.5f, -4.6f, 0);
                var warning = Create(player, sounds, new Vector3(side * 2.95f, -4.6f, 0), Vector3.left * side);
                var warningVoice = warning.GetComponent<MissileFlightSound>().Source;
                Check(warningVoice.volume > 0 && Mathf.Sign(warningVoice.panStereo) == side,
                    "A nearby missile sounds on its physical side only while aimed at the player");
                var missile = Create(player, sounds, new Vector3(side * 2.95f, -4.6f, 0), Vector3.right * side);
                var flight = missile.GetComponent<MissileFlightSound>();
                var voice = flight.Source;
                Check(voice.volume == 0 && Mathf.Sign(voice.panStereo) == side,
                    "A nearby missile moving away from the player is silent");
                var clip = voice.clip;
                missile.Tick(.1f);
                Check(missile.transform.position.x * side > PlayerMovement.HalfWidth,
                    "Missiles continue offscreen without wrapping");
                Check(!missile.Finished && voice.clip == clip && voice.volume == 0,
                    "Crossing the edge keeps a nearby outward missile silent until cleanup");
                var paused = missile.transform.position;
                missile.Suspended = true;
                missile.Tick(2);
                Check(missile.transform.position == paused && voice.volume == 0 && voice.clip == null,
                    "Pause freezes offscreen flight and silences it");
                missile.Suspended = false;
                Check(voice.volume == 0 && voice.clip == null, "An offscreen outward missile remains silent after pause");
                missile.transform.position = player.transform.position + new Vector3(side * 2.05f, 1, 0);
                flight.Refresh();
                Check(voice.volume == 0, "Flight audio fades completely before side cleanup");
                missile.Tick(.1f);
                Check(missile.Finished && missile.transform.position.x * side > PlayerMovement.HalfWidth,
                    "Silent missiles retire beyond the side they exited");
                var coarse = Create(player, sounds, new Vector3(side * 2.9f, -3.6f, 0), Vector3.right * side);
                var fine = Create(player, sounds, coarse.transform.position, Vector3.right * side);
                coarse.Tick(1);
                for (int i = 0; i < 60; i++) fine.Tick(1f / 60);
                Check(coarse.Finished && fine.Finished && Vector3.Distance(coarse.transform.position, fine.transform.position) < .001f,
                    "Long and short frames agree on offscreen flight and cleanup");
                player.transform.position = new Vector3(-side * 2.5f, -4.6f, 0);
                var opposite = Create(player, sounds, new Vector3(side * 2.95f, -3.6f, 0), Vector3.right * side);
                var oppositeVoice = opposite.GetComponent<MissileFlightSound>().Source;
                Check(oppositeVoice.volume == 0 && Mathf.Sign(oppositeVoice.panStereo) == side,
                    "Opposite edges are neither nearby nor reversed in the stereo field");
                opposite.Tick(.1f);
                Check(opposite.Finished && opposite.transform.position.x * side > PlayerMovement.HalfWidth,
                    "A far missile leaving either edge cannot reappear near the player");
            });
        }

        private static void CheckDespawn(bool aimed)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                var missile = Create(player, sounds, new Vector3(player.transform.position.x, -6.45f, 0),
                    aimed ? Vector3.down : (Vector3?)null);
                var flight = missile.GetComponent<MissileFlightSound>();
                missile.Tick(.02f);
                Check(!missile.Finished && flight.Source.volume == 0,
                    "Passed missiles stay silent below the old bottom kill field");
                var settings = new SerializedObject(flight);
                settings.FindProperty("audibleDistance").floatValue = 4;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Check(player.transform.position.y - missile.BottomDespawnY > flight.AudibleDistance,
                    "The bottom kill field also accommodates customized hearing ranges");
                missile.transform.position = new Vector3(player.transform.position.x, missile.BottomDespawnY + .05f, 0);
                flight.Refresh();
                Check(flight.Source.volume == 0, "The missile is already completely silent before reaching the bottom kill field");
                missile.Tick(.1f);
                Check(missile.Finished && !missile.gameObject.activeSelf,
                    "Both ordinary and aimed missiles retire beyond the new silent boundary");
            });
        }

        private static void CheckCollision()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                player.transform.position = new Vector3(0, -4.6f, 0);
                var missile = Create(player, sounds, new Vector3(2.95f, -4.6f, 0), Vector3.right);
                missile.Tick(.1f);
                Check(missile.Finished && player.Alive, "A missile leaving the field never sweeps through its middle");
                player.transform.position = new Vector3(-2.7f, -4.6f, 0);
                missile = Create(player, sounds, new Vector3(2.8f, -4.6f, 0), Vector3.right);
                missile.Tick(.2f);
                Check(missile.Finished && player.Alive, "Missiles cannot hit the player through the opposite edge");
                player.transform.position = new Vector3(0, -4.6f, 0);
                missile = Create(player, sounds, player.transform.position + Vector3.left, Vector3.right);
                missile.Tick(1);
                Check(missile.Finished && !player.Alive, "Direct swept collision still catches hits during long frames");
            });
        }

        private static void CheckExpiry()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var sounds = grid.gameObject.AddComponent<SoundEffects>();
                var missile = Create(player, sounds, player.transform.position + Vector3.down, Vector3.down);
                typeof(EnemyMissile).GetField("age", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(missile, 4.99f);
                missile.Tick(.02f);
                Check(!missile.Finished && missile.GetComponent<MissileFlightSound>().Source.volume == 0,
                    "Lifetime expiry leaves a nearby passed missile silent");
                missile.Tick(1);
                Check(missile.Finished && MissileFlightSound.PlanarOffset(missile.transform.position, player.transform.position).magnitude > 2,
                    "Expired missiles retire once they have receded beyond hearing range");
            });
        }

        private static void TickUntilBeep(EnemyMissile missile, MissileFlightSound flight)
        {
            int before = flight.PlayedBeeps;
            for (int i = 0; i < 90 && !missile.Finished; i++)
            {
                missile.Tick(1f / 120);
                if (flight.PlayedBeeps > before) return;
            }
            throw new Exception("Missile boundary check failed: Flash beat did not produce a beep");
        }

        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Missile boundary check failed: " + message); }
    }
}
