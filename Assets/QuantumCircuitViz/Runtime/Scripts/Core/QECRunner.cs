using System;
using System.Collections.Generic;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.Engines.Quantum.ErrorCorrection;
using CSharpNumerics.Numerics.Objects;
using CSharpNumerics.Physics.Quantum;
using CSharpNumerics.Physics.Quantum.ErrorCorrection;

namespace QuantumCircuitViz.Core
{
    /// <summary>
    /// Wraps CSharpNumerics classical QEC codes and ErrorCorrectionSimulator.
    /// Runs encode → noise → syndrome decode → correct → measure fidelity.
    /// Also provides Monte-Carlo fidelity comparison (protected vs unprotected).
    /// </summary>
    public class QECRunner
    {
        private readonly IQuantumErrorCorrectionCode _code;
        private readonly ErrorCorrectionSimulator _simulator;
        private readonly SyndromeDecoder _decoder;
        private readonly Random _rng;

        public IQuantumErrorCorrectionCode Code => _code;
        public int PhysicalQubits => _code.PhysicalQubits;
        public int LogicalQubits => _code.LogicalQubits;
        public int Distance => _code.Distance;

        public QECRunner(QECCodeType codeType, int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _code = CreateCode(codeType);
            _simulator = new ErrorCorrectionSimulator();
            _decoder = new SyndromeDecoder(_code);
        }

        /// <summary>
        /// Run a single error-correction cycle.
        /// Injects random errors at the given rate and attempts correction.
        /// Returns fidelity of recovered state vs ideal.
        /// </summary>
        public QECResult RunOnce(ComplexVectorN initialState, double errorRate)
        {
            var errorGates = GenerateRandomErrors(errorRate);
            ErrorCorrectionSimulator.Result simResult;

            switch (_code)
            {
                case BitFlipCode3 bf:
                    simResult = _simulator.RunBitFlipCorrection(bf, initialState, errorGates, _rng);
                    break;
                case PhaseFlipCode3 pf:
                    simResult = _simulator.RunPhaseFlipCorrection(pf, initialState, errorGates, _rng);
                    break;
                case ShorCode9 sh:
                    simResult = _simulator.RunShorCorrection(sh, initialState, errorGates, _rng);
                    break;
                case SteaneCode7 st:
                    simResult = _simulator.RunSteaneCorrection(st, initialState, errorGates, _rng);
                    break;
                default:
                    throw new NotSupportedException($"Unknown QEC code type: {_code.GetType().Name}");
            }

            return new QECResult
            {
                Fidelity = simResult.Fidelity,
                Syndrome = simResult.Syndrome,
                Corrections = simResult.Corrections,
                RecoveredState = simResult.RecoveredState,
                ErrorsInjected = errorGates.Count
            };
        }

        /// <summary>
        /// Monte-Carlo comparison: run many rounds, return average fidelity
        /// for protected (QEC) vs unprotected circuits.
        /// </summary>
        public QECComparison RunComparison(ComplexVectorN initialState, double errorRate, int rounds)
        {
            if (_code is BitFlipCode3 bf)
            {
                var (protFid, unprotFid) = _simulator.RunMonteCarloComparison(
                    bf, initialState, errorRate, rounds, _rng);
                return new QECComparison
                {
                    ProtectedFidelity = protFid,
                    UnprotectedFidelity = unprotFid,
                    Rounds = rounds,
                    ErrorRate = errorRate
                };
            }

            // For other codes, run manually
            double protSum = 0, unprotSum = 0;
            var sim = new QuantumSimulator();
            var noisySim = new CSharpNumerics.Engines.Quantum.NoisyQuantumSimulator(_rng);
            noisySim.WithNoise(new CSharpNumerics.Physics.Quantum.NoiseModels.DepolarizingNoise(errorRate));

            for (int r = 0; r < rounds; r++)
            {
                var result = RunOnce(initialState, errorRate);
                protSum += result.Fidelity;

                // Unprotected: just run noisy circuit without correction
                var circuit = new QuantumCircuit(1);
                circuit.AddInstruction(new QuantumInstruction(new HadamardGate(), new List<int> { 0 }));
                var noisyState = noisySim.Run(circuit);
                var idealState = sim.Run(circuit);
                unprotSum += QuantumFidelity.Fidelity(noisyState, idealState);
            }

            return new QECComparison
            {
                ProtectedFidelity = protSum / rounds,
                UnprotectedFidelity = unprotSum / rounds,
                Rounds = rounds,
                ErrorRate = errorRate
            };
        }

        /// <summary>
        /// Decode a syndrome to get correction operations.
        /// </summary>
        public List<(int qubit, char pauli)> DecodeSyndrome(int syndrome)
        {
            return _decoder.Decode(syndrome);
        }

        /// <summary>Get stabilizer generators for display.</summary>
        public List<List<(int qubit, char pauli)>> GetStabilizers()
        {
            return _code.GetStabilizers();
        }

        private List<(QuantumGate gate, int qubit)> GenerateRandomErrors(double errorRate)
        {
            var errors = new List<(QuantumGate gate, int qubit)>();
            for (int q = 0; q < _code.PhysicalQubits; q++)
            {
                if (_rng.NextDouble() < errorRate)
                {
                    // Random Pauli error
                    int pauli = _rng.Next(3);
                    QuantumGate gate = pauli switch
                    {
                        0 => new PauliXGate(),
                        1 => new PauliYGate(),
                        _ => new PauliZGate()
                    };
                    errors.Add((gate, q));
                }
            }
            return errors;
        }

        private static IQuantumErrorCorrectionCode CreateCode(QECCodeType type)
        {
            return type switch
            {
                QECCodeType.BitFlip3 => new BitFlipCode3(),
                QECCodeType.PhaseFlip3 => new PhaseFlipCode3(),
                QECCodeType.Steane7 => new SteaneCode7(),
                QECCodeType.Shor9 => new ShorCode9(),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
    }

    public class QECResult
    {
        public double Fidelity { get; set; }
        public int Syndrome { get; set; }
        public List<(int qubit, char pauli)> Corrections { get; set; }
        public QuantumState RecoveredState { get; set; }
        public int ErrorsInjected { get; set; }
    }

    public class QECComparison
    {
        public double ProtectedFidelity { get; set; }
        public double UnprotectedFidelity { get; set; }
        public int Rounds { get; set; }
        public double ErrorRate { get; set; }
        public double Improvement => ProtectedFidelity - UnprotectedFidelity;
    }
}
