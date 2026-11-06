using UnityEngine;

namespace CandyCruisers
{
    // Deform the placeholder silhouette, never the actor transform or its collision bounds.
    [DefaultExecutionOrder(100)]
    public sealed class PlayerSlimeVisual : MonoBehaviour
    {
        private SpriteRenderer body;
        private Transform[] face;
        private Vector3[] facePositions;
        private float[] faceHeights;
        private Vector3[] pupilOffsets;
        private Transform visualRoot;
        private bool hasPupils;
        private Vector2 gazeDirection = Vector2.up;
        public EnemyMissile GazeTarget { get; private set; }
        public const float GazeResponse = 10;
        private readonly Color32[] pixels = new Color32[128 * 128];
        private float lastDrawnLean = float.NaN;
        private Vector3 previousPosition;
        private float lean, velocity;
        private Sprite sprite;
        private Texture2D texture;
        public Sprite PlaceholderSprite => sprite;

        public Vector3 DefaultPupilPosition(Transform pupil)
        {
            if (face != null)
                for (int i = 0; i < face.Length; i++)
                    if (face[i] == pupil) return pupil.parent.TransformPoint(facePositions[i]);
            return pupil.position;
        }

        public Vector3 FaceDisplacement(Transform piece, Transform relativeTo)
        {
            if (face == null) return Vector3.zero;
            for (int i = 0; i < face.Length; i++)
                if (face[i] != null && (piece == face[i] || piece.IsChildOf(face[i])))
                    return relativeTo.InverseTransformVector(face[i].parent.TransformVector(face[i].localPosition - facePositions[i]));
            return Vector3.zero;
        }

        public float FaceHeight(Transform piece)
        {
            if (face != null)
                for (int i = 0; i < face.Length; i++)
                    if (face[i] != null && (piece == face[i] || piece.IsChildOf(face[i]))) return faceHeights[i];
            return 0;
        }

        public static float OpposingLean(float horizontalSpeed) => Mathf.Clamp(horizontalSpeed * 2.5f, -18, 18);
        public static float JellyOffset(float height, float leanDegrees) =>
            -Mathf.Clamp(leanDegrees, -24, 24) / 18 * .28f * Mathf.Pow(Mathf.Clamp01(height), 2);

        public void Configure(CharacterVisuals art)
        {
            if (art.Body == null || art.Body.sprite == null || art.Root == transform) return;
            var original = art.Body.sprite;
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Slime dome", filterMode = FilterMode.Bilinear };
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f,
                size / original.bounds.size.x, 0, SpriteMeshType.FullRect);
            sprite.name = "Flat-bottom slime placeholder";
            art.Body.sprite = sprite;
            body = art.Body;
            var features = new System.Collections.Generic.List<Transform>();
            foreach (var part in art.Root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (part == body || art.GetComponent<PlayerShockVisual>()?.OwnsSprite(part) == true) continue;
                bool followsAnotherFeature = false;
                for (var parent = part.transform.parent; parent != null && parent != art.Root; parent = parent.parent)
                    if (parent.GetComponent<SpriteRenderer>() != null && parent != body.transform)
                        followsAnotherFeature = true;
                if (!followsAnotherFeature) features.Add(part.transform);
            }
            face = features.ToArray();
            facePositions = new Vector3[face.Length];
            faceHeights = new float[face.Length];
            pupilOffsets = new Vector3[face.Length];
            visualRoot = art.Root;
            gazeDirection = visualRoot.up;
            for (int i = 0; i < face.Length; i++)
            {
                facePositions[i] = face[i].localPosition;
                faceHeights[i] = (body.transform.InverseTransformPoint(face[i].position).y - sprite.bounds.min.y) / sprite.bounds.size.y;
                if (face[i].name != "Pupil") continue;
                Transform eye = null;
                float nearest = float.PositiveInfinity;
                foreach (var feature in face)
                {
                    float distance = (feature.position - face[i].position).sqrMagnitude;
                    if (feature.name != "Eye" || distance >= nearest) continue;
                    eye = feature; nearest = distance;
                }
                if (eye == null) continue;
                pupilOffsets[i] = face[i].localPosition - face[i].parent.InverseTransformPoint(eye.position);
                hasPupils = true;
            }
            RenderJelly();
            previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (body == null || Time.deltaTime <= 0) return;
            float dx = transform.position.x - previousPosition.x;
            previousPosition = transform.position;
            TickMotion(dx, Time.deltaTime);
            TickGaze(Time.deltaTime);
        }

