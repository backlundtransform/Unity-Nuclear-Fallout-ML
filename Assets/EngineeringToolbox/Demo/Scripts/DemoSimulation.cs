using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
using EngineeringToolbox.Core;
using EngineeringToolbox.Visualization;
using CSharpNumerics.Engines.Multiphysics;
using CSharpNumerics.Engines.Multiphysics.Enums;
using CSharpNumerics.Engines.Multiphysics.Snapshots;

namespace EngineeringToolbox.Demo
{
    /// <summary>
    /// Self-contained Engineering Toolbox demo.
    /// Attach to any empty GameObject → enter Play Mode → done.
    ///
    /// Modules:
    ///   1 = Heat Transfer (2D heatmap, animated)
    ///   2 = Electrostatics (potential heatmap + E-field vectors)
    ///   3 = Pipe Flow (1D velocity profile)
    ///   4 = Beam Stress (deflection, moment, shear, stress)
    ///
    /// Controls:
    ///   1-4       — select module
    ///   Space     — play / pause timeline
    ///   ←/→       — step backward / forward
    ///   R         — re-run simulation
    ///   M         — cycle material
    ///   V         — toggle vector overlay (electrostatics)
    ///   F12       — screenshot
    /// </summary>
    public class DemoSimulation : MonoBehaviour
    {
        [Header("Configuration")]
        public SimulationConfig config = new SimulationConfig();

        [Header("Demo Settings")]
        [Tooltip("Play timeline automatically on start")]
        public bool autoPlay = true;
        [Range(0.01f, 0.5f)]
        public float playInterval = 0.05f;

        // ── State ────────────────────────────────────────────────
        private SimulationResult _result;
        private SimulationTimeline _timeline;
        private int _currentFrame;
        private bool _playing;
        private float _playTimer;
        private bool _showVectors = true;
        private int _materialIndex;

        // ── UI ───────────────────────────────────────────────────
        private Canvas _canvas;
        private RawImage _heatmapImage;
        private Text _infoText;
        private Text _statusText;
        private HeatmapRenderer _heatmapRenderer;
        private VectorFieldOverlay _vectorOverlay;

        // ── Material cycle list ──────────────────────────────────
        private static readonly MaterialPreset[] Materials = new[]
        {
            MaterialPreset.Steel,
            MaterialPreset.Aluminum,
            MaterialPreset.Copper,
            MaterialPreset.Concrete,
            MaterialPreset.Glass,
            MaterialPreset.Water,
            MaterialPreset.Air
        };

        // ══════════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════════

        private async void Start()
        {
            EnsureCamera();
            CreateCanvas();
            _heatmapRenderer = new HeatmapRenderer(config.nx, config.ny);
            _vectorOverlay = new VectorFieldOverlay();
            _playing = autoPlay;

            await RunSimulation();
        }

        private void Update()
        {
            HandleInput();

            if (_playing && _timeline != null && _timeline.Count > 0)
            {
                _playTimer += Time.deltaTime;
                if (_playTimer >= playInterval)
                {
                    _playTimer = 0f;
                    _currentFrame = (_currentFrame + 1) % _timeline.Count;
                    UpdateVisualization();
                }
            }

            UpdateStatusBar();
        }

