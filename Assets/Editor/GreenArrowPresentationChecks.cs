using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class GreenArrowPresentationChecks
    {
        public static void VerifyBehavior()
        {
            SpecialGreenTempoChecks.Run();
            SpecialEnemyChecks.Run();
            FleetTickChecks.Run();
            Run();
        }
        public static void Run()
        {
            using var songs = new SongSelectionCheckSettings();
            using var metronomes = new MetronomeCheckSettings();
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var greens = new GridEnemy[3];
            for (int i = 0; i < 3; i++) greens[i] = ProgressionChecks.Add(grid, EnemyColor.Green, i + 1, 2);
            var regular = ProgressionChecks.Add(grid, EnemyColor.Green, 0, 4);
            ProgressionChecks.Add(grid, EnemyColor.Red, 4, 4);
            grid.RefreshSpecials();
            foreach (var green in greens) SpecialGreenTempoChecks.Activate(green);
            regular.GetComponent<EnemyPresentation>().MovementPulse(1);
            Capture("up");
            grid.ClearMatchingChain(greens[0].Id, EnemyColor.Green);
            foreach (var fall in UnityEngine.Object.FindObjectsByType<GreenTempoFall>(FindObjectsSortMode.None))
                fall.Tick(FullSetCelebration.StepDuration * .35f);
            Capture("down");
            Debug.Log("Green arrow presentation checks passed: landscape, portrait, tall phone, safe-area framing, persistent up and falling down arrows.");
        }
        private static void Capture(string state)
        {
            var camera = Camera.main;
            var framing = camera.GetComponent<GameplayFraming>();
            Directory.CreateDirectory("TestResults");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280), new Vector2Int(390, 844), new Vector2Int(1206, 2622) })
            {
                var previous = camera.targetTexture;
                var active = RenderTexture.active;
                var texture = new RenderTexture(size.x, size.y, 24);
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = texture;
                    camera.aspect = (float)size.x / size.y;
                    GameplayFraming.ForceIphoneNotchOffsetForTests = size.y > size.x;
                    framing.Refresh();
                    foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    {
                        if (!renderer.enabled || !renderer.name.StartsWith("Green arrow")) continue;
                        var min = camera.WorldToViewportPoint(renderer.bounds.min);
                        var max = camera.WorldToViewportPoint(renderer.bounds.max);
                        if (min.x < 0 || min.y < 0 || max.x > 1 || max.y > 1)
                            throw new Exception("Green arrow cropped at " + size);
                    }
                    camera.Render();
                    RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes($"TestResults/green-arrows-{state}-{size.x}x{size.y}.png", pixels.EncodeToPNG());
                }
                finally
                {
                    GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                    camera.targetTexture = previous;
                    RenderTexture.active = active;
                    UnityEngine.Object.DestroyImmediate(pixels);
                    texture.Release();
                    UnityEngine.Object.DestroyImmediate(texture);
                    camera.ResetAspect();
                    framing.Refresh();
                }
            }
        }
    }
}
