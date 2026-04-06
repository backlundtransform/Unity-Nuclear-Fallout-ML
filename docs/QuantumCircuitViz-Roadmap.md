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

## 2. CSharpNumerics Quantum Engine API:er (v2.8.0 — bekräftade)

*Submodule uppdaterad till `d476a24` (v2.8.0). Alla namespaces verifierade.*

| Namespace | Klasser |
|-----------|---------|
| `CSharpNumerics.Engines.Quantum` | `QuantumCircuit`, `QuantumInstruction`, `QuantumSimulator`, `QuantumState` |
| `CSharpNumerics.Engines.Quantum` | `QuantumCircuitBuilder` (fluent API), `NoisyQuantumSimulator` |
| `CSharpNumerics.Engines.Quantum` | `QuantumEnvironment` (RL-miljö, implementerar `IEnvironment`) |
| `CSharpNumerics.Engines.Quantum.Algorithms` | `GroverSearch`, `ShorAlgorithm`, `QFT`, `InverseQFT`, `QPE` |
| `CSharpNumerics.Engines.Quantum.ErrorCorrection` | `ErrorCorrectionSimulator`, `SyndromeDecoder` |
| `CSharpNumerics.Physics.Quantum` | `QuantumGate` (abstract), `BlochVector`, `QuantumFidelity` |
| `CSharpNumerics.Physics.Quantum` | 7 single-qubit: `HadamardGate`, `PauliX/Y/ZGate`, `SGate`, `TGate`, `PhaseGate` |
| `CSharpNumerics.Physics.Quantum` | 3 rotation: `RxGate`, `RyGate`, `RzGate` |
| `CSharpNumerics.Physics.Quantum` | 5 multi-qubit: `CNOTGate`, `CZGate`, `CPhaseGate`, `SWAPGate`, `ToffoliGate`, `FredkinGate` |
| `CSharpNumerics.Physics.Quantum` | Special: `ControlledGate`, `ModularMultiplyGate`, `PhaseOracle` |
| `CSharpNumerics.Physics.Quantum.NoiseModels` | `INoiseChannel`, `DepolarizingNoise`, `DephasingNoise`, `AmplitudeDampingNoise` |
| `CSharpNumerics.Physics.Quantum.ErrorCorrection` | `IQuantumErrorCorrectionCode`, `BitFlipCode3`, `PhaseFlipCode3`, `ShorCode9`, `SteaneCode7` |

### Nyckel-API:er

```csharp
// Fluent circuit builder (ny i v2.8)
var circuit = QuantumCircuitBuilder.New(2).H(0).CNOT(0, 1).Build();

// Simulera
QuantumState state = new QuantumSimulator().Run(circuit);

// Mät
VectorN probs = state.GetProbabilities();     // [0.5, 0, 0, 0.5] för Bell state
int outcome = state.Measure(new Random());     // Kollapsa state
var shots = state.Sample(1000, new Random());  // 1000 mätningar

// Bloch-sfär
BlochVector bloch = state.GetBlochVector();    // X, Y, Z, Theta, Phi, Radius

// Fidelity
double f = QuantumFidelity.Fidelity(state1, state2);

// Noisy simulation
var noisy = new NoisyQuantumSimulator(new Random())
    .WithNoise(new DepolarizingNoise(0.01));
QuantumState noisyState = noisy.Run(circuit);

// Quantum algorithms
var grover = GroverSearch.CreateCircuit(3, new[]{0,1,2}, new[]{5}); // Sök |101⟩
var shorResult = ShorAlgorithm.Factor(15, new Random());             // 3 × 5

// Error correction
var code = new SteaneCode7();
var ecSim = new ErrorCorrectionSimulator();
var decoder = new SyndromeDecoder(code);

// RL-miljö
var env = QuantumEnvironment.Create(2)
    .WithTargetCircuit(circuit)
    .WithMaxGates(10)
    .WithFidelityThreshold(0.99)
    .Build();
```

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

## 4. Asset-struktur (implementerad)

```
Assets/QuantumCircuitViz/
├── package.json
├── Runtime/
│   ├── QuantumCircuitViz.Runtime.asmdef
│   └── Scripts/
│       ├── Core/
│       │   ├── CircuitRunner.cs               ← Wraps QuantumCircuitBuilder + NoisySimulator
│       │   ├── GateLibrary.cs                 ← Gate registry (18 gates inkl. Toffoli, Fredkin)
│       │   └── SimulationConfig.cs            ← [Serializable] config med noise-parametrar
│       └── Visualization/
│           ├── BlochSphereRenderer.cs         ← 3D Bloch-sfär med animerad state-vektor
│           ├── CircuitDiagramRenderer.cs      ← ASCII circuit diagram med steg-markör
│           └── MeasurementHistogram.cs        ← UI probability bars per basis state
├── Editor/
│   ├── QuantumCircuitViz.Editor.asmdef
│   └── Scripts/
│       └── QuantumSimulationEditor.cs         ← Custom inspector (placeholder)
└── Demo/
    ├── QuantumCircuitViz.Demo.asmdef
    └── Scripts/
        └── DemoSimulation.cs                  ← Zero-setup demo, 7 presets, noise toggle
```