        // ══════════════════════════════════════════════════════════
        // Input
        // ══════════════════════════════════════════════════════════

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { config.module = PhysicsModule.HeatTransfer; _ = RunSimulation(); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { config.module = PhysicsModule.Electrostatics; _ = RunSimulation(); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { config.module = PhysicsModule.PipeFlow; _ = RunSimulation(); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { config.module = PhysicsModule.BeamStress; _ = RunSimulation(); }

            if (Input.GetKeyDown(KeyCode.Space)) _playing = !_playing;
            if (Input.GetKeyDown(KeyCode.RightArrow) && _timeline != null)
            {
                _currentFrame = Mathf.Min(_currentFrame + 1, _timeline.Count - 1);
                UpdateVisualization();
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow) && _timeline != null)
            {
                _currentFrame = Mathf.Max(_currentFrame - 1, 0);
                UpdateVisualization();
            }
            if (Input.GetKeyDown(KeyCode.R)) { _ = RunSimulation(); }
            if (Input.GetKeyDown(KeyCode.M)) { CycleMaterial(); _ = RunSimulation(); }
            if (Input.GetKeyDown(KeyCode.V)) { _showVectors = !_showVectors; UpdateVisualization(); }
            if (Input.GetKeyDown(KeyCode.F12)) CaptureScreenshot();
        }

        private void CycleMaterial()
        {
            _materialIndex = (_materialIndex + 1) % Materials.Length;
            config.materialPreset = Materials[_materialIndex];
        }

        // ══════════════════════════════════════════════════════════
        // Simulation
        // ══════════════════════════════════════════════════════════

        private async Task RunSimulation()
        {
            _currentFrame = 0;
            _infoText.text = $"Running {config.module}...";

            var material = config.GetMaterial();

            SimulationResult result = null;

            await Task.Run(() =>
            {
                switch (config.module)
                {
                    case PhysicsModule.HeatTransfer:
                        result = SimulationType.Create(MultiphysicsType.HeatPlate)
                            .WithMaterial(material)
                            .WithGeometry(config.width, config.height, config.nx, config.ny)
                            .WithBoundary(top: config.topBC, bottom: config.bottomBC,
                                          left: config.leftBC, right: config.rightBC)
                            .WithInitialCondition(0.0)
                            .Solve(dt: config.dt, steps: config.steps);
                        break;

                    case PhysicsModule.Electrostatics:
                        var builder = SimulationType.Create(MultiphysicsType.ElectricField)
                            .WithMaterial(material)
                            .WithGeometry(config.width, config.height, config.nx, config.ny)
                            .WithBoundary(top: config.topBC, bottom: config.bottomBC,
                                          left: config.leftBC, right: config.rightBC);
                        // Add a central charge for the demo
                        builder = builder.AddSource(config.nx / 2, config.ny / 2, 1e-6);
                        result = builder.Solve(maxIterations: 20000, tolerance: 1e-8);
                        break;

                    case PhysicsModule.PipeFlow:
                        result = SimulationType.Create(MultiphysicsType.PipeFlow)
                            .WithMaterial(material)
                            .WithGeometry(length: config.length, radius: config.radius, nodes: config.nodes)
                            .WithBoundary(pressureGradient: config.pressureGradient)
                            .Solve(dt: config.dt, steps: config.steps);
                        break;

                    case PhysicsModule.BeamStress:
                        var beamBuilder = SimulationType.Create(MultiphysicsType.BeamStress)
                            .WithMaterial(material)
                            .WithGeometry(length: config.length, nodes: config.nodes)
                            .WithCrossSection(width: config.sectionWidth, height: config.sectionHeight)
                            .WithBoundary(config.beamSupport);
                        if (config.pointLoadValue > 0)
                            beamBuilder = beamBuilder.AddSource(
                                position: config.pointLoadPosition * config.length,
                                value: config.pointLoadValue);
                        if (config.distributedLoad > 0)
                            beamBuilder = beamBuilder.WithSource(config.distributedLoad);
                        result = beamBuilder.Solve();
                        break;
                }
            });

            _result = result;

            // Build timeline for 2D transient modules
            if (_result != null && _result.Timeline != null && _result.Timeline.Count > 0)
            {
                _timeline = SimulationTimeline.FromResult(_result,
                    dt: config.dt,
                    dx: config.width / config.nx,
                    dy: config.height / config.ny);
            }
            else if (_result != null && _result.Timeline1D != null && _result.Timeline1D.Count > 0)
            {
                _timeline = null; // 1D timeline handled separately
            }
            else
            {
                _timeline = null;
            }

            Debug.Log($"[EngineeringToolbox] {config.module} complete — " +
                      $"material={material.Name}, max={_result?.MaxValue:F3}, min={_result?.MinValue:F3}, " +
                      $"iterations={_result?.Iterations}");

            UpdateVisualization();
        }

        // ══════════════════════════════════════════════════════════
        // Visualization
        // ══════════════════════════════════════════════════════════

        private void UpdateVisualization()
        {
            if (_result == null) return;

            switch (config.module)
            {
                case PhysicsModule.HeatTransfer:
                    Render2DField(_timeline != null && _currentFrame < _timeline.Count
                        ? _timeline[_currentFrame].ToArray()
                        : _result.Field);
                    break;

                case PhysicsModule.Electrostatics:
                    Render2DField(_result.Field);
                    if (_showVectors && _result.Ex != null && _result.Ey != null)
                        _vectorOverlay.Render(_heatmapImage, _result.Ex, _result.Ey, config.nx, config.ny);
                    break;

                case PhysicsModule.PipeFlow:
                case PhysicsModule.BeamStress:
                    Render1DProfile();
                    break;
            }

            UpdateInfoText();
        }

        private void Render2DField(double[,] field)
        {
            if (field == null) return;
            var tex = _heatmapRenderer.Render(field);
            _heatmapImage.texture = tex;
        }

        private void Render1DProfile()
        {
            if (_result == null || _result.Values == null) return;

            // For 1D data, render as a single-row heatmap stretched vertically
            int n = _result.Values.Length;
            var field = new double[n, 1];
            for (int i = 0; i < n; i++)
                field[i, 0] = _result.Values[i];

            _heatmapRenderer.Resize(n, 1);
            var tex = _heatmapRenderer.Render(field);
            _heatmapImage.texture = tex;
        }

        // ══════════════════════════════════════════════════════════
        // UI helpers
        // ══════════════════════════════════════════════════════════

        private void UpdateInfoText()
        {
            if (_result == null) return;

            string info = $"<b>{config.module}</b>  |  Material: {config.GetMaterial().Name}\n";
            info += $"Max: {_result.MaxValue:F3}  Min: {_result.MinValue:F3}";

            if (_timeline != null)
                info += $"  |  Frame {_currentFrame + 1}/{_timeline.Count}";

            if (config.module == PhysicsModule.BeamStress)
                info += $"\nSupport: {config.beamSupport}  Load: {config.pointLoadValue:F0} N";

            _infoText.text = info;
        }

        private void UpdateStatusBar()
        {
            string status = $"[1-4] Module  [Space] {(_playing ? "Pause" : "Play")}  " +
                            $"[←→] Step  [R] Reset  [M] Material  [V] Vectors  [F12] Screenshot";
            _statusText.text = status;
        }

        // ══════════════════════════════════════════════════════════
        // Bootstrap (zero-setup)
        // ══════════════════════════════════════════════════════════

        private void EnsureCamera()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.backgroundColor = new Color(0.12f, 0.12f, 0.16f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.orthographic = true;
                cam.orthographicSize = 5;
            }
        }

        private void CreateCanvas()
        {
            // Canvas
            var canvasGo = new GameObject("EngineeringToolbox_Canvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Heatmap image
            var imgGo = new GameObject("Heatmap");
            imgGo.transform.SetParent(canvasGo.transform, false);
            _heatmapImage = imgGo.AddComponent<RawImage>();
            var rt = _heatmapImage.rectTransform;
            rt.anchorMin = new Vector2(0.05f, 0.15f);
            rt.anchorMax = new Vector2(0.95f, 0.85f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Info text (top)
            _infoText = CreateText(canvasGo.transform, "InfoText",
                new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.98f), 16);

            // Status bar (bottom)
            _statusText = CreateText(canvasGo.transform, "StatusBar",
                new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.12f), 13);
            _statusText.color = new Color(0.7f, 0.7f, 0.7f);
        }

        private Text CreateText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.fontSize = fontSize;
            txt.color = Color.white;
            txt.alignment = TextAnchor.UpperLeft;
            txt.supportRichText = true;
            var r = txt.rectTransform;
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return txt;
        }

        private void CaptureScreenshot()
        {
            string path = $"EngineeringToolbox_{config.module}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[EngineeringToolbox] Screenshot saved: {path}");
        }
    }
}
