using System.Collections.Generic;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Core
{
    /// <summary>
    /// Fluent wrapper around CSharpNumerics QuantumCircuit / QuantumSimulator.
    /// Keeps a gate log so the visualisation layer can replay step-by-step.
    /// </summary>
    public class CircuitRunner
    {
        private readonly QuantumCircuit _circuit;
        private readonly QuantumSimulator _simulator = new QuantumSimulator();
        private readonly List<GateStep> _steps = new List<GateStep>();

        public int QubitCount => _circuit.QubitCount;
        public IReadOnlyList<GateStep> Steps => _steps;

        public CircuitRunner(int qubitCount)
        {
            _circuit = new QuantumCircuit(qubitCount);
        }

        /// <summary>Add a single-qubit gate.</summary>
        public CircuitRunner Add(QuantumGate gate, int qubit)
        {
            var indices = new List<int> { qubit };
            _circuit.AddInstruction(new QuantumInstruction(gate, indices));
            _steps.Add(new GateStep(gate, indices));
            return this;
        }

        /// <summary>Add a multi-qubit gate (e.g. CNOT).</summary>
        public CircuitRunner Add(QuantumGate gate, params int[] qubits)
        {
            var indices = new List<int>(qubits);
            _circuit.AddInstruction(new QuantumInstruction(gate, indices));
            _steps.Add(new GateStep(gate, indices));
            return this;
        }

        /// <summary>Run the full circuit and return the final state.</summary>
        public QuantumState Run()
        {
            return _simulator.Run(_circuit);
        }

        /// <summary>
        /// Run the circuit up to (and including) the given step index.
        /// Useful for step-by-step animation.
        /// </summary>
        public QuantumState RunUpTo(int stepIndex)
        {
            var partial = new QuantumCircuit(QubitCount);
            for (int i = 0; i <= stepIndex && i < _steps.Count; i++)
            {
                partial.AddInstruction(
                    new QuantumInstruction(_steps[i].Gate, _steps[i].QubitIndices));
            }
            return _simulator.Run(partial);
        }
    }

    /// <summary>One gate application with its target qubits — used for replay.</summary>
    public class GateStep
    {
        public QuantumGate Gate { get; }
        public List<int> QubitIndices { get; }

        public GateStep(QuantumGate gate, List<int> qubitIndices)
        {
            Gate = gate;
            QubitIndices = qubitIndices;
        }
    }
}
