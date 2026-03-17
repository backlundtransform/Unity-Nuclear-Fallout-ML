using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using NuclearFalloutML.Core;
using NuclearFalloutML.Visualization;

// CSharpNumerics namespaces
using CSharpNumerics.Engines.GIS.Grid;
using CSharpNumerics.Engines.GIS.Simulation;
using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Analysis;
using CSharpNumerics.Engines.GIS.Coordinates;
using CSharpNumerics.Engines.GIS.Export;
using CSharpNumerics.Physics.Enums;
using CSharpNumerics.Physics.Materials;
using CSharpNumerics.Statistics.MonteCarlo;
using CSharpNumerics.ML;
using CSharpNumerics.Numerics;

namespace NuclearFalloutML
{
    /// <summary>
    /// Main simulation manager that orchestrates the CSharpNumerics GeoEngine pipeline:
    /// Physics → Monte Carlo → ML Clustering → Probability Map → Cesium Export
    ///
    /// Uses CSharpNumerics.Engines.GIS.Scenario.RiskScenario fluent API.
    /// </summary>
    public class FalloutSimulationManager : MonoBehaviour
    {
        [Header("Configuration")]
        public SimulationConfig Config = new SimulationConfig();

        [Header("References")]
        [SerializeField] private CesiumFalloutRenderer _renderer;

        // CSharpNumerics result objects
        private RiskScenarioResult _riskResult;
        private MonteCarloScenarioResult _mcResult;
        private ScenarioClusterResult _clusterResult;
        private bool _isRunning;

        // Events
        public event Action<float> OnSimulationProgress;
        public event Action<string> OnStatusUpdate;
        public event Action OnSimulationComplete;
        public event Action<ScenarioClusterResult> OnClusteringComplete;

        // Public accessors
        public bool IsRunning => _isRunning;
        public RiskScenarioResult RiskResult => _riskResult;
        public MonteCarloScenarioResult MonteCarloResult => _mcResult;
        public ScenarioClusterResult ClusterResult => _clusterResult;

        /// <summary>
        /// Run the full CSharpNumerics GeoEngine pipeline.
        /// </summary>
        public async void RunSimulation()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[FalloutSim] Simulation already running.");
                return;
            }

            _isRunning = true;

