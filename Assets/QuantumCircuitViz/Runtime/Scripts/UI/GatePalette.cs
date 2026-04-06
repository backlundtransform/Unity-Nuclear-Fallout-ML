using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;
using QuantumCircuitViz.Core;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Interactive gate palette — shows available quantum gates as clickable buttons.
    /// Clicking a gate dispatches it to the CircuitCanvas for placement.
    /// </summary>
    public class GatePalette : MonoBehaviour
    {
        public event Action<GateInfo> OnGateSelected;

        private RectTransform _container;
        private readonly List<Button> _buttons = new List<Button>();
        private GateInfo _selectedGate;

        private static readonly Color BtnNormal = new Color(0.08f, 0.12f, 0.2f, 0.95f);
        private static readonly Color BtnHover = new Color(0.1f, 0.3f, 0.5f, 0.95f);
        private static readonly Color BtnSelected = new Color(0f, 0.6f, 0.8f, 0.95f);
        private static readonly Color TextColor = new Color(0f, 0.92f, 1f);
        private static readonly Color HeaderColor = new Color(0.5f, 0.7f, 0.9f);

        public GateInfo SelectedGate => _selectedGate;

        public void Initialise(RectTransform parent)
        {
            gameObject.name = "GatePalette";
            _container = gameObject.GetComponent<RectTransform>();
            if (_container == null)
                _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.0f, 0.52f);
            _container.anchorMax = new Vector2(0.09f, 0.97f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            var bg = gameObject.GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.10f, 0.92f);

            // Title
            CreateLabel(_container, "Gates", 0.82f, 1f, 14, HeaderColor);

            // Single-qubit header
            CreateLabel(_container, "1-Qubit", 0.72f, 0.82f, 10, HeaderColor);

            float y = 0.70f;
            foreach (var gate in GateLibrary.SingleQubitGates)
            {
                CreateGateButton(gate, y);
                y -= 0.09f;
            }

            // Multi-qubit header
            CreateLabel(_container, "Multi", y, y + 0.10f, 10, HeaderColor);
            y -= 0.02f;

            foreach (var gate in GateLibrary.MultiQubitGates)
            {
                CreateGateButton(gate, y);
                y -= 0.09f;
            }
        }

        public void ClearSelection()
        {
            _selectedGate = null;
            RefreshButtonColors();
        }

        private void CreateGateButton(GateInfo gate, float yTop)
        {
            var btnGo = new GameObject($"Btn_{gate.Symbol}");
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.SetParent(_container, false);
            btnRt.anchorMin = new Vector2(0.08f, yTop - 0.08f);
            btnRt.anchorMax = new Vector2(0.92f, yTop);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            var img = btnGo.AddComponent<Image>();
            img.color = BtnNormal;

            var btn = btnGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;
            btn.targetGraphic = img;

            var capturedGate = gate;
            btn.onClick.AddListener(() =>
            {
                _selectedGate = capturedGate;
                RefreshButtonColors();
                OnGateSelected?.Invoke(capturedGate);
            });

            // Label
            var txtGo = new GameObject("Label");
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.SetParent(btnRt, false);
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.text = $"{gate.Symbol}  {gate.DisplayName}";
            txt.font = Font.CreateDynamicFontFromOSFont("Consolas", 10);
            txt.fontSize = 10;
            txt.color = TextColor;
            txt.alignment = TextAnchor.MiddleCenter;

            _buttons.Add(btn);
        }

        private void RefreshButtonColors()
        {
            var allGates = GateLibrary.AllGates;
            for (int i = 0; i < _buttons.Count && i < allGates.Count; i++)
            {
                var img = _buttons[i].GetComponent<Image>();
                img.color = (allGates[i] == _selectedGate) ? BtnSelected : BtnNormal;
            }
        }

        private void CreateLabel(RectTransform parent, string text, float yMin, float yMax, int fontSize, Color color)
        {
            var go = new GameObject($"Label_{text}");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
        }
    }
}
