using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class OccupiedBoundaryChecks
    {
        public static void Run()
        {
            WithFleet((grid, movement) =>
            {
                int turns = 0;
                movement.SweepEnded += () => turns++;
                movement.Tick(100);
                Check(turns == 0 && movement.Direction == 1 && grid.transform.position.x == 0, "Empty fleet never turns or moves");
                Fill(grid);
                movement.Tick(2.4);
                Check(turns == 0, "No turn before occupied cell reaches edge");
                grid.Model.Remove(GridModel.Columns - 1);
                movement.Tick(0.1);
                Check(turns == 0 && movement.Direction == 1, "Removing leading column postpones turnaround");
                movement.Tick(2.5);
                Near(grid.transform.position.x, 1.5f, "Extra travel equals one empty column");
                Check(turns == 1 && movement.Direction == -1, "Turns when next occupied column touches border");
                grid.Model.Remove(0);
                movement.Tick(10);
                Near(grid.transform.position.x, -1.5f, "Empty left column extends leftward travel");
                Check(turns == 2 && movement.Direction == 1, "Both sides use live occupancy");
            });
            WithFleet((grid, movement) =>
            {
                grid.Model.TryAdd(1, EnemyColor.Red, GridModel.Columns - 1, 9);
                int turns = 0;
                movement.SweepEnded += () => turns++;
                movement.Tick(2.5);
                Check(turns == 1, "Enemy anywhere in the column counts");
            });
            foreach (int frames in new[] { 1, 600, 1200, 2400 })
                WithFleet((grid, movement) =>
                {
                    Fill(grid);
                    int turns = 0;
                    movement.SweepEnded += () => turns++;
                    for (int i = 0; i < frames; i++) movement.Tick(20.0 / frames);
                    Near(grid.transform.position.x, 0, "Large and small frames agree");
                    Check(turns == 4 && movement.Direction == 1, "Every real contact fires once");
                });
            WithFleet((grid, movement) =>
            {
                Fill(grid);
                var settings = new SerializedObject(movement);
                settings.FindProperty("speed").floatValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                movement.Tick(100);
                Near(grid.transform.position.x, 0, "Zero speed remains still");
            });
            WithFleet((grid, movement) =>
            {
                Fill(grid);
                var settings = new SerializedObject(grid);
                settings.FindProperty("spacing").floatValue = 2 * PlayerMovement.HalfWidth / GridModel.Columns;
                settings.ApplyModifiedPropertiesWithoutUndo();
                movement.Tick(100);
                Near(grid.transform.position.x, 0, "No bounce loop when formation fills the entire field width");
            });
            WithFleet((grid, movement) =>
            {
                Fill(grid);
                int turns = 0;
                movement.SweepEnded += () => { turns++; movement.enabled = false; };
                movement.Tick(100);
                Check(turns == 1, "Game-over callback stops further contacts in long frame");
                Near(grid.transform.position.x, .75f, "Stops at first contact");
            });
            WithFleet((grid, movement) =>
            {
                Fill(grid);
                int turns = 0;
                movement.SweepEnded += () => { turns++; movement.ResetSweep(); };
                movement.Tick(100);
                Check(turns == 1 && movement.Direction == 1, "Reset cancels leftover movement");
                Near(grid.transform.position.x, 0, "Reset restores origin");
            });
            CheckSpawnAlignment(false);
            CheckSpawnAlignment(true);
            Debug.Log("Occupied boundary checks passed: empty edges, live clears, both directions, timing, callbacks and spawn alignment.");
        }

        private static void CheckSpawnAlignment(bool left)
        {
            WithFleet((grid, movement) =>
            {
                var blue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blue Enemy.prefab");
                var red = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Enemy.prefab");
                var obj = UnityEngine.Object.Instantiate(blue, grid.transform);
                var enemy = obj.AddComponent<GridEnemy>();
                enemy.Configure(EnemyColor.Blue, left ? 3 : 2, 0);
                grid.Register(enemy);
                if (left) movement.Tick(7.5); // Column 3 touches the right edge, then heads left.
                var spawner = grid.gameObject.AddComponent<EnemyRowSpawner>();
                spawner.Configure(blue, red);
                int turns = 0;
                movement.SweepEnded += () =>
                {
                    float before = enemy.transform.position.x;
                    Check(spawner.TryAdvance(), "Contact spawns a new row");
                    Near(enemy.transform.position.x, before, "Reindexing does not jump survivor sideways");
                    turns++;
                };
                movement.Tick(left ? 17.5 : 10);
                Check(turns == 1 && grid.Model.Count == 1 + RunProgress.StandardRowWidth && enemy.Row == 1, "Exactly one descent and five new enemies");
                Check(enemy.Column == (left ? 0 : RunProgress.StandardRowWidth - 1), "Survivor labels align to contacted side");
                grid.OccupiedHorizontalBounds(out float min, out float max);
                Check(min >= -3.0001f && max <= 3.0001f, "Expanded row stays within playfield");
                Check(grid.Model.At(enemy.Column, 1).Id == enemy.Id && grid.Model.ColorCount(EnemyColor.Blue) >= 1,
                    "Identity and occupancy survive reindexing");
            });
        }

        private static void Fill(EnemyGrid grid)
        { for (int col = 0; col < GridModel.Columns; col++) grid.Model.TryAdd(col, EnemyColor.Blue, col, 0); }
        private static void WithFleet(Action<EnemyGrid, EnemyGridMovement> test)
        {
            var root = new GameObject("Occupied boundary fixture", typeof(EnemyGrid), typeof(EnemyGridMovement));
            try { root.GetComponent<EnemyGridMovement>().UseLegacyMovement = true; test(root.GetComponent<EnemyGrid>(), root.GetComponent<EnemyGridMovement>()); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void Near(float value, float expected, string message) => Check(Mathf.Abs(value - expected) < 0.001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Occupied boundary check failed: " + message); }
    }
}
