using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid), typeof(EnemyRowSpawner), typeof(EnemyGridMovement))]
    public sealed class GameSession : MonoBehaviour
    {
        public enum RunState { Playing, Refilling, GameOver, MainMenu, Dying }
        public const float GameOverBlackoutBeats = 4;
        public float GameOverBlackoutSeconds { get; private set; } = GameOverBlackoutBeats * FullSetCelebration.StepDuration;
        public const float GameOverTitleFadeSeconds = .65f;
        public const float EpilepsyWarningSeconds = 4f;
        public const string EpilepsyWarningTitle = "PHOTOSENSITIVITY WARNING";
        public const string EpilepsyWarningMessage =
            "This game contains flashing lights and other visual effects that may trigger seizures in people with photosensitive epilepsy.\n\n" +
            "If you or anyone in your family has an epileptic condition, consult a doctor before playing. Player discretion is advised.";
        private float epilepsyWarningElapsed;
        private float gameOverElapsed;
        public float GameOverMusicGain => 1 - GameOverBlackoutOpacity;
        public float GameOverBlackoutOpacity => State == RunState.GameOver ? 1 : State == RunState.Dying ?
            Mathf.SmoothStep(0, 1, deathElapsed / GameOverBlackoutSeconds) : 0;
        public float GameOverTitleOpacity => State == RunState.GameOver ?
            Mathf.SmoothStep(0, 1, gameOverElapsed / GameOverTitleFadeSeconds) : 0;
        public float GameOverScoreOpacity => 1 - GameOverBlackoutOpacity;
        public bool GameOverTitleReady => State == RunState.GameOver && gameOverElapsed >= GameOverTitleFadeSeconds;
        private PresentationCue deathCue;
        private bool hasDeathCue;
        private float deathElapsed;
        private bool deathAnimationStarted;
        private GameOverBlackout blackout;
        private static bool startAfterReload;
        private static bool epilepsyWarningShown;
        private static int rememberedStartLevel = RunProgress.MinLevel;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStartup()
        {
            startAfterReload = false;
            epilepsyWarningShown = false;
            rememberedStartLevel = RunProgress.MinLevel;
        }
        private float previousTimeScale = 1;
        private float appliedTempoMultiplier = 1;
        private float deathTempoMultiplier = 1;
        private float greenTempoMultiplier = 1, greenTempoTarget = 1, greenTempoStart = 1;
        private float greenTempoElapsed, greenTempoDuration;
        public float TempoMultiplier => isActiveAndEnabled && !PresentationPreview && !OpeningWarningActive &&
            (State == RunState.Playing || State == RunState.Refilling) ?
            greenTempoMultiplier : 1;
        public float MusicTempoMultiplier => State == RunState.Dying ? deathTempoMultiplier : TempoMultiplier;
        private int inputBlockedThrough = -1;
        private bool started;
        public bool IsPaused { get; private set; }
        public SongSelectionMenu SongSelection { get; } = new SongSelectionMenu();
        public bool SongSelectionMenuAvailable => !OpeningWarningActive && !MenuArriving && !MetronomeMenuOpen &&
            (IsPaused || State == RunState.MainMenu && menuTitleElapsed >= MenuWordIntroDuration);
        private SongSelectionMenu.Layout SongMenuLayout => new SongSelectionMenu.Layout(
            new Vector2(Screen.width, Screen.height), Screen.safeArea, GuiScale, MobileMenuActive);
        public bool MetronomeMenuOpen { get; private set; }
        public bool MetronomeMenuAvailable => IsPaused && !OpeningWarningActive;
        public bool VisualMetronomeVisible => MetronomeSettings.VisualEnabled && !OpeningWarningActive &&
            (State == RunState.Playing || State == RunState.Refilling) && !IsPaused;
        public bool RoundClockVisible => MetronomeSettings.RoundClockEnabled && !OpeningWarningActive &&
            (State == RunState.Playing || State == RunState.Refilling) && !IsPaused;
        public double ElapsedRoundSeconds { get; private set; }
        public string RoundClockText => FormatRoundTime(ElapsedRoundSeconds);
        public static string FormatRoundTime(double seconds)
        {
            long wholeSeconds = (long)System.Math.Floor(System.Math.Max(0, seconds));
            return (wholeSeconds / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":" +
                (wholeSeconds % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
        public bool PresentationPreview { get; set; }
        public bool OpeningWarningActive => Application.isPlaying && !PresentationPreview && !epilepsyWarningShown &&
            epilepsyWarningElapsed < EpilepsyWarningSeconds;
        public bool BlocksGameplayInput => PresentationPreview || IsPaused || State == RunState.MainMenu || State == RunState.Dying ||
            State == RunState.GameOver || OpeningWarningActive || Time.frameCount <= inputBlockedThrough;
        [SerializeField] private PlayerMovement player;
        private EnemyGrid grid;
        private EnemyRowSpawner spawner;
        private EnemyGridMovement movement;
        private PlayerMovement subscribedPlayer;
        private SoundEffects soundEffects;
        private GameplayMusicPlayer musicPlayer;
        private float refillRemaining;
        private AudioClip refillMusicClip;
        private float refillMusicSeconds;
        private float refillSpawnSeconds;
        private float refillArrivalSeconds;
        private bool restarting;
        private bool leaderboardRecorded;
        private string menuLevelDigits = "";
        public const float MenuArrivalSeconds = 1.2f;
        public const float MenuTakeoffSeconds = .32f;
        public bool MenuArriving { get; private set; }
        public float MenuArrivalProgress => Mathf.Clamp01(menuArrivalElapsed / MenuArrivalSeconds);
        public float MenuArrivalFadeProgress => MenuArrivalFadeProgressFor(menuArrivalElapsed);
        public float MenuArrivalJumpProgress => MenuArrivalJumpProgressFor(menuArrivalElapsed);
        public float MenuTitleIntroElapsed => menuTitleElapsed;
        public const string MenuIntroSeenKey = "CandyCruisers.MenuIntro.Seen";
        public const string HighestReachedLevelKey = "CandyCruisers.HighestReachedLevel";
        public bool FirstLaunchIntro { get; private set; }
        public MenuIntroAnimation.Entrance MenuEntrance { get; private set; }
        private bool menuIntroRecorded;
        private float menuArrivalElapsed;
        private float menuTitleElapsed;
        private float menuEyeDelayElapsed;
        private float menuEyeDelaySeconds;
        private bool menuEyeDelayStarted;
        private readonly MenuGazeAnimation menuGaze = new MenuGazeAnimation();
        private Rect menuDeparture;
        public bool MenuEyeTrackingReady => !MenuArriving && menuTitleElapsed >= MenuWordIntroDuration &&
            menuEyeDelayStarted && menuEyeDelayElapsed >= menuEyeDelaySeconds;

        public void SetMenuEntrancePreview(MenuIntroAnimation.Entrance entrance)
        {
            if (!PresentationPreview || entrance < MenuIntroAnimation.Entrance.FlipRight ||
                entrance > MenuIntroAnimation.Entrance.TractorBeam) return;
            MenuEntrance = entrance;
            menuTitleElapsed = menuArrivalElapsed = 0;
            menuEyeDelayElapsed = menuEyeDelaySeconds = 0;
            menuEyeDelayStarted = false;
            menuGaze.Reset();
            MenuArriving = false;
            menuDeparture = default;
        }

        public void BeginMenuArrival()
        {
            if (State != RunState.MainMenu || MenuArriving) return;
            if (FirstLaunchIntro && menuTitleElapsed < MenuWordIntroDuration) return;
            menuTitleElapsed = MenuWordIntroDuration;
            MenuArriving = true;
            menuArrivalElapsed = 0;
            // Screen dimensions outside a drawing event can describe a different editor view.
            // Capture the departure on the first draw in the actual menu canvas instead.
            menuDeparture = default;
            player?.CancelPointer();
        }

        private void ActivateMainMenu()
        {
            if (!MenuArriving && menuTitleElapsed < MenuWordIntroDuration)
            {
                if (FirstLaunchIntro) return;
                menuTitleElapsed = MenuWordIntroDuration;
                player?.CancelPointer();
                return;
            }
            BeginMenuArrival();
        }
        [SerializeField, Range(RunProgress.MinLevel, RunProgress.MaxMenuStartLevel)] private int startLevel = RunProgress.MinLevel;
        private static readonly EnemyColor[] PreviewRegularColors = {
            EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow, EnemyColor.Orange
        };
        private static readonly EnemyColor[] PreviewSpecialColors = {
            EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow
        };
        public RunState State { get; private set; }
        public RunProgress Progress { get; } = new RunProgress();
        public bool IsAutomatedPlaytest { get; internal set; }
        public bool PlaytestPanelOpen { get; internal set; }
        public FleetState Fleet => (grid != null ? grid : GetComponent<EnemyGrid>())?.Fleet;
        [SerializeField, HideInInspector] private TextAsset levelMap;
        private LevelCatalog levelCatalog;
        private bool levelMapOverridden;
        private string levelMapError;
        public LevelCatalog LevelCatalog
        {
            get => levelCatalog;
            set { levelCatalog = value; Progress.LevelCatalog = value; levelMapOverridden = true; levelMapError = null; }
        }
        public LevelDefinition CurrentLevelDefinition => levelCatalog != null ? levelCatalog.ForLevel(Progress.Level) : null;
        public event System.Action WaveSpawned;
        public event System.Action<int, int, bool, bool> CombatResolved;
        public int LastLeaderboardRank { get; private set; }
        public Color ProgressBarColor => player != null ? player.AccentColor : Color.gray;
        public const float ComboMilestoneAnimationSeconds = 1.1f;
        public const float ComboMilestonePulseSeconds = .45f;
        public const float ComboDeathFadeSeconds = .5f;
        public const float LargePhoneGuiScale = 2f;
        public const float MediumPhoneGuiScale = 1.6f;
        public const float SmallPhoneGuiScale = 1.35f;
        private int observedComboStreak;
        private int comboMilestoneMultiplier;
        private float comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
        public ComboBreakAnimation ComboBreak { get; } = new ComboBreakAnimation();
        private float comboDeathFadeElapsed = ComboDeathFadeSeconds;
        public int ComboDeathMultiplier { get; private set; }
        public Color ComboDeathColor => ComboColor;
        public float ComboDeathOpacity => ComboDeathMultiplier > 1 ?
            1 - Mathf.SmoothStep(0, 1, comboDeathFadeElapsed / ComboDeathFadeSeconds) : 0;
        public Color GameplayTextColor => ProgressBarColor;
        public Color ComboColor => GameplayTextColor;
        private Font gameplayFont;
        public Font ComboFont => gameplayFont != null ? gameplayFont :
            gameplayFont = Resources.Load<Font>("Fonts/Bungee-Regular");
        public const float GameplayTextMaxPulseScale = 1.08f;
        public const float GameplayTextSizeScale = 1.5f;
        public static int GameplayHeadingFontSize(int viewHeight) =>
            Mathf.RoundToInt((viewHeight < 600 ? 11 : 13) * GameplayTextSizeScale);
        public static int GameplayComboFontSize(int viewHeight) =>
            Mathf.RoundToInt((viewHeight < 400 ? 11 : viewHeight < 600 ? 13 : 16) * GameplayTextSizeScale);
        public const float GameplayHeadingHeight = 32;
        public static float GameplayTextPulseScale(float beatPosition) =>
            Mathf.Lerp(1, GameplayTextMaxPulseScale, GameplayMusicPlayer.BeatPulse(beatPosition));

        public GUIStyle GameplayTextStyle(int fontSize, TextAnchor alignment)
        {
            var style = new GUIStyle
            {
                font = ComboFont, fontSize = fontSize, fontStyle = FontStyle.Normal,
                alignment = alignment, padding = new RectOffset(), wordWrap = false,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = Color.white;
            return style;
        }

        public static void FitGameplayText(GUIStyle style, string text, Rect rect)
        {
            var content = new GUIContent(text);
            while (style.fontSize > 1 && (style.CalcSize(content).x * GameplayTextMaxPulseScale > rect.width ||
                style.CalcSize(content).y * GameplayTextMaxPulseScale > rect.height)) style.fontSize--;
        }

        private void DrawGameplayText(Rect rect, string text, GUIStyle style, Color tint, float opacity, float beat)
        {
            FitGameplayText(style, text, rect);
            float restingAnchorX = style.alignment == TextAnchor.MiddleLeft ? rect.xMin :
                style.alignment == TextAnchor.MiddleRight ? rect.xMax : rect.center.x;
            rect = InsetGrowingText(rect, text, style, new Vector2(restingAnchorX, rect.center.y), GameplayTextMaxPulseScale);
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            float anchorX = style.alignment == TextAnchor.MiddleLeft ? rect.xMin :
                style.alignment == TextAnchor.MiddleRight ? rect.xMax : rect.center.x;
            GUIUtility.ScaleAroundPivot(Vector2.one * GameplayTextPulseScale(beat), new Vector2(anchorX, rect.center.y));
            GUI.matrix = MagicPowerPresentation.ElementMatrix(GUI.matrix, GameplayLabelBounds(rect, text, style), MagicTextGrowthScale);
            GUI.color = new Color(0, 0, 0, .7f * opacity);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            GUI.color = new Color(tint.r, tint.g, tint.b, opacity);
            GUI.Label(rect, text, style);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private Rect InsetGrowingText(Rect rect, string text, GUIStyle style, Vector2 anchor, float originalPeak = 1)
        {
            var safe = MetronomeSafeRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, GuiScale);
            safe = new Rect(safe.x + 4, safe.y + 4, Mathf.Max(0, safe.width - 8), Mathf.Max(0, safe.height - 8));
            rect.position += MagicPowerPresentation.PeakTextInset(GameplayLabelBounds(rect, text, style), safe, anchor, originalPeak);
            return rect;
        }

        private static Rect GameplayLabelBounds(Rect rect, string text, GUIStyle style)
        {
            var content = new GUIContent(text);
            var size = style.CalcSize(content);
            size.x = Mathf.Min(size.x, rect.width);
            size.y = Mathf.Min(style.wordWrap ? style.CalcHeight(content, rect.width) : size.y, rect.height);
            int horizontal = (int)style.alignment % 3, vertical = (int)style.alignment / 3;
            return new Rect(horizontal == 0 ? rect.xMin : horizontal == 2 ? rect.xMax - size.x : rect.center.x - size.x / 2,
                vertical == 0 ? rect.yMin : vertical == 2 ? rect.yMax - size.y : rect.center.y - size.y / 2, size.x, size.y);
        }

        private Matrix4x4 BeginMagicText(Rect rect)
        {
            var previous = GUI.matrix;
            GUI.matrix = MagicPowerPresentation.ElementMatrix(previous, rect, MagicTextGrowthScale);
            return previous;
        }
        private bool accumulatingShotScore;
        private long shotScore;
        public string ScoreFeedback => feedback;
        public int StartLevel
        {
            get => startLevel;
            set
            {
                startLevel = RunProgress.ClampMenuStartLevel(value);
                rememberedStartLevel = startLevel;
            }
        }
        public static int HighestReachedLevel
        {
            get
            {
                int highest = PlayerPrefs.GetInt(HighestReachedLevelKey, RunProgress.MinLevel);
                foreach (var entry in LocalLeaderboard.Entries())
                    highest = Mathf.Max(highest, entry.Level);
                return RunProgress.ClampMenuStartLevel(highest);
            }
        }
        public int SelectableMaxStartLevel => SelectableMaxStartLevelFor(HighestReachedLevel);
        public bool MainMenuLevelSelectVisible => MainMenuLevelSelectVisibleFor(HighestReachedLevel,
            menuTitleElapsed, MenuArriving);

        public static void RecordReachedLevel(int level)
        {
            int reached = RunProgress.ClampMenuStartLevel(level);
            if (reached <= HighestReachedLevel) return;
            PlayerPrefs.SetInt(HighestReachedLevelKey, reached);
            PlayerPrefs.Save();
        }

        public static int SelectableMaxStartLevelFor(int highestReachedLevel) =>
            RunProgress.ClampMenuStartLevel(Mathf.Max(RunProgress.MinLevel, highestReachedLevel));

        public static bool MainMenuLevelSelectVisibleFor(int highestReachedLevel, float introElapsed, bool arriving) =>
            !arriving && introElapsed >= MenuWordIntroDuration && SelectableMaxStartLevelFor(highestReachedLevel) >= 2;

        private int ClampSelectableStartLevel(int level) =>
            Mathf.Clamp(RunProgress.ClampMenuStartLevel(level), RunProgress.MinLevel, SelectableMaxStartLevel);

        private void SetSelectableStartLevel(int level)
        {
            StartLevel = ClampSelectableStartLevel(level);
        }
        private float feedbackRemaining;
        private string feedback = "";
        private readonly ColorClearBarAnimation clearBarAnimation = new ColorClearBarAnimation();
        public List<EnemyColor> DisplayedColorClearBarColors => Fleet?.BarColors() ?? new List<EnemyColor>();
        public Rect DisplayedColorClearBarSlotRect(Rect row, int index)
        {
            var colors = DisplayedColorClearBarColors;
            return ColorClearBarSlotRect(row.x, row.y, row.width, colors.Count, index);
        }
        public Rect DisplayedColorClearBarRect(EnemyColor color, Rect resting, float beat, int slot)
        {
            if (!grid.HasColorClearBar(color) && LostBarAnimation.Active && LostBarAnimation.Visible(color))
                return LostBarAnimation.Present(color, resting);
            if (ClearCelebration.Active && !ClearCelebration.Waiting) return ClearCelebration.Present(color, resting);
            return ColorClearBarLevelRect(clearBarAnimation.Present(color, resting), beat, slot);
        }
        public FullSetCelebration ClearCelebration { get; } = new FullSetCelebration();
        public FullSetCelebration LostBarAnimation { get; } = new FullSetCelebration();
        private bool partialClearCelebration;
        private int rewardedLifeSlot = -1;
        private int refillPostBarBeats = 1;
        private int startedBarCount;
        private void OnCelebrationBarSpiked(EnemyColor color, float duration)
        {
            if (!partialClearCelebration && player != null && ClearCelebration.LastColor == color &&
                rewardedLifeSlot < 0 && player.ExtraLives == PlayerMovement.MaxExtraLives)
                player.LifeIcons.Choreography.BeginFlip(musicPlayer != null ? musicPlayer.SecondsForBeats(1) : FullSetCelebration.StepDuration);
            if (partialClearCelebration || player == null || !player.Alive || Camera.main == null) return;
            var colors = DisplayedColorClearBarColors;
            var camera = Camera.main;
            float left = camera.WorldToScreenPoint(new Vector3(-3, 0, 0)).x + 10 * GuiScale;
            float right = camera.WorldToScreenPoint(new Vector3(3, 0, 0)).x - 10 * GuiScale;
            int index = colors.IndexOf(color);
            if (index < 0 || colors.Count == 0) return;
            var bar = ColorClearBarSlotRect(left / GuiScale, 0, (right - left) / GuiScale, colors.Count, index);
            var body = CharacterVisuals.Ensure(player.gameObject).HitBounds;
            if (!PlayerCelebrationFlip.TryDirection(camera.WorldToScreenPoint(body.min).x,
                camera.WorldToScreenPoint(body.max).x, bar.xMin * GuiScale, bar.xMax * GuiScale,
                player.HorizontalDirection, out bool clockwise)) return;
            var flip = player.GetComponent<PlayerCelebrationFlip>();
            if (flip == null) flip = player.gameObject.AddComponent<PlayerCelebrationFlip>();
            flip.Begin(Mathf.Clamp(duration, .35f, .7f), clockwise);
        }
        private void OnCelebrationBarStarted(EnemyColor color)
        {
            if (!grid.HasColorClearBar(color)) return;
            if (partialClearCelebration) return;
            int index = startedBarCount++;
            soundEffects?.PlayCue(SoundEffect.BarPowerUp, ColorClearBarAnimation.TonePitch(index + 1));
        }
        private void OnColorCleared(EnemyColor color)
        {
            clearBarAnimation.Begin(color);
            int bars = Fleet.ClearedColorCount;
            soundEffects?.PlayCue(SoundEffect.ColorClear, ColorClearBarAnimation.TonePitch(bars));
            if (grid.AllColorClearBarsFilled) soundEffects?.PlayCue(SoundEffect.Jackpot);
        }
        private void OnColorClearStreakReset(List<EnemyColor> colors)
        {
            if (State != RunState.Playing || colors.Count == 0) return;
            float duration = CurrentBeatDuration;
            LostBarAnimation.Begin(colors, duration: duration, simultaneousPowerDown: true);
            soundEffects?.PlayCueForDuration(SoundEffect.BarPowerDown, duration);
        }
        private void OnMatchCleared(int count, bool fleetCleared, int scoreWeight)
            => AwardClear(count, fleetCleared, scoreWeight, true);
        private void OnMatchColorScored(EnemyColor color)
        {
            if (Progress.RegisterShotColor(color)) ObserveComboStreak();
        }
        private void OnOrangeBurstCleared(int count, bool fleetCleared, int scoreWeight)
            => AwardClear(count, fleetCleared, scoreWeight, false);
        private void AwardClear(int count, bool fleetCleared, int scoreWeight, bool applyCombo)
        {
            int before = Progress.Level;
            int combo = applyCombo ? Progress.ScoringComboMultiplier : 1;
            long basePoints = (100L + 10L * (before - 1)) * scoreWeight;
            long fleetBonus = fleetCleared ? 10000L * before : 0;
            long earned = Progress.RegisterClear(count, fleetCleared, scoreWeight, applyCombo);
            CombatResolved?.Invoke(before, count, fleetCleared, applyCombo);
            long previousPoints = 0;
            if (applyCombo && accumulatingShotScore)
            {
                previousPoints = shotScore;
                shotScore += earned;
            }
            feedback = ScoreCalculation(basePoints, combo, fleetBonus, previousPoints);
            if (Progress.Level > before)
            {
                if (!PresentationPreview && !IsAutomatedPlaytest) RecordReachedLevel(Progress.Level);
                soundEffects?.PlayCue(SoundEffect.LevelUp);
                feedback += "  /  Level " + Progress.Level;
                foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                    if (CurrentLevelDefinition == null && RunProgress.UnlockLevel(color) > before && RunProgress.UnlockLevel(color) <= Progress.Level)
                        feedback += "  /  " + color + " unlocked";
            }
            if (grid != null && grid.AllColorClearBarsFilled && player != null && player.GrantLife(false))
            {
                rewardedLifeSlot = player.ExtraLives - 1;
                feedback += "  /  +1 Life";
            }
            feedbackRemaining = 3f;
        }

        public static string ScoreCalculation(long basePoints, int multiplier, long fleetBonus, long previousPoints = 0) =>
            (previousPoints > 0 ? previousPoints.ToString("N0") + " + " : "") +
            basePoints.ToString("N0") + " x" + multiplier +
            (fleetBonus > 0 ? " + " + fleetBonus.ToString("N0") : "") +
            " = +" + (previousPoints + basePoints * multiplier + fleetBonus).ToString("N0");
        public void Configure(PlayerMovement controller)
        {
            player = controller;
            SubscribeToPlayer();
        }
        internal PlayerMovement Player => player;
        public float MagicTextGrowthScale => MagicGrowthScale();
        public float MagicGrowthScale(bool offbeat = false) => State == RunState.Playing || State == RunState.Refilling ?
            MagicPowerPresentation.TextGrowthScale(player != null ? player.ActiveMagicBars : 0,
                musicPlayer != null ? musicPlayer.BeatPosition : Time.time / FullSetCelebration.StepDuration, offbeat) : 1;
        private void Start()
        {
            started = true;
            if (Camera.main != null)
            {
                var magic = Camera.main.GetComponent<MagicPowerPresentation>();
                if (magic == null) magic = Camera.main.gameObject.AddComponent<MagicPowerPresentation>();
                magic.Configure(this, player);
            }
            if (Application.isPlaying && DeveloperOptions.Available) PlaytestHarness.Attach(this, player);
            if (grid.Fleet.Count == 0 && State == RunState.Playing)
            {
                bool start = startAfterReload;
                startAfterReload = false;
                if (PresentationPreview) StartLevel = rememberedStartLevel;
                else SetSelectableStartLevel(rememberedStartLevel);
                State = RunState.MainMenu;
                menuTitleElapsed = 0;
                menuEyeDelayElapsed = menuEyeDelaySeconds = 0;
                menuEyeDelayStarted = false;
                menuGaze.Reset();
                FirstLaunchIntro = !PresentationPreview && PlayerPrefs.GetInt(MenuIntroSeenKey, 0) == 0;
                MenuEntrance = FirstLaunchIntro ? MenuIntroAnimation.Entrance.TractorBeam :
                    (MenuIntroAnimation.Entrance)Random.Range(0, 3);
                Suspend(true);
                musicPlayer?.UpdatePlayback();
                if (start) StartRun();
            }
        }

        public void StartRun()
        {
            if (State != RunState.MainMenu) return;
            greenTempoMultiplier = greenTempoTarget = greenTempoStart = 1;
            greenTempoElapsed = 0;
            RefreshGameplayTempo();
            MetronomeMenuOpen = false;
            SongSelection.Close();
            LoadLevelMap();
            if (levelMapError != null) { Debug.LogError(levelMapError, this); return; }
            Progress.LevelCatalog = levelCatalog;
            if (levelCatalog != null)
            {
                var errors = levelCatalog.ValidationErrors();
                if (errors.Count > 0)
                {
                    Debug.LogError("Cannot start level progression: " + string.Join(" ", errors), this);
                    return;
                }
            }
            MenuArriving = false;
            inputBlockedThrough = Time.frameCount + 1;
            leaderboardRecorded = false;
            gameOverElapsed = 0;
            ElapsedRoundSeconds = 0;
            LastLeaderboardRank = 0;
            menuLevelDigits = "";
            if (!PresentationPreview && !IsAutomatedPlaytest) SetSelectableStartLevel(StartLevel);
            GetComponent<PlaytestHarness>()?.PrepareRun();
            Progress.Reset(StartLevel);
            Fleet.ResetCycle();
            LostBarAnimation.Reset();
            spawner.ResetSequences();
            clearBarAnimation.Reset();
            accumulatingShotScore = false;
            feedbackRemaining = 0;
            rewardedLifeSlot = -1;
            ResetComboPresentation();
            if (player != null) player.ResetForRun();
            GetComponent<PlaytestHarness>()?.BeginRecording();
            State = RunState.Playing;
            musicPlayer?.UpdatePlayback();
            BeginBatch(spawner.PlanNextFleet(),
                musicPlayer != null ? musicPlayer.SecondsForBeats(1) : CurrentBeatDuration);
        }
        private void TickGreenTempo(float seconds)
        {
            float target = 1 + CombatBalance.SpecialGreenTempoIncrease *
                ((grid != null ? grid : GetComponent<EnemyGrid>())?.ActiveGreenTempoCount ?? 0);
            if (!Mathf.Approximately(target, greenTempoTarget))
            {
                // A fresh activation still raises tempo while earlier losses are easing out.
                if (target > greenTempoTarget)
                    greenTempoMultiplier = Mathf.Min(1 + CombatBalance.SpecialGreenTempoIncrease * GridModel.Columns * GridModel.Rows,
                        greenTempoMultiplier + target - greenTempoTarget);
                greenTempoTarget = target;
                greenTempoStart = greenTempoMultiplier;
                greenTempoElapsed = 0;
                greenTempoDuration = Mathf.Max(.0001f, CurrentBeatDuration);
                if (target >= greenTempoMultiplier) greenTempoMultiplier = target;
            }
            if (greenTempoMultiplier > greenTempoTarget && !IsPaused && !SongSelection.IsOpen)
            {
                greenTempoElapsed += Mathf.Max(0, seconds);
                greenTempoMultiplier = Mathf.Lerp(greenTempoStart, greenTempoTarget,
                    Mathf.SmoothStep(0, 1, greenTempoElapsed / greenTempoDuration));
            }
        }

        private void RefreshGameplayTempo()
        {
            // Edit-mode previews and checks must not rewrite the project's time settings.
            if (Application.isPlaying) ApplyGameplayTempo(TempoMultiplier);
        }

        private void ApplyGameplayTempo(float multiplier)
        {
            if (Mathf.Approximately(appliedTempoMultiplier, multiplier)) return;
            float change = multiplier / appliedTempoMultiplier;
            if (IsPaused) previousTimeScale *= change;
            else Time.timeScale *= change;
            appliedTempoMultiplier = multiplier;
        }

        public void Pause()
        {
            if (IsPaused || State == RunState.MainMenu || State == RunState.GameOver || State == RunState.Dying) return;
            RefreshGameplayTempo();
            IsPaused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            if (soundEffects != null) soundEffects.SetPaused(true);
            musicPlayer?.UpdatePlayback();
            player.CancelPointer();
            Suspend(true);
        }
        public void Resume()
        {
            MetronomeMenuOpen = false;
            SongSelection.Close();
            if (!IsPaused) return;
            RefreshGameplayTempo();
            IsPaused = false;
            Time.timeScale = previousTimeScale;
            if (soundEffects != null) soundEffects.SetPaused(false);
            inputBlockedThrough = Time.frameCount + 1;
            Suspend(State != RunState.Playing);
            musicPlayer?.UpdatePlayback();
        }
        private void OnApplicationFocus(bool focused) { if (!PresentationPreview && started && !focused && !IsAutomatedPlaytest) Pause(); }
        private void OnApplicationPause(bool paused) { if (!PresentationPreview && started && paused) Pause(); }
        private Rect PauseButton
        {
            get
            {
                var camera = Camera.main;
                var corner = camera != null ? ToGuiPoint(camera.WorldToScreenPoint(new Vector3(3, 5.3f, 0))) :
                    new Vector3(GuiWidth, GuiHeight - 8);
                return new Rect(corner.x - 58, GuiHeight - corner.y, 48, 44);
            }
        }
        private bool PauseButtonVisible => !PresentationPreview && MobileMenuActive && !IsPaused &&
            (State == RunState.Playing || State == RunState.Refilling);
        public bool PointerOverMenu(Vector2 point) => IsPaused || State == RunState.MainMenu || State == RunState.GameOver ||
            PauseButtonVisible && PauseButton.Contains(FlipGuiY(ToGuiPoint(point)));

        private void LoadLevelMap()
        {
            if (levelMapOverridden || levelMap == null) return;
            try { levelCatalog = LevelCatalog.Load(levelMap); levelMapError = null; }
            catch (System.FormatException error) { levelCatalog = null; levelMapError = error.Message; }
        }

        private void OnEnable()
        {
            LoadLevelMap();
            Progress.LevelCatalog = levelCatalog;
            soundEffects = GetComponent<SoundEffects>();
            if (soundEffects == null) soundEffects = gameObject.AddComponent<SoundEffects>();
            musicPlayer = GetComponent<GameplayMusicPlayer>();
            if (musicPlayer == null) musicPlayer = gameObject.AddComponent<GameplayMusicPlayer>();
            musicPlayer.Configure(this);
            grid = GetComponent<EnemyGrid>();
            spawner = GetComponent<EnemyRowSpawner>();
            movement = GetComponent<EnemyGridMovement>();
            grid.FleetCleared += BeginRefill;
            ClearCelebration.BarStarted += OnCelebrationBarStarted;
            ClearCelebration.BarSpiked += OnCelebrationBarSpiked;
            grid.MatchCleared += OnMatchCleared;
            grid.MatchColorScored += OnMatchColorScored;
            grid.OrangeBurstCleared += OnOrangeBurstCleared;
            grid.ColorCleared += OnColorCleared;
            grid.ColorClearStreakReset += OnColorClearStreakReset;
            SubscribeToPlayer();
        }
        private void OnDisable()
        {
            ClearCelebration.Reset();
            LostBarAnimation.Reset();
            Fleet?.Reserve(null);
            clearBarAnimation.Reset();
            ClearCelebration.BarStarted -= OnCelebrationBarStarted;
            ClearCelebration.BarSpiked -= OnCelebrationBarSpiked;
            if (player != null) player.SetCelebrationColor(null);
            ApplyGameplayTempo(1);
            if (IsPaused) { Time.timeScale = previousTimeScale; IsPaused = false; }
            if (soundEffects != null) soundEffects.SetPaused(false);
            if (grid != null) grid.FleetCleared -= BeginRefill;
            if (grid != null) grid.MatchCleared -= OnMatchCleared;
            if (grid != null) grid.MatchColorScored -= OnMatchColorScored;
            if (grid != null) grid.OrangeBurstCleared -= OnOrangeBurstCleared;
            if (grid != null) grid.ColorCleared -= OnColorCleared;
            if (grid != null) grid.ColorClearStreakReset -= OnColorClearStreakReset;
            UnsubscribeFromPlayer();
            musicPlayer?.StopPlayback();
        }

        private void SubscribeToPlayer()
        {
            if (player != null && soundEffects != null)
            {
                var shot = player.GetComponentInChildren<TongueShot>();
                if (shot != null)
                {
                    var audio = shot.GetComponent<TongueSoundEffects>();
                    if (audio == null) audio = shot.gameObject.AddComponent<TongueSoundEffects>();
                    audio.Configure(shot, soundEffects);
                }
            }
            if (player == subscribedPlayer) return;
            UnsubscribeFromPlayer();
            subscribedPlayer = player;
            if (subscribedPlayer == null) return;
            subscribedPlayer.ShotAccepted += OnShotAccepted;
            subscribedPlayer.ShotCompleted += OnShotCompleted;
            subscribedPlayer.ShotRetractionStarted += OnShotRetractionStarted;
            subscribedPlayer.PlayerHit += OnPlayerHit;
            subscribedPlayer.FatalHit += BeginPlayerDeath;
        }

        private void UnsubscribeFromPlayer()
        {
            if (subscribedPlayer == null) return;
            subscribedPlayer.ShotAccepted -= OnShotAccepted;
            subscribedPlayer.ShotCompleted -= OnShotCompleted;
            subscribedPlayer.ShotRetractionStarted -= OnShotRetractionStarted;
            subscribedPlayer.PlayerHit -= OnPlayerHit;
            subscribedPlayer.FatalHit -= BeginPlayerDeath;
            subscribedPlayer = null;
        }

        private void OnShotAccepted()
        {
            Progress.BeginShot();
            accumulatingShotScore = true;
            shotScore = 0;
        }

        private bool ShotPreservesCombo => player != null && player.Tongue != null &&
            player.Tongue.HitRegularShield && !player.Tongue.LastCompletionCancelled;

        private void OnShotRetractionStarted(bool hit)
        {
            if (hit || ShotPreservesCombo) return;
            Progress.ResetCombo();
            ObserveComboStreak();
        }

        private void OnShotCompleted(bool hit)
        {
            accumulatingShotScore = false;
            Progress.FinishShot(hit || ShotPreservesCombo);
            ObserveComboStreak();
        }

        private void OnPlayerHit()
        {
            FadeComboOnDeath();
        }

        private void Update()
        {
            if (PresentationPreview) { Tick(Time.deltaTime); return; }
            if (PlaytestPanelOpen) { Tick(Time.deltaTime); return; }
            if (OpeningWarningActive || IsPaused || State == RunState.MainMenu || State == RunState.GameOver)
                foreach (var touch in Input.touches)
                    if (touch.phase == TouchPhase.Ended && ActivateMenuTouch(touch.position)) return;
            if ((OpeningWarningActive || State == RunState.GameOver || State == RunState.MainMenu) && Input.touchCount == 0 && Input.GetMouseButtonUp(0) &&
                ActivateMenuTouch(Input.mousePosition)) return;
            if (IsPaused && Input.touchCount == 0 && Input.GetMouseButtonUp(0) &&
                TryActivateOptionsMenu(MenuPointFromScreen(Input.mousePosition, new Vector2(Screen.width, Screen.height), MobileMenuActive),
                    new Vector2(Screen.width, Screen.height), Screen.safeArea, MobileMenuActive)) return;
            foreach (var key in MenuKeys)
                if (Input.GetKeyDown(key) && HandleMenuKey(key)) return;
            Tick(OpeningWarningActive || State == RunState.GameOver || State == RunState.Dying ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private static readonly KeyCode[] MenuKeys = { KeyCode.P, KeyCode.Escape, KeyCode.Return,
            KeyCode.KeypadEnter, KeyCode.Space, KeyCode.R, KeyCode.M, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.S, KeyCode.UpArrow, KeyCode.DownArrow,
            KeyCode.I, KeyCode.F7, KeyCode.F9, KeyCode.F10, KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
            KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
            KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9 };

        public bool HandleMenuKey(KeyCode key)
        {
            if (OpeningWarningActive)
            {
                if (key != KeyCode.Space && key != KeyCode.Return && key != KeyCode.KeypadEnter && key != KeyCode.Escape)
                    return false;
                DismissOpeningWarning();
                return true;
            }
            if (State == RunState.Dying || State == RunState.GameOver && !GameOverTitleReady) return false;
            if (SongSelection.IsOpen) return SongSelection.HandleKey(key, musicPlayer, SongMenuLayout);
            if (key == KeyCode.S && SongSelectionMenuAvailable)
            {
                SongSelection.Open();
                player?.CancelPointer();
                return true;
            }
            if (key == KeyCode.F7 || key == KeyCode.F9 || key == KeyCode.F10)
            {
                if (!MetronomeMenuAvailable) return false;
                if (key == KeyCode.F9) musicPlayer?.SetVoiceMetronome(!MetronomeSettings.VoiceEnabled);
                else if (key == KeyCode.F10) MetronomeSettings.VisualEnabled = !MetronomeSettings.VisualEnabled;
                else MetronomeSettings.RoundClockEnabled = !MetronomeSettings.RoundClockEnabled;
                return true;
            }
            if (MetronomeMenuOpen)
            {
                if (key == KeyCode.Escape || key == KeyCode.P) MetronomeMenuOpen = false;
                return true;
            }
            if (DeveloperOptions.Available && key == KeyCode.I)
            {
                TogglePlayerInvincible();
                return true;
            }
            if (HandleMenuLevelDigit(key)) return true;
            if (key == KeyCode.P || key == KeyCode.Escape)
            {
                if (IsPaused) Resume();
                else if (State == RunState.Playing || State == RunState.Refilling) Pause();
                else return false;
                return true;
            }
            if (key == KeyCode.Space)
            {
                if (State == RunState.MainMenu) ActivateMainMenu();
                else if (State == RunState.GameOver) ReturnToMenu();
                else return false;
                return true;
            }
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                if (State == RunState.MainMenu) ActivateMainMenu();
                else if (IsPaused) Resume();
                else if (State == RunState.GameOver) Restart();
                else return false;
                return true;
            }
            if (key == KeyCode.R && State != RunState.MainMenu) { Restart(); return true; }
            if (key == KeyCode.M && (IsPaused || State == RunState.GameOver)) { ReturnToMenu(); return true; }
            if (State == RunState.MainMenu && MainMenuLevelSelectVisible &&
                (key == KeyCode.LeftArrow || key == KeyCode.RightArrow))
            {
                menuLevelDigits = "";
                SetSelectableStartLevel(StartLevel + (key == KeyCode.LeftArrow ? -1 : 1));
                return true;
            }
            return false;
        }

        private bool HandleMenuLevelDigit(KeyCode key)
        {
            if (State != RunState.MainMenu || !MainMenuLevelSelectVisible) return false;
            int digit = MenuDigit(key);
            if (digit < 0) return false;
            if (menuLevelDigits.Length >= 2) menuLevelDigits = "";
            menuLevelDigits += digit.ToString();
            SetSelectableStartLevel(int.Parse(menuLevelDigits));
            return true;
        }

        private static int MenuDigit(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return (int)key - (int)KeyCode.Alpha0;
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return (int)key - (int)KeyCode.Keypad0;
            return -1;
        }

        private bool ActivateMenuTouch(Vector2 point)
        {
            if (OpeningWarningActive)
            {
                DismissOpeningWarning();
                return true;
            }
            var screen = new Vector2(Screen.width, Screen.height);
            if (TryActivateSongSelection(MenuPointFromScreen(point, screen, MobileMenuActive), screen, Screen.safeArea, MobileMenuActive)) return true;
            if (GetComponent<PlaytestHarness>()?.TryOpenPanelAtScreenPoint(point) == true) return true;
            if (State == RunState.MainMenu)
                return ActivateMainMenuTouch(point, new Vector2(Screen.width, Screen.height), MobileMenuActive);
            point = MenuPointFromScreen(point, new Vector2(Screen.width, Screen.height), MobileMenuActive);
            if (TryActivateMetronomeMenu(point, new Vector2(Screen.width, Screen.height), Screen.safeArea, MobileMenuActive)) return true;
            if (State == RunState.GameOver)
            {
                if (!GameOverTitleReady) return false;
                ReturnToMenu();
                return true;
            }
            float width = MenuWidth;
            float x = (GuiWidth - width) / 2, y = MenuAnchorY(State == RunState.MainMenu);
            if (MenuPrimaryButtonRect(x, y, width).Contains(point))
            { if (State == RunState.MainMenu) StartRun(); else if (IsPaused) Resume(); else Restart(); return true; }
            if (State == RunState.MainMenu && MenuLevelDownRect(x, y, width).Contains(point))
            { menuLevelDigits = ""; StartLevel--; return true; }
            if (State == RunState.MainMenu && MenuLevelUpRect(x, y, width).Contains(point))
            { menuLevelDigits = ""; StartLevel++; return true; }
            if (DeveloperOptions.Available && DevInvincibleButtonRect(x, y, width, State == RunState.MainMenu, IsPaused).Contains(point))
            { TogglePlayerInvincible(); return true; }
            if (IsPaused && new Rect(x, y + 42, width, 48).Contains(point)) { Restart(); return true; }
            if (State != RunState.MainMenu && new Rect(x, y + (IsPaused ? 102 : 42), width, 48).Contains(point))
            { ReturnToMenu(); return true; }
            return false;
        }

        private bool ActivateMainMenuTouch(Vector2 point, Vector2 screen, bool mobile)
        {
            float scale = GuiScaleFor(mobile, screen.x, screen.y);
            point = MenuPointFromScreen(point, screen, mobile);
            if (!TryActivateMainMenuLevelSelect(point, screen.x / scale, screen.y / scale, mobile))
                ActivateMainMenu();
            return true;
        }

        private bool TryActivateMainMenuLevelSelect(Vector2 point, float width, float height, bool mobile)
        {
            if (!MainMenuLevelSelectVisible) return false;
            var row = MainMenuLevelSelectRect(width, height, mobile);
            if (MainMenuLevelSelectLeftButtonRect(row).Contains(point))
            {
                menuLevelDigits = "";
                SetSelectableStartLevel(StartLevel - 1);
                return true;
            }
            if (MainMenuLevelSelectRightButtonRect(row).Contains(point))
            {
                menuLevelDigits = "";
                SetSelectableStartLevel(StartLevel + 1);
                return true;
            }
            // Gaps and the number belong to the selector, never the tap-to-start background.
            return MainMenuLevelSelectTouchRect(row, mobile).Contains(point);
        }

        private static void TogglePlayerInvincible() => DeveloperOptions.PlayerInvincible = !DeveloperOptions.PlayerInvincible;

        private bool TryActivateOptionsMenu(Vector2 point, Vector2 screen, Rect safe, bool mobile) =>
            TryActivateSongSelection(point, screen, safe, mobile) || TryActivateMetronomeMenu(point, screen, safe, mobile);

        private bool TryActivateSongSelection(Vector2 point, Vector2 screen, Rect safe, bool mobile)
        {
            if (!SongSelectionMenuAvailable || musicPlayer == null) return false;
            float scale = GuiScaleFor(mobile, screen.x, screen.y);
            if (SongSelection.IsOpen)
            {
                SongSelection.Activate(point, musicPlayer, new SongSelectionMenu.Layout(screen, safe, scale, mobile));
                return true;
            }
            if (!SongSelectionMenu.ButtonRect(screen, safe, scale, mobile).Contains(point)) return false;
            SongSelection.Open();
            player?.CancelPointer();
            return true;
        }

        private bool TryActivateMetronomeMenu(Vector2 point, Vector2 screen, Rect safe, bool mobile)
        {
            if (!MetronomeMenuAvailable) return false;
            float scale = GuiScaleFor(mobile, screen.x, screen.y);
            if (MetronomeMenuOpen)
            {
                var panel = MetronomeMenuRect(screen, safe, scale, mobile);
                if (MetronomeMenuRowRect(panel, 0).Contains(point))
                    musicPlayer?.SetVoiceMetronome(!MetronomeSettings.VoiceEnabled);
                else if (MetronomeMenuRowRect(panel, 1).Contains(point))
                    MetronomeSettings.VisualEnabled = !MetronomeSettings.VisualEnabled;
                else if (MetronomeMenuRowRect(panel, 2).Contains(point))
                    MetronomeSettings.RoundClockEnabled = !MetronomeSettings.RoundClockEnabled;
                else if (MetronomeMenuRowRect(panel, 3).Contains(point)) MetronomeMenuOpen = false;
                return true;
            }
            if (!MetronomeMenuButtonRect(screen, safe, scale, mobile).Contains(point)) return false;
            MetronomeMenuOpen = true;
            return true;
        }

        private void DismissOpeningWarning()
        {
            epilepsyWarningElapsed = EpilepsyWarningSeconds;
            epilepsyWarningShown = true;
            inputBlockedThrough = Time.frameCount + 1;
            player?.CancelPointer();
            musicPlayer?.UpdatePlayback();
        }

        public void Tick(float seconds)
        {
            TickGreenTempo(seconds);
            RefreshGameplayTempo();
            if (IsPaused || SongSelection.IsOpen) return;
            if (OpeningWarningActive)
            {
                epilepsyWarningElapsed = Mathf.Min(EpilepsyWarningSeconds,
                    epilepsyWarningElapsed + Mathf.Max(0, seconds));
                if (epilepsyWarningElapsed >= EpilepsyWarningSeconds)
                    DismissOpeningWarning();
                return;
            }
            if (State == RunState.MainMenu)
            {
                float elapsed = Mathf.Max(0, seconds);
                if (MenuArriving)
                {
                    menuGaze.Tick(elapsed, null);
                    menuArrivalElapsed += elapsed;
                    if (MenuArrivalProgress >= 1)
                    {
                        float overshoot = menuArrivalElapsed - MenuArrivalSeconds;
                        StartRun();
                        if (player != null) CharacterVisuals.Ensure(player.gameObject).BeginLanding(overshoot);
                    }
                }
                else
                {
                    float previousTitleElapsed = menuTitleElapsed;
                    menuTitleElapsed = Mathf.Min(MenuWordIntroDuration, menuTitleElapsed + elapsed);
                    float afterIntro = Mathf.Max(0, elapsed -
                        Mathf.Max(0, MenuWordIntroDuration - previousTitleElapsed));
                    if (menuTitleElapsed >= MenuWordIntroDuration)
                    {
                        TickMenuEyeTrackingDelay(afterIntro);
                        menuGaze.Tick(afterIntro, MenuEyeTrackingReady && !MobileMenuActive && Input.mousePresent ?
                            MenuEyeTarget(MenuPlayerRect()) : null);
                    }
                    if (FirstLaunchIntro && !menuIntroRecorded && !PresentationPreview && menuTitleElapsed >= MenuWordIntroDuration)
                    {
                        PlayerPrefs.SetInt(MenuIntroSeenKey, 1);
                        PlayerPrefs.Save();
                        menuIntroRecorded = true;
                    }
                }
                return;
            }
            if (State == RunState.GameOver)
            {
                gameOverElapsed += Mathf.Max(0, seconds);
                musicPlayer?.UpdatePlayback();
                return;
            }
            ObserveComboStreak();
            TickComboPresentation(seconds);
            clearBarAnimation.Tick(seconds);
            if (LostBarAnimation.Active)
            {
                LostBarAnimation.Tick(seconds);
                if (LostBarAnimation.Finished) LostBarAnimation.Reset();
            }
            if (State == RunState.Dying)
            {
                float previousAnimationAge = Mathf.Max(0, deathElapsed - GameOverBlackoutSeconds);
                deathElapsed += Mathf.Max(0, seconds);
                musicPlayer?.UpdatePlayback();
                if (deathElapsed < GameOverBlackoutSeconds)
                {
                    blackout?.Present(GameOverBlackoutOpacity, deathElapsed);
                    return;
                }
                if (!deathAnimationStarted)
                {
                    deathAnimationStarted = true;
                    blackout?.Present(1, GameOverBlackoutSeconds);
                    deathCue = player.BeginFatalDefeat(Progress.Level);
                    hasDeathCue = deathCue != null;
                    // The session owns cue time, including the remainder of a frame crossing the fade boundary.
                    if (deathCue != null) deathCue.enabled = false;
                    blackout.ShowDeath(deathCue);
                }
                float animationAge = Mathf.Max(0, deathElapsed - GameOverBlackoutSeconds);
                blackout?.Present(GameOverBlackoutOpacity);
                if (deathCue != null) deathCue.Tick(animationAge - previousAnimationAge);
                if (hasDeathCue ? deathCue == null || deathCue.Finished : animationAge >= PlayerMeltDeath.Duration) EndGame();
                return;
            }
            ElapsedRoundSeconds += Mathf.Max(0, seconds);
            if (CheckPlayerContact()) return;
            feedbackRemaining = Mathf.Max(0, feedbackRemaining - Mathf.Max(0, seconds));
            if (State != RunState.Refilling) return;
            float celebrationSeconds = Mathf.Max(0, seconds);
            float musicElapsed = 0;
            bool musicTimed = refillMusicClip != null && musicPlayer != null &&
                musicPlayer.TryAdvancePlayback(refillMusicClip, ref refillMusicSeconds, out musicElapsed);
            if (musicTimed)
            {
                celebrationSeconds = musicElapsed;
            }
            else if (refillMusicClip != null)
            {
                // A stopped, replaced or rewound song releases the old clock without stalling the wave.
                refillMusicClip = null;
                if (ClearCelebration.Finished) refillRemaining = refillPostBarBeats * CurrentBeatDuration;
            }
            if (ClearCelebration.Active)
            {
                bool wasFinished = ClearCelebration.Finished;
                ClearCelebration.Tick(celebrationSeconds);
                player.SetCelebrationColor(ClearCelebration.Color);
                if (!ClearCelebration.Finished) return;
                if (!wasFinished)
                {
                    rewardedLifeSlot = -1;
                    if (!musicTimed) refillRemaining = musicPlayer != null && musicPlayer.BeatClockRunning ?
                        musicPlayer.SecondsAtBeat(Mathf.Floor(musicPlayer.BeatPosition) + refillPostBarBeats) -
                            musicPlayer.PlaybackSeconds : refillPostBarBeats * CurrentBeatDuration;
                    if (refillPostBarBeats > 0 && !musicTimed) return;
                }
            }
            if (musicTimed)
            {
                if (refillMusicSeconds < refillSpawnSeconds) return;
            }
            else
            {
                refillRemaining -= Mathf.Max(0, seconds);
                if (refillRemaining > 0) return;
            }
            if (ClearCelebration.Active) player.FinishWaveReturn();
            else if (player.ShotActive || grid.HasDeathEffects) return;
            movement.ResetSweep();
            if (!spawner.SpawnBatch(Fleet.PendingPlan))
            {
                Debug.LogError("Cannot create the next fleet batch.", this);
                EndGame();
                return;
            }
            State = RunState.Playing;
            LostBarAnimation.Reset();
            clearBarAnimation.Reset();
            Suspend(false);
            grid.RefreshSpecials();
            if (musicTimed)
                foreach (var presentation in grid.GetComponentsInChildren<EnemyPresentation>())
                    presentation.SynchronizeArrival(musicPlayer, refillArrivalSeconds, refillArrivalSeconds - refillSpawnSeconds);
            ClearCelebration.Reset();
            refillMusicClip = null;
            player.SetCelebrationColor(null);
            player.RefreshColor();
            player.LifeIcons.ClearRewardColor();
            WaveSpawned?.Invoke();
        }

        private void BeginRefill()
        {
            foreach (var missile in FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
                missile.ClearWithBurst();
            if (CurrentLevelDefinition != null) musicPlayer?.UpdatePlayback();
            var reservation = spawner.PlanNextFleet();
            var plan = reservation?.OccupiedColors();
            if (State == RunState.Playing)
            {
                partialClearCelebration = !grid.AllColorClearBarsFilled;
                refillPostBarBeats = 1;
                startedBarCount = 0;
                var colors = grid.SeenColors();
                var earned = colors.FindAll(grid.HasColorClearBar);
                if (partialClearCelebration) refillPostBarBeats = 0;
                float barBeatCount = partialClearCelebration ? Mathf.Min(1, earned.Count) :
                    MinimumColorClearBarSequenceBeats(earned.Count);
                float barStepBeats = ColorClearBarStepBeats(barBeatCount, earned.Count);
                float holdDuration = 0;
                float[] removalBeatDurations = null;
                refillMusicClip = null;
                if (musicPlayer != null && earned.Count > 0 && musicPlayer.BeatClockRunning)
                {
                    float now = musicPlayer.PlaybackSeconds;
                    float beat = musicPlayer.BeatPositionAtTime(now);
                    float arrivalBeat = RefillDownbeat(musicPlayer, beat, barBeatCount, !partialClearCelebration);
                    refillArrivalSeconds = musicPlayer.SecondsAtBeat(arrivalBeat);
                    float spawnBeat = arrivalBeat - (partialClearCelebration ? 1 : FullSetFleetArrivalBeats);
                    refillSpawnSeconds = musicPlayer.SecondsAtBeat(spawnBeat);
                    // Full bars animate on the beats leading into one (two bars: three, four).
                    // The last bar and fleet growth share beat four instead of adding a beat after the bars.
                    // Partial sets retain their shared power-down across the available window.
                    if (partialClearCelebration)
                        barBeatCount = ColorClearBarSequenceBeats(beat, spawnBeat, 0);
                    barStepBeats = ColorClearBarStepBeats(barBeatCount, earned.Count);
                    float removalStartBeat = (partialClearCelebration ? spawnBeat : arrivalBeat) - barBeatCount;
                    refillMusicClip = musicPlayer.Source.clip;
                    refillMusicSeconds = now;
                    holdDuration = Mathf.Max(0, musicPlayer.SecondsAtBeat(removalStartBeat) - now);
                    removalBeatDurations = new float[earned.Count];
                    for (int i = 0; i < earned.Count; i++)
                    {
                        float stepStart = removalStartBeat + i * barStepBeats;
                        float stepEnd = Mathf.Min(stepStart + barStepBeats, spawnBeat);
                        removalBeatDurations[i] = musicPlayer.SecondsAtBeat(stepEnd) -
                            musicPlayer.SecondsAtBeat(stepStart);
                    }
                }
                float refillDuration = holdDuration + FullSetCelebration.DurationForBars(barBeatCount, CurrentBeatDuration);
                if (removalBeatDurations != null)
                {
                    refillDuration = holdDuration;
                    for (int i = 0; i < removalBeatDurations.Length; i++)
                        refillDuration += Mathf.Max(.0001f, removalBeatDurations[i]);
                }
                if (earned.Count > 0)
                {
                    ClearCelebration.Begin(earned, plan, refillDuration, removalBeatDurations, holdDuration, partialClearCelebration,
                        FullSetCelebration.BarOrder.Varied);
                    refillDuration = ClearCelebration.PlaybackDuration;
                    if (partialClearCelebration) soundEffects?.PlayCueForDuration(SoundEffect.BarPowerDown, refillDuration);
                }
                else ClearCelebration.Reset();
                if (rewardedLifeSlot >= 0)
                {
                    var slots = DisplayedColorClearBarColors;
                    int barIndex = ClearCelebration.LastColor.HasValue ? slots.IndexOf(ClearCelebration.LastColor.Value) : 0;
                    player.LifeIcons.ScheduleWaveGain(rewardedLifeSlot, musicPlayer,
                        ClearCelebration.FinalSpikeTime, barIndex, slots.Count, ClearCelebration.FinalBarOverlaps, ClearCelebration.LastColor);
                }
                // A developer override can exclude every earned color. Reserve the final reward color anyway.
                if (CurrentLevelDefinition == null && plan != null && plan.Length > 0 && ClearCelebration.LastColor.HasValue &&
                    System.Array.IndexOf(plan, ClearCelebration.LastColor.Value) < 0)
                {
                    var replaced = plan[0];
                    if (ClearCelebration.LastColor == EnemyColor.Orange) plan[0] = EnemyColor.Orange;
                    else for (int i = 0; i < plan.Length; i++)
                        if (plan[i] == replaced) plan[i] = ClearCelebration.LastColor.Value;
                }
                player.SetCelebrationColor(ClearCelebration.Active ? ClearCelebration.Color : (Color?)null);
                if (CurrentLevelDefinition == null) reservation = spawner.SnapshotPlan(plan);
                BeginBatch(reservation, refillDuration);
                return;
            }
            BeginBatch(reservation, 0);
        }

        private float CurrentBeatDuration => musicPlayer != null ? musicPlayer.BeatDuration : FullSetCelebration.StepDuration;

        public static float SecondsToNextBeat(float beatPosition, float beatDuration) =>
            (Mathf.Floor(beatPosition) + 1 - beatPosition) * Mathf.Max(.0001f, beatDuration);

        public const float FullSetFleetArrivalBeats = .5f;

        public static float RefillDownbeat(float clearBeat, float minimumBarBeats, bool allBarsFull,
            int offset = GameplayMusicPlayer.DownbeatOffsetBeats)
        {
            // A full set's last bar and fleet arrival both fit inside the final count-off beat.
            float startBeat = allBarsFull ? Mathf.Ceil(clearBeat) : clearBeat;
            float earliestArrival = startBeat + Mathf.Max(0, minimumBarBeats) + (allBarsFull ? 0 : 1);
            return Mathf.Ceil((earliestArrival - offset) / GameplayMusicPlayer.BeatsPerMeasure) *
                GameplayMusicPlayer.BeatsPerMeasure + offset;
        }

        public static float RefillDownbeat(GameplayMusicPlayer music, float clearBeat, float minimumBarBeats,
            bool allBarsFull) => RefillDownbeat(clearBeat, minimumBarBeats, allBarsFull, music.CurrentDownbeatOffsetBeats);

        public static float MinimumColorClearBarSequenceBeats(int earnedBars) =>
            Mathf.Max(0, earnedBars);

        public static float ColorClearBarSequenceBeats(float clearBeat, float spawnBeat, int postBarBeats) =>
            Mathf.Max(0, spawnBeat - clearBeat - Mathf.Max(0, postBarBeats));

        public static float ColorClearBarStepBeats(float sequenceBeats, int earnedBars) =>
            earnedBars > 0 ? Mathf.Max(.0001f, sequenceBeats) / earnedBars : 0;

        private void BeginBatch(FleetPlan reservation, float duration)
        {
            if (State != RunState.Playing) return;
            State = RunState.Refilling;
            refillRemaining = Mathf.Max(0, duration);
            if (ClearCelebration.Active) refillRemaining = 0;
            else if (musicPlayer != null && musicPlayer.BeatClockRunning && musicPlayer.Source.clip != null)
            {
                float now = musicPlayer.PlaybackSeconds;
                float downbeat = RefillDownbeat(musicPlayer, musicPlayer.BeatPositionAtTime(now), 0, false);
                refillMusicClip = musicPlayer.Source.clip;
                refillMusicSeconds = now;
                refillSpawnSeconds = musicPlayer.SecondsAtBeat(downbeat - 1);
                refillArrivalSeconds = musicPlayer.SecondsAtBeat(downbeat);
            }
            Fleet.Reserve(reservation);
            var reserved = ClearCelebration.LastColor;
            if (reservation == null || !reserved.HasValue || !reservation.Contains(reserved.Value)) reserved = null;
            player.PrepareNextWave(reservation, reserved);
            Suspend(true);
        }

        public float ContactFraction(float enemyTravelRelativeToPlayer)
        {
            if (grid == null || State != RunState.Playing || IsPaused || player == null || !player.Alive ||
                player.Invulnerable || DeveloperOptions.PlayerInvincible) return 1;
            var target = player.HitBounds;
            float fraction = 1;
            for (int column = 0; column < GridModel.Columns; column++)
            {
                var cell = grid.Model.At(column, GridModel.Rows - 1);
                if (cell == null) continue;
                var enemy = grid.View(cell.Id);
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var body = enemy.HitBounds;
                if (body.max.y < target.min.y || body.min.y > target.max.y) continue;
                if (body.max.x >= target.min.x && body.min.x <= target.max.x) return 0;
                if (Mathf.Abs(enemyTravelRelativeToPlayer) < .000001f) continue;
                float a = (target.min.x - body.max.x) / enemyTravelRelativeToPlayer;
                float b = (target.max.x - body.min.x) / enemyTravelRelativeToPlayer;
                float enter = Mathf.Min(a, b), leave = Mathf.Max(a, b);
                if (leave >= 0 && enter <= 1) fraction = Mathf.Min(fraction, Mathf.Max(0, enter));
            }
            return fraction;
        }

        public bool CheckPlayerContact()
        {
            if (State == RunState.Playing && !IsPaused && player != null && !DeveloperOptions.PlayerInvincible &&
                PlayerRowIsFull())
            {
                BeginPlayerDeath();
                return true;
            }
            if (ContactFraction(0) != 0) return false;
            return player != null && player.Hit();
        }

        private bool PlayerRowIsFull()
        {
            if (grid == null || spawner == null) return false;
            var target = player.LandingHitBounds;
            for (int row = 0; row < GridModel.Rows; row++)
            {
                int occupied = 0;
                for (int column = 0; column < GridModel.Columns; column++)
                {
                    var cell = grid.Model.At(column, row);
                    var enemy = cell != null ? grid.View(cell.Id) : null;
                    if (enemy == null || !enemy.isActiveAndEnabled) continue;
                    var bounds = enemy.HitBounds;
                    if (bounds.max.y >= target.min.y && bounds.min.y <= target.max.y) occupied++;
                }
                if (occupied >= spawner.CurrentRowWidth) return true;
            }
            return false;
        }

        public void FinishContactMove(float fraction)
        {
            if (fraction < 1) player.Hit();
            else CheckPlayerContact();
        }

        private void BeginPlayerDeath()
        {
            if (DeveloperOptions.PlayerInvincible) return;
            if (State != RunState.Playing && State != RunState.Refilling) return;
            FadeComboOnDeath();
            deathTempoMultiplier = TempoMultiplier;
            GameOverBlackoutSeconds = musicPlayer != null ? musicPlayer.SecondsForBeats(GameOverBlackoutBeats) :
                GameOverBlackoutBeats * CurrentBeatDuration;
            // Death runs on unscaled time; retain the accelerated song's real beat length.
            GameOverBlackoutSeconds /= deathTempoMultiplier;
            State = RunState.Dying;
            RefreshGameplayTempo();
            deathElapsed = 0;
            deathAnimationStarted = false;
            musicPlayer?.UpdatePlayback();
            player.PrepareFatalDefeat();
            blackout = GameOverBlackout.Create(transform, null);
            blackout.HoldPlayer(CharacterVisuals.Ensure(player.gameObject).Root, GameOverBlackoutSeconds);
            Suspend(true);
        }

        private void OnDestroy()
        {
            MenuBacklight.Shutdown();
            MenuIntroAnimation.Shutdown();
            if (blackout != null)
            {
                if (Application.isPlaying) Destroy(blackout.gameObject);
                else DestroyImmediate(blackout.gameObject);
            }
            if (deathCue == null) return;
            if (Application.isPlaying) Destroy(deathCue.gameObject);
            else DestroyImmediate(deathCue.gameObject);
        }

        private void EndGame()
        {
            if (State == RunState.GameOver) return;
            State = RunState.GameOver;
            gameOverElapsed = 0;
            RecordLeaderboard();
            Suspend(true);
            if (blackout == null) blackout = GameOverBlackout.Create(transform, null);
            blackout.Present(GameOverBlackoutOpacity);
            musicPlayer?.UpdatePlayback();
        }

        private void RecordLeaderboard()
        {
            if (leaderboardRecorded || PresentationPreview || IsAutomatedPlaytest) return;
            leaderboardRecorded = true;
            LastLeaderboardRank = LocalLeaderboard.Submit(Progress.Score, Progress.Level);
        }

        private void Suspend(bool suspended)
        {
            movement.enabled = !suspended;
            grid.enabled = !suspended;
            spawner.enabled = !suspended;
            player.ControlsLocked = IsPaused || State == RunState.MainMenu || State == RunState.GameOver || State == RunState.Dying;
            if (State == RunState.GameOver) player.StopActions();
            foreach (var ability in GetComponentsInChildren<EnemyAbilities>()) ability.Suspended = suspended;
            foreach (var missile in FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
                missile.Suspended = IsPaused || State == RunState.MainMenu || State == RunState.GameOver || State == RunState.Dying;
        }

        public void Restart()
        { Reload(true); }
        public void ReturnToMenu() => Reload(false);
        private void Reload(bool start)
        {
            if (restarting) return;
            restarting = true;
            if (IsPaused) { Time.timeScale = previousTimeScale; IsPaused = false; }
            startAfterReload = start;
            rememberedStartLevel = StartLevel;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnGUI()
        {
            var playtesting = GetComponent<PlaytestHarness>();
            if (PlaytestPanelOpen) { playtesting?.DrawMenu(); return; }
            var previousMatrix = GUI.matrix;
            float scale = GuiScale;
            if (!Mathf.Approximately(scale, 1))
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            var developmentMatrix = GUI.matrix;
            try
            {
                DrawGui();
                GUI.matrix = developmentMatrix;
                if (SongSelectionMenuAvailable && !SongSelection.IsOpen)
                    SongSelectionMenu.DrawButton(SongSelectionMenu.ButtonRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, scale, MobileMenuActive), ComboFont);
                if (MetronomeMenuAvailable && !MetronomeMenuOpen && !SongSelection.IsOpen)
                    DrawMetronomeMenuButton();
                var overlays = DeveloperOverlayRects;
                if (VisualMetronomeVisible) DrawDevelopmentMetronome(overlays.Metronome);
                if (RoundClockVisible) DrawRoundClock(overlays.Clock);
            }
            finally { GUI.matrix = previousMatrix; }
            if (!SongSelection.IsOpen) playtesting?.DrawMenu();
        }

        private static Rect MetronomeSafeRect(Vector2 screen, Rect safeArea, float scale) =>
            new Rect(safeArea.x / scale, (screen.y - safeArea.yMax) / scale, safeArea.width / scale, safeArea.height / scale);

        public static Rect MetronomeMenuButtonRect(Vector2 screen, Rect safeArea, float scale, bool mobile)
        {
            var safe = MetronomeSafeRect(screen, safeArea, scale);
            float height = mobile ? Mathf.Clamp(screen.x / scale * .12f, 44, 72) : 40;
            return new Rect(safe.x + 8, safe.y + 8, Mathf.Min(168, safe.width * .38f), height);
        }

        public static Rect MetronomeMenuRect(Vector2 screen, Rect safeArea, float scale, bool mobile)
        {
            var safe = MetronomeSafeRect(screen, safeArea, scale);
            float buttonHeight = mobile ? Mathf.Clamp(screen.x / scale * .12f, 44, 72) : 44;
            var size = new Vector2(Mathf.Min(340, safe.width - 16), Mathf.Min(84 + buttonHeight * 4, safe.height - 16));
            return new Rect(safe.center - size / 2, size);
        }

        public static Rect MetronomeMenuRowRect(Rect panel, int row)
        {
            float height = (panel.height - 84) / 4;
            return new Rect(panel.x + 12, panel.y + 48 + row * (height + 8), panel.width - 24, height);
        }

        private void DrawMetronomeMenuButton()
        {
            var rect = MetronomeMenuButtonRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, GuiScale, MobileMenuActive);
            var style = new GUIStyle(GUI.skin.button) { font = ComboFont, fontSize = 16 };
            while (style.fontSize > 9 && style.CalcSize(new GUIContent("Beat & Clock")).x > rect.width - 8) style.fontSize--;
            GUI.Box(rect, "Beat & Clock", style);
        }

        private void DrawMetronomeMenu()
        {
            var previous = GUI.color;
            GUI.color = new Color(0, 0, 0, .94f);
            GUI.DrawTexture(new Rect(0, 0, GuiWidth, GuiHeight), Texture2D.whiteTexture);
            var panel = MetronomeMenuRect(new Vector2(Screen.width, Screen.height), Screen.safeArea, GuiScale, MobileMenuActive);
            GUI.color = Color.white;
            GUI.Box(panel, "");
            var heading = new GUIStyle(GUI.skin.label) { font = ComboFont, fontSize = 20, alignment = TextAnchor.MiddleCenter };
            var title = new Rect(panel.x + 12, panel.y + 10, panel.width - 24, 30);
            FitGameplayText(heading, "BEAT & CLOCK", title);
            GUI.Label(title, "BEAT & CLOCK", heading);
            string[] labels = { "Voice metronome: " + (MetronomeSettings.VoiceEnabled ? "ON" : "OFF"),
                "Visual metronome: " + (MetronomeSettings.VisualEnabled ? "ON" : "OFF"),
                "Round clock: " + (MetronomeSettings.RoundClockEnabled ? "ON" : "OFF"), "Back" };
            for (int row = 0; row < labels.Length; row++)
            {
                var rect = MetronomeMenuRowRect(panel, row);
                var style = new GUIStyle(GUI.skin.button) { fontSize = 18 };
                while (style.fontSize > 10 && style.CalcSize(new GUIContent(labels[row])).x > rect.width - 12) style.fontSize--;
                GUI.Box(rect, labels[row], style);
            }
            GUI.color = previous;
        }

        public static Rect DevelopmentMetronomeRect(Vector2 screen, Rect safeArea, float scale, bool showClock = false,
            Rect? playfield = null) => DevelopmentOverlayLayout.Calculate(screen, safeArea, scale,
                playfield ?? default, showClock, true).Metronome;

        public Rect DevelopmentPlayfieldScreenRect()
        {
            var camera = Camera.main;
            if (camera == null) return default;
            // Use the resting projection so temporary scene pulses never move the readouts.
            var viewport = camera.pixelRect;
            var min = camera.transform.InverseTransformPoint(new Vector3(-PlayerMovement.HalfWidth - .2f, -PlayfieldFrame.OuterTopY - .08f));
            var max = camera.transform.InverseTransformPoint(new Vector3(PlayerMovement.HalfWidth + .2f, PlayfieldFrame.OuterTopY + .08f));
            float unit = viewport.height / (2 * camera.orthographicSize);
            return Rect.MinMaxRect(viewport.center.x + min.x * unit, viewport.center.y + min.y * unit,
                viewport.center.x + max.x * unit, viewport.center.y + max.y * unit);
        }

        public DevelopmentOverlayLayout.Rects DeveloperOverlayRects => DevelopmentOverlayLayout.Calculate(
            new Vector2(Screen.width, Screen.height), Screen.safeArea, GuiScale, DevelopmentPlayfieldScreenRect(),
            RoundClockVisible, VisualMetronomeVisible);

        private void DrawDevelopmentMetronome(Rect rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            int active = musicPlayer != null && musicPlayer.Source.clip != null ? musicPlayer.CurrentBeatInMeasure : 0;
            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            var caption = new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.MiddleLeft };
            caption.normal.textColor = new Color(.65f, .7f, .75f);
            GUI.color = Color.white;
            float captionWidth = rect.width >= 130 && rect.height >= 20 ? 30 : 0;
            if (captionWidth > 0) GUI.Label(new Rect(rect.x + 5, rect.y, 26, rect.height), "BEAT", caption);
            var number = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(20, rect.height * .85f))), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };
            for (int beat = 1; beat <= GameplayMusicPlayer.BeatsPerMeasure; beat++)
            {
                number.normal.textColor = beat == active ? beat == 1 ? new Color(.35f, 1, .6f) : Color.white : new Color(.3f, .35f, .4f);
                float cellWidth = (rect.width - captionWidth) / 4;
                GUI.Label(new Rect(rect.x + captionWidth + (beat - 1) * cellWidth, rect.y, cellWidth, rect.height),
                    beat.ToString(), number);
            }
            GUI.color = previous;
        }

        public static Rect RoundClockRect(Vector2 screen, Rect safeArea, float scale, bool showBeatCounter,
            Rect? playfield = null) => DevelopmentOverlayLayout.Calculate(screen, safeArea, scale,
                playfield ?? default, true, showBeatCounter).Clock;

        private void DrawRoundClock(Rect rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var caption = new GUIStyle(GUI.skin.label) { fontSize = 8, alignment = TextAnchor.MiddleLeft };
            caption.normal.textColor = new Color(.65f, .7f, .75f);
            float captionWidth = rect.width >= 100 && rect.height >= 20 ? 35 : 0;
            if (captionWidth > 0) GUI.Label(new Rect(rect.x + 5, rect.y, 30, rect.height), "TIME", caption);
            var number = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(20, rect.height * .85f))), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight, padding = new RectOffset() };
            var time = new Rect(rect.x + captionWidth + 4, rect.y, rect.width - captionWidth - 8, rect.height);
            while (number.fontSize > 1 && number.CalcSize(new GUIContent(RoundClockText)).x > time.width) number.fontSize--;
            GUI.Label(time, RoundClockText, number);
            GUI.color = previous;
        }

        private void DrawGui()
        {
            if (OpeningWarningActive) { DrawEpilepsyWarning(); return; }
            if (SongSelection.IsOpen)
            { SongSelection.Draw(musicPlayer, SongMenuLayout, new Vector2(GuiWidth, GuiHeight), ComboFont); return; }
            if (MetronomeMenuOpen && IsPaused) { DrawMetronomeMenu(); return; }
            if (State == RunState.MainMenu) { DrawMinimalMainMenu(); return; }
            if (State != RunState.MainMenu) DrawProgress(GameOverScoreOpacity);
            if (State == RunState.GameOver) { DrawGameOver(); return; }
            if (PauseButtonVisible)
            {
                var pause = PauseButton;
                if (GUI.Button(pause, new GUIContent("", "Pause (P)"))) Pause();
                GUI.DrawTexture(new Rect(pause.x + 8, pause.y + 13, 4, 18), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pause.x + 15, pause.y + 13, 4, 18), Texture2D.whiteTexture);
                var hint = new Rect(pause.x + 22, pause.y + 12, 25, 22);
                var hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                var hintMatrix = BeginMagicText(GameplayLabelBounds(hint, "(P)", hintStyle));
                GUI.Label(hint, "(P)", hintStyle);
                GUI.matrix = hintMatrix;
            }
            if (!IsPaused && State != RunState.MainMenu && State != RunState.GameOver) return;
            var previous = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.04f, State == RunState.MainMenu ? .65f : .88f);
            GUI.DrawTexture(new Rect(0, 0, GuiWidth, GuiHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float width = MenuWidth;
            float x = (GuiWidth - width) / 2;
            bool menu = State == RunState.MainMenu;
            float y = MenuAnchorY(menu);
            var heading = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            heading.normal.textColor = Color.white;
            if (menu) DrawMainMenuTitle(y);
            else GUI.Label(new Rect(x, y - 120, width, 48), IsPaused ? "Paused" : "Game Over", heading);
            if (menu) DrawEnemyPreview(new Rect(x, y - 136, width, 82));
            var scoreLabel = new GUIStyle(heading) { fontSize = 18 };
            if (!menu) GUI.Label(new Rect(x, y - 70, width, 26), "Score " + Progress.Score.ToString("N0"), scoreLabel);
            if (!menu && State == RunState.GameOver && LastLeaderboardRank > 0)
                GUI.Label(new Rect(x, y - 46, width, 22), "Leaderboard #" + LastLeaderboardRank, scoreLabel);
            DrawLeaderboard(new Rect(x, y + LeaderboardOffset(menu, IsPaused), width, 150), menu || State == RunState.GameOver);
            var button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            if (GUI.Button(MenuPrimaryButtonRect(x, y, width), menu ? "Play (Enter)" : IsPaused ? "Resume (P / Enter)" : "Restart (R / Enter)", button))
            { if (menu) StartRun(); else if (IsPaused) Resume(); else Restart(); }
            if (menu) DrawStartLevelSelector(x, y, width, button);
            if (IsPaused && GUI.Button(new Rect(x, y + 42, width, 48), "Restart (R)", button)) Restart();
            if (!menu && GUI.Button(new Rect(x, y + (IsPaused ? 102 : 42), width, 48), "Main Menu (M)", button)) ReturnToMenu();
            if (DeveloperOptions.Available)
            {
                string label = "Invincible: " + (DeveloperOptions.PlayerInvincible ? "ON" : "OFF") + " (I)";
                if (GUI.Button(DevInvincibleButtonRect(x, y, width, menu, IsPaused), label, button)) TogglePlayerInvincible();
            }
            GUI.color = previous;
        }

        public const string GameOverTitle = "GAME OVER";
        public static Rect EpilepsyWarningRect(float screenWidth, float screenHeight)
        {
            float width = Mathf.Min(720, Mathf.Max(120, screenWidth - 32));
            float height = Mathf.Min(320, Mathf.Max(200, screenHeight - 32));
            return new Rect((screenWidth - width) / 2, (screenHeight - height) / 2, width, height);
        }

        public static Rect EpilepsyWarningTitleRect(float screenWidth, float screenHeight)
        {
            var warning = EpilepsyWarningRect(screenWidth, screenHeight);
            return new Rect(warning.x, warning.y, warning.width, Mathf.Min(42, warning.height * .25f));
        }

        public static Rect EpilepsyWarningMessageRect(float screenWidth, float screenHeight)
        {
            var warning = EpilepsyWarningRect(screenWidth, screenHeight);
            var title = EpilepsyWarningTitleRect(screenWidth, screenHeight);
            return new Rect(warning.x, title.yMax + 14, warning.width, warning.yMax - title.yMax - 58);
        }

        public static Rect EpilepsyWarningSkipRect(float screenWidth, float screenHeight)
        {
            var warning = EpilepsyWarningRect(screenWidth, screenHeight);
            return new Rect(warning.x, warning.yMax - 32, warning.width, 32);
        }

        private void DrawEpilepsyWarning()
        {
            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, GuiWidth, GuiHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var titleRect = EpilepsyWarningTitleRect(GuiWidth, GuiHeight);
            var messageRect = EpilepsyWarningMessageRect(GuiWidth, GuiHeight);
            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = GuiHeight < 400 ? 16 : GuiHeight < 600 ? 21 : 26,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset()
            };
            title.normal.textColor = Color.white;
            var titleContent = new GUIContent(EpilepsyWarningTitle);
            while (title.fontSize > 10 && title.CalcSize(titleContent).x > titleRect.width) title.fontSize--;
            GUI.Label(titleRect, titleContent, title);
            var message = new GUIStyle(GUI.skin.label)
            {
                fontSize = GuiHeight < 400 ? 12 : GuiHeight < 600 ? 15 : 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset()
            };
            message.normal.textColor = Color.white;
            GUI.Label(messageRect, EpilepsyWarningMessage, message);
            var skip = new GUIStyle(message) { fontSize = GuiHeight < 400 ? 12 : 14 };
            GUI.Label(EpilepsyWarningSkipRect(GuiWidth, GuiHeight), MobileMenuActive ?
                "Tap anywhere to skip" : "Click or press Space / Enter / Esc to skip", skip);
            GUI.color = previous;
        }

        public static Rect GameOverTitleRect(float screenWidth, float screenHeight)
        {
            float width = Mathf.Min(560, screenWidth - 32), height = Mathf.Min(96, screenHeight - 32);
            float top = Mathf.Max(16, screenHeight * .4f - height / 2);
            return new Rect((screenWidth - width) / 2, top, width, height);
        }

        public static Rect GameOverSummaryRect(float screenWidth, float screenHeight)
        {
            var title = GameOverTitleRect(screenWidth, screenHeight);
            return new Rect(title.x, title.yMax + 8, title.width, 26);
        }

        public static string GameOverSummaryText(int level, long score) =>
            "LEVEL " + level + "    SCORE " + score.ToString("N0");

        public static int FitGameOverTitle(GUIStyle style, Rect rect)
        {
            var content = new GUIContent(GameOverTitle);
            while (style.fontSize > 20 &&
                (style.CalcSize(content).x > rect.width || style.CalcHeight(content, rect.width) > rect.height))
                style.fontSize--;
            return style.fontSize;
        }

        private void DrawGameOver()
        {
            float opacity = GameOverTitleOpacity;
            if (opacity <= 0) return;
            var previousColor = GUI.color;
            GUI.color = new Color(1, 1, 1, opacity);
            var title = CreateTitleStyle();
            var rect = GameOverTitleRect(GuiWidth, GuiHeight);
            FitGameOverTitle(title, rect);
            GUI.Label(rect, GameOverTitle, title);
            var summary = new GUIStyle(GUI.skin.label)
            {
                fontSize = GuiHeight < 400 ? 12 : GuiHeight < 600 ? 15 : 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset()
            };
            summary.normal.textColor = Color.white;
            GUI.Label(GameOverSummaryRect(GuiWidth, GuiHeight),
                GameOverSummaryText(Progress.Level, Progress.Score), summary);
            GUI.color = previousColor;
        }

        private static float MenuAnchorY(bool menu)
        {
            return MenuAnchorYFor(GuiHeight, menu);
        }

        public static float MenuAnchorYFor(float guiHeight, bool menu)
        {
            float center = guiHeight / 2f;
            return menu ? Mathf.Min(center, Mathf.Max(190, guiHeight - 278)) : center;
        }

        private static float MenuWidth => Mathf.Min(300, GuiWidth - 32);
        private static Rect MenuPrimaryButtonRect(float x, float y, float width) => new Rect(x, y - 24, width, 54);
        private static Rect MenuLevelDownRect(float x, float y, float width) => new Rect(x, y + 42, 82, 42);
        private static Rect MenuLevelValueRect(float x, float y, float width) => new Rect(x + 86, y + 42, width - 172, 42);
        private static Rect MenuLevelUpRect(float x, float y, float width) => new Rect(x + width - 82, y + 42, 82, 42);
        private static float LeaderboardOffset(bool menu, bool paused) =>
            DeveloperOptions.Available ? (menu ? 138 : paused ? 208 : 104) : (menu ? 112 : paused ? 164 : 104);
        private static Rect DevInvincibleButtonRect(float x, float y, float width, bool menu, bool paused) =>
            new Rect(x, y + (menu ? 90 : paused ? 154 : 96), width, 40);
        public const string MainMenuTitle = "BASS INVADERS";
        public const string MainMenuSubtitle = "THE RHYTHM IS OUT THERE";
        private static readonly string[] MainMenuTitleWords = { "BASS", "INVADERS" };
        private static readonly string[] MainMenuSubtitleWords = { "THE", "RHYTHM", "IS", "OUT", "THERE" };
        private static readonly Color[] MenuSubtitleBeatPalette = {
            new Color(.015f, .45f, 1f), new Color(1f, .025f, .04f), new Color(1f, .62f, .01f),
            new Color(.015f, 1f, .04f), new Color(.5f, .015f, 1f)
        };
        public static int MainMenuAnimatedWordCount => MainMenuTitleWords.Length + MainMenuSubtitleWords.Length;
        public static float MenuWordIntroDuration => MenuIntroAnimation.Duration;
        public static float GuiScaleFor(bool mobilePlatform, float screenWidth, float screenHeight)
        {
            if (!mobilePlatform) return 1;
            float shortest = Mathf.Min(screenWidth, screenHeight);
            if (shortest >= 900) return LargePhoneGuiScale;
            if (shortest >= 700) return MediumPhoneGuiScale;
            return SmallPhoneGuiScale;
        }
