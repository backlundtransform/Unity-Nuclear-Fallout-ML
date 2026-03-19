// Simulation results are provided by CSharpNumerics GeoEngine types:
//
// - MonteCarloScenarioResult  (CSharpNumerics.Engines.GIS.Scenario)
//     .ScenarioMatrix          → Matrix of all scenarios (N × cells·timeSteps)
//     .GetCellDistribution()   → Per-cell distribution across scenarios
//     .GetScenarioVector()     → Single scenario row
//
// - RiskScenarioResult  (CSharpNumerics.Engines.GIS.Scenario)
//     .ProbabilityAt(pos, time)           → Exceedance probability at point
//     .CumulativeProbabilityAt(pos, time) → Time-cumulative probability
//     .ProbabilityMapAt(timeIndex)        → Full probability map snapshot
//     .Snapshots                           → All GridSnapshots
//     .ExportGeoJson(path)
//     .ExportCesium(path)
//     .ExportUnity(path)
//
// - ScenarioClusterResult  (CSharpNumerics.Engines.GIS.Analysis)
//     .DominantCluster         → Largest cluster index
//     .GetClusterIterations()  → Scenario indices in cluster
//     .GetClusterMeanSnapshots() → Mean snapshot for a cluster
//
// - ProbabilityMap  (CSharpNumerics.Engines.GIS.Analysis)
//     .At(pos)                 → Exceedance probability at position
//     .CellsAbove(threshold)   → Hot cells
//
// - MonteCarloResult  (CSharpNumerics.Statistics.MonteCarlo)
//     .Mean, .Percentile(95), .StdDev, .Min, .Max
//
// No custom result types needed — use CSharpNumerics directly.

using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Analysis;
using CSharpNumerics.Engines.GIS.Grid;
using CSharpNumerics.Numerics.Objects;

namespace NuclearFalloutML.Core
{
    /// <summary>
    /// Helper extensions for working with CSharpNumerics result objects in Unity.
    /// </summary>
    public static class SimulationResultExtensions
    {
        /// <summary>
        /// Get a flattened array of probability values for a given time index.
        /// Useful for texture generation in CesiumFalloutRenderer.
        /// </summary>
        public static double[] GetProbabilityArray(this ScenarioResult result, int timeIndex = 0)
        {
            var probMap = result.ProbabilityMapAt(timeIndex: timeIndex);
            if (probMap == null) return new double[0];
            return probMap.GetValues();
        }
    }
}
