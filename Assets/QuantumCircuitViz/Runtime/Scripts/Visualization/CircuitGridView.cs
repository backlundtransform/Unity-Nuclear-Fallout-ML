using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using QuantumCircuitViz.Core;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Interactive quantum circuit view:
    ///  • Gate palette for placing gates
    ///  • Visual qubit wires with gate blocks
    ///  • Step cursor / highlight
    ///  • Play / Pause / Step / Reset controls
    ///  • Hover tooltip with gate description
    ///  • Click gate → show matrix / details
    /// </summary>
    public class CircuitGridView : MonoBehaviour
    {
        // ── Events ───────────────────────────────────────────────
        public event Action<List<GateStep>, int> OnCircuitChanged;
        public event Action<int> OnStepChanged;
        public event Action OnPlayToggle;
        public event Action OnReset;

        // ── Layout panels ────────────────────────────────────────
        private RectTransform _container;
        private RectTransform _palettePanel;
        private RectTransform _gridPanel;
        private RectTransform _controlPanel;

        // ── Tooltip ──────────────────────────────────────────────
        private RectTransform _tooltipPanel;
        private Text _tooltipText;

        // ── Status / placement mode ──────────────────────────────
        private Text _statusText;
        private GateInfo _selectedGate;
        private int _placementControlQubit = -1;

        // ── Circuit data ─────────────────────────────────────────
        private List<GateStep> _steps = new List<GateStep>();
        private int _qubitCount = 2;
        private int _currentStep = -1;
        private bool _isPlaying;

        // ── Grid rendering ───────────────────────────────────────
        private readonly List<GameObject> _renderedBlocks = new List<GameObject>();
        private Image[] _wireImages;
        private Button[] _wireButtons;
        private Image _stepCursor;

        // ── Transport buttons ────────────────────────────────────
        private Button _btnStepBack, _btnPlayPause, _btnStepFwd, _btnReset, _btnClear;
        private Text _playPauseText;
        private Text _stepCounterText;

        // ── Palette buttons ──────────────────────────────────────
        private readonly Dictionary<GateInfo, Image> _paletteBtnImages = new Dictionary<GateInfo, Image>();

        // ── Constants ────────────────────────────────────────────
        private const float CellWidth = 60f;
        private const float WireHeight = 36f;
        private static readonly Color WireColor = new Color(0.25f, 0.35f, 0.45f, 0.8f);
        private static readonly Color CursorColor = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color GridBg = new Color(0.04f, 0.04f, 0.10f, 0.92f);

        // ── Public properties ────────────────────────────────────
        public int CurrentStep => _currentStep;
        public int StepCount => _steps.Count;
        public bool IsPlaying => _isPlaying;

        // ──────────────────────────────────────────────────────────
        // Initialise
        // ──────────────────────────────────────────────────────────
        public void Initialise(RectTransform parent, int qubitCount)
        {
            _qubitCount = qubitCount;
            gameObject.name = "CircuitView";

            _container = GetComponent<RectTransform>();
            if (_container == null) _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = Vector2.zero;
            _container.anchorMax = Vector2.one;
            _container.offsetMin = _container.offsetMax = Vector2.zero;

            BuildPalette();
            BuildGrid();
            BuildControls();
            BuildTooltip();
        }

        // ──────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────
        public void SetCircuit(IReadOnlyList<GateStep> steps, int qubitCount)
        {
            _qubitCount = qubitCount;
            _steps = new List<GateStep>(steps);
            _currentStep = -1;
            CancelPlacement();
            RebuildGrid();
            RenderGates();
            UpdateStepCounter();
        }

        public void SetStep(int step)
        {
            _currentStep = Mathf.Clamp(step, -1, _steps.Count - 1);
            UpdateCursor();
            UpdateStepCounter();
        }

        public void SetPlaying(bool playing)
        {
            _isPlaying = playing;
            if (_playPauseText != null)
                _playPauseText.text = _isPlaying ? "❚❚" : "►";
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        // ──────────────────────────────────────────────────────────
        // Palette
        // ──────────────────────────────────────────────────────────
        private void BuildPalette()
        {
            var go = UIGo("Palette");
            _palettePanel = go.GetComponent<RectTransform>();
            _palettePanel.SetParent(_container, false);
            _palettePanel.anchorMin = new Vector2(0f, 0.88f);
            _palettePanel.anchorMax = Vector2.one;
            _palettePanel.offsetMin = _palettePanel.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);

            // Title
            CreateText(_palettePanel, "PaletteTitle", "Gate Palette",
                new Vector2(0f, 0.55f), new Vector2(0.12f, 1f), 11,
                new Color(0.4f, 0.5f, 0.6f), TextAnchor.MiddleCenter);

            // Single-qubit row
            float x = 0.12f;
            foreach (var gate in GateLibrary.SingleQubitGates)
            {
                float w = 0.055f;
                CreatePaletteButton(gate, new Vector2(x, 0.52f), new Vector2(x + w, 0.98f));
                x += w + 0.005f;
            }

            // Multi-qubit row
            x = 0.12f;
            foreach (var gate in GateLibrary.MultiQubitGates)
            {
                float w = 0.07f;
                CreatePaletteButton(gate, new Vector2(x, 0.02f), new Vector2(x + w, 0.48f));
                x += w + 0.005f;
            }

            // Cancel button
            CreatePaletteActionButton("✕", new Vector2(0.92f, 0.02f), new Vector2(0.99f, 0.98f),
                new Color(0.6f, 0.2f, 0.2f), CancelPlacement);
        }

        private void CreatePaletteButton(GateInfo gate, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = UIGo($"Pal_{gate.Symbol}");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_palettePanel, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(1, 1);
            rt.offsetMax = new Vector2(-1, -1);

            var img = go.AddComponent<Image>();
            img.color = gate.Color * 0.7f;
            _paletteBtnImages[gate] = img;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => OnPaletteClick(gate));

            var txt = CreateChildText(rt, gate.Symbol, 11, Color.white, TextAnchor.MiddleCenter);

            // Hover tooltip
            var trigger = go.AddComponent<EventTrigger>();
            AddHoverEvents(trigger, gate.DisplayName + "\n" + gate.Description);
        }

        private void CreatePaletteActionButton(string label, Vector2 anchorMin, Vector2 anchorMax,
            Color color, UnityEngine.Events.UnityAction action)
        {
            var go = UIGo($"PalAction_{label}");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_palettePanel, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(1, 1);
            rt.offsetMax = new Vector2(-1, -1);

            go.AddComponent<Image>().color = color;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(action);
            CreateChildText(rt, label, 14, Color.white, TextAnchor.MiddleCenter);
        }

        private void OnPaletteClick(GateInfo gate)
        {
            // Deselect any previous
            CancelPlacement();

            _selectedGate = gate;
            _placementControlQubit = -1;

            // Highlight the selected palette button
            if (_paletteBtnImages.TryGetValue(gate, out var img))
                img.color = gate.Color;

            if (gate.QubitCount == 1)
                SetStatus($"Click a qubit wire to place {gate.DisplayName}");
            else if (gate.QubitCount == 2)
                SetStatus($"Click CONTROL qubit for {gate.DisplayName}");
            else
                SetStatus($"Click first CONTROL qubit for {gate.DisplayName}");
        }

        private void CancelPlacement()
        {
            if (_selectedGate != null && _paletteBtnImages.TryGetValue(_selectedGate, out var img))
                img.color = _selectedGate.Color * 0.7f;
            _selectedGate = null;
            _placementControlQubit = -1;
            SetStatus("");
        }

        // ──────────────────────────────────────────────────────────
        // Grid
        // ──────────────────────────────────────────────────────────
        private void BuildGrid()
        {
            var go = UIGo("Grid");
            _gridPanel = go.GetComponent<RectTransform>();
            _gridPanel.SetParent(_container, false);
            _gridPanel.anchorMin = new Vector2(0f, 0.15f);
            _gridPanel.anchorMax = new Vector2(1f, 0.87f);
            _gridPanel.offsetMin = _gridPanel.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = GridBg;

            BuildWires();
            BuildStepCursor();
        }

        private void RebuildGrid()
        {
            // Destroy old wires and rebuild
            if (_wireImages != null)
                foreach (var w in _wireImages)
                    if (w != null) Destroy(w.gameObject);
            BuildWires();
            BuildStepCursor();
        }

        private void BuildWires()
        {
            _wireImages = new Image[_qubitCount];
            _wireButtons = new Button[_qubitCount];

            float rowH = 1f / Mathf.Max(_qubitCount, 1);

            for (int q = 0; q < _qubitCount; q++)
            {
                float yMid = 1f - (q + 0.5f) * rowH;

                // Qubit label
                CreateText(_gridPanel, $"QLabel_{q}", $"q{q}",
                    new Vector2(0f, yMid - rowH * 0.4f), new Vector2(0.05f, yMid + rowH * 0.4f),
                    12, new Color(0.5f, 0.65f, 0.8f), TextAnchor.MiddleCenter);

                // Wire line
                var wireGo = UIGo($"Wire_{q}");
                var wireRt = wireGo.GetComponent<RectTransform>();
                wireRt.SetParent(_gridPanel, false);
                wireRt.anchorMin = new Vector2(0.06f, yMid - 0.003f);
                wireRt.anchorMax = new Vector2(0.98f, yMid + 0.003f);
                wireRt.offsetMin = wireRt.offsetMax = Vector2.zero;
                _wireImages[q] = wireGo.AddComponent<Image>();
                _wireImages[q].color = WireColor;

                // Clickable wire area (invisible, larger hit area)
                var hitGo = UIGo($"WireHit_{q}");
                var hitRt = hitGo.GetComponent<RectTransform>();
                hitRt.SetParent(_gridPanel, false);
                hitRt.anchorMin = new Vector2(0.06f, yMid - rowH * 0.4f);
                hitRt.anchorMax = new Vector2(0.98f, yMid + rowH * 0.4f);
                hitRt.offsetMin = hitRt.offsetMax = Vector2.zero;

                var hitImg = hitGo.AddComponent<Image>();
                hitImg.color = Color.clear;
                hitImg.raycastTarget = true;

                _wireButtons[q] = hitGo.AddComponent<Button>();
                int qubitIdx = q;
                _wireButtons[q].onClick.AddListener(() => OnWireClick(qubitIdx));
            }
        }

        private void BuildStepCursor()
        {
            if (_stepCursor != null) Destroy(_stepCursor.gameObject);

            var go = UIGo("StepCursor");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_gridPanel, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0, 0);
            _stepCursor = go.AddComponent<Image>();
            _stepCursor.color = CursorColor;
            _stepCursor.raycastTarget = false;
            _stepCursor.gameObject.SetActive(false);
        }

        private void OnWireClick(int qubit)
        {
            if (_selectedGate == null) return;

            if (_selectedGate.QubitCount == 1)
            {
                // Place single-qubit gate
                var gate = _selectedGate.Create();
                _steps.Add(new GateStep(gate, new List<int> { qubit }));
                CancelPlacement();
                RenderGates();
                NotifyCircuitChanged();
            }
            else if (_selectedGate.QubitCount == 2)
            {
                if (_placementControlQubit < 0)
                {
                    _placementControlQubit = qubit;
                    SetStatus($"Click TARGET qubit for {_selectedGate.DisplayName}  (control=q{qubit})");
                }
                else
                {
                    if (qubit == _placementControlQubit) return; // same qubit, ignore
                    var gate = _selectedGate.Create();
                    _steps.Add(new GateStep(gate, new List<int> { _placementControlQubit, qubit }));
                    CancelPlacement();
                    RenderGates();
                    NotifyCircuitChanged();
                }
            }
            else if (_selectedGate.QubitCount == 3)
            {
                // Toffoli/Fredkin: need 2 controls + 1 target
                if (_placementControlQubit < 0)
                {
                    _placementControlQubit = qubit;
                    SetStatus($"Click second CONTROL qubit  (first=q{qubit})");
                }
                else if (_placementControlQubit >= 0 && _placementControlQubit < 100)
                {
                    // Encode both controls: use _placementControlQubit for first, store second
                    int c1 = _placementControlQubit;
                    if (qubit == c1) return;
                    _placementControlQubit = c1 * 100 + qubit + 1; // encode
                    SetStatus($"Click TARGET qubit  (controls=q{c1},q{qubit})");
                }
                else
                {
                    int c1 = _placementControlQubit / 100;
                    int c2 = (_placementControlQubit % 100) - 1;
                    if (qubit == c1 || qubit == c2) return;
                    var gate = _selectedGate.Create();
                    _steps.Add(new GateStep(gate, new List<int> { c1, c2, qubit }));
                    CancelPlacement();
                    RenderGates();
                    NotifyCircuitChanged();
                }
            }
        }

        private void NotifyCircuitChanged()
        {
            OnCircuitChanged?.Invoke(_steps, _qubitCount);
        }

        // ──────────────────────────────────────────────────────────
        // Render gate blocks
        // ──────────────────────────────────────────────────────────
        public void RenderGates()
        {
            // Clear previous
            foreach (var go in _renderedBlocks)
                if (go != null) Destroy(go);
            _renderedBlocks.Clear();

            if (_steps.Count == 0) return;

            float rowH = 1f / Mathf.Max(_qubitCount, 1);
            int totalSteps = _steps.Count;
            float gridStart = 0.08f;
            float gridEnd = 0.96f;
            float stepW = (gridEnd - gridStart) / Mathf.Max(totalSteps, 1);
            float blockW = Mathf.Min(stepW * 0.75f, 0.08f);

            for (int s = 0; s < totalSteps; s++)
            {
                var step = _steps[s];
                float xCenter = gridStart + (s + 0.5f) * stepW;
                var info = GateLibrary.Find(step.Gate);
                Color gateColor = info != null ? info.Color : new Color(0.5f, 0.5f, 0.5f);
                string sym = info != null ? info.Symbol : step.Gate.GetType().Name.Replace("Gate", "");
                string desc = info != null ? $"{info.DisplayName}\n{info.Description}" : sym;

                // For multi-qubit: draw connector line
                if (step.QubitIndices.Count > 1)
                {
                    int minQ = step.QubitIndices[0], maxQ = step.QubitIndices[0];
                    foreach (int qi in step.QubitIndices)
                    {
                        if (qi < minQ) minQ = qi;
                        if (qi > maxQ) maxQ = qi;
                    }

                    float yTop = 1f - (minQ + 0.5f) * rowH;
                    float yBot = 1f - (maxQ + 0.5f) * rowH;
                    var lineGo = UIGo($"Conn_{s}");
                    var lineRt = lineGo.GetComponent<RectTransform>();
                    lineRt.SetParent(_gridPanel, false);
                    lineRt.anchorMin = new Vector2(xCenter - 0.002f, yBot);
                    lineRt.anchorMax = new Vector2(xCenter + 0.002f, yTop);
                    lineRt.offsetMin = lineRt.offsetMax = Vector2.zero;
                    var lineImg = lineGo.AddComponent<Image>();
                    lineImg.color = gateColor * 0.8f;
                    lineImg.raycastTarget = false;
                    _renderedBlocks.Add(lineGo);
                }

                // Gate block on each participating qubit
                for (int qi = 0; qi < step.QubitIndices.Count; qi++)
                {
                    int q = step.QubitIndices[qi];
                    float yMid = 1f - (q + 0.5f) * rowH;

                    bool isControl = step.QubitIndices.Count > 1 && qi < step.QubitIndices.Count - 1
                        && !(step.Gate is SWAPGate) && !(step.Gate is FredkinGate && qi > 0);

                    // For CNOT/CZ control qubits: small dot
                    if (isControl && (step.Gate is CNOTGate || step.Gate is CZGate || step.Gate is ToffoliGate))
                    {
                        var dotGo = UIGo($"Ctrl_{s}_q{q}");
                        var dotRt = dotGo.GetComponent<RectTransform>();
                        dotRt.SetParent(_gridPanel, false);
                        float dotSize = rowH * 0.22f;
                        dotRt.anchorMin = new Vector2(xCenter - dotSize * 0.5f, yMid - dotSize * 0.5f);
                        dotRt.anchorMax = new Vector2(xCenter + dotSize * 0.5f, yMid + dotSize * 0.5f);
                        dotRt.offsetMin = dotRt.offsetMax = Vector2.zero;
                        var dotImg = dotGo.AddComponent<Image>();
                        dotImg.color = gateColor;
                        var trigger = dotGo.AddComponent<EventTrigger>();
                        AddHoverEvents(trigger, desc);
                        _renderedBlocks.Add(dotGo);
                    }
                    else
                    {
                        // Gate box
                        var boxGo = UIGo($"Gate_{s}_q{q}");
                        var boxRt = boxGo.GetComponent<RectTransform>();
                        boxRt.SetParent(_gridPanel, false);
                        float halfW = blockW * 0.5f;
                        float halfH = rowH * 0.35f;
                        boxRt.anchorMin = new Vector2(xCenter - halfW, yMid - halfH);
                        boxRt.anchorMax = new Vector2(xCenter + halfW, yMid + halfH);
                        boxRt.offsetMin = boxRt.offsetMax = Vector2.zero;

                        var boxImg = boxGo.AddComponent<Image>();
                        boxImg.color = gateColor * 0.85f;

                        // Gate symbol text
                        string displaySym = sym;
                        if (step.Gate is CNOTGate && qi == step.QubitIndices.Count - 1)
                            displaySym = "⊕";
                        else if (step.Gate is ToffoliGate && qi == step.QubitIndices.Count - 1)
                            displaySym = "⊕";

                        CreateChildText(boxRt, displaySym, 12, Color.white, TextAnchor.MiddleCenter);

                        // Hover + click
                        var trigger = boxGo.AddComponent<EventTrigger>();
                        AddHoverEvents(trigger, desc);

                        // Click: show matrix detail
                        var btn = boxGo.AddComponent<Button>();
                        string detailText = desc;
                        btn.onClick.AddListener(() => ShowGateDetail(detailText));

                        _renderedBlocks.Add(boxGo);
                    }
                }
            }

            // Measurement columns at end
            for (int q = 0; q < _qubitCount; q++)
            {
                float yMid = 1f - (q + 0.5f) * rowH;
                float xEnd = gridEnd + 0.005f;
                var mGo = UIGo($"Meas_q{q}");
                var mRt = mGo.GetComponent<RectTransform>();
                mRt.SetParent(_gridPanel, false);
                mRt.anchorMin = new Vector2(xEnd, yMid - rowH * 0.25f);
                mRt.anchorMax = new Vector2(xEnd + 0.03f, yMid + rowH * 0.25f);
                mRt.offsetMin = mRt.offsetMax = Vector2.zero;
                mGo.AddComponent<Image>().color = new Color(0.6f, 0.6f, 0.4f, 0.6f);
                CreateChildText(mRt, "M", 10, Color.white, TextAnchor.MiddleCenter);
                _renderedBlocks.Add(mGo);
            }

            UpdateCursor();
        }

        private void UpdateCursor()
        {
            if (_stepCursor == null) return;

            if (_currentStep < 0 || _steps.Count == 0)
            {
                _stepCursor.gameObject.SetActive(false);
                return;
            }

            float gridStart = 0.08f;
            float gridEnd = 0.96f;
            float stepW = (gridEnd - gridStart) / Mathf.Max(_steps.Count, 1);
            float xCenter = gridStart + (_currentStep + 0.5f) * stepW;
            float halfCW = stepW * 0.5f;

            var rt = _stepCursor.rectTransform;
            rt.anchorMin = new Vector2(xCenter - halfCW, 0f);
            rt.anchorMax = new Vector2(xCenter + halfCW, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _stepCursor.gameObject.SetActive(true);
            _stepCursor.color = new Color(0.3f, 0.8f, 1f, 0.12f);
        }

        private void ShowGateDetail(string detail)
        {
            if (_tooltipPanel == null) return;
            _tooltipText.text = detail;
            _tooltipPanel.gameObject.SetActive(true);
        }

        // ──────────────────────────────────────────────────────────
        // Transport controls
        // ──────────────────────────────────────────────────────────
        private void BuildControls()
        {
            var go = UIGo("Controls");
            _controlPanel = go.GetComponent<RectTransform>();
            _controlPanel.SetParent(_container, false);
            _controlPanel.anchorMin = new Vector2(0f, 0f);
            _controlPanel.anchorMax = new Vector2(1f, 0.14f);
            _controlPanel.offsetMin = _controlPanel.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.12f, 0.95f);

            // Status text (placement instructions)
            _statusText = CreateText(_controlPanel, "Status", "",
                new Vector2(0.02f, 0.55f), new Vector2(0.70f, 0.98f), 12,
                new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft).GetComponent<Text>();

            // Transport buttons
            float bx = 0.02f;
            float bw = 0.065f;
            float by = 0.05f;
            float bh = 0.50f;

            _btnStepBack = CreateControlButton("◀", new Vector2(bx, by), new Vector2(bx + bw, bh),
                new Color(0.15f, 0.25f, 0.35f), () => OnStepChanged?.Invoke(-1));
            bx += bw + 0.01f;

            _btnPlayPause = CreateControlButton("►", new Vector2(bx, by), new Vector2(bx + bw, bh),
                new Color(0.1f, 0.4f, 0.25f), () => OnPlayToggle?.Invoke());
            _playPauseText = _btnPlayPause.GetComponentInChildren<Text>();
            bx += bw + 0.01f;

            _btnStepFwd = CreateControlButton("▶", new Vector2(bx, by), new Vector2(bx + bw, bh),
                new Color(0.15f, 0.25f, 0.35f), () => OnStepChanged?.Invoke(1));
            bx += bw + 0.01f;

            _btnReset = CreateControlButton("⟲", new Vector2(bx, by), new Vector2(bx + bw, bh),
                new Color(0.35f, 0.2f, 0.15f), () => OnReset?.Invoke());
            bx += bw + 0.015f;

            _btnClear = CreateControlButton("Clear", new Vector2(bx, by), new Vector2(bx + bw * 1.2f, bh),
                new Color(0.4f, 0.15f, 0.15f), ClearCircuit);
            bx += bw * 1.2f + 0.015f;

            // Step counter
            _stepCounterText = CreateText(_controlPanel, "StepCounter", "Step 0/0",
                new Vector2(bx, by), new Vector2(bx + 0.15f, bh), 12,
                new Color(0.6f, 0.75f, 0.9f), TextAnchor.MiddleLeft).GetComponent<Text>();
        }

        private Button CreateControlButton(string label, Vector2 anchorMin, Vector2 anchorMax,
            Color bgColor, UnityEngine.Events.UnityAction action)
        {
            var go = UIGo($"Btn_{label}");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_controlPanel, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(action);
            CreateChildText(rt, label, 14, Color.white, TextAnchor.MiddleCenter);
            return btn;
        }

        private void ClearCircuit()
        {
            _steps.Clear();
            _currentStep = -1;
            CancelPlacement();
            RenderGates();
            UpdateStepCounter();
            NotifyCircuitChanged();
        }

        private void UpdateStepCounter()
        {
            if (_stepCounterText == null) return;
            string step = _currentStep < 0 ? "Init" : $"{_currentStep + 1}";
            _stepCounterText.text = $"Step {step}/{_steps.Count}";
        }

        // ──────────────────────────────────────────────────────────
        // Tooltip
        // ──────────────────────────────────────────────────────────
        private void BuildTooltip()
        {
            var go = UIGo("Tooltip");
            _tooltipPanel = go.GetComponent<RectTransform>();
            _tooltipPanel.SetParent(_container, false);
            _tooltipPanel.anchorMin = new Vector2(0.55f, 0.15f);
            _tooltipPanel.anchorMax = new Vector2(0.99f, 0.32f);
            _tooltipPanel.offsetMin = _tooltipPanel.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.18f, 0.95f);

            _tooltipText = CreateText(_tooltipPanel, "TipText", "",
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f), 12,
                new Color(0.85f, 0.9f, 1f), TextAnchor.UpperLeft).GetComponent<Text>();

            _tooltipPanel.gameObject.SetActive(false);
        }

        private void AddHoverEvents(EventTrigger trigger, string text)
        {
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) =>
            {
                if (_tooltipText != null)
                {
                    _tooltipText.text = text;
                    _tooltipPanel.gameObject.SetActive(true);
                }
            });
            trigger.triggers.Add(enterEntry);

            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) =>
            {
                if (_tooltipPanel != null)
                    _tooltipPanel.gameObject.SetActive(false);
            });
            trigger.triggers.Add(exitEntry);
        }

        // ──────────────────────────────────────────────────────────
        // Status
        // ──────────────────────────────────────────────────────────
        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        // ──────────────────────────────────────────────────────────
        // UI Helpers
        // ──────────────────────────────────────────────────────────
        private GameObject CreateText(RectTransform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, TextAnchor anchor)
        {
            var go = UIGo(name);
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = anchor;
            return go;
        }

        private Text CreateChildText(RectTransform parent, string content, int fontSize,
            Color color, TextAnchor anchor)
        {
            var go = UIGo("Text");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = anchor;
            return txt;
        }

        private static GameObject UIGo(string name) => new GameObject(name, typeof(RectTransform));
    }
}
