using UnityEngine;

namespace EngineeringToolbox.Visualization
{
    /// <summary>
    /// Maps a 2D double[,] scalar field to a <see cref="Texture2D"/> using a coolwarm color gradient.
    /// Supports auto min/max scaling.
    /// </summary>
    public class HeatmapRenderer
    {
        private Texture2D _texture;
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
            if (range < 1e-12) range = 1.0;

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                float t = (float)((field[x, y] - min) / range);
                _texture.SetPixel(x, y, CoolWarm(t));
            }

            _texture.Apply();
            return _texture;
        }

        /// <summary>
        /// Cool-warm diverging colormap: blue (0) → white (0.5) → red (1).
        /// </summary>
        private static Color CoolWarm(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.5f)
            {
                float s = t * 2f; // 0..1
                return new Color(s, s, 1f); // blue → white
            }
            else
            {
                float s = (t - 0.5f) * 2f; // 0..1
                return new Color(1f, 1f - s, 1f - s); // white → red
            }
        }
    }
}
