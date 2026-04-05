# Quantum Circuit Visualizer

**Interactive quantum circuit builder and visualizer for Unity** — powered by [CSharpNumerics](https://csnumerics.com/).

Build quantum circuits, step through gate-by-gate, watch Bloch spheres evolve in real time, run measurement sampling, and explore quantum error-correction — all from a single drag-and-drop component.

## Features

### Core Visualization
- **Bloch Sphere Rendering** — procedural 3D spheres with wireframe, axis labels (|0⟩, |1⟩, |+⟩, |−⟩, |i⟩, |−i⟩), and animated state vectors
- **Circuit Diagram** — 2D UI circuit diagram with gate symbols and step highlighting
- **3D World-Space Circuit** — hologram-style 3D circuit with glowing wires, gate cubes, and control dots
- **Measurement Histogram** — probability bars with animated shot-by-shot sampling (256 shots)
- **Density Matrix Heatmap** — full ρ = |ψ⟩⟨ψ| visualization, magnitude → brightness, phase → hue
- **Entanglement Visualizer** — glowing lines between entangled qubits

### Interactive Circuit Builder
- **Gate Palette** — clickable panel with all 11 gate types (H, X, Y, Z, S, T, CNOT, CZ, SWAP, Toffoli, Fredkin)
- **Circuit Canvas** — grid-based drag-and-drop circuit editor with multi-qubit gate placement
- **7 Preset Circuits** — Bell State, GHZ, Superposition Chain, Phase Kickback, Grover Search, Toffoli, QFT

### Noise & Error Correction
- **Noisy Simulation** — depolarizing, dephasing, and amplitude damping noise channels
- **Quantum Error Correction** — 4 QEC codes (BitFlip-3, PhaseFlip-3, Steane-7, Shor-9) with Monte-Carlo fidelity comparison
- **RL Error Correction** — DQN/DoubleDQN agent learns gate sequences on a background thread
- **Error Heatmap** — per-qubit error rate overlay
- **Noise Glitch VFX** — visual jitter on Bloch spheres proportional to error rate

### Export & Polish
- **OpenQASM 2.0 Export** — copy circuits to clipboard, compatible with Qiskit/Cirq
- **JSON Export** — structured gate list
- **Screenshot Capture** — supersampled PNG export (F12)
- **Custom Editor Inspector** — preset buttons, export controls, step navigation in the Inspector

## Quick Start

1. **Import** the package into your Unity project (2021.3+)
2. Ensure `CSharpNumerics.dll` is in `Assets/Plugins/CSharpNumerics/`
3. Create an empty GameObject in any scene
4. Attach the `DemoSimulation` component
5. Enter Play Mode — the demo creates its own camera, canvas, and all visuals automatically

No scene setup, no materials, no prefabs required.

## Views

The demo uses a **view system** — press **Tab** to cycle between views. Each view shows only its relevant UI, keeping the screen clean and readable.

| View | What you see | Key features |
|------|-------------|--------------|
| **Bloch** | Bloch spheres only | Focused qubit-state view without extra UI. |
| **Histogram** | Measurement histogram only | Clean probability view for measurement outcomes. |
| **Circuit** | Circuit diagram only | Clear gate-order and step-by-step overview. |

## Keyboard Controls

| Key | Action |
|-----|--------|
| Tab | Cycle view forward (Shift+Tab = backward) |
| ← / → | Step backward / forward through gates |
| Space | Play / pause auto-advance |
| R | Reset to \|0…0⟩ |
| N | Toggle noise on/off |
| M | Run measurement sampling animation (Histogram view) |
| F5 | Copy OpenQASM to clipboard |
| F12 | Take screenshot |
| 1–7 | Load preset circuits |

## Preset Circuits

| # | Circuit | Qubits | Description |
|---|---------|--------|-------------|
| 1 | Bell State | 2 | H → CNOT — maximally entangled pair |
| 2 | GHZ State | 3 | H → CNOT → CNOT — 3-qubit entanglement |
| 3 | Superposition Chain | 1–5 | Hadamard on every qubit |
| 4 | Phase Kickback | 2 | Deutsch-like interference |
| 5 | Grover Search | 3 | Amplitude amplification (target \|101⟩) |
| 6 | Toffoli | 3 | X X → CCX controlled-controlled-NOT |
| 7 | QFT | 3 | Quantum Fourier Transform |

## Project Structure

```
QuantumCircuitViz/
├── package.json
├── README.md
├── Runtime/
│   ├── QuantumCircuitViz.Runtime.asmdef
│   └── Scripts/
│       ├── Core/
│       │   ├── CircuitRunner.cs          — Gate replay & step-by-step simulation
│       │   ├── GateLibrary.cs            — Registry of 11 gate types
│       │   ├── SimulationConfig.cs       — Inspector-friendly config
│       │   ├── QECRunner.cs              — Quantum error correction wrapper
│       │   └── RLErrorCorrectionRunner.cs — Background DQN training
│       ├── Visualization/
│       │   ├── BlochSphereRenderer.cs    — Procedural 3D Bloch sphere
│       │   ├── MeasurementHistogram.cs   — Probability bars + sampling
│       │   ├── CircuitDiagramRenderer.cs — 2D UI circuit diagram
│       │   ├── DensityMatrixHeatmap.cs   — ρ heatmap (magnitude/phase)
│       │   ├── WorldSpaceCircuit.cs      — 3D hologram circuit
│       │   ├── EntanglementVisualizer.cs — Qubit entanglement links
│       │   ├── QubitInspectorPanel.cs    — Click-to-inspect panel
│       │   ├── ErrorCorrectionVisualizer.cs
│       │   ├── RLTrainingPanel.cs
│       │   ├── ErrorHeatmapOverlay.cs
│       │   └── NoiseGlitchEffect.cs
│       ├── UI/
│       │   ├── GatePalette.cs            — Gate selection panel
│       │   └── CircuitCanvas.cs          — Drag-and-drop circuit grid
│       └── Export/
│           ├── CircuitExporter.cs        — QASM / JSON / text export
│           └── ScreenshotUtility.cs      — Screenshot capture
├── Editor/
│   ├── QuantumCircuitViz.Editor.asmdef
│   └── Scripts/
│       └── QuantumSimulationEditor.cs    — Custom Inspector
└── Demo/
    ├── QuantumCircuitViz.Demo.asmdef
    └── Scripts/
        └── DemoSimulation.cs             — Zero-setup demo MonoBehaviour
```

## CSharpNumerics APIs Used

| API | Namespace | Purpose |
|-----|-----------|---------|
| `QuantumCircuit` | `CSharpNumerics.Engines.Quantum` | Circuit definition |
| `QuantumCircuitBuilder` | `CSharpNumerics.Engines.Quantum` | Fluent circuit construction |
| `QuantumSimulator` | `CSharpNumerics.Engines.Quantum` | Clean state evolution |
| `NoisyQuantumSimulator` | `CSharpNumerics.Engines.Quantum` | Noisy simulation |
| `QuantumState` | `CSharpNumerics.Engines.Quantum` | State vector, probabilities, Bloch |
| `GroverSearch` | `CSharpNumerics.Engines.Quantum.Algorithms` | Grover circuit generation |
| `ErrorCorrectionSimulator` | `CSharpNumerics.Physics.Quantum.ErrorCorrection` | QEC codes |
| `DQN` / `DoubleDQN` | `CSharpNumerics.ML.ReinforcementLearning` | RL error correction |

## Requirements

- **Unity** 2021.3+ (tested on Unity 6 / 6000.3.9f1)
- **CSharpNumerics** v2.8.0+ (netstandard2.1 DLL)
- No additional packages required — Input System support is optional via `versionDefines`

## License

See the [LICENSE](../../LICENSE) file in the repository root.
