using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SpecialGreenTempoChecks
    {
        private const string PendingKey = "CandyCruisers.SpecialGreenTempoChecks";

        public static void RunInPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void RestoreRuntimeCheck()
        {
            if (SessionState.GetBool(PendingKey, false))
                EditorApplication.playModeStateChanged += OnPlayMode;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            SessionState.SetBool(PendingKey, false);
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            using var songs = new SongSelectionCheckSettings();
            using var metronomes = new MetronomeCheckSettings();
            float originalTimeScale = Time.timeScale;
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player);
                    if (!Application.isPlaying) Lifecycle(session, "OnEnable");
                    if (session.OpeningWarningActive) session.HandleMenuKey(KeyCode.Return);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    var clip = AudioClip.Create("AnotherJoe", 44100 * 8, 1, 44100, false);
                    try
                    {
                        var settings = new SerializedObject(music);
                        settings.FindProperty("track").objectReferenceValue = clip;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        ProgressionChecks.Add(grid, EnemyColor.Red, 5, 0);
                        var first = ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                        var second = ProgressionChecks.Add(grid, EnemyColor.Green, 1, 0);
                        session.Tick(0); music.UpdatePlayback();
                        Near(session.TempoMultiplier, 1, "Ordinary greens do not speed up the game");
                        Near(Time.timeScale, originalTimeScale, "Ordinary greens retain the incoming time scale");
                        Near(music.Source.pitch, 1, "Ordinary greens retain normal music playback");

                        ProgressionChecks.Add(grid, EnemyColor.Green, 2, 0);
                        grid.RefreshSpecials();
                        float beatBefore = music.BeatPosition;
                        session.Tick(0); music.UpdatePlayback();
                        Check(grid.SpecialGreenCount == 3, "Each promoted green counts separately");
                        Near(session.TempoMultiplier, 1.09f, "Three special greens stack three tempo boosts");
                        Near(Time.timeScale, originalTimeScale * (Application.isPlaying ? 1.09f : 1),
                            "The live game clock accelerates without changing edit-mode time settings");
                        Near(music.Source.pitch, 1.09f, "The current track plays faster");
                        Near(music.CurrentTempo, 140 * 1.09f, "Reported tempo includes the playback speed");
                        Check(music.Source.clip == clip && Mathf.Abs(music.BeatPosition - beatBefore) < .25f,
                            "Changing tempo retains the recording and musical phase");
                        Near(music.SecondsAtBeat(4) - music.SecondsAtBeat(3), 60f / 140,
                            "Beat durations stay in game seconds to avoid double acceleration");
                        for (int i = 0; i < 20; i++) session.Tick(0);
                        Near(Time.timeScale, originalTimeScale * (Application.isPlaying ? 1.09f : 1),
                            "Repeated updates do not compound the boost");

                        grid.SetColor(first.Id, EnemyColor.Red);
                        session.Tick(0); music.UpdatePlayback();
                        Near(session.TempoMultiplier, 1.06f, "A transformed green removes exactly one boost");
                        second.gameObject.SetActive(false);
                        session.Tick(0); music.UpdatePlayback();
                        Near(Time.timeScale, originalTimeScale * (Application.isPlaying ? 1.03f : 1),
                            "An unregistered green immediately stops contributing");
                        Near(music.Source.pitch, 1.03f, "The music follows the surviving special green");

                        session.Pause();
                        session.Tick(10);
                        Near(Time.timeScale, 0, "Tempo updates cannot unpause the game");
                        grid.ClearMatchingChain(grid.Model.At(2, 0).Id, EnemyColor.Green);
                        session.Tick(0);
                        Near(Time.timeScale, 0, "Removing a boost while paused keeps time frozen");
                        session.Resume();
                        Near(Time.timeScale, originalTimeScale, "Resume uses the current count instead of a stale boost");
                        Near(music.Source.pitch, 1, "Clearing special greens restores normal track speed");

                        // Fill every supported cell: all per-enemy boosts must remain
                        // below the AudioSource playback limit on desktop and phones.
                        for (int row = 0; row < GridModel.Rows; row++)
                        for (int column = 0; column < GridModel.Columns; column++)
                        {
                            var cell = grid.Model.At(column, row);
                            if (cell == null) ProgressionChecks.Add(grid, EnemyColor.Green, column, row);
                            else grid.SetColor(cell.Id, EnemyColor.Green);
                        }
                        grid.RefreshSpecials();
                        Check(grid.SpecialGreenCount == GridModel.Columns * GridModel.Rows,
                            "A full green fleet counts every special");
                        Check(session.TempoMultiplier < 3, "The largest fleet fits the supported audio playback range");
                        session.Pause();
                        Near(Time.timeScale, 0, "The largest tempo boost can still be paused");
                        if (!Application.isPlaying) Lifecycle(session, "OnDisable");
                        UnityEngine.Object.DestroyImmediate(session);
                        session = null;
                        Near(Time.timeScale, originalTimeScale, "Destroying a paused boosted session restores global time");
                    }
                    finally
                    {
                        if (session != null)
                        {
                            if (!Application.isPlaying) Lifecycle(session, "OnDisable");
                            UnityEngine.Object.DestroyImmediate(session);
                        }
                        UnityEngine.Object.DestroyImmediate(clip);
                    }
                });
            }
            finally { Time.timeScale = originalTimeScale; }
            Debug.Log("Special Green tempo checks passed: per-enemy stacking, music phase, shared game clock, transformations, removals, pause, cleanup and full-fleet audio limits.");
        }

        private static void Lifecycle(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
        private static void Near(float actual, float expected, string message) =>
            Check(Mathf.Abs(actual - expected) < .001f, message + $" (expected {expected}, got {actual})");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Special Green tempo check failed: " + message); }
    }
}
