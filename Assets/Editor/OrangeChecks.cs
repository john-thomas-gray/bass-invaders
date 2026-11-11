using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class OrangeChecks
    {
        public static void RunPlayMode()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            UnityEditor.SessionState.SetBool("CandyCruisers.OrangePlayChecks", true);
            UnityEditor.EditorApplication.isPlaying = true;
        }
        [UnityEditor.InitializeOnLoadMethod]
        private static void ResumePlayChecks()
        {
            if (UnityEditor.SessionState.GetBool("CandyCruisers.OrangePlayChecks", false))
                UnityEditor.EditorApplication.update += PollPlayChecks;
        }
        private static void PollPlayChecks()
        {
            if (!Application.isPlaying) return;
            UnityEditor.EditorApplication.update -= PollPlayChecks;
            UnityEditor.SessionState.SetBool("CandyCruisers.OrangePlayChecks", false);
            try { Run(); SpawnPresentationChecks.RunSummonTint(); UnityEditor.EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            CheckSpecialAppearance();
            Check(!RunProgress.IsUnlocked(EnemyColor.Orange, 8) && RunProgress.IsUnlocked(EnemyColor.Orange, 9), "Orange unlocks at nine");
            var model = new GridModel();
            Check(model.TryAdd(1, EnemyColor.Orange, 0, 0), "Add first Orange");
            Check(!model.TryAdd(2, EnemyColor.Orange, 1, 0) && !model.TryAdd(2, EnemyColor.Orange, 0, 1), "Spawn rejects orthogonal Orange adjacency");
            Check(model.TryAdd(2, EnemyColor.Red, 1, 0) && !model.SetColor(2, EnemyColor.Orange), "Conversion cannot create Orange adjacency");
            Check(model.TryAdd(3, EnemyColor.Orange, 3, 0) && !model.TryMove(3, 0, 1), "Direct moves preserve isolation");

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var orange = Add(grid, EnemyColor.Orange, 0, 1);
                var neighbor = Add(grid, EnemyColor.Red, 1, 1);
                var target = Add(grid, EnemyColor.Red, 4, 0);
                Add(grid, EnemyColor.Green, 3, 2);
                Check(grid.Model.OrangeSwapTargets(orange.Id).Select(e => e.Id).SequenceEqual(new[] { target.Id }), "Only remote matching singleton is eligible");
                var ability = orange.GetComponent<EnemyAbilities>();
                ability.Tick(ability.CooldownRemaining);
                Check(ability.IsCasting && orange.Column == 0, "Orange warns before swapping");
                ability.Suspended = true;
                ability.Tick(10);
                Check(orange.Column == 0, "Suspension freezes swaps");
                ability.Suspended = false;
                ability.Tick(AbilityBeatClock.DefaultBeatSeconds);
                Check(orange.Column == 4 && orange.Row == 0 && target.Column == 0 && target.Row == 1, "Swap updates both views");
                Check(grid.Model.At(4, 0).Id == orange.Id && grid.Model.At(0, 1).Id == target.Id && grid.Model.Count == 4,
                    "Swap preserves identities, colors and total occupancy");
                Check(grid.Model.ColorGroup(neighbor.Id).Count == 2 && !orange.IsSpecial, "Swap joins the matching group without upgrading Orange");
                Check(target.transform.localPosition == grid.CellPosition(0, 1) && orange.transform.localPosition == grid.CellPosition(4, 0), "Views follow swapped cells");
                Check(target.GetComponent<EnemyPresentation>().IsPhasing && orange.GetComponent<EnemyPresentation>().IsPhasing, "Both swap endpoints animate");
                Check(orange.Visuals.Body.sprite == grid.GetComponent<EnemyRowSpawner>().SpecialSprite,
                    "Orange keeps its special sprite through its swap animation");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var orange = Add(grid, EnemyColor.Orange, 0, 1);
                Add(grid, EnemyColor.Red, 1, 1);
                var target = Add(grid, EnemyColor.Red, 4, 0);
                Add(grid, EnemyColor.Orange, 5, 0);
                Check(!grid.TryOrangeSwap(orange.Id), "Swap cannot land next to another Orange");
                grid.SetColor(grid.Model.At(5, 0).Id, EnemyColor.Red);
                Check(!grid.TryOrangeSwap(orange.Id), "Matching pairs are not singletons");
                grid.Unregister(target); UnityEngine.Object.DestroyImmediate(target.gameObject);
                var remaining = grid.View(grid.Model.At(5, 0).Id);
                grid.Unregister(remaining); UnityEngine.Object.DestroyImmediate(remaining.gameObject);
                Check(!grid.TryOrangeSwap(orange.Id), "A sole adjacent matching neighbor cannot be swapped away from itself");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var orange = Add(grid, EnemyColor.Orange, 1, 0);
                var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
                var ability = yellow.GetComponent<EnemyAbilities>();
                Check(!ability.BeginImitation(orange) && !grid.Model.BeginImitation(yellow.Id, orange.Id), "Ordinary Yellow cannot become Orange");
                Add(grid, EnemyColor.Yellow, 0, 1); Add(grid, EnemyColor.Yellow, 1, 1);
                grid.RefreshSpecials();
                Check(yellow.IsSpecial && ability.BeginImitation(orange), "Special Yellow can mimic Orange");
                ability.Tick(EnemyAbilities.DefaultImitationSeconds);
                Check(ability.IsDisguised && yellow.Color == EnemyColor.Yellow && grid.Model.ColorCount(EnemyColor.Orange) == 1,
                    "Disguised Yellow remains logically Yellow beside Orange");
                Check(yellow.Visuals.Body.sprite == grid.GetComponent<EnemyRowSpawner>().SpecialSprite &&
                    yellow.Visuals.Body.sprite == orange.Visuals.Body.sprite, "A Yellow disguised as Orange copies its special appearance");
                Check(ability.RevealDisguise(EnemyColor.Red) && yellow.Color == EnemyColor.Yellow, "Orange disguise reveals normally");
            });
            foreach (int oranges in new[] { 2, 3 })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                for (int shot = 0; shot < 2; shot++)
                {
                    session.Progress.BeginShot();
                    session.Progress.RegisterShotColor(EnemyColor.Red);
                    session.Progress.FinishShot(true);
                }
                session.Progress.BeginShot();
                Add(grid, EnemyColor.Orange, 0, 0);
                if (oranges == 3) Add(grid, EnemyColor.Orange, 2, 0);
                Add(grid, EnemyColor.Orange, 1, 1);
                Add(grid, EnemyColor.Green, 4, 0);
                int ordinaryClears = 0;
                grid.MatchCleared += (count, all, weight) => ordinaryClears++;
                var music = grid.GetComponent<GameplayMusicPlayer>();
                if (music == null) music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                music.PreviewTempo = GameplayMusicPlayer.DefaultBeatsPerMinute;
                music.PreviewTime = 0;
                grid.TickRetreat(0);
                music.PreviewTime = AbilityBeatClock.DefaultBeatSeconds;
                grid.TickRetreat(0);
                Check(grid.Model.ColorCount(EnemyColor.Orange) == 0 && grid.Model.Count == 1, "Retreat detonates all touching Oranges only");
                Check(session.Progress.Score == oranges * 500 && session.Progress.Defeated == oranges && ordinaryClears == 0,
                    "Retreat awards exactly five base points per Orange, without borrowing shot combo");
                Check(session.Progress.ComboStreak == 2 && session.Progress.ActiveComboMultiplier == 3,
                    "Environmental Orange bursts cannot advance an active shot's combo");
                Check(grid.HasColorClearBar(EnemyColor.Orange) && grid.SeenColors().Contains(EnemyColor.Orange), "Explosion grants Orange color-clear credit");
                long score = session.Progress.Score;
                grid.TickRetreat(1);
                Check(session.Progress.Score == score, "Retreat cannot award explosion twice");
            });
            bool enabled = SpawnOverride.Enabled; int types = SpawnOverride.Types;
            try
            {
                SpawnOverride.Enabled = true;
                foreach (int mask in new[] { 1 << (int)EnemyColor.Orange, SpawnOverride.AllTypes, (1 << (int)EnemyColor.Orange) | 1 })
                for (int sample = 0; sample < 20; sample++)
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    SpawnOverride.Types = mask;
                    var spawner = grid.GetComponent<EnemyRowSpawner>();
                    Check(spawner.SpawnBatch(5), "Orange batch spawns");
                    Check(grid.Model.ColorCount(EnemyColor.Orange) == 1, "Every eligible new wave includes exactly one Orange");
                    VerifyIsolation(grid);
                    Check(spawner.TryAdvance(), "Orange descending row spawns");
                    VerifyIsolation(grid);
                    var source = grid.GetComponentsInChildren<GridEnemy>().First();
                    for (int i = 0; i < 10; i++) { spawner.TrySummon(source); VerifyIsolation(grid); }
                    grid.RefreshSpecials();
                    Check(grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Orange).All(e => !e.IsSpecial), "Orange never upgrades");
                });
            }
            finally { SpawnOverride.Enabled = enabled; SpawnOverride.Types = types; }
            CheckRareSpawns();
            Debug.Log("Orange checks passed: always-special appearance, stable hitboxes, unlock, isolation, matching-singleton swaps, pause, Yellow rules, retreat bonus and spawn paths.");
        }
        private static void CheckSpecialAppearance()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                {
                    var ordinary = spawner.Prefab(color).GetComponentInChildren<SpriteRenderer>().sprite;
                    Check(spawner.AppearanceSprite(color) == (color == EnemyColor.Orange ? spawner.SpecialSprite : ordinary) &&
                        spawner.AppearanceSprite(color, true) == spawner.SpecialSprite,
                        "Gameplay and menu previews share Orange's always-special sprite without changing other colors");
                }
                var orange = Add(grid, EnemyColor.Orange, 2, 1);
                var bounds = orange.HitBounds;
                var body = orange.Visuals.Body;
                var ordinaryOrange = spawner.Prefab(EnemyColor.Orange).GetComponentInChildren<SpriteRenderer>().sprite;
                Check(body.sprite == spawner.SpecialSprite && !orange.IsSpecial && orange.Tier == 1 &&
                    Mathf.Abs(body.bounds.size.x - ordinaryOrange.bounds.size.x * orange.transform.lossyScale.x) < .0001f,
                    "Orange starts with special artwork at its normal width without changing its gameplay tier");
                var ability = orange.GetComponent<EnemyAbilities>();
                foreach (bool warp in new[] { false, true })
                {
                    ability.BeginSpawnEffect(null, warp);
                    foreach (float age in new[] { 0f, .4f, .4f })
                    {
                        ability.Tick(age);
                        Check(body.sprite == spawner.SpecialSprite && orange.HitBounds == bounds,
                            "Orange keeps special artwork and its authored hitbox throughout growth and warp arrivals");
                    }
                }
                Check(grid.SetColor(orange.Id, EnemyColor.Red) && body.sprite == spawner.AppearanceSprite(EnemyColor.Red),
                    "Changing away from Orange restores the destination color's regular sprite");
                Check(grid.SetColor(orange.Id, EnemyColor.Orange) && body.sprite == spawner.SpecialSprite,
                    "Changing to Orange immediately applies the special sprite");
                grid.RefreshSpecials();
                Check(!orange.IsSpecial && body.sprite == spawner.SpecialSprite && orange.HitBounds == bounds,
                    "Promotion refresh preserves Orange's appearance without promoting it");
                var replacement = player.GetComponentInChildren<SpriteRenderer>().sprite;
                spawner.ConfigureSpecialSprite(replacement);
                spawner.ApplyAppearance(orange);
                Check(body.sprite == replacement && orange.HitBounds == bounds,
                    "Orange follows the configured special artwork without changing collision bounds");
                spawner.ConfigureSpecialSprite(null);
                spawner.ApplyAppearance(orange);
                Check(body.sprite == EnemyPlaceholderArt.Triangle && orange.HitBounds == bounds,
                    "Orange uses the same special-art fallback when no custom sprite is configured");
            });
        }
        private static void CheckRareSpawns()
        {
            bool enabled = SpawnOverride.Enabled; int types = SpawnOverride.Types;
            try
            {
                SpawnOverride.Enabled = true;
                SpawnOverride.Types = 1 << (int)EnemyColor.Orange;
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    UnityEngine.Random.InitState(91753);
                    var purple = Add(grid, EnemyColor.Purple, 2, 1);
                    var spawner = grid.GetComponent<EnemyRowSpawner>();
                    int oranges = 0;
                    for (int attempt = 0; attempt < 1500; attempt++)
                    {
                        var spawned = spawner.TrySummon(purple);
                        if (spawned == null) continue;
                        oranges++;
                        Check(spawned.Color == EnemyColor.Orange, "Orange-only override never substitutes other types");
                        Check(spawned.Visuals.Body.sprite == spawner.SpecialSprite, "Rare summoned Orange arrives with special artwork");
                        Check(spawner.TrySummon(purple) == null, "An existing Orange prevents a second summon");
                        grid.Unregister(spawned); UnityEngine.Object.DestroyImmediate(spawned.gameObject);
                    }
                    Check(EnemyRowSpawner.OrangeSpawnOdds == 75 && oranges >= 5 && oranges <= 45,
                        "Orange-only summons still use the rare 1-in-75 roll, not unconditional selection: " + oranges);
                    var orange = Add(grid, EnemyColor.Orange, 4, 0);
                    grid.ClearMatchingChain(orange.Id, EnemyColor.Orange);
                    for (int attempt = 0; attempt < 100; attempt++)
                        Check(spawner.TrySummon(purple) == null, "Color-clear lock still blocks rare Orange spawns");
                    grid.ResetColorClearStreak();
                    bool returned = false;
                    for (int attempt = 0; attempt < 1000 && !returned; attempt++) returned = spawner.TrySummon(purple) != null;
                    Check(returned && grid.Model.ColorCount(EnemyColor.Orange) == 1, "Releasing the color lock permits rare Orange spawns again");
                });
            }
            finally { SpawnOverride.Enabled = enabled; SpawnOverride.Types = types; }
        }
        private static void VerifyIsolation(EnemyGrid grid)
        {
            Check(grid.Model.ColorCount(EnemyColor.Orange) <= 1, "Spawn paths enforce one Orange on screen");
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                if (enemy.Color == EnemyColor.Orange)
                {
                    Check(!grid.Model.HasOrangeNeighbor(enemy.Column, enemy.Row), "No adjacent Oranges after spawning");
                    Check(enemy.Visuals.Body.sprite == grid.GetComponent<EnemyRowSpawner>().SpecialSprite,
                        "Every Orange uses special artwork across batch, row and summon spawn paths");
                }
        }
        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Orange check failed: " + message); }
    }
}
