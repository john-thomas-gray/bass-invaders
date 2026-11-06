using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class FleetStateChecks
    {
        public static void Run()
        {
            CheckModel();
            CheckPaletteSizes();
            CheckSavedCatalog();
            bool previousOverride = SpawnOverride.Enabled;
            var first = new LevelDefinition();
            var second = new LevelDefinition();
            var catalog = new LevelCatalog();
            try
            {
                SpawnOverride.Enabled = false;
                first.fleets[0].initialFleet = new[] { "11223", "33112" }; first.fleets[0].lineSpawns = new[] { "11223" };
                first.fleets[0].possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow };
                first.fleets[0].requiredColors = new[] { EnemyColor.Green };
                first.possibleSoundtrackTracks = new[] { "MissingFleetStateCheck" };
                second.fleets[0].initialFleet = new[] { "11111", "11111" }; second.fleets[0].lineSpawns = new[] { "11111" };
                second.fleets[0].possibleColors = second.fleets[0].requiredColors = new[] { EnemyColor.Blue };
                second.possibleSoundtrackTracks = first.possibleSoundtrackTracks;
                catalog.levels = new[] { first, second };
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player); session.LevelCatalog = catalog; session.PresentationPreview = true;
                    if (!Application.isPlaying) Invoke(session, "OnEnable");
                    Invoke(session, "Start");
                    Check(ReferenceEquals(session.Fleet, grid.Model.Fleet) && ReferenceEquals(player.Fleet, session.Fleet),
                        "Session, player and logical grid share the same state object");
                    Check(session.DisplayedColorClearBarColors.Count == 0, "No fleet or reservation means no invented bars");
                    session.StartRun();
                    var reservation = session.Fleet.PendingPlan;
                    var expected = reservation.Colors.ToArray();
                    Check(expected.Length == 3 && reservation.Rows == 2 && session.DisplayedColorClearBarColors.SequenceEqual(expected),
                        "Opening bars use exactly the reserved selection, not the five possible colors");
                    var copied = reservation.CopyCells(); copied[0] = EnemyColor.Orange;
                    first.fleets[0].initialFleet = new[] { "11111", "11111", "11111" };
                    first.fleets[0].possibleColors = first.fleets[0].requiredColors = new[] { EnemyColor.Blue };
                    Check(!reservation.CopyCells().Contains(EnemyColor.Orange) && reservation.Rows == 2 &&
                        session.DisplayedColorClearBarColors.SequenceEqual(expected), "Reserved cells and dimensions survive edits without changing the bars");
                    session.Tick(20);
                    Check(session.Fleet.Count == 10 && session.Fleet.ActivePlan == reservation && session.Fleet.PendingPlan == null &&
                        session.Fleet.PresentColors().SequenceEqual(expected), "Spawning atomically activates the same reserved fleet");
                    var spawner = grid.GetComponent<EnemyRowSpawner>();
                    Check(spawner.TryAdvance() && session.Fleet.Count == 15 &&
                        session.Fleet.PresentColors().All(expected.Contains), "Later rows keep the active palette despite level asset edits");
                    CheckGeometry(session);
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().ToArray())
                    {
                        if (grid.View(enemy.Id) == null) continue;
                        grid.ClearMatchingChain(enemy.Id, enemy.Color);
                        Check(session.DisplayedColorClearBarColors.SequenceEqual(expected),
                            "Clearing enemies or a color never deletes a slot from this fleet");
                    }
                    Check(session.Progress.Level == 2 && session.Fleet.Count == 0 && session.Fleet.AllColorsCleared &&
                        session.Fleet.PendingPlan.Colors.SequenceEqual(new[] { EnemyColor.Blue }),
                        "An empty completed fleet keeps its own rewards while the next palette is reserved separately");
                    var row = new Rect(20, 500, 320, 5);
                    var original = session.DisplayedColorClearBarSlotRect(row, 0);
                    session.Pause(); session.Tick(100); session.Resume();
                    Check(session.DisplayedColorClearBarColors.SequenceEqual(expected) &&
                        session.DisplayedColorClearBarSlotRect(row, 0) == original, "Pause and resume cannot mix outgoing and incoming bar layouts");
                    foreach (var effect in UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None)) effect.Tick(20);
                    session.Tick(100);
                    if (session.State == GameSession.RunState.Refilling)
                        Check(session.DisplayedColorClearBarColors.SequenceEqual(expected), "Even a long celebration frame keeps the outgoing slots until arrival");
                    session.Tick(100);
                    Check(session.State == GameSession.RunState.Playing && session.Fleet.Count == 10 &&
                        session.Fleet.ColorCount(EnemyColor.Blue) == 10 && session.Fleet.ClearedColorCount == 0 &&
                        session.DisplayedColorClearBarColors.SequenceEqual(new[] { EnemyColor.Blue }),
                        "A smaller incoming fleet replaces all colors, counts and clear flags together");
                    CheckGeometry(session);
                    if (!Application.isPlaying) Invoke(session, "OnDisable");
                });
            }
            finally
            {
                SpawnOverride.Enabled = previousOverride;
            }
            Debug.Log("Fleet state checks passed: shared identity, live model counts, immutable reservations, exact bars, asset edits, color clears, level transitions, smaller fleets and desktop/phone slot geometry.");
        }

        private static void CheckModel()
        {
            var model = new GridModel(); var fleet = model.Fleet;
            Check(model.TryAdd(1, EnemyColor.Red, 0, 0) && model.TryAdd(2, EnemyColor.Blue, 2, 0), "Model fixture registers two colors");
            Check(!model.TryAdd(3, EnemyColor.Green, 0, 0) && fleet.Count == 2 && !fleet.Colors.Contains(EnemyColor.Green),
                "Rejected registrations cannot invent colors or counts");
            Check(model.SetColor(1, EnemyColor.Green) && fleet.ColorCount(EnemyColor.Red) == 0 && fleet.ColorCount(EnemyColor.Green) == 1,
                "Recoloring updates live counts at their owning source");
            model.Remove(1);
            Check(fleet.Count == 1 && fleet.PresentColors().SequenceEqual(new[] { EnemyColor.Blue }) && fleet.Colors.Count == 3,
                "Live presence and stable fleet history have distinct, explicit meanings");
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                grid.Model.SetColor(enemy.Id, EnemyColor.Green);
                Check(enemy.Color == EnemyColor.Green && grid.Fleet.ColorCount(enemy.Color) == 1,
                    "A registered view reads its true color from its logical occupant");
                grid.TryDescend();
                Check(enemy.Row == grid.Model.Find(enemy.Id).Row && enemy.Row == 1,
                    "View coordinates share the model after exactly one descent");
                enemy.gameObject.SetActive(false);
                grid.Unregister(enemy); // Edit mode does not dispatch the runtime lifecycle automatically.
                Check(grid.Fleet.Count == 0, "Disabled enemies immediately leave the live count");
                enemy.gameObject.SetActive(true);
                grid.Register(enemy);
                Check(grid.Fleet.Count == 1 && grid.Fleet.ColorCount(EnemyColor.Green) == 1, "Re-enabling restores the same true color once");
            });
        }

        private static void CheckPaletteSizes()
        {
            var definition = new LevelDefinition();
            var catalog = new LevelCatalog(); catalog.levels = new[] { definition };
            bool previousOverride = SpawnOverride.Enabled; SpawnOverride.Enabled = false;
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player); session.LevelCatalog = catalog;
                    var spawner = grid.GetComponent<EnemyRowSpawner>();
                    foreach (int count in new[] { 1, 2, 3, 4, 5, 6, 2, 1 })
                    {
                        definition.fleets[0].possibleColors = EnemyPalette.Colors.Take(count).ToArray();
                        string pattern = string.Concat(Enumerable.Range(0, 10).Select(i => ((char)('1' + i % count)).ToString()));
                        definition.fleets[0].initialFleet = new[] { pattern.Substring(0, 5), pattern.Substring(5) };
                        definition.fleets[0].lineSpawns = Array.Empty<string>();
                        var plan = spawner.PlanNextFleet();
                        Check(spawner.SpawnBatch(plan) && grid.Fleet.Count == 10 &&
                            session.DisplayedColorClearBarColors.SequenceEqual(grid.Fleet.PresentColors()) &&
                            session.DisplayedColorClearBarColors.Count == count, "One through six colors, then shrinking palettes, produce exactly that many bars");
                        CheckGeometry(session);
                        foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                        { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
                    }
                });
            }
            finally
            {
                SpawnOverride.Enabled = previousOverride;
            }
        }

        private static void CheckSavedCatalog()
        {
            var catalog = LevelCatalog.Load(Resources.Load<TextAsset>("LevelMap"));
            if (catalog == null) return;
            bool previousOverride = SpawnOverride.Enabled; SpawnOverride.Enabled = false;
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player); session.LevelCatalog = catalog;
                    var spawner = grid.GetComponent<EnemyRowSpawner>();
                    var plan = spawner.PlanNextFleet();
                    Check(spawner.SpawnBatch(plan) && session.DisplayedColorClearBarColors.SequenceEqual(grid.Fleet.PresentColors()) &&
                        session.DisplayedColorClearBarColors.Count == plan.Colors.Count && grid.Fleet.Count == plan.CellCount,
                        "The user's saved opening palette drives actual enemies and bars together");
                    CheckGeometry(session);
                });
            }
            finally { SpawnOverride.Enabled = previousOverride; }
        }

        private static void CheckGeometry(GameSession session)
        {
            foreach (int width in new[] { 320, 540, 1206, 1920 })
            {
                var row = new Rect(10, 500, width - 20, 5);
                var colors = session.DisplayedColorClearBarColors;
                for (int i = 0; i < colors.Count; i++)
                {
                    var rect = session.DisplayedColorClearBarSlotRect(row, i);
                    Check(rect.width > 0 && rect.x >= row.x && rect.xMax <= row.xMax &&
                        rect == GameSession.ColorClearBarSlotRect(row.x, row.y, row.width, colors.Count, i),
                        "Every slot uses the same active count on desktop and phone widths");
                }
            }
        }
        private static void Invoke(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Fleet state check failed: " + message); }
    }
}
