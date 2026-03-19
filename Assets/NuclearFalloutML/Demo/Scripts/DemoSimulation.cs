using UnityEngine;
using UnityEngine.UI;
using NuclearFalloutML.Core;
using NuclearFalloutML.Visualization;

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
    /// Demo that runs a CSharpNumerics fallout simulation and visualizes
    /// the probability heatmap on a quad in the scene.
    /// Attach to any GameObject → enter Play Mode → see the plume.
    ///
    /// Controls:
    ///   Left/Right arrow — step through time
    ///   Space — toggle probability / cluster view
    /// </summary>
    public class DemoSimulation : MonoBehaviour
    {
        [Header("Demo Settings")]
        [Tooltip("Number of Monte Carlo iterations (keep low for fast demo)")]
        [Range(10, 500)]
        public int iterations = 50;

        [Tooltip("Texture resolution for the heatmap")]
        [Range(64, 1024)]
        public int textureResolution = 256;

        [Tooltip("Run automatically on Start")]
        public bool autoRun = true;

        private ScenarioResult _result;
        private Texture2D _heatmapTexture;
        private RawImage _rawImage;
        private int _timeIndex;
        private int _maxTimeIndex;
        private bool _showClusters;
        private bool _running;

        private async void Start()
        {
            if (autoRun)
            {
                try
                {
                    await RunDemo();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Demo] FAILED: {ex}");
                }
            }
        }

        private void Update()
        {
            if (_result == null) return;

            bool changed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) && _timeIndex < _maxTimeIndex - 1)
            {
                _timeIndex++;
                changed = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) && _timeIndex > 0)
            {
                _timeIndex--;
                changed = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                _showClusters = !_showClusters;
                changed = true;
            }
#else
            // New Input System or both — use Keyboard
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame && _timeIndex < _maxTimeIndex - 1)
                {
                    _timeIndex++;
                    changed = true;
                }
                else if (kb.leftArrowKey.wasPressedThisFrame && _timeIndex > 0)
                {
                    _timeIndex--;
                    changed = true;
                }
                else if (kb.spaceKey.wasPressedThisFrame)
                {
                    _showClusters = !_showClusters;
                    changed = true;
                }
            }
