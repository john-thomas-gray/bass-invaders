using UnityEngine;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid))]
    public sealed class EnemyGridMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 0.3f;
        [UnityEngine.Serialization.FormerlySerializedAs("useGreenDashes")]
        [SerializeField] private bool useLegacyMovement;
        [SerializeField, Min(.001f)] private float stepDistance = .15f;
        private double bankedDistance;
        private readonly System.Collections.Generic.List<int> greenOrder = new();
        private readonly System.Collections.Generic.HashSet<int> liveGreens = new();
        private readonly System.Collections.Generic.HashSet<int> pulsedGreens = new();
        public event System.Action<int> GreenPulsed;
        public bool UseLegacyMovement { get => useLegacyMovement; set => useLegacyMovement = value; }
        private Vector3 origin;
        private bool initialized;
        private EnemyGrid grid;
        private int resetVersion;
        private double beatPosition;
        private bool beatClockInitialized;
        private bool beatClockUsesMusic;
        private int lastMovementBeat = int.MinValue;
        public int Direction { get; private set; } = 1;
        public float CurrentSpeed => speed * CombatBalance.FleetSpeedMultiplier(GetComponent<GameSession>()?.Progress.Level ?? 1)
            * (useLegacyMovement ? 1 : .1f * GetComponent<EnemyGrid>().RegularGreenCount);
        public event System.Action SweepEnded;

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (initialized) return;
            grid = GetComponent<EnemyGrid>();
            origin = transform.localPosition;
            initialized = true;
        }

        public void ResetSweep()
        {
            Initialize();
            Direction = 1;
            bankedDistance = 0;
            beatPosition = 0;
            beatClockInitialized = false;
            beatClockUsesMusic = false;
            lastMovementBeat = int.MinValue;
            greenOrder.Clear();
            pulsedGreens.Clear();
            transform.localPosition = origin;
            resetVersion++;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(double seconds)
        {
            Initialize();
            if (!enabled || speed <= 0 || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            if (!useLegacyMovement) { TickGreenBeatMovement(seconds); return; }
            TickLegacyMovement(seconds);
        }

        private void TickLegacyMovement(double seconds)
        {
            int version = resetVersion;
            // Integrate the existing speed, spending distance only on discrete steps.
            while (seconds > 0 && enabled && version == resetVersion)
            {
                float currentSpeed = CurrentSpeed;
                if (currentSpeed <= 0 || !grid.OccupiedHorizontalBounds(out float left, out float right) ||
                    right - left >= 2f * PlayerMovement.HalfWidth)
                { if (grid.Fleet.Count == 0) { bankedDistance = 0; greenOrder.Clear(); pulsedGreens.Clear(); } return; }
                double distance = System.Math.Max(0, Direction > 0 ? PlayerMovement.HalfWidth - right : left + PlayerMovement.HalfWidth);
                double step = System.Math.Min(System.Math.Max(.001, stepDistance), distance);
                double untilStep = System.Math.Max(0, step - bankedDistance) / currentSpeed;
                if (seconds + .000001 < untilStep)
                {
                    bankedDistance += seconds * currentSpeed;
                    return;
                }
                bankedDistance = System.Math.Max(0, bankedDistance - step);
                seconds = System.Math.Max(0, seconds - untilStep);
                if (step > 0) PulseNextGreen();
                AdvanceDistance(step);
            }
        }

        private void TickGreenBeatMovement(double seconds)
        {
            float beatDuration = CurrentBeatDuration;
            double current = AdvanceBeatClock(seconds, beatDuration, out double previous);
            if (current < previous)
            {
                beatPosition = current;
                lastMovementBeat = (int)System.Math.Floor(current);
                return;
            }
            if (lastMovementBeat == int.MinValue) lastMovementBeat = (int)System.Math.Floor(previous);
            int version = resetVersion;
            int currentBeat = (int)System.Math.Floor(current + .000001);
            while (lastMovementBeat < currentBeat && enabled && version == resetVersion)
            {
                lastMovementBeat++;
                var music = GetComponent<GameplayMusicPlayer>();
                int downbeatOffset = beatClockUsesMusic && music != null ? music.CurrentDownbeatOffsetBeats : 0;
                if (GameplayMusicPlayer.BeatInMeasure(lastMovementBeat, downbeatOffset) % 2 != 0) continue;
                float currentSpeed = CurrentSpeed;
                if (currentSpeed <= 0 || !grid.OccupiedHorizontalBounds(out float left, out float right) ||
                    right - left >= 2f * PlayerMovement.HalfWidth)
                {
                    if (grid.RegularGreenCount == 0 || grid.Fleet.Count == 0)
                    {
                        greenOrder.Clear();
                        pulsedGreens.Clear();
                    }
                    continue;
                }
                PulseNextGreen();
                // Keep the original single-beat step; skipping counts one and three halves travel.
                float elapsedBeat = beatClockUsesMusic && music != null ? music.CompletedBeatDuration(lastMovementBeat) : beatDuration;
                AdvanceDistance(currentSpeed * elapsedBeat);
            }
        }

        private float CurrentBeatDuration
        {
            get
            {
                var music = GetComponent<GameplayMusicPlayer>();
                return music != null ? music.BeatDuration : FullSetCelebration.StepDuration;
            }
        }

        private double AdvanceBeatClock(double seconds, float beatDuration, out double previous)
        {
            var music = GetComponent<GameplayMusicPlayer>();
            if (Application.isPlaying && music != null && music.BeatClockRunning)
            {
                double current = music.BeatPosition;
                previous = beatClockInitialized && beatClockUsesMusic ? beatPosition : current;
                beatPosition = current;
                beatClockInitialized = true;
                beatClockUsesMusic = true;
                return current;
            }
            if (!beatClockInitialized || beatClockUsesMusic)
            {
                beatPosition = 0;
                beatClockInitialized = true;
                beatClockUsesMusic = false;
            }
            previous = beatPosition;
            beatPosition += seconds / Mathf.Max(.0001f, beatDuration);
            return beatPosition;
        }

        private void PulseNextGreen()
        {
            liveGreens.Clear();
            for (int row = 0; row < GridModel.Rows; row++)
            for (int column = 0; column < GridModel.Columns; column++)
            {
                var enemy = grid.Model.At(column, row);
                if (enemy == null || enemy.Color != EnemyColor.Green || grid.View(enemy.Id)?.IsSpecial == true) continue;
                liveGreens.Add(enemy.Id);
                if (!pulsedGreens.Contains(enemy.Id) && !greenOrder.Contains(enemy.Id)) greenOrder.Add(enemy.Id);
            }
            greenOrder.RemoveAll(id => !liveGreens.Contains(id));
            pulsedGreens.RemoveWhere(id => !liveGreens.Contains(id));
            if (greenOrder.Count == 0 && liveGreens.Count > 0)
            {
                pulsedGreens.Clear();
                for (int row = 0; row < GridModel.Rows; row++)
                for (int column = 0; column < GridModel.Columns; column++)
                {
                    var enemy = grid.Model.At(column, row);
                    if (enemy != null && enemy.Color == EnemyColor.Green && grid.View(enemy.Id)?.IsSpecial != true) greenOrder.Add(enemy.Id);
                }
            }
            if (greenOrder.Count == 0) return;
            int next = greenOrder[0];
            greenOrder.RemoveAt(0);
            pulsedGreens.Add(next);
            float untilNextBeat = CurrentBeatDuration * (float)(System.Math.Floor(beatPosition) + 1 - beatPosition);
            grid.View(next)?.GetComponent<EnemyPresentation>()?.MovementPulse(Direction, untilNextBeat);
            GreenPulsed?.Invoke(next);
        }

        public void AdvanceDistance(double remaining)
        {
            Initialize();
            if (!enabled || remaining < 0 || double.IsNaN(remaining) || double.IsInfinity(remaining)) return;
            if (!useLegacyMovement && grid.RegularGreenCount == 0) return;
            int version = resetVersion;
            while (remaining >= 0 && enabled && version == resetVersion)
            {
                if (!grid.OccupiedHorizontalBounds(out float left, out float right)) return;
                if (right - left >= 2f * PlayerMovement.HalfWidth) return;
                double gap = Direction > 0 ? PlayerMovement.HalfWidth - right : left + PlayerMovement.HalfWidth;
                double distance = System.Math.Max(0, gap);
                if (remaining + 0.000001 < distance)
                {
                    Translate((float)(Direction * remaining));
                    return;
                }
                // Stop exactly at contact before notifying the descent/spawn listener.
                if (!Translate((float)(Direction * distance))) return;
                remaining = System.Math.Max(0, remaining - distance);
                Direction = -Direction;
                SweepEnded?.Invoke();
                if (remaining <= 0) return;
                // A callback may spawn, reindex, end the run, or reset the fleet.
                // Re-read occupancy before spending the frame's remaining movement.
                if (!useLegacyMovement && grid.RegularGreenCount == 0) return;
            }
        }

        private bool Translate(float distance)
        {
            var session = GetComponent<GameSession>();
            float fraction = session != null ? session.ContactFraction(distance) : 1;
            transform.position += Vector3.right * (distance * fraction);
            if (session != null) session.FinishContactMove(fraction);
            return enabled;
        }
    }
}
