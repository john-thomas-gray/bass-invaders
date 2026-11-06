using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class TongueTipChecks
    {
        public static void Run()
        {
            CheckLifecycle();
            CheckRendering();
            Debug.Log("Tongue tip checks passed: visible bulb, endpoint tracking, normal/magic shots, restart, recovery, fatal hit and cleanup.");
        }

        private static void CheckLifecycle()
        {
            bool invincible = DeveloperOptions.PlayerInvincible;
            DeveloperOptions.PlayerInvincible = false;
            try
            {
                foreach (float step in new[] { 1f / 30, 1f / 60, 1f / 120, .5f })
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                    tongue.Draw();
                    var bulb = tongue.TipBulb;
                    player.ResetForRun();
                    Check(!bulb.enabled, "Starting a run cannot enable an idle tongue bulb");
                    foreach (bool magic in new[] { false, true })
                    {
                        Check(tongue.TryFire(EnemyColor.Red, 3, magic), "A fresh shot can fire");
                        tongue.Tick(.02f);
                        Check(bulb.enabled && bulb.transform.localPosition.y > tongue.Length,
                            "The bulb follows the extending endpoint");
                        tongue.Tick(0);
                        Check(bulb.enabled, "Pausing preserves an active bulb");
                        tongue.Tick(10);
                        Check(!tongue.Active && !bulb.enabled, "A completed return removes the bulb");
                        tongue.TryFire(EnemyColor.Red, 3, magic);
                        tongue.Tick(.02f);
                        Check(player.Hit(true), "A recoverable hit interrupts the shot");
                        Check(!tongue.Active && !bulb.enabled, "Damage cancels the bulb immediately");
                        for (float time = 0; time < 4; time += step)
                        {
                            player.TickSurvival(step);
                            Check(!bulb.enabled, "Respawn and invulnerability flashing cannot resurrect the canceled bulb");
                        }
                        Check(player.Alive && !player.Invulnerable, "Recovery still completes");
                    }
                    tongue.TryFire(EnemyColor.Red, 3);
                    tongue.Tick(.05f);
                    player.PrepareFatalDefeat();
                    Check(!tongue.Active && !bulb.enabled && CharacterVisuals.Ensure(player.gameObject).Body.enabled,
                        "The fatal fade keeps player artwork but not the canceled tongue");
                    player.ResetForRun();
                    Check(!bulb.enabled, "Restart cannot leave a colored circle at the previous endpoint");
                });
            }
            finally { DeveloperOptions.PlayerInvincible = invincible; }
        }

        public static void CheckRendering()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(390, 844), new Vector2Int(1206, 2622) })
            foreach (bool magic in new[] { false, true })
            foreach (bool returning in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var line = tongue.GetComponent<LineRenderer>();
                line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Tongue.mat");
                line.useWorldSpace = false;
                line.startWidth = .075f;
                line.endWidth = .11f;
                line.sortingOrder = 19;
                tongue.TryFire(EnemyColor.Red, 4, magic);
                if (returning)
                {
                    tongue.Tick(4f / (magic ? 56 : 14));
                    tongue.SetReturnColor(EnemyColor.Red, true);
                    tongue.Draw();
                }
                else tongue.Tick(magic ? .025f : .1f);
                foreach (var child in tongue.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
                var cameraObject = new GameObject("Tongue tip render camera", typeof(Camera));
                var view = cameraObject.GetComponent<Camera>();
                view.enabled = false;
                view.orthographic = true;
                view.orthographicSize = 6;
                view.transform.position = new Vector3(0, 0, -10);
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = Color.black;
                view.cullingMask = 1 << 31;
                var target = new RenderTexture(size.x, size.y, 24);
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    view.targetTexture = target;
                    view.aspect = (float)size.x / size.y;
                    view.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                    pixels.Apply();
                    var center = view.WorldToScreenPoint(tongue.TipBulb.transform.position);
                    int row = Mathf.RoundToInt(center.y);
                    Check(pixels.GetPixel(Mathf.RoundToInt(center.x), row).maxColorComponent > .85f,
                        "The bulb has an opaque center instead of a faint translucent disc");
                    int width = 0;
                    for (int x = Mathf.RoundToInt(center.x) - 60; x <= center.x + 60; x++)
                        if (pixels.GetPixel(x, row).maxColorComponent > .3f) width++;
                    float shaftPixels = line.endWidth * size.y / (2 * view.orthographicSize);
                    Directory.CreateDirectory("TestResults");
                    File.WriteAllBytes("TestResults/tongue-tip-" + size.x + "x" + size.y + (magic ? "-magic" : "") +
                        (returning ? "-returning-rainbow" : "") + ".png",
                        pixels.EncodeToPNG());
                    Check(width >= shaftPixels * 1.7f,
                        "Rendered bulb is visibly wider than the shaft at " + size + ", magic=" + magic + ": " + width);
                    if (returning)
                    {
                        var midpoint = view.WorldToScreenPoint(tongue.transform.position + Vector3.up * (tongue.Length * .5f));
                        var tint = pixels.GetPixel(Mathf.RoundToInt(midpoint.x), Mathf.RoundToInt(midpoint.y));
                        Check(tint.g > tint.r + .2f && tint.g > tint.b + .2f,
                            "The first return frame visibly renders the rainbow's green midpoint at " + size);
                    }
                    tongue.Cancel();
                    player.ResetForRun();
                    view.Render();
                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                    pixels.Apply();
                    Check(Array.TrueForAll(pixels.GetPixels32(), pixel => pixel.r == 0 && pixel.g == 0 && pixel.b == 0),
                        "No tongue pixels survive cancellation and restart");
                }
                finally
                {
                    view.targetTexture = null;
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(target);
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }
            });
        }

        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Tongue tip check failed: " + message); }
    }
}
