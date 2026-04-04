using UnityEngine;
using System.Collections.Generic;
using QuantumCircuitViz.Core;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders the quantum circuit as 3D world-space geometry — hologram aesthetic.
    /// Qubit wires are glowing horizontal lines, gates are translucent cubes with
    /// text labels, multi-qubit connections are vertical bars.
    /// Toggle between 2D UI diagram and this 3D view.
    /// </summary>
    public class WorldSpaceCircuit : MonoBehaviour
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private int _qubitCount;
        private float _wireSpacing = 1.2f;
        private float _gateSpacing = 1.0f;
        private int _currentStep = -1;

        private static readonly Color WireColor = new Color(0f, 0.5f, 0.8f, 0.6f);
        private static readonly Color GateColor = new Color(0f, 0.7f, 0.9f, 0.4f);
        private static readonly Color GateActiveColor = new Color(0f, 1f, 0.5f, 0.6f);
        private static readonly Color ControlDotColor = new Color(1f, 0.8f, 0f, 0.9f);
        private static readonly Color TextColor = new Color(0.9f, 0.95f, 1f);

        public void Initialise(float wireSpacing = 1.2f, float gateSpacing = 1.0f)
        {
            _wireSpacing = wireSpacing;
            _gateSpacing = gateSpacing;
        }

        /// <summary>
        /// Build the 3D circuit from a CircuitRunner.
        /// Origin is at transform.position.
        /// </summary>
        public void BuildCircuit(CircuitRunner runner)
        {
            Clear();
            _qubitCount = runner.QubitCount;

            float totalHeight = (_qubitCount - 1) * _wireSpacing;
            float totalLength = (runner.Steps.Count + 1) * _gateSpacing;

            // Qubit wires
            for (int q = 0; q < _qubitCount; q++)
            {
                float y = totalHeight / 2f - q * _wireSpacing;
                var wire = CreateWire(
                    new Vector3(-_gateSpacing, y, 0),
                    new Vector3(totalLength, y, 0));
                _objects.Add(wire);

                // Qubit label
                var label = CreateWorldLabel($"|q{q}⟩",
                    new Vector3(-_gateSpacing - 0.6f, y, 0), 0.04f);
                _objects.Add(label);
            }

            // Gates
            for (int s = 0; s < runner.Steps.Count; s++)
            {
                var step = runner.Steps[s];
                float x = (s + 0.5f) * _gateSpacing;
                string gateName = step.Gate.GetType().Name
                    .Replace("Gate", "").Replace("Hadamard", "H")
                    .Replace("PauliX", "X").Replace("PauliY", "Y").Replace("PauliZ", "Z");
                if (gateName.Length > 3) gateName = gateName.Substring(0, 3);

                if (step.QubitIndices.Count == 1)
                {
                    int q = step.QubitIndices[0];
                    float y = totalHeight / 2f - q * _wireSpacing;
                    var gate = CreateGateBox(new Vector3(x, y, 0), gateName, s);
                    _objects.Add(gate);
                }
                else
                {
                    // Multi-qubit gate: control dots + target box + vertical connector
                    int minQ = int.MaxValue, maxQ = int.MinValue;
                    foreach (var qi in step.QubitIndices) {
                        if (qi < minQ) minQ = qi;
                        if (qi > maxQ) maxQ = qi;
                    }

                    // Vertical connector line
                    float yTop = totalHeight / 2f - minQ * _wireSpacing;
                    float yBot = totalHeight / 2f - maxQ * _wireSpacing;
                    var connector = CreateWire(
                        new Vector3(x, yTop, 0), new Vector3(x, yBot, 0), ControlDotColor, 0.03f);
                    _objects.Add(connector);

                    // Control dots (all except last qubit = target)
                    for (int i = 0; i < step.QubitIndices.Count - 1; i++)
                    {
                        int q = step.QubitIndices[i];
                        float y = totalHeight / 2f - q * _wireSpacing;
                        var dot = CreateControlDot(new Vector3(x, y, 0));
                        _objects.Add(dot);
                    }

                    // Target gate box
                    int tq = step.QubitIndices[step.QubitIndices.Count - 1];
                    float ty = totalHeight / 2f - tq * _wireSpacing;
                    var targetGate = CreateGateBox(new Vector3(x, ty, 0), gateName, s);
                    _objects.Add(targetGate);
                }
            }

            // Measurement boxes at end
            for (int q = 0; q < _qubitCount; q++)
            {
                float y = totalHeight / 2f - q * _wireSpacing;
                float x = (runner.Steps.Count + 0.5f) * _gateSpacing;
                var mbox = CreateGateBox(new Vector3(x, y, 0), "M", -1);
                _objects.Add(mbox);
            }
        }

        /// <summary>Highlight gates up to currentStep.</summary>
        public void SetCurrentStep(int step)
        {
            _currentStep = step;
            // Gate boxes store step index in name — update colors
            foreach (var obj in _objects)
            {
                if (obj.name.StartsWith("Gate_"))
                {
                    if (int.TryParse(obj.name.Substring(5), out int gateStep))
                    {
                        var rend = obj.GetComponent<Renderer>();
                        if (rend != null)
                            rend.material.color = gateStep <= step ? GateActiveColor : GateColor;
                    }
                }
            }
        }

        public void Clear()
        {
            foreach (var obj in _objects)
                if (obj != null) Destroy(obj);
            _objects.Clear();
        }

        public void Show()
        {
            foreach (var obj in _objects)
                if (obj != null) obj.SetActive(true);
        }

        public void Hide()
        {
            foreach (var obj in _objects)
                if (obj != null) obj.SetActive(false);
        }

        private GameObject CreateWire(Vector3 from, Vector3 to, Color? color = null, float width = 0.02f)
        {
            var go = new GameObject("Wire");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, transform.TransformPoint(from));
            lr.SetPosition(1, transform.TransformPoint(to));
            lr.startWidth = width;
            lr.endWidth = width;
            var c = color ?? WireColor;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = c;
            lr.endColor = c;
            lr.useWorldSpace = true;
            return go;
        }

        private GameObject CreateGateBox(Vector3 localPos, string label, int stepIndex)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Gate_{stepIndex}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.3f);

            var rend = go.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = GateColor;

            // Label as child TextMesh
            var labelObj = CreateWorldLabel(label, localPos + Vector3.back * 0.2f, 0.05f);
            labelObj.transform.SetParent(go.transform, true);

            // Remove collider (we don't need click on gate boxes)
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        private GameObject CreateControlDot(Vector3 localPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ControlDot";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.2f;

            var rend = go.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = ControlDotColor;

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        private GameObject CreateWorldLabel(string text, Vector3 localPos, float charSize)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = TextColor;

            return go;
        }
    }
}
