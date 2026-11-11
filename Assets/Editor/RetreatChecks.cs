using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class RetreatChecks
    {
        public static void Run()
        {
            var model = new GridModel();
            model.TryAdd(1, EnemyColor.Red, 0, 0);
            model.TryAdd(2, EnemyColor.Blue, 0, 1);
            model.TryAdd(3, EnemyColor.Red, 0, 3);
            model.TryAdd(4, EnemyColor.Blue, 1, 3);
            model.TryAdd(5, EnemyColor.Red, 1, 4);
            Check(model.RetreatStep().Count == 3, "Floating mixed-color group moves together");
            Check(model.At(0, 2).Id == 3 && model.At(1, 2).Id == 4 && model.At(1, 3).Id == 5,
                "Group shape preserved");
            Check(model.RetreatStep().Count == 0, "Stops upon vertical reconnection");
            Check(model.Count == 5 && model.ColorCount(EnemyColor.Blue) == 2, "Identity and counts preserved");
            model.Remove(2);
            Check(model.RetreatStep().Count == 3 && model.RetreatStep().Count == 0,
                "New disconnection restarts retreat");

            var diagonal = new GridModel();
            diagonal.TryAdd(1, EnemyColor.Red, 0, 0);
            diagonal.TryAdd(2, EnemyColor.Blue, 1, 1);
            Check(diagonal.RetreatStep().Count == 1 && diagonal.At(1, 0).Id == 2,
                "Diagonal contact does not anchor");
            var sideways = new GridModel();
            for (int row = 0; row < 4; row++) sideways.TryAdd(row, EnemyColor.Red, 0, row);
            sideways.TryAdd(9, EnemyColor.Blue, 1, 5);
            Check(sideways.RetreatStep().Count == 1 && sideways.RetreatStep().Count == 1 && sideways.RetreatStep().Count == 0,
                "Stops on first horizontal reconnection");
            var emptyTop = new GridModel();
            emptyTop.TryAdd(1, EnemyColor.Blue, 4, 8);
            emptyTop.TryAdd(2, EnemyColor.Red, 4, 9);
            for (int i = 0; i < 8; i++) Check(emptyTop.RetreatStep().Count == 2, "Unanchored fleet rises to top");
            Check(emptyTop.At(4, 0).Id == 1 && emptyTop.At(4, 1).Id == 2 && emptyTop.RetreatStep().Count == 0,
                "Top boundary anchors without overlap");
            Check(new GridModel().RetreatStep().Count == 0, "Empty field is stable");

            foreach (float delta in new[] { 1f / 30, 1f / 60, 1f / 120, 2f })
            {
                var root = new GameObject("Retreat fixture", typeof(EnemyGrid));
                try
                {
                    var grid = root.GetComponent<EnemyGrid>();
                    var obj = new GameObject("Floating enemy", typeof(GridEnemy));
                    obj.transform.SetParent(root.transform, false);
                    var enemy = obj.GetComponent<GridEnemy>();
                    enemy.Configure(EnemyColor.Blue, 2, 6);
                    grid.Register(enemy);
                    for (float time = 0; time < 4; time += delta) grid.TickRetreat(delta);
                    Check(enemy.Row == 0 && grid.Model.At(2, 0).Id == enemy.Id && grid.Model.Count == 1,
                        "Timed retreat preserves view and model");
                    Check(Vector3.Distance(enemy.transform.localPosition, grid.CellPosition(2, 0)) < 0.0001f,
                        "View reaches correct position");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            Debug.Log("Retreat checks passed: mixed colors, group shape, reconnection, empty top, counts and frame timing.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Retreat check failed: " + message); }
    }
}
