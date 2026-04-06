using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using QuantumCircuitViz.Core;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders a per-qubit error rate heatmap overlay on the circuit diagram area.
    /// Each qubit wire gets a color-coded strip: green (low error) → red (high error).
    /// Overlays on top of the circuit diagram when QEC mode is active.
    /// </summary>
    public class ErrorHeatmapOverlay : MonoBehaviour
    {
        private RectTransform _container;
        private Image[] _qubitStrips;
        private Text[] _qubitLabels;
        private Text _titleText;
        private int _qubitCount;

        private static readonly Color LowError = new Color(0.1f, 0.8f, 0.3f, 0.7f);
        private static readonly Color MidError = new Color(1f, 0.8f, 0.1f, 0.7f);
        private static readonly Color HighError = new Color(1f, 0.15f, 0.1f, 0.7f);
        private static readonly Color PanelBg = new Color(0.03f, 0.04f, 0.10f, 0.85f);

        public void Initialise(RectTransform parent, int qubitCount)
        {
            _qubitCount = qubitCount;

            var go = new GameObject("ErrorHeatmap");
            _container = go.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.02f, 0.22f);
            _container.anchorMax = new Vector2(0.55f, 0.40f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = PanelBg;

            // Title
            var titleGo = new GameObject("Title");
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.SetParent(_container, false);
            titleRt.anchorMin = new Vector2(0.02f, 0.82f);
            titleRt.anchorMax = new Vector2(0.98f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _titleText = titleGo.AddComponent<Text>();
            _titleText.text = "Error Rate per Qubit";
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            _titleText.fontSize = 11;
            _titleText.color = new Color(0f, 0.9f, 1f);
            _titleText.alignment = TextAnchor.MiddleCenter;

            // Qubit strips
            _qubitStrips = new Image[qubitCount];
            _qubitLabels = new Text[qubitCount];
            float stripHeight = 0.75f / Mathf.Max(qubitCount, 1);

            for (int q = 0; q < qubitCount; q++)
            {
                float yTop = 0.80f - q * stripHeight;
                float yBot = yTop - stripHeight * 0.7f;

                // Label
                var lblGo = new GameObject($"QLabel_{q}");
                var lblRt = lblGo.AddComponent<RectTransform>();
                lblRt.SetParent(_container, false);
                lblRt.anchorMin = new Vector2(0.02f, yBot);
                lblRt.anchorMax = new Vector2(0.12f, yTop);
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
                _qubitLabels[q] = lblGo.AddComponent<Text>();
                _qubitLabels[q].text = $"q{q}";
                _qubitLabels[q].font = Font.CreateDynamicFontFromOSFont("Consolas", 10);
                _qubitLabels[q].fontSize = 10;
                _qubitLabels[q].color = new Color(0.7f, 0.8f, 0.9f);
                _qubitLabels[q].alignment = TextAnchor.MiddleCenter;

                // Color strip
                var stripGo = new GameObject($"Strip_{q}");
                var stripRt = stripGo.AddComponent<RectTransform>();
                stripRt.SetParent(_container, false);
                stripRt.anchorMin = new Vector2(0.14f, yBot);
                stripRt.anchorMax = new Vector2(0.96f, yTop);
                stripRt.offsetMin = Vector2.zero;
                stripRt.offsetMax = Vector2.zero;
                _qubitStrips[q] = stripGo.AddComponent<Image>();
                _qubitStrips[q].color = LowError;
            }

            _container.gameObject.SetActive(false);
        }

        /// <summary>
        /// Update the heatmap with per-qubit error rates.
        /// Rates should be in [0, 1] range.
        /// </summary>
        public void UpdateErrorRates(float[] errorRates)
        {
            if (_qubitStrips == null) return;
            for (int q = 0; q < _qubitCount && q < errorRates.Length; q++)
            {
                float rate = Mathf.Clamp01(errorRates[q]);
                _qubitStrips[q].color = ErrorToColor(rate);
            }
        }

        /// <summary>Set uniform error rate for all qubits.</summary>
        public void SetUniformRate(float rate)
        {
            if (_qubitStrips == null) return;
            var color = ErrorToColor(rate);
            for (int q = 0; q < _qubitCount; q++)
                _qubitStrips[q].color = color;
        }

        public void Show() => _container.gameObject.SetActive(true);
        public void Hide() => _container.gameObject.SetActive(false);

        private static Color ErrorToColor(float rate)
        {
            // 0→green, 0.15→yellow, 0.3+→red
            if (rate < 0.15f)
                return Color.Lerp(LowError, MidError, rate / 0.15f);
            return Color.Lerp(MidError, HighError, Mathf.Clamp01((rate - 0.15f) / 0.15f));
        }
    }
}
