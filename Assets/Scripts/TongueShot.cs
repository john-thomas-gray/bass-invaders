using UnityEngine;

namespace CandyCruisers
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TongueShot : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float extendSpeed = 14f;
        [SerializeField, Min(0.1f)] private float retractSpeed = 20f;
        public const float NormalHitRadius = .055f;
        public const float MagicHitRadius = .11f;
        public const float TipBulbWidthMultiplier = 2.2f;
        private LineRenderer line;
        private SpriteRenderer tipBulb;
        private static Sprite tipBulbSprite;
        private float travel;
        private float maximumLength;
        private float normalStartWidth, normalEndWidth;
        private bool widthInitialized;
        private float deflectedRetractSpeed;
        public const float ShieldShockSeconds = 60f / GameplayMusicPlayer.DefaultBeatsPerMinute;
        private float shockAge, shockDuration;
        private LineRenderer shockGlow, shockCore;
        public bool ShockActive => Active && shockAge < shockDuration;
        public float ShockProgress => shockDuration > 0 ? Mathf.Clamp01(shockAge / shockDuration) : 1;
        public float ShockDuration => shockDuration;
        public event System.Action ShieldShockArrived;
        private EnemyColor? returnPreviewColor;
        private bool returnPreviewMagic;
        public bool IsDeflected => Active && deflectedRetractSpeed > 0;
        // legacy-v1 Tongue.cs: maxSpeed 20, speedFactor = -0.25 + 0.01 * level.
        public static float DeflectedReturnSpeed(int level) => 20f * Mathf.Max(.01f, .25f - .01f * Mathf.Max(1, level));
        public bool Active { get; private set; }
        public bool LastCompletionCancelled { get; private set; }
        public bool HitRegularShield { get; private set; }
        public bool Retracting => Active && travel >= maximumLength;
        public float Length => Active ? (Retracting ? Mathf.Max(0, 2 * maximumLength - travel) : travel) : 0;
        public float ReturnSpeed => deflectedRetractSpeed > 0 ? deflectedRetractSpeed : retractSpeed * (IsMagic ? 4 : 1);
        public float RemainingReturnSeconds => !Active ? 0 :
            Mathf.Max(0, Retracting ? 2 * maximumLength - travel : maximumLength) / Mathf.Max(.0001f, ReturnSpeed);
        public EnemyColor ShotColor { get; private set; }
        public bool IsMagic { get; private set; }
        public int MagicMultiplier { get; private set; }
        public event System.Action ExtensionStarted;
        public event System.Action RetractionStarted;
        public event System.Action MotionUpdated;
        public event System.Action Finished;
        public event System.Action Deflected;
        public SpriteRenderer TipBulb => tipBulb;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.enabled = false;
            EnsureTipBulb();
        }

        private void EnsureTipBulb()
        {
            if (tipBulb != null) return;
            var existing = transform.Find("Tongue tip bulb");
            var bulbObject = existing != null ? existing.gameObject : new GameObject("Tongue tip bulb");
            bulbObject.transform.SetParent(transform, false);
            tipBulb = bulbObject.GetComponent<SpriteRenderer>();
            if (tipBulb == null) tipBulb = bulbObject.AddComponent<SpriteRenderer>();
            tipBulb.sprite = TipBulbSprite;
            tipBulb.sharedMaterial = line.sharedMaterial;
            tipBulb.sortingLayerID = line.sortingLayerID;
            tipBulb.sortingOrder = line.sortingOrder + 1;
            tipBulb.enabled = false;
        }

        private static Sprite TipBulbSprite
        {
            get
            {
                if (tipBulbSprite != null) return tipBulbSprite;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Tongue tip bulb", wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
                };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2((x + .5f) / size * 2 - 1,
                        (y + .5f) / size * 2 - 1).magnitude;
                    float alpha = Mathf.Clamp01((1 - radius) * size * .5f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                texture.Apply(false, true);
                tipBulbSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size,
                    0, SpriteMeshType.FullRect);
                tipBulbSprite.name = texture.name;
                tipBulbSprite.hideFlags = HideFlags.HideAndDontSave;
                return tipBulbSprite;
            }
        }
        public bool TryFire(EnemyColor color, float length, bool magic = false)
        {
            if (Active || length <= 0) return false;
            if (line == null) line = GetComponent<LineRenderer>();
            if (!widthInitialized)
            {
                normalStartWidth = line.startWidth;
                normalEndWidth = line.endWidth;
                widthInitialized = true;
            }
            line.startWidth = normalStartWidth * (magic ? 2 : 1);
            line.endWidth = normalEndWidth * (magic ? 2 : 1);
            ShotColor = color;
            IsMagic = magic;
            MagicMultiplier = 0;
            deflectedRetractSpeed = 0;
            ResetShock();
            returnPreviewColor = null;
            maximumLength = length;
            travel = 0;
            Active = true;
            LastCompletionCancelled = false;
            HitRegularShield = false;
            SetVisualColor(color, magic);
            Draw();
            ExtensionStarted?.Invoke();
            return true;
        }

        public void SetReturnColor(EnemyColor color, bool magic)
        {
            if (IsDeflected) return;
            if (!Retracting || returnPreviewColor == color && returnPreviewMagic == magic) return;
            returnPreviewColor = color;
            returnPreviewMagic = magic;
            SetVisualColor(color, magic);
        }

        private void SetVisualColor(EnemyColor color, bool magic)
        {
            var plain = new Gradient();
            plain.SetKeys(new[] { new GradientColorKey(EnemyPalette.Get(color), 0), new GradientColorKey(EnemyPalette.Get(color), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            line.colorGradient = plain;
            if (magic)
            {
                var gradient = new Gradient();
                gradient.SetKeys(new[] {
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Red), 0),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Yellow), .25f),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Green), .5f),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Blue), .75f),
                    new GradientColorKey(EnemyPalette.Get(EnemyColor.Purple), 1)
                }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                line.colorGradient = gradient;
            }
        }

        public void Tick(float seconds, EnemyGrid grid = null)
        {
            if (!Active || seconds <= 0) return;
            bool wasRetracting = Retracting;
            float extensionRate = extendSpeed * (IsMagic ? 4 : 1);
            if (grid != null && grid.Fleet.Count == 0 && !Retracting)
            {
                maximumLength = Mathf.Max(.0001f, Length);
                travel = maximumLength;
            }
            if (!Retracting)
            {
                float remaining = (maximumLength - travel) / extensionRate;
                float extending = Mathf.Min(seconds, remaining);
                float nextLength = travel + extending * extensionRate;
                if (IsMagic && grid != null)
                {
                    // Resolve every crossed enemy, even during a long frame. Each hit removes
                    // its same-color chain before the next nearest contact is queried.
                    bool clearedFleet = false;
                    while (grid.FindMatchingHit(transform.position, travel, nextLength,
                        ShotColor, MagicHitRadius, out int magicId, out float magicHitLength, true))
                    {
                        MagicMultiplier += grid.ClearMagicChain(magicId, MagicMultiplier);
                        if (!Active) return;
                        if (grid.Fleet.Count == 0)
                        {
                            seconds -= (magicHitLength - travel) / extensionRate;
                            maximumLength = Mathf.Max(.0001f, magicHitLength);
                            travel = maximumLength;
                            clearedFleet = true;
                            break;
                        }
                    }
                    if (!clearedFleet)
                    {
                        travel = nextLength;
                        seconds -= extending;
                    }
                }
                else
                {
                    bool stopped = false;
                    // Revealed Yellows cease matching this shot; keep sweeping the same segment.
                    while (grid != null && grid.FindMatchingHit(transform.position, travel, nextLength,
                        ShotColor, NormalHitRadius, out int id, out float hitLength))
                    {
                        var result = grid.ResolveTongueHit(id, ShotColor);
                        if (!Active) return;
                        if (result == EnemyGrid.TongueHitResult.PassThrough) continue;
                        HitRegularShield = result == EnemyGrid.TongueHitResult.ShieldAbsorbed;
                        seconds -= (hitLength - travel) / extensionRate;
                        if (result == EnemyGrid.TongueHitResult.Deflected)
                        {
                            deflectedRetractSpeed = DeflectedReturnSpeed(grid.GetComponent<GameSession>()?.Progress.Level ?? 1);
                            BeginShock(hitLength, grid.GetComponent<GameplayMusicPlayer>());
                            Deflected?.Invoke();
                        }
                        if (!Active) return;
                        maximumLength = Mathf.Max(.0001f, hitLength);
                        travel = maximumLength;
                        stopped = true;
                        break;
                    }
                    if (!stopped)
                    {
                        travel = nextLength;
                        seconds -= extending;
                    }
                }
            }
            if (!wasRetracting && Retracting) RetractionStarted?.Invoke();
            if (!Active) return;
            float returnRate = ReturnSpeed;
            AdvanceShock(seconds);
            if (seconds > 0) travel += seconds * returnRate;
            if (travel >= 2 * maximumLength)
            {
                LastCompletionCancelled = false;
                Active = false;
                Finished?.Invoke();
            }
            Draw();
        }

        private void BeginShock(float length, GameplayMusicPlayer music)
        {
            shockAge = 0;
            float beat = music != null ? music.SecondsForBeats(1) : ShieldShockSeconds;
            shockDuration = Mathf.Min(Mathf.Max(.0001f, beat), Mathf.Max(.0001f, length / ReturnSpeed));
            if (shockGlow == null) shockGlow = CreateShockLine("Shield shock glow", .15f, new Color(.2f, .65f, 1, .85f), 1);
            if (shockCore == null) shockCore = CreateShockLine("Shield shock core", .045f, new Color(.88f, .97f, 1), 2);
        }
        private LineRenderer CreateShockLine(string name, float width, Color tint, int order)
        {
            var child = new GameObject(name, typeof(LineRenderer));
            child.transform.SetParent(transform, false);
            var renderer = child.GetComponent<LineRenderer>();
            renderer.sharedMaterial = line.sharedMaterial;
            renderer.useWorldSpace = false;
            renderer.positionCount = 11;
            renderer.startWidth = renderer.endWidth = width;
            renderer.startColor = renderer.endColor = tint;
            renderer.sortingLayerID = line.sortingLayerID;
            renderer.sortingOrder = line.sortingOrder + order;
            renderer.numCapVertices = 3;
            renderer.enabled = false;
            return renderer;
        }
        private void AdvanceShock(float seconds)
        {
            if (!ShockActive) return;
            shockAge = Mathf.Min(shockDuration, shockAge + Mathf.Max(0, seconds));
            if (shockAge >= shockDuration)
            {
                line.startColor = line.endColor = Color.gray;
                ShieldShockArrived?.Invoke();
            }
        }
        private void DrawShock()
        {
            if (shockGlow == null || shockCore == null) return;
            shockGlow.enabled = shockCore.enabled = ShockActive;
            if (!ShockActive) return;
            float front = Mathf.Clamp01(1 - ShockProgress);
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(EnemyPalette.Get(ShotColor), 0),
                new GradientColorKey(EnemyPalette.Get(ShotColor), Mathf.Max(0, front - .015f)),
                new GradientColorKey(Color.gray, Mathf.Min(1, front + .015f)),
                new GradientColorKey(Color.gray, 1)
            }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            line.colorGradient = gradient;
            float head = Length * (1 - ShockProgress);
            float tail = Mathf.Min(Length, head + .8f);
            for (int i = 0; i < 11; i++)
            {
                float t = i / 10f;
                float zigzag = i == 0 || i == 10 ? 0 : (i % 2 == 0 ? 1 : -1) *
                    (.035f + .025f * Mathf.Sin(i * 7 + ShockProgress * 40));
                var point = new Vector3(zigzag, Mathf.Lerp(head, tail, t), -.01f);
                shockGlow.SetPosition(i, point); shockCore.SetPosition(i, point);
            }
        }
        private void ResetShock()
        {
            shockAge = shockDuration = 0;
            if (shockGlow != null) shockGlow.enabled = false;
            if (shockCore != null) shockCore.enabled = false;
        }

        public void Draw()
        {
            if (line == null) line = GetComponent<LineRenderer>();
            EnsureTipBulb();
            line.enabled = Active;
            line.positionCount = IsMagic || Retracting && returnPreviewMagic || ShockActive ? 33 : 2;
            for (int i = 0; i < line.positionCount; i++)
                line.SetPosition(i, Vector3.up * (Length * i / (line.positionCount - 1)));
            tipBulb.enabled = Active;
            if (Active)
            {
                float diameter = line.endWidth * TipBulbWidthMultiplier;
                tipBulb.transform.localPosition = new Vector3(0, Length + diameter * .2f, -.001f);
                tipBulb.transform.localScale = Vector3.one * diameter;
            }
            DrawShock();
            if (Active) tipBulb.color = line.colorGradient.Evaluate(1);
            MotionUpdated?.Invoke();
        }
        public void Cancel(bool completed = false)
        {
            bool wasActive = Active;
            LastCompletionCancelled = wasActive && !completed;
            Active = false;
            deflectedRetractSpeed = 0;
            ResetShock();
            if (line != null) line.enabled = false;
            if (tipBulb != null) tipBulb.enabled = false;
            if (wasActive) Finished?.Invoke();
        }
        private void OnDisable() => Cancel();
    }
}
