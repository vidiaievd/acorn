using UnityEngine;

namespace MagicWorld
{
    /// <summary>Синий кристалл: парит, вращается, пульсирует светом.</summary>
    public class Crystal : MonoBehaviour
    {
        public Transform visual;
        public Light glow;
        Vector3 basePos;
        float phase;

        void Start()
        {
            basePos = visual != null ? visual.localPosition : Vector3.zero;
            phase = Random.value * 6.28f;
        }

        void Update()
        {
            if (visual == null) return;
            visual.Rotate(0, 70f * Time.deltaTime, 0, Space.World);
            visual.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 2f + phase) * 0.18f;
            if (glow != null) glow.intensity = 2.2f + Mathf.Sin(Time.time * 3f + phase) * 0.6f;
        }
    }
}
