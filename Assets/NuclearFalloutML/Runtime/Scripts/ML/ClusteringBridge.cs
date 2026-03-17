// ML Clustering is provided by CSharpNumerics.ML and CSharpNumerics.Engines.GIS.Analysis.
// See: https://csnumerics.com/docs/Csharpnumerics/Machine%20learning/
// See: https://csnumerics.com/docs/Csharpnumerics/Simulation%20Engines/Geo%20Engine/
//
// CSharpNumerics ML namespace provides:
//   - ClusteringGrid    — Model registry for comparing clustering algorithms
//   - KMeans            — K-Means clustering
//   - SilhouetteEvaluator — Silhouette Score evaluator
//
// CSharpNumerics GeoEngine Analysis namespace provides:
//   - ScenarioClusterAnalyzer  — Clusters Monte Carlo scenarios
//   - ProbabilityMap            — Exceedance probability per cell
//   - TimeAnimator              — Time-animated probability maps
//
// Usage — ScenarioClusterAnalyzer (used within RiskScenario pipeline):
//
//   using CSharpNumerics.Engines.GIS.Analysis;
//   using CSharpNumerics.ML;
//
//   var analysis = ScenarioClusterAnalyzer
//       .For(mcResult)
//       .WithAlgorithm(new ClusteringGrid().AddModel<KMeans>(g => g.Add("K", 3, 5)))
//       .WithEvaluator(new SilhouetteEvaluator())
//       .Run();
//
//   int dominant = analysis.DominantCluster;
//   int[] members = analysis.GetClusterIterations(dominant);
//   List<GridSnapshot> mean = analysis.GetClusterMeanSnapshots(dominant);
//
// Usage — ProbabilityMap (per-cluster filtering):
//
//   var pMap = ProbabilityMap.Build(
//       mcResult.Snapshots,
//       timeIndex: 30,
//       threshold: 1e-6,
//       iterationFilter: members);
//
//   double pAt = pMap.At(new Vector(200, 50, 0));
//   var hotCells = pMap.CellsAbove(0.5);
//
// Usage — Unsupervised AutoML (from CSharpNumerics.ML):
//
//   Automated pipeline search for optimal clustering parameters,
//   fluent API, clustering experiment with cross-validation.
//
// All clustering in this project is delegated to CSharpNumerics.
// The ClusteringGrid comparison (Silhouette Score, model selection)
// is built into the RiskScenario.AnalyzeWith() call.

namespace NuclearFalloutML.ML
{
    /// <summary>
    /// Marker class documenting that ML clustering is fully delegated
    /// to CSharpNumerics.ML and CSharpNumerics.Engines.GIS.Analysis.
    /// </summary>
    public static class ClusteringInfo
    {
        public const string ClusteringEngine = "CSharpNumerics.ML.ClusteringGrid + KMeans";
        public const string Evaluator = "CSharpNumerics.ML.SilhouetteEvaluator";
        public const string ScenarioAnalyzer = "CSharpNumerics.Engines.GIS.Analysis.ScenarioClusterAnalyzer";
        public const string ProbabilityMapping = "CSharpNumerics.Engines.GIS.Analysis.ProbabilityMap";
    }
}
