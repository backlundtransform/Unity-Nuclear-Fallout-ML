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
        public static readonly GateInfo Hadamard = new GateInfo("H", "Hadamard", 1, () => new HadamardGate());
        public static readonly GateInfo PauliX   = new GateInfo("X", "Pauli-X", 1, () => new PauliXGate());
        public static readonly GateInfo PauliZ   = new GateInfo("Z", "Pauli-Z", 1, () => new PauliZGate());
        public static readonly GateInfo SGate    = new GateInfo("S", "S Gate", 1, () => new SGate());
        public static readonly GateInfo TGate    = new GateInfo("T", "T Gate", 1, () => new TGate());
        public static readonly GateInfo CNOT     = new GateInfo("CX", "CNOT", 2, () => new CNOTGate());
        public static readonly GateInfo CZ       = new GateInfo("CZ", "CZ", 2, () => new CZGate());
        public static readonly GateInfo SWAP     = new GateInfo("SW", "SWAP", 2, () => new SWAPGate());

        public static IReadOnlyList<GateInfo> SingleQubitGates => new[]
        {
            Hadamard, PauliX, PauliZ, SGate, TGate
        };

        public static IReadOnlyList<GateInfo> MultiQubitGates => new[]
        {
            CNOT, CZ, SWAP
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
