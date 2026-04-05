using UnityEngine;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders a single Bloch sphere: semi-transparent sphere, axis lines,
    /// latitude/longitude wireframe, axis labels, and an animated state vector arrow.
    /// All geometry is created procedurally at runtime — no prefabs needed.
    /// </summary>
    public class BlochSphereRenderer : MonoBehaviour
    {
        private float _radius = 1.5f;
        private int _segments = 16;

        // Child objects
        private GameObject _sphereObj;
        private LineRenderer _stateArrow;
        private GameObject _stateGizmo;
        private LineRenderer[] _axisLines;
        private LineRenderer[] _wireframeLines;
        private TextMesh[] _labels;

        // Current & target Bloch vector (for smooth animation)
        private Vector3 _currentBloch = Vector3.up;
        private Vector3 _targetBloch = Vector3.up;
        private float _lerpSpeed = 4f;

        // Colors
        private static readonly Color SphereColor = new Color(0f, 0.4f, 0.6f, 0.08f);
        private static readonly Color WireColor = new Color(0f, 0.9f, 1f, 0.15f);
        private static readonly Color AxisColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly Color ArrowColor = new Color(1f, 0.25f, 1f, 1f); // magenta
        private static readonly Color GizmoColor = new Color(1f, 0.6f, 1f, 1f);

        public void Initialise(float radius, int segments, float animSpeed)
        {
            _radius = radius;
            _segments = segments;
            _lerpSpeed = 1f / Mathf.Max(animSpeed, 0.05f);
            Build();
        }

        /// <summary>Set the target Bloch vector. The arrow animates toward it.</summary>
        public void SetState(Vector3 bloch)
        {
            _targetBloch = bloch.normalized * Mathf.Clamp01(bloch.magnitude);
        }

        /// <summary>Snap immediately (no animation).</summary>
        public void SetStateImmediate(Vector3 bloch)
        {
            _targetBloch = bloch;
            _currentBloch = bloch;
            UpdateArrow();
        }

        private void Update()
        {
            if (_stateArrow == null) return;
            _currentBloch = Vector3.Lerp(_currentBloch, _targetBloch, Time.deltaTime * _lerpSpeed);
            UpdateArrow();
        }

        private void Build()
        {
            // Semi-transparent sphere
            _sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphereObj.transform.SetParent(transform, false);
            _sphereObj.transform.localScale = Vector3.one * _radius * 2f;
            var sphereMat = new Material(Shader.Find("Standard"));
            sphereMat.SetFloat("_Mode", 3); // Transparent
            sphereMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            sphereMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            sphereMat.SetInt("_ZWrite", 0);
            sphereMat.DisableKeyword("_ALPHATEST_ON");
            sphereMat.EnableKeyword("_ALPHABLEND_ON");
            sphereMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            sphereMat.renderQueue = 3000;
            sphereMat.color = SphereColor;
            _sphereObj.GetComponent<Renderer>().material = sphereMat;
            var col = _sphereObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            BuildWireframe();
            BuildAxes();
            BuildLabels();
            BuildStateArrow();
        }

        private void BuildWireframe()
        {
            int latCount = _segments / 2;
            int lonCount = _segments;
            var lines = new System.Collections.Generic.List<LineRenderer>();

            // Latitude circles
            for (int i = 1; i < latCount; i++)
            {
                float theta = Mathf.PI * i / latCount;
                var lr = CreateLine($"Lat_{i}", WireColor, 0.005f * _radius);
                int pts = lonCount + 1;
                lr.positionCount = pts;
                for (int j = 0; j <= lonCount; j++)
                {
                    float phi = 2f * Mathf.PI * j / lonCount;
                    lr.SetPosition(j, new Vector3(
                        Mathf.Sin(theta) * Mathf.Cos(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Sin(phi)) * _radius);
                }
                lines.Add(lr);
            }

            // Longitude meridians
            for (int i = 0; i < lonCount / 2; i++)
            {
                float phi = 2f * Mathf.PI * i / lonCount;
                var lr = CreateLine($"Lon_{i}", WireColor, 0.005f * _radius);
                int pts = latCount * 2 + 1;
                lr.positionCount = pts;
                for (int j = 0; j <= latCount * 2; j++)
                {
                    float theta = Mathf.PI * j / (latCount * 2);
                    lr.SetPosition(j, new Vector3(
                        Mathf.Sin(theta) * Mathf.Cos(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Sin(phi)) * _radius);
                }
                lines.Add(lr);
            }

            _wireframeLines = lines.ToArray();
        }

        private void BuildAxes()
        {
            _axisLines = new LineRenderer[3];
            Vector3[] dirs = { Vector3.right, Vector3.up, Vector3.forward };
            for (int i = 0; i < 3; i++)
            {
                var lr = CreateLine($"Axis_{i}", AxisColor, 0.008f * _radius);
                lr.positionCount = 2;
                lr.SetPosition(0, -dirs[i] * _radius * 1.15f);
                lr.SetPosition(1, dirs[i] * _radius * 1.15f);
                _axisLines[i] = lr;
            }
        }

        private void BuildLabels()
        {
            // |0⟩ top, |1⟩ bottom, |+⟩ right, |−⟩ left, |i⟩ front, |−i⟩ back
            string[] texts = { "|0⟩", "|1⟩", "|+⟩", "|−⟩", "|i⟩", "|−i⟩" };
            Vector3[] positions =
            {
                Vector3.up * _radius * 1.3f,
                Vector3.down * _radius * 1.3f,
                Vector3.right * _radius * 1.3f,
                Vector3.left * _radius * 1.3f,
                Vector3.forward * _radius * 1.3f,
                Vector3.back * _radius * 1.3f
            };

            _labels = new TextMesh[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                var go = new GameObject($"Label_{texts[i]}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = positions[i];
                var tm = go.AddComponent<TextMesh>();
                tm.text = texts[i];
                tm.characterSize = 0.035f * _radius;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(0.8f, 0.9f, 1f, 0.85f);
                tm.fontSize = 36;
                _labels[i] = tm;
            }
        }

        private void BuildStateArrow()
        {
            _stateArrow = CreateLine("StateArrow", ArrowColor, 0.025f * _radius);
            _stateArrow.positionCount = 2;

            // Gizmo sphere at tip
            _stateGizmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _stateGizmo.name = "StateGizmo";
            _stateGizmo.transform.SetParent(transform, false);
            _stateGizmo.transform.localScale = Vector3.one * _radius * 0.08f;
            var gizmoMat = new Material(Shader.Find("Standard"));
            gizmoMat.EnableKeyword("_EMISSION");
            gizmoMat.SetColor("_EmissionColor", GizmoColor * 2f);
            gizmoMat.color = GizmoColor;
            _stateGizmo.GetComponent<Renderer>().material = gizmoMat;
            var col = _stateGizmo.GetComponent<Collider>();
            if (col != null) Destroy(col);

            UpdateArrow();
        }

        private void UpdateArrow()
        {
            if (_stateArrow == null) return;
            Vector3 tip = _currentBloch * _radius;
            _stateArrow.SetPosition(0, Vector3.zero);
            _stateArrow.SetPosition(1, tip);
            _stateGizmo.transform.localPosition = tip;
        }

        private LineRenderer CreateLine(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.startWidth = width;
            lr.endWidth = width;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            lr.material = mat;
            lr.startColor = color;
            lr.endColor = color;
            return lr;
        }
    }
}
