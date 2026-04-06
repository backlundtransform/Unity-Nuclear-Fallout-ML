using UnityEngine;
using UnityEngine.UI;
using QuantumCircuitViz.Core;
using System.Collections.Generic;

namespace QuantumCircuitViz.Visualization
{
    /// <summary>
    /// Renders a textual circuit diagram on a UI panel.
    /// Shows qubit wires with gate symbols placed at the correct positions.
    /// </summary>
    public class CircuitDiagramRenderer : MonoBehaviour
    {
        private RectTransform _container;
        private Text _diagramText;

        public void Initialise(RectTransform parent)
        {
            gameObject.name = "CircuitDiagram";
            _container = gameObject.GetComponent<RectTransform>();
            if (_container == null)
                _container = gameObject.AddComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = new Vector2(0.02f, 0.05f);
            _container.anchorMax = new Vector2(0.55f, 0.20f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;

            var bg = gameObject.GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.85f);

            var textGo = new GameObject("DiagramText");
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.SetParent(_container, false);
            textRt.anchorMin = new Vector2(0.02f, 0.02f);
            textRt.anchorMax = new Vector2(0.98f, 0.98f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _diagramText = textGo.AddComponent<Text>();
            _diagramText.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            _diagramText.fontSize = 12;
            _diagramText.color = new Color(0f, 0.9f, 1f);
            _diagramText.alignment = TextAnchor.MiddleLeft;
        }

        /// <summary>
        /// Render the circuit as ASCII art with a cursor showing the current step.
        /// </summary>
        public void Render(IReadOnlyList<GateStep> steps, int qubitCount, int currentStep)
        {
            if (_diagramText == null) return;

            // Build wire strings for each qubit
            var wires = new string[qubitCount];
            for (int q = 0; q < qubitCount; q++)
                wires[q] = $"q{q} ";

            for (int s = 0; s < steps.Count; s++)
            {
                var step = steps[s];
                bool isCurrent = s == currentStep;
                string bracket = isCurrent ? "»" : "─";

                // Put gate symbol on target qubits, wire dash on others
                var placed = new HashSet<int>(step.QubitIndices);
                for (int q = 0; q < qubitCount; q++)
                {
                    if (placed.Contains(q))
                    {
                        string sym = step.Gate.GetType().Name
                            .Replace("Gate", "")
                            .Replace("Hadamard", "H")
                            .Replace("PauliX", "X")
                            .Replace("PauliZ", "Z");

                        if (sym.Length > 2) sym = sym.Substring(0, 2);
                        wires[q] += $"{bracket}[{sym}]{bracket}";
                    }
                    else
                    {
                        wires[q] += "──────";
                    }
                }
            }

            // Append measurement placeholder
            for (int q = 0; q < qubitCount; q++)
                wires[q] += "──[M]";

            _diagramText.text = string.Join("\n", wires);
        }
    }
}
