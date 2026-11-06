using System;
using UnityEngine;

namespace CandyCruisers
{
    // Decisions use only the present board and visible projectiles. The driver cannot
    // grant charges, choose player colors, change lives, or alter combat outcomes.
    public sealed class PlaytestDriver
    {
        private readonly PlayerMovement player;
        private readonly EnemyGrid grid;
        private readonly PlaytestProfile profile;
        private readonly bool skilled;
        private readonly System.Random random;
        private float remaining, targetX, readySeconds;
        private EnemyMissile[] missiles = Array.Empty<EnemyMissile>();
        private bool fire, dodge;
        private int trackingId;
        private float trackingOffset;
        private EnemyColor? plannedColor;
        private bool plannedMagic;
        public bool MeasuredStage { get; private set; }
        public bool ConfigurationMatches { get; }
        public PlaytestDriver(PlayerMovement player, EnemyGrid grid, PlaytestProfile profile, bool skilled, int seed, string configurationHash)
        {
            this.player = player; this.grid = grid; this.profile = profile; this.skilled = skilled;
            random = new System.Random(unchecked(seed ^ 0x34f21)); targetX = player.transform.position.x;
            ConfigurationMatches = profile != null && profile.configurationHash == configurationHash;
        }
        public void Tick(float seconds, int level)
        {
            if (seconds <= 0 || !player.Alive || player.ControlsLocked || player.Stunned) return;
            var measured = profile?.ForLevel(level);
            var policy = measured ?? PlaytestProfile.Bootstrap(skilled);
            bool magic = player.MagicCharges > 0;
            if (!player.ShotActive && player.HasFleet) readySeconds += seconds;
            MeasuredStage = ConfigurationMatches && measured != null && (magic ? measured.HasMagicEvidence : measured.HasNormalEvidence);
            // Never turn zero samples into a zero-success policy. Report these stages as unmeasured.
            float hitRate = magic ? (measured != null && measured.observed.magic.completed > 0 ? policy.magicHitRate : PlaytestProfile.Bootstrap(skilled).magicHitRate)
                : (measured != null && measured.observed.normal.completed > 0 ? policy.normalHitRate : PlaytestProfile.Bootstrap(skilled).normalHitRate);
            remaining -= seconds;
            if (remaining <= 0)
            {
                missiles = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                remaining = Mathf.Max(.05f, policy.reactionSeconds);
                dodge = random.NextDouble() < policy.dodgeProbability;
                Decide(policy, magic, hitRate);
            }
            float delta = PlayerMovement.Wrap(targetX - player.transform.position.x);
            player.Move(Mathf.Clamp(delta / Mathf.Max(.0001f, 5 * seconds), -policy.movementFraction, policy.movementFraction), seconds);
            if (fire && readySeconds >= policy.readyDelay && Mathf.Abs(PlayerMovement.Wrap(targetX - player.transform.position.x)) < .005f && !player.ShotActive && !player.ReplacementFalling)
            {
                if (plannedColor != player.ReadyColor || plannedMagic != (player.MagicCharges > 0))
                { fire = false; remaining = 0; return; }
                if (player.Fire()) readySeconds = 0;
                fire = false;
                // Firing cadence must not delay observation of new hazards in flight.
            }
        }
        private void Decide(PlaytestBenchmark policy, bool magic, float hitRate)
        {
            if (player.ShotActive)
            {
                // Follow the last observed target using the same movement limits as the player.
                var target = grid.View(trackingId);
                if (!player.Tongue.Retracting && target != null &&
                    (player.Tongue.IsMagic || target.Color == player.Tongue.ShotColor))
                {
                    float follow = Mathf.Clamp(target.HitBounds.center.x + trackingOffset,
                        -PlayerMovement.HalfWidth + .001f, PlayerMovement.HalfWidth - .001f);
                    if (!dodge || Danger(follow) <= .1f) targetX = follow;
                }
                if (dodge && (Danger(player.transform.position.x) > .1f || Danger(targetX) > .1f)) ChooseSafeLane();
                return;
            }
            if (!player.HasFleet) { if (dodge) ChooseSafeLane(); return; }
            var observation = new PlaytestObservation(grid, player);
            float efficiency = magic ? policy.magicEfficiency : policy.normalEfficiency;
            bool attemptHit = random.NextDouble() < hitRate;
            float desired = attemptHit ? Mathf.Max(1, observation.Best * Mathf.Clamp01(efficiency)) : 0;
            float bestScore = float.NegativeInfinity;
            int chosenKills = 0;
            foreach (var lane in observation.lanes)
            {
                float distance = Mathf.Abs(PlayerMovement.Wrap(lane.x - player.transform.position.x));
                float score = -Mathf.Abs(lane.kills - desired) - distance * .08f;
                // A grazing snapshot contact is fragile under normal sprite animation.
                // Prefer an interior lane with the same capacity on either side.
                if (observation.At(lane.x, .06f) != lane.kills)
                    score -= 2;
                if (dodge) score -= Danger(lane.x) * 8;
                score += (float)random.NextDouble() * .03f;
                if (score <= bestScore) continue;
                bestScore = score; targetX = lane.x; chosenKills = lane.kills;
            }
            var aimTarget = observation.AimTargetAt(targetX);
            trackingId = aimTarget != null ? aimTarget.Id : 0;
            if (aimTarget != null)
            {
                float center = aimTarget.HitBounds.center.x;
                if (observation.lanes.Exists(lane => Mathf.Abs(lane.x - center) < .0001f && lane.kills == chosenKills) &&
                    (!dodge || Danger(center) <= .1f)) targetX = center;
                trackingOffset = targetX - center;
            }
            plannedColor = player.ReadyColor; plannedMagic = magic;
            // A safe dodge position is not automatically a firing opportunity. When all
            // lanes are blocked, a shot can still break a shield or advance the color.
            fire = observation.lanes.Count > 0 && (!attemptHit || observation.Best == 0 || chosenKills > 0);
        }
        private void ChooseSafeLane()
        {
            float best = float.PositiveInfinity;
            foreach (var interval in player.ReachableShotOrigins())
            for (float x = interval.x + .03f; x <= interval.y - .03f; x += .15f)
            {
                float score = Danger(x) * 10 + Mathf.Abs(PlayerMovement.Wrap(x - player.transform.position.x)) * .1f;
                if (score < best) { best = score; targetX = x; }
            }
        }
        private float Danger(float x)
        {
            float result = 0;
            foreach (var missile in missiles)
            {
                if (missile == null || missile.Finished || missile.Suspended) continue;
                var position = missile.transform.position;
                float height = position.y - player.HitBounds.min.y;
                if (height < 0 || height > 3) continue;
                // Visible current heading, without consulting a future random ability timer.
                Vector3 heading = missile.transform.rotation * Vector3.down;
                float projected = position.x + heading.x * height / Mathf.Max(.1f, -heading.y);
                float distance = Mathf.Abs(x - projected);
                result += Mathf.Max(0, 1 - distance / .65f) * (1 - height / 3);
            }
            return result;
        }
    }
}
