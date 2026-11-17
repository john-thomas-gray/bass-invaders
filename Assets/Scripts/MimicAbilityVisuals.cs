using UnityEngine;

namespace CandyCruisers
{
    // Shared presentation palette and world-aligned coverage for copied abilities.
    public static class MimicAbilityVisuals
    {
        private static Material material;
        public static Material Material => material != null ? material : material = new Material(
            Resources.Load<Shader>("MimicAbility")) { name = "Yellow copied ability" };
        public static Color ShieldBlue => EnemyPalette.Get(EnemyColor.Blue);
        public static Color ShieldYellow => EnemyPalette.Get(EnemyColor.Yellow);

        public static void Configure(MaterialPropertyBlock block, EnemyAbilities ability, Vector3 center, bool gradient)
        {
            int mask = ability.MimicHostMask;
            block.SetColor("_HostColor", EnemyPalette.Get(ability.EffectColor));
            block.SetColor("_YellowColor", ShieldYellow);
            block.SetVector("_HostSides", new Vector4((mask & 1) != 0 ? 1 : 0, (mask & 2) != 0 ? 1 : 0,
                (mask & 4) != 0 ? 1 : 0, (mask & 8) != 0 ? 1 : 0));
            block.SetVector("_EffectCenter", center);
            block.SetFloat("_Split", ability.IsDisguised ? 0 : 1);
            block.SetFloat("_Gradient", gradient ? 1 : 0);
        }

        public static Color ShieldBand(Color tint, float progress)
        {
            // Dark outside keyline, bright edge, saturated band, then a clean inward fade.
            if (progress < .0625f) return Color.Lerp(Color.black, tint, .22f);
            if (progress < .1875f) return Color.Lerp(tint, Color.white, .7f);
            var color = Color.Lerp(tint, Color.black, Mathf.Clamp01((progress - .25f) * .9f));
            color.a = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .375f) / .625f));
            return color;
        }
    }
}
