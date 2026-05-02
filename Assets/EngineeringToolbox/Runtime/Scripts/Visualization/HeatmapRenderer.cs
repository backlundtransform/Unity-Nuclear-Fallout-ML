using UnityEngine;

namespace EngineeringToolbox.Visualization
{
    /// <summary>
    /// Maps a 2D double[,] scalar field to a <see cref="Texture2D"/> using a seven-stop engineering gradient.
    /// Supports auto min/max scaling.
    /// </summary>
    public class HeatmapRenderer
    {
        private static readonly Color[] EngineeringGradient =
        {
            new Color(0.07f, 0.28f, 0.78f),
            new Color(0.39f, 0.74f, 1.0f),
            new Color(0.12f, 0.66f, 0.24f),
            new Color(0.58f, 0.89f, 0.40f),
            new Color(0.98f, 0.87f, 0.16f),
            new Color(0.97f, 0.52f, 0.12f),
            new Color(0.84f, 0.10f, 0.10f)
        };

        private Texture2D _texture;
        private Texture2D _legendTexture;
        private int _nx;
        private int _ny;

        public HeatmapRenderer(int nx, int ny)
        {
            _nx = nx;
            _ny = ny;
            _texture = new Texture2D(nx, ny, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        public void Resize(int nx, int ny)
        {
            if (_nx == nx && _ny == ny) return;
            _nx = nx;
            _ny = ny;
            _texture = new Texture2D(nx, ny, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        /// <summary>
        /// Renders the scalar field to a texture. Returns the updated texture.
        /// Auto-scales min/max from the field data.
        /// </summary>
        public Texture2D Render(double[,] field)
        {
            int w = field.GetLength(0);
            int h = field.GetLength(1);

            double min = double.MaxValue;
            double max = double.MinValue;
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                double v = field[x, y];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            return Render(field, min, max);
        }

        /// <summary>
        /// Renders the scalar field to a texture using explicit min/max for normalization.
        /// Use this overload when animating a timeline so all frames share the same color scale.
        /// </summary>
        public Texture2D Render(double[,] field, double min, double max)
        {
            int w = field.GetLength(0);
            int h = field.GetLength(1);
            Resize(w, h);

            double range = max - min;
            if (!double.IsFinite(min) || !double.IsFinite(max) || !double.IsFinite(range) || range < 1e-12)
            {
                min = 0.0;
                max = 1.0;
                range = 1.0;
            }

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                double value = field[x, y];
                float t = double.IsFinite(value) ? (float)((value - min) / range) : 0f;
                _texture.SetPixel(x, y, EvaluateColor(t));
            }

            _texture.Apply();
            return _texture;
        }

        public Texture2D Render(double[,] field, double min, double max, bool[,] mask, Color maskColor)
        {
            int w = field.GetLength(0);
            int h = field.GetLength(1);
            Resize(w, h);

            double range = max - min;
            if (!double.IsFinite(min) || !double.IsFinite(max) || !double.IsFinite(range) || range < 1e-12)
            {
                min = 0.0;
                max = 1.0;
                range = 1.0;
            }

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (mask != null && mask[x, y])
                {
                    _texture.SetPixel(x, y, maskColor);
                    continue;
                }

                double value = field[x, y];
                float t = double.IsFinite(value) ? (float)((value - min) / range) : 0f;
                _texture.SetPixel(x, y, EvaluateColor(t));
            }

            _texture.Apply();
            return _texture;
        }

        /// <summary>
        /// Creates a vertical legend texture that uses the same color mapping as the heatmap.
        /// Top corresponds to the maximum value, bottom to the minimum.
        /// </summary>
        public Texture2D RenderLegend(int width, int height)
        {
            if (_legendTexture == null || _legendTexture.width != width || _legendTexture.height != height)
            {
                _legendTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            for (int y = 0; y < height; y++)
            {
                float t = height > 1 ? y / (float)(height - 1) : 0f;
                Color color = EvaluateColor(t);
                for (int x = 0; x < width; x++)
                {
                    _legendTexture.SetPixel(x, y, color);
                }
            }

            _legendTexture.Apply();
            return _legendTexture;
        }

        /// <summary>
        /// Engineering gradient: blue → light blue → green → light green → yellow → orange → red.
        /// </summary>
        public static Color EvaluateColor(float t)
        {
            return SampleGradient(t);
        }

        private static Color SampleGradient(float t)
        {
            if (float.IsNaN(t) || float.IsInfinity(t))
            {
                t = 0f;
            }

            t = Mathf.Clamp01(t);

            if (EngineeringGradient.Length == 1)
            {
                return EngineeringGradient[0];
            }

            float scaled = t * (EngineeringGradient.Length - 1);
            int lowerIndex = Mathf.FloorToInt(scaled);
            int upperIndex = Mathf.Min(lowerIndex + 1, EngineeringGradient.Length - 1);
            float blend = scaled - lowerIndex;

            if (lowerIndex >= EngineeringGradient.Length - 1)
            {
                return EngineeringGradient[EngineeringGradient.Length - 1];
            }

            return Color.Lerp(EngineeringGradient[lowerIndex], EngineeringGradient[upperIndex], blend);
        }
    }
}
