using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class LevelProgressionRuntimeChecks
    {
        private const string Pending = "CandyCruisers.LevelProgressionRuntimeChecks";
        private static int stage;
        private static double began, spawned;
        private static bool previousInvincibility;
        private static bool previousOverride;
        private static EnemyColor[] outgoingColors;
        private static double finished;
        static LevelProgressionRuntimeChecks() => EditorApplication.update += Poll;

        public static void RunDesktop() => RunSized(1100, 740, "desktop");
        public static void RunPhone() => RunSized(440, 900, "phone");
        private static void RunSized(int width, int height, string name)
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type)) ((EditorWindow)existing).Close();
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility(); window.position = new Rect(40, 40, width, height); window.Focus();
            SessionState.SetString(Pending + ".Size", name);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunAdditionalChecks()
        {
            GameplayFramingChecks.Run();
            MusicLoudnessChecks.Run();
            CountingMetronomeChecks.Run();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            try
            {
                Application.runInBackground = true;
                if (began == 0) began = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - began > 45) throw new Exception("Level runtime check timed out at stage " + stage);
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null || Time.timeSinceLevelLoad < .2f) return;
                if (session.IsPaused) session.Resume();
                var grid = session.GetComponent<EnemyGrid>();
                var music = session.GetComponent<GameplayMusicPlayer>();
                var background = GameObject.Find("Star Background").GetComponent<SpriteRenderer>();
                if (stage == 0)
                {
                    Check(session.State == GameSession.RunState.MainMenu, "Runtime starts at the menu");
                    Check(session.LevelCatalog != null && session.LevelCatalog.ForLevel(1) != null,
                        "The gameplay scene loads its JSON map before Play without Unity level assets");
                    previousInvincibility = DeveloperOptions.PlayerInvincible;
                    DeveloperOptions.PlayerInvincible = true;
                    previousOverride = SpawnOverride.Enabled; SpawnOverride.Enabled = false;
                    var catalog = LevelCatalog.Parse(LevelProgressionChecks.FixtureJson);
                    var first = catalog.levels[0];
                    first.backgroundImage = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/galaxy.jpeg");
                    catalog.levels[1].fleets[0].initialFleet[0] = "0b*b*b";
                    catalog.levels[1].fleets[0].lineSpawns = new[] { "******" };
                    session.LevelCatalog = catalog; session.PresentationPreview = true; session.StartLevel = 1;
                    session.StartRun();
                    Check(session.Progress.ClearedFleets == 0 && session.Progress.FleetsToClear == 2 && session.Progress.Defeated == 0,
                        "Live run starts with zero completed fleets out of the two scripted entries");
                    Check(session.DisplayedColorClearBarColors.SequenceEqual(session.Fleet.PendingPlan.Colors) &&
                        session.DisplayedColorClearBarColors.Count == 3, "Opening bars show the reserved three colors before any enemy arrives");
                    stage = 1;
                }
                else if (stage == 1 && session.State == GameSession.RunState.Playing)
                {
                    if (spawned == 0) spawned = EditorApplication.timeSinceStartup;
                    if (EditorApplication.timeSinceStartup - spawned < .9f) return;
                    Check(grid.Model.Count == 20 && grid.GetComponentsInChildren<GridEnemy>().Select(enemy => enemy.Color).Distinct().Count() == 3 &&
                        grid.Model.ColorCount(EnemyColor.Green) > 0 && grid.Model.ColorCount(EnemyColor.Purple) > 0,
                        "Live opening uses four lines, exactly three colors and both required colors");
                    CheckNarrowRows(grid, 4);
                    var planned = session.Fleet.ActivePlan.CopyCells();
                    for (int i = 0; i < planned.Length; i++)
                        Check(grid.Model.At(i % 6, i / 6)?.Color == planned[i], "Live sparse fleet preserves every scripted gap and color assignment");
                    Check(background.enabled && background.sprite == session.CurrentLevelDefinition.backgroundImage,
                        "Live level displays its assigned image");
                    Check(music.Source.clip == null || music.Source.clip.name == "AnotherJoe", "Live level restricts menu carryover and soundtrack selection");
                    outgoingColors = session.Fleet.PresentColors().ToArray();
                    Check(session.DisplayedColorClearBarColors.SequenceEqual(outgoingColors), "Live bars match the exact active palette");
                    Directory.CreateDirectory("TestResults");
                    ScreenCapture.CaptureScreenshot("TestResults/level-progression-" + SessionState.GetString(Pending + ".Size", "desktop") + ".png");
                    stage = 2;
                }
                else if (stage == 2)
                {
                    Check(grid.GetComponent<EnemyRowSpawner>().TryAdvance(), "Live configured fleet adds a new top row");
                    var round = session.Fleet.ActivePlan;
                    var line = SpawnScript.Decode(round.LineSpawns[0], round.Bindings);
                    for (int column = 0; column < line.Length; column++)
                        Check(grid.Model.At(column, 0)?.Color == line[column], "Live incoming line shares the opening's numbered assignments");
                    CheckNarrowRows(grid, 5);
                    var partial = grid.GetComponentsInChildren<GridEnemy>().First();
                    grid.ClearMatchingChain(partial.Id, partial.Color);
                    Check(grid.Model.Count > 0 && session.Progress.Level == 1 && session.Progress.ClearedFleets == 0 &&
                        session.Progress.ActiveLevelProgressFraction == 0, "A live partial clear cannot complete a fleet");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().ToArray())
                        if (grid.View(enemy.Id) != null) grid.ClearMatchingChain(enemy.Id, enemy.Color);
                    Check(session.Progress.Level == 1 && session.Progress.ClearedFleets == 1 && session.State == GameSession.RunState.Refilling &&
                        session.Progress.ActiveLevelProgressFraction == .5f && session.Fleet.PendingPlan.Rows == 2,
                        "The first live full clear stays in level one and reserves its second scripted fleet");
                    spawned = 0;
                    stage = 3;
                }
                else if (stage == 3 && session.State == GameSession.RunState.Refilling)
                {
                    Check(session.Fleet.Count == 0 && session.Fleet.PendingPlan.Colors.Count == 3 &&
                        session.DisplayedColorClearBarColors.SequenceEqual(outgoingColors),
                        "A same-level refill keeps its outgoing bars until the next scripted fleet arrives");
                }
                else if (stage == 3 && session.State == GameSession.RunState.Playing)
                {
                    if (spawned == 0) spawned = EditorApplication.timeSinceStartup;
                    if (EditorApplication.timeSinceStartup - spawned < .9f) return;
                    Check(grid.Model.Count == 10 && session.Progress.Level == 1 && session.Progress.ActiveLevelProgressFraction == .5f &&
                        session.Fleet.ActivePlan.Rows == 2 && background.enabled,
                        "The second scripted fleet plays in the same level with half the progress bar filled");
                    ScreenCapture.CaptureScreenshot("TestResults/fleet-progress-half-" + SessionState.GetString(Pending + ".Size", "desktop") + ".png");
                    stage = 4;
                }
                else if (stage == 4)
                {
                    outgoingColors = session.Fleet.PresentColors().ToArray();
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().ToArray())
                        if (grid.View(enemy.Id) != null) grid.ClearMatchingChain(enemy.Id, enemy.Color);
                    Check(session.Progress.Level == 2 && session.Progress.ClearedFleets == 0 && session.Progress.FleetsToClear == 1 &&
                        session.State == GameSession.RunState.Refilling && session.Fleet.PendingPlan.Colors.SequenceEqual(new[] { EnemyColor.Blue }),
                        "Clearing the final scripted fleet advances exactly once and reserves the new level's first fleet");
                    stage = 5;
                }
                else if (stage == 5 && session.State == GameSession.RunState.Refilling)
                {
                    Check(session.Fleet.Count == 0 && session.DisplayedColorClearBarColors.SequenceEqual(outgoingColors),
                        "The level transition keeps its outgoing colors until arrival");
                }
                else if (stage == 5 && session.State == GameSession.RunState.Playing)
                {
                    Check(grid.Model.Count == 15 && grid.Model.ColorCount(EnemyColor.Blue) == 15,
                        "Live refill resolves opening stars within the next level's Blue-only palette");
                    Check(!background.enabled && (music.Source.clip == null || music.Source.clip.name == "Contact"),
                        "Live level transition restores black and changes the allowed soundtrack");
                    Check(session.DisplayedColorClearBarColors.SequenceEqual(new[] { EnemyColor.Blue }) &&
                        session.Fleet.ClearedColorCount == 0 && session.Fleet.ActivePlan.Columns == grid.GetComponent<EnemyRowSpawner>().CurrentRowWidth,
                        "Arrival changes slots, color counts, dimensions and reward flags together");
                    Check(grid.GetComponent<EnemyRowSpawner>().TryAdvance() && grid.Model.Count == 21,
                        "A live incoming wildcard line adds six cells to the existing fleet");
                    for (int column = 0; column < 6; column++)
                        Check(grid.Model.At(column, 0)?.Color == EnemyColor.Blue, "Live incoming stars use only the captured opening palette");
                    finished = EditorApplication.timeSinceStartup; stage = 6;
                }
                else if (stage == 6 && EditorApplication.timeSinceStartup - finished > 1f)
                {
                    Check(grid.GetComponentsInChildren<GridEnemy>().All(enemy => enemy.Visuals.Body.transform.lossyScale.x > .01f),
                        "Opening and incoming wildcard enemies finish their arrival animation");
                    ScreenCapture.CaptureScreenshot("TestResults/fleet-bars-next-" + SessionState.GetString(Pending + ".Size", "desktop") + ".png");
                    finished = EditorApplication.timeSinceStartup; stage = 7;
                }
                else if (stage == 7 && EditorApplication.timeSinceStartup - finished > .25f)
                {
                    DeveloperOptions.PlayerInvincible = previousInvincibility;
                    SpawnOverride.Enabled = previousOverride;
                    SessionState.SetBool(Pending, false);
                    Debug.Log("Level progression runtime checks passed: " + SessionState.GetString(Pending + ".Size", "desktop") +
                        ", JSON source, sparse opening, round-wide assignments, opening and incoming wildcards, required colors, partial clears, every scripted fleet, progress bar, level change, refill, bars, background and soundtrack selection.");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception error)
            {
                SessionState.SetBool(Pending, false);
                DeveloperOptions.PlayerInvincible = previousInvincibility;
                SpawnOverride.Enabled = previousOverride;
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        private static void CheckNarrowRows(EnemyGrid grid, int rows)
        {
            for (int row = 0; row < rows; row++)
            for (int column = 2; column < GridModel.Columns; column++)
                Check(grid.Model.At(column, row) == null || grid.Model.At(column - 1, row) == null || grid.Model.At(column - 2, row) == null ||
                    grid.Model.At(column, row).Color != grid.Model.At(column - 1, row).Color ||
                    grid.Model.At(column, row).Color != grid.Model.At(column - 2, row).Color,
                    "Live scripted opening and top rows never contain three matching neighbors");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Level progression runtime check failed: " + message); }
    }
}
