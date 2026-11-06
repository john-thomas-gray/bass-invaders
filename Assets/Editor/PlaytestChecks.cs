using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlaytestChecks
    {
        public static void Run()
        {
            CheckCalibration();
            foreach (float step in new[] { 1f / 30, 1f / 60, 1f / 120 }) CheckRecording(step);
            CheckOpportunity(false); CheckOpportunity(true);
            CheckShield();
            CheckStageAttribution();
            CheckDriverAim();
            CheckDriverHazardDuringShot();
            CheckDriverBlockedColor();
            CheckDriverTracksMovingFleet();
            CheckDriverWaitsWhileDodging();
            foreach (var screen in new[] { new Vector2(320, 320), new Vector2(390, 844), new Vector2(960, 540) })
            {
                var safe = new Rect(10, 20, screen.x - 20, screen.y - 40);
                var panel = PlaytestHarness.PanelRect(screen, safe);
                Require(panel.xMin >= safe.xMin && panel.xMax <= safe.xMax && panel.yMin >= screen.y - safe.yMax && panel.yMax <= screen.y - safe.yMin,
                    "Development controls stay within portrait, landscape and small-screen safe areas");
            }
            Debug.Log("Playtest checks passed: per-stage calibration, sparse evidence, rejected assisted runs, read-only shot capacity, cancellations and recording at 30/60/120 frames per second.");
        }
        private static void CheckCalibration()
        {
            var run = new PlaytestRunData { id = "a", controller = "human", completed = true, configurationHash = "map-a" };
            var stage1 = run.Stage(1); var stage7 = run.Stage(7);
            stage1.normal.fired = stage1.normal.completed = 10; stage1.normal.hits = 8;
            stage7.normal.fired = stage7.normal.completed = 100; stage7.normal.hits = 80;
            stage1.combatSeconds = 100; stage7.combatSeconds = 100;
            stage1.normal.kills = 20; stage7.normal.kills = 200;
            stage1.enemySeconds = 1000; stage7.enemySeconds = 10000;
            var profile = PlaytestProfile.Calibrate("player", new[] { run, run });
            Require(profile.sourceRunIds.Count == 1 && profile.ForLevel(1).shots == 10 && profile.ForLevel(7).shots == 100,
                "Duplicate recordings do not inflate evidence and late-stage counts do not replace early-stage benchmarks");
            Require(Mathf.Approximately(profile.ForLevel(1).normalHitRate, profile.ForLevel(7).normalHitRate) &&
                Mathf.Approximately(stage1.KillsPerEnemySecond, stage7.KillsPerEnemySecond), "Stage and enemy exposure normalization");
            Require(profile.ForLevel(8) == null && !profile.ForLevel(1).HasMagicEvidence, "Unseen stages and unobserved magic remain unmeasured");
            run.assisted = true;
            Reject(() => PlaytestProfile.Calibrate("bad", new[] { run })); run.assisted = false;
            run.controller = "bot"; Reject(() => PlaytestProfile.Calibrate("bad", new[] { run })); run.controller = "human";
            var other = new PlaytestRunData { id = "b", controller = "human", completed = true, configurationHash = "map-b" };
            Reject(() => PlaytestProfile.Calibrate("bad", new[] { run, other }));
            string json = JsonUtility.ToJson(profile);
            Require(JsonUtility.FromJson<PlaytestProfile>(json).ForLevel(7).observed.normal.hits == 80, "Profile survives serialization");
        }
        private static void CheckRecording(float step)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                Lifecycle(session, "OnEnable");
                string root = Path.Combine(Path.GetTempPath(), "bass-playtest-check-" + Guid.NewGuid().ToString("N"));
                try
                {
                    var target = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 2);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 2, 2);
                    ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                    // Select Red before adding a differently colored enemy would randomize it.
                    for (int i = 0; i < 100 && player.ReadyColor != EnemyColor.Red; i++) player.RefreshColor(true);
                    player.transform.position = new Vector3(target.transform.position.x, -4.6f, 0);
                    var data = new PlaytestRunData { id = "run", controller = "human", startLevel = 1 };
                    using (var recorder = new PlaytestRecorder(session, player, data, root))
                    {
                        recorder.Tick(2);
                        Require(player.Fire(), "Recorded shot uses the real firing method");
                        for (int i = 0; i < 1000 && tongue.Active; i++) { tongue.Tick(step, grid); recorder.Tick(step); }
                        var stage = data.Stage(1);
                        Require(stage.normal.fired == 1 && stage.normal.completed == 1 && stage.normal.hits == 1 && stage.normal.kills == 2,
                            "Actual chain kills are attributed once to the launch stage");
                        Require(stage.magicAwards == 1 && stage.normal.Efficiency == 1 && stage.normal.contacts == 1, "Earned charge and snapshot-relative efficiency recorded");
                        Require(player.Fire() && tongue.IsMagic, "Earned charge uses normal magic rules");
                        tongue.Cancel();
                        Require(stage.magic.interrupted == 1 && stage.magic.completed == 0 && stage.magic.hits == 0,
                            "An interrupted magic shot is not counted as a completed miss");
                        recorder.Finish("manual_stop");
                    }
                    string summary = Path.Combine(root, "Runs", "run", "summary.json");
                    Require(File.Exists(summary) && JsonUtility.FromJson<PlaytestRunData>(File.ReadAllText(summary)).completed, "Atomic summary is readable");
                    var lines = File.ReadAllLines(Path.Combine(root, "Runs", "run", "events.jsonl"));
                    Require(lines.Count(line => JsonUtility.FromJson<PlaytestRecorder.Event>(line).type == "shot_started") == 2,
                        "Action log contains both accepted shots");
                }
                finally { Lifecycle(session, "OnDisable"); UnityEngine.Object.DestroyImmediate(session); if (Directory.Exists(root)) Directory.Delete(root, true); }
            });
        }
        private static void CheckOpportunity(bool magic)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var target = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 3);
                ProgressionChecks.Add(grid, EnemyColor.Red, 2, 3);
                player.RefreshColor(true);
                ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 0);
                if (magic)
                {
                    var charge = ProgressionChecks.Add(grid, EnemyColor.Green, 5, 0);
                    grid.ClearMatchingChain(charge.Id, EnemyColor.Green);
                }
                var random = UnityEngine.Random.state;
                int before = grid.Model.Count;
                var observation = new PlaytestObservation(grid, player);
                Require(before == grid.Model.Count && JsonUtility.ToJson(random) == JsonUtility.ToJson(UnityEngine.Random.state), "Observation preserves the board and gameplay random stream");
                int expected = observation.At(target.transform.position.x);
                Require(expected == (magic ? 3 : 2) && observation.Best == expected, "Best stationary lane handles ordinary groups and penetrating magic");
                player.transform.position = new Vector3(target.transform.position.x, -4.6f, 0);
                Require(player.Fire(), "Opportunity fixture fires");
                tongue.Tick(3, grid);
                Require(before - grid.Model.Count == expected, "Snapshot agrees with actual stationary combat resolution");
            });
        }
        private static void Lifecycle(GameSession session, string method) => typeof(GameSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
        private static void CheckDriverAim()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                foreach (int column in new[] { 0, 2, 3, 5 }) ProgressionChecks.Add(grid, EnemyColor.Red, column, 0);
                player.RefreshColor(true);
                var policy = new PlaytestBenchmark { level = 1, normalHitRate = 1, normalEfficiency = 1,
                    movementFraction = 1, readyDelay = .05f, reactionSeconds = .1f };
                policy.observed.normal.completed = 10;
                var profile = new PlaytestProfile { configurationHash = "fixture" }; profile.stages.Add(policy);
                var driver = new PlaytestDriver(player, grid, profile, true, 54321, "fixture");
                for (int i = 0; i < 120 && !player.ShotActive; i++) driver.Tick(1f / 30, 1);
                Require(player.ShotActive && new PlaytestObservation(grid, player).At(player.transform.position.x, .06f) == 2,
                    "Bot targets the interior of a group instead of the animated gap between its members");
                tongue.Tick(3, grid);
                Require(grid.Model.Count == 2, "Aimed bot shot resolves through actual combat");
            });
        }
        private static void CheckDriverHazardDuringShot()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0); player.RefreshColor(true);
                var policy = new PlaytestBenchmark { level = 1, normalHitRate = 1, normalEfficiency = 1,
                    movementFraction = 1, readyDelay = 2, reactionSeconds = .1f, dodgeProbability = 1 };
                policy.observed.normal.completed = 10;
                var profile = new PlaytestProfile { configurationHash = "fixture" }; profile.stages.Add(policy);
                var driver = new PlaytestDriver(player, grid, profile, true, 54321, "fixture");
                driver.Tick(2.01f, 1);
                Require(player.ShotActive, "The slow-cadence profile fires through the normal method");
                float before = player.transform.position.x;
                var missile = new GameObject("Calibration hazard", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                missile.transform.position = player.transform.position + Vector3.up;
                missile.SetTarget(player);
                driver.Tick(.11f, 1);
                Require(Mathf.Abs(PlayerMovement.Wrap(player.transform.position.x - before)) > .1f && player.ShotActive,
                    "The bot reacts to a new missile during its shot instead of waiting for its firing delay");
            });
        }
        private static void CheckDriverBlockedColor()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0); player.RefreshColor(true);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 3);
                var shield = blue.GetComponent<EnemyAbilities>(); shield.Tick(shield.CooldownRemaining + .35f);
                Require(new PlaytestObservation(grid, player).Best == 0, "The selected color is fully shielded in the fixture");
                var policy = new PlaytestBenchmark { level = 1, normalHitRate = 1, normalEfficiency = 1,
                    movementFraction = 1, readyDelay = .1f, reactionSeconds = .1f };
                policy.observed.normal.completed = 10;
                var profile = new PlaytestProfile { configurationHash = "fixture" }; profile.stages.Add(policy);
                var driver = new PlaytestDriver(player, grid, profile, true, 54321, "fixture");
                driver.Tick(1, 1);
                Require(player.ShotActive, "A perfect observed hit rate does not freeze the bot when a shot is needed to break a shield or change color");
            });
        }
        private static void CheckDriverTracksMovingFleet()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0); player.RefreshColor(true);
                var policy = new PlaytestBenchmark { level = 1, normalHitRate = 1, normalEfficiency = 1,
                    movementFraction = 1, readyDelay = .1f, reactionSeconds = .1f };
                policy.observed.normal.completed = 10;
                var profile = new PlaytestProfile { configurationHash = "fixture" }; profile.stages.Add(policy);
                var driver = new PlaytestDriver(player, grid, profile, true, 54321, "fixture");
                driver.Tick(1, 1); tongue.Tick(.1f, grid);
                Require(player.ShotActive && !tongue.Retracting, "Moving-target fixture has an extending shot");
                float before = player.transform.position.x;
                grid.transform.position += Vector3.left * .3f;
                driver.Tick(.11f, 1);
                Require(player.transform.position.x < before - .2f, "The bot follows a formation that moves during extension");
                tongue.Tick(3, grid);
                Require(grid.Model.Count == 0, "The tracked shot hits through actual combat rules");
            });
        }
        private static void CheckDriverWaitsWhileDodging()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0); player.RefreshColor(true);
                var missile = new GameObject("Dodge before firing", typeof(EnemyMissile)).GetComponent<EnemyMissile>();
                missile.transform.position = new Vector3(enemy.HitBounds.center.x, player.transform.position.y + .5f, 0); missile.SetTarget(player);
                var policy = new PlaytestBenchmark { level = 1, normalHitRate = 1, normalEfficiency = 1,
                    movementFraction = 1, readyDelay = .1f, reactionSeconds = .1f, dodgeProbability = 1 };
                policy.observed.normal.completed = 10;
                var profile = new PlaytestProfile { configurationHash = "fixture" }; profile.stages.Add(policy);
                var driver = new PlaytestDriver(player, grid, profile, true, 54321, "fixture");
                driver.Tick(1, 1);
                Require(!player.ShotActive, "Reaching a safe but empty lane does not waste a deliberate-hit shot");
                UnityEngine.Object.DestroyImmediate(missile.gameObject);
                driver.Tick(1, 1);
                Require(player.ShotActive, "The bot resumes firing after the threat clears");
            });
        }
        private static void CheckShield()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0); player.RefreshColor(true);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 1, 3);
                var shield = blue.GetComponent<EnemyAbilities>(); shield.Tick(shield.CooldownRemaining + .35f);
                Require(shield.ShieldActive && new PlaytestObservation(grid, player).At(red.transform.position.x) == 0 && shield.ShieldActive,
                    "Capacity query respects a shield without spending it");
                player.transform.position = new Vector3(red.transform.position.x, -4.6f, 0);
                Require(player.Fire(), "Shield fixture fires"); tongue.Tick(3, grid);
                Require(grid.Model.Count == 2 && !shield.ShieldActive, "Actual shield absorbs the shot");
            });
        }
        private static void CheckStageAttribution()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player); Lifecycle(session, "OnEnable");
                session.LevelCatalog = new LevelCatalog { levels = new[] { new LevelDefinition(), new LevelDefinition() } };
                string root = Path.Combine(Path.GetTempPath(), "bass-stage-check-" + Guid.NewGuid().ToString("N"));
                try
                {
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0); player.RefreshColor(true);
                    player.transform.position = new Vector3(red.transform.position.x, -4.6f, 0);
                    var data = new PlaytestRunData { id = "stage", startLevel = 1 };
                    using var recorder = new PlaytestRecorder(session, player, data, root);
                    Require(player.Fire(), "Stage fixture fires"); tongue.Tick(.6f, grid);
                    player.FinishWaveReturn();
                    Require(session.Progress.Level == 2 && data.Stage(1).normal.kills == 1 && data.Stage(1).fleetsCleared == 1 &&
                        data.Stage(1).normal.completed == 1 && data.Stage(1).normal.interrupted == 0 && data.Stage(2).normal.fired == 0,
                        "A fleet-clearing shot and shortened successful return stay with the launch stage");
                    recorder.Finish("manual_stop");
                }
                finally { Lifecycle(session, "OnDisable"); UnityEngine.Object.DestroyImmediate(session); if (Directory.Exists(root)) Directory.Delete(root, true); }
            });
        }
        private static void Reject(Action action)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new Exception("Playtest check failed: invalid calibration was accepted");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception("Playtest check failed: " + message); }
    }
}
