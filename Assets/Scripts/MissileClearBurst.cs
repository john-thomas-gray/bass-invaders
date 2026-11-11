using UnityEngine;

namespace CandyCruisers
{
    // Detached visual only: the missile and its flight sound stop immediately.
    public sealed class MissileClearBurst : MonoBehaviour
    {
        public const float Duration = .28f;
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[7];
        private float age;
        public bool Finished { get; private set; }

        public static MissileClearBurst Create(Vector3 position, SpriteRenderer source)
        {
            var burst = new GameObject("Missile clear burst").AddComponent<MissileClearBurst>();
            burst.transform.position = position;
            for (int i = 0; i < burst.sparks.Length; i++)
            {
                var spark = new GameObject("Spark", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                spark.transform.SetParent(burst.transform, false);
                spark.sprite = EnemyPlaceholderArt.SpaceDust;
                if (source != null)
                {
                    spark.sharedMaterial = source.sharedMaterial;
                    spark.sortingLayerID = source.sortingLayerID;
                }
                spark.sortingOrder = (source != null ? source.sortingOrder : 25) + 5;
                burst.sparks[i] = spark;
            }
            burst.Tick(0);
            return burst;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            if (Finished) return;
            age += Mathf.Max(0, seconds);
            float progress = Mathf.Clamp01(age / Duration);
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / sparks.Length;
                var spark = sparks[i];
                spark.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (.025f + .3f * progress);
                spark.transform.localScale = Vector3.one * Mathf.Lerp(.12f, .015f, progress);
                var color = Color.Lerp(new Color(1, .95f, .65f), new Color(1, .35f, .08f), progress);
                color.a = 1 - progress;
                spark.color = color;
            }
            if (progress < 1) return;
            Finished = true;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
