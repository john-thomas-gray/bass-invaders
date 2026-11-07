using UnityEngine;

namespace CandyCruisers
{
    public static class CombatBalance
    {
        public const float SpecialGreenTempoIncrease = .03f;

        public static float FleetSpeedMultiplier(int level) => Mathf.Min(2, 1 + .03f * Mathf.Max(0, level - 1));

        public static Vector2Int CooldownBeats(EnemyColor color, int level, bool special = false)
        {
            // Preserve the former balance at the reference tempo; faster songs now mean faster abilities.
            var seconds = CooldownRange(color, level, special);
            int minimum = Mathf.Max(1, Mathf.CeilToInt(seconds.x / AbilityBeatClock.DefaultBeatSeconds));
            return new Vector2Int(minimum, Mathf.Max(minimum, Mathf.FloorToInt(seconds.y / AbilityBeatClock.DefaultBeatSeconds)));
        }

        public static Vector2 CooldownRange(EnemyColor color, int level, bool special = false)
        {
            Vector2 range = special ? SpecialCooldown(color) : BasicCooldown(color);
            range *= .75f;
            range.y = Mathf.Max(range.x + 3, range.y - .25f * Mathf.Max(0, level - 1));
            return range;
        }

        private static Vector2 BasicCooldown(EnemyColor color)
        {
            switch (color)
            {
                case EnemyColor.Blue: return new Vector2(3, 10);
                case EnemyColor.Green: return new Vector2(8, 10);
                case EnemyColor.Purple: return new Vector2(8, 12);
                case EnemyColor.Yellow: return new Vector2(2, 12);
                case EnemyColor.Orange: return new Vector2(1, 1);
                default: return new Vector2(3, 10);
            }
        }

        // Separate values let special abilities be tuned without changing their basic counterparts.
        private static Vector2 SpecialCooldown(EnemyColor color)
        {
            switch (color)
            {
                case EnemyColor.Blue: return new Vector2(1, 1);
                case EnemyColor.Green: return new Vector2(2, 16);
                case EnemyColor.Purple: return new Vector2(8, 24);
                case EnemyColor.Yellow: return new Vector2(2, 10);
                case EnemyColor.Orange: return new Vector2(1, 1);
                default: return new Vector2(3, 18);
            }
        }
    }
}
