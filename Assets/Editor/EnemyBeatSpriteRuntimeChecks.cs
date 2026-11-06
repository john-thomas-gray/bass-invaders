using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class EnemyBeatSpriteRuntimeChecks
    {
        private const string Key = "CandyCruisers.EnemyBeatSpriteRuntimeChecks";
        private static readonly Dictionary<GridEnemy, Sprite> appearances = new Dictionary<GridEnemy, Sprite>();
        private static readonly Dictionary<GridEnemy, Bounds> hitBounds = new Dictionary<GridEnemy, Bounds>();
        private static double started, changed;
        private static int stage, lastBeat, transitions;
        private static float pausedBeat;
        static EnemyBeatSpriteRuntimeChecks() => EditorApplication.update += Poll;
        public static void RunDesktop() => Begin(false);
        public static void RunPhone() => Begin(true);
        private static void Begin(bool phone)
        {
            MetronomeCheckSettings.BeginRuntime(Key, voice: true);
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
                Check(EditorApplication.timeSinceStartup - started < 45, "Live playback timed out at stage " + stage);
                bool phone = SessionState.GetBool(Key + ".Phone", false);
                Application.runInBackground = true;
                Application.targetFrameRate = phone ? 30 : 60;
                GameSession.ForceMobileMenuForTests = phone;
                GameplayFraming.ForceIphoneNotchOffsetForTests = phone;
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null) return;
                var grid = session.GetComponent<EnemyGrid>();
                var music = session.GetComponent<GameplayMusicPlayer>();
                if (stage < 3 && session.IsPaused) session.Resume();
                if (stage == 0)
                {
                    session.PresentationPreview = true;
                    session.LevelCatalog = null;
                    DeveloperOptions.PlayerInvincible = true;
                    if (session.State != GameSession.RunState.MainMenu) return;
                    session.StartRun(); stage = 1;
                }
                else if (stage == 1 && session.State == GameSession.RunState.Playing && grid.Model.Count > 0)
                {
                    if (!music.Source.isPlaying) return;
                    if (grid.GetComponentsInChildren<EnemyPresentation>().Any(effect => effect.IsPhasing)) return;
                    var enemies = grid.GetComponentsInChildren<GridEnemy>().OrderBy(enemy => enemy.Row).ThenBy(enemy => enemy.Column).ToArray();
                    foreach (var enemy in enemies.Take(3)) grid.SetColor(enemy.Id, EnemyColor.Red);
                    grid.SetColor(enemies.Last().Id, EnemyColor.Green);
                    grid.RefreshSpecials();
                    foreach (var enemy in enemies)
                    {
                        enemy.GetComponent<EnemyAbilities>().Suspended = true;
                        appearances.Add(enemy, enemy.Visuals.Body.sprite);
                        hitBounds.Add(enemy, enemy.HitBounds);
                    }
                    Check(appearances.Values.Any(sprite => sprite.name.StartsWith("Regular")) &&
                        appearances.Values.Any(sprite => sprite.name.StartsWith("Special")), "Live fixture has both designs");
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    Check(music.Source.isPlaying, "Actual song playback drives the animation");
                    lastBeat = Mathf.FloorToInt(music.BeatPosition);
                    stage = 2;
                }
                else if (stage == 2)
                {
                    float beat = music.BeatPosition;
                    // Poll follows the previous rendered frame, so avoid sampling the audio boundary itself.
                    if (Mathf.Repeat(beat, 1) < .2f || Mathf.Repeat(beat, 1) > .8f) return;
                    CheckFrames(beat);
                    int index = Mathf.FloorToInt(beat);
                    if (index == lastBeat) return;
                    lastBeat = index;
                    Directory.CreateDirectory("TestResults");
                    ScreenCapture.CaptureScreenshot("TestResults/enemy-beat-live-" + (phone ? "phone-" : "desktop-") + (index & 1) + ".png");
                    if (++transitions < 4) return;
                    session.Pause();
                    pausedBeat = music.BeatPosition;
                    changed = EditorApplication.timeSinceStartup;
                    stage = 3;
                }
                else if (stage == 3 && EditorApplication.timeSinceStartup - changed > .7)
                {
                    Check(session.IsPaused && Mathf.Abs(music.BeatPosition - pausedBeat) < .01f, "Pause freezes the song clock");
                    CheckFrames(pausedBeat);
                    session.Resume();
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    foreach (var enemy in appearances.Keys) enemy.GetComponent<EnemyAbilities>().Suspended = true;
                    changed = EditorApplication.timeSinceStartup;
                    stage = 4;
                }
                else if (stage == 4 && EditorApplication.timeSinceStartup - changed > .7)
                {
                    if (Mathf.Repeat(music.BeatPosition, 1) < .2f || Mathf.Repeat(music.BeatPosition, 1) > .8f) return;
                    Check(music.Source.isPlaying && music.BeatPosition > pausedBeat, "Resume continues the song clock");
                    CheckFrames(music.BeatPosition);
                    Debug.Log("Enemy beat sprite runtime checks passed: " + (phone ? "phone layout at 30 frames per second" : "desktop at 60 frames per second") +
                        ", both designs, four live beat transitions, stable hitboxes, pause and resume.");
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void CheckFrames(float beat)
        {
            foreach (var pair in appearances)
                Check(pair.Key.Visuals.Body.sprite == EnemyBeatSprites.FrameAt(pair.Value, beat) &&
                    pair.Key.HitBounds == hitBounds[pair.Key],
                    $"Both designs follow live music without moving their hitboxes: stage={stage}, beat={beat}, " +
                    $"enemy={pair.Key.name}, expected={EnemyBeatSprites.FrameAt(pair.Value, beat)?.name}, actual={pair.Key.Visuals.Body.sprite?.name}, " +
                    $"bounds={pair.Key.HitBounds}, original={hitBounds[pair.Key]}");
        }
        private static void Finish(int code)
        {
            MetronomeCheckSettings.EndRuntime(Key);
            SpawnOverride.Enabled = SessionState.GetBool(Key + ".Override", false);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(code);
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Enemy beat sprite runtime check failed: " + message); }
    }
}
