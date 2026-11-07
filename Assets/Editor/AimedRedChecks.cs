using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class AimedRedChecks
    {
        public static void Run()
        {
            var existingPlayers = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            foreach (var player in existingPlayers) player.gameObject.SetActive(false);
            try
            {
                CheckRespawnShots();
                foreach (float side in new[] { -2f, 2f })
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                    grid.RefreshSpecials();
                    player.transform.position = new Vector3(side, -4.6f, 0);
                    var ability = red.GetComponent<EnemyAbilities>();
                    var body = red.Visuals.Body.transform;
                    var rest = body.localRotation;
                    var bounds = red.HitBounds;
                    var before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                    ability.Tick(ability.CooldownRemaining - .46f);
                    Check(Quaternion.Angle(rest, body.localRotation) < .001f, "Red rests before its warning");
                    ability.Tick(.02f);
                    float turned = Quaternion.Angle(rest, body.localRotation);
                    Check(turned > 1 && turned < 20, "Warning begins a gradual turn toward the player");
                    Check(NewMissiles(before).Length == 0, "Turning does not fire early");
                    ability.Suspended = true;
                    var paused = body.localRotation;
                    float wait = ability.CooldownRemaining;
                    ability.Tick(10);
                    Check(body.localRotation == paused && ability.CooldownRemaining == wait, "Pause freezes aim and cooldown");
                    ability.Suspended = false;
                    for (int i = 0; i < 15; i++) ability.Tick(.02f);
                    Vector3 direction = (player.transform.position - red.transform.position).normalized;
                    Check(Vector3.Angle(body.rotation * Vector3.down, direction) < .01f, "Triangle points at the player before firing");
                    ability.Tick(ability.CooldownRemaining + .001f);
                    var missile = NewMissiles(before).Single();
                    Check(missile.Aimed && !missile.Homing, "Special Red fires aimed, not homing");
                    Check(Vector3.Angle(missile.transform.rotation * Vector3.down, direction) < .01f,
                        "Projectile heading matches the triangle and target at launch");
                    Check(red.HitBounds == bounds && red.transform.rotation == Quaternion.identity,
                        "Aim leaves gameplay bounds and fleet transform unchanged");
                    var smoke = missile.SmokeTrail;
                    Check(smoke != null && !smoke.enabled, "Aimed shot smoke trail starts empty");
                    var origin = missile.transform.position;
                    player.transform.position = new Vector3(-side, -4.6f, 0);
                    missile.Tick(.2f);
                    float firstDistance = Vector3.Distance(missile.transform.position, origin);
                    Check(firstDistance > 0 && Vector3.Dot(missile.transform.position - origin, direction) < 0,
                        "Shot initially bobs backward after launch");
                    Check(smoke != null && smoke.enabled && smoke.positionCount == 4, "Aimed shots draw a smoke trail");
                    var missileSprite = missile.GetComponent<SpriteRenderer>();
                    float missileWidth = missileSprite.sprite.bounds.size.x * Mathf.Abs(missile.transform.lossyScale.x);
                    Check(Mathf.Abs(smoke.startWidth - smoke.endWidth) < .001f &&
                        Mathf.Abs(smoke.startWidth - missileWidth * EnemyMissile.SmokeTrailWidthRatio) < .001f,
                        "Aimed shot smoke trail keeps a uniform half-missile width");
                    Vector3 smokeTail = smoke.GetPosition(0), smokeHead = smoke.GetPosition(smoke.positionCount - 1);
                    float screenThird = (Camera.main != null && Camera.main.orthographic ?
                        Camera.main.orthographicSize * 2 : EnemyMissile.FallbackViewHeight) * EnemyMissile.SmokeTrailScreenRatio;
                    Check(Vector3.Distance(smokeTail, smokeHead) < screenThird &&
                        Mathf.Abs(Vector3.Distance(smokeTail, smokeHead) - firstDistance) < .02f,
                        "Aimed shot smoke trail grows from the distance actually traveled");
                    float smokeAngle = Vector3.Angle(smokeHead - smokeTail, direction);
                    Check(smokeAngle < .05f,
                        "Aimed shot smoke trail follows the launch direction (" + smokeAngle + " degrees)");
                    missile.Tick(2f);
                    Check(Vector3.Dot(missile.transform.position - origin, direction) > 0,
                        "Shot reverses direction and moves forward after accelerating");
                    smokeTail = smoke.GetPosition(0); smokeHead = smoke.GetPosition(smoke.positionCount - 1);
                    Check(Mathf.Abs(Vector3.Distance(smokeTail, smokeHead) - screenThird) < .02f,
                        "Aimed shot smoke trail caps at about one third of the screen");
                    ability.Tick(.01f);
                    Check(Quaternion.Angle(rest, body.localRotation) > .1f && Quaternion.Angle(rest, body.localRotation) < turned + 25,
                        "Return begins gradually after firing");
                    ability.Tick(.4f);
                    Check(Quaternion.Angle(rest, body.localRotation) < .001f, "Triangle returns to its resting orientation");
                    ability.Tick(ability.CooldownRemaining - .2f);
                    Check(Quaternion.Angle(rest, body.localRotation) > 1, "Next shot aims at the player's new side");
                    grid.SetColor(red.Id, EnemyColor.Blue);
                    ability.Tick(0);
                    Check(Quaternion.Angle(rest, body.localRotation) < .001f, "Color changes clear the aiming pose");
                });
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    player.transform.position = new Vector3(2, -4.6f, 0);
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    var before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                    red.GetComponent<EnemyAbilities>().Tick(100);
                    var missile = NewMissiles(before).Single();
                    Check(!missile.Aimed && !missile.Homing, "Ordinary Red still fires downward");
                    var start = missile.transform.position;
                    missile.Tick(.2f);
                    float distance = Vector3.Distance(missile.transform.position, start);
                    Check(distance > 0 && distance < 1,
                        "Ordinary projectile starts below full speed");
                });
            }
            finally { foreach (var player in existingPlayers) if (player != null) player.gameObject.SetActive(true); }
            Debug.Log("Aimed Red checks passed: warning turn, downward respawn shots, post-landing aim, launch direction, fixed flight, return, pause, color reset and ordinary fire.");
        }

        private static void CheckRespawnShots()
        {
            foreach (float recoveryAge in new[] { 0, PlayerLifeIcons.ApexSeconds + .05f, PlayerLifeIcons.Duration - .01f })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                ProgressionChecks.Add(grid, EnemyColor.Red, 3, 0);
                grid.RefreshSpecials();
                player.transform.position = new Vector3(2, -4.6f, 0);
                var ability = red.GetComponent<EnemyAbilities>();
                ability.Tick(ability.CooldownRemaining - .2f);
                Check(Quaternion.Angle(red.Visuals.Body.transform.localRotation, Quaternion.identity) > 1,
                    "Special Red begins aiming before the player is hit");
                Check(player.Hit(), "Fixture starts the replacement jump");
                player.TickSurvival(recoveryAge);
                var before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                ability.Tick(ability.CooldownRemaining + .001f);
                var missile = NewMissiles(before).Single();
                Check(missile.Aimed && !missile.Homing &&
                    Vector3.Angle(missile.transform.rotation * Vector3.down, Vector3.down) < .01f &&
                    Vector3.Angle(red.Visuals.Body.transform.rotation * Vector3.down, Vector3.down) < .01f,
                    "A warning interrupted by respawn fires straight down before the apex and throughout the fall");
                var origin = missile.transform.position;
                missile.Tick(.05f);
                float respawnDistance = Vector3.Distance(missile.transform.position, origin);
                Check(respawnDistance > 0 && respawnDistance < .175f,
                    "Respawn shots launch slowly while flying straight down");
                player.TickSurvival(PlayerLifeIcons.Duration - recoveryAge + .001f);
                Check(!player.ReplacementFalling && player.RespawnProtectionActive, "Replacement has landed with protection");
                player.transform.position = new Vector3(-2, -4.6f, 0);
                before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                ability.Tick(ability.CooldownRemaining + .001f);
                var aimed = NewMissiles(before).Single();
                Check(Vector3.Angle(aimed.transform.rotation * Vector3.down,
                    player.transform.position - red.transform.position) < .01f,
                    "Aimed fire resumes on landing without waiting for flashing protection to expire");
            });
        }

        private static EnemyMissile[] NewMissiles(EnemyMissile[] before) =>
            UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Where(missile => !before.Contains(missile)).ToArray();
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Aimed Red check failed: " + message); }
    }
}