#if UNITY_EDITOR
        public static bool ForceMobileMenuForTests { get; set; }
        private static bool MobileMenuActive => Application.isMobilePlatform || ForceMobileMenuForTests;
#else
        private static bool MobileMenuActive => Application.isMobilePlatform;
#endif
        private static float GuiScale => GuiScaleFor(MobileMenuActive, Screen.width, Screen.height);
        private static float GuiWidth => Screen.width / GuiScale;
        private static float GuiHeight => Screen.height / GuiScale;
        private static Vector2 ToGuiPoint(Vector2 point) => point / GuiScale;
        private static Vector3 ToGuiPoint(Vector3 point) => point / GuiScale;
        private static Vector2 FlipGuiY(Vector2 point) => new Vector2(point.x, GuiHeight - point.y);
        public static Vector2 MenuPointFromScreen(Vector2 point, Vector2 screen, bool mobile) =>
            new Vector2(point.x, screen.y - point.y) / GuiScaleFor(mobile, screen.x, screen.y);
        public static Rect MainMenuTitleRect(float screenWidth, float screenHeight, float menuAnchorY)
        {
            float width = Mathf.Min(560, Mathf.Max(120, screenWidth - 32));
            float previewTop = menuAnchorY - 136;
            float bottom = Mathf.Max(28, previewTop - 8);
            float desiredHeight = screenHeight < 420 ? 62 : screenHeight < 700 ? 78 : 96;
            float y = Mathf.Max(12, bottom - desiredHeight);
            return new Rect((screenWidth - width) / 2, y, width, bottom - y);
        }

        public static Rect MainMenuTitleLineRect(Rect titleBlock)
        {
            float height = titleBlock.height * .64f;
            return new Rect(titleBlock.x, titleBlock.y, titleBlock.width, height);
        }

        public static Rect MainMenuSubtitleLineRect(Rect titleBlock)
        {
            var titleLine = MainMenuTitleLineRect(titleBlock);
            return new Rect(titleBlock.x, titleLine.yMax, titleBlock.width, titleBlock.yMax - titleLine.yMax);
        }

        public static int FitMainMenuTitle(GUIStyle style, Rect rect)
        {
            var content = new GUIContent(MainMenuTitle);
            while (style.fontSize > 20 &&
                (style.CalcSize(content).x > rect.width || style.CalcHeight(content, rect.width) > rect.height))
                style.fontSize--;
            return style.fontSize;
        }

        public static int FitMainMenuSubtitle(GUIStyle style, Rect rect)
        {
            var content = new GUIContent(MainMenuSubtitle);
            while (style.fontSize > 11 &&
                (style.CalcSize(content).x > rect.width || style.CalcHeight(content, rect.width) > rect.height))
                style.fontSize--;
            return style.fontSize;
        }

        public static float MainMenuWordRevealProgress(int wordIndex, float elapsed)
        {
            if (wordIndex < 0 || wordIndex >= MainMenuAnimatedWordCount) return 0;
            return wordIndex < MainMenuTitleWords.Length ? 1 : MenuIntroAnimation.SubtitleProgress(elapsed);
        }

        public static Color MainMenuSubtitleBeatColor(float beatPosition, float opacity = 1)
        {
            var color = MainMenuPaletteColor(beatPosition, opacity);
            color = Color.Lerp(color, Color.white, .35f * GameplayMusicPlayer.BeatPulse(beatPosition));
            color.a = Mathf.Clamp01(opacity);
            return color;
        }

        public static Color MainMenuCharacterColor(float beatPosition, float opacity = 1) =>
            MainMenuPaletteColor(beatPosition, opacity);

        private static Color MainMenuPaletteColor(float beatPosition, float opacity)
        {
            var color = MenuSubtitleBeatPalette[PositiveModulo(Mathf.FloorToInt(beatPosition), MenuSubtitleBeatPalette.Length)];
            color.a = Mathf.Clamp01(opacity);
            return color;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        public static Rect ComboMultiplierRestRect(float playfieldX, float textWidth, float headingTop, int viewHeight = 960)
        {
            float width = Mathf.Min(118, textWidth * .35f);
            float height = viewHeight < 400 ? 27 : 34;
            var row = PlayerLifeIcons.RowRect(playfieldX, headingTop, viewHeight);
            return new Rect(playfieldX + textWidth - width, row.center.y - height / 2, width, height);
        }

        public static Rect GameplayScoreRect(float x, float y, float textWidth, int viewHeight)
        {
            float left = PlayerLifeIcons.IconRect(PlayerLifeIcons.RowRect(x, y, viewHeight), 0).xMin;
            return new Rect(left, y, x + textWidth * .65f - 4 - left, GameplayHeadingHeight);
        }

        public static Rect GameplayLevelRect(float x, float y, float textWidth) =>
            new Rect(x + textWidth * .65f, y, textWidth * .35f, GameplayHeadingHeight);

        public static List<EnemyColor> ColorClearBarColors(EnemyRowSpawner spawner, EnemyGrid grid)
        {
            return grid != null ? grid.Fleet.BarColors() : new List<EnemyColor>();
        }

        public static Rect ColorClearBarSlotRect(float x, float bottom, float width, int count, int index)
        {
            if (count <= 0) return new Rect(x, bottom, 0, 0);
            float segmentWidth = width / count;
            return new Rect(x + index * segmentWidth, bottom, Mathf.Max(1, segmentWidth - 2), 5);
        }

        public static float ColorClearBarMusicLevel(float beatPosition, int slot)
        {
            float primary = GameplayMusicPlayer.BeatPulse(beatPosition - slot * .17f);
            float offbeat = GameplayMusicPlayer.BeatPulse(beatPosition + slot * .11f, true) * .55f;
            return Mathf.Clamp01(.18f + .82f * Mathf.Max(primary, offbeat));
        }

        public static Rect ColorClearBarLevelRect(Rect resting, float beatPosition, int slot)
        {
            float level = ColorClearBarMusicLevel(beatPosition, slot);
            float height = Mathf.Max(resting.height, Mathf.Lerp(resting.height * 1.2f, 24, level));
            return new Rect(resting.x, resting.yMax - height, resting.width, height);
        }

        public static Color ColorClearBarTint(EnemyColor color, bool powered, float opacity = 1)
        {
            var source = EnemyPalette.Get(color);
            if (powered) return new Color(source.r, source.g, source.b, .95f * opacity);
            return new Color(Mathf.Lerp(.08f, source.r, .42f), Mathf.Lerp(.1f, source.g, .42f),
                Mathf.Lerp(.12f, source.b, .42f), .28f * opacity);
        }

        public static Color ColorClearBarPowerDownTint(EnemyColor color, float progress, float opacity = 1) =>
            Color.Lerp(ColorClearBarTint(color, true, opacity), ColorClearBarTint(color, false, opacity),
                Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress)));

        public static Rect ComboMultiplierDisplayRect(Rect resting, float screenWidth, float screenHeight, float elapsed)
        {
            if (elapsed >= ComboMilestoneAnimationSeconds) return resting;
            var center = new Vector2(screenWidth * .5f, screenHeight * .5f);
            float scale;
            if (elapsed < ComboMilestonePulseSeconds)
            {
                float pulse = Mathf.Clamp01(elapsed / ComboMilestonePulseSeconds);
                scale = 2.65f + .25f * Mathf.Sin(pulse * Mathf.PI * 2f);
            }
            else
            {
                float shrink = Mathf.Clamp01((elapsed - ComboMilestonePulseSeconds) /
                    (ComboMilestoneAnimationSeconds - ComboMilestonePulseSeconds));
                float eased = Mathf.SmoothStep(0, 1, shrink);
                center = Vector2.Lerp(center, resting.center, eased);
                scale = Mathf.Lerp(2.45f, 1, eased);
            }
            return new Rect(center.x - resting.width * scale * .5f, center.y - resting.height * scale * .5f,
                resting.width * scale, resting.height * scale);
        }

        private void DrawStartLevelSelector(float x, float y, float width, GUIStyle button)
        {
            var value = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            value.normal.textColor = Color.white;
            var stepButton = new GUIStyle(button) { fontSize = 14 };
            if (GUI.Button(MenuLevelDownRect(x, y, width), "- (Left)", stepButton)) { menuLevelDigits = ""; StartLevel--; }
            GUI.Label(MenuLevelValueRect(x, y, width), "Level " + StartLevel, value);
            if (GUI.Button(MenuLevelUpRect(x, y, width), "+ (Right)", stepButton)) { menuLevelDigits = ""; StartLevel++; }
        }

        private GUIStyle CreateTitleStyle()
        {
            var title = new GUIStyle(GUI.skin.label)
            {
                font = ComboFont,
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset()
            };
            title.normal.textColor = Color.white;
            return title;
        }

        private void DrawMainMenuTitle(float menuAnchorY)
        {
            var title = CreateTitleStyle();
            var subtitle = new GUIStyle(title)
            {
                fontSize = 18
            };

            var block = MainMenuTitleRect(GuiWidth, GuiHeight, menuAnchorY);
            var titleRect = MainMenuTitleLineRect(block);
            var subtitleRect = MainMenuSubtitleLineRect(block);
            FitMainMenuTitle(title, titleRect);
            FitMainMenuSubtitle(subtitle, subtitleRect);
            GUI.Label(titleRect, MainMenuTitle, title);
            GUI.Label(subtitleRect, MainMenuSubtitle, subtitle);
        }

        public static Rect MinimalTitleRect(float width, float height, float pulse)
        {
            float scale = 1 + .075f * Mathf.Clamp01(pulse);
            float w = Mathf.Min(640, width - 40) * scale;
            return new Rect((width - w) / 2, height * .43f - 42 * scale, w, 84 * scale);
        }

        public static float MenuArrivalJumpProgressFor(float elapsed) =>
            Mathf.Clamp01(elapsed / MenuTakeoffSeconds);

        public static float MenuArrivalFadeProgressFor(float elapsed) =>
            Mathf.Clamp01((elapsed - MenuTakeoffSeconds) / Mathf.Max(.0001f, MenuArrivalSeconds - MenuTakeoffSeconds));

        public static float MenuJumpVerticalScale(float progress) =>
            PlayerLifeIcons.LossVerticalScale(Mathf.Lerp(0, PlayerLifeIcons.ApexProgress, Mathf.Clamp01(progress)));

        public static Rect MenuJumpRect(Rect resting, float progress)
        {
            float t = Mathf.Clamp01(progress);
            float scale = MenuJumpVerticalScale(t);
            float lift = resting.height * .62f * Mathf.SmoothStep(0, 1, t);
            var size = new Vector2(resting.width / Mathf.Sqrt(scale), resting.height * scale);
            float bottom = resting.yMax - lift;
            return new Rect(resting.center.x - size.x / 2, bottom - size.y, size.x, size.y);
        }

        private Rect PulsingTitleRect => MinimalTitleRect(GuiWidth, GuiHeight,
            musicPlayer != null ? GameplayMusicPlayer.BeatPulse(musicPlayer.BeatPosition) : 0);

        public static Rect MinimalSubtitleRect(float width, float height)
        {
            var title = MinimalTitleRect(width, height, 0);
            return new Rect(title.x, title.center.y + 26, title.width, 34);
        }

        public static Rect MainMenuLevelSelectRect(float width, float height, bool mobile = false)
        {
            var subtitle = MinimalSubtitleRect(width, height);
            float size = mobile ? Mathf.Clamp(width * .11f, 44, 72) : height < 520 ? 30 : height < 720 ? 34 : 40;
            float rowWidth = Mathf.Min(width - 72, Mathf.Max(size * 3.8f, size * 2 + 86));
            float y = Mathf.Min(height - size - 34, subtitle.yMax + (height < 520 ? 12 : 20));
            y = Mathf.Max(subtitle.yMax + 8, y);
            return new Rect((width - rowWidth) / 2, y, rowWidth, size);
        }

        public static Rect MainMenuLevelSelectLeftButtonRect(Rect row) =>
            new Rect(row.x, row.y, row.height, row.height);

        public static Rect MainMenuLevelSelectRightButtonRect(Rect row) =>
            new Rect(row.xMax - row.height, row.y, row.height, row.height);

        public static Rect MainMenuLevelSelectTouchRect(Rect row, bool mobile)
        {
            float padding = mobile ? 12 : 0;
            return new Rect(row.x - padding, row.y - padding, row.width + padding * 2, row.height + padding * 2);
        }

        public static Rect MainMenuLevelSelectValueRect(Rect row)
        {
            float gap = Mathf.Max(8, row.height * .35f);
            return new Rect(row.x + row.height + gap, row.y, row.width - row.height * 2 - gap * 2, row.height);
        }

        private GUIStyle MenuTitleStyle(Rect rect)
        {
            var style = new GUIStyle { font = ComboFont, fontSize = 56,
                alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset() };
            style.normal.textColor = Color.white;
            FitMainMenuTitle(style, MinimalTitleRect(GuiWidth, GuiHeight, 0));
            style.fontSize = Mathf.RoundToInt(style.fontSize * rect.height / 84);
            return style;
        }

        private Rect MenuPlayerRect()
        {
            var title = PulsingTitleRect;
            var style = MenuTitleStyle(title);
            // Bungee's capital ink sits above the line center; the feet follow that same pulsing edge.
            float top = title.center.y - style.fontSize * .36f;
            return PerchedPlayerRect(title.center.x, top, GameplayPlayerRect().size);
        }

        public static Rect PerchedPlayerRect(float centerX, float top, Vector2 gameplaySize) =>
            new Rect(centerX - gameplaySize.x / 2, top - gameplaySize.y, gameplaySize.x, gameplaySize.y);

        private Rect GameplayPlayerRect()
        {
            if (player == null || Camera.main == null) return default;
            var body = CharacterVisuals.Ensure(player.gameObject).Body;
            if (body == null) return default;
            var lo = Camera.main.WorldToViewportPoint(body.bounds.min);
            var hi = Camera.main.WorldToViewportPoint(body.bounds.max);
            return new Rect(lo.x * GuiWidth, (1 - hi.y) * GuiHeight,
                (hi.x - lo.x) * GuiWidth, (hi.y - lo.y) * GuiHeight);
        }

        private void DrawMinimalMainMenu()
        {
            var previous = GUI.color;
            float fade = MenuArriving ? MenuArrivalFadeProgress : 0;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, GuiWidth, GuiHeight), Texture2D.whiteTexture);
            var title = MenuIntroAnimation.TitleRect(PulsingTitleRect, GuiWidth, menuTitleElapsed);
            float opacity = 1 - Mathf.SmoothStep(0, 1, fade * 1.8f);
            var titleStyle = MenuTitleStyle(title);
            var subtitleRect = MinimalSubtitleRect(GuiWidth, GuiHeight);
            var subtitle = new GUIStyle(titleStyle) { fontSize = 24 };
            FitMainMenuSubtitle(subtitle, subtitleRect);
            float reveal = MenuIntroAnimation.SubtitleProgress(menuTitleElapsed);
            var subtitleTransform = MenuIntroAnimation.SubtitleTransform(subtitleRect, menuTitleElapsed);
            var art = player != null ? CharacterVisuals.Ensure(player.gameObject) : null;
            float beat = musicPlayer != null ? musicPlayer.BeatPosition : 0;
            var character = art != null ? MenuCharacterPose(art) : default;
            Color lightColor = MainMenuSubtitleBeatColor(beat);
            float lightOpacity = opacity * MainMenuWordRevealProgress(MainMenuTitleWords.Length, menuTitleElapsed);
            if (Event.current.type == EventType.Repaint && lightOpacity > 0)
            {
                MenuBacklight.Begin(new Vector2(GuiWidth, GuiHeight));
                int shadowWord = 0;
                DrawAnimatedMenuWords(MainMenuTitleWords, title, titleStyle, menuTitleElapsed, 1, Color.white,
                    ref shadowWord, true);
                if (art != null && character.Visible)
                {
                    foreach (var piece in art.Root.GetComponentsInChildren<SpriteRenderer>())
                        if (piece.enabled && piece.sprite != null)
                            MenuBacklight.Sprite(piece.sprite, MenuPieceRect(piece.bounds, art.Body.bounds, character.Body), character.Transform);
                }
                var sourceRect = MenuBacklight.TextInkBounds(MainMenuSubtitle, subtitleRect, subtitle);
                MenuBacklight.End(MenuIntroAnimation.TransformRect(sourceRect, subtitleTransform), lightColor, beat, lightOpacity, reveal);
            }
            int wordIndex = 0;
            GUI.color = Color.white;
            DrawAnimatedMenuWords(MainMenuTitleWords, title, titleStyle, menuTitleElapsed, opacity, Color.white,
                ref wordIndex);
            var previousMatrix = GUI.matrix;
            // A fully edge-on subtitle has a singular transform; it is invisible at that point.
            GUI.matrix = reveal > .001f ? previousMatrix * subtitleTransform : previousMatrix;
            // The subtitle stays saturated; its colored glow is the visible light source.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var glowRect = subtitleRect;
                glowRect.position += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3.5f;
                int glowWord = MainMenuTitleWords.Length;
                DrawAnimatedMenuWords(MainMenuSubtitleWords, glowRect, subtitle, menuTitleElapsed,
                    opacity * .08f * MenuBacklight.Intensity(beat), lightColor, ref glowWord);
            }
            // A fine white keyline separates the solid letter faces from their own light.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var outlineRect = subtitleRect;
                outlineRect.position += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                int outlineWord = MainMenuTitleWords.Length;
                DrawAnimatedMenuWords(MainMenuSubtitleWords, outlineRect, subtitle, menuTitleElapsed,
                    opacity * .8f, Color.white, ref outlineWord);
            }
            DrawAnimatedMenuWords(MainMenuSubtitleWords, subtitleRect, subtitle, menuTitleElapsed, opacity,
                lightColor, ref wordIndex);
            GUI.matrix = previousMatrix;
            DrawMainMenuLevelSelect(beat, opacity);
            if (art != null && character.Visible)
            {
                GUI.matrix = previousMatrix * character.Transform;
                DrawMenuCharacter(art, character.Body, menuGaze,
                    MenuEntranceForwardGazeFor(menuTitleElapsed, MenuArriving), MainMenuCharacterColor(beat));
                GUI.matrix = previousMatrix;
                if (!MenuArriving && MenuEntrance == MenuIntroAnimation.Entrance.TractorBeam)
                    MenuIntroAnimation.DrawBeam(MenuPlayerRect(), menuTitleElapsed);
            }
            GUI.color = previous;
        }

        private void DrawMainMenuLevelSelect(float beat, float opacity)
        {
            if (!MainMenuLevelSelectVisible) return;
            var row = MainMenuLevelSelectRect(GuiWidth, GuiHeight, MobileMenuActive);
            var color = MainMenuSubtitleBeatColor(beat, opacity);
            float thickness = Mathf.Max(1, Mathf.Round(row.height * .05f));
            DrawLevelSelectArrow(MainMenuLevelSelectLeftButtonRect(row), true, color, thickness);
            DrawLevelSelectArrow(MainMenuLevelSelectRightButtonRect(row), false, color, thickness);
            var value = MainMenuLevelSelectValueRect(row);
            var style = new GUIStyle
            {
                font = ComboFont,
                fontSize = Mathf.RoundToInt(row.height * .9f),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset()
            };
            style.normal.textColor = color;
            string text = StartLevel.ToString();
            var content = new GUIContent(text);
            while (style.fontSize > 12 && style.CalcSize(content).x > value.width) style.fontSize--;
            GUI.Label(value, text, style);
        }

        private static void DrawLevelSelectArrow(Rect rect, bool left, Color color, float thickness)
        {
            DrawThinRect(rect, thickness, color);
            float back = left ? rect.xMax - rect.width * .34f : rect.x + rect.width * .34f;
            float tip = left ? rect.x + rect.width * .36f : rect.xMax - rect.width * .36f;
            var upper = new Vector2(back, rect.y + rect.height * .28f);
            var middle = new Vector2(tip, rect.center.y);
            var lower = new Vector2(back, rect.yMax - rect.height * .28f);
            DrawThinLine(upper, middle, thickness, color);
            DrawThinLine(middle, lower, thickness, color);
        }

        private static void DrawThinRect(Rect rect, float thickness, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawThinLine(Vector2 start, Vector2 end, float thickness, Color color)
        {
            var delta = end - start;
            if (delta.sqrMagnitude <= .0001f) return;
            var previousColor = GUI.color;
            var previousMatrix = GUI.matrix;
            GUI.color = color;
            GUI.matrix = previousMatrix * MenuLineTransform(start, end);
            GUI.DrawTexture(new Rect(0, -thickness / 2, delta.magnitude, thickness), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        public static Matrix4x4 MenuLineTransform(Vector2 start, Vector2 end) =>
            Matrix4x4.TRS(start, Quaternion.Euler(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg),
                Vector3.one);

        private MenuIntroAnimation.Pose MenuCharacterPose(CharacterVisuals art)
        {
            var target = MenuPlayerRect();
            if (MenuArriving && menuDeparture.width <= 0) menuDeparture = target;
            if (!MenuArriving) return MenuIntroAnimation.CharacterPose(target, GuiWidth, menuTitleElapsed, MenuEntrance);
            if (Camera.main == null || art.Body == null) return new MenuIntroAnimation.Pose(target);
            float fade = MenuArrivalFadeProgress;
            var departure = MenuJumpRect(menuDeparture, 1);
            var destination = GameplayPlayerRect();
            float ease = Mathf.SmoothStep(0, 1, fade);
            return new MenuIntroAnimation.Pose(fade <= 0 ? MenuJumpRect(menuDeparture, MenuArrivalJumpProgress) :
                new Rect(Vector2.Lerp(departure.position, destination.position, ease),
                    Vector2.Lerp(departure.size, destination.size, ease)));
        }

        private Vector2? MenuEyeTarget(Rect characterBody) =>
            MenuEyeTargetFor(MobileMenuActive, Input.mousePresent, Input.mousePosition, GuiScale, GuiHeight,
                characterBody, MenuEyeTrackingReady);

        public static Vector2? MenuEyeTargetFor(bool mobilePlatform, bool mousePresent, Vector2 mousePosition,
            float guiScale, float guiHeight, Rect characterBody, bool trackingReady = true)
        {
            if (!trackingReady || mobilePlatform || !mousePresent) return null;
            var point = mousePosition / Mathf.Max(.0001f, guiScale);
            var target = new Vector2(point.x, guiHeight - point.y);
            return target.y < characterBody.center.y ? target : (Vector2?)null;
        }

        public static bool MenuEntranceForwardGazeFor(float elapsed, bool arriving) =>
            !arriving && elapsed >= MenuIntroAnimation.CharacterStart && elapsed < MenuWordIntroDuration;

        private void TickMenuEyeTrackingDelay(float seconds)
        {
            if (!menuEyeDelayStarted)
            {
                menuEyeDelayStarted = true;
                menuEyeDelaySeconds = musicPlayer != null ?
                    musicPlayer.SecondsForBeats(GameplayMusicPlayer.BeatsPerMeasure) :
                    GameplayMusicPlayer.BeatsPerMeasure * CurrentBeatDuration;
                menuEyeDelaySeconds = Mathf.Max(.0001f, menuEyeDelaySeconds);
            }
            menuEyeDelayElapsed = Mathf.Min(menuEyeDelaySeconds,
                menuEyeDelayElapsed + Mathf.Max(0, seconds));
        }

        private static void DrawAnimatedMenuWords(string[] words, Rect lineRect, GUIStyle style, float elapsed,
            float opacity, Color color, ref int wordIndex, bool shadow = false)
        {
            if (words == null || words.Length == 0 || opacity <= 0) return;
            float space = style.CalcSize(new GUIContent(" ")).x;
            float total = 0;
            var widths = new float[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                widths[i] = style.CalcSize(new GUIContent(words[i])).x;
                total += widths[i];
            }
            total += space * (words.Length - 1);
            float x = lineRect.center.x - total / 2f;
            var wordStyle = new GUIStyle(style);
            for (int i = 0; i < words.Length; i++, wordIndex++)
            {
                float progress = MainMenuWordRevealProgress(wordIndex, elapsed);
                if (progress > .001f)
                {
                    color.a = opacity * progress;
                    wordStyle.normal.textColor = color;
                    var wordRect = new Rect(x, lineRect.y, widths[i], lineRect.height);
                    if (shadow) MenuBacklight.Word(words[i], wordRect, wordStyle, progress);
                    else GUI.Label(wordRect, words[i], wordStyle);
                }
                x += widths[i] + space;
            }
        }

        public static bool IsMenuEyePiece(SpriteRenderer piece)
        {
            if (piece == null) return false;
            var name = piece.name;
            return name == "Pupil" || name.Contains("Pupil");
        }

        public static Rect MenuEyeFollowRect(Rect pupil, Rect eye, Vector2 mousePosition)
            => MenuEyeGazeRect(pupil, eye, Vector2.ClampMagnitude((mousePosition - eye.center) / 180f, 1));

        public static Rect MenuEyeGazeRect(Rect pupil, Rect eye, Vector2 direction)
        {
            var travel = new Vector2(Mathf.Max(0, eye.width - pupil.width),
                Mathf.Max(0, eye.height - pupil.height)) * .4f;
            var offset = Vector2.Scale(Vector2.ClampMagnitude(direction, 1), travel);
            // Center each pupil independently so a forward gaze looks directly at the player.
            return new Rect(eye.center + offset - pupil.size / 2, pupil.size);
        }

        private static Rect MenuPieceRect(Bounds part, Bounds body, Rect target) =>
            new Rect(target.x + (part.min.x - body.min.x) / body.size.x * target.width,
                target.y + (body.max.y - part.max.y) / body.size.y * target.height,
                part.size.x / body.size.x * target.width, part.size.y / body.size.y * target.height);

        private static void DrawMenuCharacter(CharacterVisuals art, Rect target, MenuGazeAnimation gaze,
            bool forwardGaze, Color bodyColor)
        {
            if (art.Body == null || art.Body.sprite == null) return;
            var bounds = art.Body.bounds;
            var pieces = art.Root.GetComponentsInChildren<SpriteRenderer>();
            System.Array.Sort(pieces, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            foreach (var piece in pieces)
            {
                if (piece.sprite == null || !piece.enabled) continue;
                var rect = MenuPieceRect(piece.bounds, bounds, target);
                if (IsMenuEyePiece(piece))
                {
                    SpriteRenderer eye = null;
                    float nearest = float.PositiveInfinity;
                    foreach (var candidate in pieces)
                    {
                        if (candidate.name != "Eye" || candidate.sprite == null || !candidate.enabled) continue;
                        float distance = (candidate.bounds.center - piece.bounds.center).sqrMagnitude;
                        if (distance >= nearest) continue;
                        eye = candidate;
                        nearest = distance;
                    }
                    if (eye != null)
                    {
                        var eyeRect = MenuPieceRect(eye.bounds, bounds, target);
                        rect = MenuEyeGazeRect(rect, eyeRect, forwardGaze ? Vector2.zero : gaze.DirectionFor(eyeRect));
                    }
                }
                var source = piece.sprite.rect;
                var texture = piece.sprite.texture;
                GUI.color = piece == art.Body ? bodyColor : piece.color;
                GUI.DrawTextureWithTexCoords(rect, texture, new Rect(source.x / texture.width,
                    source.y / texture.height, source.width / texture.width, source.height / texture.height));
            }
        }

        private void DrawEnemyPreview(Rect area)
        {
            if (spawner == null) return;
            float regularSize = Mathf.Min(34, (area.width - 5 * 10) / 6f);
            float specialSize = Mathf.Min(30, (area.width - 4 * 12) / 5f);
            DrawPreviewRow(PreviewRegularColors, area.x + (area.width - (regularSize * PreviewRegularColors.Length + 10 * (PreviewRegularColors.Length - 1))) / 2,
                area.y, regularSize, 10, false);
            DrawPreviewRow(PreviewSpecialColors, area.x + (area.width - (specialSize * PreviewSpecialColors.Length + 12 * (PreviewSpecialColors.Length - 1))) / 2,
                area.y + 44, specialSize, 12, true);
        }

        private void DrawPreviewRow(EnemyColor[] colors, float x, float y, float size, float gap, bool special)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                var color = colors[i];
                var sprite = spawner.AppearanceSprite(color, special);
                DrawPreviewSprite(new Rect(x + i * (size + gap), y, size, size), sprite, EnemyPalette.Get(color));
            }
        }

        private void ObserveComboStreak()
        {
            int streak = Progress.ComboStreak;
            if (streak == observedComboStreak) return;
            int previous = observedComboStreak;
            bool advanced = streak > observedComboStreak;
            observedComboStreak = streak;
            if (streak <= 0)
            {
                comboMilestoneMultiplier = 0;
                comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
                if (previous > 0) ComboBreak.Begin(previous + 1, ComboColor);
                return;
            }
            comboDeathFadeElapsed = ComboDeathFadeSeconds;
            if (advanced && IsComboMilestone(Progress.ComboMultiplier)) BeginComboMilestone();
        }

        private void FadeComboOnDeath()
        {
            // Cancelling an in-flight tongue can reset the combo before the hit event arrives.
            int multiplier = Progress.ComboMultiplier > 1 ? Progress.ComboMultiplier : ComboBreak.Active ? ComboBreak.Multiplier : 1;
            if (multiplier > 1)
            {
                ComboDeathMultiplier = multiplier;
                comboDeathFadeElapsed = 0;
            }
            Progress.ResetCombo();
            observedComboStreak = Progress.ComboStreak;
            comboMilestoneMultiplier = 0;
            comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
            ComboBreak.Reset();
        }

        private void BeginComboMilestone()
        {
            comboMilestoneMultiplier = Progress.ComboMultiplier;
            comboMilestoneElapsed = 0;
        }

        private void TickComboPresentation(float seconds)
        {
            ComboBreak.Tick(seconds);
            comboDeathFadeElapsed = Mathf.Min(ComboDeathFadeSeconds, comboDeathFadeElapsed + Mathf.Max(0, seconds));
            if (comboMilestoneMultiplier <= 1) return;
            comboMilestoneElapsed = Mathf.Min(ComboMilestoneAnimationSeconds,
                comboMilestoneElapsed + Mathf.Max(0, seconds));
        }

        private void ResetComboPresentation()
        {
            observedComboStreak = Progress.ComboStreak;
            comboMilestoneMultiplier = 0;
            comboMilestoneElapsed = ComboMilestoneAnimationSeconds;
            ComboBreak.Reset();
            ComboDeathMultiplier = 0;
            comboDeathFadeElapsed = ComboDeathFadeSeconds;
        }

        public static bool IsComboMilestone(int multiplier) =>
            multiplier == 5 || multiplier == 25 || multiplier >= 75 && (multiplier - 25) % 50 == 0;
        public static bool ShouldDrawComboMultiplier(int multiplier) => multiplier > 1;
        public static Rect LevelProgressFillRect(Rect bar, float fraction) =>
            new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fraction), bar.height);
        public static Rect LevelProgressBankedRect(Rect bar, float fraction) =>
            new Rect(bar.x, bar.y + bar.height * .55f, bar.width * Mathf.Clamp01(fraction), Mathf.Max(2, bar.height * .45f));
        public static Rect LevelProgressSolidRect(Rect bar, bool pending, float pulse)
        {
            float expansion = (pending ? 3 : LevelProgressPulseExpansion) * Mathf.Clamp01(pulse);
            return new Rect(bar.x, bar.y - expansion, bar.width, bar.height + expansion * 2);
        }
        public static Rect LevelProgressGlowRect(Rect bar, float pulse)
        {
            float expansion = Mathf.Lerp(3, 8, Mathf.Clamp01(pulse));
            return new Rect(bar.x, bar.y - expansion * .55f, bar.width, bar.height + expansion * 1.1f);
        }

        public const float LevelProgressBaseHeight = 9f * 1.5f;
        public const float LevelProgressPulseExpansion = 1.5f;

        private static void DrawPreviewSprite(Rect rect, Sprite sprite, Color tint)
        {
            if (sprite == null || sprite.texture == null) return;
            var previous = GUI.color;
            GUI.color = tint;
            var texture = sprite.texture;
            var spriteRect = sprite.rect;
            var texCoords = new Rect(spriteRect.x / texture.width, spriteRect.y / texture.height,
                spriteRect.width / texture.width, spriteRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, texCoords, true);
            GUI.color = previous;
        }

        private void DrawProgress(float opacity = 1)
        {
            var camera = Camera.main;
            if (camera == null || opacity <= 0) return;
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, opacity);
            var left = ToGuiPoint(camera.WorldToScreenPoint(new Vector3(-3, 5.3f, 0)));
            var right = ToGuiPoint(camera.WorldToScreenPoint(new Vector3(3, 5.3f, 0)));
            float width = right.x - left.x - 20;
            float x = left.x + 10, y = GuiHeight - left.y;
            int viewHeight = Mathf.RoundToInt(camera.pixelHeight / GuiScale);
            var lifeRow = PlayerLifeIcons.RowRect(x, y, viewHeight);
            float progressY = y + PlayerLifeIcons.ProgressOffset(viewHeight);
            float textWidth = width - (PauseButtonVisible ? 54 : 0);
            float beat = musicPlayer != null ? musicPlayer.BeatPosition : Time.time / FullSetCelebration.StepDuration;
            float pulse = GameplayMusicPlayer.BeatPulse(beat);
            var progressBar = new Rect(x, progressY, width, LevelProgressBaseHeight);
            var progressMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(1, MagicTextGrowthScale), progressBar.center);
            var solidBar = LevelProgressSolidRect(progressBar, Progress.LevelUpPending, pulse);
            GUI.color = new Color(.3f, .4f, .45f, .7f * opacity);
            GUI.DrawTexture(solidBar, Texture2D.whiteTexture);
            var progressColor = ProgressBarColor;
            if (Progress.LevelUpPending)
            {
                var glowColor = progressColor;
                glowColor.a = Mathf.Lerp(.16f, .46f, pulse) * opacity;
                GUI.color = glowColor;
                GUI.DrawTexture(LevelProgressGlowRect(progressBar, pulse), Texture2D.whiteTexture);
                progressColor = Color.Lerp(progressColor, Color.white, Mathf.Lerp(.2f, .48f, pulse));
            }
            progressColor.a *= opacity;
            GUI.color = progressColor;
            GUI.DrawTexture(LevelProgressFillRect(solidBar, Progress.ActiveLevelProgressFraction), Texture2D.whiteTexture);
            if (Progress.LevelUpPending && Progress.BankedLevelProgressFraction > 0)
            {
                var banked = Color.Lerp(progressColor, Color.white, .45f);
                banked.a = Mathf.Lerp(.65f, 1f, pulse) * opacity;
                GUI.color = banked;
                GUI.DrawTexture(LevelProgressBankedRect(solidBar, Progress.BankedLevelProgressFraction), Texture2D.whiteTexture);
            }
            GUI.matrix = progressMatrix;
            var colors = DisplayedColorClearBarColors;
            float bottom = GuiHeight - ToGuiPoint(camera.WorldToScreenPoint(new Vector3(0, -5.25f, 0))).y;
            for (int i = 0; i < colors.Count; i++)
            {
                var color = colors[i];
                var resting = ColorClearBarSlotRect(x, bottom, width, colors.Count, i);
                bool losingPower = grid != null && !grid.HasColorClearBar(color) &&
                    LostBarAnimation.Active && LostBarAnimation.Visible(color);
                bool powered = losingPower || grid != null && grid.HasColorClearBar(color) && ClearCelebration.Visible(color);
                if (!powered)
                {
                    DrawPoweredDownColorClearBar(color, resting, opacity);
                    continue;
                }
                bool transitioning = ClearCelebration.Active && !ClearCelebration.Waiting;
                var animated = DisplayedColorClearBarRect(color, resting, beat, i);
                // The enlarged bar stays inside the playfield, including the end slots.
                animated.width = Mathf.Min(animated.width, width);
                animated.x = Mathf.Clamp(animated.x, x, x + width - animated.width);
                float drain = losingPower ? LostBarAnimation.PowerDownProgressFor(color) : ClearCelebration.PowerDownProgressFor(color);
                if (losingPower || transitioning && (ClearCelebration.SimultaneousPowerDown || drain > 0))
                    DrawPoweringDownColorClearBar(color, animated, opacity, drain);
                else DrawPoweredColorClearBar(color, animated, opacity, beat, i);
            }
            var comboRect = ComboMultiplierRestRect(x, textWidth, y, viewHeight);
            int comboFontSize = GameplayComboFontSize(viewHeight);
            if (State == RunState.Playing || State == RunState.Refilling || State == RunState.Dying)
            {
                var comboStyle = GameplayTextStyle(comboFontSize, TextAnchor.MiddleRight);
                if (ComboDeathOpacity > 0 || ShouldDrawComboMultiplier(Progress.ComboMultiplier))
                    DrawComboMultiplier(comboRect, comboStyle, opacity, beat);
                if (ComboBreak.Active)
                {
                    var comboMatrix = GUI.matrix;
                    var breakRect = ComboBreak.DisplayRect(comboRect, GuiWidth, GuiHeight);
                    GUIUtility.ScaleAroundPivot(Vector2.one * GameplayTextPulseScale(beat), breakRect.center);
                    ComboBreak.Draw(comboRect, comboStyle, opacity, GuiWidth, GuiHeight, GameplayTextColor, MagicTextGrowthScale);
                    GUI.matrix = comboMatrix;
                }
            }
            GUI.color = new Color(1, 1, 1, opacity);
            if (player != null) player.LifeIcons.Draw(player, lifeRow, opacity, GuiWidth, GuiHeight, MagicTextGrowthScale);
            // Keep the heading above life artwork, including traveling reward/loss animations.
            GUI.color = new Color(1, 1, 1, opacity);
            int headingSize = GameplayHeadingFontSize(viewHeight);
            DrawGameplayText(GameplayScoreRect(x, y, textWidth, viewHeight), "SCORE " + Progress.Score.ToString("N0"),
                GameplayTextStyle(headingSize, TextAnchor.MiddleLeft), GameplayTextColor, opacity, beat);
            DrawGameplayText(GameplayLevelRect(x, y, textWidth), "LEVEL " + Progress.Level,
                GameplayTextStyle(headingSize, TextAnchor.MiddleRight), GameplayTextColor, opacity, beat);
            GUI.color = previous;
        }

        private static void DrawPoweredDownColorClearBar(EnemyColor color, Rect resting, float opacity)
        {
            var shadow = ColorClearBarTint(color, false, opacity);
            shadow.a *= .5f;
            GUI.color = shadow;
            GUI.DrawTexture(new Rect(resting.x, resting.y + 1, resting.width, resting.height), Texture2D.whiteTexture);
            GUI.color = ColorClearBarTint(color, false, opacity);
            GUI.DrawTexture(resting, Texture2D.whiteTexture);
        }

        private static void DrawPoweredColorClearBar(EnemyColor color, Rect level, float opacity, float beatPosition, int slot)
        {
            float pulse = ColorClearBarMusicLevel(beatPosition, slot);
            var tint = ColorClearBarTint(color, true, opacity);
            var glow = tint;
            glow.a = .14f * opacity * Mathf.Lerp(.55f, 1, pulse);
            GUI.color = glow;
            GUI.DrawTexture(Expanded(level, 8, 7), Texture2D.whiteTexture);
            glow.a *= .65f;
            GUI.color = glow;
            GUI.DrawTexture(Expanded(level, 4, 4), Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.DrawTexture(level, Texture2D.whiteTexture);
            var shine = Color.Lerp(tint, Color.white, .65f);
            shine.a = .35f * opacity;
            GUI.color = shine;
            GUI.DrawTexture(new Rect(level.x, level.y, level.width, Mathf.Min(3, level.height * .28f)), Texture2D.whiteTexture);
        }

        private static void DrawPoweringDownColorClearBar(EnemyColor color, Rect rect, float opacity, float progress)
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress));
            float fade = 1 - t;
            var tint = ColorClearBarPowerDownTint(color, progress, opacity);
            var shadow = ColorClearBarTint(color, false, opacity);
            shadow.a *= .5f * t;
            GUI.color = shadow;
            GUI.DrawTexture(new Rect(rect.x, rect.y + 1, rect.width, rect.height), Texture2D.whiteTexture);
            var glow = tint;
            glow.a = .16f * opacity * fade;
            GUI.color = glow;
            GUI.DrawTexture(Expanded(rect, 6 * fade, 6 * fade), Texture2D.whiteTexture);
            glow.a *= .45f;
            GUI.color = glow;
            GUI.DrawTexture(Expanded(rect, 3 * fade, 3 * fade), Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            var shine = Color.Lerp(tint, Color.white, .45f * fade);
            shine.a = .22f * opacity * fade;
            GUI.color = shine;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, Mathf.Min(3, rect.height * .22f)), Texture2D.whiteTexture);
        }

        private static Rect Expanded(Rect rect, float x, float y) =>
            new Rect(rect.x - x, rect.y - y, rect.width + x * 2, rect.height + y * 2);

        private void DrawComboMultiplier(Rect resting, GUIStyle baseStyle, float opacity, float beat)
        {
            bool fading = ComboDeathOpacity > 0;
            bool animating = !fading && comboMilestoneMultiplier > 1 && comboMilestoneElapsed < ComboMilestoneAnimationSeconds;
            int multiplier = fading ? ComboDeathMultiplier : Progress.ComboMultiplier;
            var color = GameplayTextColor;
            if (fading) opacity *= ComboDeathOpacity;
            var rect = animating ? ComboMultiplierDisplayRect(resting, GuiWidth, GuiHeight, comboMilestoneElapsed) : resting;
            float scale = Mathf.Max(1, rect.height / resting.height);
            var style = new GUIStyle(baseStyle)
            {
                alignment = animating ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight,
                fontSize = Mathf.RoundToInt(baseStyle.fontSize * Mathf.Min(3, scale))
            };
            string text = "x" + multiplier;
            DrawGameplayText(rect, text, style, color, opacity, beat);
        }

        private void DrawLeaderboard(Rect area, bool visible)
        {
            if (!visible || area.height < 44) return;
            var entries = LocalLeaderboard.Entries();
            var label = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter };
            label.normal.textColor = Color.white;
            GUI.Label(new Rect(area.x, area.y, area.width, 22), "Top Scores", label);
            label.fontSize = 13;
            label.alignment = TextAnchor.UpperLeft;
            if (entries.Count == 0)
            {
                label.alignment = TextAnchor.UpperCenter;
                GUI.Label(new Rect(area.x, area.y + 24, area.width, 22), "No scores yet", label);
                return;
            }
            for (int i = 0; i < entries.Count && 24 + (i + 1) * 20 <= area.height; i++)
            {
                string row = (i + 1) + ". " + entries[i].Score.ToString("N0") + "  Level " + entries[i].Level;
                label.fontSize = 13;
                while (label.fontSize > 8 && label.CalcSize(new GUIContent(row)).x > area.width - 48) label.fontSize--;
                GUI.Label(new Rect(area.x + 24, area.y + 24 + i * 20, area.width - 48, 20), row, label);
            }
        }
    }
}
