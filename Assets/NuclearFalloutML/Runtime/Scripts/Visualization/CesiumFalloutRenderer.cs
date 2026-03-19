using System;
using UnityEngine;
using NuclearFalloutML.Core;

using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Analysis;
using CSharpNumerics.Engines.GIS.Grid;
using CSharpNumerics.Numerics.Objects;

namespace NuclearFalloutML.Visualization
{
    /// <summary>
    /// Visualization layer for rendering CSharpNumerics GeoEngine results on a Cesium 3D globe.
    /// Creates textured overlays from ProbabilityMap / RiskScenarioResult data.
    /// Compatible with CesiumForUnity tile system.
    /// </summary>
    public class CesiumFalloutRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private Material _overlayMaterial;
        [SerializeField] private float _overlayAltitude = 50f;
        [SerializeField] private int _textureResolution = 512;

        [Header("Display Mode")]
        public FalloutDisplayMode DisplayMode = FalloutDisplayMode.Probability;

        [Header("References")]
        [SerializeField] private GameObject _overlayQuadPrefab;

        private Texture2D _currentTexture;
        private GameObject _overlayInstance;
        private ScenarioResult _scenarioResult;
        private SimulationConfig _config;
        private int _currentTimeIndex;

        public enum FalloutDisplayMode
        {
            Probability,
            CumulativeProbability,
            ClusterMap
        }

        /// <summary>
        /// Initialize the renderer with simulation config and CSharpNumerics results.
        /// </summary>
        public void Initialize(SimulationConfig config, ScenarioResult scenarioResult, int timeIndex = 0)
        {
            _config = config;
            _scenarioResult = scenarioResult;
            _currentTimeIndex = timeIndex;

            CreateOverlayTexture();
            UpdateVisualization();
        }

        /// <summary>
        /// Update visualization when display mode or time index changes.
        /// </summary>
        public void UpdateVisualization()
        {
            if (_scenarioResult == null) return;

            switch (DisplayMode)
            {
                case FalloutDisplayMode.Probability:
                    RenderProbabilityOverlay();
                    break;
                case FalloutDisplayMode.CumulativeProbability:
                    RenderCumulativeOverlay();
                    break;
                case FalloutDisplayMode.ClusterMap:
                    RenderClusterOverlay();
                    break;
            }

            ApplyTexture();
        }

        /// <summary>
        /// Change the time index and re-render.
        /// </summary>
        public void SetTimeIndex(int timeIndex)
        {
            _currentTimeIndex = timeIndex;
            UpdateVisualization();
        }

