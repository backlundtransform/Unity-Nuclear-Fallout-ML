// CSharpNumerics provides a comprehensive numerics layer:
//
//   CSharpNumerics.Numerics.Vector     — 3D vector (X, Y, Z)
//   CSharpNumerics.Numerics.Matrix     — Dense matrix (rows × cols)
//   CSharpNumerics.Numerics.ScalarField — Grid-aligned scalar field
//   CSharpNumerics.Numerics.VectorField — Grid-aligned vector field
//
//   CSharpNumerics.Statistics.MonteCarlo.MonteCarloSimulator
//     .Run(func, N)  → MonteCarloResult (.Mean, .Percentile(p), .StdDev, .Min, .Max)
//
//   CSharpNumerics.Statistics.MonteCarlo.MonteCarloResult
//     .Mean, .StdDev, .Min, .Max, .Percentile(95), .Histogram(bins)
//
//   CSharpNumerics.ML.KMeans           — K-Means++ clustering
//   CSharpNumerics.ML.ClusteringGrid   — Multi-model comparison
//   CSharpNumerics.ML.SilhouetteEvaluator — Cluster quality evaluator
//
// This file is a documentation/reference entry point.
// No custom numerics code is needed — use CSharpNumerics directly.
//
// NuGet package: CSharpNumerics (v2.6.3+)
// Website: https://csnumerics.com/
// Author: Göran Bäcklund (backlundtransform)

using System;
using CSharpNumerics.Numerics.Objects;
using CSharpNumerics.Statistics.MonteCarlo;

namespace NuclearFalloutML.Numerics
{
    /// <summary>
    /// Reference entry point for CSharpNumerics numerics and statistics.
    /// All heavy numerical work is delegated to CSharpNumerics directly.
    /// This class provides convenience wrappers for common Unity-side queries.
    /// </summary>
    public static class NumericsEngine
    {
        /// <summary>
        /// Run a scalar Monte Carlo simulation and return summary statistics.
        /// Wraps CSharpNumerics.Statistics.MonteCarlo.MonteCarloSimulator.
        /// </summary>
        public static MonteCarloResult RunScalarMonteCarlo(
            Func<RandomGenerator, double> trialFunc, int iterations)
        {
            var simulator = new MonteCarloSimulator();
            return simulator.Run(trialFunc, iterations);
        }

        /// <summary>
        /// Create a 3D vector (used as source position, wind direction, etc.).
        /// </summary>
        public static Vector Vec(double x, double y, double z) => new Vector(x, y, z);
    }
}
