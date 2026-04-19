using UnityEngine;
using UnityEngine.UI;

namespace EngineeringToolbox.Visualization
{
    /// <summary>
    /// Renders 2D vector field (Ex, Ey) as arrow glyphs overlaid on a RawImage.
    /// Draws using GL lines in OnRenderObject.
    /// </summary>
    public class VectorFieldOverlay
    {
        private double[,] _ex;
        private double[,] _ey;
        private int _nx, _ny;
        private RawImage _target;
        private Material _lineMaterial;
        private int _skipFactor = 2;
        private bool _visible = true;

        public void Render(RawImage target, double[,] ex, double[,] ey, int nx, int ny)
        {
            _target = target;
            _ex = ex;
            _ey = ey;
            _nx = nx;
            _ny = ny;

            if (_lineMaterial == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader != null)
                {
                    _lineMaterial = new Material(shader);
                    _lineMaterial.SetInt("_ZWrite", 0);
                    _lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                }
            }
        }

        /// <summary>
        /// Call from MonoBehaviour.OnRenderObject or manually to draw arrows.
        /// Uses GL immediate mode.
        /// </summary>
        public void DrawGL(RectTransform imageRect)
        {
            if (_ex == null || _ey == null || _lineMaterial == null || !_visible) return;

            // Find max magnitude for normalization
            double maxMag = 0;
            for (int x = 0; x < _nx; x += _skipFactor)
            for (int y = 0; y < _ny; y += _skipFactor)
            {
                double mag = System.Math.Sqrt(_ex[x, y] * _ex[x, y] + _ey[x, y] * _ey[x, y]);
                if (mag > maxMag) maxMag = mag;
            }
            if (maxMag < 1e-12) return;

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            GL.Color(new Color(1f, 1f, 0f, 0.8f)); // yellow arrows

            Vector3[] corners = new Vector3[4];
            imageRect.GetWorldCorners(corners);
            float xMin = corners[0].x;
            float yMin = corners[0].y;
            float w = corners[2].x - corners[0].x;
            float h = corners[2].y - corners[0].y;

            float cellW = w / _nx;
            float cellH = h / _ny;
            float arrowScale = Mathf.Min(cellW, cellH) * _skipFactor * 0.8f;

            for (int ix = 0; ix < _nx; ix += _skipFactor)
            for (int iy = 0; iy < _ny; iy += _skipFactor)
            {
                float cx = xMin + (ix + 0.5f) * cellW;
                float cy = yMin + (iy + 0.5f) * cellH;

                float dx = (float)(_ex[ix, iy] / maxMag) * arrowScale;
                float dy = (float)(_ey[ix, iy] / maxMag) * arrowScale;

                GL.Vertex3(cx, cy, 0);
                GL.Vertex3(cx + dx, cy + dy, 0);
            }

            GL.End();
            GL.PopMatrix();
        }

        public void SetVisible(bool visible) => _visible = visible;
        public void SetSkipFactor(int skip) => _skipFactor = Mathf.Max(1, skip);
    }
}