        private void CreateOverlayTexture()
        {
            _currentTexture = new Texture2D(_textureResolution, _textureResolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private void RenderProbabilityOverlay()
        {
            var probMap = _scenarioResult.ProbabilityMapAt(timeIndex: _currentTimeIndex);
            if (probMap == null) return;

            var grid = probMap.Grid;
            RenderFromGrid(grid, pos => probMap.At(pos), FalloutColorMapper.ProbabilityToColor);
        }

        private void RenderCumulativeOverlay()
        {
            var grid = _scenarioResult.Grid;
            if (grid == null) return;

            double timeSeconds = _config.TimeStartSeconds +
                _currentTimeIndex * _config.TimeStepSeconds;

            RenderFromGrid(grid, pos =>
                _scenarioResult.CumulativeProbabilityAt(pos, timeSeconds),
                FalloutColorMapper.ProbabilityToColor);
        }

        private void RenderClusterOverlay()
        {
            var probMap = _scenarioResult.ProbabilityMapAt(timeIndex: _currentTimeIndex);
            if (probMap == null) return;

            var grid = probMap.Grid;
            RenderFromGrid(grid, pos => probMap.At(pos),
                val => FalloutColorMapper.ClusterToColor((int)(val * 10), 10));
        }

        /// <summary>
        /// Generic grid rendering: sample values from the CSharpNumerics grid,
        /// map to colors, and fill the texture.
        /// </summary>
        private void RenderFromGrid(GeoGrid grid, Func<Vector, double> valueFn, Func<double, Color> colorFn)
        {
            Color[] pixels = new Color[_textureResolution * _textureResolution];
            int cellCount = grid.CellCount;

            // Compute grid bounds for texture mapping
            double xMin = double.MaxValue, xMax = double.MinValue;
            double yMin = double.MaxValue, yMax = double.MinValue;

            // Sample a few cells to determine 2D bounds
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
            if (xRange < 1e-6 || yRange < 1e-6) return;

            // Build a 2D lookup from grid cells
            // Map each cell to nearest texture pixel and aggregate
            for (int i = 0; i < cellCount; i++)
            {
                var pos = grid.CellCentre(i);
                double val = valueFn(pos);
                if (val < 1e-10) continue;

                int tx = (int)(((pos.x - xMin) / xRange) * (_textureResolution - 1));
                int ty = (int)(((pos.y - yMin) / yRange) * (_textureResolution - 1));
                tx = Mathf.Clamp(tx, 0, _textureResolution - 1);
                ty = Mathf.Clamp(ty, 0, _textureResolution - 1);

                pixels[ty * _textureResolution + tx] = colorFn(val);
            }

            _currentTexture.SetPixels(pixels);
            _currentTexture.Apply();
        }

        private void ApplyTexture()
        {
            if (_overlayMaterial != null)
            {
                _overlayMaterial.mainTexture = _currentTexture;
            }

            SetupOverlayGeometry();
        }

        /// <summary>
        /// Create or update the overlay quad positioned on the Cesium globe.
        /// </summary>
        private void SetupOverlayGeometry()
        {
            if (_config == null) return;

#if CESIUM_AVAILABLE
            var georeference = FindFirstObjectByType<CesiumForUnity.CesiumGeoreference>();
            if (georeference != null)
            {
                PositionOverlayOnGlobe(georeference);
            }
            else
            {
                PositionOverlayFlat();
            }
#else
            PositionOverlayFlat();
#endif
        }

#if CESIUM_AVAILABLE
        private void PositionOverlayOnGlobe(CesiumForUnity.CesiumGeoreference georeference)
        {
            if (_overlayInstance == null)
            {
                _overlayInstance = _overlayQuadPrefab != null
                    ? Instantiate(_overlayQuadPrefab, transform)
                    : CreateDefaultQuad();
            }

            double3 ecef = CesiumForUnity.CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                new double3(_config.SourceLongitude, _config.SourceLatitude, _overlayAltitude));

            _overlayInstance.transform.position = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);

            float scale = _config.GridExtentMeters * 2f;
            _overlayInstance.transform.localScale = new Vector3(scale, 1f, scale);

            var renderer = _overlayInstance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = _overlayMaterial;
                renderer.material.mainTexture = _currentTexture;
            }
        }
#endif

        private void PositionOverlayFlat()
        {
            if (_overlayInstance == null)
            {
                _overlayInstance = CreateDefaultQuad();
            }

            float scale = _config.GridExtentMeters * 2f / 1000f;
            _overlayInstance.transform.localScale = new Vector3(scale, 1f, scale);
            _overlayInstance.transform.position = new Vector3(0, _overlayAltitude / 1000f, 0);

            var meshRenderer = _overlayInstance.GetComponent<Renderer>();
            if (meshRenderer != null)
            {
                if (_overlayMaterial != null)
                {
                    meshRenderer.material = _overlayMaterial;
                }
                meshRenderer.material.mainTexture = _currentTexture;
            }
        }

        private GameObject CreateDefaultQuad()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(transform);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.name = "FalloutOverlay";

            var collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            return quad;
        }

        /// <summary>
        /// Get the current overlay texture for export.
        /// </summary>
        public Texture2D GetCurrentTexture() => _currentTexture;

        private void OnDestroy()
        {
            if (_currentTexture != null) Destroy(_currentTexture);
            if (_overlayInstance != null) Destroy(_overlayInstance);
        }
    }
}
