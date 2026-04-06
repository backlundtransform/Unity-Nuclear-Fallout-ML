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
        public float sphereRadius = 1.0f;

        [Tooltip("Spacing between Bloch spheres for multi-qubit circuits")]
        [Range(2f, 8f)]
        public float sphereSpacing = 3f;

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

        [Header("Error Correction")]
        [Tooltip("Enable QEC demonstration")]
        public bool enableErrorCorrection = false;

        [Tooltip("QEC code to use")]
        public QECCodeType qecCode = QECCodeType.BitFlip3;

        [Tooltip("Simulated error rate for QEC comparison")]
        [Range(0f, 0.3f)]
        public float qecErrorRate = 0.05f;

        [Tooltip("Monte-Carlo rounds for fidelity comparison")]
        [Range(10, 500)]
        public int qecRounds = 100;

        [Header("RL Training")]
        [Tooltip("Enable RL error-correction agent")]
        public bool enableRL = false;

        [Tooltip("Number of training episodes per run")]
        [Range(10, 2000)]
        public int rlEpisodes = 200;

        [Tooltip("Max gates the RL agent can place per episode")]
        [Range(3, 20)]
        public int rlMaxGates = 10;

        [Tooltip("Fidelity threshold for episode success")]
        [Range(0.9f, 0.999f)]
        public float rlFidelityThreshold = 0.95f;

        [Tooltip("DQN learning rate")]
        [Range(0.0001f, 0.01f)]
        public float rlLearningRate = 0.001f;

        [Tooltip("DQN discount factor")]
        [Range(0.8f, 0.999f)]
        public float rlGamma = 0.99f;
    }

    public enum QECCodeType
    {
        BitFlip3,
        PhaseFlip3,
        Steane7,
        Shor9
    }
}
