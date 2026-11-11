using System;
using System.Reflection;
using System.Linq;
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

                        var third = ProgressionChecks.Add(grid, EnemyColor.Green, 2, 0);
                        grid.RefreshSpecials();
                        session.Tick(0); music.UpdatePlayback();
                        Check(grid.SpecialGreenCount == 3 && grid.RegularGreenCount == 0, "Specials leave the movement roster");
                        Near(session.TempoMultiplier, 1, "Promotion alone cannot raise tempo");
                        var movement = grid.GetComponent<EnemyGridMovement>();
                        var position = grid.transform.position;
                        movement.Tick(2);
                        Near(movement.CurrentSpeed, 0, "Special-only fleet has no movement speed");
                        Check(grid.transform.position == position, "Special-only fleet stays still");
                        foreach (var green in new[] { first, second, third }) Activate(green);
                        float beatBefore = music.BeatPosition;
                        session.Tick(0); music.UpdatePlayback();
                        Check(grid.ActiveGreenTempoCount == 3, "Only activated specials contribute boosts");
                        grid.Unregister(first);
                        grid.Register(first);
                        Check(first.GetComponent<EnemyAbilities>().GreenTempoActive,
                            "Re-registering the same special preserves its spent activation");
                        Near(session.TempoMultiplier, 1.09f, "Three activations stack three tempo boosts");
                        Near(Time.timeScale, originalTimeScale * (Application.isPlaying ? 1.09f : 1), "Shared clock follows tempo");
                        Near(music.Source.pitch, 1.09f, "Music follows the triggered tempo");
                        Near(music.CurrentTempo, 140 * 1.09f, "Reported tempo includes playback speed");
                        Check(music.Source.clip == clip && Mathf.Abs(music.BeatPosition - beatBefore) < .25f,
                            "Tempo changes preserve musical phase");
                        foreach (var green in new[] { first, second, third })
                        {
                            var ability = green.GetComponent<EnemyAbilities>();
                            Set(ability, "cooldownBeat", -1);
                            ability.Tick(0);
                            Check(!ability.IsCasting, "Spent special cannot cast again");
                            Check(green.GetComponentsInChildren<MeshRenderer>().Length == 4, "Shared layered dance arrow exists");
                            var arrow = green.GetComponentsInChildren<MeshRenderer>()[0].transform.parent;
                            Check(Vector3.Dot(arrow.right, Vector3.up) > .99f, "Special arrow points upward");
                            Check(arrow.position.y > green.Visuals.Body.bounds.max.y, "Special arrow remains above its owner");
                            var presentation = green.GetComponent<EnemyPresentation>();
                            float width = arrow.lossyScale.x;
                            presentation.Tick(EnemyPresentation.MovementPulseSeconds / 4, EnemyColor.Green, 0);
                            Check(arrow.lossyScale.x > width, "Special up arrow uses the regular arrow's pop animation");
                            presentation.Tick(2, EnemyColor.Green, 0);
                            Check(green.GetComponentsInChildren<MeshRenderer>().All(layer => layer.enabled),
                                "Special up arrow persists after its activation animation");
                        }
                        float beatDuration = music.BeatDuration;
                        grid.SetColor(first.Id, EnemyColor.Red);
                        session.Tick(0); music.UpdatePlayback();
                        Near(session.TempoMultiplier, 1.09f, "Removing a boost starts a gradual decrease");
                        session.Tick(beatDuration / 2); music.UpdatePlayback();
                        Near(session.TempoMultiplier, 1.075f, "Tempo is halfway down halfway through one beat");
                        session.Pause(); session.Tick(10);
                        Near(session.TempoMultiplier, 1.075f, "Pause freezes the tempo ramp");
                        Near(Time.timeScale, 0, "Tempo ramp cannot unpause the game");
                        session.Resume(); session.Tick(beatDuration / 2); music.UpdatePlayback();
                        Near(session.TempoMultiplier, 1.06f, "Decrease completes in one beat");
                        var existingFalls = UnityEngine.Object.FindObjectsByType<GreenTempoFall>(FindObjectsSortMode.None);
                        int cleared = grid.ClearMatchingChain(second.Id, EnemyColor.Green);
                        Check(cleared == 2, "Both remaining greens clear as one group");
                        session.Tick(0); music.UpdatePlayback();
                        Near(session.TempoMultiplier, 1.06f, "Group removal also ramps smoothly");
                        var falls = UnityEngine.Object.FindObjectsByType<GreenTempoFall>(FindObjectsSortMode.None)
                            .Where(fall => !existingFalls.Contains(fall)).ToArray();
                        Check(falls.Length == 2, "Destroyed greens leave one down arrow each");
                        Check(Mathf.Abs(falls[0].transform.position.x - falls[1].transform.position.x) > .1f,
                            "Detached down arrows preserve their individual world positions");
                        Check(falls.All(fall => fall.transform.lossyScale.x < .8f),
                            "Detached down arrows preserve their world size");
                        foreach (var fall in falls)
                        {
                            var origin = fall.transform.position;
                            Check(Vector3.Dot(fall.transform.right, Vector3.down) > .99f, "Removal arrow points down");
                            fall.Tick(beatDuration / 2);
                            Check(fall.transform.position.y < origin.y, "Removal arrow animates downward");
                            fall.Tick(beatDuration / 2);
                            Check(!fall.gameObject.activeSelf, "Down arrows finish after one beat");
                            UnityEngine.Object.DestroyImmediate(fall.gameObject);
                        }
                        session.Tick(beatDuration); music.UpdatePlayback();
                        Near(session.TempoMultiplier, 1, "Group removal restores baseline in one beat");
                        Near(music.Source.pitch, 1, "Music returns to baseline with the game clock");

                        for (int row = 0; row < GridModel.Rows; row++)
                        for (int column = 0; column < GridModel.Columns; column++)
                        {
                            var cell = grid.Model.At(column, row);
                            if (cell == null) ProgressionChecks.Add(grid, EnemyColor.Green, column, row);
                            else grid.SetColor(cell.Id, EnemyColor.Green);
                        }
                        grid.RefreshSpecials();
                        foreach (var green in grid.GetComponentsInChildren<GridEnemy>()) Activate(green);
                        typeof(GameSession).GetMethod("TickGreenTempo", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(session, new object[] { 0f });
                        Check(grid.ActiveGreenTempoCount == GridModel.Columns * GridModel.Rows, "Every special can activate once");
                        Check(session.TempoMultiplier < 3, "Full fleet remains inside audio playback limits");
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
            Debug.Log("Special Green tempo checks passed: one-shot activation, stationary specials, persistent up arrows, one-beat tempo ramps, down arrows, pause and full-fleet limits.");
        }

        internal static void Activate(GridEnemy green)
        {
            var ability = green.GetComponent<EnemyAbilities>();
            if (ability.GreenTempoActive) return;
            ability.Tick(0); // Reset any changed color before scheduling the test activation.
            Set(ability, "cooldownBeat", -1);
            ability.Tick(0);
            Check(ability.IsCasting, "Special begins its ability windup");
            Check(!ability.GreenTempoActive, "Windup alone does not raise tempo");
            Set(ability, "castBeat", -1);
            ability.Tick(0);
            Check(ability.GreenTempoActive, "Completing the windup activates tempo");
        }
        private static void Set(EnemyAbilities ability, string name, float value) => typeof(EnemyAbilities)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ability, value);

        private static void Lifecycle(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
        private static void Near(float actual, float expected, string message) =>
            Check(Mathf.Abs(actual - expected) < .001f, message + $" (expected {expected}, got {actual})");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Special Green tempo check failed: " + message); }
    }
}
