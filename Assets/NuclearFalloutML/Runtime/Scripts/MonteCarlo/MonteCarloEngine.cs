// Monte Carlo simulation is provided by CSharpNumerics GeoEngine.
// See: https://csnumerics.com/docs/Csharpnumerics/Simulation%20Engines/Geo%20Engine/
//
// Key classes:
//   CSharpNumerics.Engines.GIS.Scenario.PlumeMonteCarloModel
//   CSharpNumerics.Engines.GIS.Scenario.ScenarioVariation
//   CSharpNumerics.Engines.GIS.Scenario.MonteCarloScenarioResult
//   CSharpNumerics.Statistics.MonteCarlo.MonteCarloSimulator
//
// Usage via PlumeMonteCarloModel:
//   var mcModel = new PlumeMonteCarloModel(
//       emissionRate: 5.0,
//       windSpeed: 10,
//       windDirection: new Vector(1, 0, 0),
//       stackHeight: 50,
//       sourcePosition: new Vector(0, 0, 50),
//       grid: grid,
//       timeFrame: tf,
//       variation: variation,
//       stability: StabilityClass.D,
//       mode: PlumeMode.SteadyState);
//
//   MonteCarloScenarioResult result = mcModel.RunBatch(iterations: 1000, seed: 42);
//   Matrix scenarioMatrix = result.ScenarioMatrix;
//   double[] cellDist = result.GetCellDistribution(cellIndex: idx, timeIndex: 30);
//
// ScenarioVariation fluent API:
//   var variation = new ScenarioVariation()
//       .WindSpeed(8, 12)
//       .WindDirectionJitter(15)
//       .EmissionRate(3, 7)
//       .SetStabilityWeights(d: 0.6, c: 0.2, e: 0.2);
//
// Scalar Monte Carlo (peak concentration stats):
//   var sim = new MonteCarloSimulator(seed: 42);
//   MonteCarloResult peakStats = sim.Run(mcModel, iterations: 1000);
//   double mean = peakStats.Mean;
//   double p95 = peakStats.Percentile(95);
//
// The RiskScenario fluent API (used in FalloutSimulationManager) wraps all of
// this automatically. This file exists as documentation.

namespace NuclearFalloutML.MonteCarlo
{
    /// <summary>
    /// Marker class documenting that Monte Carlo simulation is fully delegated
    /// to CSharpNumerics.Engines.GIS.Scenario.PlumeMonteCarloModel.
    /// </summary>
    public static class MonteCarloInfo
    {
        public const string Engine = "CSharpNumerics.Engines.GIS.Scenario.PlumeMonteCarloModel";
        public const string Variation = "CSharpNumerics.Engines.GIS.Scenario.ScenarioVariation";
        public const string ScalarStats = "CSharpNumerics.Statistics.MonteCarlo.MonteCarloSimulator";
    }
}