---

## 5. Roadmap — Faser

### Fas 0: CSharpNumerics Quantum Engine ✅ (klart — v2.8.0)
> *Fullständig quantum engine med noise, QEC, algorithms och RL*

| # | Uppgift | Status |
|---|---------|--------|
| 0.1 | Quantum circuit builder API (fluent `QuantumCircuitBuilder`) | ✅ Klart |
| 0.2 | Standard gates — 18 total (H, X, Y, Z, S, T, Phase, Rx/Ry/Rz, CNOT, CZ, CPhase, SWAP, Toffoli, Fredkin, Controlled, PhaseOracle, ModularMultiply) | ✅ Klart |
| 0.3 | State vector simulator (`QuantumSimulator` + `NoisyQuantumSimulator`) | ✅ Klart |
| 0.4 | Measurement (`GetProbabilities()`, `Measure()`, `Sample()`) | ✅ Klart |
| 0.5 | Bloch sphere + Fidelity (`BlochVector`, `QuantumFidelity`) | ✅ Klart |
| 0.6 | Noise models (`DepolarizingNoise`, `DephasingNoise`, `AmplitudeDampingNoise`) | ✅ Klart |
| 0.7 | QEC codes (`BitFlipCode3`, `PhaseFlipCode3`, `ShorCode9`, `SteaneCode7`) | ✅ Klart |
| 0.8 | QEC simulation (`ErrorCorrectionSimulator`, `SyndromeDecoder`) | ✅ Klart |
| 0.9 | Quantum algorithms (`GroverSearch`, `ShorAlgorithm`, `QFT`, `QPE`) | ✅ Klart |
| 0.10 | RL environment (`QuantumEnvironment : IEnvironment`) | ✅ Klart |
| 0.11 | Unit tests (50+ tester) | ✅ Klart |

### Fas 1: Scaffold & Bloch Sphere (v1.0) — "First Light" ✅ KLART
> *Mål: En roterande Bloch-sfär som visar qubit state i realtid*

| # | Uppgift | Status |
|---|---------|--------|
| 1.1 | Skapa `Assets/QuantumCircuitViz/` med package.json, asmdef-filer | ✅ Klart |
| 1.2 | Uppdatera CSharpNumerics submodule till v2.8.0 | ✅ Klart |
| 1.3 | Bygg ny CSharpNumerics.dll med full quantum-stöd | ✅ Klart |
| 1.4 | `BlochSphereRenderer.cs` — semi-transparent sfär, axlar, state-vektor | ✅ Klart |
| 1.5 | `CircuitRunner.cs` — fluent builder wrapper + noisy simulation | ✅ Klart |
| 1.6 | `DemoSimulation.cs` — zero-setup demo med 7 presets + noise toggle | ✅ Klart |
| 1.7 | `MeasurementHistogram.cs` — sannolikheter per basis state | ✅ Klart |
| 1.8 | `CircuitDiagramRenderer.cs` — ASCII-krets med steg-markör | ✅ Klart |
| 1.9 | Multi-qubit reduced Bloch vectors (partial trace) | ✅ Klart |
| 1.10 | Grover, Toffoli, QFT presets via CSharpNumerics Algorithms API | ✅ Klart |
| 1.11 | Post-processing setup (Bloom, mörk bakgrund) | P1 — nästa |

**Leverans:** Attach DemoSimulation → Play Mode → ser en glödande Bloch-sfär som animerar genom H → X → Z gates.

### Fas 2: Circuit Builder & Measurement (v2.0) — "Build & Measure" ✅ KLART
> *Mål: Användare bygger kretsar visuellt och ser mätresultat*

| # | Uppgift | Status |
|---|---------|--------|
| 2.1 | `CircuitCanvas.cs` — interaktiv krets-canvas med klickbara slots | ✅ Klart |
| 2.2 | `GatePalette.cs` — UI-panel med alla 11 gates (single + multi-qubit) | ✅ Klart |
| 2.3 | Click-to-place: välj gate → klicka slot → gate placeras | ✅ Klart |
| 2.4 | Multi-qubit gate placement (klicka kontroll → mål) | ✅ Klart |
| 2.5 | Builder mode toggle (G-tangent), CircuitCanvas → CircuitRunner pipeline | ✅ Klart |
| 2.6 | `MeasurementHistogram.cs` — animerad sampling (256 shots, CDF-baserad) | ✅ Klart |
| 2.7 | M-tangent triggar mätnings-sampling animation med shot count display | ✅ Klart |
| 2.8 | `EntanglementVisualizer.cs` — glödande linjer mellan entanglade qubits | ✅ Klart |
| 2.9 | `QubitInspectorPanel.cs` — klicka Bloch-sfär → Bloch-vektor, P(|0⟩), purity | ✅ Klart |
| 2.10 | SphereCollider på Bloch-sfärer för raycast-klick | ✅ Klart |
| 2.11 | EventSystem auto-setup för UI-interaktion | ✅ Klart |

