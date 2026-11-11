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

            WithFixture((grid, player, spawner) =>
            {
                var first = Add(grid, EnemyColor.Yellow, 0, 0);
                player.RefreshColor();
                var red = Add(grid, EnemyColor.Red, 1, 0);
                var second = Add(grid, EnemyColor.Yellow, 2, 0);
                first.GetComponent<EnemyAbilities>().BeginImitation(red);
                Check(grid.Model.SelectablePlayerColors().Contains(EnemyColor.Yellow),
                    "Yellow remains selectable while any Yellow is not transforming");
                second.GetComponent<EnemyAbilities>().BeginImitation(red);
                player.RefreshColor();
                Check(player.ReadyColor == EnemyColor.Red, "Ready color switches when every Yellow becomes unselectable");
                Check(!grid.Model.SelectablePlayerColors().Contains(EnemyColor.Yellow) &&
                    grid.Model.AvailableColors().Contains(EnemyColor.Yellow), "All transforming Yellows remain occupied but cannot be selected");
                for (int i = 0; i < 100; i++)
                {
                    player.RefreshColor(true);
                    Check(player.ReadyColor == EnemyColor.Red, "Rerolls never choose an all-transforming Yellow");
                }
                grid.Model.CancelImitation(first.Id);
                Check(grid.Model.SelectablePlayerColors().Contains(EnemyColor.Yellow), "Canceled transformation restores Yellow eligibility");
                grid.Model.BeginImitation(first.Id, red.Id);
                Add(grid, EnemyColor.Yellow, 4, 3);
                Check(grid.Model.SelectablePlayerColors().Contains(EnemyColor.Yellow), "New untransformed Yellow restores eligibility");
            });

            foreach (bool hitYellow in new[] { true, false })
                WithFixture((grid, player, spawner) =>
                {
                    var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                    player.RefreshColor();
                    var blue = Add(grid, EnemyColor.Blue, 1, 0);
                    Add(grid, EnemyColor.Blue, 2, 0);
                    Add(grid, EnemyColor.Red, 4, 3);
                    int scoreEvents = 0, defeats = 0, yellowAwards = 0;
                    grid.MatchCleared += (count, all, weight) => { scoreEvents++; defeats += count; };
                    grid.ColorCleared += color => { if (color == EnemyColor.Yellow) yellowAwards++; };
                    var ability = yellow.GetComponent<EnemyAbilities>();
                    Check(ability.BeginImitation(blue), "Begin attachment");
                    ability.Tick(1);
                    Check(yellow.Color == EnemyColor.Yellow && grid.Model.ColorCount(EnemyColor.Yellow) == 1 && !ability.ShieldActive,
                        "Transforming Yellow remains Yellow and has no destination shield");
                    var target = hitYellow ? yellow : blue;
                    var shotColor = hitYellow ? EnemyColor.Yellow : EnemyColor.Blue;
                    Check(grid.FindMatchingHit(new Vector3(target.transform.position.x, -4, 0), 0, 10, shotColor, .055f,
                        out int id, out _) && id == target.Id, "Tongue can hit either side of the attachment");
                    grid.ResolveTongueHit(id, shotColor);
                    Check(grid.Model.Count == 1 && grid.Model.ColorCount(EnemyColor.Blue) == 0 && grid.Model.ColorCount(EnemyColor.Yellow) == 0,
                        "Either hit clears Yellow and the full attached group");
                    Check(grid.IsColorCleared(EnemyColor.Yellow) && grid.IsColorCleared(EnemyColor.Blue) && yellowAwards == 1,
                        "Both destroyed colors earn their bars exactly once");
                    Check(player.Alive && player.MagicCharges == 2 && scoreEvents == 1 && defeats == 3,
                        "Linked kill scores once per enemy and awards magic without a penalty");
                    Check(grid.ClearMatchingChain(id, shotColor) == 0 && yellowAwards == 1, "Repeated hits cannot repeat rewards");
                });

            WithFixture((grid, player, spawner) =>
            {
                var first = Add(grid, EnemyColor.Yellow, 0, 0);
                var red = Add(grid, EnemyColor.Red, 1, 0);
                var second = Add(grid, EnemyColor.Yellow, 2, 0);
                Add(grid, EnemyColor.Blue, 4, 3);
                first.GetComponent<EnemyAbilities>().BeginImitation(red);
                second.GetComponent<EnemyAbilities>().BeginImitation(red);
                Check(grid.ClearMatchingChain(first.Id, EnemyColor.Blue) == 0, "Mismatched shot does not follow tendrils");
                Check(grid.ClearMatchingChain(red.Id, EnemyColor.Red) == 3 && grid.IsColorCleared(EnemyColor.Yellow),
                    "Clearing a group kills all attached Yellows without double counting");
            });

            foreach (bool playerIsYellow in new[] { true, false })
                WithFixture((grid, player, spawner) =>
                {
                    var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                    if (playerIsYellow) player.RefreshColor();
                    var blue = Add(grid, EnemyColor.Blue, 1, 0);
                    if (!playerIsYellow)
                    {
                        grid.SetColor(yellow.Id, EnemyColor.Blue);
                        player.RefreshColor();
                        grid.SetColor(yellow.Id, EnemyColor.Yellow);
                    }
                    int penalties = 0, clears = 0;
                    grid.LastYellowTransformed += () => penalties++;
                    grid.ColorCleared += color => clears++;
                    var ability = yellow.GetComponent<EnemyAbilities>();
                    Check(player.Fire(), "Player fires before Yellow becomes unselectable");
                    Check(ability.BeginImitation(blue), "Start last Yellow conversion");
                    Check(player.ReadyColor == (playerIsYellow ? EnemyColor.Yellow : EnemyColor.Blue),
                        "An active shot preserves its color while Yellow transforms");
                    ability.Suspended = true;
                    ability.Tick(10);
                    Check(yellow.Color == EnemyColor.Yellow, "Paused transformation cannot finish");
                    ability.Suspended = false;
                    ability.Tick(EnemyAbilities.DefaultImitationSeconds - .01f);
                    Check(player.Alive && yellow.Color == EnemyColor.Yellow, "No penalty before completion");
                    ability.Tick(.01f);
                    Check(yellow.Color == EnemyColor.Blue && !ability.IsTransforming && penalties == 1, "Conversion completes exactly once");
                    Check(player.Alive != playerIsYellow, "Last conversion hits only a Yellow player");
                    if (playerIsYellow) Check(!player.ShotActive, "Transformation hit cancels the player's shot");
                    Check(!grid.IsColorCleared(EnemyColor.Yellow) && clears == 0 && player.MagicCharges == 0,
                        "Conversion grants neither a clear bar nor magic");
                    ability.Tick(1);
                    Check(penalties == 1, "Completed conversion cannot repeat the penalty");
                    Add(grid, EnemyColor.Red, 4, 3);
                    grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                    Check(!grid.IsColorCleared(EnemyColor.Yellow), "Killing the converted group cannot retroactively clear Yellow");
                    SpawnOverride.Enabled = true;
                    SpawnOverride.Types = 1 << (int)EnemyColor.Yellow;
                    Check(spawner.TryAdvance() && grid.Model.ColorCount(EnemyColor.Yellow) == RunProgress.StandardRowWidth,
                        "Yellow can spawn again after its last member transformed");
                });

            WithFixture((grid, player, spawner) =>
            {
                var first = Add(grid, EnemyColor.Yellow, 0, 0);
                var second = Add(grid, EnemyColor.Yellow, 2, 0);
                player.RefreshColor();
                var red = Add(grid, EnemyColor.Red, 1, 0);
                first.GetComponent<EnemyAbilities>().BeginImitation(red);
                second.GetComponent<EnemyAbilities>().BeginImitation(red);
                first.GetComponent<EnemyAbilities>().Tick(2);
                Check(player.Alive && grid.Model.ColorCount(EnemyColor.Yellow) == 1, "Another Yellow prevents last-color penalty");
                Check(grid.ClearMatchingChain(second.Id, EnemyColor.Yellow) == 3 && grid.IsColorCleared(EnemyColor.Yellow) && player.Alive,
                    "Killing the last remaining Yellow still earns its bar after earlier conversions");
            });

            WithFixture((grid, player, spawner) =>
            {
                var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                var red = Add(grid, EnemyColor.Red, 1, 0);
                var ability = yellow.GetComponent<EnemyAbilities>();
                ability.BeginImitation(red);
                grid.Unregister(red);
                UnityEngine.Object.DestroyImmediate(red.gameObject);
                ability.Tick(2);
                Check(yellow.Color == EnemyColor.Yellow && !ability.IsTransforming && !grid.IsColorCleared(EnemyColor.Yellow),
                    "Losing an attachment outside combat safely cancels conversion");
            });

            WithFixture((grid, player, spawner) =>
            {
                var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                var yellowSprite = yellow.Visuals.Body.sprite;
                var blue = Add(grid, EnemyColor.Blue, 1, 0);
                Add(grid, EnemyColor.Blue, 2, 0);
                var ability = yellow.GetComponent<EnemyAbilities>();
                var presentation = yellow.GetComponent<EnemyPresentation>();
                Check(ability.BeginImitation(blue), "Start conversion with a joined group");
                ability.Tick(EnemyAbilities.DefaultImitationSeconds);
                var pulse = grid.GetComponent<YellowGroupPulse>();
                Check(yellow.Color == EnemyColor.Blue && presentation.YellowRevealReturnActive &&
                    pulse != null && pulse.ActivePulseCount == 1 && pulse.EdgeCount == 8,
                    "Completed conversion starts one Yellow outline around the full joined group");
                var overlay = yellow.Visuals.Root.Find("Imitation overlay").GetComponent<SpriteRenderer>();
                Check(overlay.enabled && overlay.sprite == yellowSprite &&
                    Vector4.Distance(yellow.Visuals.Body.color, EnemyPalette.Get(EnemyColor.Yellow)) < .0001f,
                    "The converted enemy is revealed as Yellow when the pulse begins");
                float beat = AbilityBeatClock.DefaultBeatSeconds;
                float startingAlpha = pulse.MaxAlpha;
                pulse.Tick(beat * .5f);
                ability.Tick(beat * .5f);
                Check(pulse.MaxAlpha > startingAlpha && pulse.MaxAlpha < 1 && overlay.color.a > 0 && overlay.color.a < 1,
                    "The Yellow pulse and reveal come in over the first beat");
                pulse.Tick(beat * .5f);
                ability.Tick(beat * .5f);
                Check(pulse.MaxAlpha > .99f && overlay.color.a > .99f,
                    "The Yellow pulse reaches full strength after one beat");
                pulse.Tick(beat);
                ability.Tick(beat);
                Check(pulse.MaxAlpha > 0 && pulse.MaxAlpha < 1 && overlay.color.a > 0 && overlay.color.a < 1 &&
                    Vector4.Distance(yellow.Visuals.Body.color, EnemyPalette.Get(EnemyColor.Blue)) > .0001f,
                    "The outline and Yellow reveal fade while the enemy changes back to its mimic");
                pulse.Tick(beat);
                ability.Tick(beat);
                Check(pulse.ActivePulseCount == 0,
                    $"After three beats the pulse is gone (remaining pulses: {pulse.ActivePulseCount}, alpha: {pulse.MaxAlpha})");
                Check(!overlay.enabled,
                    $"After three beats the Yellow overlay is gone (enabled: {overlay.enabled}, alpha: {overlay.color.a})");
                Check(!presentation.YellowRevealReturnActive,
                    $"After three beats the Yellow reveal has ended (active: {presentation.YellowRevealReturnActive})");
                Check(Vector4.Distance(yellow.Visuals.Body.color, EnemyPalette.Get(EnemyColor.Blue)) < .0001f,
                    $"After three beats the mimic color is restored (actual: {yellow.Visuals.Body.color}, expected: {EnemyPalette.Get(EnemyColor.Blue)})");
            });
            Debug.Log("Yellow transformation checks passed: two-way linked kills, multiple attachments, kill-only rewards, conversion penalty, suspension, respawning and joined-group pulse timing.");
        }

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
