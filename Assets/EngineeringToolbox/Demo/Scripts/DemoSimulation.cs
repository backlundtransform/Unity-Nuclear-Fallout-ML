using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
using EngineeringToolbox.Core;
using EngineeringToolbox.Visualization;
using CSharpNumerics.Engines.Multiphysics;
using CSharpNumerics.Engines.Multiphysics.Enums;
using CSharpNumerics.Engines.Multiphysics.Snapshots;
using System.Collections.Generic;

namespace EngineeringToolbox.Demo
{
    /// <summary>
    /// Self-contained Engineering Toolbox demo.
    /// Attach to any empty GameObject, enter Play Mode, done.
    ///
    /// Modules:
    ///   1 = Heat Transfer (2D heatmap, animated)
    ///   2 = Electrostatics (potential heatmap + E-field vectors)
    ///   3 = Pipe Flow (1D velocity profile)
    ///   4 = Beam Stress (deflection, moment, shear, stress)
    ///
    /// Controls:
    ///   1-4        select module
    ///   Space      play / pause timeline
    ///   Left/Right step backward / forward
    ///   R          re-run simulation
    ///   M          cycle material
    ///   V          toggle vector overlay (electrostatics)
    ///   F12        screenshot
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
        [Range(0.1f, 4f)]
        public float electrostaticVectorSpeed = 1.15f;

        private SimulationResult _result;
        private SimulationTimeline _timeline;
        private List<double[]> _timeline1D;
        private int _currentFrame;
        private bool _playing;
        private float _playTimer;
        private bool _showVectors = true;
        private int _materialIndex;
        private double _globalMin;
        private double _globalMax;
        private double[,] _activeVectorFieldX;
        private double[,] _activeVectorFieldY;
        private string _configFingerprint;
        private PhysicsModule _lastObservedModule;
        private bool _isRunningSimulation;
        private bool _isDraggingVolume;
        private Vector2 _dragStartMousePosition;
        private Vector3 _dragStartVolumeRotation;
        private VolumeMoveAxis _activeMoveAxis;

        private Canvas _canvas;
        private Text _infoText;
        private Text _statusText;
        private Text _titleText;
        private Text _legendTitleText;
        private Text _legendTopValueText;
        private Text _legendBottomValueText;
        private RawImage _legendImage;
        private RectTransform _chartOverlay;
        private RectTransform _chartPanel;
        private Text _chartTitleText;
        private RawImage _chartImage;
        private Text _chartFooterText;
        private Button _chartToggleButton;
        private Text _chartToggleButtonText;
        private Image _materialSwatchImage;
        private Text _materialSwatchText;
        private Button _playPauseButton;
        private Text _playPauseButtonText;
        private Button _previousFrameButton;
        private Button _nextFrameButton;
        private Button _vectorToggleButton;
        private Text _vectorToggleButtonText;
        private Button _materialButton;
        private Button[] _moduleButtons;
        private Text[] _moduleButtonTexts;
        private HeatmapRenderer _heatmapRenderer;
        private VectorFieldOverlay _vectorOverlay;
        private SimulationVolumeView _volumeView;
        private Texture2D _chartTexture;
        private double[] _chartSeries;
        private bool _chartOverlayOpen;

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

        private static readonly PhysicsModule[] ModuleOrder =
        {
            PhysicsModule.HeatTransfer,
            PhysicsModule.Electrostatics,
            PhysicsModule.PipeFlow,
            PhysicsModule.BeamStress
        };

        private async void Start()
        {
            EnsureLighting();
            ApplyDemoDefaults();

            _heatmapRenderer = new HeatmapRenderer(config.nx, config.ny);
            _vectorOverlay = new VectorFieldOverlay();
            _volumeView = new SimulationVolumeView();
            _volumeView.Initialize(transform);
            EnsureCamera();
            FrameCameraToVolume();
            CreateCanvas();
            _playing = autoPlay;
            _lastObservedModule = config.module;
            _configFingerprint = BuildConfigFingerprint();

            await RunSimulation();
        }

        private void ApplyDemoDefaults()
        {
            config.width = 0.1f;
            config.height = 0.1f;
            config.nx = 30;
            config.ny = 30;
            config.topBC = 100f;
            config.bottomBC = 0f;
            config.leftBC = 0f;
            config.rightBC = 0f;
            config.dt = 0.05f;
            config.steps = 200;
            config.length = 2f;
            config.radius = 0.01f;
            config.nodes = 141;
            config.sectionWidth = 0.05f;
            config.sectionHeight = 0.12f;
            config.pointLoadValue = 900f;
            config.pointLoadPosition = 1f;
            config.distributedLoad = 400f;
            config.pressureGradient = -100f;
        }

