# QuantumCircuitViz — Unity Asset Roadmap

> **Free marketing asset** for [CSharpNumerics](https://csnumerics.com/) — showcases the Quantum Engine by letting users build quantum circuits, run RL-based error correction, and explore qubit states on interactive Bloch spheres — all inside Unity.

---

## 1. Vision & Marknadsföringsvärde

| Mål | Beskrivning |
|-----|-------------|
| **Visuellt WOW** | 3D Bloch-sfärer med glödande state-vektorer, animerade gate-operationer, partikeleffekter vid mätning — ser ut som en sci-fi kvantdator |
| **Interaktivt** | Drag-and-drop kretsar, klicka på qubits för att se state evolution i realtid |
| **Pedagogiskt** | Perfekt för kurser, YouTube-demos, konferenspresentationer |
| **CSharpNumerics-showcase** | Visar att biblioteket hanterar kvantberäkningar, RL-träning och visualisering i en ren C#-pipeline |

---

## 2. CSharpNumerics Quantum Engine API:er (bekräftade)

*Submodule uppdaterad till `489b51b` (BlochVector). Alla namespaces verifierade.*

| Namespace | Klasser |
|-----------|---------|
| `CSharpNumerics.Engines.Quantum` | `QuantumCircuit`, `QuantumInstruction`, `QuantumSimulator`, `QuantumState` |
| `CSharpNumerics.Physics.Quantum` | `QuantumGate` (abstract), `BlochVector` |
| `CSharpNumerics.Physics.Quantum` | `HadamardGate`, `PauliXGate`, `PauliZGate`, `SGate`, `TGate` |
| `CSharpNumerics.Physics.Quantum` | `RxGate`, `RyGate`, `RzGate` (rotation gates med θ-parameter) |
| `CSharpNumerics.Physics.Quantum` | `CNOTGate`, `CZGate`, `SWAPGate` (multi-qubit) |
| `CSharpNumerics.ML.ReinforcementLearning` | `IEnvironment`, `IAgent` — RL-policies för error correction (framtid) |

### Nyckel-API:er

```csharp
// Bygg krets
var circuit = new QuantumCircuit(2);
circuit.AddInstruction(new QuantumInstruction(new HadamardGate(), new List<int>{0}));
circuit.AddInstruction(new QuantumInstruction(new CNOTGate(), new List<int>{0, 1}));

// Simulera
var simulator = new QuantumSimulator();
QuantumState state = simulator.Run(circuit);

// Mät
VectorN probs = state.GetProbabilities(); // [0.5, 0, 0, 0.5] för Bell state

// Bloch-sfär (single-qubit)
BlochVector bloch = state.GetBlochVector(); // X, Y, Z, Theta, Phi, Radius
Vector v = bloch.ToVector(); // 3D-vektor för rendering
```

### Saknas (krävs för Fas 3 — Error Correction)
- `NoiseModel` / `ErrorChannel` (depolarizing, bit-flip, phase-flip)
- `QuantumErrorCorrectionEnv : IEnvironment` (RL-miljö)
- Error correction codes (Shor, Steane, Surface)

---

## 3. Visuell Design i Unity 3D

### 3.1 Bloch-sfär (hjärtat i visualiseringen)

```
             |z⟩ = |0⟩
              ●
             /|\
            / | \
           /  |  \    ← Semi-transparent sfär med latitude/longitude-linjer
          /   |   \
   |−x⟩ ●----●----● |+x⟩
          \   |   /
           \  |  /
            \ | /
             \|/
              ●
             |1⟩
```

**Unity-implementation:**
- **Sfär:** Semi-transparent shader (URP/HDRP Lit med alpha ~0.15), wireframe overlay med latitude/longitude-arcs (LineRenderer eller procedural mesh)
- **State-vektor:** Glödande pil från origo till ytan — `LineRenderer` med HDR emissive material + liten sfär-gizmo på spetsen
- **Axlar:** X, Y, Z-axlar med labels (|0⟩, |1⟩, |+⟩, |−⟩, |i⟩, |−i⟩)
- **Animation:** Smooth Slerp-interpolation vid gate-operationer (Hadamard roterar vektorn 90° runt Y → X)
- **Partikeleffekt:** Burst av partiklar vid mätning (collapse) — blå för |0⟩, röd för |1⟩
- **Multi-qubit:** En Bloch-sfär per qubit, placerade i en rad. Entanglade qubits kopplas visuellt med glödande "quantum link"-linjer

**Shader-förslag:**
```
Bloch Sphere Material:
  - Base: Transparent URP/Lit, albedo dark blue, alpha 0.12
  - Fresnel rim glow: Cyan/teal HDR emission
  - Wireframe overlay: Procedural grid shader eller LineRenderer arcs

State Vector Arrow:
  - HDR emissive (glow bloom) i magenta/vit
  - Trail Renderer för motion blur vid rotation
```

### 3.2 Kretskort-editor (Circuit Builder)

**Layout — horisontell tidslinje:**
```
q₀ ──[H]──●──[M]──
           |
q₁ ──────[X]──[M]──
```

**Unity-implementation (2 alternativ):**

**Alt A — UI Toolkit / uGUI (2D overlay):**
- Canvas-baserad drag-and-drop
- Gate-ikoner som UI-knappar i en palett till vänster
- Qubit-linjer som horisontella Image-strips
- Bra för: snabb implementation, tydlig UX, fungerar i WebGL

**Alt B — 3D World Space (rekommenderat för marketing):**
- Kretsen renderas som ett 3D-objekt i scenen (flat board med glödande ledningar)
- Gates är 3D-objekt (kuber/cylindrar med ikoner) som kan dras längs ledningarna
- Kamera orbitar runt kretsen → Bloch-sfärerna flyter bredvid
- Neon/hologram-estetik — **sci-fi quantum lab**-känsla
- Partikeleffekter flödar längs ledningarna vid simulering (photon-liknande bursts)

**Rekommendation:** Bygg Alt A först (snabbare, fungerar), Alt B som polish i fas 3.

### 3.3 Mätnings- och resultatvy

- **Histogram:** 3D bar chart med glödande staplar — varje outcome (|00⟩, |01⟩, |10⟩, |11⟩) har en stapel
- **Animerad sampling:** Varje "shot" animeras som en partikel som landar i rätt stapel
- **Sannolikhetsfördelning:** Realtids-update synkad med kretsändringar (ändra en gate → se histogram förändras live)

### 3.4 RL Error Correction Visualization

- **Noise-indikering:** Qubits "glitchar" visuellt (shader distortion, color jitter) när noise appliceras
- **RL-agent-vy:** Panel som visar agentens beslutsprocess — vilka korrigeringsåtgärder den väljer
- **Träningskurva:** Realtids-linjegraf som visar fidelity över episoder
- **Före/efter:** Split-view — vänster: noisy krets med errors, höger: RL-korrigerad krets med högre fidelity
- **Heatmap:** Error-rates per qubit renderat som färgad overlay på kretsen

### 3.5 Övergripande visuell stil

| Element | Stil |
|---------|------|
| **Bakgrund** | Mörk (#0a0a1a) med subtilt grid/starfield |
| **Primärfärg** | Cyan/teal (#00e5ff) — "quantum blue" |
| **Sekundärfärg** | Magenta/lila (#e040fb) — state vectors |
| **Accent** | Guld (#ffd740) — mätresultat |
| **Typsnitt** | Monospace (JetBrains Mono / Consolas) för state-labels |
| **Post-processing** | Bloom (HDR glow), chromatic aberration (subtle), vignette |
| **Partikeleffekter** | Använd VFX Graph för photon-liknande partiklar |

---

## 4. Asset-struktur

```
Assets/QuantumCircuitViz/
├── package.json
├── README.md
├── Runtime/
│   ├── QuantumCircuitViz.Runtime.asmdef
│   └── Scripts/
│       ├── Core/
│       │   ├── QuantumSimulationManager.cs    ← Orchestrerar simulering
│       │   ├── SimulationConfig.cs            ← [Serializable] inspector-konfiguration
│       │   └── QuantumState.cs                ← Unity-vänlig state wrapper
│       ├── Circuits/
│       │   ├── CircuitBuilder.cs              ← Bygger QuantumCircuit via CSharpNumerics
│       │   ├── GateDefinitions.cs             ← Gate metadata (namn, ikon, matris)
│       │   └── CircuitValidator.cs            ← Validerar krets innan simulering
│       ├── Visualization/
│       │   ├── BlochSphereRenderer.cs         ← 3D Bloch-sfär med state-vektor
│       │   ├── CircuitDiagramRenderer.cs      ← Renderar kretskort (2D/3D)
│       │   ├── MeasurementHistogram.cs        ← 3D bar chart för mätresultat
│       │   ├── QuantumStateHeatmap.cs         ← Density matrix som heatmap
│       │   └── EntanglementVisualizer.cs      ← Visuella länkar mellan entanglade qubits
│       ├── ErrorCorrection/
│       │   ├── NoiseSimulationBridge.cs       ← CSharpNumerics noise model wrapper
│       │   ├── RLErrorCorrectionRunner.cs     ← Kör RL-agent för error correction
│       │   └── ErrorCorrectionVisualizer.cs   ← Visar noise + correction visuellt
│       ├── UI/
│       │   ├── GatePalette.cs                 ← Drag-and-drop gate-urval
│       │   ├── CircuitCanvas.cs               ← Drop-target för gates
│       │   ├── QubitInspectorPanel.cs         ← Visar qubit state detaljer
│       │   └── RLTrainingPanel.cs             ← RL-träningsstatistik
│       └── Export/
│           ├── CircuitExporter.cs             ← Exportera krets som QASM/JSON
│           └── ScreenshotExporter.cs          ← Spara visualiseringar som PNG
├── Editor/
│   ├── QuantumCircuitViz.Editor.asmdef
│   └── Scripts/
│       ├── QuantumSimulationEditor.cs         ← Custom inspector
│       └── CircuitPresetWizard.cs             ← Wizard för vanliga kretsar
└── Demo/
    ├── QuantumCircuitViz.Demo.asmdef
    └── Scripts/
        └── DemoSimulation.cs                  ← Zero-setup demo
```

---

## 5. Roadmap — Faser

### Fas 0: CSharpNumerics Quantum Engine ✅ (klart)
> *Grundläggande quantum engine finns i submodulen*

| # | Uppgift | Status |
|---|---------|--------|
| 0.1 | Quantum circuit builder API (`QuantumCircuit`, `QuantumInstruction`) | ✅ Klart |
| 0.2 | Standard gates (H, X, Z, S, T, CNOT, CZ, SWAP, Rx/Ry/Rz) | ✅ 13 gates |
| 0.3 | State vector simulator (`QuantumSimulator`) | ✅ Klart |
| 0.4 | Measurement & probability distribution (`GetProbabilities()`) | ✅ Klart |
| 0.5 | Bloch sphere (`BlochVector.FromAmplitudes()`, `.ToVector()`) | ✅ Klart |
| 0.6 | Noise models (depolarizing, bit-flip, phase-flip) | 🔲 Nästa steg |
| 0.7 | Error correction codes (Shor, Steane, Surface) | 🔲 Planerat |
| 0.8 | RL environment for error correction | 🔲 Planerat |
| 0.9 | Unit tests (50+ tester finns) | ✅ Klart |

### Fas 1: Scaffold & Bloch Sphere (v1.0) — "First Light" 🔨 PÅGÅR
> *Mål: En roterande Bloch-sfär som visar qubit state i realtid*

| # | Uppgift | Status |
|---|---------|--------|
| 1.1 | Skapa `Assets/QuantumCircuitViz/` med package.json, asmdef-filer | ✅ Klart |
| 1.2 | Uppdatera CSharpNumerics submodule till commit med Quantum Engine | ✅ Klart |
| 1.3 | Bygg ny CSharpNumerics.dll med quantum-stöd | ✅ Klart |
| 1.4 | `BlochSphereRenderer.cs` — semi-transparent sfär, axlar, state-vektor | ✅ Klart |
| 1.5 | `CircuitRunner.cs` — pipeline: skapa krets → stega → visa state | ✅ Klart |
| 1.6 | `DemoSimulation.cs` — zero-setup demo med 4 preset-kretsar | ✅ Klart |
| 1.7 | `MeasurementHistogram.cs` — sannolikheter per basis state | ✅ Klart |
| 1.8 | `CircuitDiagramRenderer.cs` — ASCII-krets med steg-markör | ✅ Klart |
| 1.9 | Post-processing setup (Bloom, mörk bakgrund) | P1 |
| 1.10 | Multi-qubit reduced Bloch vectors (partial trace) | ✅ Klart |

**Leverans:** Attach DemoSimulation → Play Mode → ser en glödande Bloch-sfär som animerar genom H → X → Z gates.

### Fas 2: Circuit Builder & Measurement (v2.0) — "Build & Measure"
> *Mål: Användare bygger kretsar visuellt och ser mätresultat*

| # | Uppgift | Prio |
|---|---------|------|
| 2.1 | `CircuitBuilder.cs` — wrappa CSharpNumerics circuit API | P0 |
| 2.2 | `CircuitDiagramRenderer.cs` — renderar qubit-linjer och gate-symboler | P0 |
| 2.3 | `GatePalette.cs` — UI-panel med tillgängliga gates | P0 |
| 2.4 | Drag-and-drop: dra gates från paletten till kretsen | P0 |
| 2.5 | Multi-qubit stöd (2–5 qubits), en Bloch-sfär per qubit | P0 |
| 2.6 | `MeasurementHistogram.cs` — 3D histogram som visar sannolikheter | P1 |
| 2.7 | Animerat mätningsflöde (partiklar → histogram) | P1 |
| 2.8 | `EntanglementVisualizer.cs` — glödande linjer mellan entanglade qubits | P1 |
| 2.9 | Preset-kretsar: Bell State, GHZ, Teleportation, Deutsch-Jozsa | P1 |
| 2.10 | `QubitInspectorPanel.cs` — klicka på qubit → se amplitud, fas, sannolikhet | P2 |

**Leverans:** Drag-and-drop circuit builder med live Bloch-sfärer och mätnings-histogram. Redo för screenshots.

### Fas 3: RL Error Correction (v3.0) — "Self-Healing Qubits"
> *Mål: Visa hur RL-agenter korrigerar kvantfel i realtid*

| # | Uppgift | Prio |
|---|---------|------|
| 3.1 | `NoiseSimulationBridge.cs` — applicera noise models från CSharpNumerics | P0 |
| 3.2 | `RLErrorCorrectionRunner.cs` — kör RL-agent (DQN/PPO) i bakgrundstråd | P0 |
| 3.3 | `ErrorCorrectionVisualizer.cs` — visuell noise + correction | P0 |
| 3.4 | Noise-shader: qubits "glitchar" visuellt proportionellt mot error rate | P1 |
| 3.5 | `RLTrainingPanel.cs` — realtids linjegraf med fidelity over episodes | P1 |
| 3.6 | Split-view: noisy vs korrigerad krets | P1 |
| 3.7 | Steg-för-steg replay av RL-agentens beslut | P2 |
| 3.8 | Jämförelse: RL vs klassisk error correction (Shor/Steane) | P2 |
| 3.9 | Error rate heatmap overlay på kretsen | P2 |

**Leverans:** Demoscen där noise appliceras → RL-agent tränas live → fidelity förbättras visuellt. Perfekt för video-demo.

### Fas 4: Polish & Export (v4.0) — "Marketing Ready"
> *Mål: Asset Store-redo, exportfunktioner, maxad visuell kvalitet*

| # | Uppgift | Prio |
|---|---------|------|
| 4.1 | 3D world-space circuit (hologram-estetik) som alternativ till 2D UI | P1 |
| 4.2 | VFX Graph-partiklar: photon-flöde längs ledningar | P1 |
| 4.3 | Density matrix heatmap (`QuantumStateHeatmap.cs`) | P1 |
| 4.4 | QASM-export (`CircuitExporter.cs`) | P2 |
| 4.5 | Screenshot/video capture utility | P2 |
| 4.6 | Custom inspectors med live-preview i Editor | P2 |
| 4.7 | README, dokumentation, Asset Store-beskrivning | P0 |
| 4.8 | WebGL build + demo-sida | P1 |
| 4.9 | Promotional screenshots och GIF-animationer | P1 |

---

## 6. Demo-scenarier (marknadsföring)

### Demo 1: "Quantum Hello World"
- Skapar en qubit i |0⟩
- Applicerar Hadamard → Bloch-sfär animerar till ekvatorn
- Mäter → partikelexplosion → resultat visas
- **Tid:** 10 sekunder, perfekt för Twitter/X-clip

### Demo 2: "Bell State Entanglement"
- Två qubits, Hadamard + CNOT
- Bloch-sfärerna kopplas med glödande linje
- Mätning på en qubit → den andra kollapsar samtidigt
- **Visar:** Entanglement visuellt

### Demo 3: "Noisy Quantum Computer"
- 3-qubit krets med noise
- Qubits glitchar, fidelity sjunker
- RL-agent aktiveras → qubits stabiliseras
- Fidelity-graf klättrar uppåt
- **Visar:** CSharpNumerics RL + Quantum integration

### Demo 4: "Quantum Teleportation Protocol"
- Klassisk teleportation-krets (3 qubits)
- Steg-för-steg animation med förklaring
- **Visar:** Pedagogiskt värde, kursanvändning

---

## 7. Tekniska beslut

| Beslut | Val | Motivering |
|--------|-----|------------|
| **Render pipeline** | URP | Bred kompatibilitet, Bloom via post-processing |
| **UI-system** | uGUI (Canvas) | Beprövat, fungerar i WebGL, Input System-stöd |
| **Bloch-sfär mesh** | Procedural vid runtime | Inga externa assets behövs, full kontroll |
| **Animationer** | Koroutiner + DOTween (valfritt) | Smooth Slerp/Lerp för state transitions |
| **Threading** | `Task.Run` för simulering | Samma mönster som NuclearFalloutML |
| **Max qubits i demo** | 5 (32 states) | Visuellt hanterbart, state vector-sim klarar det |
| **Minimum Unity** | 2021.3 LTS | Matchar NuclearFalloutML |

---

## 8. Risker & beroenden

| Risk | Mitigation |
|------|------------|
| Quantum Engine API ändras under utveckling | Tunna wrappers i `Circuits/` — byt internt utan att röra visualisering |
| State vector-sim för långsam för >5 qubits | Begränsa demo till 5 qubits, visa warning i UI |
| RL-träning tar för lång tid live | Pre-träna policies, erbjud "instant" mode med sparad modell |
| Bloch-sfär svårläst för multi-qubit | Erbjud alternativ "state table"-vy vid sidan |
| WebGL saknar threading | Kör simulering synkront med progress indicator i WebGL |

---

## 9. Tidsordning (föreslaget)

```
Fas 0 ████████████░░░░░░░░░░░░░░░░  CSharpNumerics Quantum Engine
Fas 1          ████████░░░░░░░░░░░░  Scaffold + Bloch Sphere
Fas 2                  ████████░░░░  Circuit Builder + Measurement
Fas 3                        ██████  RL Error Correction
Fas 4                          ████  Polish & Export
```

Fas 1 kan påbörjas parallellt med Fas 0 — Bloch-sfär-rendering behöver bara `BlochCoordinates` (θ, φ), resten kan mockas med dummy-data.

---

## 10. Koppling till NuclearFalloutML-mönster

Asseten följer exakt samma mönster:
- **Samma assembly-struktur:** Runtime → Editor → Demo
- **Samma DemoSimulation-mönster:** `async Start()`, zero-setup, self-contained
- **Samma CSharpNumerics.dll-referens:** `overrideReferences: true`
- **Samma conditional dependencies:** `versionDefines` för valfria paket
- **Nytt:** 3D-visualisering (NuclearFalloutML är 2D heatmap, detta är 3D sfärer + kretsar)
