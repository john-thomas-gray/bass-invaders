using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlayerLifeIcons
    {
        public const float Duration = 1.5f;
        public const float ApexProgress = .34f;
        public const float ApexSeconds = Duration * ApexProgress;
        public const float AnticipationSeconds = .2f;
        public const float AnticipationProgress = AnticipationSeconds / Duration;
        private readonly System.Random presentationRandom = new System.Random();
        public bool LossFlips { get; private set; }
        public bool LossHandedOff { get; private set; }
        public float LossRotation => LossFlips ? -360 * Mathf.SmoothStep(0, 1,
            Mathf.Clamp01((Age - AnticipationSeconds) / (ApexSeconds - AnticipationSeconds))) : 0;
        private const int LayoutSlots = 3;
        public int ConsumedSlot { get; private set; } = -1;
        public bool Active => ConsumedSlot >= 0;
        public bool Started => Active && Age > 0;
        public float Age { get; private set; }
        public float TravelProgress => Mathf.Clamp01(Age / Duration);
        public PlayerLifeGainAnimation Gain { get; } = new PlayerLifeGainAnimation();
        public LifeSlimeChoreography Choreography { get; } = new LifeSlimeChoreography();
        public EnemyColor? RewardColor { get; private set; }
        private readonly LifeSlimeArtwork artwork = new LifeSlimeArtwork();
        private readonly LifeSlimeArtwork travelingArtwork = new LifeSlimeArtwork();
        private readonly List<int> pendingGains = new List<int>();
        private readonly HashSet<int> reservedGains = new HashSet<int>();
        public bool Animating => Active || Gain.Active || pendingGains.Count > 0 || reservedGains.Count > 0 || Choreography.Flipping;

        public void BeginLoss(int slot, bool? flip = null)
        {
            ResetLoss();
            if (slot < 0 || slot >= PlayerMovement.MaxExtraLives) return;
            ConsumedSlot = slot;
            LossFlips = flip ?? presentationRandom.Next(3) == 0;
            pendingGains.RemoveAll(pending => pending >= slot);
            reservedGains.RemoveWhere(reserved => reserved >= slot);
            if (Gain.Active && Gain.Slot >= slot)
            {
                Gain.Reset();
                RewardColor = null;
            }
        }

        public void Reset()
        {
            ResetLoss(); Gain.Reset(); Choreography.Reset();
            RewardColor = null;
            pendingGains.Clear(); reservedGains.Clear();
        }
        public void ClearRewardColor() => RewardColor = null;
        public void Dispose() { artwork.Dispose(); travelingArtwork.Dispose(); }

        public void BeginGain(int slot, GameplayMusicPlayer music = null)
        {
            if (slot < 0 || slot >= PlayerMovement.MaxExtraLives || IsGainPending(slot)) return;
            pendingGains.Add(slot);
            StartNextGain(music);
        }

        public void ReserveGain(int slot)
        {
            if (slot >= 0 && slot < PlayerMovement.MaxExtraLives && !IsGainPending(slot)) reservedGains.Add(slot);
        }

        public void ScheduleWaveGain(int slot, GameplayMusicPlayer music, float spikeTime, int barIndex, int barCount,
            bool overlappingWave = false, EnemyColor? rewardColor = null)
        {
            if (slot < 0 || slot >= PlayerMovement.MaxExtraLives || IsGainPending(slot)) return;
            RewardColor = rewardColor;
            Gain.Begin(slot, music, spikeTime, barIndex, barCount, overlappingWave);
        }

        public void ReleaseGain(int slot, GameplayMusicPlayer music = null, float? appearanceBeat = null)
        {
            if (!reservedGains.Remove(slot)) return;
            BeginGain(slot, music);
        }

        public bool IsGainPending(int slot) => Gain.Active && Gain.Slot == slot ||
            pendingGains.Contains(slot) || reservedGains.Contains(slot);

        private void StartNextGain(GameplayMusicPlayer music)
        {
            if (Active || Gain.Active || pendingGains.Count == 0) return;
            int slot = pendingGains[0]; pendingGains.RemoveAt(0);
            Gain.Begin(slot, music);
        }

        public void HandOffLoss() => LossHandedOff = true;
        private void ResetLoss() { ConsumedSlot = -1; Age = 0; LossFlips = LossHandedOff = false; }

        public void Tick(float seconds, GameplayMusicPlayer music = null)
        {
            seconds = Mathf.Max(0, seconds);
            Choreography.Tick(seconds, music);
            if (Active)
            {
                Age = Mathf.Min(Duration, Age + seconds);
                if (Age >= Duration) ResetLoss();
            }
            Gain.Tick(seconds, music);
            StartNextGain(music);
        }

        public static float BeatScale(float beat) => 1 + .09f * GameplayMusicPlayer.BeatPulse(beat);
        public const float SizeScale = 1.3f;
        public static Rect RowRect(float x, float y, int viewHeight)
        {
            float size = (viewHeight < 400 ? 10 : viewHeight < 600 ? 14 : 18) * SizeScale;
            float center = viewHeight < 400 ? 46 : 50;
            return new Rect(x + 2, y + center - size / 2, 96, size);
        }
        public static float ProgressOffset(int viewHeight) => viewHeight < 400 ? 68 : viewHeight < 600 ? 76 : 78;
        public static Rect IconRect(Rect row, int slot, float scale = 1)
        {
            float cell = row.width / LayoutSlots;
            float size = Mathf.Min(20 * SizeScale, Mathf.Min(row.height, cell - 8));
            // Fixed spacing leaves room for the largest bonus without moving the icons on a beat.
            float pitch = size * 1.09f * MagicPowerPresentation.TextGrowthScale(EnemyPalette.Colors.Count, 0) + 4;
            var center = new Vector2(row.x + cell * .5f + slot * pitch, row.center.y);
            size *= scale;
            return new Rect(center.x - size / 2, center.y - size / 2, size, size);
        }

        public static Rect LossDisplayRect(Rect resting, Rect player, float progress, float? fieldCenterX = null)
        {
            float t = Mathf.Clamp01(progress);
            if (t <= AnticipationProgress) return resting;
            float inward = Mathf.MoveTowards(resting.center.x, fieldCenterX ?? player.center.x, player.width * .8f);
            var apex = new Vector2(inward,
                resting.center.y - Mathf.Min(resting.height * 1.2f, Mathf.Max(0, resting.center.y - player.height * .5f - 4)));
            Vector2 center, size;
            if (t <= ApexProgress)
            {
                float rise = Mathf.Clamp01((t - AnticipationProgress) / (ApexProgress - AnticipationProgress));
                center = Vector2.Lerp(resting.center, apex, Mathf.SmoothStep(0, 1, rise));
                size = Vector2.Lerp(resting.size, player.size, Mathf.SmoothStep(0, 1, rise));
            }
            else
            {
                float fall = (t - ApexProgress) / (1 - ApexProgress);
                center = Vector2.Lerp(apex, player.center, fall * fall);
                center.x = Mathf.Lerp(apex.x, player.center.x, fall);
                size = player.size;
            }
            return new Rect(center - size / 2, size);
        }

        public static float LossVerticalScale(float progress)
        {
            float t = Mathf.Clamp01(progress);
            if (t <= AnticipationProgress)
                return Mathf.Lerp(1, .5f, Mathf.SmoothStep(0, 1, t / (AnticipationProgress * .5f)));
            if (t < ApexProgress)
            {
                float rise = (t - AnticipationProgress) / (ApexProgress - AnticipationProgress);
                return rise < .4f ? Mathf.Lerp(.5f, 1.2f, Mathf.SmoothStep(0, 1, rise / .4f))
                    : Mathf.Lerp(1.2f, 1, Mathf.SmoothStep(0, 1, (rise - .4f) / .6f));
            }
            float fall = (t - ApexProgress) / (1 - ApexProgress);
            return fall < .8f ? 1 + .12f * Mathf.Sin(Mathf.PI * fall / .8f)
                : 1 - .12f * Mathf.Sin(Mathf.PI * (fall - .8f) / .2f);
        }

        public Vector3 ApexWorldPosition(PlayerMovement player)
        {
            var camera = Camera.main;
            if (camera == null) return new Vector3(player.transform.position.x, 4.7f, player.transform.position.z);
            float scale = GameSession.GuiScaleFor(Application.isMobilePlatform, camera.pixelWidth, camera.pixelHeight);
            float width = camera.pixelWidth / scale, height = camera.pixelHeight / scale;
            var left = camera.WorldToViewportPoint(new Vector3(-3, 5.3f));
            var row = RowRect(left.x * width + 10, (1 - left.y) * height, Mathf.RoundToInt(height));
            var destination = PlayerRect(player, width, height);
            var apex = LossDisplayRect(IconRect(row, Mathf.Max(0, ConsumedSlot)), destination, ApexProgress, width / 2);
            var playerPoint = camera.WorldToViewportPoint(player.transform.position);
            var rootOffset = new Vector2(playerPoint.x * width, (1 - playerPoint.y) * height) - destination.center;
            var origin = apex.center + rootOffset;
            var result = camera.ViewportToWorldPoint(new Vector3(origin.x / width, 1 - origin.y / height, playerPoint.z));
            result.z = player.transform.position.z;
            return result;
        }

        private Rect PlayerRect(PlayerMovement player, float width, float height)
        {
            var visuals = CharacterVisuals.Ensure(player.gameObject);
            artwork.Bind(visuals); travelingArtwork.Bind(visuals);
            if (Camera.main == null) return new Rect(width / 2 - 20, height * .9f, 40, 32);
            var body = visuals.HitBounds;
            var lo = Camera.main.WorldToViewportPoint(body.min);
            var hi = Camera.main.WorldToViewportPoint(body.max);
            return artwork.FromBodyRect(new Rect(lo.x * width, (1 - hi.y) * height,
                (hi.x - lo.x) * width, (hi.y - lo.y) * height));
        }

        public void Draw(PlayerMovement player, Rect row, float opacity) =>
            Draw(player, row, opacity, Screen.width, Screen.height);

        public void Draw(PlayerMovement player, Rect row, float opacity, float screenWidth, float screenHeight, float growth = 1)
        {
            Rect destination = PlayerRect(player, screenWidth, screenHeight);
            var color = player.AccentColor;
            Rect bars = new Rect(20, screenHeight - 20, screenWidth - 40, 5);
            if (Camera.main != null)
            {
                var camera = Camera.main;
                var left = camera.WorldToViewportPoint(new Vector3(-3, -5.25f));
                var right = camera.WorldToViewportPoint(new Vector3(3, -5.25f));
                bars = new Rect(left.x * screenWidth + 10, (1 - left.y) * screenHeight,
                    (right.x - left.x) * screenWidth - 20, 5);
            }
            for (int slot = 0; slot < PlayerMovement.MaxExtraLives; slot++)
            {
                bool consuming = Active && slot == ConsumedSlot;
                if (consuming && LossHandedOff) continue;
                if (!consuming && (slot >= player.ExtraLives || IsGainPending(slot))) continue;
                var rect = IconRect(row, slot, consuming ? 1 : Choreography.Scale);
                if (consuming)
                    DrawGrowing(travelingArtwork, LossDisplayRect(rect, destination, TravelProgress, screenWidth / 2), color, opacity, growth,
                        rotation: LossRotation, verticalScale: LossVerticalScale(TravelProgress));
                else
                {
                    rect.y -= Choreography.FlipHop * rect.height * .65f;
                    float angle = Choreography.FlipAngle * Mathf.Deg2Rad;
                    float fit = 1 / (Mathf.Abs(Mathf.Sin(angle)) + Mathf.Abs(Mathf.Cos(angle)));
                    rect = new Rect(rect.center - rect.size * fit / 2, rect.size * fit);
                    DrawGrowing(artwork, rect, color, opacity, growth, Choreography.Lean, Choreography.FlipAngle,
                        Choreography.FlipVerticalScale);
                }
            }
            if (Gain.Visible)
            {
                var clip = PlayerLifeGainAnimation.StageClip(bars, screenHeight);
                var rect = Gain.DisplayRect(artwork.Fit(IconRect(row, Gain.Slot)), bars, destination.size, screenWidth);
                rect.position -= clip.position;
                GUI.BeginGroup(clip);
                try { DrawGrowing(travelingArtwork, rect, color, opacity, growth, rotation: Gain.Rotation); }
                finally { GUI.EndGroup(); }
            }
        }

        private static void DrawGrowing(LifeSlimeArtwork artwork, Rect rect, Color color, float opacity, float growth,
            float lean = 0, float rotation = 0, float verticalScale = 1)
        {
            var previous = GUI.matrix;
            GUI.matrix = MagicPowerPresentation.ElementMatrix(previous, rect, growth);
            try { artwork.Draw(rect, color, opacity, lean, rotation, verticalScale); }
            finally { GUI.matrix = previous; }
        }
    }
}
