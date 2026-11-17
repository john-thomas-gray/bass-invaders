using UnityEngine;

namespace CandyCruisers
{
    // A detached visual snapshot: no occupancy, ability, scoring or collision behavior.
    public sealed class EnemyDeathBurst : MonoBehaviour
    {
        public const float RingDelay = .065f;
        public const float BurstSeconds = .5f;
        public const float ColorClearScatterSeconds = .4f;
        public const float ColorClearStreamStaggerSeconds = .45f;
        public const float ColorClearReturnSpeed = 11f;
        public const float ColorClearMaxSeconds = 6f;
        private SpriteRenderer snapshot;
        private TextMesh digit;
        private SpriteRenderer[] shards;
        private float[] dustSizes;
        private bool[] dustReturning;
        private bool[] dustAbsorbed;
        private Color tint;
        private float age;
        private bool appliedAbsorption;
        private PlayerMovement colorClearPlayer;
        private Transform colorClearTarget;
        public int Depth { get; private set; }
        public int Multiplier { get; private set; }
        public bool ColorClear { get; private set; }
        public float Duration => ColorClear ? 1.15f : BurstSeconds;
        public bool Exploded => age >= (Depth - 1) * RingDelay;
        public bool Finished { get; private set; }

        public static EnemyDeathBurst Create(SpriteRenderer source, EnemyColor color, int depth, int? multiplier = null, bool colorClear = false)
        {
            var root = new GameObject("Enemy death " + depth);
            root.transform.position = source.transform.position;
            var effect = root.AddComponent<EnemyDeathBurst>();
            effect.Depth = Mathf.Max(1, depth);
            effect.Multiplier = Mathf.Max(1, multiplier ?? effect.Depth);
            effect.ColorClear = colorClear;
            effect.tint = EnemyPalette.Get(color);
            var copy = new GameObject("Dying body", typeof(SpriteRenderer));
            copy.transform.SetParent(root.transform, false);
            copy.transform.localScale = source.transform.lossyScale;
            copy.transform.rotation = source.transform.rotation;
            effect.snapshot = copy.GetComponent<SpriteRenderer>();
            effect.snapshot.sprite = source.sprite;
            effect.snapshot.sharedMaterial = EnemyPresentation.StableEffectMaterial(source);
            effect.snapshot.sortingOrder = source.sortingOrder;
            effect.snapshot.color = effect.tint;
            var label = new GameObject("Connection depth", typeof(TextMesh));
            label.transform.SetParent(root.transform, false);
            effect.digit = label.GetComponent<TextMesh>();
            effect.digit.text = effect.Multiplier == 1 ? "" : effect.Multiplier.ToString();
            effect.digit.font = Resources.Load<Font>("Fonts/Bungee-Regular");
            effect.digit.fontSize = 64;
            effect.digit.characterSize = .08f;
            effect.digit.fontStyle = FontStyle.Normal;
            effect.digit.anchor = TextAnchor.MiddleCenter;
            effect.digit.alignment = TextAlignment.Center;
            effect.digit.color = effect.tint;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = effect.digit.font.material;
            renderer.sortingOrder = source.sortingOrder + 6;
            effect.shards = new SpriteRenderer[colorClear ? 32 : 7];
            effect.dustSizes = new float[effect.shards.Length];
            if (colorClear)
            {
                effect.dustReturning = new bool[effect.shards.Length];
                effect.dustAbsorbed = new bool[effect.shards.Length];
            }
            for (int i = 0; i < effect.shards.Length; i++)
            {
                var shard = new GameObject(colorClear ? "Color-clear spacedust" : "Shard", typeof(SpriteRenderer));
                shard.transform.SetParent(root.transform, false);
                var sprite = shard.GetComponent<SpriteRenderer>();
                sprite.sprite = colorClear ? EnemyPlaceholderArt.SpaceDust : EnemyPlaceholderArt.Triangle;
                sprite.sharedMaterial = EnemyPresentation.StableEffectMaterial(source);
                sprite.sortingLayerID = source.sortingLayerID;
                sprite.sortingOrder = colorClear ? source.sortingOrder - 5 : source.sortingOrder + 5;
                effect.shards[i] = sprite;
                if (colorClear) effect.dustSizes[i] = EnemyPlaceholderArt.RandomDustSize(.55f);
            }
            if (effect.ColorClear) effect.BindColorClearTarget();
            effect.Tick(0);
            return effect;
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (Finished) return;
            float previousBurstAge = age - (Depth - 1) * RingDelay;
            age += Mathf.Max(0, seconds);
            float burstAge = age - (Depth - 1) * RingDelay;
            snapshot.enabled = burstAge < 0;
            digit.gameObject.SetActive(burstAge >= 0);
            float progress = Mathf.Clamp01(burstAge / BurstSeconds);
            float dustProgress = Mathf.Clamp01(burstAge / Duration);
            digit.color = new Color(tint.r, tint.g, tint.b, 1 - progress * progress);
            digit.transform.localScale = Vector3.one * Mathf.Lerp(.5f, 1.15f, Mathf.Min(1, progress * 5));
            digit.transform.localPosition = Vector3.up * progress * .12f;
            bool allAbsorbed = ColorClear;
            for (int i = 0; i < shards.Length; i++)
            {
                var shard = shards[i];
                shard.enabled = burstAge >= 0;
                if (ColorClear)
                {
                    UpdateDustTravel(i, previousBurstAge, burstAge, dustProgress);
                    shard.enabled = burstAge >= 0 && !dustAbsorbed[i];
                    allAbsorbed &= dustAbsorbed[i];
                    float shimmer = .5f + .5f * Mathf.Sin(burstAge * (8 + i % 5) + i * 2.399963f);
                    float size = dustSizes[i] * Mathf.Lerp(.9f, 1.1f, shimmer);
                    shard.transform.localScale = new Vector3(size, size, 1);
                    var color = EnemyPalette.Get(EnemyPalette.CycleColor(i));
                    // Shimmer without fading or shrinking away before reaching the player.
                    float alpha = Mathf.Lerp(.7f, 1, shimmer);
                    shard.color = new Color(color.r, color.g, color.b, alpha);
                    continue;
                }
                float angle = i * Mathf.PI * 2 / shards.Length;
                shard.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (.06f + progress * .3f);
                shard.transform.localScale = Vector3.one * (.07f * (1 - progress));
                shard.transform.localRotation = Quaternion.Euler(0, 0, i * 51 + progress * 120);
                shard.color = new Color(tint.r, tint.g, tint.b, 1 - progress);
            }
            if (ColorClear && colorClearTarget != null)
            {
                if (!allAbsorbed && burstAge < ColorClearMaxSeconds) return;
            }
            else if (burstAge < Duration) return;
            if (ColorClear) CompleteAbsorption();
            Finished = true;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
        }

