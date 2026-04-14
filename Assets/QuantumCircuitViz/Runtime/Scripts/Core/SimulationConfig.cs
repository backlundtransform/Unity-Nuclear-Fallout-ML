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

        [Tooltip("Seconds per gate animation step")]
        [Range(0.1f, 3f)]
        public float animationSpeed = 0.8f;

        [Tooltip("Number of latitude/longitude lines on sphere wireframe")]
        [Range(8, 512)]
        public int wireframeSegments = 256;

        [Header("Noise")]
        [Tooltip("Enable quantum noise simulation")]
        public bool enableNoise = false;

        [Tooltip("Depolarizing noise probability per gate")]
        [Range(0f, 0.5f)]
        public float depolarizingRate = 0.01f;

        [Tooltip("Dephasing noise probability per gate")]
        [Range(0f, 0.5f)]
        public float dephasingRate = 0.0f;

        [Tooltip("Amplitude damping gamma per gate")]
        [Range(0f, 0.5f)]
        public float amplitudeDampingGamma = 0.0f;
    }
}