**Leverans:** G → gate palette + circuit canvas. Klicka gates → live Bloch-sfärer + entanglement-linjer. M → animerad mätning. Klicka sfär → qubit inspector.

### Fas 3: RL Error Correction (v3.0) — "Self-Healing Qubits" ✅ KLART
> *Mål: Visa hur RL-agenter korrigerar kvantfel i realtid*
> *CSharpNumerics API:er: `NoisyQuantumSimulator`, `QuantumEnvironment`, `ErrorCorrectionSimulator`, `SyndromeDecoder`, alla QEC-koder, `DQN`/`DoubleDQN`*

| # | Uppgift | Status |
|---|---------|--------|
| 3.1 | `SimulationConfig.cs` — QEC config (kod-typ, error rate, MC rounds) + RL config (episodes, max gates, fidelity threshold, LR, gamma) | ✅ Klart |
| 3.2 | `QECRunner.cs` — wrapper runt alla 4 QEC-koder (BitFlip3/PhaseFlip3/Steane7/Shor9), SyndromeDecoder, Monte-Carlo jämförelse | ✅ Klart |
| 3.3 | `RLErrorCorrectionRunner.cs` — DQN/DoubleDQN mot QuantumEnvironment i bakgrundstråd, thread-safe episode reports | ✅ Klart |
| 3.4 | `NoiseGlitchEffect.cs` — visuellt "glitch" på Bloch-sfärer: jitter + röd-shift + pulsande alpha proportionellt mot error rate | ✅ Klart |
| 3.5 | `ErrorCorrectionVisualizer.cs` — split-view: noisy vs QEC-protected fidelity-staplar, syndrom-display, corrections-lista | ✅ Klart |
| 3.6 | `RLTrainingPanel.cs` — realtids linjegraf ritat på Texture2D (Bresenham), fidelity + reward per episode, status-text | ✅ Klart |
| 3.7 | `ErrorHeatmapOverlay.cs` — per-qubit error rate heatmap (grön→gul→röd), overlay på krets-diagrammet | ✅ Klart |
| 3.8 | Demo: Q = kör QEC comparison, E = starta/stoppa RL-träning, C = byt QEC-kod | ✅ Klart |
| 3.9 | Thread-safe rapport-kö (main thread drain) för RL-episode callbacks | ✅ Klart |
| 3.10 | NoiseGlitch auto-cleanup vid preset-byte, OnDestroy stoppar RL-tråd | ✅ Klart |

**Leverans:** Q → Monte-Carlo QEC jämförelse med animerade staplar + syndrom. E → DQN tränas live med realtids-graf. C → cykla mellan 4 QEC-koder. Noise-glitch visuellt på sfärer.

### Fas 4: Polish & Export (v4.0) — "Marketing Ready" ✅ KLART
> *Mål: Asset Store-redo, exportfunktioner, maxad visuell kvalitet*

| # | Uppgift | Status |
|---|---------|--------|
| 4.1 | 3D world-space circuit (`WorldSpaceCircuit.cs`) — hologram-estetik, glowing wires, gate cubes, control dots, step highlighting | ✅ Klart |
| 4.2 | VFX Graph-partiklar: photon-flöde längs ledningar | ⏭️ Skippat (kräver VFX Graph-paket) |
| 4.3 | Density matrix heatmap (`DensityMatrixHeatmap.cs`) — ρ=\|ψ⟩⟨ψ\| med magnitud→ljusstyrka, fas→hue via HSV | ✅ Klart |
| 4.4 | QASM-export (`CircuitExporter.cs`) — OpenQASM 2.0, JSON, compact text notation, alla 14+ gate-typer | ✅ Klart |
| 4.5 | Screenshot/video capture (`ScreenshotUtility.cs`) — supersampled PNG, camera-to-texture | ✅ Klart |
| 4.6 | Custom inspector (`QuantumSimulationEditor.cs`) — preset-knappar, export-kontroller, steg-navigation | ✅ Klart |
| 4.7 | README, dokumentation (`Assets/QuantumCircuitViz/README.md`) — fullständig med features, quick start, API-referens, tangentbindningar | ✅ Klart |
| 4.8 | WebGL build + demo-sida | ⏭️ Framtida (kräver hosting) |
| 4.9 | Promotional screenshots och GIF-animationer | ⏭️ Framtida (kräver Play Mode) |

**Nya tangentbindningar:** D → density matrix, W → 3D circuit, F5 → QASM till clipboard, F12 → screenshot

**Leverans:** Asseten är funktionellt komplett med 20+ scripts, 7 presets, full interaktivitet, QASM/JSON-export, densitetsmatris, 3D-krets, custom editor, och README-dokumentation.

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
