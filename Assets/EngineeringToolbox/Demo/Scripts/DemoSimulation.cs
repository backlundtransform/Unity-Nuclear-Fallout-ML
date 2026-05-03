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
    /// Categories:
    ///   1 = Thermodynamics
    ///   2 = Solid Mechanics
    ///   3 = Electromagnetism
    ///   4 = Fluid Dynamics
    ///
    /// Submodel cycling:
    ///   Q/E = previous / next submodel inside the active category
    ///
    /// Controls:
    ///   1-4        select main category
    ///   Q / E      cycle submodel
    ///   Space      play / pause timeline
    ///   Left/Right step backward / forward
    ///   R          re-run simulation
    ///   M          cycle material
    ///   V          toggle vector overlay
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
        private double _activeSimulationDt;
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
        private Button[] _disciplineButtons;
        private Text[] _disciplineButtonTexts;
        private Button[] _submoduleButtons;
        private Text[] _submoduleButtonTexts;
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
            MaterialPreset.Titanium,
            MaterialPreset.Brass,
            MaterialPreset.StainlessSteel,
            MaterialPreset.Concrete,
            MaterialPreset.Glass,
            MaterialPreset.Wood,
            MaterialPreset.Rubber,
            MaterialPreset.Plastic,
            MaterialPreset.Water,
            MaterialPreset.Air,
            MaterialPreset.Oil,
            MaterialPreset.Glycerin
        };

        private static readonly PhysicsDiscipline[] DisciplineOrder =
        {
            PhysicsDiscipline.Thermodynamics,
            PhysicsDiscipline.SolidMechanics,
            PhysicsDiscipline.Electromagnetism,
            PhysicsDiscipline.FluidDynamics
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
            config.inletVelocity = 1f;
            config.cylinderCenterX = 0.2f;
            config.cylinderCenterY = 0.2f;
            config.cylinderRadius = 0.05f;
            config.currentDensity = 1e6f;
            config.uniformLoad = 0f;
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

            if (_playing && _showVectors)
            {
                if (config.module == PhysicsModule.Electrostatics)
                {
                    UpdateElectrostaticVectorAnimation();
                }
                else if (config.module == PhysicsModule.FluidFlow2D)
                {
                    UpdateFlowVectorAnimation();
                }
                else if (config.module == PhysicsModule.CylinderFlow)
                {
                    UpdateFlowVectorAnimation(_result != null ? _result.CylinderMask : null);
                }
                else if (config.module == PhysicsModule.Magnetostatics)
                {
                    UpdateFlowVectorAnimation();
                }
                else if (config.module == PhysicsModule.PlaneStress)
                {
                    UpdateFlowVectorAnimation();
                }
            }

            UpdateStatusBar();
            UpdateControlBar();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { SelectDiscipline(PhysicsDiscipline.Thermodynamics); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { SelectDiscipline(PhysicsDiscipline.SolidMechanics); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { SelectDiscipline(PhysicsDiscipline.Electromagnetism); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { SelectDiscipline(PhysicsDiscipline.FluidDynamics); }
            if (Input.GetKeyDown(KeyCode.Q)) { CycleSubmodule(-1); }
            if (Input.GetKeyDown(KeyCode.E)) { CycleSubmodule(1); }

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

        private void SetMaterialPreset(MaterialPreset preset)
        {
            config.materialPreset = preset;
            for (int index = 0; index < Materials.Length; index++)
            {
                if (Materials[index] == preset)
                {
                    _materialIndex = index;
                    return;
                }
            }

            _materialIndex = 0;
        }

        private void SelectModule(PhysicsModule module)
        {
            config.module = module;
            ApplyModuleDefaults(module);
            _ = RunSimulation();
        }

        private void SelectDiscipline(PhysicsDiscipline discipline)
        {
            PhysicsDiscipline activeDiscipline = GetActiveDiscipline();
            if (discipline == activeDiscipline)
            {
                return;
            }

            SelectModule(PhysicsModuleCatalog.GetDefaultModule(discipline));
        }

        private void CycleSubmodule(int delta)
        {
            PhysicsModule[] modules = GetActiveSubmodules();
            if (modules.Length <= 1)
            {
                return;
            }

            int currentIndex = PhysicsModuleCatalog.GetModuleIndexInDiscipline(config.module);
            int nextIndex = (currentIndex + delta) % modules.Length;
            if (nextIndex < 0)
            {
                nextIndex += modules.Length;
            }

            SelectModule(modules[nextIndex]);
        }

        private PhysicsDiscipline GetActiveDiscipline()
        {
            return PhysicsModuleCatalog.GetDiscipline(config.module);
        }

        private PhysicsModule[] GetActiveSubmodules()
        {
            return PhysicsModuleCatalog.GetModules(GetActiveDiscipline());
        }

        private static bool SupportsVectorOverlay(PhysicsModule module)
        {
            return module == PhysicsModule.HeatTransfer
                || module == PhysicsModule.Electrostatics
                || module == PhysicsModule.FluidFlow2D
                || module == PhysicsModule.CylinderFlow
                || module == PhysicsModule.Magnetostatics
                || module == PhysicsModule.PlaneStress;
        }

        private static bool SupportsAnimatedPlayback(PhysicsModule module)
        {
            return module == PhysicsModule.HeatTransfer
                || module == PhysicsModule.Electrostatics
                || module == PhysicsModule.PipeFlow
                || module == PhysicsModule.FluidFlow2D
                || module == PhysicsModule.CylinderFlow
                || module == PhysicsModule.Magnetostatics
                || module == PhysicsModule.PlaneStress;
        }

        private void ApplyModuleDefaults(PhysicsModule module)
        {
            _showVectors = SupportsVectorOverlay(module);
            _playing = SupportsAnimatedPlayback(module) && autoPlay;
            _playTimer = 0f;
            _currentFrame = 0;
            _lastObservedModule = module;

            switch (module)
            {
                case PhysicsModule.HeatTransfer:
                    config.width = 0.1f;
                    config.height = 0.1f;
                    config.nx = 30;
                    config.ny = 30;
                    config.dt = 0.001f;
                    config.steps = 400;
                    SetMaterialPreset(MaterialPreset.Steel);
                    break;
                case PhysicsModule.CylinderFlow:
                    config.width = 2.4f;
                    config.height = 1.2f;
                    config.nx = 120;
                    config.ny = 60;
                    config.dt = 0.01f;
                    config.steps = 900;
                    config.inletVelocity = 0.06f;
                    config.cylinderCenterX = 0.55f;
                    config.cylinderCenterY = 0.6f;
                    config.cylinderRadius = 0.12f;
                    SetMaterialPreset(MaterialPreset.Oil);
                    break;
                case PhysicsModule.FluidFlow2D:
                    config.width = 1.6f;
                    config.height = 0.9f;
                    config.nx = 72;
                    config.ny = 40;
                    config.dt = 0.002f;
                    config.steps = 240;
                    config.inletVelocity = 0.03f;
                    SetMaterialPreset(MaterialPreset.Oil);
                    break;
                case PhysicsModule.PipeFlow:
                    config.radius = 0.01f;
                    config.nodes = 41;
                    config.length = 1.0f;
                    config.dt = 0.01f;
                    config.steps = 300;
                    config.pressureGradient = -100f;
                    SetMaterialPreset(MaterialPreset.Water);
                    break;
                case PhysicsModule.BeamStress:
                    config.length = 2f;
                    config.nodes = 101;
                    config.sectionWidth = 0.05f;
                    config.sectionHeight = 0.1f;
                    config.pointLoadValue = 5000f;
                    config.pointLoadPosition = 1f;
                    config.distributedLoad = 0f;
                    SetMaterialPreset(MaterialPreset.Steel);
                    break;
                case PhysicsModule.Magnetostatics:
                    SetMaterialPreset(MaterialPreset.Steel);
                    break;
                case PhysicsModule.PlaneStress:
                    config.width = 1.2f;
                    config.height = 0.6f;
                    config.nx = 72;
                    config.ny = 36;
                    config.topBC = 0f;
                    config.bottomBC = 0f;
                    config.rightBC = 0f;
                    config.pointLoadValue = 5_000_000f;
                    config.uniformLoad = -250_000f;
                    SetMaterialPreset(MaterialPreset.Aluminum);
                    break;
            }
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
            _activeSimulationDt = config.dt;
            _result = null;
            _timeline = null;
            _timeline1D = null;
            _chartSeries = null;
            _vectorOverlay.ClearWorld();

            var material = config.GetMaterial();
            var heatTransferSolve = GetHeatTransferSolveParameters(material);
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
                            .Solve(dt: heatTransferSolve.dt, steps: heatTransferSolve.steps);
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

                    case PhysicsModule.FluidFlow2D:
                        result = SimulationType.Create(MultiphysicsType.FluidFlow2D)
                            .WithMaterial(material)
                            .WithGeometry(config.width, config.height, config.nx, config.ny)
                            .WithBoundary(top: 0, bottom: 0, left: config.inletVelocity, right: 0)
                            .WithInitialCondition(0.0)
                            .Solve(dt: config.dt, steps: config.steps, maxIterations: 900, tolerance: 1e-4);
                        break;

                    case PhysicsModule.CylinderFlow:
                        result = SimulationType.Create(MultiphysicsType.CylinderFlow)
                            .WithMaterial(material)
                            .WithGeometry(config.width, config.height, config.nx, config.ny)
                            .WithCylinder(config.cylinderCenterX, config.cylinderCenterY, config.cylinderRadius)
                            .WithInletVelocity(config.inletVelocity)
                            .Solve(dt: config.dt, steps: config.steps, maxIterations: 1500, tolerance: 1e-4);
                        break;

                    case PhysicsModule.Magnetostatics:
                        var magBuilder = SimulationType.Create(MultiphysicsType.MagneticField)
                            .WithMaterial(material)
                            .WithGeometry(config.width, config.height, config.nx, config.ny)
                            .WithBoundary(top: 0, bottom: 0, left: 0, right: 0);
                        magBuilder = magBuilder.AddSource(config.nx / 2, config.ny / 2, config.currentDensity);
                        result = magBuilder.Solve(maxIterations: 20000, tolerance: 1e-8);
                        break;

                    case PhysicsModule.PlaneStress:
                        int planeStressLoadX = Mathf.Clamp(config.nx - 2, 1, config.nx - 1);
                        int planeStressLoadY = Mathf.Clamp(config.ny / 2, 1, config.ny - 2);
                        // The solver's Navier-Cauchy PDE expects body force density (N/m³).
                        // Convert point force (N) to force density by dividing by cell area (dx*dy).
                        double psDx = (double)config.width / config.nx;
                        double psDy = (double)config.height / config.ny;
                        double forceDensity = config.pointLoadValue / (psDx * psDy);
                        var stressBuilder = SimulationType.Create(MultiphysicsType.PlaneStress)
                            .WithMaterial(material)
                            .WithGeometry(config.width, config.height, config.nx, config.ny)
                            .WithBoundary(top: config.topBC, bottom: config.bottomBC, left: 0, right: config.rightBC);
                        stressBuilder = stressBuilder.AddSource(planeStressLoadX, planeStressLoadY, forceDensity);
                        if (config.uniformLoad != 0)
                        {
                            // UniformLoad is already a distributed load (N/m³), no scaling needed
                            double uniformDensity = config.uniformLoad / (psDx * psDy);
                            stressBuilder = stressBuilder.WithSource(uniformDensity);
                        }
                        result = stressBuilder.Solve(maxIterations: 20000, tolerance: 1e-12);
                        break;
                }
            });

            _result = SanitizeSimulationResult(result);
            _activeSimulationDt = config.module == PhysicsModule.HeatTransfer ? heatTransferSolve.dt : config.dt;
            _volumeView.ApplyMaterialTheme(material);
            _volumeView.ConfigureForModule(config.module);
            _volumeView.ConfigureGrid(config.module, config.nx, config.ny);
            FrameCameraToVolume();

            if (_result != null && _result.Timeline != null && _result.Timeline.Count > 0)
            {
                _timeline = SimulationTimeline.FromResult(_result,
                    dt: _activeSimulationDt,
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

            var safeResultRange = GetSafeResultRange(_result);
            Debug.Log($"[EngineeringToolbox] {config.module} complete - material={material.Name}, max={safeResultRange.max:F3}, min={safeResultRange.min:F3}, iterations={_result?.Iterations}, timeline2D={_timeline?.Count ?? 0} frames, timeline1D={_timeline1D?.Count ?? 0} frames, dt={_activeSimulationDt:F6}, steps={_result?.Iterations ?? config.steps}, globalMin={_globalMin:F3}, globalMax={_globalMax:F3}");

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

            var resultRange = GetSafeResultRange(_result);

            _activeVectorFieldX = null;
            _activeVectorFieldY = null;

            var currentMaterial = config.GetMaterial();
            string legendTitle = GetLegendTitle();
            string legendUnit = GetLegendUnit();
            _volumeView.ApplyMaterialTheme(currentMaterial);
            UpdateMaterialSwatch(currentMaterial);
            if (_titleText != null)
            {
                _titleText.text = $"{PhysicsModuleCatalog.GetDisciplineLabel(GetActiveDiscipline())} | {PhysicsModuleCatalog.GetModuleLabel(config.module)}";
            }

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
                        UpdateLegend(resultRange.min, resultRange.max, legendTitle, legendUnit);
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
                    UpdateLegend(resultRange.min, resultRange.max, legendTitle, legendUnit);
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
                    var beamRange = ComputeFiniteMinMax(_result.Values);
                    Render1DProfile(_result.Values, beamRange.min, beamRange.max);
                    UpdateLegend(beamRange.min, beamRange.max, legendTitle, legendUnit);
                    _vectorOverlay.ClearWorld();
                    break;

                case PhysicsModule.FluidFlow2D:
                    double[,] fluidField;
                    if (_timeline != null && _currentFrame < _timeline.Count)
                    {
                        fluidField = _timeline[_currentFrame].ToArray();
                        Render2DField(fluidField, _globalMin, _globalMax);
                        UpdateLegend(_globalMin, _globalMax, legendTitle, legendUnit);
                    }
                    else
                    {
                        fluidField = _result.Field;
                        Render2DField(fluidField);
                        UpdateLegend(resultRange.min, resultRange.max, legendTitle, legendUnit);
                    }

                    if (_showVectors && _result.Vx != null && _result.Vy != null)
                    {
                        _activeVectorFieldX = _result.Vx;
                        _activeVectorFieldY = _result.Vy;
                        if (_playing)
                        {
                            UpdateFlowVectorAnimation();
                        }
                        else
                        {
                            _vectorOverlay.SetVisible(true);
                            _vectorOverlay.RenderWorld(_volumeView.VectorAnchor, _volumeView.SurfaceSize,
                                _activeVectorFieldX, _activeVectorFieldY,
                                _activeVectorFieldX.GetLength(0), _activeVectorFieldX.GetLength(1));
                        }
                    }
                    else
                    {
                        _vectorOverlay.ClearWorld();
                    }
                    break;

                case PhysicsModule.CylinderFlow:
                    double[,] cylField;
                    if (_timeline != null && _currentFrame < _timeline.Count)
                    {
                        cylField = _timeline[_currentFrame].ToArray();
                        Render2DField(cylField, _globalMin, _globalMax, _result.CylinderMask);
                        UpdateLegend(_globalMin, _globalMax, legendTitle, legendUnit);
                    }
                    else
                    {
                        cylField = _result.Vorticity ?? _result.Field;
                        Render2DField(cylField, resultRange.min, resultRange.max, _result.CylinderMask);
                        UpdateLegend(resultRange.min, resultRange.max, legendTitle, legendUnit);
                    }

                    if (_showVectors && _result.Vx != null && _result.Vy != null)
                    {
                        _activeVectorFieldX = _result.Vx;
                        _activeVectorFieldY = _result.Vy;
                        if (_playing)
                        {
                            UpdateFlowVectorAnimation(_result.CylinderMask);
                        }
                        else
                        {
                            _vectorOverlay.SetVisible(true);
                            _vectorOverlay.RenderWorld(_volumeView.VectorAnchor, _volumeView.SurfaceSize,
                                _activeVectorFieldX, _activeVectorFieldY,
                                _activeVectorFieldX.GetLength(0), _activeVectorFieldX.GetLength(1), _result.CylinderMask, 0f);
                        }
                    }
                    else
                    {
                        _vectorOverlay.ClearWorld();
                    }
                    break;

                case PhysicsModule.Magnetostatics:
                    Render2DField(_result.VectorPotential ?? _result.Field);
                    UpdateLegend(resultRange.min, resultRange.max, legendTitle, legendUnit);
                    if (_showVectors && _result.Bx != null && _result.By != null)
                    {
                        _activeVectorFieldX = _result.Bx;
                        _activeVectorFieldY = _result.By;
                        if (_playing)
                        {
                            UpdateFlowVectorAnimation();
                        }
                        else
                        {
                            _vectorOverlay.SetVisible(true);
                            _vectorOverlay.RenderWorld(_volumeView.VectorAnchor, _volumeView.SurfaceSize,
                                _activeVectorFieldX, _activeVectorFieldY,
                                _activeVectorFieldX.GetLength(0), _activeVectorFieldX.GetLength(1));
                        }
                    }
                    else
                    {
                        _vectorOverlay.ClearWorld();
                    }
                    break;

                case PhysicsModule.PlaneStress:
                    double[,] stressField = _result.StressXX ?? _result.Field;
                    var stressRange = ComputeFiniteMinMax(stressField);
                    bool hasStressContrast = double.IsFinite(stressRange.min)
                        && double.IsFinite(stressRange.max)
                        && System.Math.Abs(stressRange.max - stressRange.min) >= 1e-12;
                    if (hasStressContrast)
                    {
                        Render2DField(stressField, stressRange.min, stressRange.max);
                        UpdateLegend(stressRange.min, stressRange.max, legendTitle, legendUnit);
                    }
                    else
                    {
                        double[,] fallbackField = _result.Field ?? stressField;
                        var fallbackRange = ComputeFiniteMinMax(fallbackField);
                        Render2DField(fallbackField, fallbackRange.min, fallbackRange.max);
                        UpdateLegend(fallbackRange.min, fallbackRange.max, legendTitle, legendUnit);
                    }
                    if (_showVectors && _result.Ux != null && _result.Uy != null)
                    {
                        ScaleVectorFieldForDisplay(_result.Ux, _result.Uy, 1e-5, out _activeVectorFieldX, out _activeVectorFieldY);
                        if (_playing)
                        {
                            UpdateFlowVectorAnimation();
                        }
                        else
                        {
                            _vectorOverlay.SetVisible(true);
                            _vectorOverlay.RenderWorld(_volumeView.VectorAnchor, _volumeView.SurfaceSize,
                                _activeVectorFieldX, _activeVectorFieldY,
                                _activeVectorFieldX.GetLength(0), _activeVectorFieldX.GetLength(1));
                        }
                    }
                    else
                    {
                        _vectorOverlay.ClearWorld();
                    }
                    break;
            }

            UpdateChart();
            UpdateInfoText();
        }

        private (double dt, int steps) GetHeatTransferSolveParameters(CSharpNumerics.Physics.Materials.Engineering.EngineeringMaterial material)
        {
            const double DemoHeatTransferDtCap = 0.001;
            double requestedDt = config.dt;
            int requestedSteps = Mathf.Max(1, config.steps);
            double dx = config.width / config.nx;
            double dy = config.height / config.ny;

            if (requestedDt <= 0.0 || dx <= 0.0 || dy <= 0.0)
            {
                return (requestedDt, requestedSteps);
            }

            double volumetricHeatCapacity = material.Density * material.SpecificHeat;
            if (material.ThermalConductivity <= 0.0 || volumetricHeatCapacity <= 0.0)
            {
                return (requestedDt, requestedSteps);
            }

            double alpha = material.ThermalConductivity / volumetricHeatCapacity;
            double inverseSpacing = (1.0 / (dx * dx)) + (1.0 / (dy * dy));
            double stableDt = 0.25 / (alpha * inverseSpacing);
            double effectiveDt = System.Math.Min(stableDt, DemoHeatTransferDtCap);
            if (!double.IsFinite(effectiveDt) || effectiveDt <= 0.0 || requestedDt <= effectiveDt)
            {
                return (requestedDt, requestedSteps);
            }

            return (effectiveDt, requestedSteps);
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

        private static void ScaleVectorFieldForDisplay(double[,] sourceX, double[,] sourceY, double minimumMagnitude, out double[,] scaledX, out double[,] scaledY)
        {
            int width = sourceX.GetLength(0);
            int height = sourceX.GetLength(1);
            scaledX = sourceX;
            scaledY = sourceY;

            double maxMagnitude = 0.0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                double magnitude = System.Math.Sqrt(sourceX[x, y] * sourceX[x, y] + sourceY[x, y] * sourceY[x, y]);
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                }
            }

            if (!double.IsFinite(maxMagnitude) || maxMagnitude <= 0.0 || maxMagnitude >= minimumMagnitude)
            {
                return;
            }

            double scale = minimumMagnitude / maxMagnitude;
            scaledX = new double[width, height];
            scaledY = new double[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                scaledX[x, y] = sourceX[x, y] * scale;
                scaledY[x, y] = sourceY[x, y] * scale;
            }
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

        private void UpdateFlowVectorAnimation(bool[,] mask = null)
        {
            if (_activeVectorFieldX == null || _activeVectorFieldY == null || _volumeView == null)
            {
                return;
            }

            float phase = Mathf.Repeat(Time.time * 0.8f, 1f);
            _vectorOverlay.SetVisible(true);
            _vectorOverlay.RenderWorld(
                _volumeView.VectorAnchor,
                _volumeView.SurfaceSize,
                _activeVectorFieldX,
                _activeVectorFieldY,
                _activeVectorFieldX.GetLength(0),
                _activeVectorFieldX.GetLength(1),
                mask,
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

        private void Render2DField(double[,] field, double min, double max, bool[,] mask)
        {
            if (field == null) return;
            if (mask == null)
            {
                Render2DField(field, min, max);
                return;
            }

            var tex = _heatmapRenderer.Render(field, min, max, mask, new Color(0.02f, 0.02f, 0.03f, 1f));
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

        private SimulationResult SanitizeSimulationResult(SimulationResult result)
        {
            if (result == null)
            {
                return null;
            }

            // All SimulationResult properties are read-only from outside the DLL.
            // NaN/Infinity values are handled at render time by safe helpers
            // (ComputeFiniteMinMax, SafeMinValue, SafeMaxValue, HeatmapRenderer guards).

            return result;
        }

        private static double[,] Sanitize2DField(double[,] field)
        {
            if (field == null)
            {
                return null;
            }

            int width = field.GetLength(0);
            int height = field.GetLength(1);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!double.IsFinite(field[x, y]))
                {
                    field[x, y] = 0.0;
                }
            }

            return field;
        }

        private static double[] Sanitize1DField(double[] values)
        {
            if (values == null)
            {
                return null;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (!double.IsFinite(values[index]))
                {
                    values[index] = 0.0;
                }
            }

            return values;
        }

        private static (double min, double max) ComputeFiniteMinMax(double[,] field)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            int width = field.GetLength(0);
            int height = field.GetLength(1);

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                double value = field[x, y];
                if (!double.IsFinite(value))
                {
                    continue;
                }

                if (value < min) min = value;
                if (value > max) max = value;
            }

            if (min == double.MaxValue || max == double.MinValue)
            {
                return (0.0, 1.0);
            }

            return (min, max);
        }

        private static (double min, double max) ComputeFiniteMinMax(double[] values)
        {
            double min = double.MaxValue;
            double max = double.MinValue;

            for (int index = 0; index < values.Length; index++)
            {
                double value = values[index];
                if (!double.IsFinite(value))
                {
                    continue;
                }

                if (value < min) min = value;
                if (value > max) max = value;
            }

            if (min == double.MaxValue || max == double.MinValue)
            {
                return (0.0, 1.0);
            }

            return (min, max);
        }

        private static double SafeScalar(double value, double fallback = 0.0)
        {
            return double.IsFinite(value) ? value : fallback;
        }

        private static (double min, double max) GetSafeResultRange(SimulationResult result)
        {
            if (result == null)
            {
                return (0.0, 1.0);
            }

            // For PlaneStress, always compute from actual stress data to avoid
            // the solver's vonMises min=0 collapsing contrast.
            if (result.Type == MultiphysicsType.PlaneStress && result.StressXX != null)
            {
                return ComputeFiniteMinMax(result.StressXX);
            }

            // For BeamStress, compute from actual values to use full gradient range.
            if (result.Type == MultiphysicsType.BeamStress && result.Values != null && result.Values.Length > 0)
            {
                return ComputeFiniteMinMax(result.Values);
            }

            double safeMin = SafeScalar(result.MinValue, double.NaN);
            double safeMax = SafeScalar(result.MaxValue, double.NaN);
            if (!double.IsNaN(safeMin) && !double.IsNaN(safeMax) && safeMax >= safeMin)
            {
                return (safeMin, safeMax);
            }

            double[,] primaryField = result.Field;
            if (result.Type == MultiphysicsType.CylinderFlow && result.Vorticity != null)
            {
                primaryField = result.Vorticity;
            }
            else if (result.Type == MultiphysicsType.MagneticField && result.VectorPotential != null)
            {
                primaryField = result.VectorPotential;
            }
            else if (result.Type == MultiphysicsType.PlaneStress && result.StressXX != null)
            {
                primaryField = result.StressXX;
            }

            if (primaryField != null)
            {
                return ComputeFiniteMinMax(primaryField);
            }

            if (result.Values != null && result.Values.Length > 0)
            {
                return ComputeFiniteMinMax(result.Values);
            }

            return (0.0, 1.0);
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
            double endTime = (_chartSeries.Length - 1) * _activeSimulationDt;
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
                double val = series[index];
                if (!double.IsFinite(val)) continue;
                minValue = System.Math.Min(minValue, val);
                maxValue = System.Math.Max(maxValue, val);
            }

            if (minValue == double.MaxValue || maxValue == double.MinValue)
            {
                minValue = 0.0;
                maxValue = 1.0;
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
                double val = series[index];
                float clampedVal = double.IsFinite(val) ? (float)val : (float)minValue;
                float normalizedValue = Mathf.InverseLerp((float)minValue, (float)maxValue, clampedVal);
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
            double highlightVal = series[highlightedIndex];
            float clampedHighlight = double.IsFinite(highlightVal) ? (float)highlightVal : (float)minValue;
            float highlightedYT = Mathf.InverseLerp((float)minValue, (float)maxValue, clampedHighlight);
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
            var resultRange = GetSafeResultRange(_result);
            string info = $"<b>{PhysicsModuleCatalog.GetDisciplineLabel(GetActiveDiscipline())}</b> / {PhysicsModuleCatalog.GetModuleLabel(config.module)}  |  Material: {config.GetMaterial().Name}\n";
            info += $"Max: {FormatValueWithUnit(resultRange.max, valueUnit)}  Min: {FormatValueWithUnit(resultRange.min, valueUnit)}";

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
            else if (config.module == PhysicsModule.FluidFlow2D)
                info += $"\nInlet velocity: {config.inletVelocity:F2} m/s  |  Vectors: {(_showVectors ? "On" : "Off")}";
            else if (config.module == PhysicsModule.CylinderFlow)
                info += $"\nU∞: {config.inletVelocity:F2} m/s  R: {config.cylinderRadius:F3} m  Cd: {SafeScalar(_result.DragCoefficient):F3}  St: {SafeScalar(_result.StrouhalNumber):F3}";
            else if (config.module == PhysicsModule.Magnetostatics)
                info += $"\nJ: {config.currentDensity:E1} A/m²  |  B-field vectors: {(_showVectors ? "On" : "Off")}";
            else if (config.module == PhysicsModule.PlaneStress)
                info += $"\nLoad: {config.pointLoadValue:F0} N  |  Displacement vectors: {(_showVectors ? "On" : "Off")}";

            if (_activeMoveAxis != VolumeMoveAxis.None)
                info += $"\nRotating axis: {_activeMoveAxis}";

            _infoText.text = info;
        }

        private void UpdateStatusBar()
        {
            string status = "";
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
            var bottomPanel = CreatePanel(canvasGo.transform, "BottomPanel", new Vector2(0.14f, 0.015f), new Vector2(0.86f, 0.135f), new Color(0.03f, 0.08f, 0.12f, 0.8f));
            var titlePanel = CreatePanel(canvasGo.transform, "TitlePanel", new Vector2(0.70f, 0.88f), new Vector2(0.97f, 0.96f), new Color(0.05f, 0.16f, 0.24f, 0.66f));
            var legendPanel = CreatePanel(canvasGo.transform, "LegendPanel", new Vector2(0.90f, 0.20f), new Vector2(0.975f, 0.56f), new Color(0.03f, 0.08f, 0.12f, 0.74f));
            var materialPanel = CreatePanel(canvasGo.transform, "MaterialPanel", new Vector2(0.79f, 0.77f), new Vector2(0.975f, 0.84f), new Color(0.03f, 0.08f, 0.12f, 0.72f));
            CreateChartOverlay(canvasGo.transform);

            _infoText = CreateText(topPanel, "InfoText", 16, TextAnchor.UpperLeft);
            _statusText = CreateText(bottomPanel, "StatusBar", 13, TextAnchor.MiddleLeft);
            _titleText = CreateText(titlePanel, "TitleText", 18, TextAnchor.MiddleCenter);
            _titleText.text = "Engineering Toolbox | Category / Submodel";
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
            _disciplineButtons = new Button[DisciplineOrder.Length];
            _disciplineButtonTexts = new Text[DisciplineOrder.Length];
            _submoduleButtons = new Button[3];
            _submoduleButtonTexts = new Text[3];

            float disciplineWidth = 0.115f;
            float disciplineGap = 0.01f;
            float disciplineStart = 0.02f;
            for (int i = 0; i < DisciplineOrder.Length; i++)
            {
                float xMin = disciplineStart + i * (disciplineWidth + disciplineGap);
                float xMax = xMin + disciplineWidth;
                PhysicsDiscipline discipline = DisciplineOrder[i];
                _disciplineButtons[i] = CreateButton(parent, $"DisciplineButton_{discipline}", PhysicsModuleCatalog.GetDisciplineLabel(discipline), new Vector2(xMin, 0.54f), new Vector2(xMax, 0.92f), () => SelectDiscipline(discipline), out _disciplineButtonTexts[i]);
                _disciplineButtonTexts[i].fontSize = 11;
            }

            float submoduleWidth = 0.155f;
            float submoduleGap = 0.01f;
            float submoduleStart = 0.02f;
            for (int i = 0; i < _submoduleButtons.Length; i++)
            {
                float xMin = submoduleStart + i * (submoduleWidth + submoduleGap);
                float xMax = xMin + submoduleWidth;
                int submoduleIndex = i;
                _submoduleButtons[i] = CreateButton(parent, $"SubmoduleButton_{i}", "Submodel", new Vector2(xMin, 0.08f), new Vector2(xMax, 0.46f), () => SelectActiveSubmodule(submoduleIndex), out _submoduleButtonTexts[i]);
                _submoduleButtonTexts[i].fontSize = 11;
            }

            _playPauseButton = CreateButton(parent, "PlayPauseButton", "Play", new Vector2(0.56f, 0.18f), new Vector2(0.64f, 0.82f), TogglePlayback, out _playPauseButtonText);
            _previousFrameButton = CreateButton(parent, "PrevFrameButton", "Prev", new Vector2(0.65f, 0.18f), new Vector2(0.71f, 0.82f), () => StepFromButton(-1), out _);
            _nextFrameButton = CreateButton(parent, "NextFrameButton", "Next", new Vector2(0.72f, 0.18f), new Vector2(0.78f, 0.82f), () => StepFromButton(1), out _);
            _vectorToggleButton = CreateButton(parent, "VectorToggleButton", "Vectors", new Vector2(0.79f, 0.18f), new Vector2(0.87f, 0.82f), ToggleVectors, out _vectorToggleButtonText);
            _chartToggleButton = CreateButton(parent, "ChartToggleButton", "Chart", new Vector2(0.88f, 0.18f), new Vector2(0.93f, 0.82f), ToggleChartOverlay, out _chartToggleButtonText);
            _materialButton = CreateButton(parent, "MaterialButton", "Material", new Vector2(0.94f, 0.18f), new Vector2(0.985f, 0.82f), CycleMaterialFromButton, out _);
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

        private void SelectActiveSubmodule(int index)
        {
            PhysicsModule[] activeSubmodules = GetActiveSubmodules();
            if (index < 0 || index >= activeSubmodules.Length)
            {
                return;
            }

            SelectModule(activeSubmodules[index]);
        }

        private string GetLegendTitle()
        {
            switch (config.module)
            {
                case PhysicsModule.HeatTransfer: return "Temperature";
                case PhysicsModule.Electrostatics: return "Potential";
                case PhysicsModule.PipeFlow: return "Velocity";
                case PhysicsModule.BeamStress: return "Deflection";
                case PhysicsModule.FluidFlow2D: return "Velocity Mag";
                case PhysicsModule.CylinderFlow: return "Vorticity";
                case PhysicsModule.Magnetostatics: return "Vector Potential";
                case PhysicsModule.PlaneStress: return "Stress σxx";
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
                case PhysicsModule.FluidFlow2D: return "Average Velocity vs Time";
                case PhysicsModule.CylinderFlow: return "Average Velocity vs Time";
                case PhysicsModule.Magnetostatics: return "Vector Potential";
                case PhysicsModule.PlaneStress: return "Stress Distribution";
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
                case PhysicsModule.FluidFlow2D: return "m/s";
                case PhysicsModule.CylinderFlow: return "m/s";
                case PhysicsModule.Magnetostatics: return "T·m";
                case PhysicsModule.PlaneStress: return "Pa";
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
                case PhysicsModule.FluidFlow2D: return "m/s";
                case PhysicsModule.CylinderFlow: return "1/s";
                case PhysicsModule.Magnetostatics: return "T·m";
                case PhysicsModule.PlaneStress: return "Pa";
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
            if (config.module == PhysicsModule.BeamStress)
                return value * 1000.0; // m → mm
            return value;
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
            if (_disciplineButtons == null || _disciplineButtonTexts == null || _submoduleButtons == null || _submoduleButtonTexts == null)
            {
                return;
            }

            PhysicsDiscipline activeDiscipline = GetActiveDiscipline();
            for (int i = 0; i < _disciplineButtons.Length; i++)
            {
                bool isActive = DisciplineOrder[i] == activeDiscipline;
                ApplyButtonVisualState(_disciplineButtons[i], _disciplineButtonTexts[i], true, isActive);
            }

            PhysicsModule[] activeSubmodules = GetActiveSubmodules();
            for (int i = 0; i < _submoduleButtons.Length; i++)
            {
                bool hasModule = i < activeSubmodules.Length;
                if (_submoduleButtonTexts[i] != null)
                {
                    _submoduleButtonTexts[i].text = hasModule ? PhysicsModuleCatalog.GetModuleLabel(activeSubmodules[i]) : "-";
                }

                bool isActive = hasModule && activeSubmodules[i] == config.module;
                ApplyButtonVisualState(_submoduleButtons[i], _submoduleButtonTexts[i], hasModule, isActive);
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
