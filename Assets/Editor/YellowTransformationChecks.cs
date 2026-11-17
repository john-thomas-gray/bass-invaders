using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class YellowTransformationChecks
    {
        public static void Run()
        {
            WithFixture((grid, player, spawner) =>
            {
                var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                var red = Add(grid, EnemyColor.Red, 2, 0);
                Add(grid, EnemyColor.Red, 4, 3);
                grid.ClearMatchingChain(yellow.Id, EnemyColor.Yellow);
                var tongue = player.GetComponentInChildren<TongueShot>();
                player.transform.position = new Vector3(10, -4.6f, 0);
                Check(player.Fire() && tongue.IsMagic, "Earned magic can fire a miss");
                tongue.Tick(2, grid);
                player.RefreshColor(true);
                Check(!tongue.Active && !grid.IsColorCleared(EnemyColor.Yellow) && !grid.HasColorClearBar(EnemyColor.Yellow),
                    "Magic miss removes the Yellow bar and unlocks Yellow spawns");
                var green = Add(grid, EnemyColor.Green, 0, 0);
                grid.ClearMatchingChain(green.Id, EnemyColor.Green);
                player.Hit();
                player.TickSurvival(3);
                Check(player.Fire() && !tongue.IsMagic, "Next ordinary shot can miss");
                tongue.Tick(2, grid);
                player.RefreshColor(true);
                Check(!tongue.Active && !grid.IsColorCleared(EnemyColor.Green) && !grid.HasColorClearBar(EnemyColor.Green),
                    "Ordinary miss removes the Green bar and unlocks Green spawns");
                Check(grid.ClearMatchingChain(red.Id, EnemyColor.Red) == 1 && !grid.IsColorCleared(EnemyColor.Red),
                    "Partial clear does not wipe out its color");
                player.RefreshColor(true);
                Check(!grid.IsColorCleared(EnemyColor.Yellow) && grid.SeenColors().Count == 3,
                    "Breaking a streak releases spawn locks but preserves seen-color sizing");
            });

            WithFixture((grid, player, spawner) =>
            {
                var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                var red = Add(grid, EnemyColor.Red, 2, 0);
                var purple = Add(grid, EnemyColor.Purple, 4, 0);
                Add(grid, EnemyColor.Purple, 0, 3);
                Add(grid, EnemyColor.Green, 1, 3);
                grid.ClearMatchingChain(yellow.Id, EnemyColor.Yellow);
                var tongue = player.GetComponentInChildren<TongueShot>();
                player.transform.position = new Vector3(red.transform.position.x, -4.6f, 0);
                Check(player.Fire(), "Fire continuing streak shot");
                tongue.Tick(2, grid);
                Check(!tongue.Active && grid.HasColorClearBar(EnemyColor.Yellow) && grid.HasColorClearBar(EnemyColor.Red),
                    "A shot clearing another color keeps previous bars and adds its own");
                player.transform.position = new Vector3(purple.transform.position.x, -4.6f, 0);
                Check(player.Fire(), "Fire partial-clear shot");
                tongue.Tick(.085f, grid);
                Check(grid.Model.ColorCount(EnemyColor.Purple) == 1 && tongue.Active && grid.HasColorClearBar(EnemyColor.Red),
                    "Partial hit keeps bars until the shot has finished");
                tongue.Tick(2, grid);
                Check(!tongue.Active && !grid.HasColorClearBar(EnemyColor.Yellow) && !grid.HasColorClearBar(EnemyColor.Red) &&
                    !grid.IsColorCleared(EnemyColor.Yellow) && !grid.IsColorCleared(EnemyColor.Red),
                    "Partial-clear shot removes bars and their spawn locks");
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                    if (enemy.Color == EnemyColor.Purple) grid.ClearMatchingChain(enemy.Id, EnemyColor.Purple);
                Check(grid.HasColorClearBar(EnemyColor.Purple) && !grid.HasColorClearBar(EnemyColor.Red),
                    "A later color clear starts a fresh streak without restoring old bars");
                Check(player.Fire(), "Fire cancelable shot");
                player.CancelShot();
                Check(!grid.HasColorClearBar(EnemyColor.Purple) && !grid.IsColorCleared(EnemyColor.Purple),
                    "Canceled unsuccessful shot also ends the streak and releases its spawn locks");
            });

            foreach (bool summon in new[] { false, true })
                WithFixture((grid, player, spawner) =>
                {
                    var red = Add(grid, EnemyColor.Red, 0, 0);
                    var purple = Add(grid, EnemyColor.Purple, 2, 0);
                    int awards = 0;
                    grid.ColorCleared += color => { if (color == EnemyColor.Red) awards++; };
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    SpawnOverride.Enabled = true;
                    SpawnOverride.Types = 1 << (int)EnemyColor.Red;
                    Check(spawner.TrySummon(purple) == null, "Visible Red bar blocks Red summons even with override");
                    player.transform.position = new Vector3(10, -4.6f, 0);
                    Check(player.Fire(), "Fire streak-ending miss");
                    player.GetComponentInChildren<TongueShot>().Tick(2, grid);
                    Check(!grid.HasColorClearBar(EnemyColor.Red) && grid.SeenColors().Count == 2,
                        "Miss releases Red without erasing seen-color history");
                    if (summon)
                    {
                        var returned = spawner.TrySummon(purple);
                        Check(returned != null && returned.Color == EnemyColor.Red, "Purple can summon Red after its bar disappears");
                    }
                    else
                        Check(spawner.TryAdvance() && grid.Model.ColorCount(EnemyColor.Red) == RunProgress.StandardRowWidth,
                            "New row can contain Red after its bar disappears");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                        if (enemy != null && enemy.Color == EnemyColor.Red) grid.ClearMatchingChain(enemy.Id, EnemyColor.Red);
                    Check(awards == 2 && grid.HasColorClearBar(EnemyColor.Red) && grid.IsColorCleared(EnemyColor.Red) &&
                        player.MagicCharges == 1 && spawner.TrySummon(purple) == null,
                        "Clearing returning Red re-awards its bar and magic and blocks spawns again");
                });

            RunHostRules();
            Debug.Log("Yellow checks passed: separate host/yellow groups, both rainbow rules, coverage, conversion, promotion and special shield penalties.");
        }

        public static void RunHostRules()
        {
            var prefabs = new GameObject[6];
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor))) prefabs[(int)color] = Prefab(color);
            MimicMechanicContract.Run(prefabs, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
        }
        public static void RunMimicCombatRules() => RunHostRules();

        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int column, int row) => ProgressionChecks.Add(grid, color, column, row);

        private static void WithFixture(Action<EnemyGrid, PlayerMovement, EnemyRowSpawner> test)
        {
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            var root = new GameObject("Yellow fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var controller = new GameObject("Yellow test player", typeof(PlayerMovement), typeof(SpriteRenderer));
            var mouth = new GameObject("Yellow test tongue", typeof(LineRenderer), typeof(TongueShot));
            mouth.transform.SetParent(controller.transform, false);
            controller.transform.position = Vector3.down * 4.6f;
            try
            {
                SpawnOverride.Enabled = false;
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
                grid.ConfigureAbilities(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
                var body = controller.GetComponentInChildren<SpriteRenderer>();
                body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PlayerPlaceholder.png");
                var player = controller.GetComponent<PlayerMovement>();
                player.Configure(grid, mouth.GetComponent<TongueShot>(), body);
                test(grid, player, spawner);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller);
                UnityEngine.Object.DestroyImmediate(root);
                SpawnOverride.Types = types;
                SpawnOverride.Enabled = enabled;
                UnityEngine.Random.state = random;
            }
        }
        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Yellow transformation check failed: " + message); }
    }
}
