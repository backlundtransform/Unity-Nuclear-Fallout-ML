using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using QuantumCircuitViz.Core;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Real-time line graph showing RL training progress: fidelity/reward per episode.
    /// Draws directly onto a UI RawImage texture — no external graphing libs needed.
    /// </summary>
    public class RLTrainingPanel : MonoBehaviour
    {
        private RectTransform _container;
        private RawImage _graphImage;
        private Text _titleText;
        private Text _statsText;
        private Text _statusText;
        private Texture2D _graphTex;

        private readonly List<float> _fidelityHistory = new List<float>();
        private readonly List<float> _rewardHistory = new List<float>();
        private float _bestFidelity;
        private bool _dirty;

        private const int GraphWidth = 320;
        private const int GraphHeight = 160;

        private static readonly Color BgColor = new Color(0.04f, 0.04f, 0.10f);
        private static readonly Color GridColor = new Color(0.12f, 0.14f, 0.22f);
        private static readonly Color FidelityLineColor = new Color(0.2f, 0.9f, 0.4f);
        private static readonly Color RewardLineColor = new Color(0f, 0.7f, 1f);
        private static readonly Color PanelBg = new Color(0.03f, 0.04f, 0.10f, 0.92f);
        private static readonly Color AccentColor = new Color(0f, 0.9f, 1f);

        public void Initialise(RectTransform parent)
        {
            var go = new GameObject("RLTrainingPanel");
            _container = go.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.02f, 0.60f);
            _container.anchorMax = new Vector2(0.55f, 0.98f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = PanelBg;

            // Title
            var titleGo = new GameObject("Title");
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.SetParent(_container, false);
            titleRt.anchorMin = new Vector2(0.02f, 0.88f);
            titleRt.anchorMax = new Vector2(0.70f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _titleText = titleGo.AddComponent<Text>();
            _titleText.text = "RL Training — Fidelity";
            _titleText.font = Font.CreateDynamicFontFromOSFont("Consolas", 13);
            _titleText.fontSize = 13;
            _titleText.color = AccentColor;
            _titleText.alignment = TextAnchor.MiddleLeft;

            // Stats (top-right)
            var statsGo = new GameObject("Stats");
            var statsRt = statsGo.AddComponent<RectTransform>();
            statsRt.SetParent(_container, false);
            statsRt.anchorMin = new Vector2(0.70f, 0.88f);
            statsRt.anchorMax = new Vector2(0.98f, 0.98f);
            statsRt.offsetMin = Vector2.zero;
            statsRt.offsetMax = Vector2.zero;
            _statsText = statsGo.AddComponent<Text>();
            _statsText.text = "";
            _statsText.font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            _statsText.fontSize = 11;
            _statsText.color = new Color(0.6f, 0.8f, 0.6f);
            _statsText.alignment = TextAnchor.MiddleRight;

            // Graph texture
            _graphTex = new Texture2D(GraphWidth, GraphHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            ClearGraph();

            var imgGo = new GameObject("Graph");
            var imgRt = imgGo.AddComponent<RectTransform>();
            imgRt.SetParent(_container, false);
            imgRt.anchorMin = new Vector2(0.04f, 0.12f);
            imgRt.anchorMax = new Vector2(0.96f, 0.86f);
            imgRt.offsetMin = Vector2.zero;
            imgRt.offsetMax = Vector2.zero;
            _graphImage = imgGo.AddComponent<RawImage>();
            _graphImage.texture = _graphTex;

            // Status (bottom)
            var statusGo = new GameObject("Status");
            var statusRt = statusGo.AddComponent<RectTransform>();
            statusRt.SetParent(_container, false);
            statusRt.anchorMin = new Vector2(0.02f, 0.01f);
            statusRt.anchorMax = new Vector2(0.98f, 0.11f);
            statusRt.offsetMin = Vector2.zero;
            statusRt.offsetMax = Vector2.zero;
            _statusText = statusGo.AddComponent<Text>();
            _statusText.text = "Press E to start RL training";
            _statusText.font = Font.CreateDynamicFontFromOSFont("Consolas", 10);
            _statusText.fontSize = 10;
            _statusText.color = new Color(0.5f, 0.6f, 0.7f);
            _statusText.alignment = TextAnchor.MiddleCenter;

            _container.gameObject.SetActive(false);
        }

        public void Show() => _container.gameObject.SetActive(true);
        public void Hide() => _container.gameObject.SetActive(false);

        /// <summary>Called from the main thread with each episode report.</summary>
        public void AddEpisode(EpisodeReport report)
        {
            _fidelityHistory.Add((float)report.Fidelity);
            _rewardHistory.Add((float)report.TotalReward);
            if (report.BestFidelity > _bestFidelity) _bestFidelity = (float)report.BestFidelity;
            _dirty = true;

            _statsText.text = $"ep {report.Episode}  best {_bestFidelity:F3}";
            _statusText.text = $"Training... episode {report.Episode}  reward={report.TotalReward:F2}";
        }

        public void SetStatus(string status)
        {
            if (_statusText != null) _statusText.text = status;
        }

        public void Clear()
        {
            _fidelityHistory.Clear();
            _rewardHistory.Clear();
            _bestFidelity = 0;
            _dirty = true;
            if (_statsText != null) _statsText.text = "";
            if (_statusText != null) _statusText.text = "Press E to start RL training";
        }

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                RedrawGraph();
            }
        }

        private void ClearGraph()
        {
            var pixels = _graphTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = BgColor;
            _graphTex.SetPixels(pixels);
            _graphTex.Apply();
        }

        private void RedrawGraph()
        {
            // Clear
            var pixels = _graphTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = BgColor;

            // Grid lines (horizontal at 0.25, 0.5, 0.75, 1.0)
            for (int g = 1; g <= 4; g++)
            {
                int y = (int)(g * 0.25f * (GraphHeight - 1));
                if (y >= 0 && y < GraphHeight)
                    for (int x = 0; x < GraphWidth; x++)
                        pixels[y * GraphWidth + x] = GridColor;
            }

            int count = _fidelityHistory.Count;
            if (count < 2) { _graphTex.SetPixels(pixels); _graphTex.Apply(); return; }

            // Determine X scale
            float xStep = (float)(GraphWidth - 1) / (count - 1);

            // Normalize reward to [0, 1] range for display
            float maxReward = 0.01f;
            foreach (var r in _rewardHistory) maxReward = Mathf.Max(maxReward, Mathf.Abs(r));

            // Draw reward line (blue)
            DrawLine(pixels, _rewardHistory, xStep, count, RewardLineColor, v => Mathf.Clamp01((v / maxReward + 1f) * 0.5f));

            // Draw fidelity line (green) — already [0, 1]
            DrawLine(pixels, _fidelityHistory, xStep, count, FidelityLineColor, v => Mathf.Clamp01(v));

            _graphTex.SetPixels(pixels);
            _graphTex.Apply();
        }

        private void DrawLine(Color[] pixels, List<float> data, float xStep, int count,
            Color color, System.Func<float, float> normalize)
        {
            for (int i = 1; i < count; i++)
            {
                int x0 = Mathf.Clamp((int)((i - 1) * xStep), 0, GraphWidth - 1);
                int x1 = Mathf.Clamp((int)(i * xStep), 0, GraphWidth - 1);
                int y0 = Mathf.Clamp((int)(normalize(data[i - 1]) * (GraphHeight - 1)), 0, GraphHeight - 1);
                int y1 = Mathf.Clamp((int)(normalize(data[i]) * (GraphHeight - 1)), 0, GraphHeight - 1);

                // Bresenham line
                int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                int err = dx + dy;

                int cx = x0, cy = y0;
                for (int iter = 0; iter < 1000; iter++)
                {
                    if (cx >= 0 && cx < GraphWidth && cy >= 0 && cy < GraphHeight)
                    {
                        pixels[cy * GraphWidth + cx] = color;
                        // Thicc line — draw ±1 pixel vertically
                        if (cy + 1 < GraphHeight) pixels[(cy + 1) * GraphWidth + cx] = color;
                        if (cy - 1 >= 0) pixels[(cy - 1) * GraphWidth + cx] = color;
                    }
                    if (cx == x1 && cy == y1) break;
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; cx += sx; }
                    if (e2 <= dx) { err += dx; cy += sy; }
                }
            }
        }
    }
}
