using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class EnemyBeatArtUpgrade
    {
        public const string SheetPath = "Assets/Resources/EnemyBeatSheet.png";
        public const string FramesPath = "Assets/Resources/EnemyBeatFrames.png";
        public const int FrameSize = 64, FramePadding = 2;
        private const int FrameStride = FrameSize + FramePadding * 2;

        [MenuItem("Candy Cruisers/Use Beat Enemy Sprites")]
        public static void Apply()
        {
            var widths = EnemyPalette.Colors.ToDictionary(color => color, color =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
                var body = prefab.GetComponent<CharacterVisuals>().Body;
                return body.sprite.bounds.size.x * body.transform.localScale.x;
            });
            BuildPaddedFrames();
            AssetDatabase.ImportAsset(FramesPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FramesPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            // Match the previous 224-pixel canvas at 256 pixels per world unit.
            // Ability shield sizing also reads the sprite's unscaled bounds.
            importer.spritePixelsPerUnit = 64f * 256 / 224;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            string[] names = { "Regular A", "Regular B", "Retained A", "Retained B", "Special A", "Special B" };
#pragma warning disable CS0618
            importer.spritesheet = names.Select((name, index) => new SpriteMetaData
            {
                name = name,
                rect = new Rect(index * FrameStride + FramePadding, FramePadding, FrameSize, FrameSize),
                alignment = (int)SpriteAlignment.Center,
                pivot = Vector2.one * .5f
            }).ToArray();
#pragma warning restore CS0618
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(FramesPath).OfType<Sprite>().ToArray();
            var regular = sprites.Single(sprite => sprite.name == "Regular A");
            var special = sprites.Single(sprite => sprite.name == "Special A");
            foreach (var color in EnemyPalette.Colors)
            {
                string path = "Assets/Prefabs/" + color + " Enemy.prefab";
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var body = prefab.GetComponent<CharacterVisuals>().Body;
                    body.sprite = color == EnemyColor.Orange ? special : regular;
                    body.transform.localScale = Vector3.one * (widths[color] / body.sprite.bounds.size.x);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            Object.FindFirstObjectByType<EnemyRowSpawner>().ConfigureSpecialSprite(special);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Beat enemy sprites assigned; earlier artwork retained.");
        }

        private static void BuildPaddedFrames()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var frames = new Texture2D(FrameStride * 6, FrameStride, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(SheetPath)) || source.width != FrameSize * 6 || source.height != FrameSize)
                    throw new System.InvalidOperationException("Expected the original six-frame enemy sheet.");
                var original = source.GetPixels32();
                var padded = new Color32[frames.width * frames.height];
                // Keep every source pixel, with transparent texels outside each sprite rectangle.
                // Sampling a cell boundary can no longer reach an adjacent enemy.
                for (int frame = 0; frame < 6; frame++)
                for (int y = 0; y < FrameSize; y++)
                for (int x = 0; x < FrameSize; x++)
                    padded[(y + FramePadding) * frames.width + frame * FrameStride + FramePadding + x] =
                        original[y * source.width + frame * FrameSize + x];
                frames.SetPixels32(padded);
                frames.Apply();
                File.WriteAllBytes(FramesPath, frames.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(frames);
            }
        }
    }
}
