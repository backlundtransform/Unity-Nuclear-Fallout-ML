using UnityEngine;
using UnityEngine.UI;
using CSharpNumerics.Numerics.Objects;
using CSharpNumerics.Engines.Quantum;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// State / Probability view with two sub-modes:
    ///   1. Probability bar chart — |α|² for each basis state, color-coded by phase.
    ///   2. Complex plane (Argand) — each amplitude plotted as a vector in ℂ.
    ///
    /// This is the most important pedagogical view: it shows both
    /// probabilities AND phases, which is where most quantum visualisations
    /// become too shallow.
    /// </summary>
    public class StateView : MonoBehaviour
    {
        private enum SubMode { Probability, Argand }

        // ── Layout ───────────────────────────────────────────────
        private RectTransform _container;
        private Button _probBtn, _argandBtn;
        private Image _probBtnBg, _argandBtnBg;
        private SubMode _mode = SubMode.Probability;

        // ── Probability mode ─────────────────────────────────────
        private RectTransform _probPanel;
        private Image[] _bars;
        private Text[] _basisLabels;
        private Text[] _valueLabels;
        private Image[] _phaseIndicators;
        private int _stateCount;
        private int _qubitCount;

        // ── Argand mode ──────────────────────────────────────────
        private RectTransform _argandPanel;
        private RawImage _argandImage;
        private Texture2D _argandTexture;
        private const int TexSize = 480;

        // ── Phase legend ─────────────────────────────────────────
        private RawImage _legendImage;

        // ── State data cache ─────────────────────────────────────
        private ComplexVectorN _amplitudes;
        private VectorN _probabilities;
        private bool _hasState;

        // ── Colors ───────────────────────────────────────────────
        private static readonly Color ActiveTab   = new Color(0.00f, 0.45f, 0.65f, 0.95f);
        private static readonly Color InactiveTab = new Color(0.10f, 0.10f, 0.15f, 0.90f);
        private static readonly Color PanelBg     = new Color(0.04f, 0.04f, 0.10f, 0.92f);

        // ── Public API ───────────────────────────────────────────
        public void Initialise(RectTransform parent, int qubitCount)
        {
            _qubitCount = qubitCount;
            _stateCount = 1 << qubitCount;

            gameObject.name = "StateView";
            _container = GetComponent<RectTransform>();
            if (_container == null) _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = Vector2.zero;
            _container.anchorMax = Vector2.one;
            _container.offsetMin = _container.offsetMax = Vector2.zero;

            BuildModeToggle();
            BuildProbabilityPanel();
            BuildArgandPanel();
            BuildPhaseLegend();
            SwitchMode(SubMode.Probability);
        }

        public void Rebuild(int qubitCount)
        {
            _qubitCount = qubitCount;
            _stateCount = 1 << qubitCount;
            _hasState = false;

            // Destroy old panels
            if (_probPanel != null) Destroy(_probPanel.gameObject);
            if (_argandPanel != null) Destroy(_argandPanel.gameObject);
            if (_legendImage != null) Destroy(_legendImage.gameObject);

            BuildProbabilityPanel();
            BuildArgandPanel();
            BuildPhaseLegend();
            SwitchMode(_mode);
        }

        public void UpdateState(QuantumState state)
        {
            _amplitudes = state.Amplitudes;
            _probabilities = state.GetProbabilities();
            _hasState = true;

            if (_mode == SubMode.Probability)
                RenderProbabilityBars();
            else
                RenderArgand();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (_hasState && _amplitudes.Length > 0)
            {
                if (_mode == SubMode.Probability) RenderProbabilityBars();
                else RenderArgand();
            }
        }

        public void Hide() => gameObject.SetActive(false);

        // ──────────────────────────────────────────────────────────
        // Mode toggle
        // ──────────────────────────────────────────────────────────
        private void BuildModeToggle()
        {
            // Probability button
            var probGo = UIGo("ProbBtn");
            var probRt = probGo.GetComponent<RectTransform>();
            probRt.SetParent(_container, false);
            probRt.anchorMin = new Vector2(0.30f, 0.94f);
            probRt.anchorMax = new Vector2(0.48f, 0.99f);
            probRt.offsetMin = probRt.offsetMax = Vector2.zero;
            _probBtnBg = probGo.AddComponent<Image>();
            _probBtn = probGo.AddComponent<Button>();
            _probBtn.onClick.AddListener(() => SwitchMode(SubMode.Probability));
            CreateChildText(probRt, "Probability", 12, Color.white, TextAnchor.MiddleCenter);

            // Argand button
            var argGo = UIGo("ArgandBtn");
            var argRt = argGo.GetComponent<RectTransform>();
            argRt.SetParent(_container, false);
            argRt.anchorMin = new Vector2(0.52f, 0.94f);
            argRt.anchorMax = new Vector2(0.70f, 0.99f);
            argRt.offsetMin = argRt.offsetMax = Vector2.zero;
            _argandBtnBg = argGo.AddComponent<Image>();
            _argandBtn = argGo.AddComponent<Button>();
            _argandBtn.onClick.AddListener(() => SwitchMode(SubMode.Argand));
            CreateChildText(argRt, "Complex Plane", 12, Color.white, TextAnchor.MiddleCenter);
        }

        private void SwitchMode(SubMode mode)
        {
            _mode = mode;
            _probBtnBg.color  = mode == SubMode.Probability ? ActiveTab : InactiveTab;
            _argandBtnBg.color = mode == SubMode.Argand     ? ActiveTab : InactiveTab;

            _probPanel.gameObject.SetActive(mode == SubMode.Probability);
            _argandPanel.gameObject.SetActive(mode == SubMode.Argand);

            if (_hasState && _amplitudes.Length > 0)
            {
                if (mode == SubMode.Probability) RenderProbabilityBars();
                else RenderArgand();
            }
        }

        // ──────────────────────────────────────────────────────────
        // Probability bar chart
        // ──────────────────────────────────────────────────────────
        private void BuildProbabilityPanel()
        {
            var go = UIGo("ProbPanel");
            _probPanel = go.GetComponent<RectTransform>();
            _probPanel.SetParent(_container, false);
            _probPanel.anchorMin = new Vector2(0.02f, 0.02f);
            _probPanel.anchorMax = new Vector2(0.98f, 0.93f);
            _probPanel.offsetMin = _probPanel.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = PanelBg;

            // Title
            CreateText(_probPanel, "ProbTitle", "Amplitude / Probability",
                new Vector2(0.02f, 0.93f), new Vector2(0.50f, 0.99f), 13,
                new Color(0.6f, 0.8f, 1f));

            _bars = new Image[_stateCount];
            _basisLabels = new Text[_stateCount];
            _valueLabels = new Text[_stateCount];
            _phaseIndicators = new Image[_stateCount];

            float rowH = 0.90f / Mathf.Max(_stateCount, 1);
            float topY = 0.92f;

            for (int i = 0; i < _stateCount; i++)
            {
                float yTop = topY - i * rowH;
                float yBot = yTop - rowH * 0.75f;

                // Basis label |000⟩
                string basis = "|" + System.Convert.ToString(i, 2).PadLeft(_qubitCount, '0') + "⟩";
                _basisLabels[i] = CreateText(_probPanel, $"Basis_{i}", basis,
                    new Vector2(0.02f, yBot), new Vector2(0.10f, yTop), 12,
                    new Color(0.7f, 0.8f, 0.9f)).GetComponent<Text>();

                // Probability bar
                var barGo = UIGo($"Bar_{i}");
                var barRt = barGo.GetComponent<RectTransform>();
                barRt.SetParent(_probPanel, false);
                barRt.anchorMin = new Vector2(0.11f, yBot);
                barRt.anchorMax = new Vector2(0.11f, yTop); // width set in render
                barRt.offsetMin = barRt.offsetMax = Vector2.zero;
                _bars[i] = barGo.AddComponent<Image>();
                _bars[i].color = PhaseColor(0f);

                // Phase indicator (small colored square)
                var phGo = UIGo($"Phase_{i}");
                var phRt = phGo.GetComponent<RectTransform>();
                phRt.SetParent(_probPanel, false);
                phRt.anchorMin = new Vector2(0.88f, yBot);
                phRt.anchorMax = new Vector2(0.90f, yTop);
                phRt.offsetMin = phRt.offsetMax = Vector2.zero;
                _phaseIndicators[i] = phGo.AddComponent<Image>();

                // Value label
                _valueLabels[i] = CreateText(_probPanel, $"Val_{i}", "0.000",
                    new Vector2(0.91f, yBot), new Vector2(0.99f, yTop), 11,
                    new Color(0.6f, 0.7f, 0.8f)).GetComponent<Text>();
            }
        }

        private void RenderProbabilityBars()
        {
            if (!_hasState || _bars == null || _amplitudes.Length == 0) return;

            for (int i = 0; i < _stateCount && i < _amplitudes.Length; i++)
            {
                var amp = _amplitudes[i];
                float prob = (float)_probabilities[i];
                float phase = (float)amp.GetArgument();

                // Bar width proportional to probability
                float barEnd = 0.11f + prob * 0.75f;
                var rt = _bars[i].rectTransform;
                rt.anchorMin = new Vector2(0.11f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(barEnd, rt.anchorMax.y);
                _bars[i].color = PhaseColor(phase);

                // Phase indicator
                _phaseIndicators[i].color = PhaseColor(phase);

                // Value text
                string phaseStr = prob > 0.001f ? $"  φ={phase * Mathf.Rad2Deg:F0}°" : "";
                _valueLabels[i].text = $"{prob:F3}{phaseStr}";
            }
        }

        // ──────────────────────────────────────────────────────────
        // Argand / Complex Plane
        // ──────────────────────────────────────────────────────────
        private void BuildArgandPanel()
        {
            var go = UIGo("ArgandPanel");
            _argandPanel = go.GetComponent<RectTransform>();
            _argandPanel.SetParent(_container, false);
            _argandPanel.anchorMin = new Vector2(0.02f, 0.02f);
            _argandPanel.anchorMax = new Vector2(0.98f, 0.93f);
            _argandPanel.offsetMin = _argandPanel.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = PanelBg;

            // Title
            CreateText(_argandPanel, "ArgTitle", "Complex Plane (Argand Diagram)",
                new Vector2(0.02f, 0.93f), new Vector2(0.60f, 0.99f), 13,
                new Color(0.6f, 0.8f, 1f));

            // Explanation
            CreateText(_argandPanel, "ArgDesc",
                "Each vector shows a basis state amplitude. Length = |α|, angle = phase φ.\n" +
                "Algorithms manipulate phases — not just probabilities.",
                new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.93f), 10,
                new Color(0.4f, 0.5f, 0.6f));

            // Texture for Argand diagram
            _argandTexture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            _argandTexture.filterMode = FilterMode.Bilinear;

            var imgGo = UIGo("ArgandImage");
            var imgRt = imgGo.GetComponent<RectTransform>();
            imgRt.SetParent(_argandPanel, false);
            imgRt.anchorMin = new Vector2(0.15f, 0.02f);
            imgRt.anchorMax = new Vector2(0.85f, 0.85f);
            imgRt.offsetMin = imgRt.offsetMax = Vector2.zero;
            _argandImage = imgGo.AddComponent<RawImage>();
            _argandImage.texture = _argandTexture;

            // Axis labels around the diagram
            CreateText(_argandPanel, "ReLabel", "Re",
                new Vector2(0.86f, 0.40f), new Vector2(0.92f, 0.46f), 11,
                new Color(0.6f, 0.6f, 0.6f));
            CreateText(_argandPanel, "ImLabel", "Im",
                new Vector2(0.48f, 0.82f), new Vector2(0.54f, 0.87f), 11,
                new Color(0.6f, 0.6f, 0.6f));
            CreateText(_argandPanel, "OneLabel", "1",
                new Vector2(0.82f, 0.43f), new Vector2(0.86f, 0.48f), 10,
                new Color(0.4f, 0.4f, 0.4f));
            CreateText(_argandPanel, "NegOneLabel", "−1",
                new Vector2(0.14f, 0.43f), new Vector2(0.19f, 0.48f), 10,
                new Color(0.4f, 0.4f, 0.4f));
            CreateText(_argandPanel, "iLabel", "i",
                new Vector2(0.50f, 0.81f), new Vector2(0.54f, 0.85f), 10,
                new Color(0.4f, 0.4f, 0.4f));
            CreateText(_argandPanel, "NiLabel", "−i",
                new Vector2(0.49f, 0.02f), new Vector2(0.54f, 0.06f), 10,
                new Color(0.4f, 0.4f, 0.4f));

            // Legend for basis state labels
            _argandPanel.gameObject.SetActive(false);
        }

        private void RenderArgand()
        {
            if (!_hasState || _argandTexture == null || _amplitudes.Length == 0) return;

            int sz = TexSize;
            var pixels = new Color32[sz * sz];

            // Background
            var bgColor = new Color32(10, 10, 26, 240);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bgColor;

            int cx = sz / 2, cy = sz / 2;
            int unitR = (int)(sz * 0.40f); // unit circle radius in pixels

            // Grid at 0.5 intervals
            var gridCol = new Color32(30, 35, 50, 120);
            DrawCircle(pixels, sz, cx, cy, unitR / 2, gridCol);
            DrawCircle(pixels, sz, cx, cy, unitR, gridCol);

            // Axes
            var axisCol = new Color32(50, 60, 80, 160);
            DrawLine(pixels, sz, 0, cy, sz - 1, cy, axisCol);    // Re axis
            DrawLine(pixels, sz, cx, 0, cx, sz - 1, axisCol);    // Im axis

            // Unit circle
            var circCol = new Color32(80, 100, 130, 200);
            DrawCircle(pixels, sz, cx, cy, unitR, circCol);

            // Amplitude vectors
            for (int i = 0; i < _stateCount && i < _amplitudes.Length; i++)
            {
                var amp = _amplitudes[i];
                float re = (float)amp.realPart;
                float im = (float)amp.imaginaryPart;
                float mag = Mathf.Sqrt(re * re + im * im);

                if (mag < 0.005f) continue; // skip negligible amplitudes

                int px = cx + (int)(re * unitR);
                int py = cy + (int)(im * unitR);

                float phase = Mathf.Atan2(im, re);
                Color c = PhaseColor(phase);
                var c32 = (Color32)c;

                // Draw vector from center to amplitude point
                DrawLine(pixels, sz, cx, cy, px, py, c32);

                // Draw dot at endpoint
                DrawFilledCircle(pixels, sz, px, py, 4, c32);

                // Draw basis label near the dot
                // (Simple approach: draw a small colored ring for identification)
                DrawCircle(pixels, sz, px, py, 6, new Color32(255, 255, 255, 200));
            }

            _argandTexture.SetPixels32(pixels);
            _argandTexture.Apply();
        }

        // ──────────────────────────────────────────────────────────
        // Phase Legend
        // ──────────────────────────────────────────────────────────
        private void BuildPhaseLegend()
        {
            var legendTex = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            var cols = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                float phase = (i / 255f) * 2f * Mathf.PI - Mathf.PI;
                cols[i] = (Color32)PhaseColor(phase);
            }
            legendTex.SetPixels32(cols);
            legendTex.Apply();

            var go = UIGo("PhaseLegend");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_container, false);
            rt.anchorMin = new Vector2(0.15f, 0.00f);
            rt.anchorMax = new Vector2(0.85f, 0.015f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _legendImage = go.AddComponent<RawImage>();
            _legendImage.texture = legendTex;

            // Legend labels
            CreateText(_container, "PhLbl_pi", "−π", new Vector2(0.12f, 0.00f), new Vector2(0.15f, 0.02f),
                9, new Color(0.4f, 0.5f, 0.6f));
            CreateText(_container, "PhLbl_0", "0", new Vector2(0.495f, 0.00f), new Vector2(0.52f, 0.02f),
                9, new Color(0.4f, 0.5f, 0.6f));
            CreateText(_container, "PhLbl_pp", "π", new Vector2(0.85f, 0.00f), new Vector2(0.88f, 0.02f),
                9, new Color(0.4f, 0.5f, 0.6f));
        }

        // ──────────────────────────────────────────────────────────
        // Phase → Color mapping
        // ──────────────────────────────────────────────────────────
        /// <summary>
        /// Map phase angle to color using HSV wheel.
        /// 0 → cyan, π/2 → green, π → red, −π/2 → magenta.
        /// </summary>
        public static Color PhaseColor(float phase)
        {
            // Normalize to [0, 1] for hue
            float h = (phase + Mathf.PI) / (2f * Mathf.PI);
            h = ((h % 1f) + 1f) % 1f;
            // Offset so 0 phase = cyan (H≈0.5)
            h = (h + 0.5f) % 1f;
            return Color.HSVToRGB(h, 0.85f, 0.95f);
        }

        // ──────────────────────────────────────────────────────────
        // Texture drawing primitives
        // ──────────────────────────────────────────────────────────
        private static void DrawLine(Color32[] pixels, int sz, int x0, int y0, int x1, int y1, Color32 col)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            for (int i = 0; i < sz * 4; i++) // safety limit
            {
                if (x0 >= 0 && x0 < sz && y0 >= 0 && y0 < sz)
                    pixels[y0 * sz + x0] = col;
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private static void DrawCircle(Color32[] pixels, int sz, int cx, int cy, int r, Color32 col)
        {
            int x = 0, y = r, d = 3 - 2 * r;
            while (x <= y)
            {
                SetPixelSafe(pixels, sz, cx + x, cy + y, col);
                SetPixelSafe(pixels, sz, cx - x, cy + y, col);
                SetPixelSafe(pixels, sz, cx + x, cy - y, col);
                SetPixelSafe(pixels, sz, cx - x, cy - y, col);
                SetPixelSafe(pixels, sz, cx + y, cy + x, col);
                SetPixelSafe(pixels, sz, cx - y, cy + x, col);
                SetPixelSafe(pixels, sz, cx + y, cy - x, col);
                SetPixelSafe(pixels, sz, cx - y, cy - x, col);
                if (d < 0) d += 4 * x + 6;
                else { d += 4 * (x - y) + 10; y--; }
                x++;
            }
        }

        private static void DrawFilledCircle(Color32[] pixels, int sz, int cx, int cy, int r, Color32 col)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r)
                        SetPixelSafe(pixels, sz, cx + dx, cy + dy, col);
        }

        private static void SetPixelSafe(Color32[] pixels, int sz, int x, int y, Color32 col)
        {
            if (x >= 0 && x < sz && y >= 0 && y < sz)
                pixels[y * sz + x] = col;
        }

        // ──────────────────────────────────────────────────────────
        // UI Helpers
        // ──────────────────────────────────────────────────────────
        private GameObject CreateText(RectTransform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color)
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
            txt.alignment = TextAnchor.MiddleLeft;
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