#endif

            if (changed) RenderHeatmap();
        }

        public async System.Threading.Tasks.Task RunDemo()
        {
            _running = true;
            Debug.Log("[Demo] Running Monte Carlo simulation...");

            _result = await System.Threading.Tasks.Task.Run(() =>
                RiskScenario
                    .ForGaussianPlume(5.0)
                    .FromSource(new Vector(0, 0, 50))
                    .WithMode(PlumeMode.Transient, releaseSeconds: 30)
                    .WithWind(10, new Vector(1, 0, 0))
                    .WithStability(StabilityClass.D)
                    .WithMaterial(Materials.Radioisotope("Cs137"))
                    .WithVariation(v => v
                        .WindSpeed(8, 12)
                        .WindDirectionJitter(15)
                        .EmissionRate(3, 7)
                        .SetStabilityWeights(c: 0.2, d: 0.6, e: 0.2))
                    .OverGrid(new GeoGrid(-500, 8000, -2000, 2000, 0, 100, 50))
                    .OverTime(0, 600, 30)
                    .RunMonteCarlo(iterations, seed: 42)
                    .AnalyzeWith(new KMeans(), new SilhouetteEvaluator(), minK: 2, maxK: 4)
                    .Build(threshold: 1e-6));

            // Determine how many time steps are available
            _maxTimeIndex = (int)((600 - 0) / 30) + 1;
            _timeIndex = 0; // start at beginning to watch the puff emerge

            Debug.Log($"[Demo] Simulation complete. {_result.Grid.CellCount} grid cells, " +
                      $"{_maxTimeIndex} time steps.");
            Debug.Log($"[Demo] Cluster analysis: K={_result.ClusterAnalysis?.BestClusterCount}, " +
                      $"Score={_result.ClusterAnalysis?.BestScore:F4}");

            // Create visualization
            EnsureCamera();
            CreateHeatmapUI();
            RenderHeatmap();

            Debug.Log("[Demo] ✓ Visualization ready.");
            Debug.Log("[Demo] Controls: Left/Right = time step, Space = toggle clusters");

            _running = false;
        }

        private void EnsureCamera()
        {
            // Unity Game view needs at least one active camera
            if (Camera.main == null && FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 0)
            {
                var camObj = new GameObject("DemoCamera");
                var cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
                cam.cullingMask = 0; // render nothing in 3D, just clear the screen
            }
        }

        private void CreateHeatmapUI()
        {
            // Create a Screen Space Overlay canvas — works with any render pipeline
            var canvasObj = new GameObject("DemoCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            // Heatmap image — stretched to fill the screen
            var imgObj = new GameObject("HeatmapImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            _rawImage = imgObj.AddComponent<RawImage>();
            _rawImage.color = Color.white; // don't tint the texture

            var rt = _rawImage.rectTransform;
            rt.anchorMin = UnityEngine.Vector2.zero;
            rt.anchorMax = UnityEngine.Vector2.one;
            rt.offsetMin = UnityEngine.Vector2.zero;
            rt.offsetMax = UnityEngine.Vector2.zero;

            _heatmapTexture = new Texture2D(textureResolution, textureResolution,
                TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _rawImage.texture = _heatmapTexture;

            Debug.Log($"[Demo] Canvas created, RawImage size={rt.rect}, texture={textureResolution}x{textureResolution}");
        }

        private void RenderHeatmap()
        {
            if (_result == null || _heatmapTexture == null) return;

            var probMap = _result.ProbabilityMapAt(_timeIndex);
            if (probMap == null)
            {
                Debug.LogWarning($"[Demo] ProbabilityMapAt({_timeIndex}) returned null");
                return;
            }

            var grid = probMap.Grid;
            int res = textureResolution;
            var pixels = new Color[res * res];

            // Dark background (fully opaque so it's visible)
            var bg = new Color(0.05f, 0.05f, 0.08f, 1f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // Compute grid bounds
            double xMin = double.MaxValue, xMax = double.MinValue;
            double yMin = double.MaxValue, yMax = double.MinValue;

            int cellCount = grid.CellCount;
            for (int i = 0; i < cellCount; i++)
            {
                var c = grid.CellCentre(i);
                if (c.x < xMin) xMin = c.x;
                if (c.x > xMax) xMax = c.x;
                if (c.y < yMin) yMin = c.y;
                if (c.y > yMax) yMax = c.y;
            }

            double xRange = xMax - xMin;
            double yRange = yMax - yMin;
            if (xRange < 1e-6 || yRange < 1e-6)
            {
                Debug.LogWarning($"[Demo] Grid range too small: x={xRange}, y={yRange}");
                return;
            }

            // Paint cells — first pass: find max value for normalization
            double maxVal = 0;
            for (int i = 0; i < cellCount; i++)
            {
                double v = probMap.At(grid.CellCentre(i));
                if (v > maxVal) maxVal = v;
            }
            if (maxVal < 1e-20) maxVal = 1; // avoid division by zero

            int totalClusters = _result.ClusterAnalysis?.BestClusterCount ?? 3;
            int paintedCells = 0;
            for (int i = 0; i < cellCount; i++)
            {
                var pos = grid.CellCentre(i);
                double val = probMap.At(pos);
                if (val < 1e-10) continue;

                // Normalize to 0–1 so the color mapper produces visible colors
                double normalized = val / maxVal;

                Color color;
                if (_showClusters)
                    color = FalloutColorMapper.ClusterToColor((int)(normalized * totalClusters * 10) % totalClusters, totalClusters);
                else
                    color = FalloutColorMapper.ProbabilityToColor(normalized);

                color.a = 1f;
                paintedCells++;

                // Map grid position to texture pixel — spread across a small radius
                int cx = (int)(((pos.x - xMin) / xRange) * (res - 1));
                int cy = (int)(((pos.y - yMin) / yRange) * (res - 1));

                int radius = Mathf.Max(1, res / (int)System.Math.Sqrt(cellCount));
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int px = Mathf.Clamp(cx + dx, 0, res - 1);
                        int py = Mathf.Clamp(cy + dy, 0, res - 1);
                        int idx = py * res + px;
                        pixels[idx] = color;
                    }
                }
            }

            _heatmapTexture.SetPixels(pixels);
            _heatmapTexture.Apply();

            // Save to disk so we can verify the texture visually
            string pngPath = System.IO.Path.Combine(Application.dataPath, "..", "DemoHeatmap.png");
            System.IO.File.WriteAllBytes(pngPath, _heatmapTexture.EncodeToPNG());

            double timeSec = _timeIndex * 60;
            string mode = _showClusters ? "CLUSTERS" : "PROBABILITY";
            Debug.Log($"[Demo] Rendered t={timeSec}s [{mode}] step {_timeIndex}/{_maxTimeIndex - 1} — " +
                      $"{cellCount} cells, {paintedCells} painted, maxVal={maxVal:E3}, grid x=[{xMin:F0},{xMax:F0}] y=[{yMin:F0},{yMax:F0}]");
            Debug.Log($"[Demo] Texture saved to: {pngPath}");
        }

        private void OnGUI()
        {
            if (_running)
            {
                GUI.Label(new Rect(10, 10, 400, 30),
                    "<size=18><color=white>Running simulation...</color></size>",
                    new GUIStyle { richText = true });
                return;
            }

            if (_result == null) return;

            double timeSec = _timeIndex * 60;
            string mode = _showClusters ? "Cluster Map" : "Probability Map";

            GUI.Label(new Rect(10, 10, 500, 30),
                $"<size=16><color=white>{mode}  |  t = {timeSec}s  |  Step {_timeIndex}/{_maxTimeIndex - 1}</color></size>",
                new GUIStyle { richText = true });
            GUI.Label(new Rect(10, 35, 500, 25),
                "<size=12><color=#aaa>← → Time  |  Space = Toggle mode  |  MC iterations: " + iterations + "</color></size>",
                new GUIStyle { richText = true });
        }
    }
}
