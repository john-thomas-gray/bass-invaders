using UnityEngine;

namespace CandyCruisers
{
    public static class EnemyBeatSprites
    {
        public const string ResourceName = "EnemyBeatFrames";
        private static Sprite regularA, regularB, specialA, specialB;
        private static bool loaded;

        public static Sprite FrameAt(Sprite appearance, float beatPosition)
        {
            if (appearance == null) return null;
            Load();
            bool alternate = (Mathf.FloorToInt(beatPosition) & 1) != 0;
            // Follow the displayed design so Yellow's copied appearance animates too.
            if (appearance == regularA || appearance == regularB)
                return alternate ? regularB : regularA;
            if (appearance == specialA || appearance == specialB)
                return alternate ? specialB : specialA;
            return appearance;
        }

        private static void Load()
        {
            if (loaded) return;
            foreach (var sprite in Resources.LoadAll<Sprite>(ResourceName))
            {
                switch (sprite.name)
                {
                    case "Regular A": regularA = sprite; break;
                    case "Regular B": regularB = sprite; break;
                    case "Special A": specialA = sprite; break;
                    case "Special B": specialB = sprite; break;
                }
            }
            loaded = true;
        }
    }
}
