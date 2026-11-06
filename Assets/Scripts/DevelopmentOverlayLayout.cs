using UnityEngine;

namespace CandyCruisers
{
    public static class DevelopmentOverlayLayout
    {
        public struct Rects
        {
            public Rect Clock, Metronome;
        }

        private const float Height = 24, Gap = 4, Margin = 8, MinimumWidth = 64;

        // All returned rectangles use the unpulsed graphical user interface coordinate space.
        // Prefer unused gutters; compact readouts omit their captions when space is narrow.
        public static Rects Calculate(Vector2 screen, Rect safeArea, float scale, Rect playfieldPixels,
            bool clock, bool metronome)
        {
            if (!clock && !metronome) return default;
            scale = Mathf.Max(.0001f, scale);
            var safe = new Rect(safeArea.x / scale + Margin, (screen.y - safeArea.yMax) / scale + Margin,
                Mathf.Max(0, safeArea.width / scale - Margin * 2), Mathf.Max(0, safeArea.height / scale - Margin * 2));
            var field = new Rect(playfieldPixels.x / scale, (screen.y - playfieldPixels.yMax) / scale,
                playfieldPixels.width / scale, playfieldPixels.height / scale);
            if (field.width <= 0 || field.height <= 0)
            {
                Rects unblocked = default;
                float score = -1;
                Consider(safe, clock, metronome, ref unblocked, ref score);
                return unblocked;
            }

            var right = new Rect(Mathf.Max(safe.xMin, field.xMax + Margin), safe.yMin,
                Mathf.Max(0, safe.xMax - Mathf.Max(safe.xMin, field.xMax + Margin)), safe.height);
            var left = new Rect(safe.xMin, safe.yMin,
                Mathf.Max(0, Mathf.Min(safe.xMax, field.xMin - Margin) - safe.xMin), safe.height);
            var bottom = new Rect(safe.xMin, Mathf.Max(safe.yMin, field.yMax + Margin), safe.width,
                Mathf.Max(0, safe.yMax - Mathf.Max(safe.yMin, field.yMax + Margin)));
            var top = new Rect(safe.xMin, safe.yMin, safe.width,
                Mathf.Max(0, Mathf.Min(safe.yMax, field.yMin - Margin) - safe.yMin));
            Rects best = default;
            float bestScore = -1;
            Consider(right, clock, metronome, ref best, ref bestScore);
            Consider(left, clock, metronome, ref best, ref bestScore);
            Consider(bottom, clock, metronome, ref best, ref bestScore);
            Consider(top, clock, metronome, ref best, ref bestScore);
            return best;
        }

        private static void Consider(Rect free, bool clock, bool metronome, ref Rects best, ref float bestScore)
        {
            if (free.width <= 0 || free.height <= 0) return;
            int count = (clock ? 1 : 0) + (metronome ? 1 : 0);
            for (int mode = 0; mode < 2; mode++)
            {
                bool vertical = mode == 1;
                float minimumWidth = vertical ? MinimumWidth : MinimumWidth * count + Gap * (count - 1);
                float height = vertical ? Height * count + Gap * (count - 1) : Height;
                float fit = Mathf.Min(1, Mathf.Min(free.width / minimumWidth, free.height / height));
                float preferred = vertical ? (metronome ? 158 : 100) : (clock ? 100 : 0) + (metronome ? 158 : 0) + Gap * (count - 1);
                float score = fit * 10 + Mathf.Min(1, free.width / (preferred * fit));
                if (score <= bestScore) continue;
                best = Fit(free, clock, metronome, vertical, fit);
                bestScore = score;
            }
        }

        private static Rects Fit(Rect free, bool clock, bool metronome, bool vertical, float fit)
        {
            var result = new Rects();
            int count = (clock ? 1 : 0) + (metronome ? 1 : 0);
            float gap = Gap * fit, height = Height * fit;
            if (vertical)
            {
                float width = Mathf.Min(free.width, (metronome ? 158 : 100) * fit);
                float y = free.yMin;
                if (clock) { result.Clock = new Rect(free.xMax - width, y, width, height); y += height + gap; }
                if (metronome) result.Metronome = new Rect(free.xMax - width, y, width, height);
            }
            else
            {
                float width = Mathf.Min(free.width, ((clock ? 100 : 0) + (metronome ? 158 : 0)) * fit + gap * (count - 1));
                float x = free.xMax - width;
                float clockWidth = !metronome ? width : !clock ? 0 :
                    MinimumWidth * fit + Mathf.Max(0, width - gap - MinimumWidth * fit * 2) * (100 - MinimumWidth) / (258 - MinimumWidth * 2);
                if (clock) { result.Clock = new Rect(x, free.yMin, clockWidth, height); x += clockWidth + gap; }
                if (metronome) result.Metronome = new Rect(x, free.yMin, free.xMax - x, height);
            }
            return result;
        }
    }
}
