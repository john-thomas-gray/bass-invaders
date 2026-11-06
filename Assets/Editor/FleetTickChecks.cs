using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class FleetTickChecks
    {
        public static void Run()
        {
            foreach (int frames in new[] { 1, 60, 600 })
            {
                var root = new GameObject("Fleet tick fixture", typeof(EnemyGrid), typeof(EnemyGridMovement));
                try
                {
                    var grid = root.GetComponent<EnemyGrid>();
                    var movement = root.GetComponent<EnemyGridMovement>();
                    grid.Model.TryAdd(1, EnemyColor.Green, 0, 0);
                    grid.Model.TryAdd(2, EnemyColor.Green, 1, 0);
                    grid.Model.TryAdd(3, EnemyColor.Green, 2, 0);
                    var pulses = new List<int>();
                    movement.GreenPulsed += pulses.Add;
                    float beat = FullSetCelebration.StepDuration;
                    movement.Tick(beat * .5);
                    Check(root.transform.position.x == 0 && pulses.Count == 0, "Fleet holds still between beats");
                    for (int i = 0; i < frames; i++) movement.Tick(12.0 * beat / frames);
                    float expectedTravel = movement.CurrentSpeed * beat * 6;
                    Check(Mathf.Abs(root.transform.position.x - expectedTravel) < .0001f,
                        $"Even-beat movement halves original travel across frame sizes: expected {expectedTravel}, got {root.transform.position.x}");
                    Check(pulses.SequenceEqual(new[] { 1, 2, 3, 1, 2, 3 }), "Every Green pulses once before any repeats");
                    grid.Model.Remove(1);
                    grid.Model.SetColor(2, EnemyColor.Blue);
                    movement.Tick(beat);
                    Check(pulses.Last() == 3 && pulses.Count == 7, "Dead and converted Greens leave the rotation");
                    movement.Tick(beat * .25);
                    movement.enabled = false;
                    movement.Tick(100);
                    Check(pulses.Count == 7, "Pause cannot accrue movement");
                    movement.enabled = true;
                    movement.Tick(beat * 1.75);
                    Check(pulses.Count == 8, "Resume preserves partial beat progress");
                    movement.Tick(beat * .25);
                    movement.ResetSweep();
                    movement.Tick(beat * .5);
                    Check(root.transform.position.x == 0 && pulses.Count == 8, "New wave discards previous partial movement");
                    grid.Model.TryAdd(4, EnemyColor.Green, 0, 0);
                    movement.Tick(4 * beat);
                    Check(pulses.Skip(8).SequenceEqual(new[] { 4, 3 }), "New Greens join deterministic row order after reset");
                    Check(grid.Model.TryAdd(5, EnemyColor.Green, 3, 0), "New Green occupies an empty cell");
                    movement.Tick(2 * beat);
                    Check(pulses.Last() == 5, "A joining Green gets a turn before an earlier Green repeats");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var cadenceRoot = new GameObject("Even beat fixture", typeof(EnemyGrid), typeof(EnemyGridMovement));
            try
            {
                var grid = cadenceRoot.GetComponent<EnemyGrid>();
                grid.Model.TryAdd(1, EnemyColor.Green, 0, 0);
                var movement = cadenceRoot.GetComponent<EnemyGridMovement>();
                float beat = FullSetCelebration.StepDuration;
                movement.Tick(beat * .99);
                Check(cadenceRoot.transform.position.x == 0, "Count one holds the fleet still");
                movement.Tick(beat * .01);
                float onTwo = cadenceRoot.transform.position.x;
                Check(Mathf.Abs(onTwo - movement.CurrentSpeed * beat) < .0001f,
                    "Count two moves the fleet by the original single-beat distance");
                movement.Tick(beat);
                Check(cadenceRoot.transform.position.x == onTwo, "Count three holds the fleet still");
                movement.Tick(beat);
                Check(Mathf.Abs(cadenceRoot.transform.position.x - 2 * onTwo) < .0001f,
                    "Count four moves the fleet again");
                movement.Tick(beat);
                Check(Mathf.Abs(cadenceRoot.transform.position.x - 2 * onTwo) < .0001f,
                    "The next downbeat holds the fleet still");
            }
            finally { UnityEngine.Object.DestroyImmediate(cadenceRoot); }
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                var visual = green.GetComponent<EnemyPresentation>();
                visual.Tick(1, EnemyColor.Green, 0);
                Color resting = green.Visuals.Body.color;
                visual.MovementPulse();
                Check(green.Visuals.Body.color != resting, "Selected Green visibly flashes");
                var bolts = green.GetComponentsInChildren<LineRenderer>().Where(line => line.name.StartsWith("Green surge")).ToArray();
                Check(bolts.Length == 10 && bolts.All(line => line.enabled && !line.useWorldSpace),
                    "Green sparks have a corona and four electrical branches that follow the enemy");
                Check(!green.GetComponentsInChildren<LineRenderer>().Any(line => line.name.Contains("arrow")),
                    "Green electricity contains no arrows");
                Vector3 point = bolts[0].GetPosition(1);
                visual.Tick(.06f, EnemyColor.Green, 0);
                Check(bolts[0].GetPosition(1) != point, "Electricity changes shape during the pulse");
                visual.MovementPulse(-1);
                Check(bolts.All(line => line.enabled), "Leftward movement uses the same electrical sparks");
                visual.Tick(EnemyPresentation.MovementPulseSeconds, EnemyColor.Green, 0);
                Check(bolts.All(line => !line.enabled), "Electrical arcs disappear after the pulse");
                Check(green.Visuals.Body.color == resting, "Pulse returns to original color without changing geometry");
                visual.MovementPulse();
                visual.Clear();
                Check(bolts.All(line => !line.enabled), "Disabling or recoloring cannot leave electricity behind");
            });
            Debug.Log("Fleet tick checks passed: counts two and four, half-speed travel, frame timing, Green rotation, removals, pause, reset and electrical sparks.");
        }
        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            for (int col = 0; col < 5; col++)
                ProgressionChecks.Add(grid, col == 2 ? EnemyColor.Green : EnemyColor.Blue, col, 2);
            var green = grid.GetComponentsInChildren<GridEnemy>().First(enemy => enemy.Color == EnemyColor.Green);
            green.GetComponent<EnemyPresentation>().MovementPulse();
            GameplayChecks.Capture(540, 960, "green-electric-surge");
            GameplayChecks.Capture(960, 540, "green-electric-surge-landscape");
            green.GetComponent<EnemyPresentation>().Clear();
            var purple = ProgressionChecks.Add(grid, EnemyColor.Purple, 2, 1);
            purple.GetComponent<EnemyPresentation>().SummonFlash();
            GameplayChecks.Capture(540, 960, "purple-summon-flash");
            GameplayChecks.Capture(960, 540, "purple-summon-flash-landscape");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Fleet tick check failed: " + message); }
    }
}
