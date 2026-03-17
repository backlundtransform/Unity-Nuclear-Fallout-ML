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
using CSharpNumerics.Statistics.MonteCarlo;

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
        public static double[] GetProbabilityArray(this RiskScenarioResult result, int timeIndex = 0)
        {
            var probMap = result.ProbabilityMapAt(timeIndex: timeIndex);
            return GetProbabilityArrayFromMap(probMap, result);
        }

        /// <summary>
        /// Extract probability values from a ProbabilityMap into a flat array
        /// ordered by grid cell index.
        /// </summary>
        private static double[] GetProbabilityArrayFromMap(ProbabilityMap probMap,
            RiskScenarioResult result)
        {
            var snapshots = result.Snapshots;
            if (snapshots == null || snapshots.Count == 0) return new double[0];

            var grid = snapshots[0].Grid;
            int cellCount = grid.CellCount;
            double[] values = new double[cellCount];

            for (int i = 0; i < cellCount; i++)
            {
                var pos = grid.CellCentre(i);
                values[i] = probMap.At(pos);
            }

            return values;
        }
    }
}
