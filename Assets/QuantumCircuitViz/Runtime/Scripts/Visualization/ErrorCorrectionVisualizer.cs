using UnityEngine;
using UnityEngine.UI;
using QuantumCircuitViz.Core;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Split-view panel comparing noisy (unprotected) vs QEC-protected fidelity.
    /// Shows two vertical bars side-by-side with numeric fidelity values,
    /// a syndrome display, and correction info from the last QEC cycle.
    /// </summary>
    public class ErrorCorrectionVisualizer : MonoBehaviour
    {
        private RectTransform _container;
        private Image _unprotectedBar;
        private Image _protectedBar;
        private Text _unprotectedLabel;
        private Text _protectedLabel;
        private Text _titleText;
        private Text _syndromeText;
        private Text _correctionText;
        private Text _codeInfoText;

        private static readonly Color UnprotectedColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        private static readonly Color ProtectedColor = new Color(0.2f, 0.9f, 0.4f, 0.9f);
        private static readonly Color PanelBg = new Color(0.03f, 0.04f, 0.10f, 0.92f);
        private static readonly Color LabelColor = new Color(0.8f, 0.9f, 1f);
        private static readonly Color AccentColor = new Color(0f, 0.9f, 1f);

        // Animated bar heights
        private float _targetUnprot;
        private float _targetProt;
        private float _currentUnprot;
        private float _currentProt;

        public void Initialise(RectTransform parent)
        {
            var go = new GameObject("ErrorCorrectionViz");
            _container = go.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.60f, 0.42f);
            _container.anchorMax = new Vector2(0.98f, 0.80f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = PanelBg;

            // Title
            CreateText("Title", _container, "QEC Fidelity Comparison", 14, AccentColor,
                new Vector2(0.02f, 0.88f), new Vector2(0.98f, 0.98f), TextAnchor.MiddleCenter);

            // Code info
            _codeInfoText = CreateText("CodeInfo", _container, "", 11, new Color(0.5f, 0.6f, 0.7f),
                new Vector2(0.02f, 0.80f), new Vector2(0.98f, 0.88f), TextAnchor.MiddleCenter);

            // Bars region
            // Unprotected bar
            var unprotBarGo = new GameObject("UnprotBar");
            var unprotRt = unprotBarGo.AddComponent<RectTransform>();
            unprotRt.SetParent(_container, false);
            unprotRt.anchorMin = new Vector2(0.10f, 0.25f);
            unprotRt.anchorMax = new Vector2(0.35f, 0.25f); // height animated
            unprotRt.offsetMin = Vector2.zero;
            unprotRt.offsetMax = Vector2.zero;
            _unprotectedBar = unprotBarGo.AddComponent<Image>();
            _unprotectedBar.color = UnprotectedColor;

            // Protected bar
            var protBarGo = new GameObject("ProtBar");
            var protRt = protBarGo.AddComponent<RectTransform>();
            protRt.SetParent(_container, false);
            protRt.anchorMin = new Vector2(0.40f, 0.25f);
            protRt.anchorMax = new Vector2(0.65f, 0.25f); // height animated
            protRt.offsetMin = Vector2.zero;
            protRt.offsetMax = Vector2.zero;
            _protectedBar = protBarGo.AddComponent<Image>();
            _protectedBar.color = ProtectedColor;

            // Labels under bars
            _unprotectedLabel = CreateText("UnprotLabel", _container, "Noisy\n0.000", 11, UnprotectedColor,
                new Vector2(0.05f, 0.12f), new Vector2(0.38f, 0.25f), TextAnchor.UpperCenter);
            _protectedLabel = CreateText("ProtLabel", _container, "QEC\n0.000", 11, ProtectedColor,
                new Vector2(0.38f, 0.12f), new Vector2(0.68f, 0.25f), TextAnchor.UpperCenter);

            // Syndrome display
            _syndromeText = CreateText("Syndrome", _container, "", 11, LabelColor,
                new Vector2(0.68f, 0.50f), new Vector2(0.98f, 0.78f), TextAnchor.UpperLeft);

            // Correction display
            _correctionText = CreateText("Corrections", _container, "", 10, new Color(0.6f, 0.8f, 0.6f),
                new Vector2(0.68f, 0.25f), new Vector2(0.98f, 0.50f), TextAnchor.UpperLeft);

            _container.gameObject.SetActive(false);
        }

        /// <summary>Update with Monte-Carlo comparison results.</summary>
        public void ShowComparison(QECComparison comparison, string codeName)
        {
            _container.gameObject.SetActive(true);
            _targetUnprot = (float)comparison.UnprotectedFidelity;
            _targetProt = (float)comparison.ProtectedFidelity;

            _codeInfoText.text = $"{codeName}  |  ε={comparison.ErrorRate:F3}  |  {comparison.Rounds} rounds";
        }

        /// <summary>Update with a single QEC cycle result + syndrome info.</summary>
        public void ShowCycleResult(QECResult result, string codeName, int physicalQubits)
        {
            _container.gameObject.SetActive(true);

            // Syndrome bits
            string syndromeBin = System.Convert.ToString(result.Syndrome, 2).PadLeft(4, '0');
            _syndromeText.text = $"Syndrome:\n  {syndromeBin}\n  (dec {result.Syndrome})";

            // Corrections
            if (result.Corrections != null && result.Corrections.Count > 0)
            {
                var sb = new System.Text.StringBuilder("Corrections:\n");
                foreach (var (qubit, pauli) in result.Corrections)
                    sb.AppendLine($"  {pauli} on q{qubit}");
                _correctionText.text = sb.ToString();
            }
            else
            {
                _correctionText.text = "No errors\ndetected";
            }
        }

        public void Hide()
        {
            _container.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Smooth bar animation
            _currentUnprot = Mathf.Lerp(_currentUnprot, _targetUnprot, Time.deltaTime * 4f);
            _currentProt = Mathf.Lerp(_currentProt, _targetProt, Time.deltaTime * 4f);

            if (_unprotectedBar != null)
            {
                var rt = _unprotectedBar.rectTransform;
                rt.anchorMax = new Vector2(rt.anchorMax.x, 0.25f + _currentUnprot * 0.52f);
            }
            if (_protectedBar != null)
            {
                var rt = _protectedBar.rectTransform;
                rt.anchorMax = new Vector2(rt.anchorMax.x, 0.25f + _currentProt * 0.52f);
            }

            if (_unprotectedLabel != null)
                _unprotectedLabel.text = $"Noisy\n{_currentUnprot:F3}";
            if (_protectedLabel != null)
                _protectedLabel.text = $"QEC\n{_currentProt:F3}";
        }

        private Text CreateText(string name, RectTransform parent, string content,
            int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax, TextAnchor align)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = align;
            return txt;
        }
    }
}
