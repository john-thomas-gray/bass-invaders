using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ProgressionChecks
    {
        public static void Run()
        {
            var progress = new RunProgress();
            Check(progress.Level == 1 && progress.Score == 0, "Fresh run");
            Check(progress.RegisterClear(19, false) == 1900 && progress.Level == 1 && progress.EarnedLevel == 1,
                "Nineteen defeats remain below the level-two threshold");
            progress.RegisterClear(1, false);
            Check(progress.Level == 1 && progress.EarnedLevel == 2 && progress.LevelUpPending &&
                progress.ActiveLevelProgressFraction == 1 && progress.BankedLevelProgressFraction == 0,
                "First threshold fills the bar but waits for fleet clear");
            progress.RegisterClear(100, false);
            Check(progress.Level == 1 && progress.EarnedLevel == 4 && progress.Defeated == 120,
                "Multiple thresholds can bank during one wave without changing the active level");
            Check(progress.RegisterClear(1, true) == 10100 && progress.Level == 4 && progress.NextThreshold == 200,
                "Fleet clear jumps to the furthest earned level");
            progress.RegisterClear(179, false);
            Check(progress.Level == 4 && progress.EarnedLevel == 6 && progress.BatchRows == 5 &&
                progress.RowWidth == RunProgress.StandardRowWidth, "Purple level banks without changing current-row tuning");
            progress.RegisterClear(1, true);
            Check(progress.Level == 6 && progress.BatchRows == 5 && progress.RowWidth == RunProgress.StandardRowWidth,
                "Fleet clear commits the banked Purple level");
            var wideProgress = new RunProgress();
            while (wideProgress.Level < RunProgress.WideRowsStartLevel)
                wideProgress.RegisterClear(wideProgress.NextThreshold - wideProgress.Defeated, true);
            Check(wideProgress.Level == 7 && wideProgress.BatchRows == 6 && wideProgress.RowWidth == RunProgress.WideRowWidth &&
                wideProgress.BatchEnemies == 36, "Level seven expands refill rows to six wide");
            long before = wideProgress.Score;
            Check(wideProgress.RegisterClear(0, true) == 0 && wideProgress.Score == before, "No duplicate empty-clear bonus");
            var bonusProgress = new RunProgress();
            while (bonusProgress.Level < 6)
                bonusProgress.RegisterClear(bonusProgress.NextThreshold - bonusProgress.Defeated, true);
            Check(bonusProgress.RegisterClear(2, true) == 60300, "Per-enemy scaling and fleet bonus");
            var weighted = new RunProgress();
            Check(weighted.RegisterClear(5, false, 11) == 1100 && weighted.Defeated == 5,
                "Branch digits 1,2,2,3,3 multiply score without multiplying defeats");
            weighted.RegisterClear(13, false);
            Check(weighted.RegisterClear(2, true, 3) == 10300 && weighted.Level == 2 && weighted.Defeated == 20,
                "Weighted clear uses pre-clear level and leaves fleet bonus unmultiplied");
            var progressBar = new Rect(10, 20, 100, 9);
            Check(Mathf.Abs(GameSession.LevelProgressBaseHeight - 9f * 1.5f) < .001f,
                "Level progress bar is 50 percent taller than the old nine-pixel bar");
            foreach (var screen in new[] { new Vector3(960, 540, 0), new Vector3(1179, 2556, 1) })
            {
                float scale = GameSession.GuiScaleFor(screen.z > 0, screen.x, screen.y);
                float oldPixels = 9f * scale;
                float newPixels = GameSession.LevelProgressBaseHeight * scale;
                Check(Mathf.Abs(newPixels / oldPixels - 1.5f) < .001f,
                    "Level progress bar keeps the 50 percent height increase on desktop and iPhone-sized canvases");
            }
            Check(Mathf.Abs(GameSession.LevelProgressFillRect(progressBar, 2).width - progressBar.width) < .001f &&
                Mathf.Abs(GameSession.LevelProgressBankedRect(progressBar, .35f).width - 35) < .001f,
                "Level progress bar clamps the filled current bar and tracks banked next-bar progress");
            Check(GameSession.LevelProgressGlowRect(progressBar, 1).height > GameSession.LevelProgressGlowRect(progressBar, 0).height &&
                GameSession.LevelProgressGlowRect(progressBar, 1).width == progressBar.width,
                "Pending level progress glow swells only vertically on the beat");
            var swollen = GameSession.LevelProgressSolidRect(progressBar, true, 1);
            Check(swollen.width == progressBar.width && swollen.height == progressBar.height + 6 &&
                swollen.center == progressBar.center, "The pending solid fill expands vertically around its center on the beat");
            Check(GameSession.LevelProgressSolidRect(progressBar, true, 0) == progressBar &&
                GameSession.LevelProgressSolidRect(progressBar, false, 0) == progressBar,
                "Normal and pending progress return to resting size between beats");
            var normalPulse = GameSession.LevelProgressSolidRect(progressBar, false, 1);
            Check(normalPulse.width == progressBar.width && normalPulse.height > progressBar.height &&
                normalPulse.height < swollen.height && normalPulse.center == progressBar.center,
                "Normal progress also pulses on the beat, with a stronger pulse while a level is pending");
            foreach (float fraction in new[] { 0f, .25f, .75f, 1f })
                Check(Mathf.Abs(GameSession.LevelProgressFillRect(normalPulse, fraction).width / normalPulse.width - fraction) < .0001f,
                    "Pulsing changes presentation without changing the displayed completion fraction");
            var bankedRect = GameSession.LevelProgressBankedRect(swollen, .35f);
            Check(swollen.Contains(bankedRect.min) && swollen.Contains(bankedRect.max - Vector2.one * .001f),
                "Banked progress stays inside the expanded solid fill");
            for (int level = 1; level <= 7; level++)
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                Check(RunProgress.IsUnlocked(color, level) == (level >=
                    (color == EnemyColor.Green ? 2 : color == EnemyColor.Yellow ? 4 : color == EnemyColor.Purple ? 6 : color == EnemyColor.Orange ? 9 : 1)), "Exact unlock schedule");

            var random = UnityEngine.Random.state;
            UnityEngine.Random.InitState(8301);
            var root = new GameObject("Progression fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var movement = root.GetComponent<EnemyGridMovement>();
                movement.UseGreenDashes = true;
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
                grid.ConfigureAbilities(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
                var session = root.AddComponent<GameSession>();
                var scenePlayer = GameObject.Find("Player");
                session.Configure(scenePlayer != null ? scenePlayer.GetComponent<PlayerMovement>() : null);
                for (int level = 1; level <= 6; level++)
                {
                    if (level > 1) session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated, true);
                    Check(spawner.SpawnBatch(10), "Spawn unlocked fleet");
                    foreach (var enemy in root.GetComponentsInChildren<GridEnemy>())
                        Check(RunProgress.IsUnlocked(enemy.Color, level), "No premature row unlock");
                    Check(grid.Model.ColorCount(level >= 6 ? EnemyColor.Purple : level >= 4 ? EnemyColor.Yellow : level >= 2 ? EnemyColor.Green : EnemyColor.Red) > 0,
                        "Newly unlocked type enters random pool");
                    Empty(grid);
                }

                var green = Add(grid, EnemyColor.Green, 2, 0);
                var green2 = Add(grid, EnemyColor.Green, 3, 0);
                var ability = green.GetComponent<EnemyAbilities>();
                ability.Tick(21);
                Check(ability.IsCasting && root.transform.position == Vector3.zero, "Green warns before moving");
                ability.Tick(.8f);
                ability.Tick(.18f);
                Check(Mathf.Abs(root.transform.position.x - .1f) < .001f, "One Green contributes a shortened physical step");
                var second = green2.GetComponent<EnemyAbilities>();
                second.Tick(21); second.Tick(.8f); second.Tick(.18f);
                Check(Mathf.Abs(root.transform.position.x - .2f) < .001f, "Each Green contributes independently");
                second.Suspended = true;
                second.Tick(100);
                Check(Mathf.Abs(root.transform.position.x - .2f) < .001f, "Suspended abilities do not advance");
                movement.ResetSweep();
                // Edit-mode fixtures do not invoke MonoBehaviour.OnEnable automatically.
                movement.SweepEnded += () => spawner.TryAdvance();
                grid.OccupiedHorizontalBounds(out _, out float beforeDashRight);
                movement.AdvanceDistance(PlayerMovement.HalfWidth - beforeDashRight - .05f);
                ability.Tick(21); ability.Tick(.8f); ability.Tick(.18f);
                Check(movement.Direction == -1 && grid.Model.Count == 2 + RunProgress.StandardRowWidth, "Green burst at occupied edge turns and descends exactly once: direction=" +
                    movement.Direction + " count=" + grid.Model.Count + " x=" + root.transform.position.x + " casting=" + ability.IsCasting);
                grid.OccupiedHorizontalBounds(out float left, out float right);
                Check(left >= -3.0001f && right <= 3.0001f, "Green burst respects playfield after reindexing");
                Empty(grid); movement.ResetSweep();

                movement.enabled = false;
                float shortest = float.MaxValue, longest = 0;
                int resampled = 0;
                var greenRange = CombatBalance.CooldownRange(EnemyColor.Green, session.Progress.Level);
                for (int sample = 0; sample < 24; sample++)
                {
                    var timed = Add(grid, EnemyColor.Green, 2, 0).GetComponent<EnemyAbilities>();
                    float initial = UntilGreenWarning(timed);
                    timed.Tick(.8f);
                    timed.Tick(.18f);
                    float subsequent = UntilGreenWarning(timed) + .18f;
                    Check(initial >= greenRange.x - .1f && initial <= greenRange.y + .1f &&
                        subsequent >= greenRange.x - .1f && subsequent <= greenRange.y + .1f,
                        "Initial and repeated Green cooldowns stay within the configured range");
                    shortest = Mathf.Min(shortest, initial);
                    longest = Mathf.Max(longest, initial);
                    if (Mathf.Abs(initial - subsequent) > .5f) resampled++;
                    Empty(grid);
                }
                Check(longest - shortest > (greenRange.y - greenRange.x) * .66f && resampled > 12,
                    "Greens use staggered, freshly sampled cooldowns across the configured range");
                movement.enabled = true;

                var yellow = Add(grid, EnemyColor.Yellow, 2, 0);
                var blue = Add(grid, EnemyColor.Blue, 3, 0);
                yellow.GetComponent<EnemyAbilities>().Tick(41);
                Check(yellow.Color == EnemyColor.Yellow && yellow.GetComponent<EnemyAbilities>().IsTransforming,
                    "Yellow remains matchable during transformation");
                yellow.GetComponent<EnemyAbilities>().Tick(EnemyAbilities.ImitationSeconds);
                Check(yellow.Color == EnemyColor.Blue && grid.Model.ColorCount(EnemyColor.Yellow) == 0 &&
                    grid.Model.ColorCount(EnemyColor.Blue) == 2, "Ordinary Yellow permanently changes model color");
                var copiedBlue = yellow.GetComponent<EnemyAbilities>();
                Check(!copiedBlue.ShieldActive && copiedBlue.CooldownRemaining > 0, "Imitation becomes an ordinary Blue with an initial cooldown");
                copiedBlue.Tick(copiedBlue.CooldownRemaining + .35f);
                Check(copiedBlue.ShieldActive, "Copied Blue powers up after its cooldown");
                grid.Unregister(blue); UnityEngine.Object.DestroyImmediate(blue.gameObject);
                yellow.GetComponent<EnemyAbilities>().Tick(1);
                Check(yellow.Color == EnemyColor.Blue, "Neighbor death does not undo ordinary imitation");
                Empty(grid);
                yellow = Add(grid, EnemyColor.Yellow, 2, 0);
                Add(grid, EnemyColor.Yellow, 3, 0);
                yellow.GetComponent<EnemyAbilities>().Tick(41);
                Check(yellow.Color == EnemyColor.Yellow, "Yellow cannot imitate Yellow");
                Empty(grid);

                var purple = Add(grid, EnemyColor.Purple, 0, 0);
                Add(grid, EnemyColor.Red, GridModel.Columns - 1, 4);
                var candidates = spawner.SummonCandidates(grid.Model);
                Check(candidates.ContainsKey(5 * GridModel.Columns - 1) && candidates[5 * GridModel.Columns - 1].Any(c => c == new Vector2Int(GridModel.Columns - 2, 4)),
                    "Purple can fill gaps far from caller");
                Check(candidates.Values.SelectMany(c => c).All(c => c.y <= 4), "Ordinary Purple cannot extend deepest row");
                int count = grid.Model.Count;
                purple.GetComponent<EnemyAbilities>().Tick(51);
                Check(grid.Model.Count == count + 1, "Purple summons one ordinary enemy");
                foreach (var enemy in root.GetComponentsInChildren<GridEnemy>())
                    Check(grid.Model.At(enemy.Column, enemy.Row).Id == enemy.Id, "Summon preserves model/view agreement");
                Empty(grid);
                Check(spawner.SpawnBatch(GridModel.Rows), "Fill capacity");
                for (int row = 0; row < GridModel.Rows; row++)
                    Add(grid, EnemyColor.Blue, GridModel.Columns - 1, row);
                purple = root.GetComponentsInChildren<GridEnemy>()[0];
                Check(spawner.TrySummon(purple) == null && grid.Model.Count == GridModel.Columns * GridModel.Rows, "Full fleet cannot be overwritten");
                Empty(grid);
                UnityEngine.Object.DestroyImmediate(session);
                purple = Add(grid, EnemyColor.Purple, 3, 3);
                for (int i = 0; i < 12; i++)
                {
                    var summoned = spawner.TrySummon(purple);
                    Check(summoned != null && RunProgress.IsUnlocked(summoned.Color, 1), "Summons obey the same unlock gate");
                    grid.Unregister(summoned); UnityEngine.Object.DestroyImmediate(summoned.gameObject);
                }
                Empty(grid);
                int events = 0, removed = 0;
                grid.MatchCleared += (n, complete, weight) => { events++; removed += n; };
                var red = Add(grid, EnemyColor.Red, 2, 0);
                int id = red.Id;
                grid.ClearMatchingChain(id, EnemyColor.Blue);
                Check(events == 0, "Mismatch awards nothing");
                grid.ClearMatchingChain(id, EnemyColor.Red);
                grid.ClearMatchingChain(id, EnemyColor.Red);
                Check(events == 1 && removed == 1, "Scoring event exactly once per real removal");
                red = Add(grid, EnemyColor.Red, 2, 1);
                Add(grid, EnemyColor.Red, 1, 1);
                Add(grid, EnemyColor.Red, 3, 1);
                Add(grid, EnemyColor.Red, 1, 0);
                Add(grid, EnemyColor.Red, 3, 0);
                int emittedWeight = 0;
                grid.MatchCleared += (n, complete, weight) => emittedWeight = weight;
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(emittedWeight == 11 && removed == 6 && events == 2,
                    "Real branching clear emits the sum of displayed death digits once");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Random.state = random;
            }
            Debug.Log("Progression checks passed: scoring, thresholds, unlocks, independent bursts, original imitation and fleet-wide summons.");
        }

        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static float UntilGreenWarning(EnemyAbilities ability)
        {
            int ticks = 0;
            while (!ability.IsCasting && ticks < 210) { ability.Tick(.1f); ticks++; }
            Check(ability.IsCasting, "Green eventually begins its windup");
            return ticks * .1f;
        }
        public static GridEnemy Add(EnemyGrid grid, EnemyColor color, int column, int row)
        {
            var instance = UnityEngine.Object.Instantiate(Prefab(color));
            instance.SetActive(false);
            var enemy = instance.AddComponent<GridEnemy>();
            enemy.Configure(color, column, row);
            instance.transform.SetParent(grid.transform, false);
            instance.SetActive(true);
            grid.Register(enemy);
            return enemy;
        }
        private static void Empty(EnemyGrid grid)
        {
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Progression check failed: " + message); }
    }
}
