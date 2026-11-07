using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ShotColorComboChecks
    {
        public static void Run()
        {
            foreach (float step in new[] { .001f, .016f, .1f, 2f })
            foreach (bool fullClear in new[] { false, true }) CheckMagic(step, fullClear);
            foreach (bool magic in new[] { false, true })
            foreach (bool hitYellow in new[] { false, true }) CheckYellowLink(magic, hitYellow);
            foreach (float step in new[] { .001f, .016f, .1f, 2f })
            foreach (bool special in new[] { false, true }) CheckShieldCombo(step, special);
            CheckDamageReset();
            Debug.Log("Shot color combo checks passed: immediate per-color streaks, next-contact scoring, repeated colors, Yellow links, regular shield preservation, special shield breaks, frame timing, fleet clears and damage reset.");
        }

        private static void CheckShieldCombo(float step, bool special)
        {
            WithSession((grid, player, tongue, session) =>
            {
                // Keep this synchronous shield check on the fallback ability clock.
                var music = grid.GetComponent<GameplayMusicPlayer>();
                music.Source.Stop(); music.Source.clip = null;
                Add(grid, EnemyColor.Red, 0, 0);
                player.RefreshColor(true);
                var blue = Add(grid, EnemyColor.Blue, 2, 3);
                if (special)
                {
                    Add(grid, EnemyColor.Blue, 1, 3);
                    Add(grid, EnemyColor.Blue, 3, 3);
                    grid.RefreshSpecials();
                }
                var shield = blue.GetComponent<EnemyAbilities>();
                shield.Tick(shield.CooldownRemaining + .35f);
                Check(shield.ShieldActive && blue.IsSpecial == special, "The intended Blue shield is active");
                for (int i = 0; i < 4; i++)
                {
                    session.Progress.BeginShot();
                    session.Progress.RegisterShotColor(EnemyColor.Red);
                    session.Progress.FinishShot(true);
                }
                session.Tick(0);
                bool? completedHit = null;
                player.ShotCompleted += hit => completedHit = hit;
                Action<bool> retraction = hit =>
                {
                    Check(!hit && session.Progress.ComboStreak == (special ? 0 : 4),
                        "Regular shield contact preserves the streak as the tongue begins returning");
                    Check(session.ComboBreak.Active == special, "Only the special shield starts the combo-break animation");
                };
                player.ShotRetractionStarted += retraction;
                player.transform.position = new Vector3(blue.transform.position.x, -4.6f, 0);
                Check(player.Fire() && tongue.ShotColor == EnemyColor.Red && !tongue.IsMagic,
                    "Fire a mismatched ordinary shot at the shield");
                for (int i = 0; tongue.Active && i < 2000; i++) tongue.Tick(step, grid);
                player.ShotRetractionStarted -= retraction;
                Check(!tongue.Active && completedHit == false && session.Progress.Score == 0 && session.Progress.Defeated == 0,
                    "A shield-only shot awards no kills, score or successful enemy-hit credit");
                Check(tongue.HitRegularShield == !special && session.Progress.ComboStreak == (special ? 0 : 4),
                    "Regular shield contact preserves the combo after the tongue arrives without advancing it");
                Check(shield.ShieldActive == special && grid.View(blue.Id) == blue,
                    "The ordinary shield is spent while the Blue enemy survives");
                if (special) return;

                var red = Add(grid, EnemyColor.Red, 2, 5);
                for (int i = 0; i < 100 && player.ReadyColor != EnemyColor.Red; i++) player.RefreshColor(true);
                Check(player.Fire() && !tongue.HitRegularShield, "The next accepted shot clears the shield-contact flag");
                tongue.Tick(2, grid);
                Check(grid.View(red.Id) == null && session.Progress.Score == 500 && session.Progress.ComboStreak == 5,
                    "The next enemy clear uses the preserved x5 multiplier and advances the streak once");
                player.transform.position = new Vector3(-2.9f, -4.6f, 0);
                Check(player.Fire(), "Fire the next shot down an empty column");
                tongue.Tick(2, grid);
                Check(!tongue.Active && session.Progress.ComboStreak == 0 && session.ComboBreak.Active,
                    "A later real miss still breaks the combo");
            });
        }

        private static void CheckMagic(float step, bool fullClear)
        {
            WithSession((grid, player, tongue, session) =>
            {
                var first = Add(grid, EnemyColor.Red, 2, 5);
                Add(grid, EnemyColor.Red, 1, 5);
                Add(grid, EnemyColor.Red, 3, 5);
                Add(grid, EnemyColor.Green, 2, 3);
                Add(grid, EnemyColor.Red, 2, 1);
                if (!fullClear) Add(grid, EnemyColor.Blue, 0, 0);
                GrantMagic(grid, player, session);
                player.transform.position = new Vector3(first.transform.position.x, -4.6f, 0);
                var multipliers = new List<int>();
                var scores = new List<long>();
                var streaks = new List<int>();
                grid.MatchCleared += (count, all, weight) =>
                {
                    multipliers.Add(session.Progress.ScoringComboMultiplier);
                    scores.Add(session.Progress.Score);
                };
                grid.MatchColorScored += color =>
                {
                    Check(tongue.Active, "The streak updates while the magic shot is still in flight");
                    streaks.Add(session.Progress.ComboStreak);
                };
                Check(player.Fire() && tongue.IsMagic, "Fire the real magic shot");
                for (int i = 0; tongue.Active && i < 2000; i++) tongue.Tick(step, grid);
                Check(!tongue.Active && grid.Model.Count == (fullClear ? 0 : 1), "All crossed contacts resolve exactly once");
                Check(multipliers.SequenceEqual(new[] { 1, 2, 3 }) && streaks.SequenceEqual(new[] { 1, 2, 2 }),
                    "Each distinct color advances immediately, but a later Red contact cannot award Red again");
                long total = 1000 + (fullClear ? 10000 : 0);
                Check(scores.SequenceEqual(new long[] { 500, 700, total }) && session.Progress.Score == total,
                    "Existing chain digits combine with the combo active at each contact");
                Check(session.Progress.ComboStreak == 2 && session.Progress.ComboMultiplier == 3,
                    "Return and fleet refill cannot add an extra streak point");
                Check(session.ScoreFeedback.StartsWith(GameSession.ScoreCalculation(100, 3, fullClear ? 10000 : 0, 700)),
                    "Score feedback preserves previously awarded points instead of multiplying them again");
                Check(tongue.MagicMultiplier == 2, "Streak advancement does not alter the separate magic-chain peak");
            });
        }

        private static void CheckYellowLink(bool magic, bool hitYellow)
        {
            WithSession((grid, player, tongue, session) =>
            {
                GridEnemy yellow = null;
                if (hitYellow)
                {
                    yellow = Add(grid, EnemyColor.Yellow, 2, 5);
                    player.RefreshColor(true);
                }
                var green = Add(grid, EnemyColor.Green, 3, 5);
                Add(grid, EnemyColor.Green, 4, 5);
                if (!hitYellow)
                {
                    player.RefreshColor(true);
                    yellow = Add(grid, EnemyColor.Yellow, 2, 5);
                }
                var first = hitYellow ? yellow : green;
                var next = Add(grid, EnemyColor.Red, first.Column, 2);
                Add(grid, EnemyColor.Purple, 0, 0);
                if (magic) GrantMagic(grid, player, session);
                player.transform.position = new Vector3(first.transform.position.x, -4.6f, 0);
                Check(player.Fire() && tongue.IsMagic == magic, "Accept the intended shot before Yellow starts merging");
                var ability = yellow.GetComponent<EnemyAbilities>();
                Check(!yellow.IsSpecial && ability.BeginImitation(green), "Basic Yellow links to the other color group in flight");
                var colors = new List<EnemyColor>();
                grid.MatchColorScored += colors.Add;
                while (session.Progress.Score == 0 && tongue.Active) tongue.Tick(.001f, grid);
                int firstWeight = hitYellow ? 6 : 5;
                Check(session.Progress.Score == firstWeight * 100 && session.Progress.ComboStreak == 2 &&
                    session.Progress.ActiveComboMultiplier == 3 && tongue.Active,
                    "Destroying linked Yellow and Green scores once and immediately awards two color streaks");
                Check(colors.Count == 2 && colors.Contains(EnemyColor.Yellow) && colors.Contains(EnemyColor.Green),
                    "Both true destroyed colors count regardless of which end of the link was hit");
                Check(grid.Model.ColorCount(EnemyColor.Yellow) == 0 && grid.Model.ColorCount(EnemyColor.Green) == 0,
                    "The entire linked group is removed");
                tongue.Tick(2, grid);
                Check(session.Progress.ComboStreak == (magic ? 3 : 2) &&
                    session.Progress.Score == firstWeight * 100 + (magic ? 300 : 0),
                    "Magic's next contact immediately uses x3; ordinary return awards no additional point");
                Check(magic ? grid.View(next.Id) == null : grid.View(next.Id) == next,
                    "Only the magic shot continues to the next enemy");
            });
        }

        private static void CheckDamageReset()
        {
            WithSession((grid, player, tongue, session) =>
            {
                var red = Add(grid, EnemyColor.Red, 2, 5);
                Add(grid, EnemyColor.Green, 2, 1);
                GrantMagic(grid, player, session);
                for (int i = 0; i < 3; i++)
                {
                    session.Progress.BeginShot();
                    session.Progress.RegisterShotColor(EnemyColor.Red);
                    session.Progress.FinishShot(true);
                }
                player.transform.position = new Vector3(red.transform.position.x, -4.6f, 0);
                Check(player.Fire(), "Start a magic shot with an existing streak");
                tongue.Tick(.02f, grid);
                Check(tongue.Active && session.Progress.Score == 400 && session.Progress.ComboMultiplier == 5,
                    "A milestone multiplier advances before the shot finishes");
                int milestone = (int)typeof(GameSession).GetField("comboMilestoneMultiplier",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(session);
                Check(milestone == 5, "Immediate streak updates trigger milestone presentation during flight");
                Check(!player.Fire() && session.Progress.ComboMultiplier == 5, "Rejected fire cannot reset per-shot colors or streak");
                Check(player.Hit() && !tongue.Active && session.Progress.ComboStreak == 0 &&
                    session.Progress.ActiveComboMultiplier == 1 && !session.ComboBreak.Active &&
                    session.ComboDeathMultiplier == 5 && session.ComboDeathOpacity == 1 && player.LifeIcons.Active,
                    "Damage resets the combo, fades its corner display and plays only life loss");
                Check(!session.Progress.RegisterShotColor(EnemyColor.Blue), "A canceled shot cannot award late color credit");
            });
        }

        private static void GrantMagic(EnemyGrid grid, PlayerMovement player, GameSession session)
        {
            var charge = Add(grid, EnemyColor.Orange, 5, 0);
            grid.ClearMatchingChain(charge.Id, EnemyColor.Orange);
            Check(player.MagicCharges > 0 && session.Progress.ComboStreak == 0,
                "A setup clear outside a shot can grant magic but cannot advance its combo");
            session.Progress.Reset();
        }

        private static void WithSession(Action<EnemyGrid, PlayerMovement, TongueShot, GameSession> check)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) Lifecycle(session, "OnEnable");
                try { check(grid, player, tongue, session); }
                finally
                {
                    if (!Application.isPlaying) Lifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static void Lifecycle(GameSession session, string method) => typeof(GameSession).GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int column, int row) => ProgressionChecks.Add(grid, color, column, row);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Shot color combo check failed: " + message); }
    }
}
