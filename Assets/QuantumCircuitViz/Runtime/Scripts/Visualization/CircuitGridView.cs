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
        public event Action<int> OnMeasureRequested;
        public event Action<int> OnStepChanged;
        public event Action OnPlayToggle;
        public event Action OnReset;
        /// <summary>Fires when the user creates a new blank circuit with the given qubit count.</summary>
        public event Action<int> OnNewCircuit;

        // ── Layout panels ────────────────────────────────────────
        private RectTransform _container;
        private RectTransform _palettePanel;
        private RectTransform _gridPanel;
        private RectTransform _classicalPanel;
        private RectTransform _controlPanel;

        // ── Tooltip ──────────────────────────────────────────────
        private RectTransform _tooltipPanel;
        private Text _tooltipText;

        // ── Status / placement mode ──────────────────────────────
        private Text _statusText;
        private GateInfo _selectedGate;
        private readonly List<int> _pendingGateQubits = new List<int>();

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
        private Button _btnUndo, _btnNew, _btnQubitMinus, _btnQubitPlus;
        private Text _playPauseText;
        private Text _stepCounterText;
        private Text _qubitCountText;
        private Text _classicalHeaderText;
        private Text[] _classicalBitTexts;

        // ── Palette buttons ──────────────────────────────────────
        private readonly Dictionary<GateInfo, Image> _paletteBtnImages = new Dictionary<GateInfo, Image>();

        // ── Constants ────────────────────────────────────────────
        private const float CellWidth = 60f;
        private const float WireHeight = 36f;
        private static readonly Color WireColor = new Color(0.45f, 0.50f, 0.55f, 0.8f);
        private static readonly Color CursorColor = new Color(0.3f, 0.8f, 1f, 0.15f);
        private static readonly Color GridBg = new Color(0.06f, 0.06f, 0.08f, 1f);

        // ── Public properties ────────────────────────────────────
        public int CurrentStep => _currentStep;
        public int StepCount => _steps.Count;
        public bool IsPlaying => _isPlaying;
        public int QubitCount => _qubitCount;
        public int PendingQubitCount => _pendingQubitCount;

        /// <summary>Change qubit count for the next new circuit (does not alter current circuit).</summary>
        public void SetPendingQubitCount(int count)
        {
            _pendingQubitCount = Mathf.Clamp(count, 1, 5);
            UpdateQubitCountLabel();
        }

        /// <summary>Change qubit count and immediately rebuild the circuit as a blank canvas.</summary>
        public void ChangeQubitCount(int delta)
        {
            SetPendingQubitCount(_pendingQubitCount + delta);
            NewBlankCircuit();
        }

        /// <summary>Create a new blank circuit with the pending qubit count.</summary>
        public void NewBlankCircuit()
        {
            _qubitCount = _pendingQubitCount;
            _pendingQubitCount = _qubitCount;
            _steps.Clear();
            _currentStep = -1;
            CancelPlacement();
            RebuildGrid();
            RenderGates();
            UpdateStepCounter();
            UpdateQubitCountLabel();
            OnNewCircuit?.Invoke(_qubitCount);
        }

        private int _pendingQubitCount = 2;

        private void UpdateQubitCountLabel()
        {
            if (_qubitCountText != null)
                _qubitCountText.text = $"{_pendingQubitCount}q";
        }

        // ──────────────────────────────────────────────────────────
        // Initialise
        // ──────────────────────────────────────────────────────────
        public void Initialise(RectTransform parent, int qubitCount)
        {
            _qubitCount = qubitCount;
            _pendingQubitCount = qubitCount;
            gameObject.name = "CircuitView";

            _container = GetComponent<RectTransform>();
            if (_container == null) _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = Vector2.zero;
            _container.anchorMax = Vector2.one;
            _container.offsetMin = _container.offsetMax = Vector2.zero;

            BuildPalette();
            BuildGrid();
            BuildClassicalRegisterPanel();
            BuildControls();
            BuildTooltip();
        }

        // ──────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────
        public void SetCircuit(IReadOnlyList<GateStep> steps, int qubitCount)
        {
            _qubitCount = qubitCount;
            _pendingQubitCount = qubitCount;
            _steps = new List<GateStep>(steps);
            _currentStep = -1;
            CancelPlacement();
            RebuildGrid();
            RebuildClassicalRegister();
            RenderGates();
            UpdateStepCounter();
            UpdateQubitCountLabel();
            ClearClassicalRegister();
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

            // Cancel button (compact)
            CreatePaletteActionButton("✕", new Vector2(0.93f, 0.25f), new Vector2(0.99f, 0.75f),
                new Color(0.55f, 0.18f, 0.18f), CancelPlacement);
        }

        private Color GetQiskitColor(string sym)
        {
            if (sym == "X" || sym == "Y" || sym == "Z") return new Color(0.12f, 0.73f, 0.72f); // Cyan
            if (sym == "H") return new Color(0.45f, 0.65f, 0.90f); // Soft blue
            if (sym == "CX" || sym == "CZ") return new Color(0.40f, 0.60f, 0.95f); // Bright blue
            if (sym == "CCX") return new Color(0.75f, 0.50f, 0.95f); // Purple
            if (sym == "SW" || sym == "CSW") return new Color(0.20f, 0.75f, 0.50f); // Green
            if (sym == "S" || sym == "T" || sym == "S†" || sym == "T†") return new Color(0.85f, 0.55f, 0.65f); // Pinkish
            if (sym.StartsWith("R")) return new Color(0.90f, 0.40f, 0.40f); // Red
            return new Color(0.50f, 0.55f, 0.60f); // Gray
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

            Color qiskitColor = GetQiskitColor(gate.Symbol);
            var img = go.AddComponent<Image>();
            img.color = qiskitColor * 0.6f; // Dim for unselected
            _paletteBtnImages[gate] = img;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => OnPaletteClick(gate));

            var txt = CreateChildText(rt, gate.Symbol, 11, Color.white, TextAnchor.MiddleCenter);
            var textShadow = txt.gameObject.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0, 0, 0, 0.5f);
            textShadow.effectDistance = new Vector2(1, -1);

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
            _pendingGateQubits.Clear();

            // Highlight the selected palette button
            if (_paletteBtnImages.TryGetValue(gate, out var img))
            {
                img.color = GetQiskitColor(gate.Symbol); // Full color for selected
            }

            SetStatus(BuildPlacementPrompt(gate, _pendingGateQubits));
        }

        private void CancelPlacement()
        {
            if (_selectedGate != null && _paletteBtnImages.TryGetValue(_selectedGate, out var img))
            {
                img.color = GetQiskitColor(_selectedGate.Symbol) * 0.6f; // Back to dim
            }
            _selectedGate = null;
            _pendingGateQubits.Clear();
            SetStatus("");
        }

        private string BuildPlacementPrompt(GateInfo gate, IReadOnlyList<int> selectedQubits)
        {
            if (gate == null)
                return "";

            if (gate.QubitCount == 1)
                return $"Click a qubit wire to place {gate.DisplayName}";

            if (gate.QubitCount == 2)
            {
                if (gate.Symbol == "SW")
                {
                    if (selectedQubits.Count == 0)
                        return "Click first qubit for SWAP";
                    return $"Click second qubit for SWAP  (first=q{selectedQubits[0]})";
                }

                if (selectedQubits.Count == 0)
                    return $"Click CONTROL qubit for {gate.DisplayName}";
                return $"Click TARGET qubit for {gate.DisplayName}  (control=q{selectedQubits[0]})";
            }

            if (gate.Symbol == "CCX")
            {
                if (selectedQubits.Count == 0)
                    return "Click first CONTROL qubit for Toffoli";
                if (selectedQubits.Count == 1)
                    return $"Click second CONTROL qubit for Toffoli  (first=q{selectedQubits[0]})";
                return $"Click TARGET qubit for Toffoli  (controls=q{selectedQubits[0]},q{selectedQubits[1]})";
            }

            if (gate.Symbol == "CSW")
            {
                if (selectedQubits.Count == 0)
                    return "Click CONTROL qubit for Fredkin";
                if (selectedQubits.Count == 1)
                    return $"Click first TARGET qubit for Fredkin  (control=q{selectedQubits[0]})";
                return $"Click second TARGET qubit for Fredkin  (control=q{selectedQubits[0]}, target=q{selectedQubits[1]})";
            }

            return $"Click qubit {selectedQubits.Count + 1} for {gate.DisplayName}";
        }

        // ──────────────────────────────────────────────────────────
        // Grid
        // ──────────────────────────────────────────────────────────
        private void BuildGrid()
        {
            var go = UIGo("Grid");
            _gridPanel = go.GetComponent<RectTransform>();
            _gridPanel.SetParent(_container, false);
            _gridPanel.anchorMin = new Vector2(0f, 0.24f);
            _gridPanel.anchorMax = new Vector2(1f, 0.87f);
            _gridPanel.offsetMin = _gridPanel.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = GridBg;

            BuildWires();
            BuildStepCursor();
        }

        private void BuildClassicalRegisterPanel()
        {
            var go = UIGo("ClassicalRegister");
            _classicalPanel = go.GetComponent<RectTransform>();
            _classicalPanel.SetParent(_container, false);
            _classicalPanel.anchorMin = new Vector2(0f, 0.15f);
            _classicalPanel.anchorMax = new Vector2(1f, 0.23f);
            _classicalPanel.offsetMin = _classicalPanel.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.10f, 0.95f);

            _classicalHeaderText = CreateText(_classicalPanel, "ClassicalHeader", "creg c[2]  Measurement pending",
                new Vector2(0.02f, 0.08f), new Vector2(0.22f, 0.92f), 12,
                new Color(0.75f, 0.82f, 0.95f), TextAnchor.MiddleLeft).GetComponent<Text>();

            RebuildClassicalRegister();
        }

        private void RebuildClassicalRegister()
        {
            if (_classicalPanel == null)
                return;

            for (int i = _classicalPanel.childCount - 1; i >= 0; i--)
            {
                var child = _classicalPanel.GetChild(i);
                if (child.name.StartsWith("ClassicalBit_") || child.name.StartsWith("ClassicalValue_"))
                    DestroyImmediate(child.gameObject);
            }

            if (_classicalHeaderText != null)
                _classicalHeaderText.text = $"creg c[{_qubitCount}]  Measurement pending";

            _classicalBitTexts = new Text[_qubitCount];
            float startX = 0.25f;
            float gap = _qubitCount >= 4 ? 0.008f : 0.012f;
            float availableWidth = 0.72f;
            float width = (availableWidth - gap * Mathf.Max(_qubitCount - 1, 0)) / Mathf.Max(_qubitCount, 1);

            for (int i = 0; i < _qubitCount; i++)
            {
                float x0 = startX + i * (width + gap);
                float x1 = Mathf.Min(x0 + width, 0.98f);

                var boxGo = UIGo($"ClassicalBit_{i}");
                var boxRt = boxGo.GetComponent<RectTransform>();
                boxRt.SetParent(_classicalPanel, false);
                boxRt.anchorMin = new Vector2(x0, 0.14f);
                boxRt.anchorMax = new Vector2(x1, 0.86f);
                boxRt.offsetMin = boxRt.offsetMax = Vector2.zero;
                boxGo.AddComponent<Image>().color = new Color(0.10f, 0.16f, 0.24f, 0.95f);

                CreateText(boxRt, $"ClassicalLabel_{i}", $"c[{i}]",
                    new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.92f), 9,
                    new Color(0.55f, 0.75f, 0.95f), TextAnchor.MiddleCenter);

                var valueGo = UIGo($"ClassicalValue_{i}");
                var valueRt = valueGo.GetComponent<RectTransform>();
                valueRt.SetParent(boxRt, false);
                valueRt.anchorMin = new Vector2(0.08f, 0.08f);
                valueRt.anchorMax = new Vector2(0.92f, 0.55f);
                valueRt.offsetMin = valueRt.offsetMax = Vector2.zero;
                _classicalBitTexts[i] = valueGo.AddComponent<Text>();
                _classicalBitTexts[i].text = ".";
                _classicalBitTexts[i].font = Font.CreateDynamicFontFromOSFont("Consolas", 18);
                _classicalBitTexts[i].fontSize = 18;
                _classicalBitTexts[i].alignment = TextAnchor.MiddleCenter;
                _classicalBitTexts[i].color = new Color(0.95f, 0.97f, 1f);
            }
        }

        public void ClearClassicalRegister()
        {
            if (_classicalBitTexts == null)
                return;

            for (int i = 0; i < _classicalBitTexts.Length; i++)
            {
                if (_classicalBitTexts[i] != null)
                    _classicalBitTexts[i].text = ".";
            }

            if (_classicalHeaderText != null)
                _classicalHeaderText.text = $"creg c[{_qubitCount}]  Measurement pending";
        }

        public void SetClassicalRegister(string bitString)
        {
            if (_classicalBitTexts == null)
                return;

            for (int i = 0; i < _classicalBitTexts.Length; i++)
            {
                if (_classicalBitTexts[i] == null)
                    continue;

                _classicalBitTexts[i].text = i < bitString.Length ? bitString[i].ToString() : ".";
            }

            if (_classicalHeaderText != null)
                _classicalHeaderText.text = $"creg c[{_qubitCount}]  Last readout: {bitString}";
        }

        public void SetClassicalBit(int index, int? value)
        {
            if (_classicalBitTexts == null || index < 0 || index >= _classicalBitTexts.Length)
                return;

            _classicalBitTexts[index].text = value.HasValue ? value.Value.ToString() : ".";
        }

        public void SetClassicalBits(IReadOnlyList<int?> values, string headerSuffix = null)
        {
            if (_classicalBitTexts == null)
                return;

            for (int i = 0; i < _classicalBitTexts.Length; i++)
            {
                int? value = values != null && i < values.Count ? values[i] : null;
                _classicalBitTexts[i].text = value.HasValue ? value.Value.ToString() : ".";
            }

            if (_classicalHeaderText != null)
            {
                string suffix = string.IsNullOrWhiteSpace(headerSuffix) ? "Measurement pending" : headerSuffix;
                _classicalHeaderText.text = $"creg c[{_qubitCount}]  {suffix}";
            }
        }

        private void RebuildGrid()
        {
            // Clear rendered gate blocks first (they are also grid children)
            foreach (var go in _renderedBlocks)
                if (go != null) DestroyImmediate(go);
            _renderedBlocks.Clear();

            // Destroy ALL grid children immediately so new wires are the only children
            for (int i = _gridPanel.childCount - 1; i >= 0; i--)
                DestroyImmediate(_gridPanel.GetChild(i).gameObject);

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
                CreateText(_gridPanel, $"QLabel_{q}", $"|q{q}⟩",
                    new Vector2(0f, yMid - rowH * 0.4f), new Vector2(0.06f, yMid + rowH * 0.4f),
                    14, new Color(0.7f, 0.8f, 0.9f), TextAnchor.MiddleCenter);

                // Wire line (thinner, crisper)
                var wireGo = UIGo($"Wire_{q}");
                var wireRt = wireGo.GetComponent<RectTransform>();
                wireRt.SetParent(_gridPanel, false);
                wireRt.anchorMin = new Vector2(0.06f, yMid - 0.0015f);
                wireRt.anchorMax = new Vector2(0.98f, yMid + 0.0015f);
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
            if (_selectedGate == null)
            {
                SetStatus($"q{qubit} clicked. Select a gate from the palette first to place it here.");
                return;
            }

            if (_pendingGateQubits.Contains(qubit))
            {
                SetStatus($"q{qubit} is already selected for {_selectedGate.DisplayName}");
                return;
            }

            _pendingGateQubits.Add(qubit);

            if (_pendingGateQubits.Count < _selectedGate.QubitCount)
            {
                SetStatus(BuildPlacementPrompt(_selectedGate, _pendingGateQubits));
                return;
            }

            var gate = _selectedGate.Create();
            _steps.Add(new GateStep(gate, new List<int>(_pendingGateQubits)));
            CancelPlacement();
            RenderGates();
            NotifyCircuitChanged();
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
                string sym = info != null ? info.Symbol : step.Gate.GetType().Name.Replace("Gate", "");
                Color gateColor = GetQiskitColor(sym);
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
                    lineRt.anchorMin = new Vector2(xCenter - 0.001f, yBot); // Thinner line
                    lineRt.anchorMax = new Vector2(xCenter + 0.001f, yTop);
                    lineRt.offsetMin = lineRt.offsetMax = Vector2.zero;
                    var lineImg = lineGo.AddComponent<Image>();
                    lineImg.color = new Color(gateColor.r, gateColor.g, gateColor.b, 0.95f); // Match the gate color!
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
                        float dotSize = rowH * 0.4f; // Larger rect to fit the text circle
                        dotRt.anchorMin = new Vector2(xCenter - dotSize * 0.5f, yMid - dotSize * 0.5f);
                        dotRt.anchorMax = new Vector2(xCenter + dotSize * 0.5f, yMid + dotSize * 0.5f);
                        dotRt.offsetMin = dotRt.offsetMax = Vector2.zero;
                        
                        var dotImg = dotGo.AddComponent<Image>();
                        dotImg.color = Color.clear; // Invisible square background background

                        // Render a huge text dot in exactly the gate color
                        CreateChildText(dotRt, "●", 28, new Color(gateColor.r, gateColor.g, gateColor.b, 1f), TextAnchor.MiddleCenter);

                        var trigger = dotGo.AddComponent<EventTrigger>();
                        AddHoverEvents(trigger, desc);

                        // Allow the click action to also trigger on the control dot!
                        var btn = dotGo.AddComponent<Button>();
                        int stepIdx = s;
                        btn.onClick.AddListener(() => RemoveGateAt(stepIdx));

                        _renderedBlocks.Add(dotGo);
                    }
                    else
                    {
                        // Gate box
                        var boxGo = UIGo($"Gate_{s}_q{q}");
                        var boxRt = boxGo.GetComponent<RectTransform>();
                        boxRt.SetParent(_gridPanel, false);
                        
                        // If it's a target for CNOT or Toffoli, make it a circle (if we had radius) or thinner block.
                        // We will style normal blocks elegantly:
                        float halfW = blockW * 0.35f; // Smaller width
                        float halfH = rowH * 0.25f;   // Smaller height
                        
                        // For CNOT/Toffoli targets, Qiskit style draws a pure target loop matching the control color
                        bool isCnotTarget = (step.Gate is CNOTGate || step.Gate is ToffoliGate) && qi == step.QubitIndices.Count - 1;
                        if (isCnotTarget) 
                        {
                            halfW = blockW * 0.45f;
                            halfH = rowH * 0.45f;
                        }

                        boxRt.anchorMin = new Vector2(xCenter - halfW, yMid - halfH);
                        boxRt.anchorMax = new Vector2(xCenter + halfW, yMid + halfH);
                        boxRt.offsetMin = boxRt.offsetMax = Vector2.zero;

                        var boxImg = boxGo.AddComponent<Image>();
                        
                        if (isCnotTarget) 
                            boxImg.color = Color.clear; // Totally transparent backing for the target text
                        else
                        {
                            // Qiskit colors are exact, just darken a small bit for consistency if needed, but lets just use exactly the Qiskit palette we defined
                            boxImg.color = gateColor;
                        
                            // 3D Bevel & Drop Shadow effect only for standard blocks
                            var dropShadow = boxGo.AddComponent<Shadow>();
                            dropShadow.effectColor = new Color(0, 0, 0, 0.6f);
                            dropShadow.effectDistance = new Vector2(2, -3);

                            var highlight = boxGo.AddComponent<Shadow>();
                            highlight.effectColor = new Color(1, 1, 1, 0.4f);
                            highlight.effectDistance = new Vector2(-1.5f, 1.5f);

                            var innerShadow = boxGo.AddComponent<Shadow>();
                            innerShadow.effectColor = new Color(0, 0, 0, 0.3f);
                            innerShadow.effectDistance = new Vector2(1.5f, -1.5f);
                        }

                        // Gate symbol text
                        string displaySym = sym;
                        Color textColor = Color.white;
                        int textSize = 22; // Much larger text

                        if (isCnotTarget)
                        {
                            displaySym = "●"; // Massive blue/purple solid circle instead of a cross symbol
                            textSize = 58; // Massive to form the visual element itself!
                            textColor = gateColor; // Colored exactly like line/dots!
                        }

                        // Add bold outline to text
                        var txt = CreateChildText(boxRt, displaySym, textSize, textColor, TextAnchor.MiddleCenter);
                        
                        if (isCnotTarget)
                        {
                            // Add a crisp white cross directly over the colored circle
                            CreateChildText(boxRt, "+", 32, Color.white, TextAnchor.MiddleCenter);
                        }
                        else
                        {
                            var textShadow = txt.gameObject.AddComponent<Shadow>();
                            textShadow.effectColor = new Color(0, 0, 0, 0.5f);
                            textShadow.effectDistance = new Vector2(1, -1);
                        }

                        // Hover + click
                        var trigger = boxGo.AddComponent<EventTrigger>();
                        AddHoverEvents(trigger, desc);

                        // Click: remote gate
                        var btn = boxGo.AddComponent<Button>();
                        int stepIdx = s;
                        btn.onClick.AddListener(() => RemoveGateAt(stepIdx));

                        _renderedBlocks.Add(boxGo);
                    }
                }
            }

            // Measurement columns at end — meter icon style
            for (int q = 0; q < _qubitCount; q++)
            {
                float yMid = 1f - (q + 0.5f) * rowH;
                float xEnd = gridEnd + 0.015f;
                var mGo = UIGo($"Meas_q{q}");
                var mRt = mGo.GetComponent<RectTransform>();
                mRt.SetParent(_gridPanel, false);
                float mW = 0.025f;
                float mH = rowH * 0.18f;
                mRt.anchorMin = new Vector2(xEnd, yMid - mH);
                mRt.anchorMax = new Vector2(xEnd + mW, yMid + mH);
                mRt.offsetMin = mRt.offsetMax = Vector2.zero;

                // Rounded-look dark box with subtle border feel
                var mImg = mGo.AddComponent<Image>();
                mImg.color = new Color(0.12f, 0.12f, 0.20f, 1f);

                // Outer glow shadow
                var dropShadow = mGo.AddComponent<Shadow>();
                dropShadow.effectColor = new Color(0.3f, 0.5f, 0.9f, 0.25f);
                dropShadow.effectDistance = new Vector2(0, -2);

                // Meter icon: ⌐ arc + arrow, using two lines of text
                // Top: arc symbol ◠  Bottom: ↗ arrow
                var arcTxt = CreateChildText(mRt, "◠", 16, new Color(0.7f, 0.8f, 1f, 0.9f), TextAnchor.UpperCenter);
                var arrowTxt = CreateChildText(mRt, "↗", 11, new Color(0.9f, 0.5f, 0.55f, 1f), TextAnchor.LowerCenter);

                int qubitIndex = q;
                var btn = mGo.AddComponent<Button>();
                var btnColors = btn.colors;
                btnColors.highlightedColor = new Color(0.2f, 0.25f, 0.4f, 1f);
                btnColors.pressedColor = new Color(0.3f, 0.5f, 0.9f, 1f);
                btn.colors = btnColors;
                btn.onClick.AddListener(() => OnMeasureRequested?.Invoke(qubitIndex));

                var trigger = mGo.AddComponent<EventTrigger>();
                AddHoverEvents(trigger, $"Measure q{q} → c{q}");
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

        public void RemoveGateAt(int index)
        {
            if (index >= 0 && index < _steps.Count)
            {
                _steps.RemoveAt(index);
                _currentStep = Mathf.Clamp(_currentStep, -1, _steps.Count - 1);
                CancelPlacement();
                RenderGates();
                UpdateStepCounter();
                NotifyCircuitChanged();
                SetStatus($"Removed gate at step {index + 1}");
            }
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

            _btnUndo = CreateControlButton("Undo", new Vector2(bx, by), new Vector2(bx + bw * 1.15f, bh),
                new Color(0.3f, 0.2f, 0.4f), UndoLastGate);
            bx += bw * 1.15f + 0.01f;

            _btnClear = CreateControlButton("Clear", new Vector2(bx, by), new Vector2(bx + bw * 1.2f, bh),
                new Color(0.4f, 0.15f, 0.15f), ClearCircuit);
            bx += bw * 1.2f + 0.015f;

            // Step counter
            _stepCounterText = CreateText(_controlPanel, "StepCounter", "Step 0/0",
                new Vector2(bx, by), new Vector2(bx + 0.15f, bh), 12,
                new Color(0.6f, 0.75f, 0.9f), TextAnchor.MiddleLeft).GetComponent<Text>();

            // ── Qubit selector + New button (right side) ──
            float rx = 0.98f;
            float rbw = 0.04f;

            rx -= rbw;
            _btnQubitPlus = CreateControlButton("+", new Vector2(rx, by), new Vector2(rx + rbw, bh),
                new Color(0.15f, 0.35f, 0.25f), () => ChangeQubitCount(1));

            rx -= 0.055f;
            _qubitCountText = CreateText(_controlPanel, "QubitCount", $"{_pendingQubitCount}q",
                new Vector2(rx, by), new Vector2(rx + 0.055f, bh), 13,
                new Color(0f, 0.85f, 1f), TextAnchor.MiddleCenter).GetComponent<Text>();

            rx -= rbw;
            _btnQubitMinus = CreateControlButton("−", new Vector2(rx, by), new Vector2(rx + rbw, bh),
                new Color(0.35f, 0.2f, 0.15f), () => ChangeQubitCount(-1));

            rx -= bw * 1.3f + 0.01f;
            _btnNew = CreateControlButton("New", new Vector2(rx, by), new Vector2(rx + bw * 1.3f, bh),
                new Color(0.1f, 0.3f, 0.5f), NewBlankCircuit);
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

        public void UndoLastGate()
        {
            if (_steps.Count == 0)
                return;

            _steps.RemoveAt(_steps.Count - 1);
            _currentStep = Mathf.Clamp(_currentStep, -1, _steps.Count - 1);
            CancelPlacement();
            RenderGates();
            UpdateStepCounter();
            NotifyCircuitChanged();
            SetStatus("Last gate removed");
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