        private void Update()
        {
            HandleVolumeManipulation();
            HandleInput();
            DetectInspectorConfigChanges();

            if (_playing && HasFramePlayback())
            {
                _playTimer += Time.deltaTime;
                if (_playTimer >= playInterval)
                {
                    _playTimer = 0f;
                    StepFrame(1);
                    UpdateVisualization();
                }
            }

            if (_playing && config.module == PhysicsModule.Electrostatics && _showVectors)
            {
                UpdateElectrostaticVectorAnimation();
            }

            UpdateStatusBar();
            UpdateControlBar();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { SelectModule(PhysicsModule.HeatTransfer); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { SelectModule(PhysicsModule.Electrostatics); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { SelectModule(PhysicsModule.PipeFlow); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { SelectModule(PhysicsModule.BeamStress); }

            if (Input.GetKeyDown(KeyCode.Space) && CanPlayCurrentModule())
            {
                _playing = !_playing;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow) && HasFramePlayback())
            {
                StepFrame(1);
                UpdateVisualization();
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow) && HasFramePlayback())
            {
                StepFrame(-1);
                UpdateVisualization();
            }
            if (Input.GetKeyDown(KeyCode.R)) { _ = RunSimulation(); }
            if (Input.GetKeyDown(KeyCode.M)) { CycleMaterial(); _ = RunSimulation(); }
            if (Input.GetKeyDown(KeyCode.V)) { _showVectors = !_showVectors; UpdateVisualization(); }
            if (Input.GetKeyDown(KeyCode.C)) { _volumeView.ResetOrientation(); FrameCameraToVolume(); UpdateInfoText(); }
            if (Input.GetKeyDown(KeyCode.Escape) && _chartOverlayOpen) { SetChartOverlayOpen(false); }
            if (Input.GetKeyDown(KeyCode.F12)) CaptureScreenshot();
        }

        private void CycleMaterial()
        {
            _materialIndex = (_materialIndex + 1) % Materials.Length;
            config.materialPreset = Materials[_materialIndex];
        }

        private void SelectModule(PhysicsModule module)
        {
            config.module = module;
            ApplyModuleDefaults(module);
            _ = RunSimulation();
        }

        private static bool SupportsVectorOverlay(PhysicsModule module)
        {
            return module == PhysicsModule.HeatTransfer || module == PhysicsModule.Electrostatics;
        }

        private static bool SupportsAnimatedPlayback(PhysicsModule module)
        {
            return module == PhysicsModule.HeatTransfer || module == PhysicsModule.Electrostatics || module == PhysicsModule.PipeFlow;
        }

        private void ApplyModuleDefaults(PhysicsModule module)
        {
            _showVectors = SupportsVectorOverlay(module);
            _playing = SupportsAnimatedPlayback(module) && autoPlay;
            _playTimer = 0f;
            _currentFrame = 0;
            _lastObservedModule = module;
        }

        private void DetectInspectorConfigChanges()
        {
            string fingerprint = BuildConfigFingerprint();
            if (fingerprint == _configFingerprint)
            {
                return;
            }

            bool moduleChanged = config.module != _lastObservedModule;
            _configFingerprint = fingerprint;

            if (moduleChanged)
            {
                ApplyModuleDefaults(config.module);
            }

            if (!_isRunningSimulation)
            {
                _ = RunSimulation();
            }
        }

        private string BuildConfigFingerprint()
        {
            return JsonUtility.ToJson(config);
        }

        private bool CanPlayCurrentModule()
        {
            return SupportsAnimatedPlayback(config.module);
        }

        private bool HasFramePlayback()
        {
            return (_timeline != null && _timeline.Count > 0) || (_timeline1D != null && _timeline1D.Count > 0);
        }

        private int GetFrameCount()
        {
            if (_timeline != null && _timeline.Count > 0)
            {
                return _timeline.Count;
            }

            if (_timeline1D != null && _timeline1D.Count > 0)
            {
                return _timeline1D.Count;
            }

            return 0;
        }

        private void StepFrame(int delta)
        {
            int frameCount = GetFrameCount();
            if (frameCount <= 0)
            {
                return;
            }

            _currentFrame = (_currentFrame + delta) % frameCount;
            if (_currentFrame < 0)
            {
                _currentFrame += frameCount;
            }
        }

        private void HandleVolumeManipulation()
        {
            if (_volumeView == null || Camera.main == null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0)
                && _volumeView.TryPickAxis(Camera.main.ScreenPointToRay(Input.mousePosition), out VolumeMoveAxis axis))
            {
                _isDraggingVolume = true;
                _activeMoveAxis = axis;
                _dragStartMousePosition = Input.mousePosition;
                _dragStartVolumeRotation = _volumeView.RotationEuler;
                _volumeView.SetSelectedAxis(axis);
                UpdateInfoText();
            }

            if (!_isDraggingVolume)
            {
                return;
            }

            if (Input.GetMouseButton(0))
            {
                UpdateVolumeDrag(Camera.main);
            }

            if (Input.GetMouseButtonUp(0))
            {
                _isDraggingVolume = false;
                _activeMoveAxis = VolumeMoveAxis.None;
                _volumeView.SetSelectedAxis(VolumeMoveAxis.None);
                UpdateInfoText();
            }
        }

        private void UpdateVolumeDrag(Camera camera)
        {
            Vector3 axisDirection = _volumeView.GetAxisDirection(_activeMoveAxis);
            if (axisDirection == Vector3.zero)
            {
                return;
            }

            Vector2 tangentOnScreen = _volumeView.GetScreenRotationTangent(camera, _activeMoveAxis);
            if (tangentOnScreen.sqrMagnitude < 25f)
            {
                return;
            }

            Vector2 mouseDelta = (Vector2)Input.mousePosition - _dragStartMousePosition;
            float deltaPixels = Vector2.Dot(mouseDelta, tangentOnScreen.normalized);
            float rotationDegrees = deltaPixels * 0.55f;
            _volumeView.RotationEuler = _dragStartVolumeRotation + axisDirection * rotationDegrees;
            UpdateInfoText();
        }

