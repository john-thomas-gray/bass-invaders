using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class SongSelectionRuntimeChecks
    {
        private const string Pending = "CandyCruisers.SongSelectionRuntimeChecks";
        private static readonly MethodInfo Touch = typeof(GameSession).GetMethod("ActivateMenuTouch", BindingFlags.Instance | BindingFlags.NonPublic);
        private static int stage;
        private static double began, changed;
        private static string only;
        static SongSelectionRuntimeChecks() => EditorApplication.update += Poll;
        public static void RunDesktop() => RunSized(960, 640, false);
        public static void RunPhone() => RunSized(440, 900, true);
        public static void BuildPhoneSimulator()
        {
            var previous = PlayerSettings.iOS.sdkVersion;
            var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                PhoneBuildCommand.BuildIos();
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previous;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
            }
        }
        private static void RunSized(int width, int height, bool phone)
        {
            MetronomeCheckSettings.BeginRuntime(Pending);
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type)) ((EditorWindow)existing).Close();
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility(); window.position = new Rect(40, 40, width, height); window.Focus();
            type.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, 0);
            SessionState.SetBool(Pending + ".Phone", phone);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            try
            {
                Application.runInBackground = true;
                if (began == 0) began = EditorApplication.timeSinceStartup;
                Check(EditorApplication.timeSinceStartup - began < 60, "Runtime timed out at stage " + stage);
                bool phone = SessionState.GetBool(Pending + ".Phone", false);
                GameSession.ForceMobileMenuForTests = phone;
                GameplayFraming.ForceIphoneNotchOffsetForTests = phone;
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null) return;
                if (stage <= 5 && session.IsPaused) session.Resume();
                var music = session.GetComponent<GameplayMusicPlayer>();
                var screen = new Vector2(Screen.width, Screen.height);
                float scale = GameSession.GuiScaleFor(phone, screen.x, screen.y);
                var layout = new SongSelectionMenu.Layout(screen, Screen.safeArea, scale, phone);
                string prefix = "TestResults/song-selection/" + (phone ? "phone-" : "desktop-");
                if (stage == 0)
                {
                    if (session.OpeningWarningActive) session.HandleMenuKey(KeyCode.Space);
                    if (!session.SongSelectionMenuAvailable || !music.Source.isPlaying) return;
                    Check(SongSelectionSettings.SelectedCount(music.SoundtrackNames) == music.TrackCount, "Fresh menu has all songs selected");
                    Directory.CreateDirectory("TestResults/song-selection");
                    ScreenCapture.CaptureScreenshot(prefix + "main.png");
                    Next(1);
                }
                else if (stage == 1 && Ready())
                {
                    Tap(session, SongSelectionMenu.ButtonRect(screen, Screen.safeArea, scale, phone), screen, scale);
                    Check(session.SongSelection.IsOpen && session.State == GameSession.RunState.MainMenu && !session.MenuArriving,
                        "Touch opens song choices without starting gameplay");
                    ScreenCapture.CaptureScreenshot(prefix + "all.png");
                    Next(2);
                }
                else if (stage == 2 && Ready())
                {
                    Tap(session, layout.Row(0), screen, scale);
                    Check(!SongSelectionSettings.IsSelected(music.SoundtrackNames[0]), "Touch unchecks a song");
                    Tap(session, layout.Next, screen, scale);
                    Check(session.SongSelection.FocusedSong == layout.Rows, "Touch opens the second page");
                    ScreenCapture.CaptureScreenshot(prefix + "page2.png");
                    Next(3);
                }
                else if (stage == 3 && Ready())
                {
                    only = music.SoundtrackNames[music.TrackCount - 1];
                    foreach (string name in music.SoundtrackNames) music.SetSongSelected(name, name == only);
                    while (session.SongSelection.FocusedSong < music.TrackCount - 1) session.HandleMenuKey(KeyCode.DownArrow);
                    Tap(session, layout.Row((music.TrackCount - 1) % layout.Rows), screen, scale);
                    Check(SongSelectionSettings.SelectedCount(music.SoundtrackNames) == 1 && SongSelectionSettings.IsSelected(only),
                        "Touch cannot uncheck the final song on the final page");
                    Check(music.Source.clip != null && music.Source.clip.name == only, "Changing selection replaces an excluded menu song");
                    ScreenCapture.CaptureScreenshot(prefix + "last.png");
                    Next(4);
                }
                else if (stage == 4 && Ready())
                {
                    Tap(session, layout.Back, screen, scale);
                    session.StartRun();
                    Next(5);
                }
                else if (stage == 5 && session.State == GameSession.RunState.Playing && !session.IsPaused)
                {
                    Check(music.Source.isPlaying && music.Source.clip.name == only, "Gameplay honors the one selected song");
                    session.Pause();
                    Tap(session, SongSelectionMenu.ButtonRect(screen, Screen.safeArea, scale, phone), screen, scale);
                    Check(session.SongSelection.IsOpen && session.IsPaused, "Pause opens the saved song menu");
                    Tap(session, layout.Row(0), screen, scale);
                    Check(SongSelectionSettings.SelectedCount(music.SoundtrackNames) == 2 && session.IsPaused && !music.Source.isPlaying,
                        "Checking another song preserves pause");
                    ScreenCapture.CaptureScreenshot(prefix + "pause.png");
                    Next(6);
                }
                else if (stage == 6 && Ready())
                {
                    Tap(session, layout.Back, screen, scale);
                    Check(!session.SongSelection.IsOpen && session.IsPaused, "Back returns to pause");
                    session.Resume();
                    Next(7);
                }
                else if (stage == 7 && Ready() && music.Source.isPlaying)
                {
                    Check(SongSelectionSettings.IsSelected(music.Source.clip.name), "Resume plays a checked song");
                    Debug.Log("Song selection runtime checks passed: " + (phone ? "phone" : "desktop") +
                        ", real playback, touch navigation, saved checkboxes, last-song guard, filtered carryover and pause/resume.");
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static bool Ready() => EditorApplication.timeSinceStartup - changed > .4;
        private static void Next(int next) { stage = next; changed = EditorApplication.timeSinceStartup; }
        private static void Tap(GameSession session, Rect rect, Vector2 screen, float scale) => Touch.Invoke(session,
            new object[] { new Vector2(rect.center.x * scale, screen.y - rect.center.y * scale) });
        private static void Finish(int code)
        {
            MetronomeCheckSettings.EndRuntime(Pending);
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(code);
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Song selection runtime check failed: " + message); }
    }
}
