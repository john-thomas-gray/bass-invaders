using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        public const int MaxExtraLives = 2;
        public const int MaxLives = MaxExtraLives + 1;
        public const float HalfWidth = 3f;
        public const float TouchFireScreenWidthFraction = .08f;
        [SerializeField, Min(0.1f)] private float speed = 5f;
        [SerializeField, Min(0.1f)] private float touchSpeed = 12f;
        [SerializeField, Range(.01f, .25f), Tooltip("Horizontal shooting range as a fraction of the current screen width.")]
        private float touchFireScreenWidthFraction = TouchFireScreenWidthFraction;
        [SerializeField] private EnemyGrid grid;
        [SerializeField] private TongueShot tongue;
        [SerializeField] private SpriteRenderer body;
        private const float HitboxScale = 0.72f;
        private float horizontalDirection;
        private int horizontalMovementFrame = -10;
        public float HorizontalDirection => Alive && !Stunned && !ControlsLocked &&
            Time.frameCount - horizontalMovementFrame <= 1 ? horizontalDirection : 0;
        private Camera view;
        private int finger = -1;
        private Vector2 pointerStart;
        private float pointerTime;
        private float pointerTravel;
        private float targetX;
        private bool movingToTarget;
        private bool pointerActive, touchPointer, nearTouchStart, pointerDragging;
        public int Lives { get; private set; } = MaxLives;
        public int ExtraLives => Mathf.Max(0, Lives - 1);
        public PlayerLifeIcons LifeIcons { get; } = new PlayerLifeIcons();
        public EnemyColor? ReadyColor { get; private set; }
        public int MagicCharges { get; private set; }
        public bool MagicPowerActive => MagicCharges > 0 && pendingMagicAbsorptions == 0 ||
            tongue != null && tongue.Active && tongue.IsMagic;
        public int ActiveMagicBars => MagicPowerActive && Fleet != null ? Fleet.ClearedColorCount : 0;
        public FleetState Fleet => grid != null ? grid.Fleet : null;
        public bool HasColorClearBar(EnemyColor color) => grid != null && grid.HasColorClearBar(color);
        public Color? CelebrationColor { get; private set; }
        public GameplayMusicPlayer Music => grid != null ? grid.GetComponent<GameplayMusicPlayer>() : null;
        public TongueShot Tongue => tongue;
        public Transform MagicAbsorptionTarget => body != null ? body.transform : transform;
        public void SetCelebrationColor(Color? color)
        { CelebrationColor = color; RefreshPresentation(Time.time); }
        private EnemyColor? nextWaveColor;
        private EnemyColor? returnColor;
        private bool returnColorPrepared;
        private bool reservedCelebrationColor;
        public bool ShotActive => tongue != null && tongue.Active;
        public bool HasFleet => grid != null && grid.Fleet.Count > 0;
        public void FinishWaveReturn() { if (tongue != null && tongue.Active) tongue.Cancel(true); }
        public bool Stunned { get; private set; }
        public void PrepareNextWave(EnemyColor[] plan, EnemyColor? reservedColor = null)
        {
            if (plan == null || plan.Length == 0) return;
            int rowWidth = grid != null ? grid.GetComponent<EnemyRowSpawner>()?.RowWidthForPlan(plan) ?? plan.Length : plan.Length;
            if (rowWidth == 0) rowWidth = plan.Length;
            PrepareNextWave(new FleetPlan(plan, rowWidth), reservedColor);
        }

        public void PrepareNextWave(FleetPlan plan, EnemyColor? reservedColor = null)
        {
            if (plan == null) return;
            var weights = plan.PlayerColorWeights();
            if (weights.Count > 1 && ReadyColor.HasValue) weights.Remove(ReadyColor.Value);
            if (reservedColor.HasValue && !plan.Contains(reservedColor.Value)) reservedColor = null;
            nextWaveColor = reservedColor ?? ChooseColor(weights);
            reservedCelebrationColor = reservedColor.HasValue;
            RefreshColor();
        }

        private static EnemyColor? ChooseColor(Dictionary<EnemyColor, int> weights)
        {
            int total = 0;
            foreach (int weight in weights.Values) total += weight;
            if (total == 0) return null;
            int ticket = Random.Range(0, total);
            foreach (var choice in weights)
            {
                if (ticket < choice.Value) return choice.Key;
                ticket -= choice.Value;
            }
            return null;
        }
        public Color DisplayColor => body != null ? body.color : Color.gray;
        public EnemyColor? ReturnColorPreview => returnColorPrepared ?
            returnColor ?? ReadyColor ?? (tongue != null && tongue.Active ? tongue.ShotColor : (EnemyColor?)null) : null;
        public Color AccentColor => LifeIcons.RewardColor.HasValue ? EnemyPalette.Get(LifeIcons.RewardColor.Value) :
            ReturnColorPreview.HasValue ? EnemyPalette.Get(ReturnColorPreview.Value) : DisplayColor;
        public void RefreshPresentation(float time)
        {
            if (body == null) return;
            var shock = GetComponent<PlayerShockVisual>();
            if (shock != null && shock.BodyTint.HasValue) { body.color = shock.BodyTint.Value; return; }
            if (CelebrationColor.HasValue) { body.color = CelebrationColor.Value; return; }
            if (Stunned) { if (tongue == null || !tongue.ShockActive) body.color = Color.gray; return; }
            bool magic = MagicPowerActive;
            var music = Music;
            float flashPhase = music != null && music.InGameplayRun ? Mathf.Max(0, music.BeatPosition) * 4 : time * 10;
            body.color = magic ? EnemyPalette.Get(EnemyPalette.CycleColor(Mathf.FloorToInt(flashPhase))) :
                tongue != null && tongue.Active ? EnemyPalette.Get(tongue.ShotColor) :
                ReadyColor.HasValue ? EnemyPalette.Get(ReadyColor.Value) : Color.gray;
        }
        private EnemyGrid subscribedGrid;
        private TongueShot subscribedTongue;
        private bool shotClearedColor;
        private bool shotHitEnemy;
        private int pendingMagicAbsorptions;
        public event System.Action ShotAccepted;
        public event System.Action<bool> ShotCompleted;
        public event System.Action<bool> ShotRetractionStarted;
        public event System.Action PlayerHit;
        public event System.Action FatalHit;
        public event System.Action<bool> MagicChargeAwarded;
        public event System.Action<float> HorizontalAction;
        public event System.Action<float> HorizontalTravelled;
        public bool AutonomousControl { get; internal set; }
        private void OnEnable() => SubscribeToGrid();
        private void OnDisable() { UnsubscribeFromGrid(); ClearRecoveryCue(); }
        private void OnDestroy() { UnsubscribeFromGrid(); ClearRecoveryCue(); LifeIcons.Dispose(); }
        private void SubscribeToGrid()
        {
            if (grid == subscribedGrid && tongue == subscribedTongue) return;
            UnsubscribeFromGrid();
            subscribedTongue = tongue;
            if (subscribedTongue != null) subscribedTongue.Finished += OnShotFinished;
            if (subscribedTongue != null) subscribedTongue.RetractionStarted += OnRetractionStarted;
            if (subscribedTongue != null) subscribedTongue.Deflected += OnDeflected;
            if (subscribedTongue != null) subscribedTongue.ShieldShockArrived += OnShieldShockArrived;
            subscribedGrid = grid;
            if (subscribedGrid == null) return;
            subscribedGrid.ColorCleared += AwardMagic;
            subscribedGrid.MatchCleared += OnMatchCleared;
            subscribedGrid.LastYellowTransformed += OnLastYellowTransformed;
            subscribedGrid.FleetCleared += ResetMagic;
        }
        private void UnsubscribeFromGrid()
        {
            if (subscribedTongue != null) subscribedTongue.Finished -= OnShotFinished;
            if (subscribedTongue != null) subscribedTongue.RetractionStarted -= OnRetractionStarted;
            if (subscribedTongue != null) subscribedTongue.Deflected -= OnDeflected;
            if (subscribedTongue != null) subscribedTongue.ShieldShockArrived -= OnShieldShockArrived;
            subscribedTongue = null;
            if (subscribedGrid == null) return;
            subscribedGrid.ColorCleared -= AwardMagic;
            subscribedGrid.MatchCleared -= OnMatchCleared;
            subscribedGrid.LastYellowTransformed -= OnLastYellowTransformed;
            subscribedGrid.FleetCleared -= ResetMagic;
            subscribedGrid = null;
        }
        private void AwardMagic(EnemyColor color)
        {
            int previous = MagicCharges;
            MagicCharges = Mathf.Min(2, MagicCharges + 1);
            MagicChargeAwarded?.Invoke(MagicCharges > previous);
            if (!ShotActive) return;
            shotClearedColor = true;
            if (Application.isPlaying && MagicCharges > previous) pendingMagicAbsorptions++;
        }
        public void CompleteMagicAbsorption()
        {
            if (pendingMagicAbsorptions > 0) pendingMagicAbsorptions--;
            RefreshPresentation(Time.time);
        }
        private void OnMatchCleared(int count, bool fleetCleared, int scoreWeight)
        {
            if (ShotActive) shotHitEnemy = true;
        }
        private void OnShotFinished()
        {
            GetComponent<PlayerShockVisual>()?.BeginRecovery();
            Stunned = false;
            pendingMagicAbsorptions = 0;
            if (returnColorPrepared) ReadyColor = returnColor;
            RefreshColor(!returnColorPrepared);
            returnColorPrepared = false;
            if (!shotClearedColor && grid != null) grid.ResetColorClearStreak();
            ShotCompleted?.Invoke(shotHitEnemy);
            shotClearedColor = false;
            shotHitEnemy = false;
        }
        private void OnRetractionStarted()
        {
            PrepareReturnColor(true);
            ShotRetractionStarted?.Invoke(shotHitEnemy);
        }

        private void PrepareReturnColor(bool reroll)
        {
            returnColor = SelectColor(returnColorPrepared ? returnColor : ReadyColor, reroll);
            returnColorPrepared = true;
            tongue.SetReturnColor(returnColor ?? ReadyColor ?? tongue.ShotColor, MagicCharges > 0);
        }
        private void OnDeflected()
        {
            Stunned = true;
            movingToTarget = false;
            finger = -1;
            RefreshPresentation(Time.time);
        }
        private void OnShieldShockArrived()
        {
            var shock = GetComponent<PlayerShockVisual>();
            if (shock == null) shock = gameObject.AddComponent<PlayerShockVisual>();
            shock.Begin();
            RefreshPresentation(Time.time);
        }
        private void ResetMagic() { MagicCharges = 0; pendingMagicAbsorptions = 0; }
        private void OnLastYellowTransformed()
        {
            if (ReadyColor == EnemyColor.Yellow) Hit();
            RefreshColor();
        }
        private float recoveryRemaining;
        private float recoveryElapsed;
        private bool awaitingRespawn;
        private bool replacementJump;
        private Vector3 replacementApex;
        private float replacementLandingY, replacementLandingX, replacementLastX;
        public bool ReplacementFalling => replacementJump && !awaitingRespawn;
        public Bounds LandingHitBounds
        {
            get
            {
                var bounds = HitBounds;
                if (replacementJump) bounds.center += Vector3.up * (replacementLandingY - transform.position.y);
                return bounds;
            }
        }
        private PresentationCue recoveryCue;
        private void ClearRecoveryCue()
        {
            if (recoveryCue == null) return;
            recoveryCue.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(recoveryCue.gameObject);
            else DestroyImmediate(recoveryCue.gameObject);
            recoveryCue = null;
        }
        public bool ControlsLocked { get; set; }
        public void CancelPointer() { finger = -1; movingToTarget = false; pointerActive = false; }
        public void CancelShot() => tongue.Cancel();
        public void StopActions()
        {
            tongue.Cancel();
            movingToTarget = false;
            finger = -1;
        }
        public bool FatallyDefeated { get; private set; }
        private bool fatalAnimationStarted;
        public bool Alive => !FatallyDefeated && !awaitingRespawn;
        public bool RespawnProtectionActive => !replacementJump && recoveryRemaining > 0;
        public bool Invulnerable => ReplacementFalling || RespawnProtectionActive;
        public Bounds HitBounds
        {
            get
            {
                var bounds = CharacterVisuals.Ensure(gameObject).HitBounds;
                bounds.size = new Vector3(bounds.size.x * HitboxScale, bounds.size.y * HitboxScale, bounds.size.z);
                return bounds;
            }
        }

        public void PrepareFatalDefeat()
        {
            GetComponent<PlayerShockVisual>()?.ResetEffect();
            CharacterVisuals.Ensure(gameObject).ResetJumpPose();
            if (FatallyDefeated) return;
            grid?.ResetColorClearStreak();
            Lives = 0;
            LifeIcons.Reset();
            FatallyDefeated = true;
            ControlsLocked = true;
            ClearRecoveryCue();
            StopActions();
            ShowPlayer(true);
        }

        public PresentationCue BeginFatalDefeat(int level = RunProgress.MinLevel)
        {
            if (fatalAnimationStarted) return null;
            PrepareFatalDefeat();
            fatalAnimationStarted = true;
            var cue = CharacterVisuals.Ensure(gameObject).PlayerFatalDefeat(ReadyColor ?? EnemyColor.Blue, level);
            ShowPlayer(false);
            return cue;
        }

        public void ResetForRun()
        {
            GetComponent<PlayerShockVisual>()?.ResetEffect();
            CharacterVisuals.Ensure(gameObject).ResetJumpPose();
            horizontalDirection = 0;
            horizontalMovementFrame = -10;
            if (replacementJump) transform.position = new Vector3(transform.position.x, replacementLandingY, transform.position.z);
            replacementJump = false;
            CelebrationColor = null;
            Lives = MaxLives;
            LifeIcons.Reset();
            FatallyDefeated = false;
            fatalAnimationStarted = false;
            recoveryRemaining = 0;
            recoveryElapsed = 0;
            awaitingRespawn = false;
            Stunned = false;
            movingToTarget = false;
            finger = -1;
            ReadyColor = null;
            nextWaveColor = null;
            reservedCelebrationColor = false;
            ClearRecoveryCue();
            ResetMagic();
            if (tongue != null) tongue.Cancel();
            ShowPlayer(true);
            RefreshColor(true);
        }

        public bool GrantLife(bool animate = true)
        {
            if (Lives >= MaxLives || FatallyDefeated) return false;
            Lives++;
            if (animate) LifeIcons.BeginGain(ExtraLives - 1, Music);
            grid?.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.OneUp);
            return true;
        }

        public bool Hit(bool ignoreInvulnerability = false)
        {
            if (DeveloperOptions.PlayerInvincible) return false;
            if (ControlsLocked || FatallyDefeated || !Alive || (!ignoreInvulnerability && Invulnerable)) return false;
            GetComponent<PlayerShockVisual>()?.ResetEffect();
            CharacterVisuals.Ensure(gameObject).ResetJumpPose();
            grid?.ResetColorClearStreak();
            ClearRecoveryCue();
            Lives = Mathf.Max(0, Lives - 1);
            ResetMagic();
            tongue.Cancel();
            PlayerHit?.Invoke();
            movingToTarget = false;
            finger = -1;
            if (Lives == 0)
            {
                FatalHit?.Invoke();
                if (!FatallyDefeated) BeginFatalDefeat();
                return true;
            }
            if (!replacementJump) replacementLandingY = transform.position.y;
            replacementLandingX = transform.position.x;
            replacementJump = true;
            recoveryRemaining = 0;
            recoveryElapsed = 0;
            awaitingRespawn = true;
            LifeIcons.BeginLoss(ExtraLives);
            replacementApex = LifeIcons.ApexWorldPosition(this);
            replacementLastX = replacementApex.x;
            recoveryCue = CharacterVisuals.Ensure(gameObject).PlayerDefeat(ReadyColor ?? EnemyColor.Blue);
            if (recoveryCue != null) recoveryCue.enabled = false;
            ShowPlayer(false);
            return true;
        }

        public void TickSurvival(float seconds)
        {
            if (FatallyDefeated || grid != null && grid.GetComponent<GameSession>()?.IsPaused == true) return;
            seconds = Mathf.Max(0, seconds);
            LifeIcons.Tick(seconds, Music);
            if (recoveryRemaining <= 0 && !replacementJump && !LifeIcons.Animating && (recoveryCue == null || recoveryCue.Finished)) return;
            bool wasAlive = Alive;
            if (recoveryCue != null) recoveryCue.Tick(seconds);
            if (replacementJump)
            {
                bool waitingForLanding = recoveryElapsed >= PlayerLifeIcons.Duration;
                recoveryElapsed += seconds;
                if (awaitingRespawn && recoveryElapsed >= PlayerLifeIcons.ApexSeconds)
                {
                    awaitingRespawn = false;
                    LifeIcons.HandOffLoss();
                    transform.position = replacementApex;
                }
                if (!awaitingRespawn)
                {
                    bool landed = recoveryElapsed >= PlayerLifeIcons.Duration;
                    float fall = landed ? 1 : Mathf.Clamp01((recoveryElapsed - PlayerLifeIcons.ApexSeconds) /
                        (PlayerLifeIcons.Duration - PlayerLifeIcons.ApexSeconds));
                    float travel = fall * fall;
                    float plannedX = Mathf.Lerp(replacementApex.x, replacementLandingX, fall);
                    float x = Wrap(transform.position.x + plannedX - replacementLastX);
                    replacementLastX = plannedX;
                    transform.position = new Vector3(x, Mathf.Lerp(replacementApex.y, replacementLandingY, travel), transform.position.z);
                    if (landed)
                    {
                        if (TryPlaceRespawn())
                        {
                            replacementJump = false;
                            CharacterVisuals.Ensure(gameObject).BeginLanding(waitingForLanding ? 0 :
                                Mathf.Max(0, recoveryElapsed - PlayerLifeIcons.Duration));
                            recoveryRemaining = waitingForLanding ? 1.5f :
                                Mathf.Max(0, 1.5f - Mathf.Max(0, recoveryElapsed - PlayerLifeIcons.Duration));
                        }
                        else
                        {
                            // Airborne protection is untimed; landing starts the flashing recovery interval.
                            transform.position += Vector3.up * .8f;
                            recoveryElapsed = PlayerLifeIcons.Duration;
                        }
                    }
                }
            }
            else recoveryRemaining = Mathf.Max(0, recoveryRemaining - seconds);
            CharacterVisuals.Ensure(gameObject).SetJumpStretch(ReplacementFalling
                ? PlayerLifeIcons.LossVerticalScale(recoveryElapsed / PlayerLifeIcons.Duration) : 1);
            if (!wasAlive && Alive) RefreshColor(true);
            ShowPlayer(Alive && (!RespawnProtectionActive || Mathf.FloorToInt(recoveryRemaining * 8) % 2 == 0));
        }

        private bool TryPlaceRespawn()
        {
            if (grid == null) return true;
            var playerBounds = HitBounds;
            float leftOffset = playerBounds.min.x - transform.position.x;
            float rightOffset = playerBounds.max.x - transform.position.x;
            var spaces = new List<Vector2> { new Vector2(-HalfWidth - leftOffset, HalfWidth - rightOffset) };
            bool occupiedRow = false;
            for (int row = 0; row < GridModel.Rows; row++)
            for (int column = 0; column < GridModel.Columns; column++)
            {
                var cell = grid.Model.At(column, row);
                var enemy = cell != null ? grid.View(cell.Id) : null;
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var bounds = enemy.HitBounds;
                if (bounds.max.y < playerBounds.min.y || bounds.min.y > playerBounds.max.y) continue;
                occupiedRow = true;
                // Exclude player-origin positions that leave less than one enemy width of clear space.
                float left = bounds.min.x - bounds.size.x - rightOffset - .001f;
                float right = bounds.max.x + bounds.size.x - leftOffset + .001f;
                for (int i = spaces.Count - 1; i >= 0; i--)
                {
                    var space = spaces[i];
                    if (right <= space.x || left >= space.y) continue;
                    spaces.RemoveAt(i);
                    if (left > space.x) spaces.Add(new Vector2(space.x, left));
                    if (right < space.y) spaces.Add(new Vector2(right, space.y));
                }
            }
            if (!occupiedRow) return true;
            if (spaces.Count == 0) return false;
            float best = transform.position.x, distance = float.PositiveInfinity;
            foreach (var space in spaces)
            {
                float candidate = Mathf.Clamp(transform.position.x, space.x, space.y);
                float travel = Mathf.Abs(candidate - transform.position.x);
                if (travel < distance) { best = candidate; distance = travel; }
            }
            transform.position = new Vector3(best, transform.position.y, transform.position.z);
            CancelPointer();
            return true;
        }

        private void ShowPlayer(bool visible)
        {
            // Projectile visibility belongs to TongueShot, not respawn flashing.
            foreach (var visual in GetComponentsInChildren<SpriteRenderer>())
                if (visual.GetComponentInParent<TongueShot>() == null) visual.enabled = visible;
        }

        public void Configure(EnemyGrid enemyGrid, TongueShot shot, SpriteRenderer bodyRenderer)
        { grid = enemyGrid; tongue = shot; body = bodyRenderer;
            CharacterVisuals.Ensure(gameObject).Configure(bodyRenderer); SubscribeToGrid(); }

        private void Start()
        {
            Input.simulateMouseWithTouches = false;
            view = Camera.main;
            SubscribeToGrid();
            RefreshColor();
        }
        public void RefreshColor(bool reroll = false)
        {
            if (tongue.Active)
            {
                if (tongue.Retracting) PrepareReturnColor(false);
                return;
            }
            ReadyColor = SelectColor(ReadyColor, reroll);
            RefreshPresentation(Time.time);
        }

        private EnemyColor? SelectColor(EnemyColor? current, bool reroll)
        {
            if (grid == null) return current;
            var weights = grid.PlayerColorWeights(ShootableColumns());
            if (nextWaveColor.HasValue)
            {
                var chosen = nextWaveColor;
                if (grid.Fleet.Count > 0)
                {
                    if (!reservedCelebrationColor && !weights.ContainsKey(chosen.Value))
                        chosen = ChooseColor(weights);
                    nextWaveColor = null;
                    reservedCelebrationColor = false;
                }
                return chosen;
            }
            if (grid.Fleet.Count == 0) return current;
            if (weights.Count == 0) return null;
            // Reserve the preview using the same reachable colors as the next shot.
            return reroll || !current.HasValue || !weights.ContainsKey(current.Value) ? ChooseColor(weights) : current;
        }

        private bool[] ShootableColumns()
        {
            var allowed = new bool[GridModel.Columns];
            if (grid == null) return allowed;
            var reachableOrigins = ReachableShotOrigins();
            if (reachableOrigins.Count == 0) return allowed;
            for (int column = 0; column < GridModel.Columns; column++)
            for (int row = 0; row < GridModel.Rows; row++)
            {
                var cell = grid.Model.At(column, row);
                if (cell == null) continue;
                var enemy = grid.View(cell.Id);
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var bounds = enemy.HitBounds;
                if (!IntersectsAny(reachableOrigins, bounds.min.x - TongueShot.NormalHitRadius,
                    bounds.max.x + TongueShot.NormalHitRadius)) continue;
                allowed[column] = true;
                break;
            }
            return allowed;
        }

        public List<Vector2> ReachableShotOrigins()
        {
            var intervals = new List<Vector2> { new Vector2(-HalfWidth, HalfWidth) };
            if (grid == null) return intervals;
            var playerBounds = HitBounds;
            float leftOffset = playerBounds.min.x - transform.position.x;
            float rightOffset = playerBounds.max.x - transform.position.x;
            for (int column = 0; column < GridModel.Columns; column++)
            for (int row = 0; row < GridModel.Rows; row++)
            {
                var cell = grid.Model.At(column, row);
                if (cell == null) continue;
                var enemy = grid.View(cell.Id);
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var bounds = enemy.HitBounds;
                if (bounds.max.y < playerBounds.min.y || bounds.min.y > playerBounds.max.y) continue;
                SubtractInterval(intervals, bounds.min.x - rightOffset - .001f, bounds.max.x - leftOffset + .001f);
            }
            return ReachableComponent(intervals, Wrap(transform.position.x));
        }

        private static void SubtractInterval(List<Vector2> intervals, float left, float right)
        {
            left = Mathf.Max(-HalfWidth, left);
            right = Mathf.Min(HalfWidth, right);
            if (right <= left) return;
            for (int i = intervals.Count - 1; i >= 0; i--)
            {
                var interval = intervals[i];
                if (right <= interval.x || left >= interval.y) continue;
                intervals.RemoveAt(i);
                if (left > interval.x) intervals.Add(new Vector2(interval.x, left));
                if (right < interval.y) intervals.Add(new Vector2(right, interval.y));
            }
            intervals.Sort((a, b) => a.x.CompareTo(b.x));
        }

        private static List<Vector2> ReachableComponent(List<Vector2> intervals, float origin)
        {
            var result = new List<Vector2>();
            if (intervals.Count == 0) return result;
            intervals.Sort((a, b) => a.x.CompareTo(b.x));
            int current = -1;
            for (int i = 0; i < intervals.Count; i++)
                if (origin >= intervals[i].x && origin <= intervals[i].y)
                { current = i; break; }
            if (current < 0) return result;
            var first = intervals[0];
            var last = intervals[intervals.Count - 1];
            bool wraps = first.x <= -HalfWidth + .0001f && last.y >= HalfWidth - .0001f;
            if (wraps && (current == 0 || current == intervals.Count - 1))
            {
                result.Add(first);
                if (intervals.Count > 1) result.Add(last);
            }
            else result.Add(intervals[current]);
            return result;
        }

        private static bool IntersectsAny(List<Vector2> intervals, float left, float right)
        {
            foreach (var interval in intervals)
                if (right >= interval.x && left <= interval.y) return true;
            return false;
        }

        public bool Fire() => Fire(false);

        private bool Fire(bool preserveReadyColor)
        {
            if (ControlsLocked || Stunned || !Alive || grid.Fleet.Count == 0) return false;
            if (preserveReadyColor)
            {
                if (!ReadyColor.HasValue) RefreshColor();
            }
            else RefreshColor();
            bool magic = MagicCharges > 0;
            if (!ReadyColor.HasValue || !tongue.TryFire(ReadyColor.Value,
                Mathf.Max(0.1f, 5.3f - tongue.transform.position.y), magic)) return false;
            shotClearedColor = false;
            shotHitEnemy = false;
            ShotAccepted?.Invoke();
            // Only a new color clear during this shot can renew magic.
            if (magic) ResetMagic();
            CharacterVisuals.Ensure(gameObject).Fire(ReadyColor.Value);
            return true;
        }

        public static float Wrap(float x) => Mathf.Repeat(x + HalfWidth, 2 * HalfWidth) - HalfWidth;
        public static float MoveTowardWrapped(float x, float target, float distance)
        {
            float delta = Wrap(target - x);
            return Wrap(x + Mathf.Clamp(delta, -distance, distance));
        }

        public void Move(float axis, float seconds)
        {
            if (ControlsLocked || Stunned || !Alive) return;
            MoveHorizontal(Mathf.Clamp(axis, -1, 1) * speed * seconds);
        }

        private void MoveHorizontal(float distance)
        {
            HorizontalAction?.Invoke(distance);
            horizontalDirection = 0;
            horizontalMovementFrame = Time.frameCount;
            var session = grid != null ? grid.GetComponent<GameSession>() : null;
            // Wrap is a teleport, not a sweep through the middle of the field.
            while (Mathf.Abs(distance) > .000001f && !ControlsLocked)
            {
                float direction = Mathf.Sign(distance);
                float edge = direction * HalfWidth;
                float segment = direction * Mathf.Min(Mathf.Abs(distance), Mathf.Abs(edge - transform.position.x));
                float fraction = session != null ? session.ContactFraction(-segment) : 1;
                transform.position += Vector3.right * (segment * fraction);
                HorizontalTravelled?.Invoke(Mathf.Abs(segment * fraction));
                if (Mathf.Abs(segment * fraction) > .000001f) horizontalDirection = direction;
                if (session != null) session.FinishContactMove(fraction);
                if (ControlsLocked) return;
                distance -= segment;
                if (Mathf.Abs(transform.position.x - edge) < .000001f && (direction > 0 || Mathf.Abs(distance) > .000001f))
                {
                    var position = transform.position;
                    position.x = -edge;
                    transform.position = position;
                    if (session != null && session.CheckPlayerContact()) return;
                }
            }
        }

        public void BeginPointer(Vector2 position, bool isTouch = false)
        {
            var session = grid != null ? grid.GetComponent<GameSession>() : null;
            if (session != null && session.PointerOverMenu(position)) { session.Pause(); CancelPointer(); return; }
            if (Stunned || ControlsLocked || !Alive) { CancelPointer(); return; }
            if (view == null) view = Camera.main;
            if (view == null) { CancelPointer(); return; }
            pointerActive = true;
            touchPointer = isTouch;
            pointerDragging = false;
            nearTouchStart = IsTouchShootPosition(position, view.WorldToScreenPoint(transform.position),
                CurrentTouchFireRangePixels(), TouchShootUpperScreenY());
            movingToTarget = false;
            pointerStart = position;
            pointerTime = Time.unscaledTime;
            pointerTravel = 0;
            UpdatePointer(position);
        }
        public void UpdatePointer(Vector2 position)
        {
            if (!pointerActive || Stunned || ControlsLocked || !Alive) return;
            pointerTravel = Mathf.Max(pointerTravel, Vector2.Distance(position, pointerStart));
            pointerDragging |= pointerTravel > Mathf.Min(Screen.width, Screen.height) * .025f;
            if (touchPointer && nearTouchStart && !pointerDragging) return;
            if (view == null) view = Camera.main;
            targetX = Mathf.Clamp(view.ScreenToWorldPoint(position).x, -HalfWidth + 0.01f, HalfWidth - 0.01f);
            movingToTarget = true;
        }
        public static bool IsTap(float duration, float travel, float screenShortSide) =>
            duration <= 0.3f && travel <= screenShortSide * 0.025f;
        public static float TouchFireRangePixels(float screenWidth) =>
            TouchFireRangePixels(screenWidth, TouchFireScreenWidthFraction);
        public static float TouchFireRangePixels(float screenWidth, float screenWidthFraction) =>
            Mathf.Max(0, screenWidth) * Mathf.Max(0, screenWidthFraction);
        public static bool IsTouchShootPosition(Vector2 position, Vector2 playerPosition, float radius) =>
            IsTouchShootPosition(position, playerPosition, radius, float.PositiveInfinity);
        public static bool IsTouchShootPosition(Vector2 position, Vector2 playerPosition, float radius, float upperScreenY)
        {
            Vector2 offset = position - playerPosition;
            radius = Mathf.Max(0, radius) + .001f;
            return Mathf.Abs(offset.x) <= radius && position.y <= upperScreenY + .001f;
        }
        private float TouchShootUpperScreenY()
        {
            if (grid == null || view == null) return float.PositiveInfinity;
            return view.WorldToScreenPoint(grid.transform.TransformPoint(grid.CellPosition(0, 0))).y;
        }
        private float CurrentTouchFireRangePixels()
        {
            float screenWidth = view != null && view.pixelWidth > 0 ? view.pixelWidth : Screen.width;
            return TouchFireRangePixels(screenWidth, touchFireScreenWidthFraction);
        }
        public void EndPointer(Vector2 position, bool canceled)
        {
            if (!pointerActive) return;
            if (canceled || ControlsLocked || Stunned || !Alive) { CancelPointer(); return; }
            if (grid != null && grid.GetComponent<GameSession>()?.PointerOverMenu(position) == true) { CancelPointer(); return; }
            UpdatePointer(position);
            pointerActive = false;
            bool tap = IsTap(Time.unscaledTime - pointerTime, pointerTravel, Mathf.Min(Screen.width, Screen.height));
            if (tap && TryPointerFire(position))
            {
                movingToTarget = false;
                return;
            }
            if (!touchPointer) movingToTarget = false;
        }

        private bool TryPointerFire(Vector2 screenPosition)
        {
            if (!PointerFireTarget(screenPosition, out float fireX)) return false;
            MoveHorizontal(Wrap(fireX - transform.position.x));
            if (ControlsLocked || Stunned || !Alive) return false;
            return Fire(true);
        }

        private bool PointerFireTarget(Vector2 screenPosition, out float fireX)
        {
            fireX = transform.position.x;
            if (view == null) view = Camera.main;
            if (view == null) return false;
            float depth = view.WorldToScreenPoint(transform.position).z;
            var world = view.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            if (grid != null)
            {
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                {
                    if (enemy == null || !enemy.isActiveAndEnabled) continue;
                    if (!enemy.HitBounds.Contains(world)) continue;
                    fireX = Mathf.Clamp(enemy.HitBounds.center.x, -HalfWidth + .01f, HalfWidth - .01f);
                    return true;
                }
            }
            if (world.y > HitBounds.max.y + .001f && screenPosition.y <= TouchShootUpperScreenY() + .001f)
            {
                fireX = Mathf.Clamp(world.x, -HalfWidth + .01f, HalfWidth - .01f);
                return true;
            }
            float radius = view != null ? CurrentTouchFireRangePixels() : 0;
            if (IsTouchShootPosition(screenPosition, view.WorldToScreenPoint(transform.position), radius, TouchShootUpperScreenY()))
                return true;
            return false;
        }

        public void TickPointerMovement(float seconds)
        {
            if (!movingToTarget || ControlsLocked || Stunned || !Alive ||
                grid != null && grid.GetComponent<GameSession>()?.BlocksGameplayInput == true) return;
            float distance = touchSpeed * Mathf.Max(0, seconds);
            MoveHorizontal(Mathf.Clamp(Wrap(targetX - transform.position.x), -distance, distance));
            if (Mathf.Abs(Wrap(targetX - transform.position.x)) < .0001f) movingToTarget = false;
        }

        private void Update()
        {
            if (ControlsLocked || grid != null && grid.GetComponent<GameSession>()?.BlocksGameplayInput == true) return;
            TickSurvival(Time.deltaTime);
            if (!Alive) return;
            if (grid != null && grid.GetComponent<GameSession>()?.CheckPlayerContact() == true) return;
            tongue.Tick(Time.deltaTime, grid);
            RefreshColor();
            RefreshPresentation(Time.time);
            if (Stunned || AutonomousControl) return;
            float axis = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0)
                - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0);
            if (axis != 0) { movingToTarget = false; Move(axis, Time.deltaTime); }

            if (Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    if (finger == -1 && touch.phase == TouchPhase.Began)
                    { finger = touch.fingerId; BeginPointer(touch.position, true); }
                    if (touch.fingerId != finger) continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    { EndPointer(touch.position, touch.phase == TouchPhase.Canceled); finger = -1; }
                    else UpdatePointer(touch.position);
                }
            }
            else if (finger != -1) CancelPointer();
            else
            {
                if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition);
                else if (Input.GetMouseButton(0)) UpdatePointer(Input.mousePosition);
                if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition, false);
            }
            TickPointerMovement(Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Space)) Fire();
        }
        private void OnApplicationFocus(bool focused)
        { if (!focused) CancelPointer(); }
    }
}
