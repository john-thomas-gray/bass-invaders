using UnityEngine;
using UnityEngine.Rendering;

namespace CandyCruisers
{
    [ExecuteAlways, DefaultExecutionOrder(1100), RequireComponent(typeof(LineRenderer))]
    public sealed class PlayfieldFrame : MonoBehaviour
    {
        [SerializeField] private PlayerMovement player;
        private LineRenderer boundary;
        private LineRenderer outline;
        private readonly LineRenderer[] corners = new LineRenderer[4];
        private readonly EnemyColor[] earnedColors = new EnemyColor[EnemyPalette.Colors.Count];
        public const float SpawnPulseSeconds = 60f / GameplayMusicPlayer.DefaultBeatsPerMinute;
        public const float OuterTopY = 5.62f;
        public const float BoundaryLineWidth = .045f;
        public const float OutlineLineWidth = .018f;
        public const float CornerLineWidth = .065f;
        public const float InnerBeatGrowth = .09f;
        public const float InnerEdgePeakScale = 3f;
        private GameSession subscribedSession;
        private float spawnPulseDuration = SpawnPulseSeconds;
        private float spawnPulseRemaining;
        private SpriteMask playerClip;
        private Sprite clipSprite;
        private SpriteRenderer[] clippedSprites;
        private SpriteMaskInteraction[] originalMasks;
        private SortingGroup clipGroup;
        private bool ownsClipGroup, clipGroupWasEnabled;
        public bool SpawnPulseActive => spawnPulseRemaining > 0;
        public float SpawnPulseDuration => spawnPulseDuration;

        public void Configure(PlayerMovement controller)
        {
            if (player != controller) ReleasePlayerClip();
            player = controller;
            BindSession();
            boundary = GetComponent<LineRenderer>();
            outline = Line("Cabinet outline", OutlineLineWidth, true);
            float x = PlayerMovement.HalfWidth + .12f, y = OuterTopY, bevel = .18f;
            outline.positionCount = 8;
            outline.SetPositions(new[] {
                new Vector3(-x + bevel, -y), new Vector3(x - bevel, -y),
                new Vector3(x, -y + bevel), new Vector3(x, y - bevel),
                new Vector3(x - bevel, y), new Vector3(-x + bevel, y),
                new Vector3(-x, y - bevel), new Vector3(-x, -y + bevel)
            });
            for (int i = 0; i < corners.Length; i++)
            {
                float side = i < 2 ? -1 : 1, end = i % 2 == 0 ? -1 : 1;
                corners[i] = Line("Cabinet corner " + i, .065f, false);
                corners[i].positionCount = 3;
                corners[i].SetPositions(new[] {
                    new Vector3(side * 2.52f, end * 5.43f),
                    new Vector3(side * 2.93f, end * 5.43f),
                    new Vector3(side * 2.93f, end * 5.02f)
                });
            }
            Refresh();
        }

