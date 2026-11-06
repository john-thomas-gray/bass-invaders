using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Detached artwork only. The session supplies elapsed time on every platform.
    [DefaultExecutionOrder(1100)]
    public sealed class PlayerMeltDeath : MonoBehaviour
    {
        public const float FinalTurnSeconds = .8f;
        public const float CrossDiameterScale = .68f * .7f;
        public const float MeltStartSeconds = 1.2f;
        public const float WhiteSeconds = 1.9f;
        public const float LinePeakSeconds = 2.25f;
        public const float PointSeconds = 2.62f;
        public const float FadeStartSeconds = 2.65f;
        public const float Duration = 2.8f;

        private sealed class Piece
        {
            public SpriteRenderer renderer, cross;
            public Vector3 position, scale, restPosition, morphStartScale, crossScale;
            public Vector3 spinCenter, spinOffset;
            public float spinDirection;
            public Quaternion rotation;
            public Color color;
            public bool body, pupil;
        }

        private readonly List<Piece> pieces = new List<Piece>();
        private SpriteRenderer puddle;
        private Sprite crossSprite, puddleSprite;
        private Texture2D crossTexture, puddleTexture;
        private float floor, center, width, height;
        private Color bodyTint;
        private const int TextureSize = 96;
        private const float MorphCircleRadius = .85f;
        private readonly Color32[] morphPixels = new Color32[TextureSize * TextureSize];
        private readonly Color32[] puddlePixels = new Color32[TextureSize * TextureSize];
        private float lastMorph = float.NaN;
        private float lastPuddleMorph = float.NaN;
        private float lastPointMorph = float.NaN;
        private float presentedAge;

        public static PlayerMeltDeath Create(SpriteRenderer source, Transform parent)
        {
            var root = new GameObject("Player dizzy eyes and melt");
            root.transform.SetParent(parent, false);
            var melt = root.AddComponent<PlayerMeltDeath>();
            var art = source.GetComponentInParent<CharacterVisuals>();
            var sources = art != null ? art.Root.GetComponentsInChildren<SpriteRenderer>() : new[] { source };
            melt.floor = source.bounds.min.y - root.transform.position.y;
            melt.center = source.bounds.center.x - root.transform.position.x;
            melt.width = source.bounds.size.x;
            melt.height = source.bounds.size.y;
            melt.bodyTint = source.color;
            melt.crossSprite = melt.MakeSprite(true, out melt.crossTexture);
            melt.puddleSprite = melt.MakeSprite(false, out melt.puddleTexture);
            foreach (var original in sources)
            {
                if (!original.enabled || original.sprite == null) continue;
                var copy = new GameObject(original.name + " melt snapshot", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                copy.transform.localScale = original.transform.lossyScale;
                copy.sprite = original.sprite;
                copy.sharedMaterial = original.sharedMaterial;
                copy.sortingLayerID = original.sortingLayerID;
                copy.sortingOrder = original.sortingOrder + 20;
                copy.flipX = original.flipX;
                copy.flipY = original.flipY;
                var piece = new Piece
                {
                    renderer = copy, position = copy.transform.localPosition, scale = copy.transform.localScale,
                    rotation = copy.transform.localRotation, color = original.color,
                    body = original == source, pupil = original.name.Contains("Pupil")
                };
                if (piece.pupil)
                {
                    var eye = PlayerDeathEyeMotion.FindEye(original, sources);
                    piece.restPosition = root.transform.InverseTransformPoint(PlayerDeathEyeMotion.RestPosition(original, eye));
                    piece.spinCenter = (piece.position + piece.restPosition) * .5f;
                    piece.spinOffset = piece.position - piece.spinCenter;
                    piece.spinDirection = original.bounds.center.x < source.bounds.center.x ? 1 : -1;
                    float eyeSize = eye != null ? Mathf.Min(eye.bounds.size.x, eye.bounds.size.y) : melt.width * .28f;
                    piece.morphStartScale = new Vector3(original.bounds.size.x / MorphCircleRadius,
                        original.bounds.size.y / MorphCircleRadius, 1);
                    piece.crossScale = new Vector3(eyeSize * CrossDiameterScale, eyeSize * CrossDiameterScale, 1);
                    piece.cross = new GameObject("X eye", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                    piece.cross.transform.SetParent(root.transform, false);
                    piece.cross.sprite = melt.crossSprite;
                    piece.cross.sharedMaterial = original.sharedMaterial;
                    piece.cross.sortingLayerID = copy.sortingLayerID;
                    piece.cross.sortingOrder = copy.sortingOrder + 1;
                    piece.cross.transform.localScale = piece.morphStartScale;
                }
                melt.pieces.Add(piece);
            }
            melt.puddle = new GameObject("Melted puddle", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            melt.puddle.transform.SetParent(root.transform, false);
            melt.puddle.sprite = melt.puddleSprite;
            melt.puddle.sharedMaterial = source.sharedMaterial;
            melt.puddle.color = source.color;
            melt.puddle.sortingLayerID = source.sortingLayerID;
            melt.puddle.sortingOrder = source.sortingOrder + 19;
            melt.Present(0);
            return melt;
        }

        public void Present(float age)
        {
            age = Mathf.Max(0, age);
            presentedAge = age;
            float turnTime = Mathf.Clamp01(age / FinalTurnSeconds);
            float turning = turnTime * (2 - turnTime);
            float cross = Mathf.SmoothStep(0, 1, turnTime);
            PaintEyeMorph(cross);
            float melt = Mathf.SmoothStep(0, 1, (age - MeltStartSeconds) / (LinePeakSeconds - MeltStartSeconds));
            float whitening = Mathf.SmoothStep(0, 1, (age - MeltStartSeconds) / (WhiteSeconds - MeltStartSeconds));
            float fade = 1 - Mathf.SmoothStep(0, 1, (age - FadeStartSeconds) / (Duration - FadeStartSeconds));
            float collapse = Mathf.SmoothStep(0, 1, (age - LinePeakSeconds) / (PointSeconds - LinePeakSeconds));
            ViewDimensions(out float screenWidth, out float pixelHeight);
            float lineHeight = Mathf.Min(height, Mathf.Max(height * .025f, pixelHeight * 1.4f));
            float pointSize = Mathf.Max(height * .04f, pixelHeight * 2);
            // One continuous curve owns width, height and the silhouette from the first smoosh to the screen edges.
            float silhouetteWidth = GeometricLerp(GeometricLerp(width, screenWidth, melt), pointSize, collapse);
            float silhouetteHeight = GeometricLerp(GeometricLerp(height, lineHeight, melt), pointSize, collapse);
            float squash = silhouetteHeight / Mathf.Max(.0001f, height);
            foreach (var piece in pieces)
            {
                var position = piece.position;
                if (piece.pupil)
                {
                    // A decelerating half-turn keeps the incoming tangent and lands exactly on the default gaze.
                    position = piece.spinCenter + Quaternion.Euler(0, 0, piece.spinDirection * 180 * turning) * piece.spinOffset;
                }
                position.x = center + (position.x - center) * (1 + melt * (piece.body ? 0 : .25f));
                position.y = floor + (position.y - floor) * squash;
                piece.renderer.transform.localPosition = position;
                piece.renderer.transform.localScale = Vector3.Scale(piece.scale,
                    new Vector3(1 + melt * .08f, squash, 1));
                piece.renderer.transform.localRotation = piece.rotation;
                float alpha = fade * (piece.body ? 1 : 1 - Mathf.SmoothStep(0, 1, melt / .55f));
                piece.renderer.color = WithAlpha(piece.body ? Color.Lerp(piece.color, Color.white, whitening) : piece.color, alpha);
                piece.renderer.enabled = age < Duration && piece.renderer.color.a > 0 &&
                    (!piece.pupil || age == 0) && (!piece.body || age < MeltStartSeconds);
                if (piece.cross == null) continue;
                piece.cross.transform.localPosition = position;
                piece.cross.transform.localRotation = piece.rotation * Quaternion.Euler(0, 0, piece.spinDirection * 180 * turning);
                piece.cross.transform.localScale = Vector3.Scale(Vector3.Lerp(piece.morphStartScale, piece.crossScale, cross),
                    new Vector3(1 + melt * .08f, squash, 1));
                piece.cross.color = WithAlpha(piece.color, alpha);
                piece.cross.enabled = age > 0 && age < Duration && piece.cross.color.a > 0;
            }
            PaintPuddleMorph(melt, collapse);
            puddle.enabled = age >= MeltStartSeconds && age < Duration;
            puddle.transform.localPosition = new Vector3(center, floor + silhouetteHeight * .5f, 0);
            puddle.transform.localScale = new Vector3(silhouetteWidth, silhouetteHeight, 1);
            var tint = Color.Lerp(bodyTint, Color.white, whitening);
            tint.a *= fade;
            puddle.color = tint;
        }

        // Framing can change after the session ticks, including on a phone rotation.
        private void LateUpdate()
        {
            if (presentedAge >= MeltStartSeconds) Present(presentedAge);
        }

        private static float GeometricLerp(float from, float to, float progress)
        {
            if (progress <= 0) return from;
            if (progress >= 1) return to;
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(.0001f, from)), Mathf.Log(Mathf.Max(.0001f, to)), progress));
        }

        private void ViewDimensions(out float lineWidth, out float pixelHeight)
        {
            lineWidth = width * 5.5f;
            pixelHeight = 0;
            var camera = Camera.main;
            if (camera == null) return;
            var viewport = camera.WorldToViewportPoint(transform.TransformPoint(new Vector3(center, floor, 0)));
            var left = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(0, viewport.y, viewport.z)));
            var right = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(1, viewport.y, viewport.z)));
            // The rounded caps sit outside the viewport so opaque white reaches both physical screen edges.
            lineWidth = 2 * Mathf.Max(Mathf.Abs(left.x - center), Mathf.Abs(right.x - center)) * 1.06f / .98f;
            var bottom = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(viewport.x, 0, viewport.z)));
            var top = transform.InverseTransformPoint(camera.ViewportToWorldPoint(new Vector3(viewport.x, 1, viewport.z)));
            pixelHeight = Mathf.Abs(top.y - bottom.y) / Mathf.Max(1, camera.pixelHeight);
        }

        private static Color WithAlpha(Color color, float opacity)
        { color.a *= opacity; return color; }

        private void PaintEyeMorph(float progress)
        {
            if (!float.IsNaN(lastMorph) && Mathf.Abs(lastMorph - progress) < .001f) return;
            lastMorph = progress;
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float px = (x + .5f) / TextureSize * 2 - 1, py = (y + .5f) / TextureSize * 2 - 1;
                float circleEdge = MorphCircleRadius - Mathf.Sqrt(px * px + py * py);
                float crossEdge = Mathf.Min(.28f - Mathf.Min(Mathf.Abs(px - py), Mathf.Abs(px + py)),
                    .78f - Mathf.Max(Mathf.Abs(px), Mathf.Abs(py)));
                float edge = Mathf.Lerp(circleEdge, crossEdge, progress);
                morphPixels[y * TextureSize + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(edge * TextureSize * .5f)));
            }
            crossTexture.SetPixels32(morphPixels);
            crossTexture.Apply(false);
        }

        private void PaintPuddleMorph(float progress, float point)
        {
            if (!float.IsNaN(lastPuddleMorph) && Mathf.Abs(lastPuddleMorph - progress) < .001f &&
                Mathf.Abs(lastPointMorph - point) < .001f) return;
            lastPuddleMorph = progress;
            lastPointMorph = point;
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float px = (x + .5f) / TextureSize * 2 - 1, py = (y + .5f) / TextureSize * 2 - 1;
                float h = (py + 1) * .5f;
                float dome = Mathf.Sqrt(Mathf.Max(0, 1 - h * h)) * Mathf.Lerp(.91f, 1, Mathf.Clamp01(h * 12)) - Mathf.Abs(px);
                float radius = Mathf.Sqrt(px * px + py * py);
                float angle = Mathf.Atan2(py, px);
                float blob = .85f + .07f * Mathf.Sin(angle * 3 + .8f) + .04f * Mathf.Cos(angle * 5) - radius;
                float qx = Mathf.Abs(px) - .8f, qy = Mathf.Abs(py) - .68f;
                float line = .18f - Mathf.Sqrt(Mathf.Max(0, qx) * Mathf.Max(0, qx) + Mathf.Max(0, qy) * Mathf.Max(0, qy))
                    - Mathf.Min(Mathf.Max(qx, qy), 0);
                float edge = Mathf.Lerp(dome, blob, Mathf.SmoothStep(0, 1, progress / .6f));
                edge = Mathf.Lerp(edge, line, Mathf.SmoothStep(0, 1, (progress - .3f) / .7f));
                edge = Mathf.Lerp(edge, .86f - radius, point);
                puddlePixels[y * TextureSize + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(edge * TextureSize * .5f)));
            }
            puddleTexture.SetPixels32(puddlePixels);
            puddleTexture.Apply(false);
        }

        private Sprite MakeSprite(bool cross, out Texture2D texture)
        {
            const int size = 96;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = cross ? "Death X eye" : "Death puddle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x + .5f) / size * 2 - 1, py = (y + .5f) / size * 2 - 1;
                float edge;
                if (cross)
                    edge = Mathf.Min(.28f - Mathf.Min(Mathf.Abs(px - py), Mathf.Abs(px + py)),
                        .78f - Mathf.Max(Mathf.Abs(px), Mathf.Abs(py)));
                else
                {
                    float angle = Mathf.Atan2(py, px);
                    float rim = .85f + .07f * Mathf.Sin(angle * 3 + .8f) + .04f * Mathf.Cos(angle * 5);
                    edge = rim - Mathf.Sqrt(px * px + py * py);
                }
                pixels[y * size + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(edge * size * .5f)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
        }

        private void OnDestroy()
        {
            foreach (var asset in new Object[] { crossSprite, puddleSprite, crossTexture, puddleTexture })
            {
                if (asset == null) continue;
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
        }
    }
}
