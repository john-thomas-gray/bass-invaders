using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class GameplayUpgrade
    {
        private const string EnemySpritePath = "Assets/Art/space-invader-normal.png";
        private const string SpecialEnemySpritePath = "Assets/Art/space-invader-sentinel.png";
        private const string MissileSpritePath = "Assets/Art/missile.png";

        public static void Apply()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var root = GameObject.Find("Enemy Grid");
            var grid = root.GetComponent<EnemyGrid>();
            if (grid == null) grid = root.AddComponent<EnemyGrid>();
            // The opening fleet is now created by GameSession, not stored in the scene.
            foreach (var enemy in root.GetComponentsInChildren<GridEnemy>(true))
                Object.DestroyImmediate(enemy.gameObject);
            if (GameObject.Find("Player") == null) CreatePlayer(grid);
            var spawner = root.GetComponent<EnemyRowSpawner>();
            if (spawner == null) spawner = root.AddComponent<EnemyRowSpawner>();
            spawner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blue Enemy.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Enemy.prefab"));
            spawner.ConfigureNewTypes(CreateType(EnemyColor.Green),
                CreateType(EnemyColor.Purple), CreateType(EnemyColor.Yellow),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Orange Enemy.prefab"));
            spawner.ConfigureSpecialSprite(ImportSprite(SpecialEnemySpritePath));
            grid.ConfigureAbilities(CreateMissile(), CreateShieldSprite());
            var session = root.GetComponent<GameSession>();
            if (session == null) session = root.AddComponent<GameSession>();
            session.Configure(GameObject.Find("Player").GetComponent<PlayerMovement>());
            CreatePlayfieldBorder();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreateType(EnemyColor color)
        {
            string path = "Assets/Prefabs/" + color + " Enemy.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var sprite = ImportSprite(EnemySpritePath);
            var instance = new GameObject(color + " Enemy", typeof(SpriteRenderer));
            var renderer = instance.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = EnemyPalette.Get(color);
            renderer.sortingOrder = 10;
            instance.transform.localScale = Vector3.one * (.58f / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static Sprite ImportSprite(string spritePath)
        {
            AssetDatabase.ImportAsset(spritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }

        private static void CreatePlayfieldBorder()
        {
            var existing = GameObject.Find("Playfield Border");
            var border = existing != null ? existing.GetComponent<LineRenderer>() :
                new GameObject("Playfield Border", typeof(LineRenderer)).GetComponent<LineRenderer>();
            border.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Tongue.mat");
            border.useWorldSpace = false;
            border.loop = true;
            border.positionCount = 4;
            float edge = PlayerMovement.HalfWidth;
            border.SetPositions(new[] {
                new Vector3(-edge, -5.5f, 0), new Vector3(-edge, 5.5f, 0),
                new Vector3(edge, 5.5f, 0), new Vector3(edge, -5.5f, 0)
            });
            border.startWidth = border.endWidth = 0.03f;
            border.startColor = border.endColor = new Color(0.65f, 0.78f, 0.84f, 0.45f);
            border.sortingOrder = -50;
            border.numCornerVertices = 2;
            var frame = border.GetComponent<PlayfieldFrame>();
            if (frame == null) frame = border.gameObject.AddComponent<PlayfieldFrame>();
            frame.Configure(GameObject.Find("Player").GetComponent<PlayerMovement>());
        }

        private static GameObject CreateMissile()
        {
            const string path = "Assets/Prefabs/Red Missile.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var instance = new GameObject("Red Missile", typeof(SpriteRenderer), typeof(EnemyMissile));
            var renderer = instance.GetComponent<SpriteRenderer>();
            ImportMissileSprite();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MissileSpritePath);
            renderer.color = new Color(1f, 0.12f, 0.08f);
            renderer.flipY = true;
            renderer.sortingOrder = 25;
            instance.transform.localScale = new Vector3(0.5f, 0.42f, 1);
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void ImportMissileSprite()
        {
            AssetDatabase.ImportAsset(MissileSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(MissileSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 484;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        public static void RebuildShieldSprite() => CreateShieldSprite();

        private static Sprite CreateShieldSprite()
        {
            const string path = "Assets/Art/ShieldArc.png";
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64, 64));
                // Preserve the solid outer rim; extend inward over the enemy with a soft fade.
                float ring = Mathf.Clamp01(63.5f - radius) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(35, 60, radius));
                texture.SetPixel(x, y, new Color(1, 1, 1, ring));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 128;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreatePlayer(EnemyGrid grid)
        {
            // Simple replaceable sprite geometry for the control prototype.
            const string spritePath = "Assets/Art/PlayerPlaceholder.png";
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(32 - radius)));
            }
            texture.Apply();
            File.WriteAllBytes(spritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(spritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

            var player = new GameObject("Player", typeof(PlayerMovement));
            player.transform.position = new Vector3(0, -4.6f, 0);
            var body = Part(player.transform, "Body", sprite, Vector3.zero, new Vector3(0.64f, 0.46f, 1), EnemyPalette.Get(EnemyColor.Blue), 20);
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                Part(player.transform, "Eye", sprite, new Vector3(x, 0.16f, 0), Vector3.one * 0.21f, Color.white, 21);
                Part(player.transform, "Pupil", sprite, new Vector3(x, 0.20f, 0), Vector3.one * 0.085f, new Color(0.06f, 0.08f, 0.1f), 22);
            }
            var mouth = new GameObject("Tongue", typeof(LineRenderer), typeof(TongueShot));
            mouth.transform.SetParent(player.transform, false);
            mouth.transform.localPosition = new Vector3(0, 0.2f, 0);
            var line = mouth.GetComponent<LineRenderer>();
            var material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, "Assets/Art/Tongue.mat");
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = 0.075f;
            line.endWidth = 0.11f;
            line.numCapVertices = 6;
            line.sortingOrder = 19;
            line.enabled = false;
            player.GetComponent<PlayerMovement>().Configure(grid, mouth.GetComponent<TongueShot>(), body);
        }

        private static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Vector3 position, Vector3 scale, Color color, int order)
        {
            var part = new GameObject(name, typeof(SpriteRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
