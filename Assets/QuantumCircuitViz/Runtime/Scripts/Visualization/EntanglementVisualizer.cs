using UnityEngine;
using System.Collections.Generic;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Draws glowing connection lines between entangled Bloch spheres.
    /// Detects entanglement via purity of the single-qubit reduced state:
    /// a pure qubit has |bloch| ≈ 1, an entangled qubit has |bloch| &lt; 1.
    /// When two qubits both have |bloch| &lt; threshold they are linked.
    /// </summary>
    public class EntanglementVisualizer : MonoBehaviour
    {
        private BlochSphereRenderer[] _spheres;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private float _purityThreshold = 0.95f;

        private static readonly Color EntangleColor = new Color(1f, 0.3f, 0.6f, 0.7f);
        private static readonly Color StrongEntangle = new Color(1f, 0.1f, 0.9f, 0.9f);

        public void Initialise(BlochSphereRenderer[] spheres, float purityThreshold = 0.95f)
        {
            _spheres = spheres;
            _purityThreshold = purityThreshold;
        }

        /// <summary>
        /// Update entanglement lines given current per-qubit Bloch vectors.
        /// Call each time the state changes.
        /// </summary>
        public void UpdateEntanglement(Vector3[] blochVectors)
        {
            // Destroy old lines
            foreach (var lr in _lines)
                if (lr != null) Destroy(lr.gameObject);
            _lines.Clear();

            if (_spheres == null || blochVectors == null) return;

            int n = Mathf.Min(_spheres.Length, blochVectors.Length);

            // Find impure qubits (magnitude < threshold → entangled with something)
            var impure = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (blochVectors[i].magnitude < _purityThreshold)
                    impure.Add(i);
            }

            // Draw lines between all pairs of impure qubits
            for (int a = 0; a < impure.Count; a++)
            {
                for (int b = a + 1; b < impure.Count; b++)
                {
                    int qi = impure[a];
                    int qj = impure[b];

                    // Strength: lower purity = stronger entanglement visual
                    float avgMag = (blochVectors[qi].magnitude + blochVectors[qj].magnitude) * 0.5f;
                    float strength = 1f - Mathf.Clamp01(avgMag);

                    var lr = CreateLine(
                        _spheres[qi].transform.position,
                        _spheres[qj].transform.position,
                        strength);
                    _lines.Add(lr);
                }
            }
        }

        private LineRenderer CreateLine(Vector3 from, Vector3 to, float strength)
        {
            var go = new GameObject("EntangleLine");
            go.transform.SetParent(transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);

            float width = Mathf.Lerp(0.02f, 0.08f, strength);
            lr.startWidth = width;
            lr.endWidth = width;

            var color = Color.Lerp(EntangleColor, StrongEntangle, strength);
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.useWorldSpace = true;

            return lr;
        }
    }
}
