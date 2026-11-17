using UnityEngine;

namespace CandyCruisers
{
    public sealed class EnemyAbilities : MonoBehaviour
    {
        private GridEnemy enemy;
        private GameObject missilePrefab;
        private SpriteRenderer shield;
        private SpriteRenderer body;
        private EnemyColor currentColor;
        private readonly AbilityBeatClock beatClock = new AbilityBeatClock();
        private float cooldownBeat;
        private float castBeat;
        private GameplayMusicPlayer music;
        private float cooldown => beatClock.SecondsUntil(cooldownBeat, music != null && (Application.isPlaying || music.PreviewTime.HasValue) ? music : null);
        private EnemyGrid grid;
        private EnemyPresentation presentation;
        private float castRemaining;
        public const int ImitationBeats = 2;
        public const float DefaultImitationSeconds = ImitationBeats * AbilityBeatClock.DefaultBeatSeconds;
        private float imitationEndBeat;
        private float imitationRemaining;
        public EnemyColor AbilityColor => enemy == null ? currentColor :
            grid != null && grid.Model.IsAttached(enemy.Id) ? grid.Model.CopiedColor(enemy.Id).Value : enemy.Color;
        public bool YellowBlueShield => IsDisguised && AbilityColor == EnemyColor.Blue;
        public Color ShieldTint => YellowBlueShield ? EnemyPalette.Get(EnemyColor.Yellow) : new Color(.82f, 1, 1);
        public bool IsDisguised => enemy != null && grid != null && grid.Model.IsAttached(enemy.Id) && grid.Model.IsSpecialImitation(enemy.Id);
        public bool IsTransforming => imitationRemaining > 0;
        private const float ShieldPowerSeconds = .35f;
        private float shieldPowerRemaining;
        private bool shieldSpent;
        private bool awaitingSpawnShield;
        private Vector3 shieldFullScale;
        private bool groupShielded;
        public void SetGroupShielded(bool value)
        {
            groupShielded = value;
            if (shield != null) RefreshShield();
        }
        public bool IsCasting => castRemaining > 0;
        public bool Suspended { get; set; }
        public bool ShieldActive { get; private set; }
        public float CooldownRemaining => Mathf.Max(0, cooldown);
        internal void OnPromoted()
        {
            if (grid.Model.PromoteImitation(enemy.Id))
            {
                ResetAbility();
                return;
            }
            if (enemy == null || AbilityColor != EnemyColor.Blue) return;
            awaitingSpawnShield = presentation.IsPhasing;
            ShieldActive = !awaitingSpawnShield;
            if (ShieldActive) grid.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.ShieldPower);
            shieldPowerRemaining = awaitingSpawnShield ? ShieldPowerSeconds : 0;
            RefreshShield();
        }
        private bool wasGreenCaster;
        private bool greenTempoTriggered;
        public bool GreenTempoActive => greenTempoTriggered && enemy != null && enemy.IsSpecial && AbilityColor == EnemyColor.Green;
        private bool CanCastGreen => enemy != null && AbilityColor == EnemyColor.Green &&
            enemy.IsSpecial && !greenTempoTriggered;
        private bool UsesCast => enemy != null && (AbilityColor == EnemyColor.Green ||
            AbilityColor == EnemyColor.Purple || AbilityColor == EnemyColor.Orange);
        private float TriggerBeat => IsCasting ? castBeat : cooldownBeat + (UsesCast ? 1 : 0);
        public bool Warning => enemy != null && !IsTransforming &&
            (AbilityColor != EnemyColor.Green || CanCastGreen) &&
            (AbilityColor != EnemyColor.Blue || !ShieldActive && !shieldSpent && !awaitingSpawnShield && shieldPowerRemaining <= 0) &&
            beatClock.Reached(TriggerBeat - 1) && !beatClock.Reached(TriggerBeat);
        public Bounds ShieldBounds => new Bounds(enemy.HitBounds.center, enemy.HitBounds.size * 1.25f);
        public void BeginSpawnEffect(EnemyColor? effectColor = null, bool warp = false)
        {
            if (warp) presentation.PhaseIn(AbilityColor, effectColor);
            else presentation.GrowIn(AbilityColor);
            if (AbilityColor == EnemyColor.Blue)
            {
                ShieldActive = false;
                awaitingSpawnShield = true;
                shieldPowerRemaining = enemy.IsSpecial ? ShieldPowerSeconds : 0;
                if (!enemy.IsSpecial) ScheduleCooldown();
            }
            RefreshShield();
        }

