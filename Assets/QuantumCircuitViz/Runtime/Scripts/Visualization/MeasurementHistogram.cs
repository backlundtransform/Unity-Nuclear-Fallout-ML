using UnityEngine;
using UnityEngine.UI;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Numerics.Objects;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders a measurement probability histogram as UI bars on a Canvas.
    /// Each computational basis state (|00⟩, |01⟩, …) gets a coloured bar.
    /// </summary>
    public class MeasurementHistogram : MonoBehaviour
    {
        private RectTransform _container;
        private Image[] _bars;
        private Text[] _labels;
        private Text _titleText;
        private int _stateCount;

        private static readonly Color BarBaseColor = new Color(0f, 0.9f, 1f, 0.9f);
        private static readonly Color BarHighColor = new Color(1f, 0.85f, 0.25f, 0.95f);

        public void Initialise(RectTransform parent, int qubitCount)
        {
            _stateCount = 1 << qubitCount;

            // Container
            var containerGo = new GameObject("Histogram");
            _container = containerGo.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.65f, 0.02f);
            _container.anchorMax = new Vector2(0.98f, 0.40f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            // Background
            var bg = containerGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.85f);

            // Title
            var titleGo = new GameObject("Title");
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.SetParent(_container, false);
            titleRt.anchorMin = new Vector2(0, 0.88f);
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _titleText = titleGo.AddComponent<Text>();
            _titleText.text = "Measurement Probabilities";
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 14);
            _titleText.fontSize = 14;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = new Color(0.8f, 0.9f, 1f);

            // Bars
            _bars = new Image[_stateCount];
            _labels = new Text[_stateCount];
            float barWidth = 1f / _stateCount;

            for (int i = 0; i < _stateCount; i++)
            {
                // Bar
                var barGo = new GameObject($"Bar_{i}");
                var barRt = barGo.AddComponent<RectTransform>();
                barRt.SetParent(_container, false);
                float x0 = i * barWidth + barWidth * 0.15f;
                float x1 = (i + 1) * barWidth - barWidth * 0.15f;
                barRt.anchorMin = new Vector2(x0, 0.12f);
                barRt.anchorMax = new Vector2(x1, 0.12f); // height set by UpdateProbabilities
                barRt.offsetMin = Vector2.zero;
                barRt.offsetMax = Vector2.zero;
                _bars[i] = barGo.AddComponent<Image>();
                _bars[i].color = BarBaseColor;

                // Label
                var lblGo = new GameObject($"Lbl_{i}");
                var lblRt = lblGo.AddComponent<RectTransform>();
                lblRt.SetParent(_container, false);
                lblRt.anchorMin = new Vector2(x0, 0f);
                lblRt.anchorMax = new Vector2(x1, 0.12f);
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
                _labels[i] = lblGo.AddComponent<Text>();
                _labels[i].text = FormatBasis(i, qubitCount);
                _labels[i].font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
                _labels[i].fontSize = 11;
                _labels[i].alignment = TextAnchor.MiddleCenter;
                _labels[i].color = new Color(0.7f, 0.8f, 0.9f);
            }
        }

        public void UpdateProbabilities(VectorN probs)
        {
            if (_bars == null) return;
            for (int i = 0; i < _stateCount && i < probs.Length; i++)
            {
                float p = (float)probs[i];
                var rt = _bars[i].rectTransform;
                float x0 = rt.anchorMin.x;
                float x1 = rt.anchorMax.x;
                rt.anchorMin = new Vector2(x0, 0.12f);
                rt.anchorMax = new Vector2(x1, 0.12f + p * 0.74f);
                _bars[i].color = Color.Lerp(BarBaseColor, BarHighColor, p);
            }
        }

        private static string FormatBasis(int index, int qubitCount)
        {
            return "|" + System.Convert.ToString(index, 2).PadLeft(qubitCount, '0') + "⟩";
        }
    }
}
