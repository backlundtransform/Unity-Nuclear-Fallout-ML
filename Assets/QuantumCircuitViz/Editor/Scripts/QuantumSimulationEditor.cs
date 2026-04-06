using UnityEngine;
using UnityEditor;
using QuantumCircuitViz.Core;
using QuantumCircuitViz.Export;
using QuantumCircuitViz.Demo;

namespace QuantumCircuitViz.Editor
{
    [CustomEditor(typeof(DemoSimulation))]
    public class QuantumSimulationEditor : UnityEditor.Editor
    {
        private bool _presetsFoldout = true;
        private bool _exportFoldout;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var demo = (DemoSimulation)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Quantum Circuit Visualizer", EditorStyles.boldLabel);

            // ── Presets ──
            _presetsFoldout = EditorGUILayout.Foldout(_presetsFoldout, "Preset Circuits", true);
            if (_presetsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Load a preset circuit.  Only works in Play Mode.", MessageType.Info);

                using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Bell State")) demo.SendMessage("LoadPreset_BellState");
                    if (GUILayout.Button("GHZ")) demo.SendMessage("LoadPreset_GHZ");
                    if (GUILayout.Button("Superposition")) demo.SendMessage("LoadPreset_SuperpositionChain");
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Phase Kickback")) demo.SendMessage("LoadPreset_PhaseKickback");
                    if (GUILayout.Button("Grover")) demo.SendMessage("LoadPreset_Grover");
                    if (GUILayout.Button("Toffoli")) demo.SendMessage("LoadPreset_Toffoli");
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("QFT")) demo.SendMessage("LoadPreset_QFT");
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            // ── Export ──
            _exportFoldout = EditorGUILayout.Foldout(_exportFoldout, "Export", true);
            if (_exportFoldout)
            {
                EditorGUI.indentLevel++;

                using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Copy QASM to Clipboard"))
                        demo.SendMessage("ExportQASMToClipboard");

                    if (GUILayout.Button("Copy JSON to Clipboard"))
                        demo.SendMessage("ExportJSONToClipboard");

                    if (GUILayout.Button("Take Screenshot (F12)"))
                        demo.SendMessage("TakeScreenshot");
                }
                EditorGUI.indentLevel--;
            }

            // ── Quick controls ──
            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("◀ Step")) demo.SendMessage("StepBackward");
                if (GUILayout.Button("Step ▶")) demo.SendMessage("StepForward");
                if (GUILayout.Button("Reset")) demo.SendMessage("ResetCircuit");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Key bindings in Play Mode:\n" +
                "Tab — Cycle view (Bloch → Histogram → Circuit)\n" +
                "←/→ Step  |  Space Play  |  R Reset  |  N Noise\n" +
                "M Measure (Histogram)\n" +
                "F5 Export QASM  |  F12 Screenshot  |  1-7 Presets",
                MessageType.None);
        }
    }
}
