using System.Collections.Generic;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Core
{
    /// <summary>
    /// Registry of all available quantum gates with display metadata.
    /// Used by the UI gate palette and circuit diagram renderer.
    /// </summary>
    public static class GateLibrary
    {
        // Single-qubit
        public static readonly GateInfo Hadamard = new GateInfo("H", "Hadamard", 1, () => new HadamardGate());
        public static readonly GateInfo PauliX   = new GateInfo("X", "Pauli-X", 1, () => new PauliXGate());
        public static readonly GateInfo PauliY   = new GateInfo("Y", "Pauli-Y", 1, () => new PauliYGate());
        public static readonly GateInfo PauliZ   = new GateInfo("Z", "Pauli-Z", 1, () => new PauliZGate());
        public static readonly GateInfo SPhase   = new GateInfo("S", "S Gate", 1, () => new CSharpNumerics.Physics.Quantum.SGate());
        public static readonly GateInfo TPhase   = new GateInfo("T", "T Gate", 1, () => new CSharpNumerics.Physics.Quantum.TGate());

        // Two-qubit
        public static readonly GateInfo CNOT     = new GateInfo("CX", "CNOT", 2, () => new CNOTGate());
        public static readonly GateInfo CZ       = new GateInfo("CZ", "CZ", 2, () => new CZGate());
        public static readonly GateInfo SWAP     = new GateInfo("SW", "SWAP", 2, () => new SWAPGate());

        // Three-qubit
        public static readonly GateInfo Toffoli  = new GateInfo("CCX", "Toffoli", 3, () => new ToffoliGate());
        public static readonly GateInfo Fredkin  = new GateInfo("CSW", "Fredkin", 3, () => new FredkinGate());

        public static IReadOnlyList<GateInfo> SingleQubitGates => new[]
        {
            Hadamard, PauliX, PauliY, PauliZ, SPhase, TPhase
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
    }

    public class GateInfo
    {
        public string Symbol { get; }
        public string DisplayName { get; }
        public int QubitCount { get; }

        private readonly System.Func<QuantumGate> _factory;

        public GateInfo(string symbol, string displayName, int qubitCount, System.Func<QuantumGate> factory)
        {
            Symbol = symbol;
            DisplayName = displayName;
            QubitCount = qubitCount;
            _factory = factory;
        }

        /// <summary>Create a fresh gate instance.</summary>
        public QuantumGate Create() => _factory();
    }
}
