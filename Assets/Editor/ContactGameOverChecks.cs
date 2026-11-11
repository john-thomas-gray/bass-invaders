using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ContactGameOverChecks
    {
        public static void Run()
        {
            Fixture((grid, player, session) =>
            {
                Add(grid, 0);
                player.transform.position = new Vector3(2.8f, -4.6f, 0);
                Check(!grid.GetComponent<EnemyRowSpawner>().TryAdvance() && session.State == GameSession.RunState.Playing,
                    "Occupied last row blocks descent without ending game");
                var movement = grid.GetComponent<EnemyGridMovement>();
                movement.UseLegacyMovement = true;
                movement.AdvanceDistance(.1);
                Check(session.State == GameSession.RunState.Playing, "Fleet still sweeps while bottom row is occupied");
                player.transform.position = new Vector3(0, -4.6f, 0);
                var enemy = Add(grid, 2);
                session.Pause();
                Check(!session.CheckPlayerContact(), "Pause does not resolve contact");
                session.Resume();
                Check(session.CheckPlayerContact() && session.State == GameSession.RunState.Playing &&
                    player.Lives == PlayerMovement.MaxLives - 1 && !player.ControlsLocked,
                    "Touching last-row body spends one life before game over");
            });
            Fixture((grid, player, session) =>
            {
                var enemy = Add(grid, 2);
                player.transform.position = new Vector3(-2.8f, -4.6f, 0);
                player.Move(1, 1);
                Check(session.State == GameSession.RunState.Playing && player.Lives == PlayerMovement.MaxLives - 1 &&
                    player.transform.position.x < 0 &&
                    Mathf.Abs(player.HitBounds.max.x - enemy.HitBounds.min.x) < .0001f,
                    "Long player move stops at first body contact instead of tunneling");
            });
            Fixture((grid, player, session) =>
            {
                var enemy = Add(grid, 0);
                var movement = grid.GetComponent<EnemyGridMovement>();
                movement.UseLegacyMovement = true;
                movement.AdvanceDistance(3);
                Check(session.State == GameSession.RunState.Playing && player.Lives == PlayerMovement.MaxLives - 1 &&
                    Mathf.Abs(enemy.HitBounds.max.x - player.HitBounds.min.x) < .0001f,
                    "Fast fleet movement stops at first body contact");
            });
            Fixture((grid, player, session) =>
            {
                Add(grid, 2);
                player.transform.position = new Vector3(2.9f, -4.6f, 0);
                player.Move(1, .04f);
                Check(session.State == GameSession.RunState.Playing && Mathf.Abs(player.transform.position.x + 2.9f) < .0001f,
                    "Wrapping does not sweep through enemies in the middle");
                grid.transform.position += Vector3.left * 3;
                Check(session.CheckPlayerContact() && player.Lives == PlayerMovement.MaxLives - 1,
                    "Contact at the wrapped destination still costs a life");
            });
            Fixture((grid, player, session) =>
            {
                ProgressionChecks.Add(grid, EnemyColor.Red, 2, GridModel.Rows - 2);
                Check(!session.CheckPlayerContact(), "The previous boundary row does not touch the player");
                Check(player.Hit(), "Enter recovery");
                var bottom = Add(grid, 2);
                Check(!session.CheckPlayerContact(), "Absent recovering player has no contact");
                player.TickSurvival(1.5f);
                int protectedLives = player.Lives;
                Check(player.Invulnerable && !session.CheckPlayerContact() && player.Lives == protectedLives,
                    "Respawn protection prevents occupied-row contact damage");
                float protectedX = player.transform.position.x;
                player.Move(1, .1f);
                Check(player.transform.position.x > protectedX && player.Lives == protectedLives,
                    "Respawn protection lets the player move away from occupied-row contact");
                player.transform.position = new Vector3(bottom.transform.position.x, -4.6f, 0);
                player.TickSurvival(1.6f);
                Check(!player.Invulnerable && session.CheckPlayerContact() && player.Lives == protectedLives - 1,
                    "Occupied-row contact applies again after respawn protection expires");
            });
            Fixture((grid, player, session) =>
            {
                var enemy = Add(grid, 2);
                player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                Check(session.CheckPlayerContact() && player.Lives == PlayerMovement.MaxLives - 1 &&
                    session.State == GameSession.RunState.Playing,
                    "First contact spends one life");
                player.TickSurvival(3.1f);
                player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                Check(session.CheckPlayerContact() && player.Lives == 1 && player.ExtraLives == 0 &&
                    session.State == GameSession.RunState.Playing,
                    "Second contact consumes the final spare and still permits respawn");
                player.TickSurvival(3.1f);
                player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                Check(session.CheckPlayerContact() && player.Lives == 0 && session.State == GameSession.RunState.Dying &&
                    player.ControlsLocked, "Third contact starts death and locks controls");
            });
            Debug.Log("Contact game-over checks passed: bottom capacity, continued sweeping, contact lives, invulnerable respawn, pause, long moves, wrap and recovery.");
        }
        private static GridEnemy Add(EnemyGrid grid, int column) => ProgressionChecks.Add(grid, EnemyColor.Red, column, GridModel.Rows - 1);
        private static void Fixture(Action<EnemyGrid, PlayerMovement, GameSession> test) =>
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                try { test(grid, player, session); }
                finally { if (session.IsPaused) session.Resume(); }
            });
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Contact game-over check failed: " + message); }
    }
}