        private LineRenderer Line(string name, float width, bool loop)
        {
            var child = transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(LineRenderer)).transform;
                child.SetParent(transform, false);
            }
            var line = child.GetComponent<LineRenderer>();
            line.sharedMaterial = boundary.sharedMaterial;
            line.useWorldSpace = false;
            line.loop = loop;
            line.startWidth = line.endWidth = width;
            line.sortingOrder = boundary.sortingOrder;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            return line;
        }

        private void OnEnable()
        {
            BindSession();
            boundary = GetComponent<LineRenderer>();
            outline = transform.Find("Cabinet outline")?.GetComponent<LineRenderer>();
            for (int i = 0; i < corners.Length; i++)
                corners[i] = transform.Find("Cabinet corner " + i)?.GetComponent<LineRenderer>();
            Refresh();
        }

        private void OnDisable()
        {
            if (subscribedSession != null) subscribedSession.WaveSpawned -= TriggerSpawnPulse;
            subscribedSession = null;
            spawnPulseRemaining = 0;
            Refresh();
            ReleasePlayerClip(true);
        }

        private void OnDestroy() => ReleasePlayerClip();

        private void BindSession()
        {
            var session = player != null && player.Music != null ? player.Music.GetComponent<GameSession>() : null;
            if (session == subscribedSession) return;
            if (subscribedSession != null) subscribedSession.WaveSpawned -= TriggerSpawnPulse;
            subscribedSession = session;
            if (subscribedSession != null) subscribedSession.WaveSpawned += TriggerSpawnPulse;
        }

        private void LateUpdate() { BindSession(); Tick(Time.deltaTime); }
        public void TriggerSpawnPulse()
        {
            var music = player != null ? player.Music : null;
            spawnPulseDuration = music != null ? Mathf.Max(.0001f, music.BeatDuration) : SpawnPulseSeconds;
            spawnPulseRemaining = spawnPulseDuration;
            Refresh();
        }
        public void Tick(float seconds)
        {
            if (subscribedSession == null || !subscribedSession.IsPaused)
                spawnPulseRemaining = Mathf.Max(0, spawnPulseRemaining - Mathf.Max(0, seconds));
            Refresh();
        }

        public void Refresh()
        {
            var music = player != null ? player.Music : null;
            if (music != null && music.InGameplayRun && !player.CelebrationColor.HasValue)
                RefreshBeat(music.BeatPosition);
            else Refresh(Time.time);
        }

        public void RefreshBeat(float beatPosition)
        {
            if (boundary == null) return;
            if (player != null && player.ReturnColorPreview.HasValue)
            {
                ApplyTint(player.AccentColor);
                ApplyMagicPulse(beatPosition);
                return;
            }
            int phase = Mathf.FloorToInt(Mathf.Max(0, beatPosition) * 2);
            ApplyTint(player != null && player.HasFleet && phase % 2 != 0 ? player.AccentColor : Color.white);
            ApplyMagicPulse(beatPosition);
        }

        public static float InnerPulseScale(int bars, float beat, bool even, int downbeatOffset = 0,
            float basePeak = 1 + InnerBeatGrowth)
        {
            if (bars <= 0) return 1;
            int nearestBeat = Mathf.FloorToInt(beat + .5f);
            bool evenBeat = GameplayMusicPlayer.BeatInMeasure(nearestBeat, downbeatOffset) % 2 == 0;
            if (evenBeat != even) return 1;
            float pulse = GameplayMusicPlayer.BeatPulse(beat);
            return Mathf.Lerp(1, basePeak, pulse) * MagicPowerPresentation.TextGrowthScale(bars, beat);
        }

        private void ApplyMagicPulse(float beat)
        {
            int bars = isActiveAndEnabled && player != null ? player.ActiveMagicBars : 0;
            if (subscribedSession != null && subscribedSession.State != GameSession.RunState.Playing &&
                subscribedSession.State != GameSession.RunState.Refilling) bars = 0;
            int offset = player != null && player.Music != null ? player.Music.CurrentDownbeatOffsetBeats : 0;
            float edge = InnerPulseScale(bars, beat, false, offset, InnerEdgePeakScale);
            float cornerScale = InnerPulseScale(bars, beat, true, offset);
            // The inner edge swells in thickness, leaving the outer outline and playfield fixed.
            boundary.startWidth = boundary.endWidth = BoundaryLineWidth * edge;
            for (int i = 0; i < corners.Length; i++)
            {
                var corner = corners[i];
                if (corner == null) continue;
                float side = i < 2 ? -1 : 1, end = i % 2 == 0 ? -1 : 1;
                var center = new Vector3(side * 2.725f, end * 5.225f);
                corner.transform.localScale = new Vector3(cornerScale, cornerScale, 1);
                corner.transform.localPosition = center * (1 - cornerScale);
                corner.startWidth = corner.endWidth = CornerLineWidth * cornerScale;
            }
            if (Application.isPlaying || playerClip != null) RefreshPlayerClip();
        }

        public void Refresh(float time)
        {
            if (boundary == null) return;
            bool returnPreview = player != null && player.ReturnColorPreview.HasValue;
            var color = player != null ? player.AccentColor : EnemyPalette.Get(EnemyColor.Blue);
            int count = 0;
            if (!returnPreview && player != null && player.Fleet != null && !player.CelebrationColor.HasValue && player.MagicCharges > 0)
                foreach (var earned in player.Fleet.Colors)
                    if (player.HasColorClearBar(earned)) earnedColors[count++] = earned;
            if (count > 0)
            {
                float interval = Mathf.Max(.2f, .4f - .04f * (count - 1));
                int phase = Mathf.FloorToInt(Mathf.Max(0, time) / interval) % (count * 2);
                color = phase % 2 == 0 ? Color.white : EnemyPalette.Get(earnedColors[phase / 2]);
            }
            ApplyTint(color);
            // Celebration colors use this path too; color priority must not cancel size animation.
            var music = player != null ? player.Music : null;
            ApplyMagicPulse(music != null ? music.BeatPosition : time / FullSetCelebration.StepDuration);
        }

        private void ApplyTint(Color color)
        {
            boundary.startWidth = boundary.endWidth = BoundaryLineWidth;
            if (outline != null) outline.startWidth = outline.endWidth = OutlineLineWidth;
            Tint(boundary, color, .85f);
            Tint(outline, color, .5f);
            foreach (var corner in corners)
            {
                if (corner != null)
                {
                    corner.startWidth = corner.endWidth = CornerLineWidth;
                    corner.transform.localScale = Vector3.one;
                    corner.transform.localPosition = Vector3.zero;
                }
                Tint(corner, color, 1);
            }
            // Do not serialize temporary mask settings into scene snapshots on entering play mode.
            if (Application.isPlaying || playerClip != null) RefreshPlayerClip();
        }

        public void RefreshPlayerClip()
        {
            if (!isActiveAndEnabled || player == null || boundary.positionCount < 4) return;
            var art = CharacterVisuals.Ensure(player.gameObject);
            if (art.Root == null || art.Body == null) return;
            if (playerClip == null || playerClip.transform.parent != art.Root)
            {
                ReleasePlayerClip(true);
                if (clipGroup != null && clipGroup.transform != art.Root) ReleaseClipGroup();
                if (clipGroup == null)
                {
                    clipGroup = art.Root.GetComponent<SortingGroup>();
                    ownsClipGroup = clipGroup == null;
                    if (ownsClipGroup)
                    {
                        clipGroup = art.Root.gameObject.AddComponent<SortingGroup>();
                        clipGroup.hideFlags = HideFlags.DontSave;
                        clipGroup.sortingLayerID = art.Body.sortingLayerID;
                        clipGroup.sortingOrder = art.Body.sortingOrder;
                    }
                    clipGroupWasEnabled = clipGroup.enabled;
                }
                clipGroup.enabled = true;
                var root = new GameObject("Player playfield clip", typeof(SpriteMask)) { hideFlags = HideFlags.DontSave };
                root.transform.SetParent(art.Root, false);
                playerClip = root.GetComponent<SpriteMask>();
                var texture = Texture2D.whiteTexture;
                clipSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 1);
                clipSprite.hideFlags = HideFlags.DontSave;
                playerClip.sprite = clipSprite;
                clippedSprites = art.Root.GetComponentsInChildren<SpriteRenderer>(true);
                originalMasks = new SpriteMaskInteraction[clippedSprites.Length];
                for (int i = 0; i < clippedSprites.Length; i++)
                {
                    originalMasks[i] = clippedSprites[i].maskInteraction;
                    clippedSprites[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
                SortingGroup.UpdateAllSortingGroups();
            }

            var bounds = new Bounds(boundary.GetPosition(0), Vector3.zero);
            for (int i = 1; i < boundary.positionCount; i++) bounds.Encapsulate(boundary.GetPosition(i));
            Vector3 center = boundary.useWorldSpace ? bounds.center : boundary.transform.TransformPoint(bounds.center);
            Vector3 size = boundary.useWorldSpace ? bounds.size : Vector3.Scale(bounds.size, boundary.transform.lossyScale);
            // Stop at the rim's inner edge, including its increasing thickness during a crunch.
            size.x = Mathf.Max(.001f, Mathf.Abs(size.x) - boundary.startWidth);
            size.y = Mathf.Max(.001f, Mathf.Abs(size.y) - boundary.startWidth);
            playerClip.transform.SetPositionAndRotation(center, boundary.useWorldSpace ? Quaternion.identity : boundary.transform.rotation);
            Vector3 parentScale = art.Root.lossyScale;
            playerClip.transform.localScale = new Vector3(size.x / (clipSprite.bounds.size.x * Mathf.Max(.0001f, Mathf.Abs(parentScale.x))),
                size.y / (clipSprite.bounds.size.y * Mathf.Max(.0001f, Mathf.Abs(parentScale.y))), 1);
        }

        private void ReleasePlayerClip(bool keepGroup = false)
        {
            if (clippedSprites != null)
                for (int i = 0; i < clippedSprites.Length; i++)
                    if (clippedSprites[i] != null) clippedSprites[i].maskInteraction = originalMasks[i];
            clippedSprites = null;
            originalMasks = null;
            if (playerClip != null)
            {
                playerClip.enabled = false;
                Release(playerClip.gameObject);
            }
            playerClip = null;
            if (clipSprite != null) Release(clipSprite);
            clipSprite = null;
            if (clipGroup != null) clipGroup.enabled = !ownsClipGroup && clipGroupWasEnabled;
            // Keep the disabled group reusable until destruction; Unity defers Destroy until the frame ends.
            if (!keepGroup) ReleaseClipGroup();
        }

        private void ReleaseClipGroup()
        {
            if (clipGroup != null && ownsClipGroup) Release(clipGroup);
            clipGroup = null;
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private static void Tint(LineRenderer line, Color color, float alpha)
        {
            if (line == null) return;
            color.a = alpha;
            line.startColor = line.endColor = color;
        }
    }
}
