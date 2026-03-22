using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using NuclearFalloutML.Core;
using NuclearFalloutML.Visualization;

using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Grid;
using CSharpNumerics.Engines.GIS.Analysis;
using CSharpNumerics.Engines.GIS.Simulation;
using CSharpNumerics.ML.Clustering;
using CSharpNumerics.ML.Clustering.Algorithms;
using CSharpNumerics.ML.Clustering.Evaluators;
using CSharpNumerics.ML.Clustering.Results;
using CSharpNumerics.ML.Scalers;
using CSharpNumerics.ML.DimensionalityReduction.Algorithms;
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
    ///   Space — play / pause auto-advance
    ///   C — cycle view: Probability → Concentration → Activity → Dose → Clusters
    ///   M — toggle ML statistics panel
    /// </summary>
    public class DemoSimulation : MonoBehaviour
    {
        private enum ViewMode { Probability, Concentration, Activity, Dose, Clusters }
        [Header("Simulation Parameters")]
        public SimulationConfig config = new SimulationConfig();

        [Header("Demo Settings")]
        [Tooltip("Texture resolution for the heatmap")]
        [Range(64, 2048)]
        public int textureResolution = 512;

        [Tooltip("Run automatically on Start")]
        public bool autoRun = true;

        private ScenarioResult _result;
        private Texture2D _heatmapTexture;
        private int _timeIndex;
        private int _maxTimeIndex;
        private ViewMode _viewMode = ViewMode.Probability;
        private bool _running;
        private bool _autoPlay = true;
        private float _playTimer;
        private float _secondsPerStep = 0.5f;
        private float _gridAspect = 1f;
        private Texture2D _legendTexture;
        private double _legendMaxVal;
        private string _legendUnit = "";
        private bool _showStats;
        private string[] _statsLines;
        private Texture2D _mapTexture;  // OSM map background
        private Color[] _mapPixels;     // cached map pixel data
        // Grid bounds in meters (stored after first RenderHeatmap)
        private double _gxMin, _gxMax, _gyMin, _gyMax;
        private float _overlayAlpha = 0.75f;

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

            // Auto-play: advance time step at a steady rate
            if (_autoPlay)
            {
                _playTimer += Time.deltaTime;
                if (_playTimer >= _secondsPerStep)
                {
                    _playTimer -= _secondsPerStep;
                    _timeIndex++;
                    if (_timeIndex >= _maxTimeIndex) _timeIndex = 0; // loop
                    changed = true;
                }
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                _timeIndex = Mathf.Min(_timeIndex + 1, _maxTimeIndex - 1);
                changed = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _timeIndex = Mathf.Max(_timeIndex - 1, 0);
                changed = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                _autoPlay = !_autoPlay;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                _viewMode = (ViewMode)(((int)_viewMode + 1) % 5);
                UpdateLegendTexture();
                changed = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.M))
            {
                _showStats = !_showStats;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                _overlayAlpha = Mathf.Min(_overlayAlpha + 0.1f, 1f);
                changed = true;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                _overlayAlpha = Mathf.Max(_overlayAlpha - 0.1f, 0.05f);
                changed = true;
            }
