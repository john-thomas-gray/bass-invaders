using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class AbilityChecks
    {
        public static void Run()
        {
            var root = new GameObject("Ability fixture", typeof(EnemyGrid));
            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab");
            var shield = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png");
            var shot = new GameObject("Ability tongue", typeof(LineRenderer), typeof(TongueShot));
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                grid.ConfigureAbilities(projectile, shield);
                var blue = Add(grid, EnemyColor.Blue, 0, 1);
                var red = Add(grid, EnemyColor.Red, 0, 0);
                var ability = blue.GetComponent<EnemyAbilities>();
                Check(!ability.ShieldActive && !ability.Absorb(EnemyColor.Red), "Ordinary Blue starts unshielded");
                ability.Tick(ability.CooldownRemaining + .35f);
                Check(ability.ShieldActive, "Blue powers up after its initial cooldown");
                Check(!ability.Absorb(EnemyColor.Blue) && ability.ShieldActive, "Matching color bypasses shield");
                var tongue = shot.GetComponent<TongueShot>();
                shot.transform.position = new Vector3(blue.transform.position.x, -4, 0);
                tongue.TryFire(EnemyColor.Red, 8);
                tongue.Tick(2, grid);
                Check(!ability.ShieldActive && grid.Model.Count == 2 && !tongue.Active, "Shield stops mismatch before enemy behind it");
                ability.Tick(ability.CooldownRemaining * .5f);
                Check(!ability.ShieldActive, "Shield stays down during cooldown");
                ability.Tick(ability.CooldownRemaining + .35f);
                Check(!ability.ShieldActive && !ability.Absorb(EnemyColor.Red), "Spent shield never recharges");
                for (int i = 0; i < 600; i++) ability.Tick(.1f);
                Check(!ability.ShieldActive, "Spent shield stays down across small frame steps");
                tongue.TryFire(EnemyColor.Red, 8);
                tongue.Tick(2, grid);
                Check(grid.Model.Count == 1 && grid.Model.ColorCount(EnemyColor.Red) == 0,
                    "Broken shield lets mismatch through to matching enemy");
                ability.Tick(76);
                tongue.TryFire(EnemyColor.Blue, 8);
                tongue.Tick(2, grid);
                Check(grid.Model.Count == 0 && root.GetComponentsInChildren<EnemyAbilities>().Length == 0,
                    "Matching shot defeats spent Blue and removes its shield object");

                var shooter = Add(grid, EnemyColor.Red, 2, 0).GetComponent<EnemyAbilities>();
                for (int i = 0; i < 240 && !shooter.Warning; i++) shooter.Tick(0.1f);
                Check(shooter.Warning, "Red signals firing warning");
                shooter.Tick(0.7f);
                Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == 1,
                    "Red fires one missile");
                shooter.enabled = false;
                shooter.Tick(100);
                Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == 1,
                    "Disabled enemy stops firing");

                var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                var missile = UnityEngine.Object.FindFirstObjectByType<EnemyMissile>();
                missile.SetTarget(player);
                missile.transform.position = player.transform.position + Vector3.up * 2;
                player.Fire();
                missile.Tick(2);
                Check(missile.Finished && !player.Alive && player.Lives == PlayerMovement.MaxLives - 1 && !player.Fire(),
                    "Swept missile hit spends one life and disables player");
                Check(!player.GetComponentInChildren<TongueShot>().Active && !player.Hit(), "Hit cancels tongue and cannot stack");
                var position = player.transform.position;
                player.Move(1, 1);
                Check(player.transform.position == position, "Dead player cannot move");
                player.TickSurvival(1.5f);
                Check(player.Alive && player.Invulnerable && !player.Hit() && player.Fire(), "Respawn grants temporary protection");
                player.GetComponentInChildren<TongueShot>().Cancel();
                player.TickSurvival(1.6f);
                Check(!player.Invulnerable && player.Hit() && player.Lives == PlayerMovement.MaxLives - 2, "Protection expires");
                player.TickSurvival(4);
                UnityEngine.Object.DestroyImmediate(missile.gameObject);
                var stray = UnityEngine.Object.Instantiate(projectile).GetComponent<EnemyMissile>();
                stray.transform.position = new Vector3(20, 0, 0);
                stray.Tick(3);
                Check(stray.Finished, "Offscreen missiles expire");
                UnityEngine.Object.DestroyImmediate(stray.gameObject);
            }
            finally
            {
                foreach (var missile in UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(missile.gameObject);
                UnityEngine.Object.DestroyImmediate(shot);
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("Ability checks passed: shields, recharge, hit ordering, firing warning, missiles, player hits and respawn.");
        }

        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int column, int row)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(color == EnemyColor.Blue ?
                "Assets/Prefabs/Blue Enemy.prefab" : "Assets/Prefabs/Red Enemy.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab, grid.transform);
            var enemy = instance.AddComponent<GridEnemy>();
            enemy.Configure(color, column, row);
            grid.Register(enemy);
            return enemy;
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Ability check failed: " + message); }
    }
}
