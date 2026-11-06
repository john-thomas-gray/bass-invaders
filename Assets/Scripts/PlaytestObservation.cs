using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // A read-only stationary-lane estimate. Does not advance time, reveal disguises,
    // spend shields, consume the game's random stream, or remove model occupants.
    public sealed class PlaytestObservation
    {
        public struct Lane { public float x; public int kills; public Lane(float x, int kills) { this.x = x; this.kills = kills; } }
        private sealed class Target
        {
            public int id;
            public Bounds bounds;
            public bool matches, blocks;
            public HashSet<int> group;
        }
        public readonly List<Lane> lanes = new List<Lane>();
        public int Best { get; private set; }
        private readonly List<Target> targets = new List<Target>();
        private readonly EnemyGrid grid;
        private readonly Vector3 origin;
        private readonly float radius, length;
        private readonly bool magic, groupBlocks;

        public PlaytestObservation(EnemyGrid grid, PlayerMovement player)
        {
            this.grid = grid;
            origin = player.Tongue.transform.position;
            magic = player.ShotActive ? player.Tongue.IsMagic : player.MagicCharges > 0;
            var color = player.ShotActive ? player.Tongue.ShotColor : player.ReadyColor ?? EnemyColor.Red;
            radius = magic ? TongueShot.MagicHitRadius : TongueShot.NormalHitRadius;
            length = Mathf.Max(.1f, 5.3f - origin.y);
            groupBlocks = !magic && color != EnemyColor.Blue && grid.GroupShield != null;
            var groups = new Dictionary<int, HashSet<int>>();
            var xs = new List<float> { player.transform.position.x };
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            {
                if (!enemy.isActiveAndEnabled || grid.Model.Find(enemy.Id) == null) continue;
                if (!groups.TryGetValue(enemy.Id, out var group))
                {
                    group = new HashSet<int>(grid.Model.MatchingDepths(enemy.Id, enemy.Color).Keys);
                    foreach (int id in group) groups[id] = group;
                }
                var ability = enemy.GetComponent<EnemyAbilities>();
                bool blocks = !magic && color != EnemyColor.Blue && enemy.Color == EnemyColor.Blue &&
                    ability != null && ability.isActiveAndEnabled && ability.ShieldActive;
                bool groupProtected = groupBlocks && grid.GroupShield.Protects(enemy.Id);
                // Disguised Yellow reveals and lets a non-Yellow ordinary shot continue.
                bool matches = magic || enemy.Color == color;
                var bounds = blocks && !groupProtected ? ability.ShieldBounds : enemy.HitBounds;
                targets.Add(new Target { id = enemy.Id, bounds = bounds, matches = matches,
                    blocks = blocks && !groupProtected, group = group });
                xs.Add(bounds.min.x - radius + .0001f);
                xs.Add(bounds.max.x + radius - .0001f);
                xs.Add(bounds.center.x);
            }
            // Contour shields add their own exact horizontal interval boundaries.
            if (groupBlocks) grid.GroupShield.AddShotLaneBoundaries(xs, radius);
            xs.Add(-PlayerMovement.HalfWidth + .0001f); xs.Add(PlayerMovement.HalfWidth - .0001f);
            var reachable = player.ReachableShotOrigins();
            foreach (var interval in reachable) { xs.Add(interval.x + .00001f); xs.Add(interval.y - .00001f); }
            xs.Sort();
            int endpoints = xs.Count;
            for (int i = 1; i < endpoints; i++) xs.Add((xs[i - 1] + xs[i]) * .5f);
            foreach (float x in xs)
            {
                if (x < -PlayerMovement.HalfWidth || x >= PlayerMovement.HalfWidth) continue;
                if (!reachable.Exists(interval => x >= interval.x && x <= interval.y)) continue;
                int kills = At(x);
                lanes.Add(new Lane(x, kills));
                Best = Mathf.Max(Best, kills);
            }
        }

        public GridEnemy AimTargetAt(float x)
        {
            if (At(x) == 0) return null;
            Target nearest = null;
            float first = float.PositiveInfinity;
            foreach (var target in targets)
            {
                if (!target.matches || target.blocks || x + radius < target.bounds.min.x || x - radius > target.bounds.max.x ||
                    target.bounds.max.y < origin.y || target.bounds.min.y > origin.y + length) continue;
                float distance = Mathf.Max(0, target.bounds.min.y - origin.y);
                if (distance >= first) continue;
                first = distance; nearest = target;
            }
            return nearest != null ? grid.View(nearest.id) : null;
        }

        public int At(float x, float aimMargin = 0)
        {
            float first = float.PositiveInfinity;
            Target nearest = null;
            var removed = magic ? new HashSet<int>() : null;
            foreach (var target in targets)
            {
                if (!target.matches && !target.blocks) continue;
                var bounds = target.bounds;
                float reach = radius + (target.blocks ? aimMargin : -aimMargin);
                if (x + reach < bounds.min.x || x - reach > bounds.max.x ||
                    bounds.max.y < origin.y || bounds.min.y > origin.y + length) continue;
                if (magic) removed.UnionWith(target.group);
                else if (Mathf.Max(0, bounds.min.y - origin.y) < first)
                { first = Mathf.Max(0, bounds.min.y - origin.y); nearest = target; }
            }
            if (magic) return removed.Count;
            if (groupBlocks && grid.GroupShield.FindHit(new Vector3(x, origin.y, origin.z), 0, length, radius + aimMargin, out _, out float shield) && shield <= first)
                return 0;
            return nearest != null && !nearest.blocks && nearest.matches ? nearest.group.Count : 0;
        }
    }
}
