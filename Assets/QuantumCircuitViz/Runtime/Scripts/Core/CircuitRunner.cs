using System.Collections.Generic;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Physics.Quantum;
using CSharpNumerics.Physics.Quantum.NoiseModels;

namespace QuantumCircuitViz.Core
{
    /// <summary>
    /// Fluent wrapper around CSharpNumerics QuantumCircuit / QuantumSimulator.
    /// Keeps a gate log so the visualisation layer can replay step-by-step.
    /// Supports both clean and noisy simulation.
    /// </summary>
    public class CircuitRunner
    {
        private readonly QuantumCircuit _circuit;
        private readonly QuantumSimulator _simulator = new QuantumSimulator();
        private readonly List<GateStep> _steps = new List<GateStep>();
        private NoisyQuantumSimulator _noisySim;

        public int QubitCount => _circuit.QubitCount;
        public IReadOnlyList<GateStep> Steps => _steps;

        public CircuitRunner(int qubitCount)
        {
            _circuit = new QuantumCircuit(qubitCount);
        }

        /// <summary>Create from a fluent QuantumCircuitBuilder circuit.</summary>
        public static CircuitRunner FromBuilder(QuantumCircuit circuit)
        {
            var runner = new CircuitRunner(circuit.QubitCount);
            foreach (var instr in circuit.Instructions)
            {
                runner._circuit.AddInstruction(instr);
                runner._steps.Add(new GateStep(instr.Gate, instr.QubitIndices));
            }
            return runner;
        }

        /// <summary>Configure noise channels for noisy simulation.</summary>
        public CircuitRunner WithNoise(float depolarizing = 0f, float dephasing = 0f, float amplitudeDamping = 0f)
        {
            if (depolarizing <= 0f && dephasing <= 0f && amplitudeDamping <= 0f)
            {
                _noisySim = null;
                return this;
            }

            _noisySim = new NoisyQuantumSimulator(new System.Random());
            if (depolarizing > 0f) _noisySim.WithNoise(new DepolarizingNoise(depolarizing));
            if (dephasing > 0f) _noisySim.WithNoise(new DephasingNoise(dephasing));
            if (amplitudeDamping > 0f) _noisySim.WithNoise(new AmplitudeDampingNoise(amplitudeDamping));
            return this;
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
            if (_noisySim != null)
                return _noisySim.Run(_circuit);
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
            if (_noisySim != null)
                return _noisySim.Run(partial);
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
