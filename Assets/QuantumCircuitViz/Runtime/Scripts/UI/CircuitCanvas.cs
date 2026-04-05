using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using QuantumCircuitViz.Core;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Interactive circuit canvas — shows qubit wires as horizontal lines.
    /// Clicking a slot on a wire places the currently selected gate from the palette.
    /// Supports multi-qubit gates: click control qubit first, then target.
    /// Fires OnCircuitChanged whenever the circuit is modified.
    /// </summary>
    public class CircuitCanvas : MonoBehaviour
    {
        public event Action<CircuitRunner> OnCircuitChanged;

        private RectTransform _container;
        private int _qubitCount;
        private int _maxSlots = 10;

        // Grid of placed gates: [slot, qubit] → GateInfo or null
        private GateInfo[,] _grid;
        // Multi-qubit gate links: slot → list of qubit indices
        private readonly Dictionary<int, List<int>> _multiQubitSlots = new Dictionary<int, List<int>>();
        private Image[,] _slotImages;
        private Text[,] _slotTexts;

        // For multi-qubit gate placement: pending control qubit
        private GateInfo _pendingGate;
        private int _pendingSlot = -1;
        private List<int> _pendingQubits = new List<int>();

        private static readonly Color WireColor = new Color(0f, 0.5f, 0.7f, 0.6f);
        private static readonly Color SlotEmpty = new Color(0.06f, 0.08f, 0.14f, 0.7f);
        private static readonly Color SlotFilled = new Color(0f, 0.45f, 0.6f, 0.9f);
        private static readonly Color SlotHover = new Color(0.1f, 0.3f, 0.5f, 0.8f);
        private static readonly Color SlotPending = new Color(0.8f, 0.5f, 0f, 0.9f);
        private static readonly Color GateTextColor = new Color(0.9f, 0.95f, 1f);
        private static readonly Color LabelColor = new Color(0.5f, 0.7f, 0.9f);

        private GatePalette _palette;

        public void Initialise(RectTransform parent, int qubitCount, int maxSlots, GatePalette palette)
        {
            _qubitCount = qubitCount;
            _maxSlots = maxSlots;
            _palette = palette;

            _grid = new GateInfo[_maxSlots, _qubitCount];
            _slotImages = new Image[_maxSlots, _qubitCount];
            _slotTexts = new Text[_maxSlots, _qubitCount];

            gameObject.name = "CircuitCanvas";
            _container = gameObject.GetComponent<RectTransform>();
            if (_container == null)
                _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.10f, 0.68f);
            _container.anchorMax = new Vector2(0.75f, 0.97f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            var bg = gameObject.GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.03f, 0.08f, 0.90f);

            BuildGrid();
        }

        /// <summary>Clear all placed gates.</summary>
        public void Clear()
        {
            _grid = new GateInfo[_maxSlots, _qubitCount];
            _multiQubitSlots.Clear();
            _pendingGate = null;
            _pendingSlot = -1;
            _pendingQubits.Clear();

            for (int s = 0; s < _maxSlots; s++)
                for (int q = 0; q < _qubitCount; q++)
                {
                    _slotImages[s, q].color = SlotEmpty;
                    _slotTexts[s, q].text = "";
                }
        }

        /// <summary>Build a CircuitRunner from the current grid state.</summary>
        public CircuitRunner BuildCircuit()
        {
            var runner = new CircuitRunner(_qubitCount);

            for (int s = 0; s < _maxSlots; s++)
            {
                // Check for multi-qubit gate at this slot
                if (_multiQubitSlots.TryGetValue(s, out var mqQubits))
                {
                    // Find the gate — stored at first qubit index
                    var gate = _grid[s, mqQubits[0]];
                    if (gate != null)
                        runner.Add(gate.Create(), mqQubits.ToArray());
                    continue;
                }

                // Single-qubit gates
                for (int q = 0; q < _qubitCount; q++)
                {
                    var gate = _grid[s, q];
                    if (gate != null && gate.QubitCount == 1)
                        runner.Add(gate.Create(), q);
                }
            }

            return runner;
        }

        private void BuildGrid()
        {
            float wireHeight = 1f / (_qubitCount + 1);
            float slotWidth = 1f / (_maxSlots + 1);

            for (int q = 0; q < _qubitCount; q++)
            {
                float yCenter = 1f - wireHeight * (q + 1);

                // Qubit label
                var lblGo = new GameObject($"QLabel_{q}");
                var lblRt = lblGo.AddComponent<RectTransform>();
                lblRt.SetParent(_container, false);
                lblRt.anchorMin = new Vector2(0f, yCenter - 0.03f);
                lblRt.anchorMax = new Vector2(slotWidth * 0.8f, yCenter + 0.03f);
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
                var lbl = lblGo.AddComponent<Text>();
                lbl.text = $"q{q}";
                lbl.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
                lbl.fontSize = 12;
                lbl.color = LabelColor;
                lbl.alignment = TextAnchor.MiddleCenter;

                // Wire line
                var wireGo = new GameObject($"Wire_{q}");
                var wireRt = wireGo.AddComponent<RectTransform>();
                wireRt.SetParent(_container, false);
                wireRt.anchorMin = new Vector2(slotWidth * 0.8f, yCenter - 0.003f);
                wireRt.anchorMax = new Vector2(1f, yCenter + 0.003f);
                wireRt.offsetMin = Vector2.zero;
                wireRt.offsetMax = Vector2.zero;
                var wireImg = wireGo.AddComponent<Image>();
                wireImg.color = WireColor;

                // Gate slots
                for (int s = 0; s < _maxSlots; s++)
                {
                    float xCenter = slotWidth * (s + 1);
                    float halfW = slotWidth * 0.35f;
                    float halfH = wireHeight * 0.35f;

                    var slotGo = new GameObject($"Slot_{s}_{q}");
                    var slotRt = slotGo.AddComponent<RectTransform>();
                    slotRt.SetParent(_container, false);
                    slotRt.anchorMin = new Vector2(xCenter - halfW, yCenter - halfH);
                    slotRt.anchorMax = new Vector2(xCenter + halfW, yCenter + halfH);
                    slotRt.offsetMin = Vector2.zero;
                    slotRt.offsetMax = Vector2.zero;

                    var slotImg = slotGo.AddComponent<Image>();
                    slotImg.color = SlotEmpty;
                    _slotImages[s, q] = slotImg;

                    // Clickable
                    var btn = slotGo.AddComponent<Button>();
                    btn.targetGraphic = slotImg;
                    var colors = btn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
                    btn.colors = colors;

                    int capturedSlot = s;
                    int capturedQubit = q;
                    btn.onClick.AddListener(() => OnSlotClicked(capturedSlot, capturedQubit));

                    // Gate text
                    var txtGo = new GameObject("GateText");
                    var txtRt = txtGo.AddComponent<RectTransform>();
                    txtRt.SetParent(slotRt, false);
                    txtRt.anchorMin = Vector2.zero;
                    txtRt.anchorMax = Vector2.one;
                    txtRt.offsetMin = Vector2.zero;
                    txtRt.offsetMax = Vector2.zero;
                    var txt = txtGo.AddComponent<Text>();
                    txt.text = "";
                    txt.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
                    txt.fontSize = 11;
                    txt.color = GateTextColor;
                    txt.alignment = TextAnchor.MiddleCenter;
                    _slotTexts[s, q] = txt;
                }
            }
        }

        private void OnSlotClicked(int slot, int qubit)
        {
            // If slot already has a gate, remove it
            if (_grid[slot, qubit] != null)
            {
                RemoveGate(slot, qubit);
                FireChanged();
                return;
            }

            var gate = _palette?.SelectedGate;
            if (gate == null) return;

            if (gate.QubitCount == 1)
            {
                PlaceSingleQubitGate(slot, qubit, gate);
                FireChanged();
            }
            else
            {
                // Multi-qubit: collect qubits one by one
                if (_pendingGate != gate || _pendingSlot != slot)
                {
                    // Start new multi-qubit placement
                    ClearPending();
                    _pendingGate = gate;
                    _pendingSlot = slot;
                    _pendingQubits.Add(qubit);
                    _slotImages[slot, qubit].color = SlotPending;
                    _slotTexts[slot, qubit].text = gate.QubitCount == 2 ? "●" : "●";
                }
                else
                {
                    // Continue collecting
                    if (!_pendingQubits.Contains(qubit))
                    {
                        _pendingQubits.Add(qubit);
                        _slotImages[slot, qubit].color = SlotPending;
                        _slotTexts[slot, qubit].text = _pendingQubits.Count == gate.QubitCount ? "⊕" : "●";
                    }

                    if (_pendingQubits.Count == gate.QubitCount)
                    {
                        PlaceMultiQubitGate(slot, gate, new List<int>(_pendingQubits));
                        ClearPending();
                        FireChanged();
                    }
                }
            }
        }

        private void PlaceSingleQubitGate(int slot, int qubit, GateInfo gate)
        {
            _grid[slot, qubit] = gate;
            _slotImages[slot, qubit].color = SlotFilled;
            _slotTexts[slot, qubit].text = gate.Symbol;
        }

        private void PlaceMultiQubitGate(int slot, GateInfo gate, List<int> qubits)
        {
            _multiQubitSlots[slot] = qubits;
            for (int i = 0; i < qubits.Count; i++)
            {
                _grid[slot, qubits[i]] = gate;
                _slotImages[slot, qubits[i]].color = SlotFilled;
                _slotTexts[slot, qubits[i]].text = i == 0 ? "●" : gate.Symbol;
            }
        }

        private void RemoveGate(int slot, int qubit)
        {
            if (_multiQubitSlots.TryGetValue(slot, out var mqQubits))
            {
                foreach (var q in mqQubits)
                {
                    _grid[slot, q] = null;
                    _slotImages[slot, q].color = SlotEmpty;
                    _slotTexts[slot, q].text = "";
                }
                _multiQubitSlots.Remove(slot);
            }
            else
            {
                _grid[slot, qubit] = null;
                _slotImages[slot, qubit].color = SlotEmpty;
                _slotTexts[slot, qubit].text = "";
            }
        }

        private void ClearPending()
        {
            if (_pendingGate != null && _pendingSlot >= 0)
            {
                foreach (var q in _pendingQubits)
                {
                    if (_grid[_pendingSlot, q] == null)
                    {
                        _slotImages[_pendingSlot, q].color = SlotEmpty;
                        _slotTexts[_pendingSlot, q].text = "";
                    }
                }
            }
            _pendingGate = null;
            _pendingSlot = -1;
            _pendingQubits.Clear();
        }

        private void FireChanged()
        {
            var runner = BuildCircuit();
            OnCircuitChanged?.Invoke(runner);
        }
    }
}
