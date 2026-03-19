using UnityEngine;
using NuclearFalloutML.Core;

using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Grid;
using CSharpNumerics.Engines.GIS.Analysis;
using CSharpNumerics.Engines.GIS.Simulation;
using CSharpNumerics.ML.Clustering.Algorithms;
using CSharpNumerics.ML.Clustering.Evaluators;
using CSharpNumerics.Numerics.Objects;
using CSharpNumerics.Physics.Enums;
using CSharpNumerics.Physics.Materials;

namespace NuclearFalloutML.Demo
{
    /// <summary>
    /// Minimal demo that validates the CSharpNumerics pipeline compiles and runs.
    /// Attach to any GameObject and enter Play Mode.
    ///
    /// This does NOT require Cesium, UI, or any scene setup — just verifies
    /// that the DLL, namespaces, and API calls are correct.
    /// </summary>
    public class DemoSimulation : MonoBehaviour
    {
        [Header("Demo Settings")]
        [Tooltip("Number of Monte Carlo iterations (keep low for fast demo)")]
        [Range(10, 500)]
        public int iterations = 50;

        [Tooltip("Run automatically on Start")]
        public bool autoRun = true;

        private async void Start()
        {
            if (autoRun) await RunDemo();
        }

        public async System.Threading.Tasks.Task RunDemo()
        {
            Debug.Log("[Demo] Starting CSharpNumerics pipeline validation...");

            // ── Step 1: Deterministic single scenario ──────────────────
            Debug.Log("[Demo] Step 1: Single deterministic scenario...");

            var singleResult = RiskScenario
                .ForGaussianPlume(5.0)
                .FromSource(new Vector(0, 0, 50))
                .WithWind(10, new Vector(1, 0, 0))
                .WithStability(StabilityClass.D)
                .WithMaterial(Materials.Radioisotope("Cs137"))
                .OverGrid(new GeoGrid(-200, 200, -200, 200, 0, 50, 20))
                .OverTime(0, 600, 60)
                .RunSingle();

            Debug.Log($"[Demo] Single scenario - Grid cells: {singleResult.Grid.CellCount}, " +
                      $"Snapshots: {singleResult.Snapshots?.Count ?? 0}");

            // ── Step 2: Monte Carlo (no clustering) ────────────────────
            Debug.Log($"[Demo] Step 2: Monte Carlo ({iterations} iterations, no clustering)...");

            var mcResult = await System.Threading.Tasks.Task.Run(() =>
                RiskScenario
                    .ForGaussianPlume(5.0)
                    .FromSource(new Vector(0, 0, 50))
                    .WithWind(10, new Vector(1, 0, 0))
                    .WithStability(StabilityClass.D)
                    .WithMaterial(Materials.Radioisotope("Cs137"))
                    .WithVariation(v => v
                        .WindSpeed(8, 12)
                        .WindDirectionJitter(15)
                        .EmissionRate(3, 7)
                        .SetStabilityWeights(c: 0.2, d: 0.6, e: 0.2))
                    .OverGrid(new GeoGrid(-200, 200, -200, 200, 0, 50, 20))
                    .OverTime(0, 600, 60)
                    .RunMonteCarlo(iterations, seed: 42)
                    .Build(threshold: 1e-6));

            double p1 = mcResult.ProbabilityAt(new Vector(100, 0, 0), 300);
            double cp1 = mcResult.CumulativeProbabilityAt(new Vector(100, 0, 0), 300);
            var probMap = mcResult.ProbabilityMapAt(0);

            Debug.Log($"[Demo] MC result - P(100,0,0 @ 300s)={p1:F4}, " +
                      $"CumP={cp1:F4}, ProbMap cells={probMap?.CellCount ?? 0}");

            // ── Step 3: Full pipeline with clustering ──────────────────
            Debug.Log($"[Demo] Step 3: Full pipeline with KMeans clustering...");

            var fullResult = await System.Threading.Tasks.Task.Run(() =>
                RiskScenario
                    .ForGaussianPlume(5.0)
                    .FromSource(new Vector(0, 0, 50))
                    .WithWind(10, new Vector(1, 0, 0))
                    .WithStability(StabilityClass.D)
                    .WithMaterial(Materials.Radioisotope("Cs137"))
                    .WithVariation(v => v
                        .WindSpeed(8, 12)
                        .WindDirectionJitter(15)
                        .EmissionRate(3, 7)
                        .SetStabilityWeights(c: 0.2, d: 0.6, e: 0.2))
                    .OverGrid(new GeoGrid(-200, 200, -200, 200, 0, 50, 20))
                    .OverTime(0, 600, 60)
                    .RunMonteCarlo(iterations, seed: 42)
                    .AnalyzeWith(new KMeans(), new SilhouetteEvaluator(), minK: 2, maxK: 4)
                    .Build(threshold: 1e-6));

            Debug.Log($"[Demo] Full pipeline - Cluster analysis: " +
                      $"Dominant={fullResult.ClusterAnalysis?.DominantCluster}, " +
                      $"BestK={fullResult.ClusterAnalysis?.BestClusterCount}, " +
                      $"Score={fullResult.ClusterAnalysis?.BestScore:F4}");

            // ── Step 4: Verify helper classes ──────────────────────────
            Debug.Log("[Demo] Step 4: Verifying helper classes...");

            var grid = GeoGridFactory.FromConfig(new SimulationConfig());
            Debug.Log($"[Demo] GeoGridFactory: cells={grid.CellCount}");

            var coord = GeoCoordinateFactory.Create(55.605, 13.004);
            Debug.Log($"[Demo] GeoCoordinate: lat={coord.Latitude}, lon={coord.Longitude}");

            var extensions = fullResult.GetProbabilityArray(0);
            Debug.Log($"[Demo] GetProbabilityArray: length={extensions.Length}");

            // ── Step 5: Export test ────────────────────────────────────
            Debug.Log("[Demo] Step 5: Testing export...");
            string exportDir = System.IO.Path.Combine(Application.dataPath, "..", "DemoExport");
            Export.FalloutExporter.SaveAll(fullResult, exportDir);

            Debug.Log("[Demo] ✓ ALL VALIDATION STEPS PASSED");
            Debug.Log($"[Demo] Export files written to: {exportDir}");
        }
    }
}
