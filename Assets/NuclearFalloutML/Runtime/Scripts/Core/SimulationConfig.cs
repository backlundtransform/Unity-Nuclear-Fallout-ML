using System;
using UnityEngine;

namespace NuclearFalloutML.Core
{
    /// <summary>
    /// Pasquill-Gifford stability classes — mirrors CSharpNumerics.Physics.Enums.StabilityClass.
    /// </summary>
    public enum FalloutStabilityClass
    {
        A_VeryUnstable,
        B_Unstable,
        C_SlightlyUnstable,
        D_Neutral,
        E_SlightlyStable,
        F_Stable
    }

    /// <summary>
    /// Full simulation configuration. User-facing input parameters.
    /// Maps to CSharpNumerics GeoEngine types at runtime.
    /// </summary>
    [Serializable]
    public class SimulationConfig
    {
        [Header("Source Location (WGS84)")]
        [Tooltip("Source latitude (decimal degrees)")]
        [Range(-90f, 90f)]
        public double SourceLatitude = 55.6050;

        [Tooltip("Source longitude (decimal degrees)")]
        [Range(-180f, 180f)]
        public double SourceLongitude = 13.0038;

        [Tooltip("Source altitude above ground (meters)")]
        public double SourceAltitudeMeters = 50;

        [Header("Source Term")]
        [Tooltip("Emission rate (kg/s)")]
        public double EmissionRateKgPerS = 5.0;

        [Tooltip("Effective release / stack height in meters")]
        [Range(0f, 2000f)]
        public float StackHeightMeters = 50f;

        [Tooltip("Radioisotope material (e.g. Cs137, I131, Sr90)")]
        public string Radioisotope = "Cs137";

        [Header("Atmospheric Conditions")]
        [Tooltip("Wind speed in m/s")]
        [Range(0.5f, 50f)]
        public float WindSpeedMs = 10f;

        [Tooltip("Wind direction vector X component (east)")]
        public float WindDirectionX = 1f;

        [Tooltip("Wind direction vector Y component (north)")]
        public float WindDirectionY = 0f;

        [Tooltip("Atmospheric stability class")]
        public FalloutStabilityClass Stability = FalloutStabilityClass.D_Neutral;

        [Header("Monte Carlo Settings")]
        [Tooltip("Number of Monte Carlo scenarios to simulate")]
        [Range(10, 100000)]
        public int MonteCarloIterations = 20;

        [Tooltip("Random seed for reproducibility (0 = random)")]
        public int RandomSeed = 42;

        [Header("Scenario Variation")]
        [Tooltip("Min wind speed for variation range (m/s)")]
        [Range(0.5f, 40f)]
        public float WindSpeedVariationMin = 8f;

        [Tooltip("Max wind speed for variation range (m/s)")]
        [Range(1f, 50f)]
        public float WindSpeedVariationMax = 12f;

        [Tooltip("Wind direction jitter σ (degrees)")]
        [Range(0f, 90f)]
        public float WindDirectionJitterDeg = 15f;

        [Tooltip("Min emission rate for variation (kg/s)")]
        public double EmissionRateVariationMin = 3.0;

        [Tooltip("Max emission rate for variation (kg/s)")]
        public double EmissionRateVariationMax = 7.0;

        [Header("Stability Weights (must sum to ~1)")]
        [Range(0f, 1f)] public float StabilityWeightC = 0.2f;
        [Range(0f, 1f)] public float StabilityWeightD = 0.6f;
        [Range(0f, 1f)] public float StabilityWeightE = 0.2f;

        [Header("Grid Settings")]
        [Tooltip("Grid half-extent in meters from source")]
        public float GridExtentMeters = 500f;

        [Tooltip("Grid altitude max (meters)")]
        public float GridAltitudeMaxMeters = 100f;

        [Tooltip("Grid cell step size (meters)")]
        [Range(1f, 100f)]
        public float GridStepMeters = 50f;

        [Header("Time Settings")]
        [Tooltip("Simulation start time (seconds)")]
        public double TimeStartSeconds = 0;

        [Tooltip("Simulation end time (seconds)")]
        public double TimeEndSeconds = 600;

        [Tooltip("Time step (seconds)")]
        public double TimeStepSeconds = 60;

        [Header("Plume Mode")]
        public PlumeModeOption PlumeMode = PlumeModeOption.Transient;

        [Tooltip("Puff release duration (seconds, only for Transient mode)")]
        public double PuffReleaseSeconds = 10;

        [Header("Export")]
        [Tooltip("Output directory relative to project root")]
        public string ExportDirectory = "FalloutExport";
    }

    public enum PlumeModeOption
    {
        SteadyState,
        Transient
    }
}
