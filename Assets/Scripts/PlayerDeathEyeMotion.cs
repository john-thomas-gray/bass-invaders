using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // The blackout owns this pose after normal gaze updates, until the detached melt takes over.
    internal sealed class PlayerDeathEyeMotion
    {
        private sealed class Pupil
        {
            public Transform transform;
            public SpriteRenderer renderer, eye;
            public Vector3 originalPosition, initialOffset;
            public float direction, radius;
        }
        private readonly Transform artwork;
        private readonly List<Pupil> pupils = new List<Pupil>();
        public const float TurnsPerSecond = 1.25f;
        private readonly float riseSeconds, phaseCorrection;

        public PlayerDeathEyeMotion(Transform root, float duration)
        {
            artwork = root;
            riseSeconds = Mathf.Max(.12f, duration);
            float naturalAngle = riseSeconds * TurnsPerSecond * 360;
            float turns = Mathf.Max(0, Mathf.Round((naturalAngle - 180) / 360));
            phaseCorrection = 180 + turns * 360 - naturalAngle;
            var sprites = root.GetComponentsInChildren<SpriteRenderer>();
            var body = root.GetComponentInParent<CharacterVisuals>()?.Body;
            float center = body != null ? body.bounds.center.x : root.position.x;
            foreach (var sprite in sprites)
            {
                if (!sprite.name.Contains("Pupil")) continue;
                var eye = FindEye(sprite, sprites);
                if (eye == null) continue;
                pupils.Add(new Pupil
                {
                    transform = sprite.transform, renderer = sprite, eye = eye, originalPosition = sprite.transform.localPosition,
                    initialOffset = root.InverseTransformVector(sprite.transform.position - eye.bounds.center),
                    direction = sprite.bounds.center.x < center ? 1 : -1,
                    radius = Mathf.Min(eye.bounds.size.x, eye.bounds.size.y) * .19f
                });
            }
        }

        public void Present(float seconds)
        {
            float age = Mathf.Clamp(seconds, 0, riseSeconds);
            // End opposite the default gaze, with the same angular speed as the final half-turn.
            float angle = age * TurnsPerSecond * 360 + phaseCorrection * Mathf.SmoothStep(0, 1, age / riseSeconds);
            foreach (var pupil in pupils)
            {
                if (pupil.transform == null || pupil.eye == null) continue;
                var initial = pupil.initialOffset;
                var resting = artwork.InverseTransformVector(RestPosition(pupil.renderer, pupil.eye) - pupil.eye.bounds.center);
                var direction = resting.sqrMagnitude > .000001f ? resting.normalized : Vector3.up;
                var orbit = Quaternion.Euler(0, 0, pupil.direction * angle) * direction;
                var worldOffset = Vector3.Lerp(artwork.TransformVector(initial),
                    artwork.TransformDirection(orbit) * pupil.radius, Mathf.SmoothStep(0, 1, age / .12f));
                pupil.transform.position = pupil.eye.bounds.center + worldOffset;
            }
        }

        public void Restore()
        {
            foreach (var pupil in pupils)
                if (pupil.transform != null) pupil.transform.localPosition = pupil.originalPosition;
        }

        public static SpriteRenderer FindEye(SpriteRenderer pupil, SpriteRenderer[] sprites)
        {
            SpriteRenderer eye = null;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in sprites)
            {
                if (!candidate.name.Contains("Eye") || !candidate.enabled) continue;
                float distance = (pupil.transform.position - candidate.transform.position).sqrMagnitude;
                if (distance >= nearest) continue;
                eye = candidate; nearest = distance;
            }
            return eye;
        }

        public static Vector3 RestPosition(SpriteRenderer pupil, SpriteRenderer eye)
        {
            var slime = pupil.GetComponentInParent<PlayerSlimeVisual>();
            if (slime != null) return slime.DefaultPupilPosition(pupil.transform);
            return eye != null ? eye.bounds.center + pupil.transform.up *
                (Mathf.Min(eye.bounds.size.x, eye.bounds.size.y) * .19f) : pupil.transform.position;
        }
    }
}
