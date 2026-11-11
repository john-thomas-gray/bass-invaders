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
                var hitBounds = green.HitBounds;
                var random = UnityEngine.Random.state;
                visual.MovementPulse();
                Check(green.Visuals.Body.color != resting, "Selected Green visibly flashes");
                var arrow = green.Visuals.Root.Find("Green movement arrow");
                var layers = arrow.GetComponentsInChildren<MeshRenderer>();
                Check(layers.Length == 4 && layers.All(layer => layer.enabled) &&
                    layers[0].GetComponent<MeshFilter>().sharedMesh == layers[2].GetComponent<MeshFilter>().sharedMesh &&
                    layers[1].GetComponent<MeshFilter>().sharedMesh == layers[3].GetComponent<MeshFilter>().sharedMesh,
                    "Dance arrow shares silhouette and gradient meshes across its black, green and white layers");
                float enemySize = Mathf.Max(green.Visuals.Body.bounds.size.x, green.Visuals.Body.bounds.size.y);
                Check(Mathf.Abs(layers[0].bounds.size.x / enemySize - .3f) < .001f &&
                    arrow.TransformDirection(Vector3.right).x > .99f,
                    "Regular arrow begins at 30 percent of the enemy size and points right");
                Check(Vector3.Distance(arrow.position, green.Visuals.Body.bounds.center) < .0001f,
                    "Regular arrow starts at the enemy center");
                Check(layers[1].GetComponent<MeshFilter>().sharedMesh.colors.All(color => color.g > color.r && color.g > color.b),
                    "Both ends of the arrow gradient are green");
                Check(!green.GetComponentsInChildren<LineRenderer>().Any(line => line.name.StartsWith("Green surge")),
                    "The movement arrow replaces every electrical branch");
                Vector3 point = arrow.position, scale = arrow.lossyScale;
                visual.Tick(.06f, EnemyColor.Green, 0);
                Check(arrow.position == point && arrow.lossyScale.x > scale.x,
                    "The arrow grows while remaining centered on the enemy");
                point = arrow.position; scale = arrow.lossyScale;
                visual.Tick(0, EnemyColor.Green, 0);
                Check(arrow.position == point && arrow.lossyScale == scale, "Pause holds the arrow pose");
                visual.MovementPulse(-1);
                Check(layers.All(layer => layer.enabled) && arrow.TransformDirection(Vector3.right).x < -.99f,
                    "Leftward movement flips the entire arrow silhouette");
                var tint = new MaterialPropertyBlock();
                visual.Tick(visual.MovementPulseDuration * (.8f / 1.45f), EnemyColor.Green, 0);
                layers[2].GetPropertyBlock(tint);
                Check(Mathf.Abs(layers[0].bounds.size.x / enemySize - 1.1f) < .001f &&
                    Mathf.Abs(tint.GetColor("_Color").a - 1) < .001f,
                    "Regular arrow stays fully opaque through 110 percent size");
                visual.Tick(visual.MovementPulseDuration * (.1625f / 1.45f), EnemyColor.Green, 0);
                layers[2].GetPropertyBlock(tint);
                Check(Mathf.Abs(layers[0].bounds.size.x / enemySize - 1.2625f) < .001f &&
                    Mathf.Abs(tint.GetColor("_Color").a - .84375f) < .001f,
                    "Regular arrow eases out slowly at the start of its fade");
                visual.Tick(visual.MovementPulseDuration * (.1625f / 1.45f), EnemyColor.Green, 0);
                layers[2].GetPropertyBlock(tint);
                Check(Mathf.Abs(layers[0].bounds.size.x / enemySize - 1.425f) < .001f &&
                    Mathf.Abs(tint.GetColor("_Color").a - .5f) < .001f,
                    "Regular arrow is half transparent at 142.5 percent size");
                visual.Tick(visual.MovementPulseDuration, EnemyColor.Green, 0);
                layers[2].GetPropertyBlock(tint);
                Check(Mathf.Abs(layers[0].bounds.size.x / enemySize - 1.75f) < .001f &&
                    tint.GetColor("_Color").a < .001f && layers.All(layer => !layer.enabled),
                    "Regular arrow finishes at 175 percent size with zero opacity");
                Check(green.Visuals.Body.color == resting, "Pulse returns to original color without changing geometry");
                visual.MovementPulse();
                visual.Clear();
                Check(layers.All(layer => !layer.enabled), "Clearing presentation cannot leave the arrow behind");
                visual.MovementPulse();
                visual.Tick(0, EnemyColor.Blue, 0);
                Check(layers.All(layer => !layer.enabled), "Recoloring hides the movement arrow");

                Vector3 referencePosition = default, referenceScale = default;
                foreach (int rate in new[] { 30, 60, 120 })
                {
                    visual.MovementPulse();
                    float elapsed = 0;
                    while (elapsed < .15f - .000001f)
                    {
                        float step = Mathf.Min(1f / rate, .15f - elapsed);
                        visual.Tick(step, EnemyColor.Green, 0);
                        elapsed += step;
                    }
                    if (rate == 30) { referencePosition = arrow.position; referenceScale = arrow.lossyScale; }
                    else Check(Vector3.Distance(referencePosition, arrow.position) < .0001f &&
                        Vector3.Distance(referenceScale, arrow.lossyScale) < .0001f,
                        "Arrow motion agrees at 30, 60 and 120 frames per second");
                }
                grid.GetComponent<EnemyRowSpawner>().enabled = false;
                var movement = grid.GetComponent<EnemyGridMovement>();
                visual.MovementPulse();
                grid.OccupiedHorizontalBounds(out _, out float right);
                movement.AdvanceDistance(PlayerMovement.HalfWidth - right);
                visual.Tick(0, EnemyColor.Green, 0);
                Check(movement.Direction == -1 && arrow.TransformDirection(Vector3.right).x < -.99f &&
                    Vector3.Distance(arrow.position, green.Visuals.Body.bounds.center) < .0001f,
                    "A right-edge reversal turns the centered arrow left");
                grid.OccupiedHorizontalBounds(out float left, out _);
                movement.AdvanceDistance(left + PlayerMovement.HalfWidth);
                visual.Tick(0, EnemyColor.Green, 0);
                Check(movement.Direction == 1 && arrow.TransformDirection(Vector3.right).x > .99f &&
                    Vector3.Distance(arrow.position, green.Visuals.Body.bounds.center) < .0001f,
                    "A left-edge reversal turns the centered arrow right");
                Check(green.HitBounds.size == hitBounds.size && UnityEngine.Random.state.Equals(random),
                    "Arrow animation changes neither collision size nor gameplay randomness");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                var visual = green.GetComponent<EnemyPresentation>();
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                foreach (float tempo in new[] { 75f, 115.03f, 180f })
                foreach (int frames in new[] { 30, 60, 120 })
                {
                    music.PreviewTempo = tempo;
                    float beat = 60f / tempo;
                    float start = beat * 2.25f, end = beat * 3;
                    music.PreviewTime = start;
                    visual.MovementPulse();
                    var layers = green.GetComponentsInChildren<MeshRenderer>();
                    Check(Mathf.Abs(visual.MovementPulseDuration - beat * .75f) < .0001f,
                        "A late pulse uses only the time until the next beat");
                    var scale = layers[0].transform.parent.lossyScale;
                    visual.Tick(100, EnemyColor.Green, 0);
                    Check(layers[0].transform.parent.lossyScale == scale,
                        "Paused soundtrack holds arrow growth despite a large local delta");
                    for (int frame = 1; frame < frames; frame++)
                    {
                        music.PreviewTime = Mathf.Lerp(start, end, (float)frame / frames);
                        visual.Tick(1f / frames, EnemyColor.Green, 0);
                        Check(layers.All(layer => layer.enabled), "Arrow remains active before the next beat");
                    }
                    music.PreviewTime = end;
                    visual.Tick(0, EnemyColor.Green, 0);
                    var tint = new MaterialPropertyBlock();
                    layers[0].GetPropertyBlock(tint);
                    float size = Mathf.Max(green.Visuals.Body.bounds.size.x, green.Visuals.Body.bounds.size.y);
                    Check(layers.All(layer => !layer.enabled) && tint.GetColor("_Color").a == 0 &&
                        Mathf.Abs(layers[0].bounds.size.x / size - 1.75f) < .001f,
                        "Growth and fade complete together on the next beat at every tempo and frame rate");
                }
                music.PreviewTime = .2f;
                visual.MovementPulse();
                music.PreviewTime = null;
                visual.Tick(visual.MovementPulseDuration, EnemyColor.Green, 0);
                Check(green.GetComponentsInChildren<MeshRenderer>().All(layer => !layer.enabled),
                    "Clock changes cannot strand an arrow");
            });
            Debug.Log("Fleet tick checks passed: counts two and four, half-speed travel, Green rotation, removals, pause, directional rhythm arrows, reversals, fade, frame timing and edge framing.");
        }
        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            for (int col = 0; col < 5; col++)
                ProgressionChecks.Add(grid, col == 2 ? EnemyColor.Green : EnemyColor.Blue, col, 2);
            var green = grid.GetComponentsInChildren<GridEnemy>().First(enemy => enemy.Color == EnemyColor.Green);
            foreach (int direction in new[] { 1, -1 })
            {
                var visual = green.GetComponent<EnemyPresentation>();
                visual.MovementPulse(direction);
                visual.Tick(.055f, EnemyColor.Green, 0);
                string side = direction > 0 ? "right" : "left";
                GameplayChecks.Capture(540, 960, "green-arrow-" + side + "-portrait");
                GameplayChecks.Capture(960, 540, "green-arrow-" + side + "-landscape");
                GameplayChecks.Capture(390, 844, "green-arrow-" + side + "-phone");
            }
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
