using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class GameplayChecks
    {
        [MenuItem("Candy Cruisers/Run Checks")]
        public static void Run()
        {
            using var songs = new SongSelectionCheckSettings();
            using var metronomes = new MetronomeCheckSettings();
            bool previousOverride = SpawnOverride.Enabled;
            SpawnOverride.Enabled = false;
            try { RunChecks(); }
            finally { SpawnOverride.Enabled = previousOverride; }
        }

        private static void RunChecks()
        {
            OccupiedBoundaryChecks.Run();

            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid");
            Require(grid != null && grid.GetComponent<EnemyGridMovement>() != null, "Movement attached");
            Require(grid.transform.childCount == 0 && grid.GetComponent<EnemyGrid>().Model.Count == 0, "Scene starts with an empty field");
            Directory.CreateDirectory("TestResults");
            Capture(540, 960, "empty-opening");
            var spawner = grid.GetComponent<EnemyRowSpawner>();
            // The baseline mechanics fixture has fixed expectations. Level-definition and
            // fleet-state checks below exercise custom catalogs without editing user assets.
            grid.GetComponent<GameSession>().LevelCatalog = null;
            Require(spawner.SpawnBatch(spawner.PlanOpening()) && grid.transform.childCount == 2 * RunProgress.StandardRowWidth, "Opening spawns two rows of five");
            foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>()) ability.Tick(1);
            foreach (Transform enemy in grid.transform)
                Require(enemy.GetComponentInChildren<SpriteRenderer>().sprite != null, "Enemy sprite assigned");
            Require(GameObject.Find("Star Background").GetComponentInChildren<SpriteRenderer>().sprite != null, "Background assigned");
            CoreGameplayChecks.Run();
            TouchControlChecks.Run();
            MatchingChecks.Run();
            DescentChecks.Run();
            RetreatChecks.Run();
            AbilityChecks.Run();
            AbilityBeatChecks.Run();
            WaveChecks.Run();
            ProgressionChecks.Run();
            SpawnOverrideChecks.Run();
            DeveloperOptionsChecks.Run();
            MetronomeOptionsChecks.Run();
            SongSelectionChecks.Run();
            CombatChecks.Run();
            ColorCycleChecks.Run();
            WaveTransitionChecks.Run();
            SpawnPresentationChecks.Run();
            RowCompositionChecks.Run();
            FleetStateChecks.Run();
            LevelProgressionChecks.Run();
            CombatTuningChecks.Run();
            FleetTickChecks.Run();
            PlayfieldFrameChecks.Run();
            YellowTransformationChecks.Run();
            SpecialEnemyChecks.Run();
            SpecialGreenTempoChecks.Run();
            GreenCloakChecks.Run();
            PurpleYellowSpecialChecks.Run();
            PresentationChecks.Run();
            MenuChecks.Run();
            SoundEffectsChecks.Run();
            MissileFlightSoundChecks.Run();
            MissileBoundaryChecks.Run();
            MusicKeyChecks.Run();
            TempoMapChecks.Run();
            EventPresentationChecks.Run();
            GameOverPresentationChecks.Run();
            ColorClearCelebrationChecks.Run();
            RewardWaveChecks.Run();
            PlayerColorAssistChecks.Run();
            ContactGameOverChecks.Run();
            PlayerDeathChecks.Run();
            PlayerLifeIconChecks.Run();
            SafeRespawnChecks.Run();
            FullPlayerRowChecks.Run();
            DeflectedTongueChecks.Run();
            MissilePersistenceChecks.Run();
            MagicMultiplierChecks.Run();
            AimedRedChecks.Run();
            OrangeChecks.Run();
            ComboLeaderboardBackgroundChecks.Run();
            ShotColorComboChecks.Run();
            PlaytestChecks.Run();
            Directory.CreateDirectory("TestResults");
            Capture(540, 960, "portrait");
            Capture(960, 540, "landscape");
            Capture(390, 844, "tall-phone");
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            player.Fire();
            var tongue = player.GetComponentInChildren<TongueShot>();
            tongue.Tick(0.4f);
            Capture(540, 960, "tongue-extended");
            tongue.Tick(10);
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                if (enemy.Color == EnemyColor.Blue)
                {
                    var ability = enemy.GetComponent<EnemyAbilities>();
                    ability.BeginSpawnEffect();
                    ability.Tick(.8f);
                    ability.Tick(ability.CooldownRemaining);
                    ability.Tick(.175f);
                }
            Capture(540, 960, "shield-powerup");
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                if (enemy.Color == EnemyColor.Blue) enemy.GetComponent<EnemyAbilities>().Tick(.175f);
            Capture(540, 960, "shield-active");
            tongue.TryFire(EnemyColor.Red, 10, true);
            tongue.Tick(.0625f);
            Capture(540, 960, "arcade-frame-magic");
            Capture(960, 540, "arcade-frame-landscape");
            tongue.Cancel();
            int colorIndex = 0;
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            {
                grid.GetComponent<EnemyGrid>().SetColor(enemy.Id, (EnemyColor)(colorIndex++ % 5));
                var ability = enemy.GetComponent<EnemyAbilities>();
                ability.Tick(0);
                ability.BeginSpawnEffect();
                ability.Tick(.25f);
            }
            Capture(540, 960, "spawn-effects");
            Capture(960, 540, "spawn-effects-landscape");
            Debug.Log("All gameplay checks passed: motion, frame timing, scene references, framing and rendering.");
        }

        internal static void Capture(int width, int height, string name)
        {
            var camera = Camera.main;
            var grid = GameObject.Find("Enemy Grid").transform;
            var originalPosition = grid.position;
            var texture = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                camera.GetComponent<GameplayFraming>().Refresh();
                var border = GameObject.Find("Playfield Border").GetComponent<LineRenderer>();
                border.GetComponent<PlayfieldFrame>().Refresh();
                foreach (var decoration in border.GetComponentsInChildren<LineRenderer>())
                    for (int point = 0; point < decoration.positionCount; point++)
                    {
                        var viewport = camera.WorldToViewportPoint(decoration.transform.TransformPoint(decoration.GetPosition(point)));
                        Require(viewport.x > .001f && viewport.x < .999f && viewport.y > .001f && viewport.y < .999f,
                            "Entire arcade frame fits " + name);
                    }
                Require(border.loop && border.positionCount == 4, "Closed playfield border");
                for (int i = 0; i < border.positionCount; i++)
                {
                    var point = border.transform.TransformPoint(border.GetPosition(i));
                    var viewport = camera.WorldToViewportPoint(point);
                    Require(Mathf.Abs(Mathf.Abs(point.x) - PlayerMovement.HalfWidth) < 0.001f,
                        "Border marks horizontal wrap edges");
                    Require(viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1,
                        "Border visible in " + name);
                }
                foreach (float offset in new[] { -0.75f, 0, 0.75f })
                {
                    grid.position = originalPosition + Vector3.right * offset;
                    foreach (var renderer in grid.GetComponentsInChildren<SpriteRenderer>())
                    {
                        Vector3 min = camera.WorldToViewportPoint(renderer.bounds.min);
                        Vector3 max = camera.WorldToViewportPoint(renderer.bounds.max);
                        Require(min.x >= 0 && min.y >= 0 && max.x <= 1 && max.y <= 1, "Entire fleet fits " + name);
                    }
                }
                grid.position = originalPosition;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                int lit = 0, colored = 0;
                foreach (Color32 pixel in pixels.GetPixels32())
                {
                    if (pixel.r + pixel.g + pixel.b > 30) lit++;
                    if (pixel.b > pixel.r * 1.5f && pixel.b > 60 || pixel.r > pixel.b * 1.5f && pixel.r > 100) colored++;
                }
                Require(lit > width * height / 100, "Nonblank render " + name);
                Require(colored > 100, "Colored sprites visible " + name);
                File.WriteAllBytes($"TestResults/{name}.png", pixels.EncodeToPNG());
            }
            finally
            {
                grid.position = originalPosition;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                camera.ResetAspect();
                camera.GetComponent<GameplayFraming>().Refresh();
            }
        }

        private static void Near(float actual, float expected, string message) =>
            Require(Mathf.Abs(actual - expected) < 0.0001f, message);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("Gameplay check failed: " + message);
        }
    }
}
