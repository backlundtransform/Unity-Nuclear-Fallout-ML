using UnityEngine;
using UnityEngine.UI;
using CSharpNumerics.Engines.Quantum;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders the full density matrix ρ = |ψ⟩⟨ψ| as a colour-coded heatmap
    /// on a UI RawImage. Magnitude → brightness, phase → hue.
    /// For n qubits the matrix is 2^n × 2^n.
    /// </summary>
    public class DensityMatrixHeatmap : MonoBehaviour
    {
        private RectTransform _container;
        private RawImage _heatmapImage;
        private Text _titleText;
        private Texture2D _tex;
        private int _dim;

        private static readonly Color PanelBg = new Color(0.03f, 0.04f, 0.10f, 0.92f);

        public void Initialise(RectTransform parent)
        {
            var go = new GameObject("DensityMatrixHeatmap");
            _container = go.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.02f, 0.02f);
            _container.anchorMax = new Vector2(0.35f, 0.40f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = PanelBg;

            // Title
            var titleGo = new GameObject("Title");
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.SetParent(_container, false);
            titleRt.anchorMin = new Vector2(0.02f, 0.88f);
            titleRt.anchorMax = new Vector2(0.98f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _titleText = titleGo.AddComponent<Text>();
            _titleText.text = "Density Matrix  ρ = |ψ⟩⟨ψ|";
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            _titleText.fontSize = 12;
            _titleText.color = new Color(0f, 0.9f, 1f);
            _titleText.alignment = TextAnchor.MiddleCenter;

            // Heatmap image
            var imgGo = new GameObject("Heatmap");
            var imgRt = imgGo.AddComponent<RectTransform>();
            imgRt.SetParent(_container, false);
            imgRt.anchorMin = new Vector2(0.05f, 0.05f);
            imgRt.anchorMax = new Vector2(0.95f, 0.86f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;
            _heatmapImage = imgGo.AddComponent<RawImage>();

            _container.gameObject.SetActive(false);
        }

        public void Show() => _container.gameObject.SetActive(true);
        public void Hide() => _container.gameObject.SetActive(false);

        /// <summary>
        /// Compute ρ = |ψ⟩⟨ψ| from the quantum state and render as heatmap.
        /// </summary>
        public void UpdateState(QuantumState state)
        {
            var amps = state.Amplitudes;
            _dim = amps.Length; // 2^n

            if (_tex == null || _tex.width != _dim)
            {
                _tex = new Texture2D(_dim, _dim, TextureFormat.RGBA32, false)
                {
                    filterMode = _dim <= 8 ? FilterMode.Point : FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _heatmapImage.texture = _tex;
            }

            // Compute ρ[i,j] = ψ_i · ψ_j* and render
            var pixels = new Color[_dim * _dim];
            double maxMag = 0;

            // First pass: find max magnitude for normalization
            for (int i = 0; i < _dim; i++)
            {
                for (int j = 0; j < _dim; j++)
                {
                    var ai = amps[i];
                    var aj = amps[j];
                    double re = ai.realPart * aj.realPart + ai.imaginaryPart * aj.imaginaryPart;
                    double im = ai.imaginaryPart * aj.realPart - ai.realPart * aj.imaginaryPart;
                    double mag = System.Math.Sqrt(re * re + im * im);
                    if (mag > maxMag) maxMag = mag;
                }
            }

            if (maxMag < 1e-12) maxMag = 1;

            // Second pass: render
            for (int i = 0; i < _dim; i++)
            {
                for (int j = 0; j < _dim; j++)
                {
                    var ai = amps[i];
                    var aj = amps[j];
                    double re = ai.realPart * aj.realPart + ai.imaginaryPart * aj.imaginaryPart;
                    double im = ai.imaginaryPart * aj.realPart - ai.realPart * aj.imaginaryPart;
                    double mag = System.Math.Sqrt(re * re + im * im) / maxMag;
                    double phase = System.Math.Atan2(im, re); // [-π, π]

                    // Map phase to hue [0, 1], magnitude to value
                    float hue = (float)((phase + System.Math.PI) / (2.0 * System.Math.PI));
                    float sat = 0.8f;
                    float val = (float)mag;

                    // Flip Y so row 0 is at top
                    pixels[(_dim - 1 - i) * _dim + j] = Color.HSVToRGB(hue, sat, val);
                }
            }

            _tex.SetPixels(pixels);
            _tex.Apply();

            _titleText.text = $"Density Matrix  ρ  ({_dim}×{_dim})";
        }
    }
}
