using UnityEngine;

namespace CandyCruisers
{
    // Presentation-only effects. Gameplay never depends on animation completion callbacks.
    [DefaultExecutionOrder(20)]
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
        private bool tempoArrow;
        public const float MovementPulseSeconds = .38f;
        private Transform movementArrow;
        private MeshRenderer[] movementArrowLayers;
        private MaterialPropertyBlock movementArrowTint;
        private EnemyGridMovement arrowFleet;
        private int movementArrowDirection = 1, arrowFleetDirection = 1;
        private static Mesh movementArrowMesh, movementArrowGradientMesh;
        private static Material movementArrowMaterial;
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
            EnsureMovementArrow();
            movementArrowDirection = direction < 0 ? -1 : 1;
            arrowFleetDirection = arrowFleet != null ? arrowFleet.Direction : movementArrowDirection;
            movementPulseRemaining = MovementPulseSeconds;
            Tick(0, EnemyColor.Green, 0);
        }
        public void TempoUp()
        {
            EnsureMovementArrow();
            if (!tempoArrow) movementPulseRemaining = MovementPulseSeconds;
            tempoArrow = true;
            PresentMovementArrow(EnemyColor.Green);
        }

        public void TempoDown()
        {
            EnsureMovementArrow();
            // The departing arrow lives independently of the destroyed enemy.
            TempoUp();
            var copy = Instantiate(movementArrow.gameObject, movementArrow.position, Quaternion.Euler(0, 0, -90));
            copy.name = "Green tempo down arrow";
            copy.transform.localScale = movementArrow.lossyScale;
            GreenTempoFall.Create(copy, GetComponentInParent<GameplayMusicPlayer>()?.BeatDuration ?? AbilityBeatClock.DefaultBeatSeconds);
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
            PresentMovementArrow(color);
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
        private void EnsureMovementArrow()
        {
            if (movementArrow != null) return;
            if (movementArrowMesh == null)
            {
                movementArrowMesh = new Mesh { name = "Green rhythm arrow" };
                // Chamfered dance-pad silhouette. Authored upward, then rotated to
                // the right so movement, tempo-up and tempo-down share one graphic.
                var outline = new[] {
                    new Vector2(-.5f, -.06f), new Vector2(-.5f, .06f),
                    new Vector2(-.016f, .5f), new Vector2(.016f, .5f),
                    new Vector2(.5f, .06f), new Vector2(.5f, -.06f),
                    new Vector2(.4f, -.16f), new Vector2(.25f, -.16f),
                    new Vector2(.17f, -.08f), new Vector2(.17f, -.35f),
                    new Vector2(.03f, -.5f), new Vector2(-.03f, -.5f),
                    new Vector2(-.17f, -.35f), new Vector2(-.17f, -.08f),
                    new Vector2(-.25f, -.16f), new Vector2(-.4f, -.16f)
                };
                var vertices = new Vector3[outline.Length];
                var colors = new Color[outline.Length];
                for (int i = 0; i < outline.Length; i++)
                {
                    vertices[i] = new Vector3(outline[i].y, -outline[i].x);
                    colors[i] = Color.white;
                }
                movementArrowMesh.vertices = vertices;
                movementArrowMesh.triangles = TriangulateArrow(vertices);
                movementArrowMesh.colors = colors;
                movementArrowMesh.RecalculateBounds();
            }
            if (movementArrowMaterial == null)
                movementArrowMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Green rhythm arrow" };
            movementArrow = new GameObject("Green movement arrow").transform;
            movementArrow.SetParent(GetComponent<GridEnemy>().Visuals.Root, false);
            arrowFleet = GetComponentInParent<EnemyGridMovement>();
            movementArrowTint = new MaterialPropertyBlock();
            movementArrowLayers = new MeshRenderer[4];
            string[] names = { "outline", "blue rim", "white inset", "blue core" };
            float[] scales = { 1.12f, 1, .78f, .53f };
            for (int i = 0; i < movementArrowLayers.Length; i++)
            {
                var child = new GameObject("Green arrow " + names[i], typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(movementArrow, false);
                child.transform.localScale = Vector3.one * scales[i];
                child.GetComponent<MeshFilter>().sharedMesh = movementArrowMesh;
                if (i == 1 || i == 3)
                {
                    if (movementArrowGradientMesh == null)
                    {
                        movementArrowGradientMesh = Instantiate(movementArrowMesh);
                        var colors = new Color[movementArrowGradientMesh.vertexCount];
                        for (int v = 0; v < colors.Length; v++)
                            colors[v] = Color.Lerp(new Color(0, .78f, .96f), new Color(.2f, .27f, .62f),
                                movementArrowGradientMesh.vertices[v].x + .5f);
                        movementArrowGradientMesh.colors = colors;
                    }
                    child.GetComponent<MeshFilter>().sharedMesh = movementArrowGradientMesh;
                }
                var renderer = movementArrowLayers[i] = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = movementArrowMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + 4 + i;
                renderer.enabled = false;
            }
        }

        private static int[] TriangulateArrow(Vector3[] vertices)
        {
            var remaining = new System.Collections.Generic.List<int>();
            var triangles = new System.Collections.Generic.List<int>();
            // The outline is clockwise. Clip convex ears, excluding concave notches.
            for (int i = 0; i < vertices.Length; i++) remaining.Add(i);
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[b]).z >= 0) continue;
                    bool contains = false;
                    foreach (int v in remaining)
                    {
                        if (v == a || v == b || v == c) continue;
                        if (Vector3.Cross(vertices[b] - vertices[a], vertices[v] - vertices[a]).z <= 0 &&
                            Vector3.Cross(vertices[c] - vertices[b], vertices[v] - vertices[b]).z <= 0 &&
                            Vector3.Cross(vertices[a] - vertices[c], vertices[v] - vertices[c]).z <= 0)
                        { contains = true; break; }
                    }
                    if (contains) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) throw new System.InvalidOperationException("Invalid rhythm arrow outline");
            }
            return triangles.ToArray();
        }

        private void RefreshMovementArrowDirection()
        {
            if (movementArrow == null) return;
            // Follow a reversal during the pulse while retaining explicit preview directions.
            if (!tempoArrow && arrowFleet != null && arrowFleet.Direction != arrowFleetDirection)
                movementArrowDirection = arrowFleetDirection = arrowFleet.Direction;
            movementArrow.rotation = Quaternion.Euler(0, 0, tempoArrow ? 90 : movementArrowDirection < 0 ? 180 : 0);
        }

        private void LateUpdate()
        {
            // Resolve placement after fleet movement and the actor's beat-scale animation.
            if (movementArrow != null && (movementPulseRemaining > 0 || tempoArrow))
                PresentMovementArrow(GetComponent<GridEnemy>().Color);
        }

        private void PresentMovementArrow(EnemyColor color)
        {
            if (movementArrow == null) return;
            bool active = color == EnemyColor.Green && (movementPulseRemaining > 0 || tempoArrow);
            foreach (var renderer in movementArrowLayers) renderer.enabled = active;
            if (!active) return;
            RefreshMovementArrowDirection();
            float progress = 1 - movementPulseRemaining / MovementPulseSeconds;
            float width = Mathf.Clamp(Mathf.Max(body.bounds.size.x, body.bounds.size.y) * 1.8f, 1.05f, 1.35f);
            if (tempoArrow)
                width = Mathf.Min(width, (GetComponentInParent<EnemyGrid>()?.Spacing ?? .75f) * .78f);
            width *= 1 + .12f * Mathf.Sin(progress * Mathf.PI);
            var parentScale = movementArrow.parent.lossyScale;
            movementArrow.localScale = new Vector3(width / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                width / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
            var center = body.bounds.center + (tempoArrow ? Vector3.up * (body.bounds.extents.y + width * .57f) :
                Vector3.right * movementArrowDirection * .09f * Mathf.SmoothStep(0, 1, progress));
            float edge = PlayerMovement.HalfWidth - width * .56f - .025f;
            center.x = Mathf.Clamp(center.x, -edge, edge);
            movementArrow.position = center;
            float fade = tempoArrow ? 1 : Mathf.Clamp01(movementPulseRemaining / .14f);
            for (int i = 0; i < movementArrowLayers.Length; i++)
            {
                var tint = i == 0 ? new Color(.13f, .12f, .12f) : Color.white;
                tint.a = fade;
                movementArrowTint.SetColor("_Color", tint);
                movementArrowLayers[i].SetPropertyBlock(movementArrowTint);
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
            tempoArrow = false;
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
            if (movementArrowLayers != null)
                foreach (var renderer in movementArrowLayers) renderer.enabled = false;
            if (summonGlow != null) summonGlow.enabled = summonCore.enabled = false;
        }
        private void OnDisable() => Clear();
    }
}
