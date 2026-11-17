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
                        Check(ability.IsCasting && ability.Warning, "Purple warns for one beat before summoning");
                        ability.Tick(AbilityBeatClock.DefaultBeatSeconds);
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

                YellowTransformationChecks.RunHostRules();
            }
            finally { SpawnOverride.Enabled = previous; SpawnOverride.Types = types; }
            Debug.Log("Purple/Yellow special checks passed: safe forward rows, spawn locks, persistent host attachments and reversion.");
        }
        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        private static void Remove(EnemyGrid grid, GridEnemy enemy)
        { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Purple/Yellow special check failed: " + message); }
    }
}
