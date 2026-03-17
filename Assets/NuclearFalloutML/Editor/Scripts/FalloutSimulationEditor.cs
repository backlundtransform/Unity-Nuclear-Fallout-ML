#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NuclearFalloutML.Core;
using NuclearFalloutML.Visualization;

namespace NuclearFalloutML.Editor
{
    [CustomEditor(typeof(FalloutSimulationManager))]
    public class FalloutSimulationEditor : UnityEditor.Editor
    {
        private bool _showVariation = false;
        private bool _showGrid = false;
        private bool _showTime = false;
        private bool _showClustering = false;

        public override void OnInspectorGUI()
        {
            var manager = (FalloutSimulationManager)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("NUCLEAR FALLOUT ML SIMULATOR", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "CSharpNumerics GeoEngine: Physics → MC → ML Clustering → Probability → Export",
                EditorStyles.miniLabel);
            EditorGUILayout.Space();

            // Source Location
            EditorGUILayout.LabelField("Source Location (WGS84)", EditorStyles.boldLabel);
            manager.Config.SourceLatitude = EditorGUILayout.DoubleField("Latitude", manager.Config.SourceLatitude);
            manager.Config.SourceLongitude = EditorGUILayout.DoubleField("Longitude", manager.Config.SourceLongitude);
            manager.Config.SourceAltitudeMeters = EditorGUILayout.DoubleField("Altitude (m)", manager.Config.SourceAltitudeMeters);

            EditorGUILayout.Space();

            // Source Term
            EditorGUILayout.LabelField("Source Term", EditorStyles.boldLabel);
            manager.Config.EmissionRateKgPerS = EditorGUILayout.DoubleField(
                "Emission Rate (kg/s)", manager.Config.EmissionRateKgPerS);
            manager.Config.StackHeightMeters = EditorGUILayout.Slider(
                "Stack Height (m)", manager.Config.StackHeightMeters, 0f, 2000f);
            manager.Config.Radioisotope = EditorGUILayout.TextField(
                "Radioisotope", manager.Config.Radioisotope);

            EditorGUILayout.Space();

            // Atmospheric Conditions
            EditorGUILayout.LabelField("Atmospheric Conditions", EditorStyles.boldLabel);
            manager.Config.WindSpeedMs = EditorGUILayout.Slider("Wind Speed (m/s)",
                manager.Config.WindSpeedMs, 0.5f, 50f);
            manager.Config.WindDirectionX = EditorGUILayout.FloatField(
                "Wind Direction X (east)", manager.Config.WindDirectionX);
            manager.Config.WindDirectionY = EditorGUILayout.FloatField(
                "Wind Direction Y (north)", manager.Config.WindDirectionY);
            manager.Config.Stability = (FalloutStabilityClass)EditorGUILayout.EnumPopup(
                "Stability Class", manager.Config.Stability);

            EditorGUILayout.Space();

            // Monte Carlo
            EditorGUILayout.LabelField("Monte Carlo", EditorStyles.boldLabel);
            manager.Config.MonteCarloIterations = EditorGUILayout.IntSlider(
                "Iterations", manager.Config.MonteCarloIterations, 10, 100000);
            manager.Config.RandomSeed = EditorGUILayout.IntField(
                "Random Seed (0=random)", manager.Config.RandomSeed);

            EditorGUILayout.Space();

            // Plume Mode
            EditorGUILayout.LabelField("Plume Mode", EditorStyles.boldLabel);
            manager.Config.PlumeMode = (PlumeModeOption)EditorGUILayout.EnumPopup(
                "Mode", manager.Config.PlumeMode);
            if (manager.Config.PlumeMode == PlumeModeOption.Transient)
            {
                manager.Config.PuffReleaseSeconds = EditorGUILayout.DoubleField(
                    "Puff Release (s)", manager.Config.PuffReleaseSeconds);
            }

            // Scenario Variation
            _showVariation = EditorGUILayout.Foldout(_showVariation, "Scenario Variation");
            if (_showVariation)
            {
                EditorGUI.indentLevel++;
                manager.Config.WindSpeedVariationMin = EditorGUILayout.Slider(
                    "Wind Speed Min (m/s)", manager.Config.WindSpeedVariationMin, 0.5f, 40f);
                manager.Config.WindSpeedVariationMax = EditorGUILayout.Slider(
                    "Wind Speed Max (m/s)", manager.Config.WindSpeedVariationMax, 1f, 50f);
                manager.Config.WindDirectionJitterDeg = EditorGUILayout.Slider(
                    "Wind Dir Jitter (°)", manager.Config.WindDirectionJitterDeg, 0f, 90f);
                manager.Config.EmissionRateVariationMin = EditorGUILayout.DoubleField(
                    "Emission Min (kg/s)", manager.Config.EmissionRateVariationMin);
                manager.Config.EmissionRateVariationMax = EditorGUILayout.DoubleField(
                    "Emission Max (kg/s)", manager.Config.EmissionRateVariationMax);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Stability Weights", EditorStyles.miniLabel);
                manager.Config.StabilityWeightC = EditorGUILayout.Slider(
                    "C weight", manager.Config.StabilityWeightC, 0f, 1f);
                manager.Config.StabilityWeightD = EditorGUILayout.Slider(
                    "D weight", manager.Config.StabilityWeightD, 0f, 1f);
                manager.Config.StabilityWeightE = EditorGUILayout.Slider(
                    "E weight", manager.Config.StabilityWeightE, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            // Grid Settings
            _showGrid = EditorGUILayout.Foldout(_showGrid, "Grid Settings");
            if (_showGrid)
            {
                EditorGUI.indentLevel++;
                manager.Config.GridExtentMeters = EditorGUILayout.FloatField(
                    "Grid Extent (m)", manager.Config.GridExtentMeters);
                manager.Config.GridAltitudeMaxMeters = EditorGUILayout.FloatField(
                    "Altitude Max (m)", manager.Config.GridAltitudeMaxMeters);
                manager.Config.GridStepMeters = EditorGUILayout.Slider(
                    "Step (m)", manager.Config.GridStepMeters, 1f, 100f);
                EditorGUI.indentLevel--;
            }

            // Time Settings
            _showTime = EditorGUILayout.Foldout(_showTime, "Time Settings");
            if (_showTime)
            {
                EditorGUI.indentLevel++;
                manager.Config.TimeStartSeconds = EditorGUILayout.DoubleField(
                    "Start (s)", manager.Config.TimeStartSeconds);
                manager.Config.TimeEndSeconds = EditorGUILayout.DoubleField(
                    "End (s)", manager.Config.TimeEndSeconds);
                manager.Config.TimeStepSeconds = EditorGUILayout.DoubleField(
                    "Step (s)", manager.Config.TimeStepSeconds);
                EditorGUI.indentLevel--;
            }

            // Clustering
            _showClustering = EditorGUILayout.Foldout(_showClustering, "Clustering (K-Means)");
            if (_showClustering)
            {
                EditorGUI.indentLevel++;
                manager.Config.KMeansClusterCounts = EditorGUILayout.TextField(
                    "K values (csv)", manager.Config.KMeansClusterCounts);
                manager.Config.ProbabilityThreshold = EditorGUILayout.DoubleField(
                    "Prob Threshold", manager.Config.ProbabilityThreshold);
                EditorGUI.indentLevel--;
            }

            // Export
            EditorGUILayout.Space();
            manager.Config.ExportDirectory = EditorGUILayout.TextField(
                "Export Directory", manager.Config.ExportDirectory);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Run button
            GUI.backgroundColor = manager.IsRunning ? Color.red : Color.green;

            if (Application.isPlaying)
            {
                if (manager.IsRunning)
                {
                    if (GUILayout.Button("CANCEL SIMULATION", GUILayout.Height(40)))
                        manager.CancelSimulation();
                }
                else
                {
                    if (GUILayout.Button("RUN SIMULATION", GUILayout.Height(40)))
                        manager.RunSimulation();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to run the simulation.", MessageType.Info);
            }

            GUI.backgroundColor = Color.white;

            // Show cluster result if available
            if (manager.ClusterResult != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("CLUSTER RESULT", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Dominant Cluster: {manager.ClusterResult.DominantCluster}");
            }

            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }
    }
}
#endif
