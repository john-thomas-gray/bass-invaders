using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PurpleYellowSpecialChecks
    {
        public static void CapturePreview()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var yellow = Add(grid, EnemyColor.Yellow, 0, 0);
            Add(grid, EnemyColor.Yellow, 0, 1);
            Add(grid, EnemyColor.Yellow, 1, 1);
            var red = Add(grid, EnemyColor.Red, 1, 0);
            Add(grid, EnemyColor.Red, 2, 0);
            var purple = Add(grid, EnemyColor.Purple, 3, 2);
            Add(grid, EnemyColor.Purple, 4, 2);
            Add(grid, EnemyColor.Purple, 4, 3);
            grid.RefreshSpecials();
            var ability = yellow.GetComponent<EnemyAbilities>();
            ability.BeginImitation(red);
            ability.Tick(2);
            for (int attempt = 0; attempt < 128; attempt++)
            {
                var summoned = grid.GetComponent<EnemyRowSpawner>().TrySummon(purple);
                if (summoned == null) continue;
                if (summoned.Row == 4) { summoned.GetComponent<EnemyAbilities>().Tick(.25f); break; }
                Remove(grid, summoned);
            }
            GameObject.Find("Player").GetComponent<PlayerMovement>().RefreshColor();
            GameplayChecks.Capture(540, 960, "special-disguise-portrait");
            GameplayChecks.Capture(960, 540, "special-disguise-landscape");
            ability.RevealDisguise(EnemyColor.Red);
            ability.Tick(.12f);
            GameplayChecks.Capture(540, 960, "special-reveal-portrait");
            GameplayChecks.Capture(390, 844, "special-reveal-phone");
            Debug.Log("Purple/Yellow special previews rendered: independent disguise, forward summon and reveal.");
        }

        public static void Run()
        {
            bool previous = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            try
            {
                SpawnOverride.Enabled = true;
                SpawnOverride.Types = 1 << (int)EnemyColor.Red;
                foreach (bool special in new[] { false, true })
                    SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                    {
                        var purple = Add(grid, EnemyColor.Purple, 2, 1);
                        if (special)
                        {
                            Add(grid, EnemyColor.Purple, 1, 1);
                            Add(grid, EnemyColor.Purple, 1, 0);
                            grid.RefreshSpecials();
                        }
                        var presentation = purple.GetComponent<EnemyPresentation>();
                        var scale = purple.Visuals.Body.transform.localScale;
                        var summoned = grid.GetComponent<EnemyRowSpawner>().TrySummon(purple);
                        Check(summoned != null && purple.Visuals.Body.color == Color.white,
                            "Successful ordinary and special summons flash the source Purple white");
                        var halo = purple.GetComponentsInChildren<LineRenderer>()
                            .Where(line => line.name.StartsWith("Purple summon flash")).ToArray();
                        Check(halo.Length == 2 && halo.All(line => line.enabled && !line.useWorldSpace),
                            "Summoning Purple has a bright local electric halo");
                        presentation.Tick(.06f, EnemyColor.Purple, 0);
                        Check(purple.Visuals.Body.color == Color.white, "Summon flash holds white long enough to read");
                        presentation.Tick(EnemyPresentation.SummonFlashSeconds, EnemyColor.Purple, 0);
                        Check(purple.Visuals.Body.color == EnemyPalette.Get(EnemyColor.Purple) &&
                            halo.All(line => !line.enabled) && purple.Visuals.Body.transform.localScale == scale,
                            "Summon flash fades to Purple without changing geometry");
                        var ability = purple.GetComponent<EnemyAbilities>();
                        ability.Tick(ability.CooldownRemaining + .01f);
                        Check(purple.Visuals.Body.color == Color.white && halo.All(line => line.enabled),
                            "The triggering frame cannot consume the new flash, even with a large frame delta");
                        grid.SetColor(purple.Id, EnemyColor.Blue);
                        presentation.Tick(0, EnemyColor.Blue, 0);
                        Check(halo.All(line => !line.enabled), "Recoloring cancels the summon halo");
                        presentation.Clear();
                    });
                foreach (int rows in new[] { 3, GridModel.Rows - 2, GridModel.Rows - 1, GridModel.Rows })
                    SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                    {
                        for (int y = 0; y < rows; y++)
                        for (int x = 0; x < GridModel.Columns; x++) Add(grid, EnemyColor.Purple, x, y);
                        var source = grid.View(grid.Model.At(0, 0).Id);
                        var spawner = grid.GetComponent<EnemyRowSpawner>();
                        Check(spawner.TrySummon(source) == null, "Ordinary Purple cannot extend full existing rows");
                        grid.RefreshSpecials();
                        var candidates = spawner.SummonCandidates(grid.Model, true).Values.SelectMany(cells => cells).ToArray();
                        Check(candidates.All(cell => cell.y == rows && cell.y < GridModel.Rows),
                            "Special candidates are one row ahead, including the bottom row, within grid bounds");
                        var summoned = spawner.TrySummon(source);
                        if (rows < GridModel.Rows)
                            Check(summoned != null && summoned.Row == rows && summoned.Color == EnemyColor.Red,
                                "Special Purple spawns an eligible color into the next row, including the bottom row");
                        else Check(summoned == null && candidates.Length == 0, "Special Purple cannot spawn beyond the grid");
                    });

                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var source = Add(grid, EnemyColor.Purple, 0, 0);
                    Add(grid, EnemyColor.Purple, 1, 0);
                    Add(grid, EnemyColor.Purple, 2, 0);
                    var red = Add(grid, EnemyColor.Red, 4, 2);
                    grid.RefreshSpecials();
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    Check(grid.GetComponent<EnemyRowSpawner>().TrySummon(source) == null,
                        "Special forward-row summons still respect color-clear locks and override restrictions");
                });

                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var yellow = Add(grid, EnemyColor.Yellow, 0, 1);
                    player.RefreshColor();
                    var helperA = Add(grid, EnemyColor.Yellow, 0, 0);
                    var helperB = Add(grid, EnemyColor.Yellow, 1, 0);
                    var red = Add(grid, EnemyColor.Red, 1, 1);
                    var red2 = Add(grid, EnemyColor.Red, 2, 1);
                    grid.RefreshSpecials();
                    Check(yellow.IsSpecial && !red.IsSpecial && !red2.IsSpecial, "Only the true Yellow group promotes");
                    Remove(grid, helperA); Remove(grid, helperB);
                    int penalties = 0, defeats = 0;
                    grid.LastYellowTransformed += () => penalties++;
                    grid.MatchCleared += (count, all, weight) => defeats += count;
                    var ability = yellow.GetComponent<EnemyAbilities>();
                    Check(ability.BeginImitation(red), "Special Yellow begins a disguise");
                    Check(!grid.Model.TryGetImitationTarget(yellow.Id, out _) && grid.Model.SelectablePlayerColors().Contains(EnemyColor.Yellow),
                        "Disguise creates no combat link and keeps Yellow selectable");
                    ability.Tick(EnemyAbilities.ImitationSeconds);
                    Check(ability.IsDisguised && !ability.IsTransforming && yellow.Color == EnemyColor.Yellow &&
                        grid.Model.ColorCount(EnemyColor.Yellow) == 1 && grid.Model.ColorCount(EnemyColor.Red) == 2 &&
                        yellow.GetComponentInChildren<SpriteRenderer>().sprite == red.GetComponentInChildren<SpriteRenderer>().sprite &&
                        yellow.GetComponentInChildren<SpriteRenderer>().color == EnemyPalette.Get(EnemyColor.Red),
                        "Completed disguise copies appearance but remains logically Yellow");
                    grid.RefreshSpecials();
                    Check(!red.IsSpecial && !red2.IsSpecial && !yellow.Visuals.Root.Find("Assimilation tendril").GetComponent<LineRenderer>().enabled,
                        "Two Reds plus disguised Yellow neither promote nor show a tendril");
                    Check(penalties == 0 && player.Alive && !grid.HasColorClearBar(EnemyColor.Yellow),
                        "Disguising the last Yellow grants no clear or last-transformation penalty");
                    Check(grid.ClearMatchingChain(red.Id, EnemyColor.Red) == 2 && grid.View(yellow.Id) == yellow,
                        "Clearing the copied color group does not kill the disguised Yellow");
                    ability.Tick(10);
                    Check(ability.IsDisguised && yellow.Color == EnemyColor.Yellow, "Disguise survives loss of its original source");
                    player.transform.position = new Vector3(yellow.transform.position.x, -4.6f, 0);
                    Check(tongue.TryFire(EnemyColor.Green, 10), "Fire a non-Yellow color unrelated to the disguise");
                    tongue.Tick(2, grid);
                    Check(!ability.IsDisguised && grid.View(yellow.Id) == yellow && defeats == 2 && !player.Stunned &&
                        ability.CooldownRemaining > 0 && yellow.GetComponentInChildren<SpriteRenderer>().color == Color.white,
                        "Any non-Yellow ordinary hit reveals with a flash, no kill or stun, and a fresh cooldown");
                    ability.Tick(.5f);
                    Check(yellow.GetComponentInChildren<SpriteRenderer>().color == EnemyPalette.Get(EnemyColor.Yellow) &&
                        yellow.GetComponentInChildren<SpriteRenderer>().sprite == grid.GetComponent<EnemyRowSpawner>().SpecialSprite,
                        "Reveal ends as a Yellow tier-two ship");
                    var blue = Add(grid, EnemyColor.Blue, 1, 1);
                    Check(ability.BeginImitation(blue), "Revealed Yellow can disguise again");
                    ability.Tick(2);
                    tongue.TryFire(EnemyColor.Yellow, 10);
                    tongue.Tick(2, grid);
                    Check(grid.View(yellow.Id) == null && grid.View(blue.Id) == blue && grid.HasColorClearBar(EnemyColor.Yellow),
                        "Yellow shot kills the disguised Yellow and earns its true-color bar without killing the copied Blue");
                });

                foreach (bool magic in new[] { false, true })
                    SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                    {
                        var yellow = Add(grid, EnemyColor.Yellow, 0, 1);
                        Add(grid, EnemyColor.Yellow, 0, 0);
                        Add(grid, EnemyColor.Yellow, 1, 0);
                        var purple = Add(grid, EnemyColor.Purple, 1, 1);
                        grid.RefreshSpecials();
                        var ability = yellow.GetComponent<EnemyAbilities>();
                        ability.BeginImitation(purple);
                        ability.Tick(.5f);
                        grid.ResolveTongueHit(yellow.Id, EnemyColor.Purple, magic);
                        Check(grid.View(purple.Id) == purple, "Hitting the disguise never follows a copied-color link");
                        if (magic) Check(grid.Model.ColorCount(EnemyColor.Yellow) == 0 && grid.HasColorClearBar(EnemyColor.Yellow),
                            "Magic kills the real Yellow group even during disguise transition");
                        else Check(!ability.IsDisguised && !ability.IsTransforming && grid.Model.ColorCount(EnemyColor.Yellow) == 3,
                            "A copied-color hit reveals instead of killing, including during transition");
                    });

                foreach (float step in new[] { .016f, .22f, 2f })
                    SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                    {
                        var lower = Add(grid, EnemyColor.Yellow, 2, 2);
                        var upper = Add(grid, EnemyColor.Yellow, 2, 1);
                        Add(grid, EnemyColor.Yellow, 1, 1);
                        var lowerCopy = Add(grid, EnemyColor.Green, 3, 2);
                        var upperCopy = Add(grid, EnemyColor.Green, 3, 1);
                        var red = Add(grid, EnemyColor.Red, 2, 0);
                        grid.RefreshSpecials();
                        var lowerAbility = lower.GetComponent<EnemyAbilities>();
                        var upperAbility = upper.GetComponent<EnemyAbilities>();
                        lowerAbility.BeginImitation(lowerCopy); upperAbility.BeginImitation(upperCopy);
                        lowerAbility.Tick(2); upperAbility.Tick(2);
                        player.transform.position = new Vector3(lower.transform.position.x, -4.6f, 0);
                        tongue.TryFire(EnemyColor.Red, 10);
                        if (step == .22f)
                        {
                            tongue.Tick(step, grid);
                            Check(!lowerAbility.IsDisguised && upperAbility.IsDisguised && tongue.Active && !tongue.Retracting &&
                                Mathf.Abs(tongue.Length - 14 * step) < .001f,
                                "Reveal preserves extension speed and does not start retraction");
                        }
                        for (int i = 0; i < 300 && tongue.Active; i++) tongue.Tick(step, grid);
                        Check(!tongue.Active && !lowerAbility.IsDisguised && !upperAbility.IsDisguised &&
                            grid.View(red.Id) == null && grid.Model.ColorCount(EnemyColor.Yellow) == 3 &&
                            grid.Model.ColorCount(EnemyColor.Green) == 2 && !player.Stunned && grid.HasColorClearBar(EnemyColor.Red),
                            "One shot reveals multiple disguises, hits a real target behind them and preserves its clear reward at any frame size");
                    });

                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var yellow = Add(grid, EnemyColor.Yellow, 0, 1);
                    var red = Add(grid, EnemyColor.Red, 1, 1);
                    var ability = yellow.GetComponent<EnemyAbilities>();
                    ability.BeginImitation(red);
                    ability.Tick(.5f);
                    Add(grid, EnemyColor.Yellow, 0, 0);
                    Add(grid, EnemyColor.Yellow, 1, 0);
                    grid.RefreshSpecials();
                    Check(ability.IsDisguised && !grid.Model.TryGetImitationTarget(yellow.Id, out _),
                        "Promotion detaches an in-progress ordinary transformation");
                    ability.Tick(2);
                    Check(yellow.Color == EnemyColor.Yellow && ability.IsDisguised, "Promoted transition completes as a disguise, not a conversion");
                });
            }
            finally { SpawnOverride.Enabled = previous; SpawnOverride.Types = types; }
            Debug.Log("Purple/Yellow special checks passed: safe forward rows, spawn locks, true-color disguises, reveal hits, independent groups and mid-transition promotion.");
        }
        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        private static void Remove(EnemyGrid grid, GridEnemy enemy)
        { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Purple/Yellow special check failed: " + message); }
    }
}
