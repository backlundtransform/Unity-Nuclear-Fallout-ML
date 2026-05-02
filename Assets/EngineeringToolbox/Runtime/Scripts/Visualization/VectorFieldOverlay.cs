using System.Collections.Generic;
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
        private const float ArrowWidth = 0.014f;
        private const float ArrowHeadLengthFactor = 0.26f;
        private const float ArrowHeadWidthFactor = 0.16f;

        private double[,] _ex;
        private double[,] _ey;
        private int _nx, _ny;
        private RawImage _target;
        private Material _lineMaterial;
        private Material _worldLineMaterial;
        private readonly List<LineRenderer> _worldLines = new List<LineRenderer>();
        private int _skipFactor = 4;
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

        public void RenderWorld(Transform parent, Vector2 surfaceSize, double[,] ex, double[,] ey, int nx, int ny)
        {
            RenderWorld(parent, surfaceSize, ex, ey, nx, ny, null, 0f);
        }

        public void RenderWorld(Transform parent, Vector2 surfaceSize, double[,] ex, double[,] ey, int nx, int ny, float phase)
        {
            RenderWorld(parent, surfaceSize, ex, ey, nx, ny, null, phase);
        }

        public void RenderWorld(Transform parent, Vector2 surfaceSize, double[,] ex, double[,] ey, int nx, int ny, bool[,] mask, float phase)
        {
            _ex = ex;
            _ey = ey;
            _nx = nx;
            _ny = ny;

            if (!_visible || parent == null || ex == null || ey == null)
            {
                ClearWorld();
                return;
            }

            EnsureWorldLineMaterial();

            double maxMagnitude = 0.0;
            for (int x = 0; x < nx; x += _skipFactor)
            for (int y = 0; y < ny; y += _skipFactor)
            {
                if (mask != null && mask[x, y])
                {
                    continue;
                }

                double magnitude = System.Math.Sqrt(ex[x, y] * ex[x, y] + ey[x, y] * ey[x, y]);
                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                }
            }

            if (maxMagnitude < 1e-12)
            {
                ClearWorld();
                return;
            }

            int lineIndex = 0;
            int effectiveSkip = Mathf.Max(_skipFactor, Mathf.CeilToInt(Mathf.Max(nx, ny) / 10f));
            float cellWidth = surfaceSize.x / nx;
            float cellHeight = surfaceSize.y / ny;
            float arrowScale = Mathf.Min(cellWidth, cellHeight) * effectiveSkip * 0.72f;

            for (int ix = 0; ix < nx; ix += effectiveSkip)
            for (int iy = 0; iy < ny; iy += effectiveSkip)
            {
                var line = GetWorldLine(lineIndex++, parent);

                if (mask != null && mask[ix, iy])
                {
                    line.gameObject.SetActive(false);
                    continue;
                }

                float magnitude = (float)(System.Math.Sqrt(ex[ix, iy] * ex[ix, iy] + ey[ix, iy] * ey[ix, iy]) / maxMagnitude);
                float deltaX = (float)(ex[ix, iy] / maxMagnitude) * arrowScale;
                float deltaY = (float)(ey[ix, iy] / maxMagnitude) * arrowScale;
                Color color = Color.Lerp(new Color(0.35f, 0.9f, 1f, 0.9f), new Color(1f, 0.92f, 0.45f, 1f), magnitude);

                Vector2 direction = new Vector2(deltaX, deltaY);
                if (direction.sqrMagnitude < 1e-6f)
                {
                    line.gameObject.SetActive(false);
                    continue;
                }

                Vector2 normalized = direction.normalized;
                Vector2 normal = new Vector2(-normalized.y, normalized.x);
                float cellPhase = Mathf.Repeat(phase + ((ix * 0.173f) + (iy * 0.097f)), 1f);
                Vector2 flowOffset = normalized * ((cellPhase - 0.5f) * direction.magnitude * 0.85f);
                float arrowHeadLength = direction.magnitude * ArrowHeadLengthFactor;
                float arrowHeadWidth = direction.magnitude * ArrowHeadWidthFactor;
                float localX = -surfaceSize.x * 0.5f + (ix + 0.5f) * cellWidth + flowOffset.x;
                float localY = -surfaceSize.y * 0.5f + (iy + 0.5f) * cellHeight + flowOffset.y;
                Vector3 start = new Vector3(localX, localY, -0.02f);
                Vector3 tip = new Vector3(localX + deltaX, localY + deltaY, -0.02f);
                Vector3 headBase = tip - new Vector3(normalized.x, normalized.y, 0f) * arrowHeadLength;
                Vector3 headLeft = headBase + new Vector3(normal.x, normal.y, 0f) * arrowHeadWidth;
                Vector3 headRight = headBase - new Vector3(normal.x, normal.y, 0f) * arrowHeadWidth;

                line.gameObject.SetActive(true);
                line.startColor = color;
                line.endColor = color;
                line.positionCount = 5;
                line.widthMultiplier = ArrowWidth;
                line.SetPosition(0, start);
                line.SetPosition(1, tip);
                line.SetPosition(2, headLeft);
                line.SetPosition(3, tip);
                line.SetPosition(4, headRight);
            }

            for (int i = lineIndex; i < _worldLines.Count; i++)
            {
                if (_worldLines[i] != null)
                {
                    _worldLines[i].gameObject.SetActive(false);
                }
            }
        }

        public void ClearWorld()
        {
            for (int i = 0; i < _worldLines.Count; i++)
            {
                if (_worldLines[i] != null)
                {
                    _worldLines[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureWorldLineMaterial()
        {
            if (_worldLineMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Sprites/Default");
            _worldLineMaterial = new Material(shader);
        }

        private LineRenderer GetWorldLine(int index, Transform parent)
        {
            while (_worldLines.Count <= index)
            {
                var lineObject = new GameObject($"VectorLine_{_worldLines.Count}");
                var line = lineObject.AddComponent<LineRenderer>();
                line.material = _worldLineMaterial;
                line.useWorldSpace = false;
                line.alignment = LineAlignment.TransformZ;
                line.textureMode = LineTextureMode.Stretch;
                line.positionCount = 5;
                line.widthMultiplier = ArrowWidth;
                line.numCapVertices = 0;
                line.numCornerVertices = 0;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                _worldLines.Add(line);
            }

            var worldLine = _worldLines[index];
            if (worldLine.transform.parent != parent)
            {
                worldLine.transform.SetParent(parent, false);
            }

            return worldLine;
        }

        public void SetVisible(bool visible) => _visible = visible;
        public void SetSkipFactor(int skip) => _skipFactor = Mathf.Max(1, skip);
    }
}
