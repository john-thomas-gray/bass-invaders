using UnityEngine;

namespace CandyCruisers
{
    public sealed class EnemyMissile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 5f;
        [SerializeField, Range(-50f, 1f), Tooltip("Fraction of full flight speed at launch; negative values briefly move backward.")]
        private float launchSpeedRatio = -.1f;
        [SerializeField, Min(.01f), Tooltip("Seconds over which the missile accelerates to full flight speed.")]
        private float accelerationSeconds = 2f;
        private PlayerMovement target;
        private float age;
        private Vector3 direction = Vector3.down;
        [SerializeField, Min(0.1f)] private float homingSpeed = 3.5f;
        public const float SmokeTrailScreenRatio = 1f / 3f;
        public const float SmokeTrailWidthRatio = .5f;
        public const float FallbackViewHeight = 10.6f;
        public const float FallbackMissileWidth = .5f;
        [SerializeField, Range(.05f, .5f)] private float smokeTrailScreenRatio = SmokeTrailScreenRatio;
        [SerializeField, Range(.1f, 1f)] private float smokeTrailWidthRatio = SmokeTrailWidthRatio;
        // Original: 0.3 degrees per frame, expressed at a 60-frame-per-second baseline.
        [SerializeField, Min(1)] private float turnDegreesPerSecond = 18f;
        [SerializeField, Range(0, 80)] private float maxHomingAngle = 50f;
        public const float DefaultFarFlashBeats = 2f;
        public const float DefaultNearFlashBeats = .5f;
        [SerializeField, Min(.125f), Tooltip("Beats per flash cycle while the missile is far from the player.")]
        private float farFlashBeats = DefaultFarFlashBeats;
        [SerializeField, Min(.125f), Tooltip("Beats per flash cycle when the missile reaches the player's hit area.")]
        private float nearFlashBeats = DefaultNearFlashBeats;
        [SerializeField, Min(.1f), Tooltip("Distance where the missile begins accelerating its beat flash cadence.")]
        private float flashAccelerationDistance = 6f;
        [SerializeField, Min(0), Tooltip("Distance where the missile reaches its fastest beat flash cadence.")]
        private float fullFlashTempoDistance = .4f;
        private static readonly Vector3 HitboxExpansion = new Vector3(.08f, .18f, 1f);
        private const string SmokeTrailName = "Smoke Trail";
        private static Material smokeTrailMaterial;
        private SpriteRenderer visual;
        private LineRenderer smokeTrail;
        private Color baseColor;
        private float headingDegrees;
        private float traveledDistance;
        private GameplayMusicPlayer music;
        private float flashCycles;
        private int flashPeakIndex;
        private int pendingFlashBeeps;
        private float lastMusicBeat;
        private bool musicBeatInitialized;
        public bool Homing { get; private set; }
        public bool Aimed { get; private set; }
        private bool suspended;
        public PlayerMovement Target => target;
        public bool Suspended
        {
            get => suspended;
            set { suspended = value; GetComponent<MissileFlightSound>()?.Refresh(); }
        }
        public bool Finished { get; private set; }
        public LineRenderer SmokeTrail => smokeTrail;
        public float FlashCycles => flashCycles;
        public float FlashTempoProximity => FlashProximity();
        public float CurrentFlashPeriodBeats => 1 / CurrentFlashCyclesPerBeat();
        private float HearingDistance => GetComponent<MissileFlightSound>()?.AudibleDistance ?? MissileFlightSound.DefaultAudibleDistance;
        public float BottomDespawnY => Mathf.Min(-FallbackViewHeight / 2,
            target != null ? target.transform.position.y : -FallbackViewHeight / 2) - HearingDistance - .25f;
        private Vector3 FlightHeading => direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.down;
        public bool PointedAtTarget
        {
            get
            {
                if (!TryTargetBounds(out var bounds)) return false;
                if (bounds.Contains(transform.position)) return true;
                return bounds.IntersectRay(new Ray(transform.position, FlightHeading), out float distance) && distance >= 0;
            }
        }
        public bool FlightSoundEligible => PointedAtTarget;
        private bool TryTargetBounds(out Bounds bounds)
        {
            bounds = default;
            if (target == null || !target.Alive || Finished) return false;
            bounds = target.HitBounds;
            bounds.Expand(HitboxExpansion);
            bounds.center = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
            return true;
        }
        public void SetTarget(PlayerMovement player, bool homing = false)
        {
            target = player;
            Homing = homing;
            Aimed = false;
            traveledDistance = 0;
            ResetFlashClock();
            RefreshVisual();
        }

