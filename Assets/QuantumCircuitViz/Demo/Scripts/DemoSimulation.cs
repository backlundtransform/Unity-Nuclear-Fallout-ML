using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using QuantumCircuitViz.Core;
using QuantumCircuitViz.Visualization;

using CSharpNumerics.Engines.Quantum;
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
    ///   1-4 — load preset circuit (Bell, GHZ, Teleportation, Random Walk)
    ///   R — reset to |0⟩
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

        // Visualisation
        private BlochSphereRenderer[] _spheres;
        private MeasurementHistogram _histogram;
        private CircuitDiagramRenderer _diagram;
        private Canvas _canvas;
        private Text _infoText;
        private Text _controlsText;

        private void Start()
        {
            EnsureCamera();
            CreateCanvas();
            LoadPreset_BellState();
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

        private void HandleInput()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.RightArrow)) StepForward();
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) StepBackward();
            else if (Input.GetKeyDown(KeyCode.Space)) _playing = !_playing;
            else if (Input.GetKeyDown(KeyCode.R)) ResetCircuit();
            else if (Input.GetKeyDown(KeyCode.Alpha1)) LoadPreset_BellState();
            else if (Input.GetKeyDown(KeyCode.Alpha2)) LoadPreset_GHZ();
            else if (Input.GetKeyDown(KeyCode.Alpha3)) LoadPreset_SuperpositionChain();
            else if (Input.GetKeyDown(KeyCode.Alpha4)) LoadPreset_PhaseKickback();
#elif HAS_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.rightArrowKey.wasPressedThisFrame) StepForward();
            else if (kb.leftArrowKey.wasPressedThisFrame) StepBackward();
            else if (kb.spaceKey.wasPressedThisFrame) _playing = !_playing;
            else if (kb.rKey.wasPressedThisFrame) ResetCircuit();
            else if (kb.digit1Key.wasPressedThisFrame) LoadPreset_BellState();
            else if (kb.digit2Key.wasPressedThisFrame) LoadPreset_GHZ();
            else if (kb.digit3Key.wasPressedThisFrame) LoadPreset_SuperpositionChain();
            else if (kb.digit4Key.wasPressedThisFrame) LoadPreset_PhaseKickback();