        public void Configure(GridEnemy owner, GameObject projectile, Sprite shieldSprite)
        {
            bool retainGreenTempo = enemy == owner && GreenTempoActive;
            enemy = owner;
            missilePrefab = projectile;
            body = enemy.Visuals.Body;
            grid = GetComponentInParent<EnemyGrid>();
            music = grid.GetComponent<GameplayMusicPlayer>();
            AdvanceBeatClock(0);
            presentation = GetComponent<EnemyPresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<EnemyPresentation>();
            presentation.Configure(body);
            if (shield == null)
            {
                var visual = new GameObject("Shield", typeof(SpriteRenderer));
                visual.transform.SetParent(enemy.Visuals.Root, false);
                shield = visual.GetComponent<SpriteRenderer>();
                shield.sprite = shieldSprite;
                shield.color = new Color(0.3f, 0.85f, 1f, 0.9f);
                shield.sortingOrder = body.sortingOrder + 1;
            }
            ResetAbility();
            greenTempoTriggered = retainGreenTempo;
            if (GreenTempoActive) presentation.TempoUp();
        }

        private void ResetAbility()
        {
            grid.GetComponent<EnemyRowSpawner>()?.ApplyAppearance(enemy);
            currentColor = AbilityColor;
            castRemaining = 0;
            imitationRemaining = 0;
            presentation.Clear();
            ShieldActive = currentColor == EnemyColor.Blue && enemy.IsSpecial;
            awaitingSpawnShield = false;
            shieldPowerRemaining = 0;
            shieldSpent = false;
            greenTempoTriggered = false;
            ScheduleCooldown();
            wasGreenCaster = CanCastGreen;
            if (shield != null && shield.sprite != null)
                shieldFullScale = Vector3.one *
                    (Mathf.Max(body.sprite.bounds.size.x, body.sprite.bounds.size.y) * 1.25f / shield.sprite.bounds.size.x);
            RefreshVisuals();
        }

        private void ScheduleCooldown(bool noEffect = false)
        {
            var range = CombatBalance.CooldownBeats(currentColor, grid.GetComponent<GameSession>()?.Progress.Level ?? 1,
                enemy.IsSpecial);
            float beats = Random.Range(Mathf.RoundToInt(range.x), Mathf.RoundToInt(range.y) + 1);
            if (noEffect) beats = Mathf.Max(1, Mathf.Round(beats * .5f));
            cooldownBeat = beatClock.AfterBeats(Mathf.RoundToInt(beats));
        }

