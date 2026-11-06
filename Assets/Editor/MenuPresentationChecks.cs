using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class MenuPresentationChecks
    {
        private const string Pending = "CandyCruisers.MenuPresentationChecks";
        private static int stage;
        private static double since;
        private static EditorWindow window;
        static MenuPresentationChecks() => EditorApplication.update += Poll;

        public static void Run()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            window = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            window.Show();
            window.Focus();
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunPortrait()
        {
            RunSized(440, 820);
        }

        public static void RunLandscape()
        {
            RunSized(1100, 740);
        }

        public static void RunPhone()
        {
            RunSized(440, 820, true);
        }

        private static void RunSized(float width, float height, bool phone = false)
        {
            SessionState.SetBool(Pending + ".Phone", phone);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type))
                ((EditorWindow)existing).Close();
            window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility();
            window.position = new Rect(40, 40, width, height);
            Run();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            try
            {
                GameSession.ForceMobileMenuForTests = SessionState.GetBool(Pending + ".Phone", false);
                GameplayFraming.ForceIphoneNotchOffsetForTests = GameSession.ForceMobileMenuForTests;
                Application.runInBackground = true;
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
                if (session == null || player == null) return;
                if (since == 0) since = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - since < .4) return;
                if (stage == 0)
                {
                    if (session.MenuTitleIntroElapsed < GameSession.MenuWordIntroDuration) return;
                    Require(session.State == GameSession.RunState.MainMenu, "Menu starts empty");
                    var art = CharacterVisuals.Ensure(player.gameObject);
                    Require(player.GetComponent<PlayerSlimeVisual>() != null && art.Body.sprite.name.Contains("slime"),
                        "Player uses the slime placeholder");
                    ScreenCapture.CaptureScreenshot("TestResults/slime-menu.png");
                    CaptureSilhouettes();
                    stage++;
                }
                else if (stage == 1)
                {
                    session.BeginMenuArrival();
                    Require(session.BlocksGameplayInput && !player.Fire(), "Arrival cannot fire a shot");
                    stage++;
                }
                else if (stage == 2)
                {
                    Require(session.MenuArriving && session.State == GameSession.RunState.MainMenu,
                        "Character floats down before gameplay");
                    ScreenCapture.CaptureScreenshot("TestResults/slime-arrival.png");
                    stage++;
                }
                else if (stage == 3)
                {
                    if (session.State != GameSession.RunState.Playing) return;
                    Require(!session.MenuArriving && player.GetComponent<PlayerSlimeVisual>() != null,
                        "Arrival hands over to gameplay with slime visuals intact");
                    ScreenCapture.CaptureScreenshot("TestResults/slime-playing.png");
                    stage++;
                }
                else
                {
                    Debug.Log("Menu presentation checks passed: slime artwork, locked arrival and gameplay handover.");
                    GameSession.ForceMobileMenuForTests = false;
                    GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                    SessionState.SetBool(Pending, false);
                    EditorApplication.Exit(0);
                }
                since = EditorApplication.timeSinceStartup;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                GameSession.ForceMobileMenuForTests = false;
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                SessionState.SetBool(Pending, false);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Menu presentation check failed: " + message); }

        private static void CaptureSilhouettes()
        {
            var mask = MenuBacklight.SilhouetteTexture;
            if (mask == null) return;
            var previous = RenderTexture.active;
            RenderTexture.active = mask;
            var image = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes("TestResults/menu-silhouettes.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = previous;
        }
    }
}
