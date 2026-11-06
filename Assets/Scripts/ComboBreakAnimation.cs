using UnityEngine;

namespace CandyCruisers
{
    public sealed class ComboBreakAnimation
    {
        public const float TravelSeconds = .28f;
        public const float HoldSeconds = .3f;
        public const float DrainSeconds = .3f;
        public const float SlashSeconds = .12f;
        public const float SplitSeconds = .65f;
        private const float SlashStart = TravelSeconds + HoldSeconds;
        private const float SplitStart = SlashStart + SlashSeconds;
        public const float Duration = SplitStart + SplitSeconds;
        public int Multiplier { get; private set; }
        public float Age { get; private set; }
        public Color SourceColor { get; private set; } = Color.white;
        public bool Active => Multiplier > 1 && Age < Duration;
        public float DrainProgress => Mathf.Clamp01((Age - SplitStart) / DrainSeconds);
        public Color Tint => Color.Lerp(SourceColor, Color.white, Mathf.SmoothStep(0, 1, DrainProgress));
        public float SlashProgress => Mathf.Clamp01((Age - SlashStart) / SlashSeconds);
        public float SlashOpacity => Active && Age >= SlashStart ?
            1 - Mathf.Clamp01((Age - SplitStart) / .14f) : 0;
        public float SplitProgress => Mathf.Clamp01((Age - SplitStart) / SplitSeconds);
        public float Opacity => Active ? 1 - SplitProgress * SplitProgress : 0;

        public void Begin(int multiplier, Color color) { Multiplier = multiplier; SourceColor = color; Age = 0; }
        public void Reset() { Multiplier = 0; Age = 0; SourceColor = Color.white; }
        public void Tick(float seconds) => Age = Mathf.Min(Duration, Age + Mathf.Max(0, seconds));

        public Rect DisplayRect(Rect resting, float screenWidth, float screenHeight)
        {
            float travel = Mathf.SmoothStep(0, 1, Age / TravelSeconds);
            float scale = Mathf.Min(3, screenWidth * .8f / resting.width);
            var size = Vector2.Lerp(resting.size, resting.size * scale, travel);
            var center = Vector2.Lerp(resting.center, new Vector2(screenWidth / 2, screenHeight / 2), travel);
            return new Rect(center - size / 2, size);
        }

        public Vector2 HalfOffset(float height, bool lower)
        {
            float split = SplitProgress;
            float direction = lower ? 1 : -1;
            return new Vector2(direction * height * .6f * split,
                direction * height * .45f * split + height * split * split);
        }

        public void Draw(Rect resting, GUIStyle baseStyle, float opacity) =>
            Draw(resting, baseStyle, opacity, Screen.width, Screen.height);

        public void Draw(Rect resting, GUIStyle baseStyle, float opacity, float screenWidth, float screenHeight, Color? liveTint = null,
            float textGrowthScale = 1)
        {
            if (!Active) return;
            string text = "x" + Multiplier;
            var style = new GUIStyle(baseStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            while (style.fontSize > 9 && style.CalcSize(new GUIContent(text)).x > resting.width) style.fontSize--;
            int fontSize = style.fontSize;
            // Start at the right-aligned digits, so departing text doesn't jump left.
            float textWidth = Mathf.Min(resting.width, Mathf.Ceil(style.CalcSize(new GUIContent(text)).x));
            resting.x = resting.xMax - textWidth;
            resting.width = textWidth;
            var rect = DisplayRect(resting, screenWidth, screenHeight);
            style.fontSize = Mathf.RoundToInt(fontSize * rect.height / resting.height);
            var previous = GUI.color;
            var tint = liveTint ?? Tint;
            tint.a = opacity * Opacity;
            GUI.color = tint;
            if (Age < SplitStart)
            {
                var matrix = GUI.matrix;
                GUI.matrix = MagicPowerPresentation.ElementMatrix(matrix, rect, textGrowthScale);
                GUI.Label(rect, text, style);
                GUI.matrix = matrix;
            }
            else
            {
                DrawHalf(rect, text, style, false, textGrowthScale);
                DrawHalf(rect, text, style, true, textGrowthScale);
            }
            DrawSlash(rect, opacity);
            GUI.color = previous;
        }

        private void DrawSlash(Rect rect, float opacity)
        {
            if (SlashOpacity <= 0 || SlashProgress <= 0) return;
            var previous = GUI.matrix;
            var from = new Vector2(rect.x - rect.width * .2f, rect.y + rect.height * .752f);
            var to = new Vector2(rect.xMax + rect.width * .2f, rect.y + rect.height * .248f);
            var delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.color = new Color(1, 1, 1, opacity * SlashOpacity);
            float thickness = Mathf.Max(2, rect.height * .035f);
            var slash = new Rect(from.x, from.y - thickness / 2, delta.magnitude * SlashProgress, thickness);
            GUI.DrawTexture(slash, Texture2D.whiteTexture);
            GUI.matrix = previous;
        }

        private void DrawHalf(Rect rect, string text, GUIStyle style, bool lower, float textGrowthScale)
        {
            rect.position += HalfOffset(rect.height, lower);
            var matrix = GUI.matrix;
            var half = new Rect(rect.x, rect.y + (lower ? rect.height / 2 : 0), rect.width, rect.height / 2);
            GUI.matrix = MagicPowerPresentation.ElementMatrix(matrix, half, textGrowthScale);
            int strips = Mathf.Clamp(Mathf.CeilToInt(rect.width), 16, 512);
            // Pixel-aligned clips keep the diagonal halves solid as they slide apart.
            for (int i = 0; i < strips; i++)
            {
                float cut = rect.height * Mathf.Lerp(.68f, .32f, (i + .5f) / strips);
                float top = lower ? cut + 1 : 0;
                float height = lower ? rect.height - top : cut - 1;
                float left = Mathf.Round(rect.x + rect.width * i / strips) - rect.x;
                float right = Mathf.Round(rect.x + rect.width * (i + 1) / strips) - rect.x;
                GUI.BeginGroup(new Rect(rect.x + left, rect.y + top, right - left, height));
                GUI.Label(new Rect(-left, -top, rect.width, rect.height), text, style);
                GUI.EndGroup();
            }
            GUI.matrix = matrix;
        }
    }
}
