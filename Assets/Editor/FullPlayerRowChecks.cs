using System;
using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class FullPlayerRowChecks
    {
        public static void Run()
        {
            foreach (int level in new[] { 1, 7 })
            foreach (bool recovering in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                player.transform.position = new Vector3(2.9f, -4.6f, 0);
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                session.Progress.Reset(level);
                int width = grid.GetComponent<EnemyRowSpawner>().CurrentRowWidth;
                if (recovering) Check(player.Hit(), "Enter recovery before row fills");
                for (int col = 0; col < width - 1; col++)
                    ProgressionChecks.Add(grid, EnemyColor.Blue, col, GridModel.Rows - 1);
                session.Tick(0);
                Check(session.State == GameSession.RunState.Playing, "A partial row does not end the run");
                session.Pause();
                ProgressionChecks.Add(grid, EnemyColor.Red, width - 1, GridModel.Rows - 1);
                Check(!session.CheckPlayerContact(), "Pause defers the full-row rule");
                session.Resume();
                Check(session.CheckPlayerContact() && session.State == GameSession.RunState.Dying &&
                    player.FatallyDefeated && player.Lives == 0 && !player.Alive,
                    "Full row ends the run regardless of lives, horizontal position or recovery");
                Check(!session.CheckPlayerContact(), "Full row cannot restart death");
                Check(CharacterVisuals.Ensure(player.gameObject).Body.enabled,
                    "Full-row defeat restores the intact player during the fade, including from a recoverable death");
                session.Tick(session.GameOverBlackoutSeconds);
                session.Tick(.45f);
                Check(session.State == GameSession.RunState.Dying, "Death animation plays before game over");
                session.Tick(PlayerMeltDeath.Duration - .45f + .01f);
                Check(session.State == GameSession.RunState.GameOver, "Full-row death reaches game over");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                for (int col = 0; col < GridModel.Columns; col++)
                    ProgressionChecks.Add(grid, EnemyColor.Blue, col, GridModel.Rows - 2);
                Check(!session.CheckPlayerContact() && session.State == GameSession.RunState.Playing,
                    "A full row above the player is not game over");
            });
            Debug.Log("Full player-row checks passed: five/six-wide rows, partial rows, recovery, pause, vertical position and animated game over.");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Full player-row check failed: " + message); }
    }
}
