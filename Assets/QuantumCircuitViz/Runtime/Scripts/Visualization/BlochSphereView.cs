using UnityEngine;
using UnityEngine.UI;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Numerics.Objects;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Bloch Sphere view — shows one qubit at a time.
    ///
    /// Features:
    ///  • Qubit selector buttons
    ///  • Centered 3D Bloch sphere (reuses BlochSphereRenderer)
    ///  • Entanglement detection: if the qubit is entangled/mixed,
    ///    displays "Bloch sphere is not fully representative because
    ///    this qubit is entangled."
    ///  • State info: θ, φ, Bloch vector components, purity
    ///
    /// Best for: single-qubit gates, superposition, rotations, phase intuition.
    /// Less useful for: multi-qubit entanglement (use State view instead).
    /// </summary>
    public class BlochSphereView : MonoBehaviour
    {
        // ── UI ───────────────────────────────────────────────────
        private RectTransform _overlayPanel;
        private Button[] _qubitButtons;
        private Image[] _qubitButtonBgs;
        private Text _stateInfoText;
        private Text _entanglementWarning;
        private Text _titleText;

        // ── Sphere ───────────────────────────────────────────────
        private BlochSphereRenderer _sphere;
        private int _selectedQubit;
        private int _qubitCount;

        // ── Cached state ─────────────────────────────────────────
        private Vector3[] _blochVectors;
        private float[] _purities;

        // ── Colors ───────────────────────────────────────────────
        private static readonly Color ActiveBg   = new Color(0.00f, 0.45f, 0.65f, 0.95f);
        private static readonly Color InactiveBg = new Color(0.10f, 0.10f, 0.18f, 0.90f);
        private static readonly Color WarningCol = new Color(1f, 0.7f, 0.2f, 0.95f);

        public int SelectedQubit => _selectedQubit;

        // ──────────────────────────────────────────────────────────
        // Initialise
        // ──────────────────────────────────────────────────────────
        public void Initialise(RectTransform canvasParent, int qubitCount, float sphereRadius, int wireframeSegments, float animSpeed)
        {
            _qubitCount = qubitCount;
            gameObject.name = "BlochView";

            // Create the 3D sphere
            var sphereGo = new GameObject("BlochSphere_Main");
            sphereGo.transform.position = new Vector3(0, 0.5f, 0);
            _sphere = sphereGo.AddComponent<BlochSphereRenderer>();
            _sphere.Initialise(sphereRadius, wireframeSegments, animSpeed);
            _sphere.SetStateImmediate(Vector3.up);
            sphereGo.SetActive(false); // hidden until this view is shown

            // UI overlay on canvas
            var overlayGo = UIGo("BlochOverlay");
            _overlayPanel = overlayGo.GetComponent<RectTransform>();
            _overlayPanel.SetParent(canvasParent, false);
            _overlayPanel.anchorMin = Vector2.zero;
            _overlayPanel.anchorMax = Vector2.one;
            _overlayPanel.offsetMin = _overlayPanel.offsetMax = Vector2.zero;
            // No background image — let the 3D sphere show through

            BuildQubitSelector();
            BuildStateInfo();
            BuildEntanglementWarning();
            BuildTitle();

            SelectQubit(0);
        }

        public void Rebuild(int qubitCount, float sphereRadius, int wireframeSegments, float animSpeed)
        {
            _qubitCount = qubitCount;

            // Rebuild sphere
            if (_sphere != null) Destroy(_sphere.gameObject);
            var sphereGo = new GameObject("BlochSphere_Main");
            sphereGo.transform.position = new Vector3(0, 0.5f, 0);
            _sphere = sphereGo.AddComponent<BlochSphereRenderer>();
            _sphere.Initialise(sphereRadius, wireframeSegments, animSpeed);
            _sphere.SetStateImmediate(Vector3.up);

            // Rebuild qubit selector
            if (_qubitButtons != null)
                foreach (var b in _qubitButtons)
                    if (b != null) Destroy(b.gameObject);
            BuildQubitSelector();

            bool showing = gameObject.activeSelf;
            _sphere.gameObject.SetActive(showing);
            if (_selectedQubit >= _qubitCount) _selectedQubit = 0;
            SelectQubit(_selectedQubit);
        }

        // ──────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────
        public void UpdateState(QuantumState state)
        {
            int n = state.QubitCount;
            _blochVectors = new Vector3[n];
            _purities = new float[n];

            for (int q = 0; q < n; q++)
            {
                var bloch = ComputeReducedBloch(state, q);
                _blochVectors[q] = bloch;
                // Purity = (1 + |b|²)/2.  Pure qubit: purity=1. Mixed/entangled: < 1.
                _purities[q] = (1f + bloch.sqrMagnitude) / 2f;
            }

            RefreshDisplay();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _overlayPanel.gameObject.SetActive(true);
            if (_sphere != null) _sphere.gameObject.SetActive(true);
            RefreshDisplay();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _overlayPanel.gameObject.SetActive(false);
            if (_sphere != null) _sphere.gameObject.SetActive(false);
        }

        public void SelectQubit(int q)
        {
            _selectedQubit = Mathf.Clamp(q, 0, _qubitCount - 1);
            for (int i = 0; i < _qubitButtons.Length; i++)
                _qubitButtonBgs[i].color = i == _selectedQubit ? ActiveBg : InactiveBg;
            RefreshDisplay();
        }

        // ──────────────────────────────────────────────────────────
        // Build UI
        // ──────────────────────────────────────────────────────────
        private void BuildQubitSelector()
        {
            _qubitButtons = new Button[_qubitCount];
            _qubitButtonBgs = new Image[_qubitCount];

            float btnW = Mathf.Min(0.08f, 0.6f / _qubitCount);
            float startX = 0.5f - (_qubitCount * btnW) / 2f;

            for (int i = 0; i < _qubitCount; i++)
            {
                var go = UIGo($"QBtn_{i}");
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(_overlayPanel, false);
                rt.anchorMin = new Vector2(startX + i * btnW, 0.93f);
                rt.anchorMax = new Vector2(startX + (i + 1) * btnW - 0.005f, 0.99f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;

                _qubitButtonBgs[i] = go.AddComponent<Image>();
                _qubitButtons[i] = go.AddComponent<Button>();
                int idx = i;
                _qubitButtons[i].onClick.AddListener(() => SelectQubit(idx));

                var txtGo = UIGo("Text");
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.SetParent(rt, false);
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
                var txt = txtGo.AddComponent<Text>();
                txt.text = $"q{i}";
                txt.font = Font.CreateDynamicFontFromOSFont("Consolas", 13);
                txt.fontSize = 13;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
            }
        }

        private void BuildTitle()
        {
            var go = UIGo("BlochTitle");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_overlayPanel, false);
            rt.anchorMin = new Vector2(0.02f, 0.86f);
            rt.anchorMax = new Vector2(0.40f, 0.93f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _titleText = go.AddComponent<Text>();
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 13);
            _titleText.fontSize = 13;
            _titleText.color = new Color(0.5f, 0.7f, 0.9f);
            _titleText.alignment = TextAnchor.MiddleLeft;
            _titleText.text = "Bloch Sphere — Local qubit state";

            // Subtitle
            var subGo = UIGo("BlochSubtitle");
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.SetParent(_overlayPanel, false);
            subRt.anchorMin = new Vector2(0.02f, 0.81f);
            subRt.anchorMax = new Vector2(0.98f, 0.86f);
            subRt.offsetMin = subRt.offsetMax = Vector2.zero;
            var subTxt = subGo.AddComponent<Text>();
            subTxt.font = Font.CreateDynamicFontFromOSFont("Consolas", 10);
            subTxt.fontSize = 10;
            subTxt.color = new Color(0.35f, 0.42f, 0.5f);
            subTxt.text = "Best for understanding single-qubit gates (H, X, Y, Z, Rx, Ry, Rz), superposition, and rotations.";
        }

        private void BuildStateInfo()
        {
            var go = UIGo("StateInfo");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_overlayPanel, false);
            rt.anchorMin = new Vector2(0.02f, 0.02f);
            rt.anchorMax = new Vector2(0.50f, 0.18f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.10f, 0.85f);

            var txtGo = UIGo("StateInfoText");
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.SetParent(rt, false);
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
            _stateInfoText = txtGo.AddComponent<Text>();
            _stateInfoText.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            _stateInfoText.fontSize = 11;
            _stateInfoText.color = new Color(0.7f, 0.85f, 1f);
            _stateInfoText.alignment = TextAnchor.UpperLeft;
        }

        private void BuildEntanglementWarning()
        {
            var go = UIGo("EntanglementWarning");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_overlayPanel, false);
            rt.anchorMin = new Vector2(0.15f, 0.19f);
            rt.anchorMax = new Vector2(0.85f, 0.26f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.25f, 0.15f, 0.02f, 0.90f);

            var txtGo = UIGo("WarningText");
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.SetParent(rt, false);
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
            _entanglementWarning = txtGo.AddComponent<Text>();
            _entanglementWarning.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            _entanglementWarning.fontSize = 11;
            _entanglementWarning.color = WarningCol;
            _entanglementWarning.alignment = TextAnchor.MiddleCenter;
            _entanglementWarning.text = "⚠ Bloch sphere is not fully representative — this qubit is entangled.";
            go.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────
        // Refresh
        // ──────────────────────────────────────────────────────────
        private void RefreshDisplay()
        {
            if (_blochVectors == null || _selectedQubit >= _blochVectors.Length) return;

            var b = _blochVectors[_selectedQubit];
            _sphere?.SetState(b);

            // State info
            float bx = b.x, by = b.z, bz = b.y; // Unity Y→Bloch Z mapping
            float theta = Mathf.Acos(Mathf.Clamp(bz, -1f, 1f));
            float phi = Mathf.Atan2(by, bx);
            float purity = _purities != null ? _purities[_selectedQubit] : 1f;
            float blochLen = b.magnitude;

            _stateInfoText.text =
                $"  Qubit q{_selectedQubit}    |b| = {blochLen:F3}\n" +
                $"  θ = {theta:F3} rad ({theta * Mathf.Rad2Deg:F1}°)\n" +
                $"  φ = {phi:F3} rad ({phi * Mathf.Rad2Deg:F1}°)\n" +
                $"  Purity Tr(ρ²) = {purity:F4}";

            // Entanglement warning: if purity significantly less than 1
            bool isEntangled = purity < 0.98f;
            _entanglementWarning.transform.parent.gameObject.SetActive(isEntangled);
        }

        // ──────────────────────────────────────────────────────────
        // Reduced Bloch vector from full state
        // ──────────────────────────────────────────────────────────
        private Vector3 ComputeReducedBloch(QuantumState state, int qubit)
        {
            int n = state.QubitCount;

            // Single-qubit: use exact Bloch vector
            if (n == 1)
            {
                var bv = state.GetBlochVector();
                return new Vector3((float)bv.X, (float)bv.Z, (float)bv.Y);
            }

            int dim = 1 << n;
            var amps = state.Amplitudes;

            double rho00_r = 0, rho01_r = 0, rho01_i = 0, rho11_r = 0;

            for (int i = 0; i < dim; i++)
            {
                int qBit_i = (i >> qubit) & 1;
                for (int j = 0; j < dim; j++)
                {
                    int qBit_j = (j >> qubit) & 1;
                    int mask = ~(1 << qubit) & ((1 << n) - 1);
                    if ((i & mask) != (j & mask)) continue;

                    var ai = amps[i];
                    var aj = amps[j];
                    double re = ai.realPart * aj.realPart + ai.imaginaryPart * aj.imaginaryPart;
                    double im = ai.imaginaryPart * aj.realPart - ai.realPart * aj.imaginaryPart;

                    if (qBit_i == 0 && qBit_j == 0) rho00_r += re;
                    else if (qBit_i == 0 && qBit_j == 1) { rho01_r += re; rho01_i += im; }
                    else if (qBit_i == 1 && qBit_j == 1) rho11_r += re;
                }
            }

            float bxVal = (float)(2.0 * rho01_r);
            float byVal = (float)(2.0 * rho01_i);
            float bzVal = (float)(rho00_r - rho11_r);

            return new Vector3(bxVal, bzVal, byVal);
        }

        private void OnDestroy()
        {
            if (_sphere != null) Destroy(_sphere.gameObject);
        }

        private static GameObject UIGo(string name) => new GameObject(name, typeof(RectTransform));
    }
}