            try
            {
                OnStatusUpdate?.Invoke("Building CSharpNumerics pipeline...");
                OnSimulationProgress?.Invoke(0.05f);

                // Build all CSharpNumerics objects from config
                var stability = MapStabilityClass(Config.Stability);
                var plumeMode = Config.PlumeMode == PlumeModeOption.SteadyState
                    ? PlumeMode.SteadyState
                    : PlumeMode.Transient;

                var sourcePos = new Vector(0, 0, Config.StackHeightMeters);
                var windDir = new Vector(Config.WindDirectionX, Config.WindDirectionY, 0);

                // Parse K-Means cluster counts
                int[] kCounts = Config.KMeansClusterCounts
                    .Split(',')
                    .Select(s => int.TryParse(s.Trim(), out int v) ? v : 3)
                    .ToArray();

                // Build the full pipeline using the RiskScenario fluent API
                OnStatusUpdate?.Invoke("Configuring RiskScenario pipeline...");
                OnSimulationProgress?.Invoke(0.1f);

                var scenarioBuilder = RiskScenario
                    .ForGaussianPlume(Config.EmissionRateKgPerS)
                    .FromSource(sourcePos)
                    .WithWind(Config.WindSpeedMs, windDir)
                    .WithStability(stability)
                    .WithMaterial(Materials.Radioisotope(Config.Radioisotope))
                    .WithVariation(v => v
                        .WindSpeed(Config.WindSpeedVariationMin, Config.WindSpeedVariationMax)
                        .WindDirectionJitter(Config.WindDirectionJitterDeg)
                        .EmissionRate(Config.EmissionRateVariationMin, Config.EmissionRateVariationMax)
                        .SetStabilityWeights(
                            c: Config.StabilityWeightC,
                            d: Config.StabilityWeightD,
                            e: Config.StabilityWeightE))
                    .OverGrid(new GeoGrid(
                        xMin: -Config.GridExtentMeters,
                        xMax: Config.GridExtentMeters,
                        yMin: -Config.GridExtentMeters,
                        yMax: Config.GridExtentMeters,
                        zMin: 0,
                        zMax: Config.GridAltitudeMaxMeters,
                        step: Config.GridStepMeters))
                    .OverTime(Config.TimeStartSeconds, Config.TimeEndSeconds, Config.TimeStepSeconds);

                // Handle transient puff release
                if (Config.PlumeMode == PlumeModeOption.Transient)
                {
                    // PlumeSimulator.ReleaseSeconds is set on the transient sim internally
                }

                // Step 1: Run Monte Carlo
                OnStatusUpdate?.Invoke($"Running {Config.MonteCarloIterations} Monte Carlo scenarios...");
                OnSimulationProgress?.Invoke(0.15f);

                _riskResult = await Task.Run(() => scenarioBuilder
                    .RunMonteCarlo(Config.MonteCarloIterations, Config.RandomSeed)
                    .AnalyzeWith(
                        new ClusteringGrid().AddModel<KMeans>(g =>
                        {
                            foreach (int k in kCounts)
                                g.Add("K", k);
                        }),
                        new SilhouetteEvaluator())
                    .Build(threshold: Config.ProbabilityThreshold));

                OnSimulationProgress?.Invoke(0.85f);
                OnStatusUpdate?.Invoke("Pipeline complete. Preparing visualization...");

                // Extract results for visualization
                var probMap = _riskResult.ProbabilityMapAt(timeIndex: 0);

                // Step 2: Update Cesium visualization
                if (_renderer != null)
                {
                    _renderer.Initialize(Config, _riskResult);
                }

                OnSimulationProgress?.Invoke(0.9f);

                // Step 3: Export
                OnStatusUpdate?.Invoke("Exporting results...");
                string outputDir = System.IO.Path.Combine(
                    Application.dataPath, "..", Config.ExportDirectory);
                System.IO.Directory.CreateDirectory(outputDir);

                await Task.Run(() =>
                {
                    _riskResult.ExportGeoJson(
                        System.IO.Path.Combine(outputDir, "fallout_plume.geojson"));
                    _riskResult.ExportCesium(
                        System.IO.Path.Combine(outputDir, "fallout_plume.czml"));
                    _riskResult.ExportUnity(
                        System.IO.Path.Combine(outputDir, "fallout_plume.bin"));
                });

                OnSimulationProgress?.Invoke(1f);
                OnStatusUpdate?.Invoke(
                    $"Complete. {Config.MonteCarloIterations} scenarios → " +
                    $"exported to {Config.ExportDirectory}/");
                OnSimulationComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FalloutSim] Error: {ex.Message}\n{ex.StackTrace}");
                OnStatusUpdate?.Invoke($"Error: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Query probability at a specific world point and time.
        /// </summary>
        public double QueryProbabilityAt(float x, float y, float z, double timeSeconds)
        {
            if (_riskResult == null) return 0;
            return _riskResult.ProbabilityAt(new Vector(x, y, z), timeSeconds: timeSeconds);
        }

        /// <summary>
        /// Query cumulative probability at a specific world point.
        /// </summary>
        public double QueryCumulativeProbabilityAt(float x, float y, float z, double timeSeconds)
        {
            if (_riskResult == null) return 0;
            return _riskResult.CumulativeProbabilityAt(new Vector(x, y, z), timeSeconds);
        }

        /// <summary>
        /// Change the visualization display mode.
        /// </summary>
        public void SetDisplayMode(CesiumFalloutRenderer.FalloutDisplayMode mode)
        {
            if (_renderer != null)
            {
                _renderer.DisplayMode = mode;
                _renderer.UpdateVisualization();
            }
        }

        /// <summary>
        /// Map our Unity-friendly enum to CSharpNumerics StabilityClass.
        /// </summary>
        private static StabilityClass MapStabilityClass(FalloutStabilityClass falloutClass)
        {
            return falloutClass switch
            {
                FalloutStabilityClass.A_VeryUnstable => StabilityClass.A,
                FalloutStabilityClass.B_Unstable => StabilityClass.B,
                FalloutStabilityClass.C_SlightlyUnstable => StabilityClass.C,
                FalloutStabilityClass.D_Neutral => StabilityClass.D,
                FalloutStabilityClass.E_SlightlyStable => StabilityClass.E,
                FalloutStabilityClass.F_Stable => StabilityClass.F,
                _ => StabilityClass.D
            };
        }
    }
}
