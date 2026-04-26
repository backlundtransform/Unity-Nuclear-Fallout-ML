using UnityEngine;
using UnityEngine.UI;

namespace EngineeringToolbox.Visualization
{
    /// <summary>
    /// Draws a structured mesh overlay on top of a 2D simulation plate.
    /// </summary>
    public class PlateMeshOverlay
    {
        private RawImage _target;
        private Material _lineMaterial;
        private int _nx;
        private int _ny;
        private bool _visible;

        public void Render(RawImage target, int nx, int ny)
        {
            _target = target;
            _nx = Mathf.Max(1, nx);
            _ny = Mathf.Max(1, ny);
            _visible = target != null;

            if (_lineMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader != null)
            {
                _lineMaterial = new Material(shader);
                _lineMaterial.SetInt("_ZWrite", 0);
                _lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
        }

        public void Clear()
        {
            _visible = false;
        }

        public void DrawGL(RectTransform imageRect)
        {
            if (!_visible || _target == null || imageRect == null || _lineMaterial == null)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            imageRect.GetWorldCorners(corners);
            float xMin = corners[0].x;
            float yMin = corners[0].y;
            float width = corners[2].x - corners[0].x;
            float height = corners[2].y - corners[0].y;
            Color lineColor = new Color(0.88f, 0.96f, 1f, 0.26f);

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            GL.Color(lineColor);

            for (int ix = 0; ix <= _nx; ix++)
            {
                float x = xMin + width * ix / _nx;
                GL.Vertex3(x, yMin, 0f);
                GL.Vertex3(x, yMin + height, 0f);
            }

            for (int iy = 0; iy <= _ny; iy++)
            {
                float y = yMin + height * iy / _ny;
                GL.Vertex3(xMin, y, 0f);
                GL.Vertex3(xMin + width, y, 0f);
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}