        public void ConfigureFlashClock(GameplayMusicPlayer clock)
        {
            music = clock;
            musicBeatInitialized = false;
        }

        public int ConsumeFlashBeeps()
        {
            int count = pendingFlashBeeps;
            pendingFlashBeeps = 0;
            return count;
        }
        public void LaunchAimed(PlayerMovement player, Vector3 heading)
        {
            SetTarget(player);
            Aimed = true;
            heading.z = 0;
            direction = heading.sqrMagnitude > .000001f ? heading.normalized : Vector3.down;
            transform.rotation = Quaternion.Euler(0, 0, Vector3.SignedAngle(Vector3.down, direction, Vector3.forward));
            RefreshVisual();
        }
        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            if (Suspended || Finished || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (OutsideField(transform.position)) { Finish(); return; }
            // Bounded steering and swept collision remain stable even during long frames.
            float lifetime = Homing || Aimed ? 5f : 6f;
            float remaining = seconds;
            while (remaining > .000001f && !Finished)
            {
                float step = Mathf.Min(remaining, 1f / 120);
                Step(step);
                remaining -= step;
                // Expiry keeps nearby missiles alive long enough to finish their path cleanly.
                if (!Finished && age >= lifetime && BeyondHearingRange()) Finish();
            }
            RefreshVisual();
            GetComponent<MissileFlightSound>()?.Refresh();
        }

        private void RefreshVisual()
        {
            if (visual == null)
            {
                visual = GetComponent<SpriteRenderer>();
                if (visual != null) baseColor = visual.color;
            }
            if (visual != null)
            {
                float pulse = .5f - .5f * Mathf.Cos(flashCycles * Mathf.PI * 2);
                var bright = new Color(1, 1, 1, baseColor.a);
                visual.color = Color.Lerp(baseColor, bright, pulse * .85f);
            }
            RefreshSmokeTrail();
        }

        private void RefreshSmokeTrail()
        {
            EnsureSmokeTrail();
            if (smokeTrail == null) return;
            float length = SmokeTrailLength();
            smokeTrail.enabled = !Finished && length > .01f;
            if (!smokeTrail.enabled) return;
            smokeTrail.startWidth = smokeTrail.endWidth = SmokeTrailWidth();
            Vector3 heading = direction.sqrMagnitude > .000001f ? direction.normalized : transform.rotation * Vector3.down;
            Vector3 head = transform.position - heading * .06f;
            Vector3 tail = head - heading * length;
            smokeTrail.SetPosition(0, tail);
            smokeTrail.SetPosition(1, Vector3.Lerp(tail, head, .38f));
            smokeTrail.SetPosition(2, Vector3.Lerp(tail, head, .72f));
            smokeTrail.SetPosition(3, head);
        }

