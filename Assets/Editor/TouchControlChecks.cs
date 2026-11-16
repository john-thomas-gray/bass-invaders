using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class TouchControlChecks
    {
        public static void RunInPlayMode()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            UnityEditor.SessionState.SetBool("CandyCruisers.TouchControlChecks", true);
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayMode;
            UnityEditor.EditorApplication.isPlaying = true;
        }
        [UnityEditor.InitializeOnLoadMethod]
        private static void RestoreCheck()
        {
            if (UnityEditor.SessionState.GetBool("CandyCruisers.TouchControlChecks", false))
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private static void OnPlayMode(UnityEditor.PlayModeStateChange state)
        {
            if (state != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayMode;
            UnityEditor.SessionState.SetBool("CandyCruisers.TouchControlChecks", false);
            try { Run(); UnityEditor.EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            foreach (var size in new[] { new Vector2(390, 844), new Vector2(844, 390),
                new Vector2(540, 960), new Vector2(1179, 2556) })
            {
                Vector2 center = new Vector2(size.x * .5f, size.y * .3f);
                float radius = PlayerMovement.TouchFireRangePixels(size.x);
                float spawnY = center.y + radius * 4;
                Check(Mathf.Abs(radius - size.x * PlayerMovement.TouchFireScreenWidthFraction) < .001f,
                    "Touch fire range uses the configured fraction of screen width");
                Check(PlayerMovement.IsTouchShootPosition(center + Vector2.right * radius, center, radius, spawnY), "Zone includes its side boundary");
                Check(PlayerMovement.IsTouchShootPosition(new Vector2(center.x + radius * .8f, 0), center, radius, spawnY),
                    "The shooting zone extends to the bottom of the screen");
                Check(PlayerMovement.IsTouchShootPosition(new Vector2(center.x, spawnY), center, radius, spawnY),
                    "Vertical distance does not block in-column shots below the spawn line");
                Check(!PlayerMovement.IsTouchShootPosition(new Vector2(center.x + radius + 1, 0), center, radius, spawnY),
                    "Below-player taps outside the column remain movement taps");
                Check(!PlayerMovement.IsTouchShootPosition(new Vector2(center.x, spawnY + 1), center, radius, spawnY),
                    "Only taps above the enemy spawn line are too high to shoot");
            }
            GameObject cameraRoot = null;
            if (Camera.main == null)
            {
                cameraRoot = new GameObject("Touch test camera", typeof(Camera));
                cameraRoot.tag = "MainCamera";
                cameraRoot.transform.position = Vector3.back * 10;
                var camera = cameraRoot.GetComponent<Camera>();
                camera.pixelRect = new Rect(0, 0, 1179, 2556);
                camera.orthographic = true;
                camera.orthographicSize = 6;
            }
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var clickedEnemy = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    player.RefreshColor();
                    int shots = 0;
                    player.ShotAccepted += () => shots++;
                    var camera = Camera.main;
                    Vector2 ScreenPoint(float x) => camera.WorldToScreenPoint(new Vector3(x, player.transform.position.y, 0));
                    player.transform.position = new Vector3(0, -4.6f, 0);
                    var original = player.transform.position;
                    Vector2 near = ScreenPoint(.1f);
                    player.BeginPointer(near, true);
                    player.TickPointerMovement(.1f);
                    player.EndPointer(near, false);
                    player.TickPointerMovement(1);
                    Check(shots == 1 && player.transform.position == original, "Near tap fires without nudging the player");
                    tongue.Cancel();
                    Vector2 below = new Vector2(ScreenPoint(.1f).x, 0);
                    player.BeginPointer(below, true);
                    player.EndPointer(below, false);
                    player.TickPointerMovement(1);
                    Check(shots == 2 && player.transform.position == original, "Tap beneath the character shoots without moving");
                    tongue.Cancel();
                    float spawnY = camera.WorldToScreenPoint(grid.transform.TransformPoint(grid.CellPosition(0, 0))).y;
                    Vector2 high = new Vector2(ScreenPoint(.1f).x, spawnY - 1);
                    player.BeginPointer(high, true);
                    player.EndPointer(high, false);
                    player.TickPointerMovement(1);
                    Check(shots == 3 && Mathf.Abs(player.transform.position.x - .1f) < .001f,
                        "Tall in-column tap aligns to the tap below the enemy spawn line");
                    tongue.Cancel();
                    Vector2 tooHigh = new Vector2(ScreenPoint(0).x, spawnY + 2);
                    player.BeginPointer(tooHigh, true);
                    player.EndPointer(tooHigh, false);
                    player.TickPointerMovement(1);
                    Check(shots == 3, "In-column tap above the enemy spawn line is too high to fire");
                    Vector2 enemyTap = camera.WorldToScreenPoint(clickedEnemy.HitBounds.center);
                    player.BeginPointer(enemyTap, true);
                    player.EndPointer(enemyTap, false);
                    player.TickPointerMovement(1);
                    Check(shots == 4 && Mathf.Abs(player.transform.position.x - clickedEnemy.HitBounds.center.x) < .001f,
                        "Tapping an enemy aligns the player with that enemy and fires");
                    tongue.Cancel();
                    Vector2 above = camera.WorldToScreenPoint(new Vector3(-1.25f, player.transform.position.y + 1.2f, 0));
                    player.BeginPointer(above, true);
                    player.EndPointer(above, false);
                    player.TickPointerMovement(1);
                    Check(shots == 5 && Mathf.Abs(player.transform.position.x + 1.25f) < .001f,
                        "Tapping above the player aligns with the tap and fires");
                    tongue.Cancel();
                    Vector2 far = ScreenPoint(2);
                    player.BeginPointer(far, true);
                    player.EndPointer(far, false);
                    player.TickPointerMovement(.02f);
                    Check(Mathf.Abs(PlayerMovement.Wrap(player.transform.position.x + 1.25f)) > .01f && shots == 5,
                        "Far tap moves after release without firing");
                    player.TickPointerMovement(1);
                    Check(Mathf.Abs(player.transform.position.x - 2) < .001f, "Tap movement reaches its destination");
                    var start = ScreenPoint(2);
                    player.BeginPointer(start, true);
                    player.UpdatePointer(ScreenPoint(.5f));
                    player.TickPointerMovement(1);
                    Check(Mathf.Abs(player.transform.position.x - .5f) < .001f, "Dragging from the shooting zone repositions the player");
                    player.EndPointer(ScreenPoint(.5f), false);
                    Check(shots == 5, "A drag never fires on release");
                    player.BeginPointer(ScreenPoint(-2), true);
                    player.EndPointer(ScreenPoint(-2), true);
                    original = player.transform.position;
                    player.TickPointerMovement(1);
                    Check(player.transform.position == original && shots == 5, "Canceled touches neither move nor fire");
                    Vector2 mouseAbove = camera.WorldToScreenPoint(new Vector3(-2, player.transform.position.y + 1, 0));
                    player.BeginPointer(mouseAbove);
                    player.EndPointer(mouseAbove, false);
                    Check(shots == 6 && Mathf.Abs(player.transform.position.x + 2) < .001f,
                        "Desktop mouse taps above the player align and fire");
                    tongue.Cancel();
                    player.BeginPointer(ScreenPoint(-2), true);
                    player.CancelPointer();
                    player.EndPointer(ScreenPoint(-2), false);
                    player.TickPointerMovement(1);
                    Check(shots == 6, "Canceled gestures cannot resume or fire later");
                });
                CheckMovementFireGuard();
            }
            finally { if (cameraRoot != null) UnityEngine.Object.DestroyImmediate(cameraRoot); }
            Debug.Log("Touch control checks passed: spawn-height shooting, enemy/above-player tap firing, distant movement, drag, cancellation and mouse compatibility.");
        }
        private static void CheckMovementFireGuard()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                player.RefreshColor();
                int shots = 0;
                player.ShotAccepted += () => shots++;
                var camera = Camera.main;
                Vector2 ScreenPoint(float x, float y) => camera.WorldToScreenPoint(new Vector3(x, y, 0));
                foreach (int frames in new[] { 30, 60, 120 })
                {
                    player.transform.position = new Vector3(0, -4.6f, 0);
                    Vector2 destination = ScreenPoint(1.5f, -4.6f);
                    player.BeginPointer(destination, true);
                    for (int frame = 0; frame < frames / 5; frame++) player.TickPointerMovement(1f / frames);
                    Check(Mathf.Abs(player.transform.position.x - 1.5f) < .001f, "Character reaches movement tap before release");
                    player.EndPointer(destination, false);
                    Check(shots == 0 && !tongue.Active,
                        "A movement tap cannot become a shooting tap after the character catches up");
                }

                player.transform.position = new Vector3(0, -4.6f, 0);
                Vector2 stationary = ScreenPoint(0, -4.6f);
                player.BeginPointer(stationary, true);
                player.Move(1, .05f);
                player.Move(-1, .05f);
                player.EndPointer(stationary, false);
                Check(shots == 0, "Moving away and back still suppresses the character-zone shot");
                player.BeginPointer(stationary, true);
                player.Move(0, .1f);
                player.EndPointer(stationary, false);
                Check(shots == 1, "A new stationary tap fires; zero movement does not trigger the guard");
                tongue.Cancel();

                Vector2 above = ScreenPoint(1.5f, -3.4f);
                player.BeginPointer(above, true);
                player.TickPointerMovement(.1f);
                Check(player.transform.position.x > 0, "Character moves before the playfield tap ends");
                player.EndPointer(above, false);
                Check(shots == 2 && Mathf.Abs(player.transform.position.x - 1.5f) < .001f,
                    "Playfield taps still align and fire after movement");
                tongue.Cancel();
                Vector2 onEnemy = camera.WorldToScreenPoint(enemy.HitBounds.center);
                player.BeginPointer(onEnemy, true);
                player.TickPointerMovement(.1f);
                player.EndPointer(onEnemy, false);
                Check(shots == 3, "Enemy taps still fire after movement");
                tongue.Cancel();

                Vector2 mouseTarget = ScreenPoint(2.5f, -4.6f);
                player.BeginPointer(mouseTarget);
                player.TickPointerMovement(1);
                player.EndPointer(mouseTarget, false);
                Check(shots == 3, "Mouse movement follows the same character-zone firing guard");
            });
        }

        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Touch control check failed: " + message); }
    }
}