        public void TickGaze(float seconds)
        {
            if (body == null || !hasPupils || seconds <= 0) return;
            var nearest = CanTrackMissile(GazeTarget) ? GazeTarget : null;
            float distance = nearest != null ? ((Vector2)(nearest.transform.position - transform.position)).sqrMagnitude : float.PositiveInfinity;
            foreach (var missile in FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
            {
                if (!CanTrackMissile(missile)) continue;
                float candidate = ((Vector2)(missile.transform.position - transform.position)).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = missile; distance = candidate;
            }
            GazeTarget = nearest;
            Vector2 target = nearest != null ? (Vector2)(nearest.transform.position - EyeCenter()) : (Vector2)visualRoot.up;
            if (target.sqrMagnitude < .000001f) target = visualRoot.up;
            gazeDirection = Vector2.Lerp(gazeDirection, target.normalized, 1 - Mathf.Exp(-GazeResponse * seconds));
            RenderJelly();
        }

        private static bool CanTrackMissile(EnemyMissile missile)
        {
            if (missile == null || !missile.isActiveAndEnabled || missile.Finished) return false;
            var position = missile.transform.position;
            // The visible rim ends at +/-5.5, before the missiles' delayed cleanup boundary.
            return Mathf.Abs(position.x) <= PlayerMovement.HalfWidth && Mathf.Abs(position.y) <= 5.5f;
        }

        private Vector3 EyeCenter()
        {
            var center = Vector3.zero;
            int count = 0;
            foreach (var feature in face)
            {
                if (feature == null || feature.name != "Eye") continue;
                center += feature.position;
                count++;
            }
            return count > 0 ? center / count : transform.position;
        }

        public void TickMotion(float dx, float seconds)
        {
            if (body == null || seconds <= 0) return;
            // Wrapping and respawning are teleports, not acceleration impulses.
            float target = Mathf.Abs(dx) < .8f ? OpposingLean(dx / seconds) : 0;
            float dt = Mathf.Min(seconds, .035f);
            velocity += ((target - lean) * 110 - velocity * 12) * dt;
            lean += velocity * dt;
            RenderJelly();
        }

        private void RenderJelly()
        {
            if (float.IsNaN(lastDrawnLean) || Mathf.Abs(lastDrawnLean - lean) >= .002f)
            {
                lastDrawnLean = lean;
                PaintDome(pixels, 128, lean);
                texture.SetPixels32(pixels);
                texture.Apply(false);
            }
            for (int i = 0; i < face.Length; i++)
            {
                if (face[i] == null) continue;
                var displacement = body.transform.TransformVector(Vector3.right *
                    (JellyOffset(faceHeights[i], lean) * sprite.bounds.size.x * .5f));
                face[i].localPosition = facePositions[i] + face[i].parent.InverseTransformVector(displacement);
                if (pupilOffsets[i].sqrMagnitude > 0)
                    face[i].localPosition += face[i].parent.InverseTransformDirection(gazeDirection) * pupilOffsets[i].magnitude - pupilOffsets[i];
            }
        }

        public static void PaintDome(Color32[] pixels, int size, float lean)
        {
            for (int y = 0; y < size; y++)
            {
                float height = (float)y / (size - 1);
                float halfWidth = Mathf.Sqrt(Mathf.Max(0, 1 - height * height)) *
                    Mathf.Lerp(.91f, 1, Mathf.Clamp01(height * 12));
                float offset = JellyOffset(height, lean);
                for (int x = 0; x < size; x++)
                {
                    float px = (x + .5f) / size * 2 - 1;
                    byte alpha = (byte)Mathf.RoundToInt(255 * Mathf.Clamp01((halfWidth - Mathf.Abs(px - offset)) * size / 2));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                if (sprite != null) Destroy(sprite);
                if (texture != null) Destroy(texture);
            }
            else
            {
                if (sprite != null) DestroyImmediate(sprite);
                if (texture != null) DestroyImmediate(texture);
            }
        }
    }
}