        private void AdvanceBeatClock(float seconds)
        {
            if (music == null) music = grid.GetComponent<GameplayMusicPlayer>();
            if (music != null && (music.PreviewTime.HasValue || Application.isPlaying && music.Source.clip != null))
                beatClock.Advance(seconds, music.BeatPosition, music.Source.clip != null ? music.Source.clip.GetInstanceID() : 0);
            else beatClock.Advance(seconds);
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (Suspended || enemy == null || !isActiveAndEnabled) return;
            AdvanceBeatClock(seconds);
            if (grid.Model.IsAttached(enemy.Id) && !grid.Model.IsSpecialImitation(enemy.Id)) grid.RefreshMimicCoverage();
            if (currentColor != AbilityColor)
            {
                ResetAbility();
                if (grid.Model.IsAttached(enemy.Id) && grid.Model.TryGetImitationTarget(enemy.Id, out var attachedHost))
                    presentation.SetHostAttachment(grid.View(attachedHost.Id), !grid.Model.IsSpecialImitation(enemy.Id));
            }
            if (CanCastGreen && !wasGreenCaster) ScheduleCooldown();
            wasGreenCaster = CanCastGreen;
            seconds = Mathf.Max(0, seconds);
            if (IsTransforming)
            {
                if (!grid.Model.TryGetImitationTarget(enemy.Id, out _))
                {
                    grid.SetColor(enemy.Id, EnemyColor.Yellow);
                    ResetAbility();
                    return;
                }
                imitationRemaining = Mathf.Max(0, imitationEndBeat - beatClock.Position);
                presentation.SetImitationProgress(1 - imitationRemaining / ImitationBeats);
                presentation.Tick(seconds, AbilityColor, 0);
                if (imitationRemaining <= .000001f)
                {
                    grid.CompleteImitation(enemy.Id, out _);
                    ResetAbility();
                    presentation.SetHostAttachment(grid.View(grid.Model.TryGetImitationTarget(enemy.Id, out var host) ? host.Id : 0), !grid.Model.IsSpecialImitation(enemy.Id));
                    grid.RefreshSpecials();
                    grid.RefreshMimicCoverage();
                }
                return;
            }
            float shieldSeconds = awaitingSpawnShield ? Mathf.Max(0, seconds - presentation.SpawnSecondsRemaining) : seconds;
            if (awaitingSpawnShield && shieldSeconds > 0)
            {
                awaitingSpawnShield = false;
                if (shieldPowerRemaining > 0) grid.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.ShieldPower);
            }
            if (!CanCastGreen && AbilityColor == EnemyColor.Green) castRemaining = 0;
            bool startSummonFlash = false;
            if (IsCasting)
            {
                if (beatClock.Reached(castBeat))
                {
                    castRemaining = 0;
                    startSummonFlash = CompleteCast();
                    ScheduleCooldown(AbilityColor == EnemyColor.Purple && !startSummonFlash);
                }
            }
            var player = AbilityColor == EnemyColor.Red ? FindFirstObjectByType<PlayerMovement>() : null;
            Vector3 aimDirection = player != null && player.Alive && !player.ReplacementFalling
                ? player.transform.position - transform.position : Vector3.down;
            aimDirection.z = 0;
            aimDirection = aimDirection.sqrMagnitude > .000001f ? aimDirection.normalized : Vector3.down;
            bool aimingRed = AbilityColor == EnemyColor.Red && enemy.IsSpecial && (Warning || beatClock.Reached(cooldownBeat));
            presentation.AimRed(aimDirection, aimingRed, seconds, aimingRed && cooldown <= 0);
            if (AbilityColor == EnemyColor.Red && beatClock.Reached(cooldownBeat))
            {
                Vector3 heading = enemy.IsSpecial ? aimDirection : Vector3.down;
                var missile = Instantiate(missilePrefab, transform.position + heading * 0.3f, Quaternion.identity);
                var projectile = missile.GetComponent<EnemyMissile>();
                if (enemy.IsSpecial) projectile.LaunchAimed(player, heading);
                else projectile.SetTarget(player);
                var flightSound = projectile.GetComponent<MissileFlightSound>();
                if (flightSound == null) flightSound = projectile.gameObject.AddComponent<MissileFlightSound>();
                flightSound.Configure(projectile, grid.GetComponent<SoundEffects>());
                enemy.Visuals.Fire(AbilityColor);
                ScheduleCooldown();
            }
            else if (AbilityColor == EnemyColor.Blue && !shieldSpent && !ShieldActive && !awaitingSpawnShield && shieldPowerRemaining <= 0 && beatClock.Reached(cooldownBeat))
            {
                shieldPowerRemaining = ShieldPowerSeconds;
                grid.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.ShieldPower);
                shieldSeconds = Mathf.Max(0, -cooldown);
            }
            else if (AbilityColor >= EnemyColor.Green && (AbilityColor != EnemyColor.Green || CanCastGreen) && beatClock.Reached(cooldownBeat) && !IsCasting)
            {
                ScheduleCooldown();
                if (AbilityColor == EnemyColor.Yellow)
                {
                    var targets = grid.Model.ImitationTargets(enemy.Id);
                    if (targets.Count > 0)
                    {
                        BeginImitation(grid.View(targets[Random.Range(0, targets.Count)].Id));
                        return;
                    }
                    ScheduleCooldown(true);
                }
                else if (AbilityColor == EnemyColor.Purple)
                {
                    if (grid.GetComponent<EnemyRowSpawner>()?.CanSummon(enemy) == true)
                    {
                        castRemaining = 1;
                        castBeat = beatClock.AfterBeats(1);
                    }
                    else ScheduleCooldown(true);
                }
                else { castRemaining = 1; castBeat = beatClock.AfterBeats(1); }
            }
            RefreshVisuals();
            if (GreenTempoActive) presentation.TempoUp();
            presentation.Tick(seconds, AbilityColor, Warning ? 1 : 0);
            if (startSummonFlash) presentation.SummonFlash();
            if (awaitingSpawnShield && !presentation.IsPhasing)
            {
                awaitingSpawnShield = false;
                if (shieldPowerRemaining > 0) grid.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.ShieldPower);
            }
            if (!awaitingSpawnShield && shieldPowerRemaining > 0)
            {
                shieldPowerRemaining = Mathf.Max(0, shieldPowerRemaining - shieldSeconds);
                if (shieldPowerRemaining < .00001f) shieldPowerRemaining = 0;
                if (shieldPowerRemaining <= 0) ShieldActive = true;
            }
            RefreshShield();
        }

        private bool CompleteCast()
        {
            if (AbilityColor == EnemyColor.Green && CanCastGreen)
            {
                greenTempoTriggered = true;
                return false;
            }
            else if (AbilityColor == EnemyColor.Purple)
            {
                return grid.GetComponent<EnemyRowSpawner>()?.TrySummon(enemy) != null;
            }
            else if (AbilityColor == EnemyColor.Orange) grid.TryOrangeSwap(enemy.Id);
            return false;
        }

        public bool BeginImitation(GridEnemy target)
        {
            if (Suspended || !isActiveAndEnabled || enemy == null || enemy.Color != EnemyColor.Yellow ||
                target == null || IsTransforming || grid.Model.IsAttached(enemy.Id) || grid.View(target.Id) != target) return false;
            if (!grid.Model.BeginImitation(enemy.Id, target.Id, enemy.IsSpecial)) return false;
            if (enemy.IsSpecial)
            {
                ResetAbility();
                grid.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.YellowHide);
                grid.RefreshSpecials();
                return true;
            }
            AdvanceBeatClock(0);
            imitationRemaining = ImitationBeats;
            imitationEndBeat = beatClock.Position + ImitationBeats;
            presentation.BeginImitation(target, body.sprite, true);
            grid.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.YellowTransform);
            presentation.Tick(0, AbilityColor, 0);
            return true;
        }

        internal void RefreshIdentity() => ResetAbility();

        internal void OnHostKilled()
        {
            ResetAbility();
            presentation.RevealDisguise();
            grid.GetComponent<SoundEffects>()?.PlayCue(SoundEffect.YellowReveal);
            presentation.Tick(0, enemy.Color, 0);
        }

        internal void OnDemoted()
        {
            if (greenTempoTriggered) presentation.TempoDown();
            ResetAbility();
        }

        public bool RevealDisguise(EnemyColor shotColor) => IsDisguised && shotColor == AbilityColor && !YellowBlueShield && grid.RevealImitation(enemy.Id);

        public bool Absorb(EnemyColor shotColor)
        {
            if (enemy == null || AbilityColor != EnemyColor.Blue || !ShieldActive || shotColor == EnemyColor.Blue || shotColor == enemy.Color) return false;
            if (enemy.IsSpecial) return true;
            ShieldActive = false;
            shieldSpent = true;
            shieldPowerRemaining = 0;
            RefreshVisuals();
            return true;
        }

        private void RefreshVisuals()
        {
            RefreshShield();
            body.color = Warning ? Color.Lerp(EnemyPalette.Get(AbilityColor), Color.white, 0.65f) : EnemyPalette.Get(AbilityColor);
        }
        private void RefreshShield()
        {
            shield.enabled = !groupShielded && AbilityColor == EnemyColor.Blue && !awaitingSpawnShield && (ShieldActive || shieldPowerRemaining > 0);
            float progress = ShieldActive ? 1 : 1 - shieldPowerRemaining / ShieldPowerSeconds;
            float growth = Mathf.SmoothStep(0, 1, progress);
            shield.transform.localScale = shieldFullScale * Mathf.Max(.001f, growth);
            shield.transform.localPosition = Vector3.down * body.sprite.bounds.extents.y * 1.1f * (1 - growth);
            var tint = ShieldTint;
            shield.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(.65f, 1, growth));
        }
        private void OnDisable()
        {
            if (grid != null && enemy != null) grid.Model.CancelImitation(enemy.Id);
            ShieldActive = false;
            awaitingSpawnShield = false;
            shieldPowerRemaining = 0;
            if (shield != null) shield.enabled = false;
            if (body != null && enemy != null) body.color = EnemyPalette.Get(AbilityColor);
            castRemaining = 0;
            imitationRemaining = 0;
            if (presentation != null) presentation.Clear();
        }
    }
}