#endif
        }

        // ── Preset Circuits ──────────────────────────────────────────

        private void LoadPreset_BellState()
        {
            var runner = new CircuitRunner(2);
            runner.Add(new HadamardGate(), 0);
            runner.Add(new CNOTGate(), 0, 1);
            ApplyCircuit(runner, "Bell State  (H → CNOT)");
        }

        private void LoadPreset_GHZ()
        {
            var runner = new CircuitRunner(3);
            runner.Add(new HadamardGate(), 0);
            runner.Add(new CNOTGate(), 0, 1);
            runner.Add(new CNOTGate(), 0, 2);
            ApplyCircuit(runner, "GHZ State  (H → CNOT → CNOT)");
        }

        private void LoadPreset_SuperpositionChain()
        {
            int n = Mathf.Clamp(config.qubitCount, 1, 5);
            var runner = new CircuitRunner(n);
            for (int i = 0; i < n; i++)
                runner.Add(new HadamardGate(), i);
            ApplyCircuit(runner, $"Superposition Chain  ({n}× Hadamard)");
        }

        private void LoadPreset_PhaseKickback()
        {
            var runner = new CircuitRunner(2);
            runner.Add(new HadamardGate(), 0);
            runner.Add(new PauliXGate(), 1);
            runner.Add(new HadamardGate(), 1);
            runner.Add(new CNOTGate(), 0, 1);
            runner.Add(new HadamardGate(), 0);
            ApplyCircuit(runner, "Phase Kickback  (Deutsch-like)");
        }

        // ── Circuit Management ───────────────────────────────────────

        private void ApplyCircuit(CircuitRunner runner, string title)
        {
            _runner = runner;
            _currentStep = -1;
            _playTimer = 0f;

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
                _playing = false; // stop at end
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
                // Initial |0…0⟩
                var amps = new CSharpNumerics.Numerics.Objects.ComplexVectorN(1 << _runner.QubitCount);
                amps[0] = new CSharpNumerics.Numerics.Objects.ComplexNumber(1, 0);
                state = new QuantumState(amps);
            }
            else
            {
                state = _runner.RunUpTo(_currentStep);
            }

            // Update Bloch spheres (partial trace for multi-qubit — use per-qubit reduced state)
            UpdateBlochSpheres(state);

            // Update histogram
            var probs = state.GetProbabilities();
            _histogram?.UpdateProbabilities(probs);

            // Update circuit diagram
            _diagram?.Render(_runner.Steps, _runner.QubitCount, _currentStep);

            // Update info
            string stepLabel = _currentStep < 0 ? "Init |0…0⟩" :
                $"Step {_currentStep + 1}/{_runner.Steps.Count}: {_runner.Steps[_currentStep].Gate.GetType().Name.Replace("Gate", "")}";
            UpdateStepInfo(stepLabel);
        }

        private void UpdateBlochSpheres(QuantumState state)
        {
            if (_spheres == null) return;

            if (state.QubitCount == 1)
            {
                var bloch = state.GetBlochVector();
                // CSharpNumerics: X,Y,Z → Unity: X,Y,Z (same convention)
                _spheres[0].SetState(new Vector3((float)bloch.X, (float)bloch.Z, (float)bloch.Y));
            }
            else
            {
                // For multi-qubit: compute single-qubit reduced density matrix per qubit
                for (int q = 0; q < state.QubitCount && q < _spheres.Length; q++)
                {
                    var bloch = ComputeReducedBlochVector(state, q);
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

            // Build 2×2 reduced density matrix for this qubit
            // ρ[a,b] = Σ_{other basis states} ψ[..a..] · ψ*[..b..]
            double rho00_r = 0, rho01_r = 0, rho01_i = 0, rho11_r = 0;

            for (int i = 0; i < dim; i++)
            {
                int qBit_i = (i >> qubit) & 1;
                for (int j = 0; j < dim; j++)
                {
                    int qBit_j = (j >> qubit) & 1;

                    // Check that all OTHER qubits match between i and j
                    int mask = ~(1 << qubit) & ((1 << n) - 1);
                    if ((i & mask) != (j & mask)) continue;

                    var ai = amps[i];
                    var aj = amps[j];
                    // ψ_i · ψ_j*
                    double re = ai.realPart * aj.realPart + ai.imaginaryPart * aj.imaginaryPart;
                    double im = ai.imaginaryPart * aj.realPart - ai.realPart * aj.imaginaryPart;

                    if (qBit_i == 0 && qBit_j == 0) rho00_r += re;
                    else if (qBit_i == 0 && qBit_j == 1) { rho01_r += re; rho01_i += im; }
                    else if (qBit_i == 1 && qBit_j == 1) rho11_r += re;
                }
            }

            // Bloch: x = 2·Re(ρ01), y = 2·Im(ρ01), z = ρ00 − ρ11
            float bx = (float)(2.0 * rho01_r);
            float by = (float)(2.0 * rho01_i);
            float bz = (float)(rho00_r - rho11_r);

            // Map to Unity coords: X→right, Z→up (Bloch Z = up), Y→forward
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

            // Controls hint (bottom-left)
            var ctrlGo = new GameObject("ControlsText");
            var ctrlRt = ctrlGo.AddComponent<RectTransform>();
            ctrlRt.SetParent(_canvas.transform, false);
            ctrlRt.anchorMin = new Vector2(0.02f, 0.42f);
            ctrlRt.anchorMax = new Vector2(0.55f, 0.58f);
            ctrlRt.offsetMin = Vector2.zero;
            ctrlRt.offsetMax = Vector2.zero;
            _controlsText = ctrlGo.AddComponent<Text>();
            _controlsText.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            _controlsText.fontSize = 12;
            _controlsText.color = new Color(0.5f, 0.6f, 0.7f);
            _controlsText.text =
                "←/→ Step gates   Space Play/Pause   R Reset\n" +
                "1 Bell   2 GHZ   3 Superposition   4 Phase Kickback";
        }

        private void BuildSpheres()
        {
            // Destroy old spheres
            if (_spheres != null)
                foreach (var s in _spheres) if (s != null) Destroy(s.gameObject);

            int n = _runner.QubitCount;
            _spheres = new BlochSphereRenderer[n];
            float totalWidth = (n - 1) * config.sphereSpacing;

            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"BlochSphere_q{i}");
                go.transform.position = new Vector3(-totalWidth / 2f + i * config.sphereSpacing, 0, 0);
                var renderer = go.AddComponent<BlochSphereRenderer>();
                renderer.Initialise(config.sphereRadius, config.wireframeSegments, config.animationSpeed);
                renderer.SetStateImmediate(Vector3.up); // |0⟩ = north pole
                _spheres[i] = renderer;
            }
        }

        private void BuildHistogram()
        {
            if (_histogram != null) Destroy(_histogram.gameObject);
            var go = new GameObject("Histogram");
            _histogram = go.AddComponent<MeasurementHistogram>();
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
