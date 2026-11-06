using UnityEngine;

namespace CandyCruisers
{
    // Presentation-only effects. Gameplay never depends on animation completion callbacks.
    public sealed class EnemyPresentation : MonoBehaviour
    {
        private SpriteRenderer body;
        private SpriteRenderer imitation;
        private LineRenderer tendril, portal, streak;
        private LineRenderer[] dashLines, feedingTendrils;
        private float dashRemaining;
        private int dashDirection;
        private bool feedingImitation;
        private float phaseRemaining, shiftRemaining, clock;
        public const float SpawnSeconds = .8f;
        private float phaseDuration = SpawnSeconds;
        public float SpawnSecondsRemaining => phaseRemaining;
        private GameplayMusicPlayer arrivalMusic;
        private AudioClip arrivalClip;
        private float arrivalEndSeconds, arrivalMusicSeconds;
        private EnemyColor phaseColor;
        private bool warpArrival;
        private bool growing;
        private bool beatLanding;
        private Vector3 restingBodyScale;
        public bool IsWarping => IsPhasing && warpArrival;
        public void GrowIn(EnemyColor color)
        {
            if (!growing) restingBodyScale = body.transform.localScale;
            growing = true;
            beatLanding = false;
            warpArrival = false;
            arrivalMusic = null;
            phaseDuration = SpawnSeconds;
            phaseRemaining = SpawnSeconds;
            Tick(0, color, 0);
        }
        public void SynchronizeArrival(GameplayMusicPlayer music, float finishSeconds, float duration = SpawnSeconds)
        {
            phaseDuration = Mathf.Max(.0001f, duration);
            beatLanding = true;
            arrivalMusic = music;
            arrivalClip = music.Source.clip;
            arrivalEndSeconds = finishSeconds;
            arrivalMusicSeconds = music.PlaybackSeconds;
            Tick(0, GetComponent<GridEnemy>().Color, 0);
        }
        private int shiftDirection;
        private GridEnemy target;
        private Vector3 targetPosition;
        private float imitationRemaining;
        private Sprite oldSprite;
        private EnemyColor imitationColor;
        private bool linkedImitation;
        private float revealRemaining;
        private Sprite yellowRevealSprite;
        private EnemyColor yellowRevealColor;
        private float yellowRevealAge, yellowRevealArrivalSeconds, yellowRevealReturnSeconds;
        public bool YellowRevealReturnActive => yellowRevealSprite != null &&
            yellowRevealAge + .0001f < yellowRevealArrivalSeconds + yellowRevealReturnSeconds;
        private float movementPulseRemaining;
        public const float MovementPulseSeconds = .38f;
        private LineRenderer[] surgeGlow, surgeCore;
        public const float SummonFlashSeconds = .42f;
        private float summonFlashRemaining;
        private LineRenderer summonGlow, summonCore;
        public void SummonFlash()
        {
            if (body == null) return;
            if (summonGlow == null)
            {
                summonGlow = Line("Purple summon flash glow", 33, .075f);
                summonCore = Line("Purple summon flash core", 33, .026f);
                summonGlow.useWorldSpace = summonCore.useWorldSpace = false;
                summonCore.sortingOrder++;
            }
            summonFlashRemaining = SummonFlashSeconds;
            Tick(0, GetComponent<GridEnemy>().Color, 0);
        }
        public void MovementPulse(int direction = 1)
        {
            if (body == null) return;
            EnsureSurge();
            movementPulseRemaining = MovementPulseSeconds;
            Tick(0, EnemyColor.Green, 0);
        }
        private Quaternion restingRotation;
        private bool aimingApplied;
        public const float GreenSpinSeconds = .35f;
        private float greenSpinRemaining;
        private Quaternion greenRestingRotation;
        public void SpinGreen()
        {
            if (body == null) return;
            if (greenSpinRemaining <= 0) greenRestingRotation = body.transform.localRotation;
            greenSpinRemaining = GreenSpinSeconds;
        }
        public void DashGreen(int direction)
        {
            SpinGreen();
            dashDirection = direction < 0 ? -1 : 1;
            dashRemaining = .42f;
            if (dashLines != null) return;
            dashLines = new LineRenderer[5];
            for (int i = 0; i < dashLines.Length; i++)
                dashLines[i] = Line("Green dash trail " + i, 3, .035f);
        }
        public void SetDashDirection(float distance)
        { if (Mathf.Abs(distance) > .000001f) dashDirection = distance < 0 ? -1 : 1; }
        public void AimRed(Vector3 direction, bool aiming, float seconds, bool firing = false)
        {
            if (body == null || !aiming && !aimingApplied) return;
            if (!aimingApplied) restingRotation = body.transform.localRotation;
            aimingApplied = true;
            float angle = aiming ? Vector3.SignedAngle(Vector3.down, direction, Vector3.forward) : 0;
            var desired = restingRotation * Quaternion.Euler(0, 0, angle);
            body.transform.localRotation = firing ? desired : Quaternion.RotateTowards(
                body.transform.localRotation, desired, 540f * Mathf.Max(0, seconds));
            if (!aiming && Quaternion.Angle(body.transform.localRotation, restingRotation) < .001f)
            { body.transform.localRotation = restingRotation; aimingApplied = false; }
        }
        public void BeginImitation(GridEnemy neighbor, Sprite previousSprite, bool linked = true)
        {
            yellowRevealAge = yellowRevealArrivalSeconds + yellowRevealReturnSeconds;
            yellowRevealSprite = null;
            linkedImitation = linked;
            feedingImitation = !linked;
            target = neighbor;
            targetPosition = neighbor.transform.position;
            oldSprite = previousSprite;
            imitationRemaining = EnemyAbilities.ImitationSeconds;
            imitationColor = neighbor.Color;
            body.sprite = neighbor.Visuals.Body.sprite;
            if (feedingImitation) EnsureFeedingTendrils();
        }
        public void DetachImitation()
        {
            linkedImitation = false;
            feedingImitation = imitationRemaining > 0;
            if (feedingImitation) EnsureFeedingTendrils();
            if (tendril != null) tendril.enabled = false;
        }
        public void BeginYellowRevealReturn(Sprite yellowSprite, EnemyColor mimickedColor,
            float arrivalSeconds, float returnSeconds)
        {
            if (body == null || yellowSprite == null) return;
            yellowRevealSprite = yellowSprite;
            yellowRevealColor = mimickedColor;
            yellowRevealArrivalSeconds = Mathf.Max(.01f, arrivalSeconds);
            yellowRevealReturnSeconds = Mathf.Max(.01f, returnSeconds);
            yellowRevealAge = 0;
            linkedImitation = false;
            feedingImitation = false;
            imitationRemaining = 0;
            target = null;
            if (tendril != null) tendril.enabled = false;
            if (feedingTendrils != null) foreach (var line in feedingTendrils) line.enabled = false;
            Tick(0, mimickedColor, 0);
        }
        public void RevealDisguise() => revealRemaining = .5f;
        public void RefreshSpriteFrame(float beatPosition)
        {
            if (imitation != null)
                imitation.sprite = EnemyBeatSprites.FrameAt(imitation.sprite, beatPosition);
        }
        public void Configure(SpriteRenderer renderer)
        {
            body = renderer;
            if (tendril != null) return;
            tendril = Line("Assimilation tendril", 17, .035f);
            portal = Line("Summoning rift", 33, .045f);
            streak = Line("Speed wake", 7, .055f);
            var overlay = new GameObject("Imitation overlay", typeof(SpriteRenderer));
            overlay.transform.SetParent(GetComponent<GridEnemy>().Visuals.Root, false);
            imitation = overlay.GetComponent<SpriteRenderer>();
            imitation.sortingOrder = body.sortingOrder + 2;
            imitation.enabled = false;
        }
        private LineRenderer Line(string name, int count, float width)
        {
            var child = new GameObject(name, typeof(LineRenderer));
            child.transform.SetParent(GetComponent<GridEnemy>().Visuals.Root, false);
            var line = child.GetComponent<LineRenderer>();
            line.sharedMaterial = body.sharedMaterial;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.startWidth = line.endWidth = width;
            line.sortingOrder = body.sortingOrder + 3;
            line.numCapVertices = 3;
            line.enabled = false;
            return line;
        }
        public bool IsPhasing => phaseRemaining > 0;
        public float SpawnOpacity => 1 - phaseRemaining / phaseDuration;
        public void PhaseIn(EnemyColor color, EnemyColor? effectColor = null)
        {
            if (growing) body.transform.localScale = restingBodyScale;
            growing = false;
            warpArrival = true;
            phaseColor = effectColor ?? color;
            arrivalMusic = null;
            phaseDuration = SpawnSeconds;
            phaseRemaining = SpawnSeconds;
            Tick(0, color, 0);
        }
        public void SpeedShift(int direction) { shiftDirection = direction; shiftRemaining = .35f; }
        public void Tick(float seconds, EnemyColor color, float warning)
        {
            if (body == null) return;
            if (greenSpinRemaining > 0)
            {
                greenSpinRemaining = Mathf.Max(0, greenSpinRemaining - Mathf.Max(0, seconds));
                float turn = 1 - greenSpinRemaining / GreenSpinSeconds;
                body.transform.localRotation = greenSpinRemaining > 0 ?
                    greenRestingRotation * Quaternion.Euler(0, 0, -360 * turn) : greenRestingRotation;
            }
            clock += seconds;
            if (arrivalMusic != null && arrivalMusic.TryAdvancePlayback(arrivalClip, ref arrivalMusicSeconds, out _))
            {
                phaseRemaining = Mathf.Clamp(arrivalEndSeconds - arrivalMusicSeconds, 0, phaseDuration);
            }
            else
            {
                // Track changes and seeks fall back to the remaining visual time without stalling.
                arrivalMusic = null;
                phaseRemaining = Mathf.Max(0, phaseRemaining - Mathf.Max(0, seconds));
            }
            if (phaseRemaining <= 0) arrivalMusic = null;
            shiftRemaining = Mathf.Max(0, shiftRemaining - seconds);
            imitationRemaining = Mathf.Max(0, imitationRemaining - seconds);
            revealRemaining = Mathf.Max(0, revealRemaining - seconds);
            if (YellowRevealReturnActive) yellowRevealAge = Mathf.Min(
                yellowRevealArrivalSeconds + yellowRevealReturnSeconds, yellowRevealAge + Mathf.Max(0, seconds));
            movementPulseRemaining = Mathf.Max(0, movementPulseRemaining - seconds);
            summonFlashRemaining = Mathf.Max(0, summonFlashRemaining - seconds);
            dashRemaining = Mathf.Max(0, dashRemaining - seconds);
            PresentDash();
            float assimilation = 1 - imitationRemaining / EnemyAbilities.ImitationSeconds;
            Color tint = EnemyPalette.Get(color);
            Color brilliant = color == EnemyColor.Purple ? new Color(1, .3f, 1) :
                color == EnemyColor.Green ? new Color(.65f, 1, .75f) : Color.white;
            var music = GetComponentInParent<GameplayMusicPlayer>();
            float pulsePhase = music != null && music.InGameplayRun ? music.BeatPosition * Mathf.PI * 4 : clock * 28;
            body.color = Color.Lerp(tint, brilliant, warning * (.6f + .4f * Mathf.Cos(pulsePhase)));
            if (color == EnemyColor.Green && movementPulseRemaining > 0)
                body.color = Color.Lerp(body.color, new Color(.85f, 1, .9f),
                    Mathf.Min(1, movementPulseRemaining * 8));
            PresentMovementSurge(color);
            PresentSummonFlash(color);
            if (phaseRemaining > 0) body.color = new Color(body.color.r, body.color.g, body.color.b, SpawnOpacity);
            if (growing)
            {
                float t = SpawnOpacity - 1;
                float scale = beatLanding ? Mathf.Pow(SpawnOpacity, 3) : 1 + 2.2f * t * t * t + 1.2f * t * t;
                body.transform.localScale = restingBodyScale * Mathf.Max(0, scale);
                if (phaseRemaining <= 0) { body.transform.localScale = restingBodyScale; growing = false; }
            }
            portal.enabled = phaseRemaining > 0 && warpArrival;
            if (portal.enabled)
            {
                Color riftColor = Color.Lerp(EnemyPalette.Get(phaseColor), Color.white, .25f);
                riftColor.a = Mathf.Min(1, phaseRemaining * 3);
                portal.startColor = portal.endColor = riftColor;
                for (int i = 0; i < portal.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / (portal.positionCount - 1);
                    float radius = .36f + .045f * Mathf.Sin(i * 7 + clock * 36);
                    portal.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle) * radius * .7f, Mathf.Sin(angle) * radius, 0));
                }
            }
            streak.enabled = shiftRemaining > 0;
            if (streak.enabled)
            {
                streak.startColor = new Color(.3f, 1, .6f, shiftRemaining * 2);
                streak.endColor = new Color(.3f, 1, .6f, 0);
                for (int i = 0; i < streak.positionCount; i++)
                    streak.SetPosition(i, transform.position + new Vector3(-shiftDirection * i * .10f, i % 2 == 0 ? -.06f : .06f, 0));
            }
            tendril.enabled = imitationRemaining > 0 && linkedImitation;
            imitation.enabled = imitationRemaining > 0;
            if (imitationRemaining > 0)
            {
                Color destination = EnemyPalette.Get(imitationColor);
                Color yellow = EnemyPalette.Get(EnemyColor.Yellow);
                body.color = Color.Lerp(yellow, destination, assimilation);
                imitation.sprite = oldSprite;
                imitation.color = new Color(yellow.r, yellow.g, yellow.b, 1 - assimilation);
                float size = body.sprite.bounds.size.x / imitation.sprite.bounds.size.x;
                imitation.transform.localScale = Vector3.one * size;
                tendril.startColor = yellow;
                tendril.endColor = destination;
                if (target != null && target.isActiveAndEnabled) targetPosition = target.transform.position;
                Vector3 delta = targetPosition - transform.position;
                Vector3 side = Vector3.Cross(delta.normalized, Vector3.forward);
                for (int i = 0; i < tendril.positionCount; i++)
                {
                    float t = (float)i / (tendril.positionCount - 1);
                    float reach = Mathf.Min(1, assimilation * 4);
                    float ripple = Mathf.Sin(t * 24 - clock * 9) * Mathf.Sin(t * Mathf.PI) * .06f;
                    tendril.SetPosition(i, transform.position + delta * t * reach + side * ripple);
                }
            }
            if (revealRemaining > 0)
                body.color = Color.Lerp(EnemyPalette.Get(EnemyColor.Yellow), Color.white,
                    .5f + .5f * Mathf.Cos((.5f - revealRemaining) * Mathf.PI * 12));
            PresentYellowRevealReturn();
            PresentFeedingTendrils(assimilation);
        }

        private void PresentYellowRevealReturn()
        {
            if (imitation == null || imitationRemaining > 0) return;
            imitation.enabled = false;
            if (!YellowRevealReturnActive) return;
            float restore = Mathf.Clamp01((yellowRevealAge - yellowRevealArrivalSeconds) /
                Mathf.Max(.01f, yellowRevealReturnSeconds));
            float settled = Mathf.SmoothStep(0, 1, restore);
            var yellow = EnemyPalette.Get(EnemyColor.Yellow);
            var destination = EnemyPalette.Get(yellowRevealColor);
            body.color = Color.Lerp(yellow, destination, settled);
            imitation.enabled = true;
            imitation.sprite = yellowRevealSprite;
            float size = body.sprite != null && yellowRevealSprite != null ?
                body.sprite.bounds.size.x / yellowRevealSprite.bounds.size.x : 1;
            float arrival = Mathf.SmoothStep(0, 1, Mathf.Clamp01(yellowRevealAge /
                Mathf.Max(.01f, yellowRevealArrivalSeconds)));
            float alpha = yellowRevealAge <= yellowRevealArrivalSeconds ? arrival : 1 - settled;
            imitation.transform.localScale = Vector3.one * size *
                Mathf.Lerp(1.16f, 1, Mathf.Min(1, arrival + settled));
            imitation.color = new Color(yellow.r, yellow.g, yellow.b, alpha);
        }
        private void PresentDash()
        {
            if (dashLines == null) return;
            float fade = Mathf.Clamp01(dashRemaining / .42f);
            for (int i = 0; i < dashLines.Length; i++)
            {
                var line = dashLines[i];
                line.enabled = dashRemaining > 0;
                if (!line.enabled) continue;
                float length = .3f + (i % 3) * .12f;
                var head = body.bounds.center + new Vector3(-dashDirection * .12f, (i - 2) * .09f);
                line.startColor = new Color(.8f, 1, .7f, fade);
                line.endColor = new Color(.25f, 1, .45f, 0);
                line.startWidth = .035f * fade; line.endWidth = .008f * fade;
                for (int j = 0; j < 3; j++)
                    line.SetPosition(j, head + Vector3.left * dashDirection * (length + (1 - fade) * .16f) * j / 2);
            }
        }

        private void EnsureFeedingTendrils()
        {
            if (feedingTendrils != null) return;
            feedingTendrils = new LineRenderer[5];
            for (int i = 0; i < feedingTendrils.Length; i++)
                feedingTendrils[i] = Line("Disguise feeding tendril " + i, 25, .04f);
        }

        private void PresentFeedingTendrils(float progress)
        {
            if (feedingTendrils == null) return;
            bool active = feedingImitation && imitationRemaining > 0;
            if (target != null && target.isActiveAndEnabled) targetPosition = target.transform.position;
            var delta = targetPosition - transform.position;
            var side = Vector3.Cross(delta.normalized, Vector3.forward);
            float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .2f));
            float fade = Mathf.Clamp01((1 - progress) / .16f);
            var yellow = EnemyPalette.Get(EnemyColor.Yellow);
            var food = EnemyPalette.Get(imitationColor);
            for (int arm = 0; arm < feedingTendrils.Length; arm++)
            {
                var line = feedingTendrils[arm];
                line.enabled = active;
                if (!active) continue;
                // Traveling colored bulges run from the neighbor back into the Yellow.
                var colors = new GradientColorKey[8];
                var widths = new Keyframe[8];
                for (int k = 0; k < 8; k++)
                {
                    float t = k / 7f;
                    float gulp = Mathf.Pow(.5f + .5f * Mathf.Sin(t * 15 + progress * 30 + arm * 1.7f), 4);
                    colors[k] = new GradientColorKey(Color.Lerp(yellow, food, gulp * reach), t);
                    widths[k] = new Keyframe(t, (.018f + .035f * gulp) * fade);
                }
                var gradient = new Gradient();
                gradient.SetKeys(colors, new[] { new GradientAlphaKey(fade, 0), new GradientAlphaKey(fade, 1) });
                line.colorGradient = gradient;
                line.widthCurve = new AnimationCurve(widths); line.widthMultiplier = 1;
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = (float)i / (line.positionCount - 1);
                    float curl = Mathf.Sin(t * Mathf.PI) * ((arm - 2) * .13f +
                        .065f * Mathf.Sin(t * 12 + clock * 5 + arm * 2));
                    line.SetPosition(i, transform.position + delta * t * reach + side * curl * reach);
                }
            }
        }
        private void EnsureSurge()
        {
            if (surgeGlow != null) return;
            surgeGlow = new LineRenderer[5];
            surgeCore = new LineRenderer[5];
            for (int i = 0; i < surgeGlow.Length; i++)
            {
                surgeGlow[i] = Line("Green surge glow " + i, i == 0 ? 25 : 7, .065f);
                surgeCore[i] = Line("Green surge core " + i, i == 0 ? 25 : 7, .018f);
                surgeGlow[i].useWorldSpace = surgeCore[i].useWorldSpace = false;
                surgeCore[i].sortingOrder++;
            }
        }

        private void PresentMovementSurge(EnemyColor color)
        {
            if (surgeGlow == null) return;
            bool active = color == EnemyColor.Green && movementPulseRemaining > 0;
            float age = MovementPulseSeconds - movementPulseRemaining;
            float fade = Mathf.Min(1, movementPulseRemaining / .12f);
            float radius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.y);
            int frame = Mathf.FloorToInt(age * 24);
            for (int arc = 0; arc < surgeGlow.Length; arc++)
            {
                var glow = surgeGlow[arc];
                var core = surgeCore[arc];
                glow.enabled = core.enabled = active;
                if (!active) continue;
                glow.startColor = glow.endColor = new Color(.05f, 1, .2f, .9f * fade);
                core.startColor = core.endColor = new Color(.8f, 1, .65f, fade);
                for (int i = 0; i < glow.positionCount; i++)
                {
                    float t = (float)i / (glow.positionCount - 1);
                    // Deterministic flicker never consumes the gameplay random sequence.
                    float jitter = Mathf.Sin((i % 24) * 17.7f + arc * 9.3f + frame * 13.1f);
                    Vector3 offset;
                    if (arc == 0)
                    {
                        float angle = t * Mathf.PI * 2;
                        float reach = radius * (1.14f + .22f * jitter);
                        offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                    }
                    else
                    {
                        float angle = arc * Mathf.PI * .5f + .35f * Mathf.Sin(frame * 2 + arc);
                        var outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
                        var side = new Vector3(-outward.y, outward.x);
                        offset = outward * radius * (.8f + t * 1.05f) + side * radius * jitter * .27f;
                    }
                    Vector3 point = glow.transform.InverseTransformPoint(body.bounds.center + offset);
                    glow.SetPosition(i, point);
                    core.SetPosition(i, point);
                }
            }
        }

        private void PresentSummonFlash(EnemyColor color)
        {
            if (summonGlow == null) return;
            bool active = color == EnemyColor.Purple && summonFlashRemaining > 0;
            summonGlow.enabled = summonCore.enabled = active;
            if (!active) return;
            float age = SummonFlashSeconds - summonFlashRemaining;
            // A brief white hold makes the source readable before the violet afterglow fades.
            float flash = 1 - Mathf.Clamp01((age - .08f) / (SummonFlashSeconds - .08f));
            body.color = Color.Lerp(body.color, Color.white, flash);
            summonGlow.startColor = summonGlow.endColor = new Color(1, .15f, 1, flash * .9f);
            summonCore.startColor = summonCore.endColor = new Color(1, .9f, 1, flash);
            float radius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.y) *
                Mathf.Lerp(1.15f, 1.8f, age / SummonFlashSeconds);
            for (int i = 0; i < summonGlow.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / (summonGlow.positionCount - 1);
                float flicker = 1 + .07f * Mathf.Sin(i * 7 + age * 50);
                Vector3 point = summonGlow.transform.InverseTransformPoint(body.bounds.center +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * flicker));
                summonGlow.SetPosition(i, point);
                summonCore.SetPosition(i, point);
            }
        }

        public void Clear()
        {
            if (growing && body != null) body.transform.localScale = restingBodyScale;
            growing = false;
            if (greenSpinRemaining > 0 && body != null) body.transform.localRotation = greenRestingRotation;
            greenSpinRemaining = 0;
            if (aimingApplied && body != null) body.transform.localRotation = restingRotation;
            aimingApplied = false;
            phaseRemaining = shiftRemaining = imitationRemaining = revealRemaining = movementPulseRemaining = summonFlashRemaining = dashRemaining = 0;
            yellowRevealSprite = null;
            yellowRevealAge = yellowRevealArrivalSeconds = yellowRevealReturnSeconds = 0;
            feedingImitation = false;
            linkedImitation = false;
            target = null;
            if (tendril == null) return;
            tendril.enabled = portal.enabled = streak.enabled = imitation.enabled = false;
            if (dashLines != null) foreach (var line in dashLines) line.enabled = false;
            if (feedingTendrils != null) foreach (var line in feedingTendrils) line.enabled = false;
            if (surgeGlow != null)
                for (int i = 0; i < surgeGlow.Length; i++) surgeGlow[i].enabled = surgeCore[i].enabled = false;
            if (summonGlow != null) summonGlow.enabled = summonCore.enabled = false;
        }
        private void OnDisable() => Clear();
    }
}
