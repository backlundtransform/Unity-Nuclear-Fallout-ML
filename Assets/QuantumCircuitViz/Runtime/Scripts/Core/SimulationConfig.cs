using System;
using UnityEngine;

namespace QuantumCircuitViz.Core
{
    /// <summary>
    /// Inspector-friendly configuration for quantum circuit demos.
    /// </summary>
    [Serializable]
    public class SimulationConfig
    {
        [Header("Circuit")]
        [Tooltip("Number of qubits in the circuit")]
        [Range(1, 5)]
        public int qubitCount = 1;

        [Header("Visualization")]
        [Tooltip("Bloch sphere radius in world units")]
        [Range(0.5f, 3f)]
        public float sphereRadius = 1.5f;

        [Tooltip("Spacing between Bloch spheres for multi-qubit circuits")]
        [Range(2f, 8f)]
        public float sphereSpacing = 4f;

        [Tooltip("Seconds per gate animation step")]
        [Range(0.1f, 3f)]
        public float animationSpeed = 0.8f;

        [Tooltip("Number of latitude/longitude lines on sphere wireframe")]
        [Range(8, 32)]
        public int wireframeSegments = 16;

        [Header("Post-Processing")]
        [Tooltip("Bloom intensity for HDR glow effects")]
        [Range(0f, 5f)]
        public float bloomIntensity = 1.5f;
    }
}
