using System.Collections.Generic;
using UnityEngine;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Core
{
    /// <summary>
    /// Registry of all available quantum gates with display metadata.
    /// Used by the UI gate palette and circuit diagram renderer.
    /// </summary>
    public static class GateLibrary
    {
        // ── Single-qubit ──────────────────────────────────────────
        public static readonly GateInfo Hadamard = new GateInfo("H", "Hadamard", 1,
            "Equal superposition: |0⟩→(|0⟩+|1⟩)/√2",
            new Color(0f, 0.85f, 1f), () => new HadamardGate());

        public static readonly GateInfo PauliX = new GateInfo("X", "Pauli-X", 1,
            "Bit flip (quantum NOT): |0⟩↔|1⟩",
            new Color(1f, 0.3f, 0.25f), () => new PauliXGate());

        public static readonly GateInfo PauliY = new GateInfo("Y", "Pauli-Y", 1,
            "Bit+phase flip: |0⟩→i|1⟩, |1⟩→−i|0⟩",
            new Color(0.7f, 0.3f, 1f), () => new PauliYGate());

        public static readonly GateInfo PauliZ = new GateInfo("Z", "Pauli-Z", 1,
            "Phase flip: |1⟩→−|1⟩",
            new Color(0.2f, 0.55f, 1f), () => new PauliZGate());

        public static readonly GateInfo SPhase = new GateInfo("S", "S Phase", 1,
            "π/2 phase: |1⟩→i|1⟩  (S²=Z)",
            new Color(0.3f, 0.9f, 0.5f), () => new SGate());

        public static readonly GateInfo TPhase = new GateInfo("T", "T Phase", 1,
            "π/4 phase: |1⟩→e^(iπ/4)|1⟩  (T²=S)",
            new Color(0.2f, 0.8f, 0.6f), () => new TGate());

        public static readonly GateInfo Rx = new GateInfo("Rx", "Rx(π/2)", 1,
            "Rotate π/2 around X-axis on Bloch sphere",
            new Color(1f, 0.5f, 0.7f), () => new RxGate(System.Math.PI / 2));

        public static readonly GateInfo Ry = new GateInfo("Ry", "Ry(π/2)", 1,
            "Rotate π/2 around Y-axis on Bloch sphere",
            new Color(0.8f, 0.4f, 0.9f), () => new RyGate(System.Math.PI / 2));

        public static readonly GateInfo Rz = new GateInfo("Rz", "Rz(π/2)", 1,
            "Rotate π/2 around Z-axis on Bloch sphere",
            new Color(0.5f, 0.35f, 0.85f), () => new RzGate(System.Math.PI / 2));

        // ── Two-qubit ─────────────────────────────────────────────
        public static readonly GateInfo CNOT = new GateInfo("CX", "CNOT", 2,
            "Flip target if control is |1⟩ — creates entanglement",
            new Color(0.3f, 1f, 0.3f), () => new CNOTGate());

        public static readonly GateInfo CZ = new GateInfo("CZ", "CZ", 2,
            "Phase flip if both qubits are |1⟩",
            new Color(0.4f, 0.8f, 0.4f), () => new CZGate());

        public static readonly GateInfo SWAP = new GateInfo("SW", "SWAP", 2,
            "Exchange two qubit states",
            new Color(1f, 0.8f, 0.2f), () => new SWAPGate());

        // ── Three-qubit ───────────────────────────────────────────
        public static readonly GateInfo Toffoli = new GateInfo("CCX", "Toffoli", 3,
            "Flip target if both controls are |1⟩",
            new Color(1f, 0.6f, 0.2f), () => new ToffoliGate());

        public static readonly GateInfo Fredkin = new GateInfo("CSW", "Fredkin", 3,
            "Swap targets if control is |1⟩",
            new Color(1f, 0.7f, 0.3f), () => new FredkinGate());

        public static IReadOnlyList<GateInfo> SingleQubitGates => new[]
        {
            Hadamard, PauliX, PauliY, PauliZ, SPhase, TPhase, Rx, Ry, Rz
        };

        public static IReadOnlyList<GateInfo> MultiQubitGates => new[]
        {
            CNOT, CZ, SWAP, Toffoli, Fredkin
        };

        public static IReadOnlyList<GateInfo> AllGates
        {
            get
            {
                var all = new List<GateInfo>();
                all.AddRange(SingleQubitGates);
                all.AddRange(MultiQubitGates);
                return all;
            }
        }

        /// <summary>Find a GateInfo by matching the runtime gate type name.</summary>
        public static GateInfo Find(QuantumGate gate)
        {
            string name = gate.GetType().Name;
            foreach (var info in AllGates)
            {
                if (info.Create().GetType().Name == name)
                    return info;
            }
            return null;
        }
    }

    public class GateInfo
    {
        public string Symbol { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Color Color { get; }
        public int QubitCount { get; }

        private readonly System.Func<QuantumGate> _factory;

        public GateInfo(string symbol, string displayName, int qubitCount,
            string description, Color color, System.Func<QuantumGate> factory)
        {
            Symbol = symbol;
            DisplayName = displayName;
            QubitCount = qubitCount;
            Description = description;
            Color = color;
            _factory = factory;
        }

        /// <summary>Create a fresh gate instance.</summary>
        public QuantumGate Create() => _factory();
    }
}
