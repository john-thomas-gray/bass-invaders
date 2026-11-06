using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class EnemyBeatSpriteChecks
    {
        public static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(EnemyBeatArtUpgrade.FramesPath)
            .OfType<Sprite>().Single(sprite => sprite.name == name);

        public static void Run()
        {
            var regularA = Sprite("Regular A");
            var regularB = Sprite("Regular B");
            var specialA = Sprite("Special A");
            var specialB = Sprite("Special B");
            var importer = (TextureImporter)AssetImporter.GetAtPath(EnemyBeatArtUpgrade.FramesPath);
            Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed,
                "Pixel art uses point filtering without mipmaps or compression");
            Check(regularA.rect == new Rect(2, 2, 64, 64) && regularB.rect == new Rect(70, 2, 64, 64) &&
                specialA.rect == new Rect(274, 2, 64, 64) && specialB.rect == new Rect(342, 2, 64, 64),
                "Regular uses the leftmost pair and special uses the rightmost pair");
            CheckIsolatedPixels();
            foreach (string path in new[] { "space-invader-normal", "space-invader-squid", "space-invader-sentinel" })
                Check(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/" + path + ".png") != null,
                    "Earlier artwork remains available: " + path);
            foreach (var color in EnemyPalette.Colors)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
                Check(prefab.GetComponent<CharacterVisuals>().Body.sprite ==
                    (color == EnemyColor.Orange ? specialA : regularA), "Saved appearance: " + color);
            }
            foreach (int rate in new[] { 30, 60, 120 })
            foreach (float tempo in new[] { 80f, 120f, 173f })
            {
                for (int step = 0; step < rate * 4; step++)
                {
                    float beat = step / (float)rate * tempo / 60;
                    Check(EnemyBeatSprites.FrameAt(regularB, beat) ==
                        ((Mathf.FloorToInt(beat) & 1) == 0 ? regularA : regularB), "Beat frame independent of frame rate");
                }
            }
            Check(EnemyBeatSprites.FrameAt(regularA, .999f) == regularA &&
                EnemyBeatSprites.FrameAt(regularA, 1) == regularB &&
                EnemyBeatSprites.FrameAt(regularB, 2) == regularA &&
                EnemyBeatSprites.FrameAt(specialA, 3.25f) == specialB &&
                EnemyBeatSprites.FrameAt(specialB, 8) == specialA,
                "Frames change at each whole beat and recover after skipped beats or seeks");
            Check(EnemyBeatSprites.FrameAt(EnemyPlaceholderArt.Triangle, 1) == EnemyPlaceholderArt.Triangle,
                "Unrelated and replacement artwork stays intact");

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                spawner.ConfigureSpecialSprite(specialA);
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 3, 0);
                var bounds = red.HitBounds;
                var position = red.transform.position;
                var tint = red.Visuals.Body.color;
                red.Visuals.RefreshBeat(.9f, false);
                red.Visuals.RefreshBeat(1.01f, false);
                Check(red.Visuals.Body.sprite == regularB && red.HitBounds == bounds &&
                    red.transform.position == position && red.Visuals.Body.color == tint,
                    "Sprite animation preserves gameplay geometry and ability tint");
                Check(yellow.GetComponent<EnemyAbilities>().BeginImitation(red), "Yellow starts copying an animated target");
                yellow.Visuals.RefreshBeat(1.5f, false);
                var overlay = yellow.GetComponentsInChildren<SpriteRenderer>().Single(renderer => renderer.name == "Imitation overlay");
                Check(yellow.Visuals.Body.sprite == regularB && overlay.sprite == regularB,
                    "Yellow's copied body and transformation overlay share the beat frame");
                yellow.Visuals.RefreshBeat(2, false);
                Check(yellow.Visuals.Body.sprite == regularA && overlay.sprite == regularA,
                    "Copied artwork continues animating");
                ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                grid.RefreshSpecials();
                red.Visuals.RefreshBeat(3, false);
                Check(red.IsSpecial && red.Visuals.Body.sprite == specialB && red.HitBounds == bounds,
                    "Promotion switches to the special design on the same global beat");
                var late = ProgressionChecks.Add(grid, EnemyColor.Blue, 5, 2);
                late.Visuals.RefreshBeat(3.1f, false);
                Check(late.Visuals.Body.sprite == regularB, "New arrivals join the current global frame");
            });
            Debug.Log("Enemy beat sprite checks passed: exact source pixels, transparent sampling borders, retained artwork, saved prefabs, beat boundaries, frame rates, stable hitboxes, promotion and Yellow imitation.");
        }

        private static void CheckIsolatedPixels()
        {
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var padded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                original.LoadImage(File.ReadAllBytes(EnemyBeatArtUpgrade.SheetPath));
                padded.LoadImage(File.ReadAllBytes(EnemyBeatArtUpgrade.FramesPath));
                string[] names = { "Regular A", "Regular B", "Retained A", "Retained B", "Special A", "Special B" };
                for (int frame = 0; frame < names.Length; frame++)
                {
                    var rect = Sprite(names[frame]).rect;
                    Check(rect.xMin >= 2 && rect.yMin >= 2 && rect.xMax <= padded.width - 2 && rect.yMax <= padded.height - 2,
                        "Every sprite has a sampling margin, including texture edges");
                    for (int y = -2; y < 66; y++)
                    for (int x = -2; x < 66; x++)
                    {
                        var actual = (Color32)padded.GetPixel((int)rect.x + x, (int)rect.y + y);
                        if (x < 0 || x >= 64 || y < 0 || y >= 64)
                            Check(actual.a == 0, "No neighboring pixels in the transparent margin: " + names[frame]);
                        else
                            Check(actual.Equals((Color32)original.GetPixel(frame * 64 + x, y)),
                                "Extracted frame preserves every original pixel: " + names[frame]);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(padded);
            }
        }

        public static void RunAndCapture()
        {
            Run();
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            grid.GetComponent<GameSession>().LevelCatalog = null;
            var spawner = grid.GetComponent<EnemyRowSpawner>();
            spawner.SpawnBatch(new[] { EnemyColor.Red, EnemyColor.Red, EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green,
                EnemyColor.Yellow, EnemyColor.Purple, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Orange });
            grid.RefreshSpecials();
            foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.Tick(.8f);
            for (int frame = 0; frame < 2; frame++)
            {
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>()) enemy.Visuals.RefreshBeat(frame, false);
                GameplayChecks.Capture(960, 540, "enemy-beat-desktop-" + frame);
                GameplayFraming.ForceIphoneNotchOffsetForTests = true;
                try { GameplayChecks.Capture(390, 844, "enemy-beat-phone-" + frame); }
                finally { GameplayFraming.ForceIphoneNotchOffsetForTests = false; }
            }
        }

        public static void BuildPhoneSimulator()
        {
            var previous = PlayerSettings.iOS.sdkVersion;
            var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
            try
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                PhoneBuildCommand.BuildIos();
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previous;
                PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Enemy beat sprite check failed: " + message); }
    }
}
