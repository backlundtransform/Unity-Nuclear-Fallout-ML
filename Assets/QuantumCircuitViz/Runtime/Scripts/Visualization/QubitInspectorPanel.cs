using UnityEngine;
using UnityEngine.UI;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Click a Bloch sphere → this panel shows detailed qubit state info:
    /// amplitude (α, β), phase, probability, purity, and Bloch coordinates.
    /// </summary>
    public class QubitInspectorPanel : MonoBehaviour
    {
        private RectTransform _panel;
        private Text _titleText;
        private Text _detailText;
        private int _selectedQubit = -1;

        private static readonly Color PanelBg = new Color(0.03f, 0.05f, 0.12f, 0.92f);
        private static readonly Color TitleColor = new Color(0f, 0.9f, 1f);
        private static readonly Color DetailColor = new Color(0.7f, 0.85f, 0.95f);

        public int SelectedQubit => _selectedQubit;

        public void Initialise(RectTransform parent)
        {
            var go = new GameObject("QubitInspector");
            _panel = go.AddComponent<RectTransform>();
            _panel.SetParent(parent, false);
            _panel.anchorMin = new Vector2(0.70f, 0.02f);
            _panel.anchorMax = new Vector2(0.98f, 0.40f);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = PanelBg;

            // Title
            var titleGo = new GameObject("Title");
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.SetParent(_panel, false);
            titleRt.anchorMin = new Vector2(0.05f, 0.85f);
            titleRt.anchorMax = new Vector2(0.95f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _titleText = titleGo.AddComponent<Text>();
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 14);
            _titleText.fontSize = 14;
            _titleText.color = TitleColor;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.text = "Qubit Inspector";

            // Detail
            var detailGo = new GameObject("Detail");
            var detailRt = detailGo.AddComponent<RectTransform>();
            detailRt.SetParent(_panel, false);
            detailRt.anchorMin = new Vector2(0.05f, 0.02f);
            detailRt.anchorMax = new Vector2(0.95f, 0.84f);
            detailRt.offsetMin = Vector2.zero;
            detailRt.offsetMax = Vector2.zero;
            _detailText = detailGo.AddComponent<Text>();
            _detailText.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            _detailText.fontSize = 12;
            _detailText.color = DetailColor;
            _detailText.alignment = TextAnchor.UpperLeft;
            _detailText.text = "Click a Bloch sphere\nto inspect qubit state.";

            _panel.gameObject.SetActive(true);
        }

        /// <summary>
        /// Select a qubit and display its state.
        /// bloch: the reduced Bloch vector for this qubit.
        /// alphaR, alphaI, betaR, betaI: complex amplitudes (for single-qubit display).
        /// </summary>
        public void ShowQubit(int qubitIndex, Vector3 bloch,
            double alphaR = double.NaN, double alphaI = double.NaN,
            double betaR = double.NaN, double betaI = double.NaN)
        {
            _selectedQubit = qubitIndex;
            _titleText.text = $"Qubit q{qubitIndex}";

            float mag = bloch.magnitude;
            float prob0 = (1f + bloch.y) * 0.5f; // bloch.y = Z in our mapping
            float prob1 = 1f - prob0;

            string purity = mag > 0.99f ? "Pure" : mag > 0.5f ? "Mixed" : "Highly Entangled";

            string ampStr = "";
            if (!double.IsNaN(alphaR))
            {
                ampStr = $"  α = {alphaR:F3} + {alphaI:F3}i\n" +
                         $"  β = {betaR:F3} + {betaI:F3}i\n";
            }

            _detailText.text =
                $"Bloch Vector:\n" +
                $"  X = {bloch.x:F4}\n" +
                $"  Y = {bloch.z:F4}\n" +
                $"  Z = {bloch.y:F4}\n" +
                $"  |r| = {mag:F4}\n\n" +
                ampStr +
                $"P(|0⟩) = {prob0:F4}\n" +
                $"P(|1⟩) = {prob1:F4}\n\n" +
                $"State: {purity}";
        }

        /// <summary>Clear the selection.</summary>
        public void ClearInspector()
        {
            _selectedQubit = -1;
            _titleText.text = "Qubit Inspector";
            _detailText.text = "Click a Bloch sphere\nto inspect qubit state.";
        }
    }
}
