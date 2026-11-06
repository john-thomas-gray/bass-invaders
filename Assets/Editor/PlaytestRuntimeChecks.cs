using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class PlaytestRuntimeChecks
    {
        private const string Key = "CandyCruisers.PlaytestRuntimeChecks";
        private static double began, pauseAt;
        private static bool paused, pauseVerified;
        private static float recordedAtPause;
        private static double readyAt;
        static PlaytestRuntimeChecks() => EditorApplication.update += Poll;
        public static void RunDesktop() => Run(960, 640, "desktop");
        public static void RunPhone() => Run(440, 900, "phone-layout");
        private static void Run(int width, int height, string layout)
        {
            SessionState.SetString(Key + ".Previous", PlayerPrefs.GetString(PlaytestOptions.Key, ""));
            SessionState.SetBool(Key + ".HadPrevious", PlayerPrefs.HasKey(PlaytestOptions.Key));
            SessionState.SetString(Key + ".Leaderboard", string.Join(",", LocalLeaderboard.Entries().Select(entry => entry.Score + ":" + entry.Level)));
            SessionState.SetInt(Key + ".Highest", GameSession.HighestReachedLevel);
            SessionState.SetBool(Key + ".SpawnOverride", SpawnOverride.Enabled);
            SpawnOverride.Enabled = false;
            string batch = "verification-" + layout + "-" + Guid.NewGuid().ToString("N");
            SessionState.SetString(Key + ".Batch", batch);
            var source = new PlaytestRunData { id = "synthetic-verification-only", controller = "human", completed = true,
                configurationHash = Hash128.Compute(PlaytestHarness.ConfigurationSnapshot(LevelCatalog.Parse(File.ReadAllText(LevelCatalog.FilePath)))).ToString() };
            var stage = source.Stage(1);
            stage.normal.fired = stage.normal.completed = 10; stage.normal.hits = 8;
            stage.normal.opportunityShots = 10; stage.normal.efficiencyTotal = 8;
            stage.magic.fired = stage.magic.completed = 10; stage.magic.hits = 8;
            stage.magic.opportunityShots = 10; stage.magic.efficiencyTotal = 8;
            stage.readySeconds = 5; stage.movingSeconds = 1; stage.movementDistance = 5;
            Directory.CreateDirectory("TestResults");
            string profilePath = Path.GetFullPath("TestResults/playtest-synthetic-profile.json");
            File.WriteAllText(profilePath, JsonUtility.ToJson(PlaytestProfile.Calibrate("synthetic verification fixture", new[] { source }), true));
            new PlaytestOptions { mode = "bot", participant = "automated-verification", runs = 2, seed = 54321,
                autoStart = true, limitSeconds = 12, batchId = batch, profileFile = profilePath }.Save();
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type)) ((EditorWindow)existing).Close();
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility(); window.position = new Rect(40, 40, width, height); window.Focus();
            SessionState.SetBool(Key, true); SessionState.SetString(Key + ".Layout", layout);
            EditorApplication.EnterPlaymode();
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (began == 0) began = EditorApplication.timeSinceStartup;
                if (readyAt > 0) { if (EditorApplication.timeSinceStartup - readyAt > .7) Finish(0); return; }
                Require(EditorApplication.timeSinceStartup - began < 100, "Autonomous batch timed out");
                var harness = UnityEngine.Object.FindFirstObjectByType<PlaytestHarness>();
                if (harness == null) return;
                var session = harness.GetComponent<GameSession>();
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
                if (harness.Recorder != null && harness.Recorder.Data.seconds > 2 && !paused)
                {
                    harness.Options.showPanel = true; session.Pause();
                    recordedAtPause = harness.Recorder.Data.seconds; pauseAt = EditorApplication.timeSinceStartup; paused = true;
                }
                if (paused && !pauseVerified && EditorApplication.timeSinceStartup - pauseAt > .3)
                {
                    Require(session.IsPaused && harness.Recorder.Data.seconds == recordedAtPause, "Paused recording does not accumulate exposure");
                    Require(player.AutonomousControl && session.IsAutomatedPlaytest, "Autonomous input and persistence isolation are active");
                    Require(harness.Driver.ConfigurationMatches && harness.Driver.MeasuredStage, "Loaded calibrated profile matches the current stage and configuration");
                    harness.Options.showPanel = false; typeof(GameSession).GetProperty("PlaytestPanelOpen").SetValue(session, false); session.Resume(); pauseVerified = true;
                }
                if (harness.Options.nextRun < 2) return;
                Require(pauseVerified && !harness.Options.autoStart && harness.Recorder == null, "Batch stops after the requested number of runs");
                string batch = SessionState.GetString(Key + ".Batch", "");
                var results = Directory.GetFiles(Path.Combine(PlaytestOptions.Root, "Runs"), "summary.json", SearchOption.AllDirectories)
                    .Select(path => JsonUtility.FromJson<PlaytestRunData>(File.ReadAllText(path))).Where(run => run.batchId == batch).ToArray();
                Require(results.Length == 2 && results[0].seed != results[1].seed && results.All(run => run.completed && run.outcome == "time_limit"), "Distinct seeds produce two complete time-limited records");
                Require(results.Sum(run => run.stages.Sum(stage => stage.Shots)) > 0 &&
                    results.Sum(run => run.stages.Sum(stage => stage.normal.kills + stage.magic.kills)) > 0, "Bot moves, fires and kills through actual runtime combat");
                Require(File.Exists(Path.Combine(PlaytestOptions.Root, "Batches", batch + ".csv")), "Per-stage comparison report saved");
                Require(SessionState.GetInt(Key + ".Highest", 0) == GameSession.HighestReachedLevel &&
                    SessionState.GetString(Key + ".Leaderboard", "") == string.Join(",", LocalLeaderboard.Entries().Select(entry => entry.Score + ":" + entry.Level)),
                    "Automated runs leave human unlocks and leaderboard untouched");
                Directory.CreateDirectory("TestResults");
                harness.Options.showPanel = true;
                ScreenCapture.CaptureScreenshot("TestResults/playtesting-" + SessionState.GetString(Key + ".Layout", "") + ".png");
                Debug.Log("Playtest runtime checks passed: " + SessionState.GetString(Key + ".Layout", "") + ", real autonomous combat, two seeds, pause, recording, reports and persistence isolation. Batch " + batch);
                readyAt = EditorApplication.timeSinceStartup;
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".HadPrevious", false)) PlayerPrefs.SetString(PlaytestOptions.Key, SessionState.GetString(Key + ".Previous", ""));
            else PlayerPrefs.DeleteKey(PlaytestOptions.Key);
            PlayerPrefs.Save(); SpawnOverride.Enabled = SessionState.GetBool(Key + ".SpawnOverride", false);
            EditorApplication.Exit(code);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception("Playtest runtime check failed: " + message); }
    }
}
