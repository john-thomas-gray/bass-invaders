using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class MagicPowerRuntimeChecks
    {
        private const string Key = "CandyCruisers.MagicPowerRuntimeChecks";
        private static int stage;
        private static double started, changed;
        private static float frozenPupils, frozenBeat;
        static MagicPowerRuntimeChecks() => EditorApplication.update += Poll;
        public static void RunDesktop() => Begin(false);
        public static void RunPhone() => Begin(true);
        private static void Begin(bool phone)
        {
            MetronomeCheckSettings.BeginRuntime(Key);
            SessionState.SetBool(Key + ".Override", SpawnOverride.Enabled);
            SpawnOverride.Enabled = false;
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type)) ((EditorWindow)existing).Close();
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility();
            window.position = new Rect(40, 40, phone ? 440 : 960, phone ? 900 : 640);
            window.Focus();
            SessionState.SetBool(Key + ".Phone", phone);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (started == 0) started = EditorApplication.timeSinceStartup;
                Check(EditorApplication.timeSinceStartup - started < 60, "Timed out at stage " + stage);
                bool phone = SessionState.GetBool(Key + ".Phone", false);
                Application.runInBackground = true;
                Application.targetFrameRate = phone ? 30 : 60;
                GameSession.ForceMobileMenuForTests = phone;
                GameplayFraming.ForceIphoneNotchOffsetForTests = phone;
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null) return;
                var grid = session.GetComponent<EnemyGrid>();
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
                var music = session.GetComponent<GameplayMusicPlayer>();
                var effect = Camera.main.GetComponent<MagicPowerPresentation>();
                if (stage < 3 && session.IsPaused) session.Resume();
                if (stage == 0)
                {
                    session.PresentationPreview = true;
                    session.LevelCatalog = null;
                    DeveloperOptions.PlayerInvincible = true;
                    if (session.State != GameSession.RunState.MainMenu) return;
                    session.StartRun();
                    changed = EditorApplication.timeSinceStartup;
                    stage = 1;
                }
                else if (stage == 1 && session.State == GameSession.RunState.Playing && grid.Model.Count >= 6 &&
                    EditorApplication.timeSinceStartup - changed > 2)
                {
                    MagicPowerChecks.Run();
                    PlayfieldFrameChecks.RunColorChecks();
                    var enemies = grid.GetComponentsInChildren<GridEnemy>();
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    foreach (var enemy in enemies)
                    {
                        enemy.GetComponent<EnemyAbilities>().Suspended = true;
                        Check(grid.SetColor(enemy.Id, EnemyColor.Red), "Prepare a surviving color");
                    }
                    var colors = EnemyPalette.Colors.Where(color => color != EnemyColor.Red).ToArray();
                    for (int i = 0; i < colors.Length; i++)
                        Check(grid.SetColor(enemies[i].Id, colors[i]), "Prepare each color-clear bar");
                    for (int i = 0; i < colors.Length; i++) grid.ClearMatchingChain(enemies[i].Id, colors[i]);
                    music.PreviewTime = 0;
                    changed = EditorApplication.timeSinceStartup;
                    stage = 2;
                }
                else if (stage == 2 && EditorApplication.timeSinceStartup - changed > .6)
                {
                    Check(player.ActiveMagicBars == 5 && Mathf.Abs(session.MagicTextGrowthScale - 1.30f) < .00001f,
                        $"Live gameplay peaks at 30 percent for five lit bars: bars={player.ActiveMagicBars}, charges={player.MagicCharges}, pulse={session.MagicTextGrowthScale}, state={session.State}, beat={music.BeatPosition}");
                    Check(effect != null && effect.PupilScale == 1.4f, "Live pupils finish their eased enlargement");
                    stage = 22;
                    session.StartCoroutine(CheckDevelopmentReadouts(session, player, phone));
                }
                else if (stage == 21 && EditorApplication.timeSinceStartup - changed > .2)
                {
                    session.Pause();
                    frozenPupils = effect.PupilScale;
                    frozenBeat = music.BeatPosition;
                    changed = EditorApplication.timeSinceStartup;
                    stage = 3;
                }
                else if (stage == 3 && EditorApplication.timeSinceStartup - changed > .4)
                {
                    Check(effect.PupilScale == frozenPupils && music.BeatPosition == frozenBeat,
                        "Pause freezes both the pupil animation and scene beat");
                    session.Resume();
                    player.ResetForRun();
                    changed = EditorApplication.timeSinceStartup;
                    stage = 4;
                }
                else if (stage == 4 && EditorApplication.timeSinceStartup - changed > .4)
                {
                    Check(session.MagicTextGrowthScale == 1 && effect.PupilScale == 1, "Restart restores normal scenery and pupils");
                    Debug.Log("Magic power runtime checks passed: " + (phone ? "phone layout with notch at 30 frames per second" :
                        "desktop at 60 frames per second") + ", live actor/life/progress growth, alternating inner border, pupil animation, pause, reset and screenshots.");
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static IEnumerator CheckDevelopmentReadouts(GameSession session, PlayerMovement player, bool phone)
        {
            float previousTimeScale = Time.timeScale;
            int charges = player.MagicCharges;
            Texture2D powered = null, normal = null;
            try
            {
                MetronomeSettings.VisualEnabled = true;
                MetronomeSettings.RoundClockEnabled = true;
                Time.timeScale = 0;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                var rectangles = session.DeveloperOverlayRects;
                float scale = GameSession.GuiScaleFor(phone, Screen.width, Screen.height);
                var field = session.DevelopmentPlayfieldScreenRect();
                var blocked = new Rect(field.x / scale, (Screen.height - field.yMax) / scale, field.width / scale, field.height / scale);
                Check(rectangles.Clock.width > 0 && rectangles.Metronome.width > 0 &&
                    !rectangles.Clock.Overlaps(blocked) && !rectangles.Metronome.Overlaps(blocked) &&
                    !rectangles.Clock.Overlaps(rectangles.Metronome), "Development readouts avoid all playfield artwork");
                powered = ScreenCapture.CaptureScreenshotAsTexture();
                Directory.CreateDirectory("TestResults");
                System.IO.File.WriteAllBytes("TestResults/magic-power-" + (phone ? "phone" : "desktop") + ".png", powered.EncodeToPNG());
                typeof(PlayerMovement).GetProperty(nameof(PlayerMovement.MagicCharges)).SetValue(player, 0);
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                normal = ScreenCapture.CaptureScreenshotAsTexture();
                CheckHudGrowth(powered, normal, session, scale);
                var first = powered.GetPixels32();
                var second = normal.GetPixels32();
                foreach (var rect in new[] { rectangles.Clock, rectangles.Metronome })
                {
                    int changedPixels = 0;
                    for (int y = Mathf.Max(0, Mathf.CeilToInt(Screen.height - rect.yMax * scale));
                        y < Mathf.Min(Screen.height, Mathf.FloorToInt(Screen.height - rect.yMin * scale)); y++)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(rect.xMin * scale));
                        x < Mathf.Min(Screen.width, Mathf.FloorToInt(rect.xMax * scale)); x++)
                        if (!first[y * Screen.width + x].Equals(second[y * Screen.width + x])) changedPixels++;
                    Check(changedPixels == 0, "Development clock and metronome render identically with and without the magic pulse: " + changedPixels);
                }
                typeof(PlayerMovement).GetProperty(nameof(PlayerMovement.MagicCharges)).SetValue(player, charges);
                var music = session.GetComponent<GameplayMusicPlayer>();
                float downbeat = music.CurrentDownbeatOffsetBeats;
                var phases = new[] { .5f, downbeat + 1, downbeat };
                var names = new[] { "-player-peak.png", "-corners-peak.png", "-inner-edge-peak.png" };
                for (int i = 0; i < phases.Length; i++)
                {
                    music.PreviewTime = music.SecondsAtBeat(phases[i]);
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                    var phase = ScreenCapture.CaptureScreenshotAsTexture();
                    System.IO.File.WriteAllBytes("TestResults/magic-power-" + (phone ? "phone" : "desktop") +
                        names[i], phase.EncodeToPNG());
                    UnityEngine.Object.Destroy(phase);
                }
                changed = EditorApplication.timeSinceStartup;
                stage = 21;
            }
            finally
            {
                typeof(PlayerMovement).GetProperty(nameof(PlayerMovement.MagicCharges)).SetValue(player, charges);
                session.GetComponent<GameplayMusicPlayer>().PreviewTime = 0;
                Time.timeScale = previousTimeScale;
                if (powered != null) UnityEngine.Object.Destroy(powered);
                if (normal != null) UnityEngine.Object.Destroy(normal);
            }
        }

        private static void CheckHudGrowth(Texture2D powered, Texture2D normal, GameSession session, float scale)
        {
            var camera = Camera.main;
            var left = camera.WorldToScreenPoint(new Vector3(-3, 5.3f));
            var right = camera.WorldToScreenPoint(new Vector3(3, 5.3f));
            float x = left.x / scale + 10, y = (Screen.height - left.y) / scale;
            int viewHeight = Mathf.RoundToInt(camera.pixelHeight / scale);
            var icon = PlayerLifeIcons.IconRect(PlayerLifeIcons.RowRect(x, y, viewHeight), 0);
            var lifeArea = new Rect(icon.center - icon.size * .9f, icon.size * 1.8f);
            float progressY = y + PlayerLifeIcons.ProgressOffset(viewHeight);
            var barArea = new Rect(x - 4, progressY - 8, (right.x - left.x) / scale - 12,
                GameSession.LevelProgressBaseHeight + 16);
            var areas = new[] { lifeArea, barArea };
            for (int i = 0; i < areas.Length; i++)
            {
                var enlarged = InkBounds(powered, areas[i], scale, i == 1);
                var resting = InkBounds(normal, areas[i], scale, i == 1);
                float ratio = enlarged.height / Mathf.Max(1, resting.height);
                Check(ratio > 1.22f && ratio < 1.39f,
                    $"Actual life/progress artwork receives the 30 percent bonus: ratio={ratio}, enlarged={enlarged}, normal={resting}");
                if (i == 1)
                    Check(Mathf.Abs(enlarged.x - resting.x) <= 1 && Mathf.Abs(enlarged.width - resting.width) <= 1 &&
                        Mathf.Abs(enlarged.width - ((right.x - left.x) - 20 * scale)) <= 2,
                        "The full-width progress bar keeps identical horizontal edges throughout its vertical magic pulse");
            }
        }

        private static Rect InkBounds(Texture2D texture, Rect area, float scale, bool progressBar = false)
        {
            var pixels = texture.GetPixels32();
            int minX = texture.width, maxX = -1, minY = texture.height, maxY = -1;
            for (int y = Mathf.Max(0, Mathf.CeilToInt(texture.height - area.yMax * scale));
                y < Mathf.Min(texture.height, Mathf.FloorToInt(texture.height - area.yMin * scale)); y++)
            {
                int run = 0, longest = 0, end = -1;
                for (int x = Mathf.Max(0, Mathf.CeilToInt(area.xMin * scale));
                    x < Mathf.Min(texture.width, Mathf.FloorToInt(area.xMax * scale)); x++)
                {
                    var pixel = pixels[y * texture.width + x];
                    if (pixel.r + pixel.g + pixel.b < 60) { run = 0; continue; }
                    run++;
                    if (run > longest) { longest = run; end = x; }
                    if (progressBar) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                // A continuous bar spans most of this row. Ignore stars and enemy pixels below it.
                if (progressBar && longest > area.width * scale * .6f)
                {
                    minX = Mathf.Min(minX, end - longest + 1); maxX = Mathf.Max(maxX, end);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            }
            Check(maxX >= minX && maxY >= minY, "The growth comparison contains visible artwork");
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        private static void Finish(int code)
        {
            MetronomeCheckSettings.EndRuntime(Key);
            SpawnOverride.Enabled = SessionState.GetBool(Key + ".Override", false);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(code);
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Magic power runtime check failed: " + message); }
    }
}
