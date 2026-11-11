using UnityEngine;
using UnityEngine.Serialization;

namespace CandyCruisers
{
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class GameplayFraming : MonoBehaviour
    {
        private const int StarfieldCount = 10;
        public const float PlayfieldBoundaryTopY = 5.5f;
        public const float IphoneNotchDefaultTopInsetRatio = .055f;
        public const float IphoneNotchMinimumTopInsetRatio = .025f;
        public const float IphoneNotchMaxPlayfieldTopViewportY = .955f;
        public const float IphoneNotchPortraitAspectLimit = .75f;
        public const float IphoneNotchFallbackAspectLimit = .52f;
        public const float WaveCrunchCompression = .084f;
        private float waveCrunch;
        public float WaveCrunchScale => 1 - WaveCrunchCompression * waveCrunch;
#if UNITY_EDITOR
        public static bool ForceIphoneNotchOffsetForTests { get; set; }
        public static float SimulatedIphoneTopInsetRatioForTests { get; set; } = IphoneNotchDefaultTopInsetRatio;
#endif

        public void SetWaveCrunch(float amount)
        {
            waveCrunch = Mathf.Clamp01(amount);
            Refresh();
        }
        [SerializeField, FormerlySerializedAs("solidWhiteBackground")] private bool solidBlackBackground = true;
        public bool SolidBlackBackground { get => solidBlackBackground; set { solidBlackBackground = value; Refresh(); } }
        [SerializeField] private SpriteRenderer starBackground;
        [SerializeField, Min(0f)] private float backgroundDrift = 0.16f;
        [SerializeField, Min(0f)] private float backgroundCyclesPerSecond = 0.035f;
        [SerializeField, Min(0f)] private float baseSpinDegreesPerSecond = 2f;
        [SerializeField, Min(0f)] private float spinDegreesPerLevel = 1.25f;
        [SerializeField, Min(0.01f)] private float levelCrossFadeSeconds = 1.2f;
        [SerializeField, Min(0.01f)] private float wavePulseSeconds = 1.2f;
        private Camera view;
        private GameSession session;
        private GameSession subscribedSession;
        private SpriteRenderer crossFadeBackground;
        private Sprite[] starfields;
        private Texture2D[] starfieldTextures;
        private Vector3 cameraHome;
        private Vector3 backgroundHome;
        private Quaternion backgroundHomeRotation = Quaternion.identity;
        private bool cameraHomeCaptured;
        private bool backgroundHomeCaptured;
        private float backgroundPhase;
        private float spinAngle;
        private float levelCrossFadeRemaining;
        private float wavePulseRemaining;
        private int displayedLevel = -1;
        private int appliedLevelBackground = -1;
        private float levelBackgroundFadeRemaining;
        private float backgroundScrollRemaining;
        public const float BackgroundScrollSeconds = 3f;
        private Vector3 outgoingBackgroundPosition;
        private Vector3 outgoingBackgroundScale;
        private Color outgoingBackgroundColor;
        private bool zoomToPlanet;
        private Material starColorMaterial;
        private int spinDirection = 1;
        private Color transitionFrom = Color.white;
        private Color transitionTo = Color.white;
        public int DisplayedLevel => displayedLevel < 0 ? 1 : displayedLevel;
        public int DisplayedBackgroundIndex => BackgroundIndexForLevel(DisplayedLevel);
        public int SpinDirection => spinDirection;
        public float BackgroundSpinAngle => spinAngle;
        public float LevelTransitionRemaining => levelCrossFadeRemaining;
        public float WavePulseRemaining => wavePulseRemaining;
        public Color BackgroundTint => starBackground != null ? starBackground.color : Color.white;

        public void SetBackground(SpriteRenderer background)
        {
            starBackground = background;
            Refresh();
        }

        public void SetSession(GameSession gameSession)
        {
            BindSession(gameSession);
            TickBackground(0);
        }

        private void OnEnable() => Refresh();
        private void OnDisable()
        {
            BindSession(null);
            waveCrunch = 0;
            if (view != null)
            {
                view.ResetProjectionMatrix();
                if (cameraHomeCaptured) view.transform.position = cameraHome;
            }
        }
        private void OnDestroy()
        {
            if (starColorMaterial != null)
            {
                if (Application.isPlaying) Destroy(starColorMaterial);
                else DestroyImmediate(starColorMaterial);
            }
            BindSession(null);
            if (crossFadeBackground != null)
            {
                if (Application.isPlaying) Destroy(crossFadeBackground.gameObject);
                else DestroyImmediate(crossFadeBackground.gameObject);
            }
            if (starfields != null)
                foreach (var sprite in starfields)
                    if (sprite != null)
                    {
                        if (Application.isPlaying) Destroy(sprite);
                        else DestroyImmediate(sprite);
                    }
            if (starfieldTextures != null)
                foreach (var texture in starfieldTextures)
                    if (texture != null)
                    {
                        if (Application.isPlaying) Destroy(texture);
                        else DestroyImmediate(texture);
                    }
        }
        private void LateUpdate()
        {
            Refresh();
            TickBackground(Time.deltaTime);
        }

        public void Refresh()
        {
            if (view == null) view = GetComponent<Camera>();
            CaptureCameraHome();
            view.orthographicSize = Mathf.Max(6f, 3.2f / Mathf.Max(0.01f, view.aspect));
            ApplyIphoneNotchOffset();
            // Compress every world-space visual together without moving actors or changing gameplay dimensions.
            view.ResetProjectionMatrix();
            if (waveCrunch > 0)
            {
                var projection = view.projectionMatrix;
                projection.m00 *= WaveCrunchScale;
                projection.m11 *= WaveCrunchScale;
                view.projectionMatrix = projection;
            }
            if (session == null) BindSession(FindFirstObjectByType<GameSession>());
            if (session != null && session.CurrentLevelDefinition != null)
            {
                ApplyLevelBackground();
                return;
            }
            if (solidBlackBackground)
            {
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = Color.black;
                if (starBackground != null) starBackground.enabled = false;
                if (crossFadeBackground != null) crossFadeBackground.enabled = false;
                return;
            }
            if (starBackground == null || starBackground.sprite == null) return;
            starBackground.enabled = true;
            CaptureBackgroundHome();
            Vector2 size = starBackground.sprite.bounds.size;
            float scale = Mathf.Max(2f * view.orthographicSize * view.aspect / size.x,
                2f * view.orthographicSize / size.y) * BackgroundScaleMultiplier();
            ApplyBackgroundPose(scale);
        }

        public float PlayfieldViewportLiftForIphoneNotch()
        {
            if (view == null) view = GetComponent<Camera>();
            CaptureCameraHome();
            if (!ShouldApplyIphoneNotchOffset()) return 0;
            float currentTop = .5f + (PlayfieldBoundaryTopY - cameraHome.y) / (2f * view.orthographicSize);
            float topInset = Mathf.Max(TopUnsafeInsetRatio(), IphoneNotchDefaultTopInsetRatio);
            float rimClearance = PlayfieldFrame.OuterTopY + PlayfieldFrame.OutlineLineWidth / 2 - PlayfieldBoundaryTopY +
                PlayfieldFrame.BoundaryLineWidth;
            float targetTop = Mathf.Min(1f - topInset - rimClearance / (2f * view.orthographicSize),
                IphoneNotchMaxPlayfieldTopViewportY);
            return targetTop - currentTop;
        }

        public void TickBackground(float seconds)
        {
            seconds = Mathf.Max(0, seconds);
            int level = CurrentLevel();
            if (session != null && session.CurrentLevelDefinition != null)
            {
                displayedLevel = level;
                levelBackgroundFadeRemaining = Mathf.Max(0, levelBackgroundFadeRemaining - seconds);
                if (!session.IsPaused) backgroundScrollRemaining = Mathf.Max(0, backgroundScrollRemaining - seconds);
                wavePulseRemaining = levelCrossFadeRemaining = 0;
                Refresh();
                return;
            }
            if (displayedLevel < 0) InitializeDisplayedLevel(level);
            else if (level != displayedLevel) StartLevelCrossFade(level);
            if (solidBlackBackground)
            {
                wavePulseRemaining = levelCrossFadeRemaining = 0;
                Refresh();
                return;
            }
            if (starBackground == null) return;
            EnsureStarfields();
            CaptureBackgroundHome();

            backgroundPhase += seconds * backgroundCyclesPerSecond;
            spinAngle += seconds * spinDirection * SpinSpeedForLevel(displayedLevel);
            wavePulseRemaining = Mathf.Max(0, wavePulseRemaining - seconds);
            if (levelCrossFadeRemaining > 0)
            {
                levelCrossFadeRemaining = Mathf.Max(0, levelCrossFadeRemaining - seconds);
                float progress = 1f - levelCrossFadeRemaining / levelCrossFadeSeconds;
                float smooth = progress * progress * (3f - 2f * progress);
                starBackground.color = WithAlpha(PulsedColor(transitionTo), smooth);
                if (crossFadeBackground != null)
                {
                    crossFadeBackground.enabled = levelCrossFadeRemaining > 0;
                    crossFadeBackground.color = WithAlpha(PulsedColor(transitionFrom), 1f - smooth);
                }
            }
            else
            {
                starBackground.color = PulsedColor(transitionTo);
                if (crossFadeBackground != null) crossFadeBackground.enabled = false;
            }
            Refresh();
        }

        private void ApplyLevelBackground()
        {
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.black;
            if (crossFadeBackground != null) crossFadeBackground.enabled = false;
            var image = session.State == GameSession.RunState.MainMenu ? null : session.CurrentLevelDefinition.backgroundImage;
            if (starBackground == null) return;
            int level = session.Progress.Level;
            if (appliedLevelBackground != level)
            {
                if (appliedLevelBackground > 0 && starBackground.enabled && image != null &&
                    session.State != GameSession.RunState.MainMenu)
                {
                    EnsureCrossFadeBackground();
                    crossFadeBackground.sprite = starBackground.sprite;
                    crossFadeBackground.sharedMaterial = starBackground.sharedMaterial;
                    crossFadeBackground.color = starBackground.color;
                    crossFadeBackground.transform.position = starBackground.transform.position;
                    crossFadeBackground.transform.rotation = starBackground.transform.rotation;
                    crossFadeBackground.transform.localScale = starBackground.transform.localScale;
                    outgoingBackgroundPosition = crossFadeBackground.transform.position;
                    outgoingBackgroundScale = crossFadeBackground.transform.localScale;
                    outgoingBackgroundColor = crossFadeBackground.color;
                    zoomToPlanet = appliedLevelBackground == 3 && level == 4 || appliedLevelBackground == 4 && level == 5;
                    backgroundScrollRemaining = BackgroundScrollSeconds;
                }
                appliedLevelBackground = level;
                levelBackgroundFadeRemaining = backgroundScrollRemaining <= 0 && level == 3 && image != null ? levelCrossFadeSeconds : 0;
            }
            starBackground.enabled = image != null;
            if (image == null) return;
            starBackground.sprite = image;
            float fade = levelBackgroundFadeRemaining > 0
                ? 1f - levelBackgroundFadeRemaining / levelCrossFadeSeconds : 1f;
            var color = session.ProgressBarColor;
            if (color == Color.clear || color == Color.gray) color = TintForLevel(level);
            float beat = FindFirstObjectByType<GameplayMusicPlayer>()?.BeatPosition ?? 0;
            float pulse = GameplayMusicPlayer.BeatPulse(beat);
            var player = FindFirstObjectByType<PlayerMovement>();
            bool magic = player != null && player.MagicPowerActive;
            if (starColorMaterial == null)
            {
                var shader = Resources.Load<Shader>("StarColors");
                if (shader != null) starColorMaterial = new Material(shader);
            }
            if (starColorMaterial != null)
            {
                starBackground.sharedMaterial = starColorMaterial;
                starColorMaterial.SetFloat("_Magic", magic ? 1 : 0);
                var music = player != null ? player.Music : null;
                float phase = music != null && music.InGameplayRun ? Mathf.Max(0, music.BeatPosition) * 4 : Time.time * 10;
                starColorMaterial.SetFloat("_Phase", phase);
                var palette = new Vector4[6];
                for (int i = 0; i < palette.Length; i++) palette[i] = EnemyPalette.Get(EnemyPalette.CycleColor(i));
                starColorMaterial.SetVectorArray("_Palette", palette);
            }
            if (magic) color = Color.white;
            color = Color.Lerp(Color.black, color, Mathf.SmoothStep(0, 1, fade));
            color = Color.Lerp(color, Color.white, .22f * pulse);
            color.a *= Mathf.Lerp(.72f, 1f, pulse);
            starBackground.color = color;
            starBackground.sortingOrder = -100;
            starBackground.transform.rotation = Quaternion.identity;
            var position = view.transform.position;
            position.z = starBackground.transform.position.z;
            starBackground.transform.position = position;
            var size = image.bounds.size;
            // Keep the complete authored background inside the fixed gameplay frame.
            // The camera is wider on desktop, but the playfield border remains the limiting area.
            float frameWidth = 2f * (PlayerMovement.HalfWidth - PlayfieldFrame.BoundaryLineWidth);
            float frameHeight = 2f * (PlayfieldFrame.OuterTopY - PlayfieldFrame.BoundaryLineWidth);
            float pulseScale = 1f + .08f * pulse;
            float scale = Mathf.Min(frameWidth / Mathf.Max(.001f, size.x),
                frameHeight / Mathf.Max(.001f, size.y)) / (WaveCrunchScale * pulseScale);
            // Fit the sprite's bounds even when its artist chose an off-center pivot.
            starBackground.transform.localScale = Vector3.one * (scale * pulseScale);
            starBackground.transform.position -= (Vector3)image.bounds.center * (scale * pulseScale);
            if (backgroundScrollRemaining > 0 && crossFadeBackground != null)
            {
                // Both layers follow the same live tint, pulse and shared magic palette.
                outgoingBackgroundColor = starBackground.color;
                crossFadeBackground.sharedMaterial = starColorMaterial;
                crossFadeBackground.color = outgoingBackgroundColor;
                float progress = Mathf.SmoothStep(0, 1, 1 - backgroundScrollRemaining / BackgroundScrollSeconds);
                if (zoomToPlanet)
                {
                    if (level == 5)
                    {
                        var smallPlanet = BackgroundPixelPoint(crossFadeBackground.sprite, new Vector2(802, 825));
                        var largePlanet = BackgroundPixelPoint(image, new Vector2(1380, 800));
                        var startAnchor = outgoingBackgroundPosition + Vector3.Scale(smallPlanet, outgoingBackgroundScale);
                        var endAnchor = starBackground.transform.TransformPoint(largePlanet);
                        var control = startAnchor + new Vector3(-2.5f, 3f, 0);
                        var anchor = (1 - progress) * (1 - progress) * startAnchor +
                            2 * (1 - progress) * progress * control + progress * progress * endAnchor;
                        var incomingScale = starBackground.transform.localScale * Mathf.Lerp(1f / 12f, 1, progress);
                        starBackground.transform.localScale = incomingScale;
                        starBackground.transform.position = anchor - Vector3.Scale(largePlanet, incomingScale);
                        var outgoingScale = outgoingBackgroundScale * Mathf.Lerp(1, 12, progress);
                        crossFadeBackground.transform.localScale = outgoingScale;
                        crossFadeBackground.transform.position = anchor - Vector3.Scale(smallPlanet, outgoingScale);
                        float planetFade = 1 - Mathf.Pow(backgroundScrollRemaining / BackgroundScrollSeconds, 2);
                        crossFadeBackground.color = outgoingBackgroundColor;
                        starBackground.color = WithAlpha(starBackground.color, starBackground.color.a * planetFade);
                        crossFadeBackground.enabled = true;
                        return;
                    }
                    // Anchors are measured in pixels from the top-left of each 1080 x 1920 section.
                    var starAnchor = BackgroundPixelPoint(crossFadeBackground.sprite, new Vector2(791, 832));
                    var planetAnchor = BackgroundPixelPoint(image, new Vector2(802, 825));
                    // Begin the curved approach early, settling at the authored framing.
                    float curveProgress = Mathf.InverseLerp(.15f, 1f, progress);
                    float curveOffset = .3f * Mathf.Pow(Mathf.Sin(Mathf.PI * curveProgress), 2);
                    starBackground.transform.position += Vector3.left * curveOffset;
                    var destination = starBackground.transform.TransformPoint(planetAnchor);
                    var zoomScale = outgoingBackgroundScale * Mathf.Lerp(1, 4, progress);
                    crossFadeBackground.transform.localScale = zoomScale;
                    crossFadeBackground.transform.position = destination - Vector3.Scale(starAnchor, zoomScale);
                    float elapsedFraction = Mathf.Clamp01(1 - backgroundScrollRemaining / BackgroundScrollSeconds);
                    // Begin revealing the planet immediately, then ease gently into full opacity.
                    float fadeProgress = 1 - Mathf.Pow(1 - elapsedFraction, 2);
                    crossFadeBackground.color = WithAlpha(outgoingBackgroundColor, outgoingBackgroundColor.a * (1 - fadeProgress));
                    starBackground.color = WithAlpha(starBackground.color, starBackground.color.a * fadeProgress);
                    crossFadeBackground.enabled = true;
                    return;
                }
                // Opaque adjoining slices avoid a dark alpha-blended seam; overlap one source texel.
                starBackground.color = WithAlpha(starBackground.color, 1);
                crossFadeBackground.color = WithAlpha(starBackground.color, 1);
                float distance = (size.y - 1f / image.pixelsPerUnit) * scale * pulseScale;
                starBackground.transform.position += Vector3.up * distance * (1 - progress);
                crossFadeBackground.transform.position = outgoingBackgroundPosition - Vector3.up * distance * progress;
                crossFadeBackground.enabled = true;
            }
        }

        private static Vector3 BackgroundPixelPoint(Sprite sprite, Vector2 pixel) =>
            new Vector3((pixel.x - sprite.pivot.x) / sprite.pixelsPerUnit,
                (sprite.rect.height - pixel.y - sprite.pivot.y) / sprite.pixelsPerUnit, 0);

        public void TriggerWaveSpawnEffect()
        {
            if (solidBlackBackground) return;
            wavePulseRemaining = wavePulseSeconds;
        }

        public static Color TintForLevel(int level)
        {
            var color = EnemyPalette.Get(EnemyPalette.CycleColor(Mathf.Abs(level - 1)));
            return Color.Lerp(Color.white, color, .28f);
        }

        public static int BackgroundIndexForLevel(int level) =>
            (Mathf.Max(1, level) - 1) % StarfieldCount;

        private int CurrentLevel()
        {
            if (session == null) BindSession(FindFirstObjectByType<GameSession>());
            return session != null ? session.Progress.Level : 1;
        }

        private void BindSession(GameSession gameSession)
        {
            if (subscribedSession == gameSession)
            {
                session = gameSession;
                return;
            }
            if (subscribedSession != null) subscribedSession.WaveSpawned -= TriggerWaveSpawnEffect;
            subscribedSession = gameSession;
            session = gameSession;
            if (subscribedSession != null) subscribedSession.WaveSpawned += TriggerWaveSpawnEffect;
        }

        private void StartLevelCrossFade(int level)
        {
            int previous = displayedLevel;
            displayedLevel = level;
            if (Mathf.Abs(level - previous) % 2 == 1) spinDirection = -spinDirection;
            transitionFrom = TintForLevel(previous);
            transitionTo = TintForLevel(level);
            if (solidBlackBackground || starBackground == null)
            {
                levelCrossFadeRemaining = 0;
                return;
            }
            EnsureCrossFadeBackground();
            if (crossFadeBackground != null)
            {
                crossFadeBackground.sprite = starBackground.sprite;
                crossFadeBackground.sortingLayerID = starBackground.sortingLayerID;
                crossFadeBackground.sortingOrder = starBackground.sortingOrder - 1;
                crossFadeBackground.enabled = true;
            }
            levelCrossFadeRemaining = levelCrossFadeSeconds;
            starBackground.sprite = StarfieldForLevel(level);
            starBackground.color = WithAlpha(transitionTo, 0);
        }

        private void InitializeDisplayedLevel(int level)
        {
            displayedLevel = level;
            spinDirection = level % 2 == 0 ? -1 : 1;
            transitionFrom = transitionTo = TintForLevel(level);
            if (starBackground == null || solidBlackBackground) return;
            EnsureStarfields();
            starBackground.sprite = StarfieldForLevel(level);
            starBackground.color = transitionTo;
        }

        private void CaptureBackgroundHome()
        {
            if (backgroundHomeCaptured || starBackground == null) return;
            backgroundHome = starBackground.transform.localPosition;
            backgroundHomeRotation = starBackground.transform.localRotation;
            backgroundHomeCaptured = true;
        }

        private void CaptureCameraHome()
        {
            if (cameraHomeCaptured) return;
            cameraHome = view.transform.position;
            cameraHomeCaptured = true;
        }

        private void ApplyIphoneNotchOffset()
        {
            if (!cameraHomeCaptured) return;
            var position = cameraHome;
            position.y -= PlayfieldViewportLiftForIphoneNotch() * view.orthographicSize * 2f;
            view.transform.position = position;
        }

        private bool ShouldApplyIphoneNotchOffset()
        {
            if (!IphoneFramingActive() || view == null || view.aspect > IphoneNotchPortraitAspectLimit)
                return false;
            float topInset = TopUnsafeInsetRatio();
            return topInset >= IphoneNotchMinimumTopInsetRatio || view.aspect <= IphoneNotchFallbackAspectLimit;
        }

        private static bool IphoneFramingActive()
        {
#if UNITY_EDITOR
            if (ForceIphoneNotchOffsetForTests) return true;
#endif
#if UNITY_IOS
            return Application.platform == RuntimePlatform.IPhonePlayer;
#else
            return false;
#endif
        }

        private static float TopUnsafeInsetRatio()
        {
#if UNITY_EDITOR
            if (ForceIphoneNotchOffsetForTests)
                return Mathf.Clamp01(SimulatedIphoneTopInsetRatioForTests);
#endif
            float height = Screen.height;
            if (height <= 0) return 0;
            return Mathf.Clamp01((height - Screen.safeArea.yMax) / height);
        }

        private float BackgroundScaleMultiplier()
        {
            if (wavePulseRemaining <= 0 || wavePulseSeconds <= 0) return 1.12f;
            float progress = 1f - wavePulseRemaining / wavePulseSeconds;
            return 1.12f + Mathf.Sin(progress * Mathf.PI) * .04f;
        }

        private float SpinSpeedForLevel(int level) =>
            baseSpinDegreesPerSecond + Mathf.Max(0, level - 1) * spinDegreesPerLevel;

        private Color PulsedColor(Color color)
        {
            if (wavePulseRemaining <= 0 || wavePulseSeconds <= 0) return color;
            float progress = 1f - wavePulseRemaining / wavePulseSeconds;
            return Color.Lerp(color, Color.white, Mathf.Sin(progress * Mathf.PI) * .35f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private void ApplyBackgroundPose(float scale)
        {
            Vector3 position = backgroundHome +
                Vector3.up * Mathf.Sin(backgroundPhase * Mathf.PI * 2f) * backgroundDrift;
            Quaternion rotation = backgroundHomeRotation * Quaternion.Euler(0, 0, spinAngle);
            starBackground.transform.localPosition = position;
            starBackground.transform.localRotation = rotation;
            starBackground.transform.localScale = Vector3.one * scale;
            if (crossFadeBackground == null) return;
            crossFadeBackground.transform.localPosition = position;
            crossFadeBackground.transform.localRotation = rotation;
            crossFadeBackground.transform.localScale = Vector3.one * scale;
        }

        private void EnsureCrossFadeBackground()
        {
            if (crossFadeBackground != null || starBackground == null) return;
            var clone = new GameObject(starBackground.name + " Crossfade", typeof(SpriteRenderer));
            clone.hideFlags = HideFlags.DontSave;
            clone.transform.SetParent(starBackground.transform.parent, false);
            crossFadeBackground = clone.GetComponent<SpriteRenderer>();
            crossFadeBackground.enabled = false;
            crossFadeBackground.sharedMaterial = starBackground.sharedMaterial;
        }

        private Sprite StarfieldForLevel(int level)
        {
            EnsureStarfields();
            return starfields[BackgroundIndexForLevel(level)];
        }

        private void EnsureStarfields()
        {
            if (starfields != null) return;
            starfields = new Sprite[StarfieldCount];
            starfieldTextures = new Texture2D[StarfieldCount];
            for (int i = 0; i < StarfieldCount; i++)
                CreateStarfield(i);
        }

        private void CreateStarfield(int index)
        {
            const int size = 384;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated Starfield " + (index + 1),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var random = new System.Random(7301 + index * 137);
            var pixels = new Color32[size * size];
            Color nebulaA = Color.HSVToRGB((index * .103f) % 1f, .52f, .35f);
            Color nebulaB = Color.HSVToRGB((index * .103f + .34f) % 1f, .45f, .28f);
            Vector2 centerA = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            Vector2 centerB = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((float)x / (size - 1), (float)y / (size - 1));
                float da = Mathf.Clamp01(1f - Vector2.Distance(uv, centerA) * 1.8f);
                float db = Mathf.Clamp01(1f - Vector2.Distance(uv, centerB) * 2.2f);
                float dust = Mathf.Sin((uv.x * (6.3f + index) + uv.y * (3.8f + index * .4f)) * Mathf.PI);
                Color color = new Color(.01f, .012f, .025f, 1f);
                color += nebulaA * da * da * .55f;
                color += nebulaB * db * db * .42f;
                color += Color.white * Mathf.Max(0, dust) * .012f;
                pixels[y * size + x] = color;
            }
            for (int i = 0; i < 850; i++)
            {
                int x = random.Next(size), y = random.Next(size);
                float value = .55f + (float)random.NextDouble() * .45f;
                var star = new Color(value, value, value, 1f);
                if (random.NextDouble() < .32)
                    star = Color.Lerp(star, Color.HSVToRGB((index * .103f + (float)random.NextDouble() * .18f) % 1f, .35f, value), .55f);
                pixels[y * size + x] = star;
                if (random.NextDouble() < .09 && x + 1 < size) pixels[y * size + x + 1] = Color.Lerp(pixels[y * size + x + 1], star, .65f);
                if (random.NextDouble() < .09 && y + 1 < size) pixels[(y + 1) * size + x] = Color.Lerp(pixels[(y + 1) * size + x], star, .65f);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            starfieldTextures[index] = texture;
            starfields[index] = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 128);
            starfields[index].name = texture.name;
            starfields[index].hideFlags = HideFlags.DontSave;
        }
    }
}
