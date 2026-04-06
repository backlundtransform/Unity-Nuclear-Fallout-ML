using System.Text;
using System.Collections.Generic;
using QuantumCircuitViz.Core;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Export
{
    /// <summary>
    /// Exports a CircuitRunner's gate sequence to industry-standard formats.
    /// Supports OpenQASM 2.0 and JSON.
    /// </summary>
    public static class CircuitExporter
    {
        /// <summary>
        /// Export circuit to OpenQASM 2.0 string.
        /// Compatible with IBM Qiskit, Google Cirq, etc.
        /// </summary>
        public static string ToQASM(CircuitRunner runner)
        {
            var sb = new StringBuilder();
            sb.AppendLine("OPENQASM 2.0;");
            sb.AppendLine("include \"qelib1.inc\";");
            sb.AppendLine();
            sb.AppendLine($"qreg q[{runner.QubitCount}];");
            sb.AppendLine($"creg c[{runner.QubitCount}];");
            sb.AppendLine();

            foreach (var step in runner.Steps)
            {
                string line = GateToQASM(step.Gate, step.QubitIndices);
                if (line != null)
                    sb.AppendLine(line);
            }

            // Measurement at end
            for (int i = 0; i < runner.QubitCount; i++)
                sb.AppendLine($"measure q[{i}] -> c[{i}];");

            return sb.ToString();
        }

        /// <summary>
        /// Export circuit to a simple JSON representation.
        /// </summary>
        public static string ToJSON(CircuitRunner runner)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"qubitCount\": {runner.QubitCount},");
            sb.AppendLine($"  \"gateCount\": {runner.Steps.Count},");
            sb.AppendLine("  \"gates\": [");

            for (int i = 0; i < runner.Steps.Count; i++)
            {
                var step = runner.Steps[i];
                string name = GetGateName(step.Gate);
                string qubits = string.Join(", ", step.QubitIndices);
                string comma = i < runner.Steps.Count - 1 ? "," : "";
                sb.AppendLine($"    {{ \"gate\": \"{name}\", \"qubits\": [{qubits}] }}{comma}");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Export circuit as a compact text notation: H(0) CX(0,1) X(2)
        /// </summary>
        public static string ToTextNotation(CircuitRunner runner)
        {
            var parts = new List<string>();
            foreach (var step in runner.Steps)
            {
                string name = GetGateName(step.Gate);
                string qubits = string.Join(",", step.QubitIndices);
                parts.Add($"{name}({qubits})");
            }
            return $"[{runner.QubitCount}q] " + string.Join(" → ", parts);
        }

        private static string GateToQASM(QuantumGate gate, List<int> qubits)
        {
            string name = gate.GetType().Name;

            return name switch
            {
                "HadamardGate" => $"h q[{qubits[0]}];",
                "PauliXGate" => $"x q[{qubits[0]}];",
                "PauliYGate" => $"y q[{qubits[0]}];",
                "PauliZGate" => $"z q[{qubits[0]}];",
                "SGate" => $"s q[{qubits[0]}];",
                "TGate" => $"t q[{qubits[0]}];",
                "CNOTGate" => $"cx q[{qubits[0]}],q[{qubits[1]}];",
                "CZGate" => $"cz q[{qubits[0]}],q[{qubits[1]}];",
                "SWAPGate" => $"swap q[{qubits[0]}],q[{qubits[1]}];",
                "ToffoliGate" => $"ccx q[{qubits[0]}],q[{qubits[1]}],q[{qubits[2]}];",
                "FredkinGate" => $"cswap q[{qubits[0]}],q[{qubits[1]}],q[{qubits[2]}];",
                "RxGate" => $"rx(pi) q[{qubits[0]}];", // angle unknown at this level
                "RyGate" => $"ry(pi) q[{qubits[0]}];",
                "RzGate" => $"rz(pi) q[{qubits[0]}];",
                _ => $"// unsupported: {name}"
            };
        }

        private static string GetGateName(QuantumGate gate)
        {
            string name = gate.GetType().Name;
            return name switch
            {
                "HadamardGate" => "H",
                "PauliXGate" => "X",
                "PauliYGate" => "Y",
                "PauliZGate" => "Z",
                "SGate" => "S",
                "TGate" => "T",
                "CNOTGate" => "CX",
                "CZGate" => "CZ",
                "SWAPGate" => "SWAP",
                "ToffoliGate" => "CCX",
                "FredkinGate" => "CSWAP",
                "RxGate" => "Rx",
                "RyGate" => "Ry",
                "RzGate" => "Rz",
                _ => name.Replace("Gate", "")
            };
        }
    }
}
