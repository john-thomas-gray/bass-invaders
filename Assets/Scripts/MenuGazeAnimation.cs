using UnityEngine;

namespace CandyCruisers
{
    public sealed class MenuGazeAnimation
    {
        public const float Response = 10f;
        public const float IdleCycleSeconds = 32f;
        private const float GlanceTransitionSeconds = .35f;
        private float elapsed, mouseWeight;
        private Vector2? mouseTarget;

        public void Reset()
        {
            elapsed = mouseWeight = 0;
            mouseTarget = null;
        }

        public void Tick(float seconds, Vector2? target)
        {
            if (seconds <= 0) return;
            elapsed += seconds;
            float blend = 1 - Mathf.Exp(-Response * seconds);
            if (target.HasValue)
                mouseTarget = mouseTarget.HasValue ? Vector2.Lerp(mouseTarget.Value, target.Value, blend) : target;
            mouseWeight = Mathf.Lerp(mouseWeight, target.HasValue ? 1 : 0, blend);
        }

        public Vector2 DirectionFor(Rect eye)
        {
            var idle = IdleDirectionFor(elapsed);
            if (!mouseTarget.HasValue) return idle;
            var tracking = Vector2.ClampMagnitude((mouseTarget.Value - eye.center) / 180f, 1);
            return Vector2.Lerp(idle, tracking, mouseWeight);
        }

        // Menu coordinates point downward. Long centered holds keep eye contact with the player.
        public static Vector2 IdleDirectionFor(float seconds)
        {
            float phase = Mathf.Repeat(Mathf.Max(0, seconds), IdleCycleSeconds);
            return Vector2.down * (.8f * Glance(phase, 7f, 1.4f)) +
                Vector2.left * (.75f * Glance(phase, 17f, 1.1f)) +
                Vector2.right * (.75f * Glance(phase, 19.2f, 1.1f));
        }

        private static float Glance(float seconds, float start, float hold)
        {
            float rise = Mathf.SmoothStep(0, 1, (seconds - start) / GlanceTransitionSeconds);
            float fall = Mathf.SmoothStep(0, 1, (seconds - start - GlanceTransitionSeconds - hold) /
                GlanceTransitionSeconds);
            return rise * (1 - fall);
        }
    }
}
