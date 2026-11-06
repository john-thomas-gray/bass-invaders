using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class CombatTuningChecks
    {
        public static void Run()
        {
            var root = new GameObject("Tuning fixture", typeof(EnemyGrid), typeof(EnemyGridMovement));
            var mouth = new GameObject("Tuning tongue", typeof(LineRenderer), typeof(TongueShot));
            var random = UnityEngine.Random.state;
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var movement = root.GetComponent<EnemyGridMovement>();
                Check(!movement.UseGreenDashes, "Green-count movement is the default");
                grid.Model.TryAdd(1, EnemyColor.Red, 1, 0);
                movement.Tick(100);
                Check(movement.CurrentSpeed == 0 && root.transform.position == Vector3.zero, "No Greens means no motion");
                grid.Model.SetColor(1, EnemyColor.Green);
                movement.ResetSweep();
                float beat = FullSetCelebration.StepDuration;
                Near(movement.CurrentSpeed, .03f, "One Green supplies base movement");
                movement.Tick(beat);
                Near(root.transform.position.x, .03f * beat, "One Green advances one beat of travel on count two");
                grid.Model.TryAdd(2, EnemyColor.Green, 2, 0);
                Near(movement.CurrentSpeed, .06f, "Two Greens double movement");
                movement.Tick(2 * beat);
                Near(root.transform.position.x, .09f * beat, "Two Greens double the next tick length on count four");
                grid.Model.Remove(1);
                Near(movement.CurrentSpeed, .03f, "Removing Green immediately slows fleet");
                grid.Model.SetColor(2, EnemyColor.Blue);
                movement.Tick(100);
                Near(root.transform.position.x, .09f * beat, "Converting last Green immediately stops fleet");
                movement.AdvanceDistance(1);
                Near(root.transform.position.x, .09f * beat, "Direct movement also respects zero Greens");
                grid.Model.Remove(2);
                movement.ResetSweep();
                grid.ConfigureAbilities(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                var ability = blue.GetComponent<EnemyAbilities>();
                var shield = blue.Visuals.Root.Find("Shield").GetComponentInChildren<SpriteRenderer>();
                var fullScale = shield.transform.localScale;
                ability.BeginSpawnEffect();
                ability.Tick(.79f);
                Check(!shield.enabled && !ability.ShieldActive, "Shield is absent during rift animation");
                ability.Tick(.01f);
                Check(!shield.enabled && !ability.ShieldActive && !ability.Absorb(EnemyColor.Red),
                    "Ordinary Blue has no shield after the rift");
                float wait = ability.CooldownRemaining;
                ability.Suspended = true;
                ability.Tick(100);
                Near(ability.CooldownRemaining, wait, "Suspension freezes the initial cooldown");
                ability.Suspended = false;
                ability.Tick(wait - .01f);
                Check(!shield.enabled && !ability.ShieldActive, "Ordinary shield remains absent until cooldown expires");
                ability.Tick(ability.CooldownRemaining);
                Check(shield.enabled && !ability.ShieldActive && shield.transform.localScale.x < fullScale.x * .01f,
                    "Shield begins at a point only after the cooldown");
                Check(shield.transform.localPosition.y < 0, "Power-up point is in front of the enemy");
                ability.Tick(.175f);
                Near(shield.transform.localScale.x / fullScale.x, .5f, "Shield expands gradually");
                Check(!ability.Absorb(EnemyColor.Red), "Incomplete shield is not secretly protective");
                ability.Tick(.175f);
                Check(ability.ShieldActive && shield.transform.localPosition == Vector3.zero, "Completed shield returns to its original shape and position");
                var bodyBounds = blue.GetComponentInChildren<SpriteRenderer>().bounds;
                Near(shield.bounds.size.x, bodyBounds.size.x * 1.25f,
                    "Shield retains its original radius and thickness as a full ring");
                Near(shield.color.r, .82f, "Shield has a bright near-white core");
                Check(shield.color.a == 1 && ability.Absorb(EnemyColor.Red) && !shield.enabled, "Visible shield absorbs a hit then disappears");
                ability.Tick(100);
                Check(!ability.ShieldActive && !shield.enabled, "Long frames cannot recharge a spent shield");
                ability.BeginSpawnEffect();
                ability.Tick(100);
                Check(!ability.ShieldActive && !shield.enabled, "Replaying spawn visuals cannot restore a spent shield");
                grid.SetColor(blue.Id, EnemyColor.Red);
                ability.Tick(0);
                grid.SetColor(blue.Id, EnemyColor.Blue);
                ability.Tick(0);
                ability.BeginSpawnEffect();
                ability.Tick(1.15f);
                Check(!ability.ShieldActive && !shield.enabled, "Long-frame arrival does not bypass the initial cooldown");
                ability.Tick(ability.CooldownRemaining + .35f);
                Check(ability.ShieldActive, "Long-frame cooldown and power-up agree with stepped animation");
                ability.BeginSpawnEffect();
                ability.Tick(100);
                Check(ability.ShieldActive, "One long frame spanning arrival and cooldown still powers up correctly");
                var green = ProgressionChecks.Add(grid, EnemyColor.Green, 3, 0);
                green.GetComponent<EnemyAbilities>().Tick(100);
                Check(!green.GetComponent<EnemyAbilities>().IsCasting && root.transform.position == Vector3.zero,
                    "Default Greens do not dash or cast");
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
                grid.Model.TryAdd(10, EnemyColor.Green, GridModel.Columns - 1, 0);
                grid.OccupiedHorizontalBounds(out _, out float rightBeforeContact);
                float contactX = PlayerMovement.HalfWidth - rightBeforeContact;
                root.transform.position = Vector3.right * (contactX - movement.CurrentSpeed * beat * .5f);
                int contacts = 0;
                Action addGreen = () => { contacts++; grid.Model.TryAdd(11, EnemyColor.Green, 0, 0); };
                movement.SweepEnded += addGreen;
                movement.Tick(beat);
                Check(root.transform.position.x <= contactX + .0001f, "Contact beat stays clamped at the occupied boundary");
                Check(contacts == 1 && movement.Direction == -1, "Changing Green count does not duplicate boundary events");
                movement.SweepEnded -= addGreen;
                grid.Model.Remove(10); grid.Model.Remove(11);
                movement.ResetSweep();
                grid.Model.TryAdd(12, EnemyColor.Green, GridModel.Columns - 1, 0);
                root.transform.position = Vector3.right * contactX;
                movement.Tick(beat);
                Check(movement.Direction == -1, "Starting exactly at contact still reverses");

                var tongue = mouth.GetComponent<TongueShot>();
                tongue.TryFire(EnemyColor.Red, 10, true);
                Check(mouth.GetComponent<MagicChargePresentation>() == null, "Magic does not create a charge-up effect");
                tongue.Tick(.01f);
                Near(tongue.Length, .56f, "Magic extends on the first frame without a delay");
                tongue.Cancel();
                tongue.Tick(1, grid);
                Check(!tongue.Active, "Canceled shot cannot fire later");
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.Tick(.1f);
                Near(tongue.Length, 1.4f, "Normal shot has no wind-up");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 10, true);
                tongue.Tick(.1f);
                Near(tongue.Length, 5.6f, "Magic spends the whole frame extending");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 10, true);
                for (int i = 0; i < 10; i++) tongue.Tick(.01f);
                Near(tongue.Length, 5.6f, "Immediate magic is frame-rate independent");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 10, true);
                grid.Model.Remove(12);
                tongue.Tick(.3f, grid);
                Check(!tongue.Active, "An empty fleet ends the shot");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mouth);
                UnityEngine.Random.state = random;
            }
            Debug.Log("Combat tuning checks passed: live Green-count movement, zero-Green stop, shield sequencing, bright power-up and immediate magic timing/cancellation.");
        }
        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Combat tuning check failed: " + message); }
    }
}
