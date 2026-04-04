using UnityEngine;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Applies a visual "noise glitch" to Bloch spheres proportional to error rate.
    /// Effect: random jitter + color desaturation + transparency pulsing.
    /// Attach alongside BlochSphereRenderer; updates each frame.
    /// </summary>
    public class NoiseGlitchEffect : MonoBehaviour
    {
        private float _errorRate;
        private bool _active;
        private Vector3 _originalPosition;
        private float _phaseOffset;

        private static readonly float MaxJitter = 0.15f;
        private static readonly float PulseSpeed = 4f;

        public void SetErrorRate(float rate)
        {
            _errorRate = Mathf.Clamp01(rate);
            _active = _errorRate > 0.001f;
        }

        public void Disable()
        {
            _active = false;
            _errorRate = 0f;
            // Snap back to original position
            transform.localPosition = Vector3.zero;
        }

        private void Awake()
        {
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void LateUpdate()
        {
            if (!_active) return;

            // Random spatial jitter proportional to error rate
            float jitterMag = _errorRate * MaxJitter;
            Vector3 jitter = new Vector3(
                (Mathf.PerlinNoise(Time.time * 8f + _phaseOffset, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, Time.time * 8f + _phaseOffset) - 0.5f) * 2f,
                (Mathf.PerlinNoise(Time.time * 8f + _phaseOffset, Time.time * 6f) - 0.5f) * 2f
            ) * jitterMag;
            transform.localPosition = jitter;

            // Color pulse on child renderers
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed + _phaseOffset);
            float alpha = Mathf.Lerp(1f, 0.3f + 0.7f * pulse, _errorRate);

            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.material.HasProperty("_Color"))
                {
                    var c = r.material.color;
                    // Shift towards red proportional to error rate
                    float redShift = _errorRate * 0.4f;
                    c.r = Mathf.Lerp(c.r, c.r + redShift, 0.1f);
                    c.a = alpha;
                    r.material.color = c;
                }
            }
        }
    }
}