#elif HAS_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame)
                {
                    _timeIndex = Mathf.Min(_timeIndex + 1, _maxTimeIndex - 1);
                    changed = true;
                }
                else if (kb.leftArrowKey.wasPressedThisFrame)
                {
                    _timeIndex = Mathf.Max(_timeIndex - 1, 0);
                    changed = true;
                }
                else if (kb.spaceKey.wasPressedThisFrame)
                {
                    _autoPlay = !_autoPlay;
                }
                else if (kb.cKey.wasPressedThisFrame)
                {
                    _viewMode = (ViewMode)(((int)_viewMode + 1) % 5);
                    UpdateLegendTexture();
                    changed = true;
                }
                else if (kb.mKey.wasPressedThisFrame)
                {
                    _showStats = !_showStats;
                }
                else if (kb.upArrowKey.wasPressedThisFrame)
                {
                    _overlayAlpha = Mathf.Min(_overlayAlpha + 0.1f, 1f);
                    changed = true;
                }
                else if (kb.downArrowKey.wasPressedThisFrame)
                {
                    _overlayAlpha = Mathf.Max(_overlayAlpha - 0.1f, 0.05f);
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

            // Build grid to cover plume travel distance
            float ws = config.WindSpeedMs;
            float tEnd = (float)config.TimeEndSeconds;
            float travel = ws * tEnd;  // max downwind distance
            float spread = Mathf.Max(travel * 0.15f, config.GridExtentMeters);
            float cellSize = config.GridStepMeters;
            float xMin = -config.GridExtentMeters;
            float xMax = travel + config.GridExtentMeters;
            // Cap aspect ratio to ~2.5:1 so the heatmap fills the game window
            float totalX = xMax - xMin;
            spread = Mathf.Max(spread, totalX / 2.5f / 2f);
            float yMin = -spread;
            float yMax = spread;
            float zMax = config.GridAltitudeMaxMeters;

            var stability = (StabilityClass)(int)config.Stability;
            var plumeMode = config.PlumeMode == PlumeModeOption.Transient
                ? PlumeMode.Transient : PlumeMode.SteadyState;

            int iters = config.MonteCarloIterations;
            int seed = config.RandomSeed;
            double emRate = config.EmissionRateKgPerS;
            double srcAlt = config.SourceAltitudeMeters;
            double releaseS = config.PuffReleaseSeconds;
            double tStart = config.TimeStartSeconds;
            double tStep = config.TimeStepSeconds;
            float wdx = config.WindDirectionX;
            float wdy = config.WindDirectionY;
            float wsMin = config.WindSpeedVariationMin;
            float wsMax = config.WindSpeedVariationMax;
            float wdJitter = config.WindDirectionJitterDeg;
            double erMin = config.EmissionRateVariationMin;
            double erMax = config.EmissionRateVariationMax;
            float swC = config.StabilityWeightC;
            float swD = config.StabilityWeightD;
            float swE = config.StabilityWeightE;
            string isotope = config.Radioisotope;
            _result = await System.Threading.Tasks.Task.Run(() =>
            {
                Debug.Log("[Demo] Building scenario pipeline...");
                var clusterGrid = new ClusteringGrid()
                    .AddModel<KMeans>(g => g.Add("K", 2, 3))
                    .AddModel<DBSCAN>(g => g.Add("Epsilon", 0.5, 1.0).Add("MinPoints", 3));
                var scenario = RiskScenario
                    .ForGaussianPlume(emRate)
                    .FromSource(new Vector(0, 0, srcAlt))
                    .WithWind(ws, new Vector(wdx, wdy, 0))
                    .WithMode(plumeMode, releaseSeconds: releaseS)
                    .WithStability(stability)
                    .WithMaterial(Materials.Radioisotope(isotope))
                    .WithVariation(v => v
                        .WindSpeed(wsMin, wsMax)
                        .WindDirectionJitter(wdJitter)
                        .EmissionRate(erMin, erMax)
                        .SetStabilityWeights(c: swC, d: swD, e: swE))
                    .OverGrid(new GeoGrid(xMin, xMax, yMin, yMax, 0, zMax, cellSize))
                    .OverTime(tStart, tEnd, tStep);
                Debug.Log("[Demo] Starting Monte Carlo...");
                var mc = scenario.RunMonteCarlo(iters, seed: seed);
                Debug.Log("[Demo] Monte Carlo done. Starting ML clustering...");
                var analyzed = mc.AnalyzeWith(clusterGrid, new CalinskiHarabaszEvaluator());
                Debug.Log("[Demo] Clustering done. Building result...");
                return analyzed.Build(threshold: 1e-6);
            });

            _maxTimeIndex = (int)((tEnd - tStart) / tStep) + 1;
            _timeIndex = 0;

            Debug.Log($"[Demo] Simulation complete. {_result.Grid.CellCount} grid cells, " +
                      $"{_maxTimeIndex} time steps.");
            Debug.Log($"[Demo] Cluster analysis: K={_result.ClusterAnalysis?.BestClusterCount}, " +
                      $"Score={_result.ClusterAnalysis?.BestScore:F4}");

            // Build ML stats summary from experiment rankings
            BuildStatsLines();

            // Create visualization
            EnsureCamera();
            CreateHeatmapTexture();
            CreateLegendTexture();
            RenderHeatmap();

            // Start fetching map tiles in background (will re-render when done)
            StartCoroutine(FetchMapBackground());

            Debug.Log("[Demo] ✓ Visualization ready.");
            Debug.Log("[Demo] Controls: ←/→ = step, Space = play/pause, C = cycle view, M = ML stats");

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
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;
            }
        }

        private void CreateHeatmapTexture()
        {
            _heatmapTexture = new Texture2D(textureResolution, textureResolution,
                TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Debug.Log($"[Demo] Texture created: {textureResolution}x{textureResolution}");
        }

        #region Map Tile Fetching

        private IEnumerator FetchMapBackground()
        {
            double srcLat = config.SourceLatitude;
            double srcLon = config.SourceLongitude;

            // Convert grid bounds (meters from source) to lat/lon
            double metersPerDegLat = 111320.0;
            double metersPerDegLon = 111320.0 * System.Math.Cos(srcLat * System.Math.PI / 180.0);

            double latMin = srcLat + _gyMin / metersPerDegLat;
            double latMax = srcLat + _gyMax / metersPerDegLat;
            double lonMin = srcLon + _gxMin / metersPerDegLon;
            double lonMax = srcLon + _gxMax / metersPerDegLon;

            // Choose zoom level: aim for ~4-6 tiles across (fast load, sufficient detail)
            double lonSpan = lonMax - lonMin;
            int zoom = 1;
            for (int z = 18; z >= 1; z--)
            {
                double tilesAcross = lonSpan / (360.0 / (1 << z));
                if (tilesAcross <= 6) { zoom = z; break; }
            }

            // Convert lat/lon bounds to tile coordinates
            int txMin = LonToTileX(lonMin, zoom);
            int txMax = LonToTileX(lonMax, zoom);
            int tyMin = LatToTileY(latMax, zoom); // note: Y is inverted (north = lower tile Y)
            int tyMax = LatToTileY(latMin, zoom);

            int tilesX = txMax - txMin + 1;
            int tilesY = tyMax - tyMin + 1;
            int stitchW = tilesX * 256;
            int stitchH = tilesY * 256;

            Debug.Log($"[Demo] Fetching {tilesX}x{tilesY} map tiles at zoom {zoom}...");

            var stitched = new Texture2D(stitchW, stitchH, TextureFormat.RGB24, false);

            // Fill with water-blue fallback
            var fallback = new Color(0.68f, 0.78f, 0.85f);
            var fallbackPixels = new Color[stitchW * stitchH];
            for (int i = 0; i < fallbackPixels.Length; i++) fallbackPixels[i] = fallback;
            stitched.SetPixels(fallbackPixels);

            // Fire all tile requests in parallel
            var requests = new System.Collections.Generic.List<(UnityWebRequest req, int tx, int ty)>();
            for (int ty = tyMin; ty <= tyMax; ty++)
            {
                for (int tx = txMin; tx <= txMax; tx++)
                {
                    string url = $"https://tile.openstreetmap.org/{zoom}/{tx}/{ty}.png";
                    var req = UnityWebRequestTexture.GetTexture(url);
                    req.SetRequestHeader("User-Agent", "CSharpNumerics.Visualization/1.0 (Unity Demo)");
                    req.SendWebRequest();
                    requests.Add((req, tx, ty));
                }
            }

            // Wait for all to complete and stitch
            foreach (var (req, tx, ty) in requests)
            {
                while (!req.isDone)
                    yield return null;

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var tile = DownloadHandlerTexture.GetContent(req);
                    int destX = (tx - txMin) * 256;
                    // Tile Y increases downward, texture Y increases upward
                    int destY = (tyMax - ty) * 256;
                    stitched.SetPixels(destX, destY, 256, 256, tile.GetPixels());
                    Object.Destroy(tile);
                }
                else
                {
                    Debug.LogWarning($"[Demo] Failed to fetch tile {zoom}/{tx}/{ty}: {req.error}");
                }
                req.Dispose();
            }

            stitched.Apply();

            // Resample stitched tiles to heatmap resolution, mapping grid bounds to tile bounds
            double tileLatMin = TileYToLat(tyMax + 1, zoom);
            double tileLatMax = TileYToLat(tyMin, zoom);
            double tileLonMin = TileXToLon(txMin, zoom);
            double tileLonMax = TileXToLon(txMax + 1, zoom);

            int mapRes = 512;
            _mapTexture = new Texture2D(mapRes, mapRes, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _mapPixels = new Color[mapRes * mapRes];

            for (int py = 0; py < mapRes; py++)
            {
                double lat = latMin + (latMax - latMin) * py / (mapRes - 1);
                float v = (float)((lat - tileLatMin) / (tileLatMax - tileLatMin));
                for (int px = 0; px < mapRes; px++)
                {
                    double lon = lonMin + (lonMax - lonMin) * px / (mapRes - 1);
                    float u = (float)((lon - tileLonMin) / (tileLonMax - tileLonMin));
                    _mapPixels[py * mapRes + px] = stitched.GetPixelBilinear(u, v);
                }
            }
            _mapTexture.SetPixels(_mapPixels);
            _mapTexture.Apply();

            Object.Destroy(stitched);
            Debug.Log("[Demo] Map background loaded.");

            // Re-render with map background
            RenderHeatmap();
        }

        private static int LonToTileX(double lon, int zoom)
        {
            return (int)System.Math.Floor((lon + 180.0) / 360.0 * (1 << zoom));
        }

        private static int LatToTileY(double lat, int zoom)
        {
            double latRad = lat * System.Math.PI / 180.0;
            return (int)System.Math.Floor((1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI) / 2.0 * (1 << zoom));
        }

        private static double TileXToLon(int x, int zoom)
        {
            return x / (double)(1 << zoom) * 360.0 - 180.0;
        }

        private static double TileYToLat(int y, int zoom)
        {
            double n = System.Math.PI - 2.0 * System.Math.PI * y / (1 << zoom);
            return 180.0 / System.Math.PI * System.Math.Atan(0.5 * (System.Math.Exp(n) - System.Math.Exp(-n)));
        }

        #endregion

        private void UpdateLegendTexture()
        {
            int h = 256;
            var pixels = new Color[h];
            bool useLogScale = _viewMode == ViewMode.Concentration
                || _viewMode == ViewMode.Activity
                || _viewMode == ViewMode.Dose;

            for (int i = 0; i < h; i++)
            {
                float t = (float)i / (h - 1); // pixel 0 = bottom of texture = low, pixel h-1 = top = high
                Color c;
                if (useLogScale)
                    c = FalloutColorMapper.DoseToColor(t, 1.0);
                else
                    c = FalloutColorMapper.ProbabilityToColor(t);
                c.a = 1f;
                pixels[i] = c;
            }
            _legendTexture.SetPixels(pixels);
            _legendTexture.Apply();
        }

        private void CreateLegendTexture()
        {
            int w = 1;
            int h = 256;
            _legendTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            UpdateLegendTexture();
        }

        private void RenderHeatmap()
        {
            if (_result == null || _heatmapTexture == null) return;

            var probMap = _result.ProbabilityMapAt(_timeIndex);
            if (probMap == null) return;

            // Get snapshot for concentration/activity/dose layers
            // Monte Carlo: snapshots are per-iteration; use first iteration as representative
            GridSnapshot snapshot = null;
            double[] layerValues = null;
            if (_viewMode == ViewMode.Concentration || _viewMode == ViewMode.Activity || _viewMode == ViewMode.Dose)
            {
                var mcSnapshots = _result.MonteCarloResult?.Snapshots;
                if (mcSnapshots != null && mcSnapshots.Count > 0 && _timeIndex < mcSnapshots[0].Count)
                {
                    snapshot = mcSnapshots[0][_timeIndex];
                    string layerName = _viewMode == ViewMode.Activity ? "activity"
                                    : _viewMode == ViewMode.Dose ? "dose" : null;
                    if (layerName != null && snapshot.HasLayer(layerName))
                        layerValues = snapshot.GetLayer(layerName);
                }
            }

            var grid = probMap.Grid;
            int res = textureResolution;
            var pixels = new Color[res * res];

            var bg = new Color(0.05f, 0.05f, 0.08f, 1f);
            // Use map tiles as background if available, otherwise dark
            if (_mapPixels != null)
            {
                int mapRes = (int)Mathf.Sqrt(_mapPixels.Length);
                for (int py2 = 0; py2 < res; py2++)
                {
                    float v2 = (float)py2 / (res - 1);
                    for (int px2 = 0; px2 < res; px2++)
                    {
                        float u2 = (float)px2 / (res - 1);
                        int mx = Mathf.Clamp((int)(u2 * (mapRes - 1)), 0, mapRes - 1);
                        int my = Mathf.Clamp((int)(v2 * (mapRes - 1)), 0, mapRes - 1);
                        pixels[py2 * res + px2] = _mapPixels[my * mapRes + mx];
                    }
                }
            }
            else
                for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // Compute grid bounds
            double gxMin = double.MaxValue, gxMax = double.MinValue;
            double gyMin = double.MaxValue, gyMax = double.MinValue;

            int cellCount = grid.CellCount;
            for (int i = 0; i < cellCount; i++)
            {
                var c = grid.CellCentre(i);
                if (c.x < gxMin) gxMin = c.x;
                if (c.x > gxMax) gxMax = c.x;
                if (c.y < gyMin) gyMin = c.y;
                if (c.y > gyMax) gyMax = c.y;
            }

            double xRange = gxMax - gxMin;
            double yRange = gyMax - gyMin;
            if (xRange < 1e-6 || yRange < 1e-6) return;

            _gridAspect = (float)(xRange / yRange);
            _gxMin = gxMin; _gxMax = gxMax; _gyMin = gyMin; _gyMax = gyMax;

            // First pass: find max value for normalization
            double maxVal = 0;
            for (int i = 0; i < cellCount; i++)
            {
                double v = GetCellValue(i, probMap, snapshot, layerValues, grid);
                if (v > maxVal) maxVal = v;
            }
            if (maxVal < 1e-30) maxVal = 1;
            _legendMaxVal = maxVal;
            _legendUnit = GetUnitLabel();

            bool useLogScale = _viewMode == ViewMode.Concentration
                || _viewMode == ViewMode.Activity
                || _viewMode == ViewMode.Dose;

            int totalClusters = _result.ClusterAnalysis?.BestClusterCount ?? 3;
            float cellSz = config.GridStepMeters;

            for (int i = 0; i < cellCount; i++)
            {
                var pos = grid.CellCentre(i);
                double val = GetCellValue(i, probMap, snapshot, layerValues, grid);
                if (val < 1e-30) continue;

                double normalized;
                Color color;

                if (_viewMode == ViewMode.Clusters)
                {
                    normalized = (probMap.At(pos)) / maxVal;
                    color = FalloutColorMapper.ClusterToColor(
                        (int)(normalized * totalClusters * 10) % totalClusters, totalClusters);
                }
                else if (useLogScale)
                {
                    color = FalloutColorMapper.DoseToColor(val, maxVal);
                }
                else
                {
                    normalized = val / maxVal;
                    color = FalloutColorMapper.ProbabilityToColor(normalized);
                }

                color.a = _overlayAlpha;

                int cx = (int)(((pos.x - gxMin) / xRange) * (res - 1));
                int cy = (int)(((pos.y - gyMin) / yRange) * (res - 1));

                int rx = Mathf.Max(1, (int)Mathf.Ceil(res / (float)(xRange / cellSz) * 0.6f));
                int ry = Mathf.Max(1, (int)Mathf.Ceil(res / (float)(yRange / cellSz) * 0.6f));
                for (int dy = -ry; dy <= ry; dy++)
                {
                    for (int dx = -rx; dx <= rx; dx++)
                    {
                        int px = Mathf.Clamp(cx + dx, 0, res - 1);
                        int py = Mathf.Clamp(cy + dy, 0, res - 1);
                        pixels[py * res + px] = Color.Lerp(pixels[py * res + px], color, color.a);
                    }
                }
            }

            _heatmapTexture.SetPixels(pixels);
            _heatmapTexture.Apply();
        }

        private double GetCellValue(int cellIndex, ProbabilityMap probMap,
            GridSnapshot snapshot, double[] layerValues, GeoGrid grid)
        {
            switch (_viewMode)
            {
                case ViewMode.Concentration:
                    return snapshot != null ? snapshot[cellIndex] : 0;
                case ViewMode.Activity:
                case ViewMode.Dose:
                    return layerValues != null && cellIndex < layerValues.Length
                        ? layerValues[cellIndex] : 0;
                default: // Probability & Clusters
                    return probMap.At(grid.CellCentre(cellIndex));
            }
        }

        private string GetUnitLabel()
        {
            switch (_viewMode)
            {
                case ViewMode.Concentration: return "kg/m\u00b3";
                case ViewMode.Activity:      return "Bq/m\u00b3";
                case ViewMode.Dose:          return "Sv";
                default:                     return "";
            }
        }

        private void OnGUI()
        {
            // Black background fills entire game window
            var prevColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            if (_running)
            {
                GUI.Label(new Rect(10, 10, 400, 30),
                    "<size=18><color=white>Running simulation...</color></size>",
                    new GUIStyle { richText = true });
                return;
            }

            if (_result == null || _heatmapTexture == null) return;

            // Draw heatmap texture — match grid aspect ratio
            float sw = Screen.width;
            float sh = Screen.height;
            float margin = 10f;
            float topBar = 60f;
            float legendW = 90f; // space for legend bar + labels to the right
            float statsH = (_showStats && _statsLines != null) ? (_statsLines.Length + 1) * 16f + 20f : 0f;
            float availW = sw - margin * 2 - legendW;
            float availH = sh - topBar - margin - statsH;
            float drawW, drawH;
            if (_gridAspect >= 1f)
            {
                drawW = Mathf.Min(availW, availH * _gridAspect);
                drawH = drawW / _gridAspect;
            }
            else
            {
                drawH = Mathf.Min(availH, availW / _gridAspect);
                drawW = drawH * _gridAspect;
            }
            float x = margin + (availW - drawW) * 0.5f;
            float y = topBar;
            GUI.DrawTexture(new Rect(x, y, drawW, drawH), _heatmapTexture, ScaleMode.StretchToFill);

            // Color legend bar (to the right of heatmap with dark background)
            if (_legendTexture != null)
            {
                float barW = 16f;
                float barH = drawH * 0.85f;
                float barX = x + drawW + 12f;
                float barY = y + (drawH - barH) * 0.5f;

                // Dark panel behind legend
                var oc = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.8f);
                GUI.DrawTexture(new Rect(barX - 4, barY - 18, legendW - 8, barH + 36), Texture2D.whiteTexture);
                GUI.color = oc;

                GUI.DrawTexture(new Rect(barX, barY, barW, barH), _legendTexture, ScaleMode.StretchToFill);

                var labelStyle = new GUIStyle { richText = true, alignment = TextAnchor.MiddleLeft };
                float labelX = barX + barW + 4f;

                if (_viewMode == ViewMode.Clusters)
                {
                    int k = _result.ClusterAnalysis?.BestClusterCount ?? 3;
                    float step = barH / k;
                    for (int ci = 0; ci < k; ci++)
                    {
                        float ly = barY + barH - (ci + 0.5f) * step;
                        GUI.Label(new Rect(labelX, ly - 10, 100, 20),
                            $"<size=11><color=white>Zone {ci + 1}</color></size>", labelStyle);
                    }
                }
                else if (_viewMode == ViewMode.Probability)
                {
                    GUI.Label(new Rect(labelX, barY - 2, 80, 20),
                        "<size=11><color=white>High</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH * 0.25f - 8, 80, 20),
                        "<size=10><color=#ccc>75%</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH * 0.5f - 8, 80, 20),
                        "<size=10><color=#ccc>50%</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH * 0.75f - 8, 80, 20),
                        "<size=10><color=#ccc>25%</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH - 16, 80, 20),
                        "<size=11><color=white>Low</color></size>", labelStyle);
                }
                else
                {
                    // Log-scale legend for Concentration / Activity / Dose
                    GUI.Label(new Rect(labelX, barY - 2, 140, 20),
                        $"<size=10><color=white>{FormatSI(_legendMaxVal)} {_legendUnit}</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH * 0.25f - 8, 140, 20),
                        $"<size=9><color=#ccc>{FormatSI(_legendMaxVal * 1e-1)}</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH * 0.5f - 8, 140, 20),
                        $"<size=9><color=#ccc>{FormatSI(_legendMaxVal * 1e-2)}</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH * 0.75f - 8, 140, 20),
                        $"<size=9><color=#ccc>{FormatSI(_legendMaxVal * 1e-3)}</color></size>", labelStyle);
                    GUI.Label(new Rect(labelX, barY + barH - 16, 140, 20),
                        $"<size=10><color=white>{FormatSI(_legendMaxVal * 1e-4)} {_legendUnit}</color></size>", labelStyle);
                }
            }

            // HUD overlay
            double timeSec = _timeIndex * config.TimeStepSeconds;
            string mode = _viewMode.ToString();
            if (_viewMode != ViewMode.Probability && _viewMode != ViewMode.Clusters)
                mode += $" ({_legendUnit})";
            string playState = _autoPlay ? "▶" : "❚❚";

            GUI.Label(new Rect(10, 10, 900, 30),
                $"<size=18><color=white><b>Nuclear Fallout — {config.Radioisotope}</b>  |  {mode}  |  t = {timeSec}s  |  Step {_timeIndex}/{_maxTimeIndex - 1}  {playState}</color></size>",
                new GUIStyle { richText = true });
            GUI.Label(new Rect(10, 38, 700, 25),
                "<size=12><color=#aaa>← → Step  |  ↑↓ Opacity  |  Space = Play/Pause  |  C = Cycle View  |  M = ML Stats  |  MC: " + config.MonteCarloIterations + $"  |  α={_overlayAlpha:P0}</color></size>",
                new GUIStyle { richText = true });

            // Map attribution (required by OSM)
            if (_mapTexture != null)
            {
                GUI.Label(new Rect(sw - 250, sh - 20, 240, 18),
                    "<size=9><color=#888>© OpenStreetMap contributors</color></size>",
                    new GUIStyle { richText = true, alignment = TextAnchor.LowerRight });
            }

            // ML Statistics panel
            if (_showStats && _statsLines != null)
                DrawStatsPanel(margin, topBar + drawH + 6f, sw - margin * 2);
        }

        private static string FormatSI(double v)
        {
            if (v == 0) return "0";
            double abs = System.Math.Abs(v);
            if (abs >= 1e12) return $"{v / 1e12:F1}T";
            if (abs >= 1e9)  return $"{v / 1e9:F1}G";
            if (abs >= 1e6)  return $"{v / 1e6:F1}M";
            if (abs >= 1e3)  return $"{v / 1e3:F1}k";
            if (abs >= 1)    return $"{v:F2}";
            if (abs >= 1e-3) return $"{v * 1e3:F1}m";
            if (abs >= 1e-6) return $"{v * 1e6:F1}µ";
            if (abs >= 1e-9) return $"{v * 1e9:F1}n";
            return $"{v:E1}";
        }

        private void BuildStatsLines()
        {
            var ca = _result.ClusterAnalysis;
            if (ca?.ExperimentResult?.Rankings == null) { _statsLines = null; return; }

            var exp = ca.ExperimentResult;
            var lines = new System.Collections.Generic.List<string>();

            lines.Add($"<b>ML Clustering Analysis</b>  —  {exp.Rankings.Count} pipelines evaluated in {exp.TotalDuration.TotalSeconds:F1}s");
            lines.Add("");

            // Best result summary
            var best = exp.Best;
            if (best != null)
            {
                lines.Add($"<b>★ Best:</b> {best.AlgorithmName}  K={best.ClusterCount}  " +
                          $"Score={GetScore(best, "CalinskiHarabasz")}  " +
                          $"Duration={best.Duration.TotalMilliseconds:F0}ms");
                if (best.Parameters != null)
                {
                    var pStr = string.Join(", ", System.Linq.Enumerable.Select(best.Parameters, kv => $"{kv.Key}={kv.Value}"));
                    lines.Add($"     Params: {pStr}");
                }
                lines.Add($"     Dominant cluster: {ca.DominantCluster} (most likely outcome)");
            }
            lines.Add("");

            // Rankings table header
            lines.Add("HEADER");

            int rank = 0;
            foreach (var r in exp.Rankings)
            {
                if (rank >= 10) break;
                string algo = r.AlgorithmName ?? "?";
                string k = r.ClusterCount.ToString();
                string score = GetScore(r, "CalinskiHarabasz");
                string dur = $"{r.Duration.TotalMilliseconds:F0}ms";
                lines.Add($"ROW|{rank}|{algo}|{k}|{score}|{dur}");
                rank++;
            }

            _statsLines = lines.ToArray();
        }

        private static string GetScore(ClusteringResult r, string name)
        {
            if (r.Scores != null && r.Scores.TryGetValue(name, out double v))
                return v.ToString("F4");
            return "—";
        }

        private void DrawStatsPanel(float px, float py, float pw)
        {
            float lineH = 16f;
            float panelH = (_statsLines.Length + 1) * lineH + 10f;

            var bgTex = Texture2D.whiteTexture;
            var oldColor = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.DrawTexture(new Rect(px, py, pw, panelH), bgTex);
            GUI.color = oldColor;

            var style = new GUIStyle { richText = true };
            float yy = py + 5f;

            // Column X offsets for the table
            float col0 = px + 10;  // #
            float col1 = col0 + 30; // Algorithm
            float col2 = col1 + 200; // K
            float col3 = col2 + 40;  // Score
            float col4 = col3 + 80;  // Time

            foreach (var line in _statsLines)
            {
                if (line == "HEADER")
                {
                    DrawCol(col0, yy, 30, "<b>#</b>", style);
                    DrawCol(col1, yy, 200, "<b>Algorithm</b>", style);
                    DrawCol(col2, yy, 40, "<b>K</b>", style);
                    DrawCol(col3, yy, 80, "<b>Score</b>", style);
                    DrawCol(col4, yy, 60, "<b>Time</b>", style);
                }
                else if (line.StartsWith("ROW|"))
                {
                    var parts = line.Split('|');
                    int rank = int.Parse(parts[1]) + 1;
                    string tag = rank == 1 ? "\u2605" : " ";
                    DrawCol(col0, yy, 30, $"{tag}{rank}", style);
                    DrawCol(col1, yy, 200, ShortenAlgo(parts[2]), style);
                    DrawCol(col2, yy, 40, parts[3], style);
                    DrawCol(col3, yy, 80, parts[4], style);
                    DrawCol(col4, yy, 60, parts[5], style);
                }
                else
                {
                    GUI.Label(new Rect(px + 10, yy, pw - 20, lineH),
                        $"<size=11><color=white>{line}</color></size>", style);
                }
                yy += lineH;
            }
        }

        private static string ShortenAlgo(string name)
        {
            if (name == null) return "?";
            return name.Replace("AgglomerativeClustering", "Agglomerative")
                       .Replace("Agglomerative Clustering", "Agglomerative");
        }

        private static void DrawCol(float cx, float cy, float cw, string text, GUIStyle style)
        {
            GUI.Label(new Rect(cx, cy, cw, 16f),
                $"<size=11><color=white>{text}</color></size>", style);
        }
    }
}
