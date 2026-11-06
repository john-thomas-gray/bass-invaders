using UnityEngine;
using UnityEngine.Rendering;

namespace CandyCruisers
{
    // Covers the world, while the detached player-death artwork stays above it.
    [DefaultExecutionOrder(1000)]
    public sealed class GameOverBlackout : MonoBehaviour
    {
        public const int CoverOrder = 30000;
        private SpriteRenderer cover;
        private Sprite sprite;
        private SortingGroup heldPlayer;
        private Transform heldArtwork;
        private bool ownsPlayerGroup, playerGroupEnabled;
        private int playerSortingLayer, playerSortingOrder;
        private Vector3 playerLocalPosition, playerLocalCenter;
        private PlayerDeathEyeMotion eyeMotion;
        private float riseAge;

        public static GameOverBlackout Create(Transform owner, PresentationCue death)
        {
            var root = new GameObject("Game-over blackout");
            root.transform.SetParent(owner, false);
            var blackout = root.AddComponent<GameOverBlackout>();
            blackout.cover = root.AddComponent<SpriteRenderer>();
            var texture = Texture2D.whiteTexture;
            blackout.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 1);
            blackout.cover.sprite = blackout.sprite;
            blackout.cover.sortingOrder = CoverOrder;
            blackout.ShowDeath(death);
            blackout.Present(0);
            return blackout;
        }

        public void HoldPlayer(Transform artwork, float risingSeconds = 2)
        {
            ReleasePlayer();
            if (artwork == null) return;
            heldArtwork = artwork;
            playerLocalPosition = artwork.localPosition;
            playerLocalCenter = VisualCenter(artwork);
            eyeMotion = new PlayerDeathEyeMotion(artwork, risingSeconds);
            heldPlayer = artwork.GetComponent<SortingGroup>();
            ownsPlayerGroup = heldPlayer == null;
            if (ownsPlayerGroup) heldPlayer = artwork.gameObject.AddComponent<SortingGroup>();
            playerSortingLayer = heldPlayer.sortingLayerID;
            playerSortingOrder = heldPlayer.sortingOrder;
            playerGroupEnabled = heldPlayer.enabled;
            PlaceAboveCover(heldPlayer);
        }

        public void ShowDeath(PresentationCue death)
        {
            ReleasePlayer();
            if (death != null)
            {
                var group = death.GetComponent<SortingGroup>();
                if (group == null) group = death.gameObject.AddComponent<SortingGroup>();
                PlaceAboveCover(group);
            }
        }

        private void PlaceAboveCover(SortingGroup group)
        {
            group.enabled = true;
            group.sortingLayerID = cover.sortingLayerID;
            group.sortingOrder = CoverOrder + 1;
            // Apply ordering before the creation frame can render the blackout.
            SortingGroup.UpdateAllSortingGroups();
        }

        private void ReleasePlayer()
        {
            eyeMotion?.Restore();
            eyeMotion = null;
            if (heldPlayer == null) return;
            heldPlayer.sortingLayerID = playerSortingLayer;
            heldPlayer.sortingOrder = playerSortingOrder;
            heldPlayer.enabled = !ownsPlayerGroup && playerGroupEnabled;
            if (ownsPlayerGroup)
            {
                if (Application.isPlaying) Destroy(heldPlayer);
                else DestroyImmediate(heldPlayer);
            }
            if (heldArtwork != null) heldArtwork.localPosition = playerLocalPosition;
            heldArtwork = null;
            heldPlayer = null;
            SortingGroup.UpdateAllSortingGroups();
        }

        public void Present(float opacity, float risingSeconds = 0)
        {
            riseAge = Mathf.Max(0, risingSeconds);
            cover.color = new Color(0, 0, 0, Mathf.Clamp01(opacity));
            cover.enabled = opacity > 0;
            MoveHeldPlayer(opacity);
            LateUpdate();
        }

        private void MoveHeldPlayer(float opacity)
        {
            var camera = Camera.main;
            if (heldArtwork == null || camera == null) return;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(opacity));
            var start = heldArtwork.parent != null ? heldArtwork.parent.TransformPoint(playerLocalPosition) : playerLocalPosition;
            var currentCenterOffset = heldArtwork.TransformPoint(playerLocalCenter) - heldArtwork.position;
            float depth = camera.WorldToViewportPoint(start + currentCenterOffset).z;
            var center = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth));
            heldArtwork.position = Vector3.Lerp(start, center - currentCenterOffset, t);
        }

        private static Vector3 VisualCenter(Transform artwork)
        {
            var renderers = artwork.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) return Vector3.zero;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return artwork.InverseTransformPoint(bounds.center);
        }

        private void LateUpdate()
        {
            eyeMotion?.Present(riseAge);
            var camera = Camera.main;
            if (cover == null || camera == null) return;
            float depth = camera.nearClipPlane + 1;
            var lower = camera.ViewportToWorldPoint(new Vector3(0, 0, depth));
            var upper = camera.ViewportToWorldPoint(new Vector3(1, 1, depth));
            transform.SetPositionAndRotation((lower + upper) * .5f, camera.transform.rotation);
            var diagonal = camera.transform.InverseTransformVector(upper - lower);
            var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(Mathf.Abs(diagonal.x / parentScale.x) / sprite.bounds.size.x,
                Mathf.Abs(diagonal.y / parentScale.y) / sprite.bounds.size.y, 1) * 1.01f;
        }

        private void OnDestroy()
        {
            ReleasePlayer();
            if (sprite == null) return;
            if (Application.isPlaying) Destroy(sprite);
            else DestroyImmediate(sprite);
        }
    }
}