        private async Task RunSimulation()
        {
            if (_isRunningSimulation)
            {
                return;
            }

            _isRunningSimulation = true;
            string requestedFingerprint = BuildConfigFingerprint();
            _configFingerprint = requestedFingerprint;
            _lastObservedModule = config.module;
            _currentFrame = 0;
            _infoText.text = $"Running {config.module}...";
            _globalMin = 0.0;
            _globalMax = 1.0;
            _timeline1D = null;
            _chartSeries = null;

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
                            beamBuilder = beamBuilder.AddSource(position: config.pointLoadPosition * config.length, value: config.pointLoadValue);
                        if (config.distributedLoad > 0)
                            beamBuilder = beamBuilder.WithSource(config.distributedLoad);
                        result = beamBuilder.Solve();
                        break;
                }
            });

            _result = result;
            _volumeView.ApplyMaterialTheme(material);
            _volumeView.ConfigureForModule(config.module);
            _volumeView.ConfigureGrid(config.module, config.nx, config.ny);
            FrameCameraToVolume();

            if (_result != null && _result.Timeline != null && _result.Timeline.Count > 0)
            {
                _timeline = SimulationTimeline.FromResult(_result,
                    dt: config.dt,
                    dx: config.width / config.nx,
                    dy: config.height / config.ny);

                _globalMin = double.MaxValue;
                _globalMax = double.MinValue;
                for (int f = 0; f < _timeline.Count; f++)
                {
                    var frame = _timeline[f].ToArray();
                    int w = frame.GetLength(0);
                    int h = frame.GetLength(1);
                    for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        double v = frame[x, y];
                        if (v < _globalMin) _globalMin = v;
                        if (v > _globalMax) _globalMax = v;
                    }
                }
            }
            else if (_result != null && _result.Timeline1D != null && _result.Timeline1D.Count > 0)
            {
                _timeline = null;
                _timeline1D = _result.Timeline1D;
                _globalMin = double.MaxValue;
                _globalMax = double.MinValue;
                for (int frameIndex = 0; frameIndex < _timeline1D.Count; frameIndex++)
                {
                    var frame = _timeline1D[frameIndex];
                    for (int i = 0; i < frame.Length; i++)
                    {
                        double value = frame[i];
                        if (value < _globalMin) _globalMin = value;
                        if (value > _globalMax) _globalMax = value;
                    }
                }
            }
            else
            {
                _timeline = null;
                _timeline1D = null;
            }

            BuildChartSeries();

            Debug.Log($"[EngineeringToolbox] {config.module} complete - material={material.Name}, max={_result?.MaxValue:F3}, min={_result?.MinValue:F3}, iterations={_result?.Iterations}, timeline2D={_timeline?.Count ?? 0} frames, timeline1D={_timeline1D?.Count ?? 0} frames, dt={config.dt}, steps={config.steps}, globalMin={_globalMin:F3}, globalMax={_globalMax:F3}");

            UpdateVisualization();

            _isRunningSimulation = false;

            string currentFingerprint = BuildConfigFingerprint();
            if (currentFingerprint != requestedFingerprint)
            {
                _configFingerprint = currentFingerprint;
                _ = RunSimulation();
            }
        }

        private void UpdateVisualization()
        {
            if (_result == null) return;

            _activeVectorFieldX = null;
            _activeVectorFieldY = null;

            var currentMaterial = config.GetMaterial();
            string legendTitle = GetLegendTitle();
            string legendUnit = GetLegendUnit();
            _volumeView.ApplyMaterialTheme(currentMaterial);
            UpdateMaterialSwatch(currentMaterial);

            switch (config.module)
            {
                case PhysicsModule.HeatTransfer:
                    double[,] heatField;
                    if (_timeline != null && _currentFrame < _timeline.Count)
                    {
                        heatField = _timeline[_currentFrame].ToArray();
                        Render2DField(heatField, _globalMin, _globalMax);
                        UpdateLegend(_globalMin, _globalMax, legendTitle, legendUnit);
                    }
                    else
                    {
                        heatField = _result.Field;
                        Render2DField(heatField);
                        UpdateLegend(_result.MinValue, _result.MaxValue, legendTitle, legendUnit);
                    }

                    if (_showVectors)
                    {
                        RenderGradientVectorOverlay(heatField, invertDirection: true);
                    }
                    else
                    {
                        _vectorOverlay.ClearWorld();
                    }
                    break;

                case PhysicsModule.Electrostatics:
                    Render2DField(_result.Field);
                    UpdateLegend(_result.MinValue, _result.MaxValue, legendTitle, legendUnit);
                    if (_showVectors)
                    {
                        CacheGradientVectorField(_result.Field, invertDirection: true);
                        if (_playing)
                        {
                            UpdateElectrostaticVectorAnimation();
                        }
                        else
                        {
                            _vectorOverlay.SetVisible(true);
                            _vectorOverlay.RenderWorld(_volumeView.VectorAnchor, _volumeView.SurfaceSize,
                                _activeVectorFieldX, _activeVectorFieldY,
                                _activeVectorFieldX.GetLength(0), _activeVectorFieldX.GetLength(1), 0f);
                        }
                    }
                    else
                    {
                        _vectorOverlay.ClearWorld();
                    }
                    break;

                case PhysicsModule.PipeFlow:
                    Render1DProfile(GetCurrent1DProfile(), _globalMin, _globalMax);
                    UpdateLegend(_globalMin, _globalMax, legendTitle, legendUnit);
                    _vectorOverlay.ClearWorld();
                    break;

                case PhysicsModule.BeamStress:
                    Render1DProfile(_result.Values, _result.MinValue, _result.MaxValue);
                    UpdateLegend(_result.MinValue, _result.MaxValue, legendTitle, legendUnit);
                    _vectorOverlay.ClearWorld();
                    break;
            }

            UpdateChart();
            UpdateInfoText();
        }

        private void BuildChartSeries()
        {
            if (_timeline != null && _timeline.Count > 1)
            {
                _chartSeries = new double[_timeline.Count];
                for (int frameIndex = 0; frameIndex < _timeline.Count; frameIndex++)
                {
                    _chartSeries[frameIndex] = ComputeAverage(_timeline[frameIndex].ToArray());
                }

                return;
            }

            if (_timeline1D != null && _timeline1D.Count > 1)
            {
                _chartSeries = new double[_timeline1D.Count];
                for (int frameIndex = 0; frameIndex < _timeline1D.Count; frameIndex++)
                {
                    _chartSeries[frameIndex] = Compute1DChartMetric(_timeline1D[frameIndex]);
                }

                return;
            }

            _chartSeries = null;
        }

        private void RenderGradientVectorOverlay(double[,] field, bool invertDirection)
        {
            if (field == null)
            {
                _activeVectorFieldX = null;
                _activeVectorFieldY = null;
                _vectorOverlay.ClearWorld();
                return;
            }

            CacheGradientVectorField(field, invertDirection);
            _vectorOverlay.SetVisible(true);
            _vectorOverlay.RenderWorld(_volumeView.VectorAnchor, _volumeView.SurfaceSize, _activeVectorFieldX, _activeVectorFieldY,
                field.GetLength(0), field.GetLength(1));
        }

        private void CacheGradientVectorField(double[,] field, bool invertDirection)
        {
            if (field == null)
            {
                _activeVectorFieldX = null;
                _activeVectorFieldY = null;
                return;
            }

            BuildGradientVectorField(field, invertDirection, out _activeVectorFieldX, out _activeVectorFieldY);
        }

        private void UpdateElectrostaticVectorAnimation()
        {
            if (_activeVectorFieldX == null || _activeVectorFieldY == null || _volumeView == null)
            {
                return;
            }

            float phase = Mathf.Repeat(Time.time * electrostaticVectorSpeed, 1f);
            _vectorOverlay.SetVisible(true);
            _vectorOverlay.RenderWorld(
                _volumeView.VectorAnchor,
                _volumeView.SurfaceSize,
                _activeVectorFieldX,
                _activeVectorFieldY,
                _activeVectorFieldX.GetLength(0),
                _activeVectorFieldX.GetLength(1),
                phase);
        }

        private static void BuildGradientVectorField(double[,] field, bool invertDirection, out double[,] vx, out double[,] vy)
        {
            int width = field.GetLength(0);
            int height = field.GetLength(1);
            vx = new double[width, height];
            vy = new double[width, height];

            double directionScale = invertDirection ? -1.0 : 1.0;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int left = Mathf.Max(x - 1, 0);
                int right = Mathf.Min(x + 1, width - 1);
                int down = Mathf.Max(y - 1, 0);
                int up = Mathf.Min(y + 1, height - 1);

                double dx = field[right, y] - field[left, y];
                double dy = field[x, up] - field[x, down];

                if (right != left) dx /= (right - left);
                if (up != down) dy /= (up - down);

                vx[x, y] = dx * directionScale;
                vy[x, y] = dy * directionScale;
            }
        }

        private void Render2DField(double[,] field)
        {
            if (field == null) return;
            var tex = _heatmapRenderer.Render(field);
            _volumeView.SetTexture(tex);
        }

        private void Render2DField(double[,] field, double min, double max)
        {
            if (field == null) return;
            var tex = _heatmapRenderer.Render(field, min, max);
            _volumeView.SetTexture(tex);
        }

        private double[] GetCurrent1DProfile()
        {
            if (_timeline1D != null && _timeline1D.Count > 0)
            {
                int frameIndex = Mathf.Clamp(_currentFrame, 0, _timeline1D.Count - 1);
                return _timeline1D[frameIndex];
            }

            return _result != null ? _result.Values : null;
        }

        private void Render1DProfile(double[] values, double min, double max)
        {
            if (values == null) return;

            int n = values.Length;
            const int bands = 24;
            var field = new double[n, bands];
            for (int i = 0; i < n; i++)
            for (int band = 0; band < bands; band++)
                field[i, band] = values[i];

            _heatmapRenderer.Resize(n, bands);
            var tex = _heatmapRenderer.Render(field, min, max);
            _volumeView.SetTexture(tex);
        }

        private double Compute1DChartMetric(double[] values)
        {
            if (values == null || values.Length == 0)
            {
                return 0.0;
            }

            double averageVelocity = ComputeAverage(values);
            if (config.module == PhysicsModule.PipeFlow)
            {
                return averageVelocity * System.Math.PI * config.radius * config.radius;
            }

            return averageVelocity;
        }

        private static double ComputeAverage(double[] values)
        {
            if (values == null || values.Length == 0)
            {
                return 0.0;
            }

            double sum = 0.0;
            for (int index = 0; index < values.Length; index++)
            {
                sum += values[index];
            }

            return sum / values.Length;
        }

        private static double ComputeAverage(double[,] field)
        {
            if (field == null)
            {
                return 0.0;
            }

            int width = field.GetLength(0);
            int height = field.GetLength(1);
            if (width == 0 || height == 0)
            {
                return 0.0;
            }

            double sum = 0.0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                sum += field[x, y];
            }

            return sum / (width * height);
        }

        private void UpdateChart()
        {
            if (_chartPanel == null || _chartTitleText == null || _chartImage == null || _chartFooterText == null)
            {
                return;
            }

            _chartTitleText.text = GetChartTitle();

            if (!HasChartData())
            {
                _chartImage.texture = null;
                _chartImage.color = new Color(0f, 0f, 0f, 0f);
                _chartFooterText.text = "No timeline chart available for the current module.";
                if (_chartOverlayOpen)
                {
                    SetChartOverlayOpen(false);
                }
                return;
            }

            _chartImage.color = Color.white;
            int highlightIndex = Mathf.Clamp(_currentFrame, 0, _chartSeries.Length - 1);
            _chartImage.texture = RenderChartTexture(_chartSeries, highlightIndex);

            double currentValue = _chartSeries[highlightIndex];
            double endTime = (_chartSeries.Length - 1) * config.dt;
            _chartFooterText.text = $"0-{FormatNumericValue(endTime)} s  |  current {FormatNumericValue(currentValue)} {GetChartUnit()}";
        }

        private bool HasChartData()
        {
            return _chartSeries != null && _chartSeries.Length >= 2;
        }

        private void ToggleChartOverlay()
        {
            if (!HasChartData())
            {
                return;
            }

            SetChartOverlayOpen(!_chartOverlayOpen);
        }

        private void SetChartOverlayOpen(bool isOpen)
        {
            _chartOverlayOpen = isOpen && HasChartData();
            if (_chartOverlay != null)
            {
                _chartOverlay.gameObject.SetActive(_chartOverlayOpen);
            }
        }

        private void ScrubChart(BaseEventData eventData)
        {
            if (!HasChartData() || _chartImage == null)
            {
                return;
            }

            var pointerEventData = eventData as PointerEventData;
            if (pointerEventData == null)
            {
                return;
            }

            RectTransform chartRect = _chartImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    chartRect,
                    pointerEventData.position,
                    pointerEventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Rect rect = chartRect.rect;
            float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
            int targetFrame = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (_chartSeries.Length - 1)), 0, _chartSeries.Length - 1);

            _currentFrame = targetFrame;
            if (CanPlayCurrentModule())
            {
                _playing = false;
            }

            UpdateVisualization();
        }

        private Texture2D RenderChartTexture(double[] series, int highlightedIndex)
        {
            const int width = 360;
            const int height = 170;
            const int leftPadding = 18;
            const int rightPadding = 10;
            const int topPadding = 10;
            const int bottomPadding = 18;

            if (_chartTexture == null || _chartTexture.width != width || _chartTexture.height != height)
            {
                _chartTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _chartTexture.wrapMode = TextureWrapMode.Clamp;
                _chartTexture.filterMode = FilterMode.Bilinear;
            }

            Color background = new Color(0.05f, 0.10f, 0.16f, 0.96f);
            Color gridColor = new Color(0.22f, 0.34f, 0.42f, 1f);
            Color lineColor = new Color(0.92f, 0.74f, 0.26f, 1f);
            Color markerColor = new Color(0.20f, 0.90f, 1.0f, 1f);

            FillTexture(_chartTexture, background);

            int plotMinX = leftPadding;
            int plotMaxX = width - rightPadding - 1;
            int plotMinY = bottomPadding;
            int plotMaxY = height - topPadding - 1;

            DrawLine(_chartTexture, plotMinX, plotMinY, plotMaxX, plotMinY, gridColor, 1);
            DrawLine(_chartTexture, plotMinX, plotMinY, plotMinX, plotMaxY, gridColor, 1);

            for (int guide = 1; guide <= 3; guide++)
            {
                int y = Mathf.RoundToInt(Mathf.Lerp(plotMinY, plotMaxY, guide / 4f));
                DrawLine(_chartTexture, plotMinX, y, plotMaxX, y, new Color(gridColor.r, gridColor.g, gridColor.b, 0.65f), 1);
            }

            double minValue = double.MaxValue;
            double maxValue = double.MinValue;
            for (int index = 0; index < series.Length; index++)
            {
                minValue = System.Math.Min(minValue, series[index]);
                maxValue = System.Math.Max(maxValue, series[index]);
            }

            if (System.Math.Abs(maxValue - minValue) < 1e-9)
            {
                maxValue += 1.0;
                minValue -= 1.0;
            }

            int previousX = plotMinX;
            int previousY = plotMinY;
            for (int index = 0; index < series.Length; index++)
            {
                float xT = series.Length == 1 ? 0f : index / (float)(series.Length - 1);
                float normalizedValue = Mathf.InverseLerp((float)minValue, (float)maxValue, (float)series[index]);
                int x = Mathf.RoundToInt(Mathf.Lerp(plotMinX, plotMaxX, xT));
                int y = Mathf.RoundToInt(Mathf.Lerp(plotMinY, plotMaxY, normalizedValue));

                if (index > 0)
                {
                    DrawLine(_chartTexture, previousX, previousY, x, y, lineColor, 2);
                }

                previousX = x;
                previousY = y;
            }

            highlightedIndex = Mathf.Clamp(highlightedIndex, 0, series.Length - 1);
            float highlightedXT = series.Length == 1 ? 0f : highlightedIndex / (float)(series.Length - 1);
            float highlightedYT = Mathf.InverseLerp((float)minValue, (float)maxValue, (float)series[highlightedIndex]);
            int highlightX = Mathf.RoundToInt(Mathf.Lerp(plotMinX, plotMaxX, highlightedXT));
            int highlightY = Mathf.RoundToInt(Mathf.Lerp(plotMinY, plotMaxY, highlightedYT));
            DrawCircle(_chartTexture, highlightX, highlightY, 4, markerColor);
            _chartTexture.Apply(false);
            return _chartTexture;
        }

        private static void FillTexture(Texture2D texture, Color color)
        {
            Color[] pixels = texture.GetPixels();
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color;
            }

            texture.SetPixels(pixels);
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            if (steps == 0)
            {
                DrawCircle(texture, x0, y0, thickness, color);
                return;
            }

            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                DrawCircle(texture, x, y, thickness, color);
            }
        }

        private static void DrawCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
        {
            int radiusSquared = radius * radius;
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radiusSquared)
                {
                    continue;
                }

                int pixelX = centerX + dx;
                int pixelY = centerY + dy;
                if (pixelX < 0 || pixelX >= texture.width || pixelY < 0 || pixelY >= texture.height)
                {
                    continue;
                }

                texture.SetPixel(pixelX, pixelY, color);
            }
        }

        private void UpdateInfoText()
        {
            if (_result == null) return;

            string valueUnit = GetLegendUnit();
            string info = $"<b>{config.module}</b>  |  Material: {config.GetMaterial().Name}\n";
            info += $"Max: {FormatValueWithUnit(_result.MaxValue, valueUnit)}  Min: {FormatValueWithUnit(_result.MinValue, valueUnit)}";

            int frameCount = GetFrameCount();
            if (frameCount > 0)
                info += $"  |  Frame {_currentFrame + 1}/{frameCount}";
            else
                info += "  |  3D slice view";

            Vector3 volumeRotation = _volumeView != null ? _volumeView.RotationEuler : Vector3.zero;
            info += $"\nRotation: X {volumeRotation.x:F1} deg  Y {volumeRotation.y:F1} deg  Z {volumeRotation.z:F1} deg";

            if (config.module == PhysicsModule.BeamStress)
                info += $"\nSupport: {config.beamSupport}  Load: {config.pointLoadValue:F0} N";
            else if (config.module == PhysicsModule.PipeFlow)
                info += $"\nPressure gradient: {config.pressureGradient:F1} Pa/m";
            else if (config.module == PhysicsModule.Electrostatics)
                info += $"\nVector overlay: {(_showVectors ? (_playing ? "Electric field live" : "Electric field") : "Off")}";
            else if (config.module == PhysicsModule.HeatTransfer)
                info += $"\nVector overlay: {(_showVectors ? "Heat flow" : "Off")}";

            if (_activeMoveAxis != VolumeMoveAxis.None)
                info += $"\nRotating axis: {_activeMoveAxis}";

            _infoText.text = info;
        }

        private void UpdateStatusBar()
        {
            string playbackLabel = CanPlayCurrentModule() ? (_playing ? "Pause" : "Play") : "Static";
            string status = $"Inspector updates rerun automatically  |  Space: {playbackLabel}  |  Drag XYZ: rotate cube  |  C: reset view  |  F12: screenshot";
            _statusText.text = status;
        }

        private void EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }

            cam.backgroundColor = new Color(0.04f, 0.07f, 0.11f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
        }

        private void FrameCameraToVolume()
        {
            Camera cam = Camera.main;
            if (cam == null || _volumeView == null)
            {
                return;
            }

            Vector3 target = _volumeView.FocusPointWorld + new Vector3(0f, 0.05f, 0f);
            Vector3 direction = new Vector3(0.78f, 0.52f, -1f).normalized;
            float distance = _volumeView.BoundsRadius * 2.15f;

            cam.transform.position = target - direction * distance;
            cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
        }

        private void EnsureLighting()
        {
            if (GameObject.Find("EngineeringToolbox_Lighting") != null)
            {
                return;
            }

            var lightingRoot = new GameObject("EngineeringToolbox_Lighting");
            CreateDirectionalLight(lightingRoot.transform, "KeyLight", new Vector3(42f, -28f, 0f), new Color(0.95f, 0.97f, 1f), 1.2f);
            CreateDirectionalLight(lightingRoot.transform, "FillLight", new Vector3(18f, 145f, 0f), new Color(0.42f, 0.63f, 0.95f), 0.45f);

            var rim = new GameObject("RimLight");
            rim.transform.SetParent(lightingRoot.transform, false);
            rim.transform.position = new Vector3(-2.2f, 2.6f, -2.6f);
            var rimLight = rim.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.color = new Color(0.22f, 0.78f, 1f);
            rimLight.intensity = 2.1f;
            rimLight.range = 12f;
        }

        private void CreateCanvas()
        {
            var canvasGo = new GameObject("EngineeringToolbox_Canvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var topPanel = CreatePanel(canvasGo.transform, "TopPanel", new Vector2(0.02f, 0.82f), new Vector2(0.31f, 0.97f), new Color(0.03f, 0.08f, 0.12f, 0.78f));
            var bottomPanel = CreatePanel(canvasGo.transform, "BottomPanel", new Vector2(0.14f, 0.015f), new Vector2(0.86f, 0.105f), new Color(0.03f, 0.08f, 0.12f, 0.8f));
            var titlePanel = CreatePanel(canvasGo.transform, "TitlePanel", new Vector2(0.70f, 0.88f), new Vector2(0.97f, 0.96f), new Color(0.05f, 0.16f, 0.24f, 0.66f));
            var legendPanel = CreatePanel(canvasGo.transform, "LegendPanel", new Vector2(0.90f, 0.20f), new Vector2(0.975f, 0.56f), new Color(0.03f, 0.08f, 0.12f, 0.74f));
            var materialPanel = CreatePanel(canvasGo.transform, "MaterialPanel", new Vector2(0.79f, 0.77f), new Vector2(0.975f, 0.84f), new Color(0.03f, 0.08f, 0.12f, 0.72f));
            CreateChartOverlay(canvasGo.transform);

            _infoText = CreateText(topPanel, "InfoText", 16, TextAnchor.UpperLeft);
            _statusText = CreateText(bottomPanel, "StatusBar", 13, TextAnchor.MiddleLeft);
            _titleText = CreateText(titlePanel, "TitleText", 18, TextAnchor.MiddleCenter);
            _titleText.text = "Engineering Toolbox | 3D Slice Demo";
            _titleText.color = new Color(0.8f, 0.96f, 1f);
            _statusText.color = new Color(0.82f, 0.87f, 0.92f);
            _statusText.rectTransform.anchorMin = new Vector2(0.02f, 0.04f);
            _statusText.rectTransform.anchorMax = new Vector2(0.98f, 0.34f);
            _statusText.rectTransform.offsetMin = Vector2.zero;
            _statusText.rectTransform.offsetMax = Vector2.zero;

            CreateLegendUI(legendPanel);
            CreateMaterialSwatchUI(materialPanel);
            CreateControlBar(bottomPanel);
        }

        private void CreateControlBar(RectTransform parent)
        {
            _moduleButtons = new Button[ModuleOrder.Length];
            _moduleButtonTexts = new Text[ModuleOrder.Length];

            string[] moduleLabels = { "Heat", "Electro", "Flow", "Beam" };
            float moduleWidth = 0.10f;
            float moduleGap = 0.015f;
            float moduleStart = 0.02f;
            for (int i = 0; i < ModuleOrder.Length; i++)
            {
                float xMin = moduleStart + i * (moduleWidth + moduleGap);
                float xMax = xMin + moduleWidth;
                var module = ModuleOrder[i];
                _moduleButtons[i] = CreateButton(parent, $"ModuleButton_{module}", moduleLabels[i], new Vector2(xMin, 0.44f), new Vector2(xMax, 0.88f), () => SelectModule(module), out _moduleButtonTexts[i]);
            }

            _playPauseButton = CreateButton(parent, "PlayPauseButton", "Play", new Vector2(0.50f, 0.44f), new Vector2(0.58f, 0.88f), TogglePlayback, out _playPauseButtonText);
            _previousFrameButton = CreateButton(parent, "PrevFrameButton", "Prev", new Vector2(0.595f, 0.44f), new Vector2(0.665f, 0.88f), () => StepFromButton(-1), out _);
            _nextFrameButton = CreateButton(parent, "NextFrameButton", "Next", new Vector2(0.68f, 0.44f), new Vector2(0.75f, 0.88f), () => StepFromButton(1), out _);
            _vectorToggleButton = CreateButton(parent, "VectorToggleButton", "Vectors", new Vector2(0.765f, 0.44f), new Vector2(0.855f, 0.88f), ToggleVectors, out _vectorToggleButtonText);
            _chartToggleButton = CreateButton(parent, "ChartToggleButton", "Chart", new Vector2(0.87f, 0.44f), new Vector2(0.925f, 0.88f), ToggleChartOverlay, out _chartToggleButtonText);
            _materialButton = CreateButton(parent, "MaterialButton", "Material", new Vector2(0.935f, 0.44f), new Vector2(0.985f, 0.88f), CycleMaterialFromButton, out _);
        }

        private RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void CreateLegendUI(RectTransform parent)
        {
            _legendTitleText = CreateText(parent, "LegendTitle", 14, TextAnchor.UpperCenter);
            _legendTitleText.rectTransform.anchorMin = new Vector2(0f, 0.82f);
            _legendTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _legendTitleText.rectTransform.offsetMin = new Vector2(8f, 0f);
            _legendTitleText.rectTransform.offsetMax = new Vector2(-8f, -4f);
            _legendTitleText.text = "Legend";
            _legendTitleText.color = new Color(0.85f, 0.95f, 1f);

            var imageObject = new GameObject("LegendGradient");
            imageObject.transform.SetParent(parent, false);
            _legendImage = imageObject.AddComponent<RawImage>();
            var imageRect = _legendImage.rectTransform;
            imageRect.anchorMin = new Vector2(0.28f, 0.12f);
            imageRect.anchorMax = new Vector2(0.62f, 0.78f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            _legendImage.texture = _heatmapRenderer.RenderLegend(32, 256);

            _legendTopValueText = CreateText(parent, "LegendTopValue", 12, TextAnchor.UpperLeft);
            _legendTopValueText.rectTransform.anchorMin = new Vector2(0.64f, 0.66f);
            _legendTopValueText.rectTransform.anchorMax = new Vector2(1f, 0.78f);
            _legendTopValueText.rectTransform.offsetMin = new Vector2(4f, 0f);
            _legendTopValueText.rectTransform.offsetMax = new Vector2(-6f, 0f);

            _legendBottomValueText = CreateText(parent, "LegendBottomValue", 12, TextAnchor.LowerLeft);
            _legendBottomValueText.rectTransform.anchorMin = new Vector2(0.64f, 0.12f);
            _legendBottomValueText.rectTransform.anchorMax = new Vector2(1f, 0.24f);
            _legendBottomValueText.rectTransform.offsetMin = new Vector2(4f, 0f);
            _legendBottomValueText.rectTransform.offsetMax = new Vector2(-6f, 0f);
        }

        private void CreateChartOverlay(Transform parent)
        {
            _chartOverlay = CreatePanel(parent, "ChartOverlay", new Vector2(0f, 0f), new Vector2(1f, 1f), new Color(0.02f, 0.05f, 0.08f, 0.88f));
            _chartOverlay.gameObject.SetActive(false);
            _chartPanel = CreatePanel(_chartOverlay, "ChartPanel", new Vector2(0.12f, 0.14f), new Vector2(0.88f, 0.86f), new Color(0.04f, 0.09f, 0.14f, 0.96f));
            CreateChartUI(_chartPanel);

            Button closeButton = CreateButton(_chartPanel, "ChartCloseButton", "Close", new Vector2(0.84f, 0.90f), new Vector2(0.96f, 0.97f), () => SetChartOverlayOpen(false), out _);
            Image closeImage = closeButton.GetComponent<Image>();
            if (closeImage != null)
            {
                closeImage.color = new Color(0.14f, 0.30f, 0.38f, 0.96f);
            }
        }

        private void CreateChartUI(RectTransform parent)
        {
            _chartTitleText = CreateText(parent, "ChartTitle", 14, TextAnchor.UpperLeft);
            _chartTitleText.rectTransform.anchorMin = new Vector2(0f, 0.86f);
            _chartTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _chartTitleText.rectTransform.offsetMin = new Vector2(12f, 0f);
            _chartTitleText.rectTransform.offsetMax = new Vector2(-120f, -8f);
            _chartTitleText.color = new Color(0.85f, 0.95f, 1f);
            _chartTitleText.text = "Time Series";

            var chartImageObject = new GameObject("ChartImage");
            chartImageObject.transform.SetParent(parent, false);
            _chartImage = chartImageObject.AddComponent<RawImage>();
            _chartImage.color = Color.white;
            EventTrigger chartTrigger = chartImageObject.AddComponent<EventTrigger>();
            AddEventTrigger(chartTrigger, EventTriggerType.PointerClick, ScrubChart);
            var chartRect = _chartImage.rectTransform;
            chartRect.anchorMin = new Vector2(0.04f, 0.15f);
            chartRect.anchorMax = new Vector2(0.96f, 0.82f);
            chartRect.offsetMin = Vector2.zero;
            chartRect.offsetMax = Vector2.zero;

            _chartFooterText = CreateText(parent, "ChartFooter", 11, TextAnchor.LowerLeft);
            _chartFooterText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _chartFooterText.rectTransform.anchorMax = new Vector2(1f, 0.14f);
            _chartFooterText.rectTransform.offsetMin = new Vector2(12f, 0f);
            _chartFooterText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            _chartFooterText.color = new Color(0.74f, 0.84f, 0.92f);
            _chartFooterText.text = "Waiting for simulation data";
        }

        private void CreateMaterialSwatchUI(RectTransform parent)
        {
            var swatchObject = new GameObject("MaterialSwatch");
            swatchObject.transform.SetParent(parent, false);
            _materialSwatchImage = swatchObject.AddComponent<Image>();
            var swatchRect = _materialSwatchImage.rectTransform;
            swatchRect.anchorMin = new Vector2(0.05f, 0.2f);
            swatchRect.anchorMax = new Vector2(0.22f, 0.8f);
            swatchRect.offsetMin = Vector2.zero;
            swatchRect.offsetMax = Vector2.zero;

            _materialSwatchText = CreateText(parent, "MaterialSwatchText", 13, TextAnchor.MiddleLeft);
            _materialSwatchText.rectTransform.anchorMin = new Vector2(0.26f, 0.15f);
            _materialSwatchText.rectTransform.anchorMax = new Vector2(0.98f, 0.85f);
            _materialSwatchText.rectTransform.offsetMin = new Vector2(4f, 0f);
            _materialSwatchText.rectTransform.offsetMax = new Vector2(-6f, 0f);
            _materialSwatchText.color = new Color(0.88f, 0.95f, 1f);
        }

        private void UpdateLegend(double minValue, double maxValue, string title, string unit)
        {
            if (_legendImage == null || _heatmapRenderer == null)
            {
                return;
            }

            _legendImage.texture = _heatmapRenderer.RenderLegend(32, 256);
            if (_legendTitleText != null)
            {
                _legendTitleText.text = string.IsNullOrWhiteSpace(unit) ? title : $"{title} ({unit})";
            }
            if (_legendTopValueText != null)
            {
                _legendTopValueText.text = FormatDisplayValue(maxValue);
            }
            if (_legendBottomValueText != null)
            {
                _legendBottomValueText.text = FormatDisplayValue(minValue);
            }
        }

        private void UpdateMaterialSwatch(CSharpNumerics.Physics.Materials.Engineering.EngineeringMaterial material)
        {
            if (_materialSwatchImage == null || _materialSwatchText == null || _volumeView == null)
            {
                return;
            }

            Color accent = _volumeView.GetMaterialAccentColor(material);
            _materialSwatchImage.color = accent;
            _materialSwatchText.text = $"Material\n{material.Name}";
        }

        private string GetLegendTitle()
        {
            switch (config.module)
            {
                case PhysicsModule.HeatTransfer: return "Temperature";
                case PhysicsModule.Electrostatics: return "Potential";
                case PhysicsModule.PipeFlow: return "Velocity";
                case PhysicsModule.BeamStress: return "Deflection";
                default: return "Value";
            }
        }

        private string GetChartTitle()
        {
            switch (config.module)
            {
                case PhysicsModule.HeatTransfer: return "Average Temperature vs Time";
                case PhysicsModule.Electrostatics: return "Average Potential vs Time";
                case PhysicsModule.PipeFlow: return "Flow Rate vs Time";
                case PhysicsModule.BeamStress: return "Deflection vs Time";
                default: return "Time Series";
            }
        }

        private string GetChartUnit()
        {
            switch (config.module)
            {
                case PhysicsModule.HeatTransfer: return "deg C";
                case PhysicsModule.Electrostatics: return "V";
                case PhysicsModule.PipeFlow: return "m^3/s";
                case PhysicsModule.BeamStress: return "mm";
                default: return string.Empty;
            }
        }

        private string GetLegendUnit()
        {
            switch (config.module)
            {
                case PhysicsModule.HeatTransfer: return "deg C";
                case PhysicsModule.Electrostatics: return "V";
                case PhysicsModule.PipeFlow: return "m/s";
                case PhysicsModule.BeamStress: return "mm";
                default: return string.Empty;
            }
        }

        private string FormatValueWithUnit(double value, string unit)
        {
            return string.IsNullOrWhiteSpace(unit) ? FormatDisplayValue(value) : $"{FormatDisplayValue(value)} {unit}";
        }

        private string FormatDisplayValue(double value)
        {
            return FormatNumericValue(ConvertDisplayValue(value));
        }

        private double ConvertDisplayValue(double value)
        {
            return config.module == PhysicsModule.BeamStress ? value * 1000.0 : value;
        }

        private static string FormatNumericValue(double value)
        {
            double absValue = System.Math.Abs(value);
            if (absValue >= 100) return value.ToString("F0");
            if (absValue >= 10) return value.ToString("F1");
            if (absValue >= 1) return value.ToString("F2");
            if (absValue >= 0.01) return value.ToString("F3");
            if (absValue > 0) return value.ToString("0.###E+0");
            return "0";
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick, out Text labelText)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.3f, 0.94f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            labelText = CreateText(buttonObject.transform, "Label", 13, TextAnchor.MiddleCenter);
            labelText.text = label;
            labelText.color = new Color(0.86f, 0.95f, 1f);
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private static void AddEventTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            if (trigger.triggers == null)
            {
                trigger.triggers = new List<EventTrigger.Entry>();
            }

            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void TogglePlayback()
        {
            if (!CanPlayCurrentModule())
            {
                return;
            }

            _playing = !_playing;
            UpdateVisualization();
        }

        private void StepFromButton(int delta)
        {
            if (!HasFramePlayback())
            {
                return;
            }

            StepFrame(delta);
            UpdateVisualization();
        }

        private void ToggleVectors()
        {
            if (!SupportsVectorOverlay(config.module))
            {
                return;
            }

            _showVectors = !_showVectors;
            UpdateVisualization();
        }

        private void CycleMaterialFromButton()
        {
            CycleMaterial();
            _ = RunSimulation();
        }

        private void UpdateControlBar()
        {
            if (_moduleButtons == null || _moduleButtonTexts == null)
            {
                return;
            }

            for (int i = 0; i < _moduleButtons.Length; i++)
            {
                bool isActive = ModuleOrder[i] == config.module;
                ApplyButtonVisualState(_moduleButtons[i], _moduleButtonTexts[i], true, isActive);
            }

            bool canPlay = CanPlayCurrentModule();
            bool hasFrames = HasFramePlayback();
            bool vectorsAvailable = SupportsVectorOverlay(config.module);

            if (_playPauseButtonText != null)
            {
                _playPauseButtonText.text = canPlay ? (_playing ? "Pause" : "Play") : "Static";
            }

            if (_vectorToggleButtonText != null)
            {
                _vectorToggleButtonText.text = vectorsAvailable ? (_showVectors ? "Vectors On" : "Vectors Off") : "No Vectors";
            }

            if (_chartToggleButtonText != null)
            {
                _chartToggleButtonText.text = _chartOverlayOpen ? "Close" : "Chart";
            }

            ApplyButtonVisualState(_playPauseButton, _playPauseButtonText, canPlay, _playing && canPlay);
            ApplyButtonVisualState(_previousFrameButton, null, hasFrames, false);
            ApplyButtonVisualState(_nextFrameButton, null, hasFrames, false);
            ApplyButtonVisualState(_vectorToggleButton, _vectorToggleButtonText, vectorsAvailable, _showVectors && vectorsAvailable);
            ApplyButtonVisualState(_chartToggleButton, _chartToggleButtonText, HasChartData(), _chartOverlayOpen && HasChartData());
            ApplyButtonVisualState(_materialButton, null, true, false);
        }

        private void ApplyButtonVisualState(Button button, Text label, bool interactable, bool active)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                if (!interactable)
                {
                    image.color = new Color(0.08f, 0.12f, 0.16f, 0.55f);
                }
                else if (active)
                {
                    image.color = new Color(0.15f, 0.52f, 0.62f, 0.96f);
                }
                else
                {
                    image.color = new Color(0.12f, 0.22f, 0.3f, 0.94f);
                }
            }

            if (label != null)
            {
                label.color = interactable ? new Color(0.86f, 0.95f, 1f) : new Color(0.5f, 0.58f, 0.64f);
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EngineeringToolbox_EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            txt.fontSize = fontSize;
            txt.color = Color.white;
            txt.alignment = alignment;
            txt.supportRichText = true;
            var r = txt.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(14f, 10f);
            r.offsetMax = new Vector2(-14f, -10f);
            return txt;
        }

        private void CreateDirectionalLight(Transform parent, string name, Vector3 eulerAngles, Color color, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
        }

        private void CaptureScreenshot()
        {
            string path = $"EngineeringToolbox_{config.module}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[EngineeringToolbox] Screenshot saved: {path}");
        }

        private void OnDestroy()
        {
            if (_vectorOverlay != null)
            {
                _vectorOverlay.ClearWorld();
            }

            if (_volumeView != null)
            {
                _volumeView.SetSelectedAxis(VolumeMoveAxis.None);
            }

            if (_chartTexture != null)
            {
                Destroy(_chartTexture);
                _chartTexture = null;
            }
        }
    }
}
