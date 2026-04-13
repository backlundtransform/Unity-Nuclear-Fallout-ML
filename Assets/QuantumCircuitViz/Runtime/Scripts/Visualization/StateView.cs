using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CSharpNumerics.Numerics.Objects;
using CSharpNumerics.Engines.Quantum;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Simplified state view: shows only the current state vector.
    /// Measurement probabilities are presented by MeasurementHistogram in the same view.
    /// </summary>
    public class StateView : MonoBehaviour
    {
        private RectTransform _container;
        private Text _titleText;
        private Text _summaryText;
        private Text _stateVectorText;
        private ComplexVectorN _amplitudes;
        private bool _hasState;
        private int _qubitCount;
        private int _stateCount;

        private static readonly Color PanelBg = new Color(0.04f, 0.04f, 0.10f, 0.92f);
        private static readonly Color Accent = new Color(0.6f, 0.8f, 1f);
        private static readonly Color Secondary = new Color(0.45f, 0.58f, 0.72f);
        private static readonly Color Body = new Color(0.86f, 0.91f, 0.98f);

        public void Initialise(RectTransform parent, int qubitCount)
        {
            _qubitCount = qubitCount;
            _stateCount = 1 << qubitCount;

            gameObject.name = "StateView";
            _container = GetComponent<RectTransform>();
            if (_container == null)
                _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = Vector2.zero;
            _container.anchorMax = Vector2.one;
            _container.offsetMin = _container.offsetMax = Vector2.zero;

            BuildLayout();
            RenderEmptyState();
        }

        public void Rebuild(int qubitCount)
        {
            _qubitCount = qubitCount;
            _stateCount = 1 << qubitCount;
            _hasState = false;

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            BuildLayout();
            RenderEmptyState();
        }

        public void UpdateState(QuantumState state)
        {
            _amplitudes = state.Amplitudes;
            _hasState = true;
            RenderStateVector();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (_hasState)
                RenderStateVector();
        }

        public void Hide() => gameObject.SetActive(false);

        private void BuildLayout()
        {
            var panelGo = UIGo("StateVectorPanel");
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.SetParent(_container, false);
            panelRt.anchorMin = new Vector2(0.02f, 0.02f);
            panelRt.anchorMax = new Vector2(0.34f, 0.93f);
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
            panelGo.AddComponent<Image>().color = PanelBg;

            _titleText = CreateText(panelRt, "Title", "State Vector", new Vector2(0.03f, 0.92f), new Vector2(0.72f, 0.98f), 14, Accent, TextAnchor.MiddleLeft).GetComponent<Text>();
            _summaryText = CreateText(panelRt, "Summary", "", new Vector2(0.03f, 0.84f), new Vector2(0.97f, 0.91f), 11, Secondary, TextAnchor.MiddleLeft).GetComponent<Text>();

            var scrollGo = UIGo("StateVectorScroll");
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(panelRt, false);
            scrollRt.anchorMin = new Vector2(0.03f, 0.04f);
            scrollRt.anchorMax = new Vector2(0.97f, 0.82f);
            scrollRt.offsetMin = scrollRt.offsetMax = Vector2.zero;
            scrollGo.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.88f);

            _stateVectorText = CreateText(scrollRt, "StateVectorText", "", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f), 12, Body, TextAnchor.UpperLeft).GetComponent<Text>();
            _stateVectorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _stateVectorText.verticalOverflow = VerticalWrapMode.Overflow;
            _stateVectorText.supportRichText = false;
        }

        private void RenderEmptyState()
        {
            if (_summaryText != null)
                _summaryText.text = $"{_qubitCount} qubits, {_stateCount} basis states";
            if (_stateVectorText != null)
                _stateVectorText.text = "|psi> = |" + new string('0', _qubitCount) + ">";
        }

        private void RenderStateVector()
        {
            if (_stateVectorText == null || _amplitudes.Length == 0)
                return;

            if (_summaryText != null)
                _summaryText.text = $"{_qubitCount} qubits, {_stateCount} basis states";

            var dirac = new StringBuilder();
            var expanded = new StringBuilder();
            bool firstTerm = true;

            for (int i = 0; i < _amplitudes.Length; i++)
            {
                var amplitude = _amplitudes[i];
                float real = (float)amplitude.realPart;
                float imaginary = (float)amplitude.imaginaryPart;
                float magnitudeSquared = real * real + imaginary * imaginary;
                if (magnitudeSquared < 0.00001f)
                    continue;

                string basis = "|" + System.Convert.ToString(i, 2).PadLeft(_qubitCount, '0') + ">";
                string ampText = FormatComplex(real, imaginary);
                if (!firstTerm)
                    dirac.Append(" + ");
                dirac.Append(ampText).Append(basis);
                expanded.Append(basis)
                    .Append("    ")
                    .Append(ampText)
                    .Append("    p=")
                    .Append(magnitudeSquared.ToString("F3"))
                    .Append('\n');
                firstTerm = false;
            }

            if (firstTerm)
            {
                dirac.Append("0");
                expanded.Append("No non-zero amplitudes");
            }

            _stateVectorText.text = "|psi> = " + dirac + "\n\nBasis amplitudes\n" + expanded;
        }

        private static string FormatComplex(float real, float imaginary)
        {
            if (Mathf.Abs(imaginary) < 0.0005f)
                return real.ToString("F3");
            if (Mathf.Abs(real) < 0.0005f)
                return imaginary.ToString("F3") + "i";
            string sign = imaginary >= 0f ? "+" : "-";
            return $"{real:F3}{sign}{Mathf.Abs(imaginary):F3}i";
        }

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

        private static GameObject UIGo(string name) => new GameObject(name, typeof(RectTransform));
    }
}
