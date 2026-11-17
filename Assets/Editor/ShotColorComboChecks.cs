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
            foreach (float step in new[] { .001f, .016f, .1f, 2f })
            foreach (EnemyColor color in new[] { EnemyColor.Red, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Orange })
            foreach (int finish in new[] { 0, 1, 2 })
            foreach (bool multiple in new[] { false, true }) CheckRevealCombo(step, color, finish, multiple);
            CheckDamageReset();
            Debug.Log("Shot color combo checks passed: per-color streaks, reveal pass-through, matching hits beyond reveals, shield rules, frame timing, misses and damage reset.");
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

        private static void CheckRevealCombo(float step, EnemyColor color, int finish, bool multiple)
        {
            WithSession((grid, player, tongue, session) =>
            {
                var music = grid.GetComponent<GameplayMusicPlayer>();
                music.Source.Stop(); music.Source.clip = null;
                var host = Add(grid, color, 3, 3);
                player.RefreshColor(true);
                Check(player.ReadyColor == color, "Reveal shot begins with the host color");
                var mimic = Add(grid, EnemyColor.Yellow, 2, 3);
                Add(grid, EnemyColor.Yellow, 1, 3); Add(grid, EnemyColor.Yellow, 0, 3);
                var upper = multiple ? Add(grid, EnemyColor.Yellow, 2, 2) : null;
                var upperHost = multiple ? Add(grid, color, color == EnemyColor.Orange ? 1 : 3, 2) : null;
                grid.RefreshSpecials();
                var ability = mimic.GetComponent<EnemyAbilities>();
                Check(ability.BeginImitation(host), "Special Yellow hides before the reveal shot");
                if (multiple) Check(upper.GetComponent<EnemyAbilities>().BeginImitation(upperHost), "A second special Yellow hides in the same lane");
                bool hitBehind = finish == 1;
                var behind = finish == 0 ? null : Add(grid, hitBehind ? color : EnemyColor.Blue, 2, 0);
                if (finish == 2)
                {
                    var shield = behind.GetComponent<EnemyAbilities>();
                    shield.Tick(shield.CooldownRemaining + .35f);
                    Check(shield.ShieldActive, "A regular shield waits beyond the reveal");
                }
                for (int i = 0; i < 4; i++)
                {
                    session.Progress.BeginShot(); session.Progress.RegisterShotColor(EnemyColor.Red); session.Progress.FinishShot(true);
                }
                session.Tick(0);
                player.transform.position = new Vector3(mimic.transform.position.x, -4.6f, 0);
                var observation = new PlaytestObservation(grid, player);
                Check(observation.At(player.transform.position.x) == (hitBehind ? 1 : 0) && ability.IsDisguised,
                    "Read-only shot opportunities see through reveals without changing them");
                int retractions = 0, completions = 0;
                Action<bool> retracting = hit =>
                {
                    retractions++;
                    Check(tongue.Length > mimic.HitBounds.max.y - tongue.transform.position.y,
                        "The tongue travels entirely beyond the revealed Yellow before returning");
                    Check(hit == hitBehind && session.Progress.ComboStreak == (hitBehind ? 5 : 0) && session.ComboBreak.Active != hitBehind,
                        "Only a real matching hit beyond the reveal preserves the combo at retraction");
                };
                Action<bool> completed = hit =>
                {
                    completions++;
                    Check(hit == hitBehind && session.Progress.ComboStreak == (hitBehind ? 5 : 0) && session.ComboBreak.Active != hitBehind,
                        "Completion retains the result of the real hit or miss beyond the reveal");
                };
                player.ShotRetractionStarted += retracting;
                player.ShotCompleted += completed;
                Check(player.Fire() && !tongue.IsMagic, "Fire an ordinary reveal shot");
                for (int i = 0; tongue.Active && i < 2000; i++) tongue.Tick(step, grid);
                player.ShotRetractionStarted -= retracting;
                player.ShotCompleted -= completed;
                Check(!tongue.Active && tongue.RevealedYellow && tongue.HitRegularShield == (finish == 2) && !ability.IsDisguised &&
                    (upper == null || !upper.GetComponent<EnemyAbilities>().IsDisguised) &&
                    session.Progress.Score == (hitBehind ? 500 : 0) && session.Progress.Defeated == (hitBehind ? 1 : 0) &&
                    retractions == 1 && completions == 1, "Reveals pass through and add no score or successful hit credit");
                Check(grid.View(mimic.Id) == mimic && grid.View(host.Id) == host &&
                    (behind == null || (grid.View(behind.Id) == null) == hitBehind), "Only the matching enemy beyond the reveal is destroyed");
                player.transform.position = new Vector3(10, -4.6f, 0);
                Check(player.Fire() && !tongue.RevealedYellow && !tongue.HitRegularShield,
                    "The next accepted shot resets reveal and shield flags"); tongue.Tick(2, grid);
                Check(session.Progress.ComboStreak == 0 && session.ComboBreak.Active,
                    "A later genuine miss also breaks the combo");
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
                int firstWeight = hitYellow ? 1 : 2;
                int firstStreak = 1;
                Check(session.Progress.Score == firstWeight * 100 && session.Progress.ComboStreak == firstStreak &&
                    session.Progress.ActiveComboMultiplier == firstStreak + 1 && tongue.Active,
                    "Direct host hits score the host color; Yellow hits score Yellow alone");
                Check(colors.Count == 1 && colors.Contains(hitYellow ? EnemyColor.Yellow : EnemyColor.Green), "Only destroyed colors advance the streak");
                Check(grid.Model.ColorCount(EnemyColor.Yellow) == (hitYellow ? 0 : 1) && grid.Model.ColorCount(EnemyColor.Green) == (hitYellow ? 2 : 0),
                    "Killing either real group preserves the other group");
                tongue.Tick(2, grid);
                Check(session.Progress.ComboStreak == firstStreak + (magic ? 1 : 0) &&
                    session.Progress.Score == firstWeight * 100 + (magic ? (firstStreak + 1) * 100 : 0),
                    "Magic's next contact uses the updated streak; ordinary return awards no additional point");
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
