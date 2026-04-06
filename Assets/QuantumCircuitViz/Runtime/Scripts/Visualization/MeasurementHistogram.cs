using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Numerics.Objects;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders a measurement probability histogram as UI bars on a Canvas.
    /// Each computational basis state (|00⟩, |01⟩, …) gets a coloured bar.
    /// Supports animated measurement sampling — bars grow shot-by-shot.
    /// </summary>
    public class MeasurementHistogram : MonoBehaviour
    {
        private RectTransform _container;
        private int _qubitCount;
        private Image[] _bars;
        private Text[] _labels;
        private Text _titleText;
        private Text _shotCountText;
        private int _stateCount;
        private Coroutine _samplingCoroutine;
        private int[] _shotCounts;

        private static readonly Color BarBaseColor = new Color(0f, 0.9f, 1f, 0.9f);
        private static readonly Color BarHighColor = new Color(1f, 0.85f, 0.25f, 0.95f);

        public void Initialise(RectTransform parent, int qubitCount)
        {
            _qubitCount = qubitCount;
            _stateCount = 1 << qubitCount;

            // Container
            gameObject.name = "Histogram";
            _container = gameObject.GetComponent<RectTransform>();
            if (_container == null)
                _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.60f, 0.05f);
            _container.anchorMax = new Vector2(0.98f, 0.35f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            // Background
            var bg = gameObject.GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
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
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            _titleText.fontSize = 12;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = new Color(0.8f, 0.9f, 1f);

            // Shot counter
            var shotGo = new GameObject("ShotCount");
            var shotRt = shotGo.AddComponent<RectTransform>();
            shotRt.SetParent(_container, false);
            shotRt.anchorMin = new Vector2(0.6f, 0.88f);
            shotRt.anchorMax = new Vector2(1f, 0.98f);
            shotRt.offsetMin = Vector2.zero;
            shotRt.offsetMax = Vector2.zero;
            _shotCountText = shotGo.AddComponent<Text>();
            _shotCountText.text = "";
            _shotCountText.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            _shotCountText.fontSize = 11;
            _shotCountText.alignment = TextAnchor.MiddleRight;
            _shotCountText.color = new Color(0.5f, 0.6f, 0.7f);

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

        public void Rebuild(int qubitCount)
        {
            _qubitCount = qubitCount;
            _stateCount = 1 << qubitCount;

            if (_samplingCoroutine != null)
            {
                StopCoroutine(_samplingCoroutine);
                _samplingCoroutine = null;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            Initialise(_container.parent as RectTransform, _qubitCount);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
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

        /// <summary>
        /// Animate measurement: perform <paramref name="totalShots"/> samples from the
        /// probability distribution, accumulating bars shot-by-shot.
        /// </summary>
        public void AnimateSampling(VectorN probs, int totalShots = 256, float shotsPerSecond = 120f)
        {
            if (_samplingCoroutine != null)
                StopCoroutine(_samplingCoroutine);

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                UpdateProbabilities(probs);
                if (_shotCountText != null)
                    _shotCountText.text = "";
                _samplingCoroutine = null;
                return;
            }

            _samplingCoroutine = StartCoroutine(SamplingRoutine(probs, totalShots, shotsPerSecond));
        }

        /// <summary>Stop any running sampling animation and show analytical probabilities.</summary>
        public void StopSampling()
        {
            if (_samplingCoroutine != null)
            {
                StopCoroutine(_samplingCoroutine);
                _samplingCoroutine = null;
            }
            if (_shotCountText != null) _shotCountText.text = "";
        }

        private IEnumerator SamplingRoutine(VectorN probs, int totalShots, float shotsPerSecond)
        {
            _shotCounts = new int[_stateCount];
            float interval = 1f / Mathf.Max(shotsPerSecond, 1f);

            // Build CDF for weighted sampling
            float[] cdf = new float[_stateCount];
            cdf[0] = (float)probs[0];
            for (int i = 1; i < _stateCount; i++)
                cdf[i] = cdf[i - 1] + (float)probs[i];

            for (int shot = 0; shot < totalShots; shot++)
            {
                // Sample one outcome
                float r = Random.value;
                int outcome = 0;
                for (int i = 0; i < _stateCount; i++)
                {
                    if (r <= cdf[i]) { outcome = i; break; }
                    outcome = i;
                }
                _shotCounts[outcome]++;

                // Update bars from empirical frequencies
                int totalSoFar = shot + 1;
                UpdateBarsFromCounts(totalSoFar);

                if (_shotCountText != null)
                    _shotCountText.text = $"shots: {totalSoFar}/{totalShots}";

                // Batch shots to avoid excessive yields — show every N-th frame
                if (shot < 32 || shot % Mathf.Max(1, (int)(shotsPerSecond * Time.deltaTime)) == 0)
                    yield return new WaitForSeconds(interval);
            }

            // Final update
            UpdateBarsFromCounts(totalShots);
            if (_shotCountText != null)
                _shotCountText.text = $"shots: {totalShots}";
            _samplingCoroutine = null;
        }

        private void UpdateBarsFromCounts(int totalShots)
        {
            if (_bars == null) return;
            for (int i = 0; i < _stateCount; i++)
            {
                float p = (float)_shotCounts[i] / totalShots;
                var rt = _bars[i].rectTransform;
                float x0 = rt.anchorMin.x;
                float x1 = rt.anchorMax.x;
                rt.anchorMin = new Vector2(x0, 0.12f);
                rt.anchorMax = new Vector2(x1, 0.12f + p * 0.74f);
                _bars[i].color = Color.Lerp(BarBaseColor, BarHighColor, p);
            }
        }
    }
}