        private void EnsureSmokeTrail()
        {
            if (smokeTrail != null) return;
            var child = transform.Find(SmokeTrailName);
            if (child == null)
            {
                child = new GameObject(SmokeTrailName, typeof(LineRenderer)).transform;
                child.SetParent(transform, false);
            }
            smokeTrail = child.GetComponent<LineRenderer>();
            if (smokeTrail == null) smokeTrail = child.gameObject.AddComponent<LineRenderer>();
            smokeTrail.sharedMaterial = visual != null ? visual.sharedMaterial : SmokeTrailMaterial();
            smokeTrail.useWorldSpace = true;
            smokeTrail.loop = false;
            smokeTrail.positionCount = 4;
            smokeTrail.numCapVertices = 4;
            smokeTrail.numCornerVertices = 3;
            smokeTrail.sortingLayerID = visual != null ? visual.sortingLayerID : 0;
            smokeTrail.sortingOrder = visual != null ? visual.sortingOrder - 1 : 24;
            var color = new Color(.74f, .76f, .78f, 1);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.2f, .35f), new GradientAlphaKey(.48f, 1) });
            smokeTrail.colorGradient = gradient;
        }

        private float SmokeTrailLength()
        {
            var camera = Camera.main;
            float viewHeight = camera != null && camera.orthographic ? camera.orthographicSize * 2 : FallbackViewHeight;
            float fullLength = Mathf.Max(.5f, viewHeight * Mathf.Clamp(smokeTrailScreenRatio, .05f, .5f));
            return Mathf.Min(fullLength, traveledDistance);
        }

        private float SmokeTrailWidth()
        {
            float missileWidth = FallbackMissileWidth;
            if (visual != null && visual.sprite != null)
                missileWidth = visual.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
            return Mathf.Max(.03f, missileWidth * Mathf.Clamp(smokeTrailWidthRatio, .1f, 1f));
        }

        private static Material SmokeTrailMaterial()
        {
            if (smokeTrailMaterial == null) smokeTrailMaterial = new Material(Shader.Find("Sprites/Default"));
            return smokeTrailMaterial;
        }

        private void Step(float seconds)
        {
            Vector3 before = transform.position;
            if (Homing && target != null && target.Alive)
            {
                float offset = target.transform.position.x - before.x;
                float turn = offset > 0 ? 1 : offset < 0 ? -1 : 0;
                float limit = Mathf.Clamp(maxHomingAngle, 0, 80);
                headingDegrees = Mathf.Clamp(headingDegrees + turn * turnDegreesPerSecond * seconds, -limit, limit);
                direction = Quaternion.Euler(0, 0, headingDegrees) * Vector3.down;
                transform.rotation = Quaternion.Euler(0, 0, headingDegrees);
            }
            float fullSpeed = Homing || Aimed ? homingSpeed : speed;
            float launchSpeed = fullSpeed * Mathf.Clamp(launchSpeedRatio, -1f, 1f);
            float acceleration = Mathf.Clamp01(age / Mathf.Max(.01f, accelerationSeconds));
            float currentSpeed = Mathf.Lerp(launchSpeed, fullSpeed, acceleration);
            Vector3 after = before + direction * (currentSpeed * seconds);
            age += seconds;
            traveledDistance += Vector3.Distance(before, after);
            transform.position = after;
            AdvanceFlash(seconds);
            if (target != null && target.Alive)
            {
                var bounds = target.HitBounds;
                bounds.Expand(HitboxExpansion);
                if (bounds.Contains(before) || bounds.IntersectRay(new Ray(before, direction), out float distance) && distance <= currentSpeed * seconds)
                {
                    target.Hit();
                    Finish();
                    return;
                }
            }
            if (OutsideField(after)) Finish();
        }

        private bool BeyondHearingRange() => target == null ||
            MissileFlightSound.PlanarOffset(transform.position, target.transform.position).magnitude > HearingDistance + .1f;

        private bool OutsideField(Vector3 position) => position.y < BottomDespawnY ||
            (Mathf.Abs(position.x) > PlayerMovement.HalfWidth || (Homing || Aimed) && position.y > FallbackViewHeight / 2) && BeyondHearingRange();

        private void Finish()
        {
            Finished = true;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
        }

        private void ResetFlashClock()
        {
            flashCycles = 0;
            flashPeakIndex = Mathf.FloorToInt(flashCycles + .5f);
            pendingFlashBeeps = 0;
            musicBeatInitialized = false;
        }

        private void AdvanceFlash(float seconds)
        {
            flashCycles += BeatDelta(seconds) * CurrentFlashCyclesPerBeat();
            int peak = Mathf.FloorToInt(flashCycles + .5f);
            if (peak > flashPeakIndex) pendingFlashBeeps += peak - flashPeakIndex;
            flashPeakIndex = peak;
        }

        private float BeatDelta(float seconds)
        {
            if (music == null) music = FindFirstObjectByType<GameplayMusicPlayer>();
            if (music != null && music.Source.clip != null)
            {
                float beat = music.BeatPosition;
                if (musicBeatInitialized)
                {
                    float delta = beat - lastMusicBeat;
                    lastMusicBeat = beat;
                    if (delta > 0 && delta < Mathf.Max(.25f, seconds * Mathf.Max(1, music.CurrentTempo) / 30f + .5f))
                        return delta;
                }
                else
                {
                    lastMusicBeat = beat;
                    musicBeatInitialized = true;
                }
                return seconds / Mathf.Max(.001f, music.BeatDuration);
            }
            return seconds * GameplayMusicPlayer.DefaultBeatsPerMinute / 60f;
        }

        private float CurrentFlashCyclesPerBeat()
        {
            float near = Mathf.Max(.125f, nearFlashBeats);
            float far = Mathf.Max(near, farFlashBeats);
            return Mathf.Lerp(1 / far, 1 / near, Mathf.SmoothStep(0, 1, FlashProximity()));
        }

        private float FlashProximity()
        {
            if (target == null) return 0;
            float far = Mathf.Max(.1f, flashAccelerationDistance);
            float near = Mathf.Clamp(fullFlashTempoDistance, 0, far - .01f);
            float distance = MissileFlightSound.PlanarOffset(transform.position, target.transform.position).magnitude;
            return 1 - Mathf.InverseLerp(near, far, distance);
        }
    }
}
