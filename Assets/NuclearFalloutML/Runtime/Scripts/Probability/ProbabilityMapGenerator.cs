// Probability map generation is provided by CSharpNumerics GeoEngine.
// See: https://csnumerics.com/docs/Csharpnumerics/Simulation%20Engines/Geo%20Engine/
//
// CSharpNumerics types:
//   - ProbabilityMap  (CSharpNumerics.Engines.GIS.Analysis)
//   - TimeAnimator    (CSharpNumerics.Engines.GIS.Analysis)
//
// Single time step probability:
//   var pMap = ProbabilityMap.Build(
//       mcResult.Snapshots,
//       timeIndex: 30,
//       threshold: 1e-6,
//       iterationFilter: members);   // optional: filter to dominant cluster
//
//   double pAt = pMap.At(new Vector(200, 50, 0));
//   var hotCells = pMap.CellsAbove(0.5);
//
// Time-animated probability:
//   var animation = TimeAnimator.Build(mcResult.Snapshots, threshold: 1e-6);
//   double p = animation.ProbabilityAt(new Vector(200, 50, 0), timeSeconds: 1800);
//   double cumP = animation.CumulativeProbabilityAt(new Vector(200, 50, 0), 1800);
//
// Via RiskScenarioResult:
//   ProbabilityMap map = result.ProbabilityMapAt(timeIndex: 30);
//   double p = result.ProbabilityAt(new Vector(200, 50, 0), timeSeconds: 1800);
//   double cumP = result.CumulativeProbabilityAt(new Vector(200, 50, 0), 1800);
//
// Scalar statistics (MonteCarloResult):
//   var sim = new MonteCarloSimulator(seed: 42);
//   MonteCarloResult peakStats = sim.Run(mcModel, iterations: 1000);
//   double meanPeak = peakStats.Mean;
//   double p95 = peakStats.Percentile(95);

namespace NuclearFalloutML.Probability
{
    /// <summary>
    /// Marker class documenting that probability mapping is fully delegated
    /// to CSharpNumerics.Engines.GIS.Analysis.ProbabilityMap and TimeAnimator.
    /// </summary>
    public static class ProbabilityInfo
    {
        public const string ProbabilityMap = "CSharpNumerics.Engines.GIS.Analysis.ProbabilityMap";
        public const string TimeAnimator = "CSharpNumerics.Engines.GIS.Analysis.TimeAnimator";
        public const string ScalarStats = "CSharpNumerics.Statistics.MonteCarlo.MonteCarloResult";
    }
}
