using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using QuantumCircuitViz.Core;
using QuantumCircuitViz.Visualization;
using QuantumCircuitViz.Export;

using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Engines.Quantum.Algorithms;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Demo
{
    /// <summary>
    /// Self-contained demo: builds a quantum circuit, steps through gate-by-gate,
    /// visualises Bloch spheres and measurement probabilities.
    /// Attach to any GameObject → enter Play Mode → see the quantum circuit.
    ///
    /// Controls:
    ///   Right/Left arrow — step through gates
    ///   Space — play / pause auto-advance
    ///   1-7 — load preset circuits
    ///   N — toggle noise on/off
    ///   R — reset to |0⟩
    ///   G — toggle circuit builder (gate palette + canvas)
    ///   M — run measurement sampling animation
    ///   Click Bloch sphere — inspect qubit
    ///   Q — run QEC comparison (protected vs unprotected)
    ///   E — start/stop RL training
    ///   C — cycle QEC code (BitFlip3 → PhaseFlip3 → Steane7 → Shor9)
    ///   D — toggle density matrix heatmap
    ///   W — toggle 3D world-space circuit
    ///   F5 — copy QASM to clipboard
    ///   F12 — take screenshot
    /// </summary>
    public class DemoSimulation : MonoBehaviour
    {
        [Header("Simulation")]
        public SimulationConfig config = new SimulationConfig();

        [Header("Demo")]
        [Tooltip("Auto-step through gates on start")]
        public bool autoPlay = true;

        private CircuitRunner _runner;
        private int _currentStep = -1; // -1 = initial |0…0⟩ state
        private bool _playing;
        private float _playTimer;
        private string _currentTitle = "";

        // Visualisation
        private BlochSphereRenderer[] _spheres;
        private MeasurementHistogram _histogram;
        private CircuitDiagramRenderer _diagram;
        private Canvas _canvas;
        private Text _infoText;
        private Text _controlsText;

        // Phase 2 — Build & Measure
        private GatePalette _gatePalette;
        private CircuitCanvas _circuitCanvas;
        private EntanglementVisualizer _entanglement;
        private QubitInspectorPanel _inspector;
        private bool _builderMode;

        // Phase 3 — RL Error Correction
        private ErrorCorrectionVisualizer _qecViz;
        private RLTrainingPanel _rlPanel;
        private ErrorHeatmapOverlay _errorHeatmap;
        private NoiseGlitchEffect[] _noiseGlitches;
        private QECRunner _qecRunner;
        private RLErrorCorrectionRunner _rlRunner;
        private bool _qecMode;
        private readonly Queue<EpisodeReport> _pendingReports = new Queue<EpisodeReport>();

        // Phase 4 — Polish & Export
        private DensityMatrixHeatmap _densityHeatmap;
        private WorldSpaceCircuit _worldCircuit;
        private bool _worldCircuitVisible;

        // Cached Bloch vectors for entanglement + inspector
        private Vector3[] _cachedBlochVectors;

        private void Start()
        {
            EnsureCamera();
            EnsureEventSystem();
            CreateCanvas();
            BuildPhase2UI();
            BuildPhase3UI();
            BuildPhase4UI();
            LoadPreset_BellState();
            _playing = autoPlay;
        }

        private void Update()
        {
            HandleInput();
            HandleSphereClick();
            DrainRLReports();

            if (_playing && _runner != null)
            {
                _playTimer += Time.deltaTime;
                if (_playTimer >= config.animationSpeed)
                {
                    _playTimer = 0f;
                    StepForward();
                }
            }
        }

        private void HandleInput()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.RightArrow)) StepForward();
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) StepBackward();
            else if (Input.GetKeyDown(KeyCode.Space)) _playing = !_playing;
            else if (Input.GetKeyDown(KeyCode.R)) ResetCircuit();
            else if (Input.GetKeyDown(KeyCode.N)) ToggleNoise();
            else if (Input.GetKeyDown(KeyCode.G)) ToggleBuilder();
            else if (Input.GetKeyDown(KeyCode.M)) RunMeasurementSampling();
            else if (Input.GetKeyDown(KeyCode.Q)) RunQECComparison();
            else if (Input.GetKeyDown(KeyCode.E)) ToggleRLTraining();
            else if (Input.GetKeyDown(KeyCode.C)) CycleQECCode();
            else if (Input.GetKeyDown(KeyCode.D)) ToggleDensityMatrix();
            else if (Input.GetKeyDown(KeyCode.W)) ToggleWorldCircuit();
            else if (Input.GetKeyDown(KeyCode.F5)) ExportQASMToClipboard();
            else if (Input.GetKeyDown(KeyCode.F12)) TakeScreenshot();
            else if (Input.GetKeyDown(KeyCode.Alpha1)) LoadPreset_BellState();
            else if (Input.GetKeyDown(KeyCode.Alpha2)) LoadPreset_GHZ();
            else if (Input.GetKeyDown(KeyCode.Alpha3)) LoadPreset_SuperpositionChain();
            else if (Input.GetKeyDown(KeyCode.Alpha4)) LoadPreset_PhaseKickback();
            else if (Input.GetKeyDown(KeyCode.Alpha5)) LoadPreset_Grover();
            else if (Input.GetKeyDown(KeyCode.Alpha6)) LoadPreset_Toffoli();
            else if (Input.GetKeyDown(KeyCode.Alpha7)) LoadPreset_QFT();
