using UnityEngine;
using UnityEngine.Events;

namespace CandyCruisers
{
    public sealed class PresentationCue : MonoBehaviour
    {
        public enum Kind { Fire, Match, Defeat, PlayerDefeat, PlayerMelt }
        [SerializeField, Min(.01f)] private float duration = .5f;
        [SerializeField] private bool tintSprites = true;
        [SerializeField] private GameObject artwork;
        public UnityEvent Started = new UnityEvent();
        public UnityEvent Completed = new UnityEvent();
        public EnemyColor Color { get; private set; }
        public int Depth { get; private set; }
        public int Multiplier { get; private set; }
        public bool ColorClear { get; private set; }
        public bool Finished { get; private set; }
        public float RemainingSeconds => Finished ? 0 : Mathf.Max(0, delay + duration - age);
        private float age, delay;
        private bool started;
        private SoundEffects sounds;
        private bool defeatSound;
        private EnemyDeathBurst burst;
        private PlayerDeathBurst playerBurst;
        private PlayerMeltDeath playerMelt;
        private LineRenderer ring;
        private UnityEngine.Color tint;

        public static PresentationCue Spawn(PresentationCue prefab, Kind kind, SpriteRenderer source, EnemyColor color, int depth, int? multiplier = null, bool colorClear = false)
        {
            var cue = prefab != null ? Instantiate(prefab, source.transform.position, Quaternion.identity) :
                new GameObject(kind + " cue").AddComponent<PresentationCue>();
            cue.transform.position = source.transform.position;
            cue.Color = color;
            cue.Depth = Mathf.Max(1, depth);
            cue.Multiplier = Mathf.Max(1, multiplier ?? cue.Depth);
            cue.ColorClear = colorClear;
            cue.sounds = source.GetComponentInParent<EnemyGrid>()?.GetComponent<SoundEffects>();
            cue.defeatSound = kind == Kind.Defeat;
            cue.delay = kind == Kind.Fire || kind == Kind.PlayerMelt ? 0 : (cue.Depth - 1) * EnemyDeathBurst.RingDelay;
            cue.tint = EnemyPalette.Get(color);
            if (prefab == null)
            {
                if (kind == Kind.PlayerDefeat)
                {
                    cue.playerBurst = PlayerDeathBurst.Create(source, cue.transform, cue.tint);
                    cue.duration = PlayerDeathBurst.Duration;
                }
                else if (kind == Kind.PlayerMelt)
                {
                    cue.playerMelt = PlayerMeltDeath.Create(source, cue.transform);
                    cue.duration = PlayerMeltDeath.Duration;
                }
                else if (kind == Kind.Defeat)
                {
                    cue.burst = EnemyDeathBurst.Create(source, color, depth, cue.Multiplier, colorClear);
                    cue.burst.transform.SetParent(cue.transform, true);
                    cue.duration = cue.burst.Duration;
                }
                else
                {
                    cue.duration = kind == Kind.Fire ? .16f : .22f;
                    cue.ring = new GameObject("Placeholder pulse", typeof(LineRenderer)).GetComponent<LineRenderer>();
                    cue.ring.transform.SetParent(cue.transform, false);
                    cue.ring.sharedMaterial = source.sharedMaterial;
                    cue.ring.useWorldSpace = false;
                    cue.ring.loop = true;
                    cue.ring.positionCount = 24;
                    cue.ring.startWidth = cue.ring.endWidth = .025f;
                    cue.ring.sortingOrder = source.sortingOrder + 8;
                }
            }
            else if (cue.tintSprites)
                foreach (var sprite in cue.GetComponentsInChildren<SpriteRenderer>(true)) sprite.color = cue.tint;
            cue.gameObject.SetActive(true);
            cue.Tick(0);
            return cue;
        }
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (Finished) return;
            age += Mathf.Max(0, seconds);
            bool visible = age >= delay;
            if (artwork != null) artwork.SetActive(visible);
            if (visible && !started)
            {
                started = true;
                if (defeatSound && sounds != null) sounds.PlayCue(SoundEffect.EnemyDefeat, SoundEffects.DefeatPitch(Multiplier));
                Started.Invoke();
            }
            if (playerBurst != null) playerBurst.Present(age - delay);
            if (playerMelt != null) playerMelt.Present(age - delay);
            if (ring != null)
            {
                ring.enabled = visible;
                float t = Mathf.Clamp01((age - delay) / duration);
                ring.startColor = ring.endColor = new UnityEngine.Color(tint.r, tint.g, tint.b, 1 - t);
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / ring.positionCount;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Mathf.Lerp(.08f, .4f, t));
                }
            }
            if (ColorClear && burst != null && !burst.Finished) return;
            if (age >= delay + duration) Finish();
        }
        // Safe for an Animation Event. The duration is a fallback for missing events.
        public void Finish()
        {
            if (Finished || age < delay) return;
            Finished = true;
            Completed.Invoke();
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
