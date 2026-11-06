using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class PlaytestAccessChecks
    {
        private const string Key = "CandyCruisers.PlaytestAccessChecks";
        private static double began, phaseAt;
        private static float pausedSeconds;
        static PlaytestAccessChecks()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception))
                    SessionState.SetString(Key + ".Error", message);
            };
        }
        public static void RunDesktop() => Run(960, 640, "desktop");
        public static void RunPhone() => Run(440, 900, "phone-layout");
        private static void Run(int width, int height, string layout)
        {
            SessionState.SetString(Key + ".Previous", PlayerPrefs.GetString(PlaytestOptions.Key, ""));
            SessionState.SetBool(Key + ".HadPrevious", PlayerPrefs.HasKey(PlaytestOptions.Key));
            new PlaytestOptions { mode = "off" }.Save();
            SessionState.SetString(Key + ".Error", "");
            SessionState.SetString(Key + ".Run", "");
            SessionState.SetInt(Key + ".Phase", 0);
            SessionState.SetString(Key + ".Layout", layout);
            SessionState.SetBool(Key, true);
            // Instantiating the real window catches Unity's reserved lifecycle-name diagnostics.
            PlaytestWindow.Open();
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(type);
            window.ShowUtility(); window.position = new Rect(40, 40, width, height); window.Focus();
            EditorApplication.EnterPlaymode();
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                Application.runInBackground = true;
                if (began == 0) began = EditorApplication.timeSinceStartup;
                Require(EditorApplication.timeSinceStartup - began < 40, "Access checks timed out");
                Require(string.IsNullOrEmpty(SessionState.GetString(Key + ".Error", "")), SessionState.GetString(Key + ".Error", ""));
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null || session.OpeningWarningActive) return;
                var harness = session.GetComponent<PlaytestHarness>();
                Require(harness != null, "The runtime panel attaches in ordinary play with recording off");
                int phase = SessionState.GetInt(Key + ".Phase", 0);
                if (phase < 4 && session.IsPaused) session.Resume();
                if (phase == 0)
                {
                    Require(session.State == GameSession.RunState.MainMenu && harness.MenuAvailable, "Main-menu access is available with playtesting disabled");
                    harness.enabled = false;
                    typeof(PlaytestHarness).GetProperty("Options").SetValue(harness, null);
                    foreach (string field in new[] { "session", "player" })
                        typeof(PlaytestHarness).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(harness, null);
                    harness.enabled = true;
                    Require(harness.Options != null && harness.MenuAvailable && session.GetComponents<PlaytestHarness>().Length == 1,
                        "Re-enabling reconnects lost runtime references without waiting for Start or adding a duplicate");
                    Next(1);
                }
                else if (phase == 1 && Elapsed > .3)
                {
                    Capture("main-menu"); Next(11);
                }
                else if (phase == 11 && Elapsed > .3)
                {
                    VerifyAccessButtonScreenshot();
                    var button = PlaytestHarness.AccessButtonScreenRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, Application.isMobilePlatform);
                    bool consumed = (bool)typeof(GameSession).GetMethod("ActivateMenuTouch", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(session, new object[] { button.center });
                    Require(consumed && session.State == GameSession.RunState.MainMenu && harness.Options.showPanel && session.PlaytestPanelOpen,
                        "Clicking the Playtesting button opens its panel before the main menu's click-anywhere handler");
                    Capture("panel"); Next(2);
                }
                else if (phase == 2 && Elapsed > .3)
                {
                    harness.Options.participant = "access-check-" + SessionState.GetString(Key + ".Layout", "");
                    harness.RecordHumanRun();
                    Require(harness.Recorder != null && harness.Recorder.Data.controller == "human" && !session.PlaytestPanelOpen,
                        "The record action starts a real human recording and closes the panel");
                    SessionState.SetString(Key + ".Run", harness.Recorder.DirectoryPath); Next(3);
                }
                else if (phase == 3 && session.State == GameSession.RunState.Playing && harness.Recorder.Data.seconds > .5f && !session.BlocksGameplayInput)
                {
                    var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
                    Require(player.Fire(), "Recording accepts ordinary player firing");
                    session.Pause();
                    Require(harness.MenuAvailable, "The button is available in the pause menu");
                    var button = PlaytestHarness.AccessButtonScreenRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, Application.isMobilePlatform);
                    Require(harness.TryOpenPanelAtScreenPoint(button.center) && session.IsPaused, "The pause menu opens recording controls without resuming");
                    pausedSeconds = harness.Recorder.Data.seconds;
                    Capture("pause-recording"); Next(4);
                }
                else if (phase == 4 && Elapsed > .3)
                {
                    Require(session.IsPaused && harness.Recorder.Data.seconds == pausedSeconds, "The recording stays paused while the controls are open");
                    harness.Finish();
                    string path = Path.Combine(SessionState.GetString(Key + ".Run", ""), "summary.json");
                    var summary = JsonUtility.FromJson<PlaytestRunData>(File.ReadAllText(path));
                    Require(summary.completed && summary.controller == "human" && summary.stages.Sum(stage => stage.Shots) == 1,
                        "Stop and save produces the human summary with the accepted shot");
                    Debug.Log("Playtest access checks passed: " + SessionState.GetString(Key + ".Layout", "") +
                        ", Editor window lifecycle, main-menu button, input routing, reconnect, human recording and pause/save controls.");
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static double Elapsed => EditorApplication.timeSinceStartup - phaseAt;
        private static void Next(int phase) { SessionState.SetInt(Key + ".Phase", phase); phaseAt = EditorApplication.timeSinceStartup; }
        private static void Capture(string name)
        {
            Directory.CreateDirectory("TestResults");
            ScreenCapture.CaptureScreenshot("TestResults/playtest-access-" + SessionState.GetString(Key + ".Layout", "") + "-" + name + ".png");
        }
        private static void VerifyAccessButtonScreenshot()
        {
            string path = "TestResults/playtest-access-" + SessionState.GetString(Key + ".Layout", "") + "-main-menu.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(texture.LoadImage(File.ReadAllBytes(path)), "The main-menu screenshot is readable");
                var button = PlaytestHarness.AccessButtonScreenRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, Application.isMobilePlatform);
                float sx = texture.width / (float)Screen.width, sy = texture.height / (float)Screen.height;
                int bright = 0;
                for (int y = Mathf.CeilToInt((button.yMin + 6) * sy); y < (button.yMax - 6) * sy; y++)
                for (int x = Mathf.CeilToInt((button.xMin + 6) * sx); x < (button.xMax - 6) * sx; x++)
                {
                    var color = texture.GetPixel(x, y);
                    if (color.r > .65f && color.g > .65f && color.b > .65f) bright++;
                }
                Require(bright > 20, "The Playtesting label is actually visible over the main-menu background");
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static void Finish(int code)
        {
            SessionState.SetBool(Key, false);
            var harness = UnityEngine.Object.FindFirstObjectByType<PlaytestHarness>();
            harness?.Finish();
            string run = SessionState.GetString(Key + ".Run", "");
            if (!string.IsNullOrEmpty(run) && Directory.Exists(run)) Directory.Delete(run, true);
            if (SessionState.GetBool(Key + ".HadPrevious", false)) PlayerPrefs.SetString(PlaytestOptions.Key, SessionState.GetString(Key + ".Previous", ""));
            else PlayerPrefs.DeleteKey(PlaytestOptions.Key);
            PlayerPrefs.Save(); EditorApplication.Exit(code);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception("Playtest access check failed: " + message); }
    }
}
