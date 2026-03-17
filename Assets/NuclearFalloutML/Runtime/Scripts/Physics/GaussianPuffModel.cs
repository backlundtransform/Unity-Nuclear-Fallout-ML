// Atmospheric dispersion physics is provided by CSharpNumerics GeoEngine.
// See: https://csnumerics.com/docs/Csharpnumerics/Simulation%20Engines/Geo%20Engine/
//
// CSharpNumerics provides:
//   - PlumeSimulator        (single-scenario Gaussian plume / puff)
//   - PlumeMonteCarloModel  (N stochastic scenarios)
//   - RiskScenario           (fluent end-to-end pipeline)
//
// The physics model uses EnvironmentalExtensions.GaussianPlume / GaussianPuff
// with Pasquill-Gifford dispersion coefficients (StabilityClass A–F).
//
// Usage:
//   using CSharpNumerics.Engines.GIS.Simulation;
//   using CSharpNumerics.Physics.Enums;
//
//   var sim = new PlumeSimulator(
//       emissionRate: 5.0,
//       windSpeed: 10,
//       windDirection: new Vector(1, 0, 0),
//       stackHeight: 50,
//       sourcePosition: new Vector(0, 0, 50),
//       stability: StabilityClass.D,
//       mode: PlumeMode.SteadyState);
//
//   List<GridSnapshot> snapshots = sim.Run(grid, timeFrame);
//
// Transient puff mode:
//   var simPuff = new PlumeSimulator(
//       emissionRate: 5.0,
//       windSpeed: 10,
//       windDirection: new Vector(1, 0, 0),
//       stackHeight: 50,
//       sourcePosition: new Vector(0, 0, 50),
//       stability: StabilityClass.D,
//       mode: PlumeMode.Transient);
//   simPuff.ReleaseSeconds = 10;
//   List<GridSnapshot> transientSnaps = simPuff.Run(grid, tf);
//
// This file exists as documentation. No custom physics code is needed —
// CSharpNumerics GeoEngine handles all atmospheric dispersion calculations.

namespace NuclearFalloutML.Physics
{
    /// <summary>
    /// Marker class documenting that atmospheric dispersion physics
    /// is fully delegated to CSharpNumerics.Engines.GIS.Simulation.PlumeSimulator.
    /// </summary>
    public static class PhysicsInfo
    {
        public const string Engine = "CSharpNumerics.Engines.GIS.Simulation.PlumeSimulator";
        public const string Models = "GaussianPlume (steady-state), GaussianPuff (transient)";
        public const string Coefficients = "Pasquill-Gifford (StabilityClass A–F)";
    }
}
