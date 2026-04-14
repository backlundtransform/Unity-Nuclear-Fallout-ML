using UnityEngine;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders a single Bloch sphere with an elegant, clear Qiskit-style aesthetic.
    /// Features:
    ///  - Semi-transparent, very subtle sphere
    ///  - Three prominent equator/meridian rings
    ///  - Prominent XYZ axes extending beyond sphere
    ///  - Magenta state vector with sphere-tip
    ///  - Projection lines dropping to the XZ plane and then to axes.
    /// </summary>
    public class BlochSphereRenderer : MonoBehaviour
    {
        private float _radius = 1.5f;
        private int _segments = 256;

        // Child objects
        private GameObject _sphereObj;
        private LineRenderer _stateArrow;
        private GameObject _stateGizmo;
        private LineRenderer[] _axisLines;
        private LineRenderer[] _wireframeLines;
        private TextMesh[] _labels;
        
        // Projection Lines
        private LineRenderer _projLineToPlane;
        private LineRenderer _projLineToX;
        private LineRenderer _projLineToZ; // Using Unity Z, which corresponds to Qiskit Y

        // Angle arcs & labels
        private LineRenderer _thetaArc;
        private LineRenderer _phiArc;
        private TextMesh _thetaLabel;
        private TextMesh _phiLabel;
        private LineRenderer _projLineFromOrigin; // line from origin to projection on equator

        // Current & target Bloch vector (for smooth animation)
        private Vector3 _currentBloch = Vector3.up;
        private Vector3 _targetBloch = Vector3.up;
        private float _lerpSpeed = 4f;

        // Colors (Qiskit inspired)
        private static readonly Color SphereColor = new Color(0.40f, 0.55f, 0.90f, 0.10f);
        private static readonly Color WireColor = new Color(0.15f, 0.65f, 0.95f, 0.40f);
        private static readonly Color ArrowColor = new Color(0.95f, 0.20f, 0.60f, 1f); // bright magenta
        private static readonly Color ProjLineColor = new Color(0.40f, 0.70f, 0.90f, 0.45f);
        private static readonly Color ThetaArcColor = new Color(0.25f, 0.90f, 0.30f, 0.90f);
        private static readonly Color PhiArcColor = new Color(0.90f, 0.25f, 0.90f, 0.90f);

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
            sphereMat.SetFloat("_Glossiness", 0.92f);
            sphereMat.SetFloat("_Metallic", 0.02f);
            _sphereObj.GetComponent<Renderer>().material = sphereMat;
            var col = _sphereObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            BuildWireframe();
            BuildAxes();
            BuildLabels();
            BuildStateArrow();
            BuildProjectionLines();
            BuildAngleArcs();
        }

        private void BuildWireframe()
        {
            var lines = new System.Collections.Generic.List<LineRenderer>();
            int segs = Mathf.Max(_segments, 256); // Force minimum 256 for smooth circles
            int pts = segs + 1;

            // 1. Equator (XZ Plane) — bright neon cyan
            var eq = CreateLine("Wire_Equator", new Color(0.1f, 0.75f, 1f, 0.55f), 0.010f * _radius);
            eq.positionCount = pts;
            for (int i = 0; i <= segs; i++)
            {
                float ang = 2f * Mathf.PI * i / segs;
                eq.SetPosition(i, new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * _radius);
            }
            lines.Add(eq);

            // 2. Meridian (XY Plane)
            var m1 = CreateLine("Wire_Meridian1", new Color(0.1f, 0.65f, 0.95f, 0.35f), 0.006f * _radius);
            m1.positionCount = pts;
            for (int i = 0; i <= segs; i++)
            {
                float ang = 2f * Mathf.PI * i / segs;
                m1.SetPosition(i, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * _radius);
            }
            lines.Add(m1);

            // 3. Meridian (YZ Plane)
            var m2 = CreateLine("Wire_Meridian2", new Color(0.1f, 0.65f, 0.95f, 0.35f), 0.006f * _radius);
            m2.positionCount = pts;
            for (int i = 0; i <= segs; i++)
            {
                float ang = 2f * Mathf.PI * i / segs;
                m2.SetPosition(i, new Vector3(0f, Mathf.Sin(ang), Mathf.Cos(ang)) * _radius);
            }
            lines.Add(m2);

            _wireframeLines = lines.ToArray();
        }

        // Axis colors matching standard Bloch sphere diagrams
        private static readonly Color AxisXColor = new Color(0.30f, 0.85f, 0.35f, 0.95f);  // bright green
        private static readonly Color AxisYColor = new Color(0.30f, 0.50f, 0.95f, 0.95f);   // bright blue
        private static readonly Color AxisZColor = new Color(0.95f, 0.30f, 0.30f, 0.95f);    // bright red

        private void BuildAxes()
        {
            _axisLines = new LineRenderer[3];
            Vector3[] dirs = { Vector3.right, Vector3.up, Vector3.forward };
            Color[] colors = { AxisXColor, AxisZColor, AxisYColor }; // X=green, Y(up)=red(Z-bloch), Z(fwd)=blue(Y-bloch)
            for (int i = 0; i < 3; i++)
            {
                var lr = CreateLine($"Axis_{i}", colors[i], 0.015f * _radius);
                lr.positionCount = 2;
                lr.SetPosition(0, -dirs[i] * _radius * 1.3f);
                lr.SetPosition(1, dirs[i] * _radius * 1.3f);
                _axisLines[i] = lr;
            }
        }

        private void BuildLabels()
        {
            // Unity mapping: +Y = Bloch +Z (|0⟩), -Y = |1⟩, +X = |+⟩, -X = |−⟩, +Z = |+i⟩, -Z = |−i⟩
            string[] texts =
            {
                "|0⟩",       // +Y  (Bloch +Z)
                "|1⟩",       // -Y  (Bloch -Z)
                "|+⟩",       // +X
                "|−⟩",       // -X
                "|+i⟩",      // +Z  (Bloch +Y)
                "|−i⟩"       // -Z  (Bloch -Y)
            };
            Color[] labelColors =
            {
                AxisZColor, AxisZColor,  // Z axis labels = red
                AxisXColor, AxisXColor,  // X axis labels = green
                AxisYColor, AxisYColor   // Y axis labels = blue
            };
            Vector3[] positions =
            {
                Vector3.up * _radius * 1.45f,
                Vector3.down * _radius * 1.45f,
                Vector3.right * _radius * 1.45f,
                Vector3.left * _radius * 1.45f,
                Vector3.forward * _radius * 1.45f,
                Vector3.back * _radius * 1.45f
            };

            _labels = new TextMesh[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                var go = new GameObject($"Label_{texts[i]}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = positions[i];
                var tm = go.AddComponent<TextMesh>();
                tm.text = texts[i];
                tm.characterSize = 0.05f * _radius;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = labelColors[i];
                tm.fontSize = 48;
                tm.fontStyle = FontStyle.Bold;
                _labels[i] = tm;
            }
        }

        private void BuildStateArrow()
        {
            _stateArrow = CreateLine("StateArrow", ArrowColor, 0.035f * _radius);
            _stateArrow.positionCount = 2;

            // Gizmo sphere at tip
            _stateGizmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _stateGizmo.name = "StateGizmo";
            _stateGizmo.transform.SetParent(transform, false);
            _stateGizmo.transform.localScale = Vector3.one * _radius * 0.1f;
            var gizmoMat = new Material(Shader.Find("Standard"));
            gizmoMat.EnableKeyword("_EMISSION");
            gizmoMat.SetColor("_EmissionColor", ArrowColor * 2.0f);
            gizmoMat.color = ArrowColor;
            _stateGizmo.GetComponent<Renderer>().material = gizmoMat;
            var col = _stateGizmo.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private void BuildProjectionLines()
        {
            _projLineToPlane = CreateLine("ProjPlane", ProjLineColor, 0.008f * _radius);
            _projLineToPlane.positionCount = 2;
            _projLineToX = CreateLine("ProjX", ProjLineColor, 0.008f * _radius);
            _projLineToX.positionCount = 2;
            _projLineToZ = CreateLine("ProjZ", ProjLineColor, 0.008f * _radius);
            _projLineToZ.positionCount = 2;
            _projLineFromOrigin = CreateLine("ProjOrigin", ProjLineColor, 0.006f * _radius);
            _projLineFromOrigin.positionCount = 2;
        }

        private void BuildAngleArcs()
        {
            int arcPts = 24;

            // θ arc (green) — from +Y axis toward state vector
            _thetaArc = CreateLine("ThetaArc", ThetaArcColor, 0.012f * _radius);
            _thetaArc.positionCount = arcPts;

            // φ arc (magenta) — on equatorial plane from +X axis
            _phiArc = CreateLine("PhiArc", PhiArcColor, 0.012f * _radius);
            _phiArc.positionCount = arcPts;

            // θ label
            var tGo = new GameObject("ThetaLabel");
            tGo.transform.SetParent(transform, false);
            _thetaLabel = tGo.AddComponent<TextMesh>();
            _thetaLabel.text = "θ";
            _thetaLabel.characterSize = 0.04f * _radius;
            _thetaLabel.anchor = TextAnchor.MiddleCenter;
            _thetaLabel.fontSize = 48;
            _thetaLabel.fontStyle = FontStyle.Bold;
            _thetaLabel.color = ThetaArcColor;

            // φ label
            var pGo = new GameObject("PhiLabel");
            pGo.transform.SetParent(transform, false);
            _phiLabel = pGo.AddComponent<TextMesh>();
            _phiLabel.text = "Φ";
            _phiLabel.characterSize = 0.04f * _radius;
            _phiLabel.anchor = TextAnchor.MiddleCenter;
            _phiLabel.fontSize = 48;
            _phiLabel.fontStyle = FontStyle.Bold;
            _phiLabel.color = PhiArcColor;
        }

        private void UpdateArrow()
        {
            if (_stateArrow == null) return;
            Vector3 tip = _currentBloch * _radius;
            _stateArrow.SetPosition(0, Vector3.zero);
            _stateArrow.SetPosition(1, tip);
            _stateGizmo.transform.localPosition = tip;

            // Update projection lines
            Vector3 planePt = new Vector3(tip.x, 0, tip.z);
            _projLineToPlane.SetPosition(0, tip);
            _projLineToPlane.SetPosition(1, planePt);

            _projLineToX.SetPosition(0, planePt);
            _projLineToX.SetPosition(1, new Vector3(tip.x, 0, 0));

            _projLineToZ.SetPosition(0, planePt);
            _projLineToZ.SetPosition(1, new Vector3(0, 0, tip.z));

            // Dashed line from origin to equatorial projection
            _projLineFromOrigin.SetPosition(0, Vector3.zero);
            _projLineFromOrigin.SetPosition(1, planePt);

            UpdateAngleArcs(tip, planePt);
        }

        private void UpdateAngleArcs(Vector3 tip, Vector3 planePt)
        {
            int arcPts = 24;
            float arcR = _radius * 0.3f; // arc drawn at 30% of sphere radius

            // ── θ arc: from +Y (|0⟩) toward the state vector ──
            // θ = angle between +Y and the Bloch vector
            float theta = Mathf.Acos(Mathf.Clamp(_currentBloch.y, -1f, 1f));

            if (theta > 0.02f && _thetaArc != null)
            {
                _thetaArc.gameObject.SetActive(true);
                _thetaLabel.gameObject.SetActive(true);

                // Plane of the θ arc: defined by +Y and the state vector direction
                // We rotate from +Y toward the projection on the equator
                Vector3 planeDir = planePt.sqrMagnitude > 0.001f ? planePt.normalized : Vector3.right;
                for (int i = 0; i < arcPts; i++)
                {
                    float t = (float)i / (arcPts - 1);
                    float a = t * theta;
                    // Interpolate in the plane of Y-axis and planeDir
                    Vector3 pt = (Mathf.Cos(a) * Vector3.up + Mathf.Sin(a) * planeDir) * arcR;
                    _thetaArc.SetPosition(i, pt);
                }

                // Label at mid-arc, offset outward
                float midTheta = theta * 0.5f;
                Vector3 labelPos = (Mathf.Cos(midTheta) * Vector3.up + Mathf.Sin(midTheta) * planeDir) * (arcR + _radius * 0.08f);
                _thetaLabel.transform.localPosition = labelPos;
            }
            else if (_thetaArc != null)
            {
                _thetaArc.gameObject.SetActive(false);
                _thetaLabel.gameObject.SetActive(false);
            }

            // ── φ arc: on equatorial plane (Y=0) from +X toward the projection ──
            float phi = Mathf.Atan2(planePt.z, planePt.x); // angle from +X on XZ plane
            float absPhi = Mathf.Abs(phi);

            if (absPhi > 0.02f && planePt.sqrMagnitude > 0.01f && _phiArc != null)
            {
                _phiArc.gameObject.SetActive(true);
                _phiLabel.gameObject.SetActive(true);

                for (int i = 0; i < arcPts; i++)
                {
                    float t = (float)i / (arcPts - 1);
                    float a = t * phi;
                    Vector3 pt = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * arcR;
                    _phiArc.SetPosition(i, pt);
                }

                // Label at mid-arc
                float midPhi = phi * 0.5f;
                Vector3 phiLabelPos = new Vector3(Mathf.Cos(midPhi), 0f, Mathf.Sin(midPhi)) * (arcR + _radius * 0.08f);
                _phiLabel.transform.localPosition = phiLabelPos;
            }
            else if (_phiArc != null)
            {
                _phiArc.gameObject.SetActive(false);
                _phiLabel.gameObject.SetActive(false);
            }
        }

        private LineRenderer CreateLine(string name, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.numCornerVertices = 8;
            lr.numCapVertices = 4;
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
