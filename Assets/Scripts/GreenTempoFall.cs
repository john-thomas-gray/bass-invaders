using UnityEngine;

namespace CandyCruisers
{
    // Detached so clearing a group cannot remove its tempo-down feedback.
    public sealed class GreenTempoFall : MonoBehaviour
    {
        private Vector3 origin;
        private float duration, elapsed;
        private MeshRenderer[] layers;
        private MaterialPropertyBlock tint;
        public static GreenTempoFall Create(GameObject arrow, float seconds)
        {
            var fall = arrow.AddComponent<GreenTempoFall>();
            fall.tint = new MaterialPropertyBlock();
            fall.origin = arrow.transform.position;
            fall.duration = Mathf.Max(.001f, seconds);
            fall.layers = arrow.GetComponentsInChildren<MeshRenderer>();
            fall.Tick(0);
            return fall;
        }
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            elapsed += Mathf.Max(0, seconds);
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.position = origin + Vector3.down * (.65f * Mathf.SmoothStep(0, 1, progress));
            for (int i = 0; i < layers.Length; i++)
            {
                var color = i == 0 ? new Color(.13f, .12f, .12f) : Color.white;
                color.a = 1 - progress;
                tint.SetColor("_Color", color);
                layers[i].SetPropertyBlock(tint);
                layers[i].enabled = progress < 1;
            }
            if (progress >= 1)
            {
                gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(gameObject);
            }
        }
    }
}
