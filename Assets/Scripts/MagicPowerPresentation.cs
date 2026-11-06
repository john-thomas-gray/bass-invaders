using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Layer size onto the existing animations without changing gameplay transforms.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class MagicPowerPresentation : MonoBehaviour
    {
        public const float InitialTextGrowth = .10f;
        public const float TextGrowthPerAdditionalBar = .05f;
        public const float PoweredPupilScale = 1.4f;
        public const float PupilTransitionSeconds = .2f;
        private GameSession session;
        private PlayerMovement player;
        private bool rendering;
        private float pupilProgress;
        public float PupilScale => Mathf.Lerp(1, PoweredPupilScale, Mathf.SmoothStep(0, 1, pupilProgress));
        private readonly List<Pose> poses = new List<Pose>();
        private readonly List<TextPiece> texts = new List<TextPiece>();
        private struct Pose
        {
            public Transform transform;
            public Vector3 position, scale;
        }
        private struct TextPiece
        {
            public Renderer renderer;
            public Vector3 center, scale;
            public int depth;
        }

        public static float TextGrowthScale(int activeBars, float beat, bool offbeat = false)
        {
            int bars = Mathf.Clamp(activeBars, 0, EnemyPalette.Colors.Count);
            float growth = bars > 0 ? InitialTextGrowth + (bars - 1) * TextGrowthPerAdditionalBar : 0;
            return 1 + growth * GameplayMusicPlayer.BeatPulse(beat, offbeat);
        }

        public static Matrix4x4 ElementMatrix(Matrix4x4 basis, Rect rect, float scale)
        {
            var center = (Vector3)rect.center;
            return basis * Matrix4x4.Translate(center) * Matrix4x4.Scale(new Vector3(scale, scale, 1)) *
                Matrix4x4.Translate(-center);
        }

        public static Vector2 PeakTextInset(Rect text, Rect safe, Vector2 anchor, float originalPeak)
        {
            var pivot = (Vector3)anchor;
            var original = Matrix4x4.Translate(pivot) * Matrix4x4.Scale(new Vector3(originalPeak, originalPeak, 1)) *
                Matrix4x4.Translate(-pivot);
            var peak = ElementMatrix(original, text, TextGrowthScale(EnemyPalette.Colors.Count, 0));
            var min = peak.MultiplyPoint3x4(text.min);
            var max = peak.MultiplyPoint3x4(text.max);
            // Reserve the maximum text extent at every beat and charge count. This
            // fixed inset avoids edge clipping without changing the pulse's movement.
            return new Vector2(max.x - min.x <= safe.width ? Mathf.Clamp(0, safe.xMin - min.x, safe.xMax - max.x) : 0,
                max.y - min.y <= safe.height ? Mathf.Clamp(0, safe.yMin - min.y, safe.yMax - max.y) : 0);
        }

        public void Configure(GameSession controller, PlayerMovement character)
        {
            RestorePresentation();
            session = controller;
            player = character;
            pupilProgress = 0;
        }

        private void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (session == null || player == null || !player.Alive ||
                session.State != GameSession.RunState.Playing && session.State != GameSession.RunState.Refilling)
            { pupilProgress = 0; return; }
            if (session.IsPaused) return;
            pupilProgress = Mathf.MoveTowards(pupilProgress, player.MagicPowerActive ? 1 : 0,
                Mathf.Max(0, seconds) / PupilTransitionSeconds);
        }

        private void OnPreCull() => ApplyPresentation();
        private void OnPostRender() => RestorePresentation();

        public void ApplyPresentation()
        {
            RestorePresentation();
            if (session == null || player == null) return;
            float growth = session.MagicTextGrowthScale;
            float playerGrowth = session.MagicGrowthScale(true);
            if (Mathf.Approximately(growth, 1) && Mathf.Approximately(playerGrowth, 1) &&
                Mathf.Approximately(PupilScale, 1)) return;
            rendering = true;
            var playerArt = player.GetComponent<CharacterVisuals>();
            GrowActor(playerArt, playerGrowth);
            if (!Mathf.Approximately(growth, 1))
            {
                session.GetComponentsInChildren(false, actors);
                foreach (var actor in actors)
                    if (actor.GetComponent<GridEnemy>() != null) GrowActor(actor, growth);
                foreach (var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                {
                    var renderer = text.GetComponent<Renderer>();
                    if (renderer == null || !renderer.enabled || !text.gameObject.activeInHierarchy) continue;
                    var owner = text.GetComponentInParent<CharacterVisuals>();
                    if (owner != null && owner.Root != null && text.transform.IsChildOf(owner.Root)) continue;
                    Remember(text.transform);
                    var piece = new TextPiece { renderer = renderer, center = renderer.bounds.center, scale = text.transform.lossyScale };
                    for (var parent = text.transform.parent; parent != null; parent = parent.parent) piece.depth++;
                    texts.Add(piece);
                }
                texts.Sort((a, b) => a.depth.CompareTo(b.depth));
                foreach (var text in texts) Grow(text.renderer, text.center, text.scale, growth);
            }
            if (playerArt == null || playerArt.Root == null) return;
            foreach (var feature in playerArt.Root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (feature.name != "Pupil") continue;
                Remember(feature.transform);
                Grow(feature, feature.bounds.center, feature.transform.lossyScale, PupilScale);
            }
        }

        private readonly List<CharacterVisuals> actors = new List<CharacterVisuals>();
        private readonly List<SpriteMask> clipMasks = new List<SpriteMask>();
        private readonly List<ClipPose> clipPoses = new List<ClipPose>();
        private struct ClipPose { public Transform transform; public Vector3 position, scale; }
        private void GrowActor(CharacterVisuals art, float growth)
        {
            if (art == null || art.Root == null || art.Root == art.transform || art.Body == null ||
                Mathf.Approximately(growth, 1)) return;
            Remember(art.Root);
            var center = art.Body.bounds.center;
            art.Root.GetComponentsInChildren(false, clipMasks);
            clipPoses.Clear();
            foreach (var mask in clipMasks)
            {
                Remember(mask.transform);
                clipPoses.Add(new ClipPose { transform = mask.transform, position = mask.transform.position,
                    scale = mask.transform.lossyScale });
            }
            art.Root.localScale = Vector3.Scale(art.Root.localScale, new Vector3(growth, growth, 1));
            art.Root.position += center - art.Body.bounds.center;
            // The player's clipping window stays fixed while the artwork grows inside it.
            foreach (var mask in clipPoses)
            {
                mask.transform.position = mask.position;
                mask.transform.localScale = LocalScale(mask.transform.parent, mask.scale);
            }
        }

        private static void Grow(Renderer renderer, Vector3 center, Vector3 scale, float factor)
        {
            var target = renderer.transform;
            target.localScale = LocalScale(target.parent, Vector3.Scale(scale, new Vector3(factor, factor, 1)));
            target.position += center - renderer.bounds.center;
        }

        private static Vector3 LocalScale(Transform parent, Vector3 world)
        {
            var scale = parent != null ? parent.lossyScale : Vector3.one;
            return new Vector3(Mathf.Abs(scale.x) > .000001f ? world.x / scale.x : 0,
                Mathf.Abs(scale.y) > .000001f ? world.y / scale.y : 0,
                Mathf.Abs(scale.z) > .000001f ? world.z / scale.z : 0);
        }

        private void Remember(Transform target) => poses.Add(new Pose
        { transform = target, position = target.localPosition, scale = target.localScale });

        public void RestorePresentation()
        {
            if (!rendering) return;
            texts.Clear();
            for (int i = poses.Count - 1; i >= 0; i--)
            {
                var pose = poses[i];
                if (pose.transform == null) continue;
                pose.transform.localPosition = pose.position;
                pose.transform.localScale = pose.scale;
            }
            poses.Clear();
            rendering = false;
        }

        private void OnDisable() { RestorePresentation(); pupilProgress = 0; }
    }
}