#elif HAS_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.rightArrowKey.wasPressedThisFrame) StepForward();
            else if (kb.leftArrowKey.wasPressedThisFrame) StepBackward();
            else if (kb.spaceKey.wasPressedThisFrame) _playing = !_playing;
            else if (kb.rKey.wasPressedThisFrame) ResetCircuit();
            else if (kb.nKey.wasPressedThisFrame) ToggleNoise();
            else if (kb.gKey.wasPressedThisFrame) ToggleBuilder();
            else if (kb.mKey.wasPressedThisFrame) RunMeasurementSampling();
            else if (kb.qKey.wasPressedThisFrame) RunQECComparison();
            else if (kb.eKey.wasPressedThisFrame) ToggleRLTraining();
            else if (kb.cKey.wasPressedThisFrame) CycleQECCode();
            else if (kb.dKey.wasPressedThisFrame) ToggleDensityMatrix();
            else if (kb.wKey.wasPressedThisFrame) ToggleWorldCircuit();
            else if (kb.f5Key.wasPressedThisFrame) ExportQASMToClipboard();
            else if (kb.f12Key.wasPressedThisFrame) TakeScreenshot();
            else if (kb.digit1Key.wasPressedThisFrame) LoadPreset_BellState();
            else if (kb.digit2Key.wasPressedThisFrame) LoadPreset_GHZ();
            else if (kb.digit3Key.wasPressedThisFrame) LoadPreset_SuperpositionChain();
            else if (kb.digit4Key.wasPressedThisFrame) LoadPreset_PhaseKickback();
            else if (kb.digit5Key.wasPressedThisFrame) LoadPreset_Grover();
            else if (kb.digit6Key.wasPressedThisFrame) LoadPreset_Toffoli();
            else if (kb.digit7Key.wasPressedThisFrame) LoadPreset_QFT();
#endif
        }

        /// <summary>Raycast click to Bloch sphere → show inspector panel.</summary>
        private void HandleSphereClick()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButtonDown(0)) return;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
#elif HAS_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
#else
            return;