        private void BindColorClearTarget()
        {
            colorClearPlayer = FindFirstObjectByType<PlayerMovement>();
            if (colorClearPlayer == null) return;
            colorClearTarget = colorClearPlayer.MagicAbsorptionTarget != null ?
                colorClearPlayer.MagicAbsorptionTarget : colorClearPlayer.transform;
        }

        private Vector3 DriftPosition(int index, float dustProgress)
        {
            float dustAngle = index * 2.399963f;
            float speed = .65f + (index * 7 % 13) * .065f;
            float spread = (1 - Mathf.Pow(1 - dustProgress, 3)) * speed;
            return new Vector3(Mathf.Cos(dustAngle), Mathf.Sin(dustAngle), 0) * spread
                + Vector3.down * dustProgress * dustProgress * .16f;
        }

        private void UpdateDustTravel(int index, float previousBurstAge, float burstAge, float dustProgress)
        {
            if (dustAbsorbed[index]) return;
            var shard = shards[index].transform;
            float returnAt = ColorClearScatterSeconds + (index * 13 % shards.Length) /
                (float)(shards.Length - 1) * ColorClearStreamStaggerSeconds;
            if (colorClearTarget == null || burstAge < returnAt)
            {
                shard.localPosition = DriftPosition(index, dustProgress);
                return;
            }
            if (!dustReturning[index])
            {
                dustReturning[index] = true;
                shard.localPosition = DriftPosition(index, returnAt / Duration);
            }
            // Each grain covers the same distance per second, without a shared arrival deadline.
            float travelSeconds = Mathf.Max(0, burstAge - Mathf.Max(previousBurstAge, returnAt));
            var target = colorClearTarget.position;
            shard.position = Vector3.MoveTowards(shard.position, target, ColorClearReturnSpeed * travelSeconds);
            dustAbsorbed[index] = (shard.position - target).sqrMagnitude < .000001f;
        }

        private void CompleteAbsorption()
        {
            if (appliedAbsorption) return;
            appliedAbsorption = true;
            colorClearPlayer?.CompleteMagicAbsorption();
        }
    }
}
