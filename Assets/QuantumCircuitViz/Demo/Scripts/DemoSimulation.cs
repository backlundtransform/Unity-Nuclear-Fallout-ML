using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using QuantumCircuitViz.Core;
using QuantumCircuitViz.Visualization;
using QuantumCircuitViz.UI;
using QuantumCircuitViz.Export;

using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Engines.Quantum.Algorithms;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Demo
{
    /// <summary>
    /// Self-contained demo: three-view quantum circuit visualiser.
    /// Attach to any empty GameObject → enter Play Mode → done.
    ///
    /// Views:
    ///   A. Circuit View  — build and step through gate-by-gate.
    ///   B. State View    — probability bars with phase, Argand diagram.
    ///   C. Bloch View    — single-qubit Bloch sphere, entanglement-aware.
    ///
    /// Controls:
    ///   Tab / Shift-Tab  — cycle view
    ///   ←/→              — step backward / forward
    ///   Space             — play / pause auto-advance
    ///   R                 — reset to |0…0⟩
    ///   N                 — toggle noise
    ///   F5                — copy QASM to clipboard
    ///   F12               — screenshot
    ///   1-7               — load preset circuits
    /// </summary>
    public class DemoSimulation : MonoBehaviour
    {
        [Header("Simulation")]
        public SimulationConfig config = new SimulationConfig();

        [Header("Demo")]
        [Tooltip("Auto-step through gates on start")]
        public bool autoPlay = true;

        // ── Core state ───────────────────────────────────────────
        private CircuitRunner _runner;
        private int _currentStep = -1;
        private bool _playing;
        private float _playTimer;
        private string _currentTitle = "";

        // ── Views ────────────────────────────────────────────────
        private enum ViewMode { Circuit, State, Bloch }
        private ViewMode _currentView = ViewMode.Circuit;

        private Canvas _canvas;
        private RectTransform _viewContent;
        private ViewTabBar _tabBar;
        private CircuitGridView _circuitView;
        private StateView _stateView;
        private BlochSphereView _blochView;

        // ── HUD ──────────────────────────────────────────────────
        private Text _infoText;
        private Text _statusBarText;

        // ──────────────────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────────────────
        private void Start()
        {
            EnsureCamera();
            EnsureEventSystem();
            CreateCanvas();
            BuildViews();
            LoadPreset_BellState();
            SwitchView(ViewMode.Circuit);
            _playing = autoPlay;
        }

        private void Update()
        {
            HandleInput();

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

        // ──────────────────────────────────────────────────────────
        // Input
        // ──────────────────────────────────────────────────────────
        private void HandleInput()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Tab)) CycleView(Input.GetKey(KeyCode.LeftShift) ? -1 : 1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) StepForward();
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) StepBackward();
            else if (Input.GetKeyDown(KeyCode.Space)) TogglePlay();
            else if (Input.GetKeyDown(KeyCode.R)) ResetCircuit();
            else if (Input.GetKeyDown(KeyCode.N)) ToggleNoise();
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
            if (kb.tabKey.wasPressedThisFrame) CycleView(kb.leftShiftKey.isPressed ? -1 : 1);
            else if (kb.rightArrowKey.wasPressedThisFrame) StepForward();
            else if (kb.leftArrowKey.wasPressedThisFrame) StepBackward();
            else if (kb.spaceKey.wasPressedThisFrame) TogglePlay();
            else if (kb.rKey.wasPressedThisFrame) ResetCircuit();
            else if (kb.nKey.wasPressedThisFrame) ToggleNoise();
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

        // ──────────────────────────────────────────────────────────
        // View switching
        // ──────────────────────────────────────────────────────────
        private void CycleView(int direction)
        {
            int count = System.Enum.GetValues(typeof(ViewMode)).Length;
            int next = (((int)_currentView + direction) % count + count) % count;
            _tabBar.Select(next);
        }

        private void SwitchView(ViewMode view)
        {
            _currentView = view;

            _circuitView.Hide();
            _stateView.Hide();
            _blochView.Hide();

            switch (view)
            {
                case ViewMode.Circuit:
                    _circuitView.Show();
                    break;
                case ViewMode.State:
                    _stateView.Show();
                    break;
                case ViewMode.Bloch:
                    _blochView.Show();
                    break;
            }

            UpdateStatusBar();
            if (_runner != null)
                PropagateState();

            Debug.Log($"[QuantumViz] View → {view}");
        }

        // ──────────────────────────────────────────────────────────
        // Preset Circuits
        // ──────────────────────────────────────────────────────────
        private void LoadPreset_BellState()
        {
            var c = QuantumCircuitBuilder.New(2).H(0).CNOT(0, 1).Build();
            ApplyCircuit(CircuitRunner.FromBuilder(c), "Bell State  (H → CNOT)");
        }

        private void LoadPreset_GHZ()
        {
            var c = QuantumCircuitBuilder.New(3).H(0).CNOT(0, 1).CNOT(0, 2).Build();
            ApplyCircuit(CircuitRunner.FromBuilder(c), "GHZ State  (H → CNOT → CNOT)");
        }

        private void LoadPreset_SuperpositionChain()
        {
            int n = Mathf.Clamp(config.qubitCount, 1, 5);
            var b = QuantumCircuitBuilder.New(n);
            for (int i = 0; i < n; i++) b.H(i);
            ApplyCircuit(CircuitRunner.FromBuilder(b.Build()), $"Superposition Chain  ({n}× Hadamard)");
        }

        private void LoadPreset_PhaseKickback()
        {
            var c = QuantumCircuitBuilder.New(2)
                .H(0).X(1).H(1).CNOT(0, 1).H(0).Build();
            ApplyCircuit(CircuitRunner.FromBuilder(c), "Phase Kickback  (Deutsch-like)");
        }

        private void LoadPreset_Grover()
        {
            var c = GroverSearch.CreateCircuit(3, new[] { 0, 1, 2 }, new[] { 5 });
            ApplyCircuit(CircuitRunner.FromBuilder(c), "Grover Search  (target |101⟩)");
        }

        private void LoadPreset_Toffoli()
        {
            var c = QuantumCircuitBuilder.New(3).X(0).X(1).Toffoli(0, 1, 2).Build();
            ApplyCircuit(CircuitRunner.FromBuilder(c), "Toffoli  (X X → CCX)");
        }

        private void LoadPreset_QFT()
        {
            var b = QuantumCircuitBuilder.New(3).X(0);
            b.ApplyQFT(0, 1, 2);
            ApplyCircuit(CircuitRunner.FromBuilder(b.Build()), "Quantum Fourier Transform  (3 qubits)");
        }

        // ──────────────────────────────────────────────────────────
        // Circuit management
        // ──────────────────────────────────────────────────────────
        private void ApplyCircuit(CircuitRunner runner, string title)
        {
            _runner = runner;
            _currentTitle = title;
            _currentStep = -1;
            _playTimer = 0f;

            if (config.enableNoise)
                _runner.WithNoise(config.depolarizingRate, config.dephasingRate, config.amplitudeDampingGamma);

            // Rebuild views with new qubit count
            _circuitView.SetCircuit(_runner.Steps, _runner.QubitCount);
            _stateView.Rebuild(_runner.QubitCount);
            _blochView.Rebuild(_runner.QubitCount, config.sphereRadius, config.wireframeSegments, config.animationSpeed);

            SwitchView(_currentView);
            PropagateState();
            UpdateInfo(title);

            Debug.Log($"[QuantumViz] Loaded: {title}  ({_runner.QubitCount}q, {_runner.Steps.Count} gates)");
        }

        /// <summary>Called when circle view's interactive builder modifies the circuit.</summary>
        private void OnCircuitChangedByBuilder(List<GateStep> steps, int qubitCount)
        {
            // Rebuild runner from step list
            var circuit = new QuantumCircuit(qubitCount);
            foreach (var step in steps)
                circuit.AddInstruction(new QuantumInstruction(step.Gate, step.QubitIndices));

            _runner = CircuitRunner.FromBuilder(circuit);
            _currentTitle = "Custom Circuit";
            _currentStep = _runner.Steps.Count - 1;

            if (config.enableNoise)
                _runner.WithNoise(config.depolarizingRate, config.dephasingRate, config.amplitudeDampingGamma);

            _stateView.Rebuild(qubitCount);
            _blochView.Rebuild(qubitCount, config.sphereRadius, config.wireframeSegments, config.animationSpeed);

            PropagateState();
            UpdateInfo("Custom Circuit");
        }

        private void ResetCircuit()
        {
            _currentStep = -1;
            _playTimer = 0f;
            _playing = false;
            _circuitView.SetStep(-1);
            _circuitView.SetPlaying(false);
            PropagateState();
        }

        private void StepForward()
        {
            if (_runner == null) return;
            if (_currentStep < _runner.Steps.Count - 1)
            {
                _currentStep++;
                _circuitView.SetStep(_currentStep);
                PropagateState();
            }
            else
            {
                _playing = false;
                _circuitView.SetPlaying(false);
            }
        }

        private void StepBackward()
        {
            if (_runner == null || _currentStep < 0) return;
            _currentStep--;
            _circuitView.SetStep(_currentStep);
            PropagateState();
        }

        private void TogglePlay()
        {
            _playing = !_playing;
            _circuitView.SetPlaying(_playing);
        }

        private void ToggleNoise()
        {
            config.enableNoise = !config.enableNoise;
            Debug.Log($"[QuantumViz] Noise: {(config.enableNoise ? "ON" : "OFF")}");
            if (_runner != null)
            {
                if (config.enableNoise)
                    _runner.WithNoise(config.depolarizingRate, config.dephasingRate, config.amplitudeDampingGamma);
                else
                    _runner.WithNoise(0, 0, 0);
                PropagateState();
            }
            UpdateStatusBar();
        }

        // ──────────────────────────────────────────────────────────
        // State propagation
        // ──────────────────────────────────────────────────────────
        private void PropagateState()
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

            // Update Circuit View step + gate diagram
            _circuitView.SetStep(_currentStep);
            _circuitView.RenderGates();

            // Update State View
            _stateView.UpdateState(state);

            // Update Bloch View
            _blochView.UpdateState(state);

            // Update info bar
            string noise = config.enableNoise ? " [NOISY]" : "";
            string stepLabel = _currentStep < 0 ? $"Init |0…0⟩{noise}" :
                $"Step {_currentStep + 1}/{_runner.Steps.Count}: " +
                $"{_runner.Steps[_currentStep].Gate.GetType().Name.Replace("Gate", "")}{noise}";
            UpdateStepInfo(stepLabel);
        }

        // ──────────────────────────────────────────────────────────
        // Export
        // ──────────────────────────────────────────────────────────
        private void ExportQASMToClipboard()
        {
            if (_runner == null) return;
            string qasm = CircuitExporter.ToQASM(_runner);
            GUIUtility.systemCopyBuffer = qasm;
            Debug.Log("[QuantumViz] QASM copied to clipboard:\n" + qasm);
        }

        private void TakeScreenshot()
        {
            string path = ScreenshotUtility.Capture(2);
            Debug.Log($"[QuantumViz] Screenshot saved to: {path}");
        }

        // ──────────────────────────────────────────────────────────
        // Scene setup
        // ──────────────────────────────────────────────────────────
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
            cam.transform.position = new Vector3(0, 1.5f, -5f);
            cam.transform.LookAt(new Vector3(0, 0.5f, 0));
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

        // ──────────────────────────────────────────────────────────
        // Canvas + Views
        // ──────────────────────────────────────────────────────────
        private void CreateCanvas()
        {
            var canvasGo = new GameObject("QuantumCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // View content area (between tab bar and status bar)
            var contentGo = new GameObject("ViewContent");
            _viewContent = contentGo.AddComponent<RectTransform>();
            _viewContent.SetParent(_canvas.transform, false);
            _viewContent.anchorMin = new Vector2(0f, 0.04f);
            _viewContent.anchorMax = new Vector2(1f, 0.96f);
            _viewContent.offsetMin = _viewContent.offsetMax = Vector2.zero;

            // Info bar (top-left, overlaid on tab area)
            var infoGo = new GameObject("InfoText");
            var infoRt = infoGo.AddComponent<RectTransform>();
            infoRt.SetParent(_canvas.transform, false);
            infoRt.anchorMin = new Vector2(0.01f, 0.96f);
            infoRt.anchorMax = new Vector2(0.28f, 1f);
            infoRt.offsetMin = infoRt.offsetMax = Vector2.zero;
            _infoText = infoGo.AddComponent<Text>();
            _infoText.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            _infoText.fontSize = 11;
            _infoText.color = new Color(0f, 0.85f, 1f);
            _infoText.alignment = TextAnchor.MiddleLeft;

            // Status bar (bottom)
            var statusGo = new GameObject("StatusBar");
            var statusRt = statusGo.AddComponent<RectTransform>();
            statusRt.SetParent(_canvas.transform, false);
            statusRt.anchorMin = new Vector2(0f, 0f);
            statusRt.anchorMax = new Vector2(1f, 0.038f);
            statusRt.offsetMin = statusRt.offsetMax = Vector2.zero;
            statusGo.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.95f);

            var sbTextGo = new GameObject("StatusText");
            var sbTextRt = sbTextGo.AddComponent<RectTransform>();
            sbTextRt.SetParent(statusRt, false);
            sbTextRt.anchorMin = Vector2.zero;
            sbTextRt.anchorMax = Vector2.one;
            sbTextRt.offsetMin = sbTextRt.offsetMax = Vector2.zero;
            _statusBarText = sbTextGo.AddComponent<Text>();
            _statusBarText.font = Font.CreateDynamicFontFromOSFont("Consolas", 10);
            _statusBarText.fontSize = 10;
            _statusBarText.color = new Color(0.35f, 0.45f, 0.55f);
            _statusBarText.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildViews()
        {
            var canvasRt = _canvas.GetComponent<RectTransform>();

            // Tab bar
            var tabGo = new GameObject("TabBar", typeof(RectTransform));
            _tabBar = tabGo.AddComponent<ViewTabBar>();
            _tabBar.Initialise(canvasRt, new[] { "A. Circuit", "B. State", "C. Bloch" });
            _tabBar.OnTabSelected += (idx) => SwitchView((ViewMode)idx);

            // A. Circuit View
            var cvGo = new GameObject("CircuitView", typeof(RectTransform));
            _circuitView = cvGo.AddComponent<CircuitGridView>();
            _circuitView.Initialise(_viewContent, 2);
            _circuitView.OnCircuitChanged += OnCircuitChangedByBuilder;
            _circuitView.OnStepChanged += (dir) =>
            {
                if (dir > 0) StepForward(); else StepBackward();
            };
            _circuitView.OnPlayToggle += TogglePlay;
            _circuitView.OnReset += ResetCircuit;

            // B. State View
            var svGo = new GameObject("StateView", typeof(RectTransform));
            _stateView = svGo.AddComponent<StateView>();
            _stateView.Initialise(_viewContent, 2);

            // C. Bloch View
            var bvGo = new GameObject("BlochView", typeof(RectTransform));
            _blochView = bvGo.AddComponent<BlochSphereView>();
            _blochView.Initialise(canvasRt, 2, config.sphereRadius, config.wireframeSegments, config.animationSpeed);
        }

        // ──────────────────────────────────────────────────────────
        // HUD updates
        // ──────────────────────────────────────────────────────────
        private void UpdateInfo(string title)
        {
            if (_infoText != null)
                _infoText.text = title;
        }

        private void UpdateStepInfo(string stepLabel)
        {
            if (_infoText != null)
                _infoText.text = $"{_currentTitle} — {stepLabel}";
        }

        private void UpdateStatusBar()
        {
            if (_statusBarText == null) return;
            string noise = config.enableNoise ? " [NOISY]" : "";
            _statusBarText.text =
                $"Tab View  ←/→ Step  Space Play  R Reset  N Noise{noise}  |  F5 QASM  F12 Shot  |  1-7 Presets";
        }
    }
}