#endif

            if (_spheres == null || _inspector == null) return;

            // Check if we hit any Bloch sphere object
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                for (int q = 0; q < _spheres.Length; q++)
                {
                    if (hit.transform.IsChildOf(_spheres[q].transform) ||
                        hit.transform == _spheres[q].transform)
                    {
                        var bloch = _cachedBlochVectors != null && q < _cachedBlochVectors.Length
                            ? _cachedBlochVectors[q] : Vector3.up;
                        _inspector.ShowQubit(q, bloch);
                        return;
                    }
                }
            }
        }

        // ── Phase 2: Builder Mode ────────────────────────────────────

        private void ToggleBuilder()
        {
            _builderMode = !_builderMode;
            Debug.Log($"[QuantumViz] Builder: {(_builderMode ? "ON" : "OFF")}");

            if (_gatePalette != null)
                _gatePalette.gameObject.SetActive(_builderMode);
            if (_circuitCanvas != null)
                _circuitCanvas.gameObject.SetActive(_builderMode);

            // Hide presets diagram when builder is active
            if (_diagram != null)
                _diagram.gameObject.SetActive(!_builderMode);

            UpdateControlsHint();
        }

        private void OnBuilderCircuitChanged(CircuitRunner runner)
        {
            _runner = runner;
            _currentTitle = "Custom Circuit";
            _currentStep = runner.Steps.Count - 1; // show latest state

            if (config.enableNoise)
                _runner.WithNoise(config.depolarizingRate, config.dephasingRate, config.amplitudeDampingGamma);

            BuildSpheres();
            BuildHistogram();
            UpdateVisualisation();
            UpdateInfo("Custom Circuit");
        }

        private void RunMeasurementSampling()
        {
            if (_runner == null || _histogram == null) return;

            QuantumState state;
            if (_currentStep < 0)
            {
                var amps = new CSharpNumerics.Numerics.Objects.ComplexVectorN(1 << _runner.QubitCount);
                amps[0] = new CSharpNumerics.Numerics.Objects.ComplexNumber(1, 0);
                state = new QuantumState(amps);
            }
            else
            {
                state = _runner.RunUpTo(_currentStep);
            }

            var probs = state.GetProbabilities();
            _histogram.AnimateSampling(probs, 256, 200f);
            Debug.Log("[QuantumViz] Measurement sampling: 256 shots");
        }

        // ── Preset Circuits ──────────────────────────────────────────

        private void LoadPreset_BellState()
        {
            var circuit = QuantumCircuitBuilder.New(2).H(0).CNOT(0, 1).Build();
            ApplyCircuit(CircuitRunner.FromBuilder(circuit), "Bell State  (H → CNOT)");
        }

        private void LoadPreset_GHZ()
        {
            var circuit = QuantumCircuitBuilder.New(3).H(0).CNOT(0, 1).CNOT(0, 2).Build();
            ApplyCircuit(CircuitRunner.FromBuilder(circuit), "GHZ State  (H → CNOT → CNOT)");
        }

        private void LoadPreset_SuperpositionChain()
        {
            int n = Mathf.Clamp(config.qubitCount, 1, 5);
            var builder = QuantumCircuitBuilder.New(n);
            for (int i = 0; i < n; i++) builder.H(i);
            ApplyCircuit(CircuitRunner.FromBuilder(builder.Build()), $"Superposition Chain  ({n}× Hadamard)");
        }

        private void LoadPreset_PhaseKickback()
        {
            var circuit = QuantumCircuitBuilder.New(2)
                .H(0).X(1).H(1).CNOT(0, 1).H(0).Build();
            ApplyCircuit(CircuitRunner.FromBuilder(circuit), "Phase Kickback  (Deutsch-like)");
        }

        private void LoadPreset_Grover()
        {
            var circuit = GroverSearch.CreateCircuit(3, new[] { 0, 1, 2 }, new[] { 5 });
            ApplyCircuit(CircuitRunner.FromBuilder(circuit), "Grover Search  (target |101⟩)");
        }

        private void LoadPreset_Toffoli()
        {
            var circuit = QuantumCircuitBuilder.New(3)
                .X(0).X(1)
                .Toffoli(0, 1, 2)
                .Build();
            ApplyCircuit(CircuitRunner.FromBuilder(circuit), "Toffoli  (X X → CCX)");
        }

        private void LoadPreset_QFT()
        {
            var builder = QuantumCircuitBuilder.New(3).X(0);
            builder.ApplyQFT(0, 1, 2);
            ApplyCircuit(CircuitRunner.FromBuilder(builder.Build()), "Quantum Fourier Transform  (3 qubits)");
        }

        private void ToggleNoise()
        {
            config.enableNoise = !config.enableNoise;
            Debug.Log($"[QuantumViz] Noise: {(config.enableNoise ? "ON" : "OFF")}  (depol={config.depolarizingRate})");
            if (_runner != null)
            {
                if (config.enableNoise)
                    _runner.WithNoise(config.depolarizingRate, config.dephasingRate, config.amplitudeDampingGamma);
                else
                    _runner.WithNoise(0, 0, 0);
                UpdateVisualisation();
            }
        }

        // ── Circuit Management ───────────────────────────────────────

        private void ApplyCircuit(CircuitRunner runner, string title)
        {
            // Exit builder mode when loading a preset
            if (_builderMode) ToggleBuilder();

            _runner = runner;
            _currentTitle = title;
            _currentStep = -1;
            _playTimer = 0f;

            if (config.enableNoise)
                _runner.WithNoise(config.depolarizingRate, config.dephasingRate, config.amplitudeDampingGamma);

            _histogram?.StopSampling();

            // Hide QEC panels when switching presets
            _qecMode = false;
            _qecViz?.Hide();
            _errorHeatmap?.Hide();
            ClearNoiseGlitch();

            // Hide Phase 4 overlays
            _worldCircuitVisible = false;
            _worldCircuit?.Clear();

            BuildSpheres();
            BuildHistogram();
            BuildDiagram();
            UpdateVisualisation();

            Debug.Log($"[QuantumViz] Loaded: {title}  ({_runner.QubitCount} qubits, {_runner.Steps.Count} gates)");
            UpdateInfo(title);
        }

        private void ResetCircuit()
        {
            _currentStep = -1;
            _playTimer = 0f;
            _histogram?.StopSampling();
            UpdateVisualisation();
        }

        private void StepForward()
        {
            if (_runner == null) return;
            if (_currentStep < _runner.Steps.Count - 1)
            {
                _currentStep++;
                UpdateVisualisation();
            }
            else
            {
                _playing = false;
            }
        }

        private void StepBackward()
        {
            if (_runner == null) return;
            if (_currentStep >= 0)
            {
                _currentStep--;
                UpdateVisualisation();
            }
        }

        // ── Visualisation ────────────────────────────────────────────

        private void UpdateVisualisation()
        {
            if (_runner == null) return;

            QuantumState state;
            if (_currentStep < 0)
            {
                var amps = new CSharpNumerics.Numerics.Objects.ComplexVectorN(1 << _runner.QubitCount);
                amps[0] = new CSharpNumerics.Numerics.Objects.ComplexNumber(1, 0);
                state = new QuantumState(amps);
            }
            else
            {
                state = _runner.RunUpTo(_currentStep);
            }

            // Bloch spheres
            UpdateBlochSpheres(state);

            // Entanglement links
            _entanglement?.UpdateEntanglement(_cachedBlochVectors);

            // Qubit inspector — refresh if a qubit is selected
            if (_inspector != null && _inspector.SelectedQubit >= 0 && _cachedBlochVectors != null)
            {
                int q = _inspector.SelectedQubit;
                if (q < _cachedBlochVectors.Length)
                    _inspector.ShowQubit(q, _cachedBlochVectors[q]);
            }

            // Histogram
            var probs = state.GetProbabilities();
            _histogram?.UpdateProbabilities(probs);

            // Circuit diagram
            _diagram?.Render(_runner.Steps, _runner.QubitCount, _currentStep);

            // Phase 4: density matrix + world circuit
            if (_densityHeatmap != null && _densityHeatmap.gameObject.activeSelf)
                _densityHeatmap.UpdateState(state);
            if (_worldCircuitVisible)
                _worldCircuit?.SetCurrentStep(_currentStep);

            // Info bar
            string noise = config.enableNoise ? " [NOISY]" : "";
            string stepLabel = _currentStep < 0 ? $"Init |0…0⟩{noise}" :
                $"Step {_currentStep + 1}/{_runner.Steps.Count}: {_runner.Steps[_currentStep].Gate.GetType().Name.Replace("Gate", "")}{noise}";
            UpdateStepInfo(stepLabel);
        }

        private void UpdateBlochSpheres(QuantumState state)
        {
            if (_spheres == null) return;

            _cachedBlochVectors = new Vector3[state.QubitCount];

            if (state.QubitCount == 1)
            {
                var bloch = state.GetBlochVector();
                var v = new Vector3((float)bloch.X, (float)bloch.Z, (float)bloch.Y);
                _cachedBlochVectors[0] = v;
                _spheres[0].SetState(v);
            }
            else
            {
                for (int q = 0; q < state.QubitCount && q < _spheres.Length; q++)
                {
                    var bloch = ComputeReducedBlochVector(state, q);
                    _cachedBlochVectors[q] = bloch;
                    _spheres[q].SetState(bloch);
                }
            }
        }

        /// <summary>
        /// Compute the Bloch vector for qubit q by tracing out all other qubits.
        /// ρ_q = Tr_{others}(|ψ⟩⟨ψ|), then x=Tr(ρ·σx), y=Tr(ρ·σy), z=Tr(ρ·σz).
        /// </summary>
        private Vector3 ComputeReducedBlochVector(QuantumState state, int qubit)
        {
            int n = state.QubitCount;
            int dim = 1 << n;
            var amps = state.Amplitudes;

            double rho00_r = 0, rho01_r = 0, rho01_i = 0, rho11_r = 0;

            for (int i = 0; i < dim; i++)
            {
                int qBit_i = (i >> qubit) & 1;
                for (int j = 0; j < dim; j++)
                {
                    int qBit_j = (j >> qubit) & 1;

                    int mask = ~(1 << qubit) & ((1 << n) - 1);
                    if ((i & mask) != (j & mask)) continue;

                    var ai = amps[i];
                    var aj = amps[j];
                    double re = ai.realPart * aj.realPart + ai.imaginaryPart * aj.imaginaryPart;
                    double im = ai.imaginaryPart * aj.realPart - ai.realPart * aj.imaginaryPart;

                    if (qBit_i == 0 && qBit_j == 0) rho00_r += re;
                    else if (qBit_i == 0 && qBit_j == 1) { rho01_r += re; rho01_i += im; }
                    else if (qBit_i == 1 && qBit_j == 1) rho11_r += re;
                }
            }

            float bx = (float)(2.0 * rho01_r);
            float by = (float)(2.0 * rho01_i);
            float bz = (float)(rho00_r - rho11_r);

            return new Vector3(bx, bz, by);
        }

        // ── Scene Setup ──────────────────────────────────────────────

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("MainCamera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 2f, -8f);
            cam.transform.LookAt(Vector3.zero);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
        }

        private void CreateCanvas()
        {
            var canvasGo = new GameObject("QuantumCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Info panel (top-left)
            var infoGo = new GameObject("InfoText");
            var infoRt = infoGo.AddComponent<RectTransform>();
            infoRt.SetParent(_canvas.transform, false);
            infoRt.anchorMin = new Vector2(0.02f, 0.85f);
            infoRt.anchorMax = new Vector2(0.60f, 0.98f);
            infoRt.offsetMin = Vector2.zero;
            infoRt.offsetMax = Vector2.zero;
            _infoText = infoGo.AddComponent<Text>();
            _infoText.font = Font.CreateDynamicFontFromOSFont("Consolas", 16);
            _infoText.fontSize = 16;
            _infoText.color = new Color(0f, 0.9f, 1f);

            // Controls hint (bottom-center)
            var ctrlGo = new GameObject("ControlsText");
            var ctrlRt = ctrlGo.AddComponent<RectTransform>();
            ctrlRt.SetParent(_canvas.transform, false);
            ctrlRt.anchorMin = new Vector2(0.02f, 0.42f);
            ctrlRt.anchorMax = new Vector2(0.55f, 0.58f);
            ctrlRt.offsetMin = Vector2.zero;
            ctrlRt.offsetMax = Vector2.zero;
            _controlsText = ctrlGo.AddComponent<Text>();
            _controlsText.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            _controlsText.fontSize = 11;
            _controlsText.color = new Color(0.5f, 0.6f, 0.7f);
            UpdateControlsHint();
        }

        private void BuildPhase2UI()
        {
            var canvasRt = _canvas.GetComponent<RectTransform>();

            // Gate palette (left strip) — starts hidden
            var paletteGo = new GameObject("PaletteHost");
            _gatePalette = paletteGo.AddComponent<GatePalette>();
            _gatePalette.Initialise(canvasRt);
            _gatePalette.gameObject.SetActive(false);

            // Circuit canvas (top area) — starts hidden
            var canvasHost = new GameObject("CircuitCanvasHost");
            _circuitCanvas = canvasHost.AddComponent<CircuitCanvas>();
            _circuitCanvas.Initialise(canvasRt, config.qubitCount, 10, _gatePalette);
            _circuitCanvas.OnCircuitChanged += OnBuilderCircuitChanged;
            _circuitCanvas.gameObject.SetActive(false);

            // Entanglement visualizer (always active)
            var entGo = new GameObject("EntanglementViz");
            _entanglement = entGo.AddComponent<EntanglementVisualizer>();

            // Qubit inspector panel (right side)
            var inspGo = new GameObject("InspectorHost");
            _inspector = inspGo.AddComponent<QubitInspectorPanel>();
            _inspector.Initialise(canvasRt);
        }

        // ── Phase 3: RL Error Correction ─────────────────────────────

        private void BuildPhase3UI()
        {
            var canvasRt = _canvas.GetComponent<RectTransform>();

            // Error correction split-view
            var qecGo = new GameObject("QECVizHost");
            _qecViz = qecGo.AddComponent<ErrorCorrectionVisualizer>();
            _qecViz.Initialise(canvasRt);

            // RL training panel
            var rlGo = new GameObject("RLPanelHost");
            _rlPanel = rlGo.AddComponent<RLTrainingPanel>();
            _rlPanel.Initialise(canvasRt);

            // Error heatmap overlay
            var heatGo = new GameObject("ErrorHeatmapHost");
            _errorHeatmap = heatGo.AddComponent<ErrorHeatmapOverlay>();
            _errorHeatmap.Initialise(canvasRt, config.qubitCount);
        }

        private void RunQECComparison()
        {
            _qecMode = true;
            _qecRunner = new QECRunner(config.qecCode);

            // Initial state |0⟩
            var initial = new CSharpNumerics.Numerics.Objects.ComplexVectorN(2);
            initial[0] = new CSharpNumerics.Numerics.Objects.ComplexNumber(1, 0);

            // Single cycle for syndrome display
            var cycle = _qecRunner.RunOnce(initial, config.qecErrorRate);
            _qecViz.ShowCycleResult(cycle, config.qecCode.ToString(), _qecRunner.PhysicalQubits);

            // Monte-Carlo comparison
            var comparison = _qecRunner.RunComparison(initial, config.qecErrorRate, config.qecRounds);
            _qecViz.ShowComparison(comparison, config.qecCode.ToString());

            // Show error heatmap with uniform rate
            _errorHeatmap.Show();
            _errorHeatmap.SetUniformRate(config.qecErrorRate);

            // Noise glitch on spheres
            ApplyNoiseGlitch(config.qecErrorRate);

            Debug.Log($"[QuantumViz] QEC {config.qecCode}: protected={comparison.ProtectedFidelity:F4}  " +
                      $"unprotected={comparison.UnprotectedFidelity:F4}  Δ={comparison.Improvement:F4}");
            UpdateControlsHint();
        }

        private void ToggleRLTraining()
        {
            if (_rlRunner != null && _rlRunner.IsTraining)
            {
                _rlRunner.Stop();
                _rlPanel.SetStatus("Training stopped");
                Debug.Log("[QuantumViz] RL training stopped");
                return;
            }

            // Build a target circuit: Bell state as default
            var target = QuantumCircuitBuilder.New(2).H(0).CNOT(0, 1).Build();

            _rlRunner = new RLErrorCorrectionRunner();
            _rlRunner.OnEpisodeComplete += OnRLEpisode;
            _rlRunner.OnTrainingComplete += OnRLTrainingDone;

            _rlPanel.Clear();
            _rlPanel.Show();

            _rlRunner.StartTraining(
                qubitCount: 2,
                targetCircuit: target,
                episodes: config.rlEpisodes,
                maxGates: config.rlMaxGates,
                fidelityThreshold: config.rlFidelityThreshold,
                learningRate: config.rlLearningRate,
                gamma: config.rlGamma);

            Debug.Log($"[QuantumViz] RL training started: {config.rlEpisodes} episodes");
        }

        private void OnRLEpisode(EpisodeReport report)
        {
            // Called from background thread — queue for main thread
            lock (_pendingReports) { _pendingReports.Enqueue(report); }
        }

        private void OnRLTrainingDone(TrainingSummary summary)
        {
            // Queue a special marker report
            lock (_pendingReports)
            {
                _pendingReports.Enqueue(new EpisodeReport
                {
                    Episode = -1, // sentinel
                    BestFidelity = summary.BestFidelity,
                    TotalReward = summary.AverageReward
                });
            }
        }

        private void DrainRLReports()
        {
            lock (_pendingReports)
            {
                while (_pendingReports.Count > 0)
                {
                    var r = _pendingReports.Dequeue();
                    if (r.Episode == -1)
                    {
                        _rlPanel.SetStatus($"Training complete — best fidelity {r.BestFidelity:F4}");
                        Debug.Log($"[QuantumViz] RL done: best={r.BestFidelity:F4}  avg={r.TotalReward:F3}");
                    }
                    else
                    {
                        _rlPanel.AddEpisode(r);
                    }
                }
            }
        }

        private void CycleQECCode()
        {
            config.qecCode = config.qecCode switch
            {
                QECCodeType.BitFlip3 => QECCodeType.PhaseFlip3,
                QECCodeType.PhaseFlip3 => QECCodeType.Steane7,
                QECCodeType.Steane7 => QECCodeType.Shor9,
                _ => QECCodeType.BitFlip3
            };
            Debug.Log($"[QuantumViz] QEC code → {config.qecCode}");

            if (_qecMode) RunQECComparison();
            UpdateControlsHint();
        }

        private void ApplyNoiseGlitch(float errorRate)
        {
            if (_spheres == null) return;
            // Lazily add NoiseGlitchEffect components
            if (_noiseGlitches == null || _noiseGlitches.Length != _spheres.Length)
            {
                _noiseGlitches = new NoiseGlitchEffect[_spheres.Length];
                for (int i = 0; i < _spheres.Length; i++)
                {
                    _noiseGlitches[i] = _spheres[i].gameObject.GetComponent<NoiseGlitchEffect>();
                    if (_noiseGlitches[i] == null)
                        _noiseGlitches[i] = _spheres[i].gameObject.AddComponent<NoiseGlitchEffect>();
                }
            }
            foreach (var ng in _noiseGlitches)
                ng.SetErrorRate(errorRate);
        }

        private void ClearNoiseGlitch()
        {
            if (_noiseGlitches == null) return;
            foreach (var ng in _noiseGlitches)
                if (ng != null) ng.Disable();
        }

        // ── Phase 4: Polish & Export ─────────────────────────────────

        private void BuildPhase4UI()
        {
            var canvasRt = _canvas.GetComponent<RectTransform>();

            // Density matrix heatmap (bottom-left)
            var dmGo = new GameObject("DensityHeatmapHost");
            _densityHeatmap = dmGo.AddComponent<DensityMatrixHeatmap>();
            _densityHeatmap.Initialise(canvasRt);

            // 3D world-space circuit
            var wcGo = new GameObject("WorldCircuit");
            wcGo.transform.position = new Vector3(0f, -3f, 2f);
            _worldCircuit = wcGo.AddComponent<WorldSpaceCircuit>();
        }

        private void ToggleDensityMatrix()
        {
            if (_densityHeatmap == null) return;

            if (_densityHeatmap.gameObject.activeSelf)
            {
                _densityHeatmap.Hide();
                Debug.Log("[QuantumViz] Density matrix: OFF");
            }
            else
            {
                _densityHeatmap.Show();
                RefreshDensityMatrix();
                Debug.Log("[QuantumViz] Density matrix: ON");
            }
        }

        private void RefreshDensityMatrix()
        {
            if (_runner == null || _densityHeatmap == null) return;
            QuantumState state;
            if (_currentStep < 0)
            {
                var amps = new CSharpNumerics.Numerics.Objects.ComplexVectorN(1 << _runner.QubitCount);
                amps[0] = new CSharpNumerics.Numerics.Objects.ComplexNumber(1, 0);
                state = new QuantumState(amps);
            }
            else
            {
                state = _runner.RunUpTo(_currentStep);
            }
            _densityHeatmap.UpdateState(state);
        }

        private void ToggleWorldCircuit()
        {
            if (_worldCircuit == null || _runner == null) return;
            _worldCircuitVisible = !_worldCircuitVisible;

            if (_worldCircuitVisible)
            {
                _worldCircuit.BuildCircuit(_runner);
                _worldCircuit.SetCurrentStep(_currentStep);
                _worldCircuit.Show();
                Debug.Log("[QuantumViz] 3D circuit: ON");
            }
            else
            {
                _worldCircuit.Hide();
                Debug.Log("[QuantumViz] 3D circuit: OFF");
            }
        }

        private void ExportQASMToClipboard()
        {
            if (_runner == null) return;
            string qasm = CircuitExporter.ToQASM(_runner);
            GUIUtility.systemCopyBuffer = qasm;
            Debug.Log("[QuantumViz] QASM copied to clipboard:\n" + qasm);
        }

        private void ExportJSONToClipboard()
        {
            if (_runner == null) return;
            string json = CircuitExporter.ToJSON(_runner);
            GUIUtility.systemCopyBuffer = json;
            Debug.Log("[QuantumViz] JSON copied to clipboard");
        }

        private void TakeScreenshot()
        {
            string path = ScreenshotUtility.Capture(2);
            Debug.Log($"[QuantumViz] Screenshot saved to: {path}");
        }

        private void OnDestroy()
        {
            _rlRunner?.Stop();
        }

        private void UpdateControlsHint()
        {
            if (_controlsText == null) return;
            string builderHint = _builderMode ? " [BUILDER]" : "";
            string qecHint = _qecMode ? $" [QEC:{config.qecCode}]" : "";
            _controlsText.text =
                $"←/→ Step  Space Play  R Reset  N Noise  G Builder  M Measure{builderHint}\n" +
                $"Q QEC  E RL-Train  C Cycle-Code{qecHint}  D Density  W 3D-Circuit  F5 QASM  F12 Screenshot  |  1-7 Presets";
        }

        private void BuildSpheres()
        {
            if (_spheres != null)
                foreach (var s in _spheres) if (s != null) Destroy(s.gameObject);

            int n = _runner.QubitCount;
            _spheres = new BlochSphereRenderer[n];
            float totalWidth = (n - 1) * config.sphereSpacing;

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"BlochSphere_q{i}");
                go.transform.position = new Vector3(-totalWidth / 2f + i * config.sphereSpacing, 0, 0);

                // Add a collider so sphere-click raycasts work
                var col = go.AddComponent<SphereCollider>();
                col.radius = config.sphereRadius;

                var renderer = go.AddComponent<BlochSphereRenderer>();
                renderer.Initialise(config.sphereRadius, config.wireframeSegments, config.animationSpeed);
                renderer.SetStateImmediate(Vector3.up);
                _spheres[i] = renderer;
            }

            // Re-init entanglement with new spheres
            _entanglement?.Initialise(_spheres);

            // Clear inspector on circuit change
            _inspector?.ClearInspector();
        }

        private void BuildHistogram()
        {
            if (_histogram != null) Destroy(_histogram.gameObject);
            var go = new GameObject("Histogram");
            _histogram = go.AddComponent<MeasurementHistogram>();

            // Move histogram left when inspector is visible
            _histogram.Initialise(_canvas.GetComponent<RectTransform>(), _runner.QubitCount);
        }

        private void BuildDiagram()
        {
            if (_diagram != null) Destroy(_diagram.gameObject);
            var go = new GameObject("Diagram");
            _diagram = go.AddComponent<CircuitDiagramRenderer>();
            _diagram.Initialise(_canvas.GetComponent<RectTransform>());
        }

        private void UpdateInfo(string title)
        {
            if (_infoText != null)
                _infoText.text = $"Quantum Circuit Visualizer\n{title}";
        }

        private void UpdateStepInfo(string stepLabel)
        {
            if (_infoText != null)
            {
                string[] lines = _infoText.text.Split('\n');
                string title = lines.Length > 1 ? lines[1] : "";
                _infoText.text = $"Quantum Circuit Visualizer — {stepLabel}\n{title}";
            }
        }
    }
}
