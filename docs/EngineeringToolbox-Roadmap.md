# Engineering Toolbox — Roadmap

Unity asset for interactive physics demonstrations aimed at **students and teachers**. Wraps CSharpNumerics' Multiphysics engine with 2D heatmaps, vector field overlays, material pickers, and scenario management.

## Target Audience

- Engineering / physics students (undergraduate level)
- Teachers building lecture demos
- Self-learners exploring PDE-based physics

## Simulation Modules

| Module | CSharpNumerics Solver | Status |
|--------|----------------------|--------|
| **Heat Transfer** | `MultiphysicsType.HeatPlate` — 2D FD | ✅ Ready |
| **Pipe Flow** | `MultiphysicsType.PipeFlow` — 1D Hagen-Poiseuille | ✅ Ready |
| **Electrostatics** | `MultiphysicsType.ElectricField` — 2D Poisson | ✅ Ready |
| **Beam Stress** | `MultiphysicsType.BeamStress` — 1D Euler-Bernoulli | ✅ Ready |
| **2D Fluid Flow** (Navier-Stokes) | ⚠ Not in engine | ❌ Issue #1 |
| **Magnetostatics** (B-field) | ⚠ Not in engine | ❌ Issue #2 |
| **2D Structural** (plane stress) | ⚠ Not in engine | ❌ Issue #3 |

## CSharpNumerics Issues (NOT developed in this repo)

These capabilities are **missing in CSharpNumerics** and must be added there before this asset can expose them. Each is logged as an issue below.

### Issue #1 — 2D Navier-Stokes Solver in Multiphysics Engine

`Physics/FluidDynamics/NavierStokesExtensions.cs` exists but there is no `MultiphysicsType.FluidFlow2D` solver that wraps it into the SimulationBuilder pipeline. Needed: a 2D transient solver on `Grid2D` producing velocity fields (Vx, Vy) and pressure, with timeline snapshots and the same fluent API.

### Issue #2 — Magnetostatics Solver in Multiphysics Engine

`Physics/Electromagnetism/MagnetismExtensions.cs` and `MaxwellExtensions.cs` exist but there is no `MultiphysicsType.MagneticField` solver. Needed: 2D magnetostatics (∇²A = −μJ) producing vector potential A and magnetic field (Bx, By), with the same fluent API and material support. `EngineeringMaterial` also **lacks magnetic permeability (μ)** — this property must be added.

### Issue #3 — 2D Plane Stress/Strain Solver in Multiphysics Engine

`Physics/SolidMechanics/StressStrainExtensions.cs` exists but only 1D beam analysis is wrapped. Needed: a 2D FE or FD plane-stress solver producing displacement and stress tensor fields, usable through the SimulationBuilder.

### Issue #4 — Extended Material Library

`EngineeringLibrary` only has 7 materials (Steel, Aluminum, Copper, Water, Air, Concrete, Glass). For educational use, add: Wood, Rubber, Titanium, Brass, Stainless Steel, Oil, Glycerin, Plastic/HDPE, and allow custom user-defined materials more easily.

### Issue #5 — VectorField Grid Evaluation

`VectorField.EvaluateRange` only walks a diagonal line. Needed: a 2D grid evaluation method `EvaluateGrid2D(xmin, xmax, ymin, ymax, nx, ny)` returning a structured grid of vectors for proper arrow-plot rendering.

---

## Phase 1 — Core Infrastructure & Heat Demo

Foundation: project scaffold, material UI, heatmap renderer, and the first working module (heat transfer).

- [x] Scaffold `Assets/EngineeringToolbox/` with Runtime, Editor, Demo asmdefs
- [x] `SimulationConfig` — serializable config with module selector enum
- [ ] `MaterialPicker` — dropdown UI for `EngineeringLibrary` materials + custom material input (k, ρ, cp, E, ν, ε, μ_viscosity)
- [x] `HeatmapRenderer` — reusable `Texture2D` renderer mapping `double[,]` to color gradient (coolwarm, viridis, inferno)
- [ ] `ColorGradient` — configurable LUT with min/max auto-scaling and legend bar
- [ ] `SimulationManager` — MonoBehaviour orchestrating build → solve → visualize pipeline
- [ ] `HeatTransferModule` — wraps `MultiphysicsType.HeatPlate`, exposes boundary temperatures and point sources via Inspector
- [x] `TimelinePlayer` — play/pause/scrub through `SimulationTimeline` snapshots
- [x] `DemoSimulation.cs` — zero-setup demo running heat transfer with animated heatmap
- [x] Keyboard controls: Space (play/pause), ←/→ (step), R (reset), M (cycle material)

## Phase 2 — Electrostatics & Vector Field Visualization

Add electric field module with vector arrow overlay.

- [x] `VectorFieldOverlay` — renders arrow glyphs on top of heatmap from `Ex[,]`/`Ey[,]` data (line renderer or procedural mesh)
- [ ] `ElectrostaticsModule` — wraps `MultiphysicsType.ElectricField`, exposes charges and boundary voltages
- [x] Arrow scaling: magnitude → length + color, configurable density (skip every N cells)
- [x] Toggle overlay: heatmap-only / vectors-only / both
- [ ] Streamline renderer (optional) — trace field lines from seed points
- [x] Add electrostatics scenario to `DemoSimulation`
- [ ] Keyboard: V (toggle vectors), +/− (arrow density)

## Phase 3 — Pipe Flow & Beam Stress Modules

1D visualization modules with profile plots.

- [ ] `ProfilePlotRenderer` — reusable 1D line-chart renderer (positions vs values) using UI LineRenderer or procedural mesh
- [ ] `PipeFlowModule` — wraps `MultiphysicsType.PipeFlow`, shows radial velocity profile with timeline animation
- [ ] `BeamStressModule` — wraps `MultiphysicsType.BeamStress`, shows deflection + moment + shear + stress curves
- [x] Support type toggle (Cantilever / Simply Supported / Fixed-Fixed)
- [x] Load input: point load position+magnitude, distributed load
- [ ] Cross-section picker (rectangular / circular / custom I)
- [x] Add both modules to `DemoSimulation` with tab/keyboard switching

## Phase 4 — Scenario Manager & Comparison

Enable save/load of simulation setups and side-by-side comparison.

- [ ] `ScenarioPreset` — serializable class holding module type + material + geometry + BCs + sources
- [ ] `ScenarioLibrary` — built-in preset scenarios per module (e.g., "Copper plate with heat source", "Cantilever with tip load")
- [ ] Save/load scenario to JSON
- [ ] `ComparisonView` — side-by-side split rendering two simulation results (e.g., Steel vs Aluminum)
- [ ] Difference overlay: show ΔT or Δσ between two results
- [ ] Add scenario selector dropdown to DemoSimulation

## Phase 5 — Monte Carlo & Parameter Exploration

Expose stochastic analysis for uncertainty-aware teaching.

- [ ] `ParameterSlider` — UI sliders for `ParameterVariation` ranges (conductivity, BC temps, source intensity)
- [ ] `MonteCarloRunner` — wraps `MultiphysicsMonteCarloModel`, runs batch async
- [ ] `DistributionOverlay` — show P95/P50 percentile maps as heatmap layers
- [ ] `HistogramView` — show distribution of peak values across MC runs
- [ ] `SurrogatePredictor` — train Ridge surrogate, predict interactively without re-solving
- [ ] Add MC demo scenario to DemoSimulation

## Phase 6 — Export & Polish

- [x] Screenshot utility (F12)
- [ ] Export current result to JSON (`MultiphysicsJsonExporter`)
- [ ] Export timeline to MPHY binary (`MultiphysicsBinaryExporter`)
- [ ] CSV export for 1D profiles (beam, pipe flow)
- [ ] Info overlay: show PDE formula, material properties, grid size, solver stats
- [ ] Color-blind friendly palette option
- [ ] README and documentation

## Phase 7 — Future Modules (Blocked on CSharpNumerics Issues)

These phases become unblocked as CSharpNumerics adds the corresponding solvers.

- [ ] **2D Fluid Flow** — velocity field heatmap + vector overlay (blocked on Issue #1)
- [ ] **Magnetostatics** — magnetic field lines + potential heatmap (blocked on Issue #2)
- [ ] **2D Structural** — stress/displacement fields with deformed mesh overlay (blocked on Issue #3)
- [ ] **Coupled simulations** — thermal-structural, electro-thermal (future CSharpNumerics feature)

---

## Architecture Overview

```
EngineeringToolbox/
├── Runtime/Scripts/
│   ├── Core/
│   │   ├── SimulationManager.cs        ← orchestrator
│   │   ├── SimulationConfig.cs         ← serializable config
│   │   ├── ScenarioPreset.cs           ← save/load scenarios
│   │   └── ScenarioLibrary.cs          ← built-in presets
│   ├── Modules/
│   │   ├── ISimulationModule.cs        ← interface per physics type
│   │   ├── HeatTransferModule.cs
│   │   ├── ElectrostaticsModule.cs
│   │   ├── PipeFlowModule.cs
│   │   └── BeamStressModule.cs
│   ├── Visualization/
│   │   ├── HeatmapRenderer.cs          ← 2D scalar → Texture2D
│   │   ├── VectorFieldOverlay.cs       ← arrow glyph layer
│   │   ├── ProfilePlotRenderer.cs      ← 1D charts
│   │   ├── ColorGradient.cs            ← LUT + legend
│   │   └── ComparisonView.cs           ← side-by-side
│   ├── UI/
│   │   ├── MaterialPicker.cs           ← material dropdown + custom
│   │   ├── TimelinePlayer.cs           ← play/pause/scrub
│   │   ├── ParameterSlider.cs          ← MC parameter ranges
│   │   └── InfoOverlay.cs              ← PDE + stats display
│   └── Export/
│       ├── ExportManager.cs
│       └── ScreenshotUtility.cs
├── Editor/Scripts/
│   └── SimulationConfigEditor.cs       ← custom inspector
└── Demo/Scripts/
    └── DemoSimulation.cs               ← zero-setup, all modules
```

## Namespace Mapping (CSharpNumerics)

| Unity Usage | CSharpNumerics Namespace |
|-------------|-------------------------|
| `SimulationType.Create(...)` | `CSharpNumerics.Engines.Multiphysics` |
| `MultiphysicsType` | `CSharpNumerics.Engines.Multiphysics.Enums` |
| `EngineeringMaterial`, `EngineeringLibrary` | `CSharpNumerics.Physics.Materials.Engineering` |
| `SimulationTimeline`, `FieldSnapshot` | `CSharpNumerics.Engines.Multiphysics.Snapshots` |
| `MultiphysicsJsonExporter` | `CSharpNumerics.Engines.Multiphysics.Export` |
| `MultiphysicsMonteCarloModel` | `CSharpNumerics.Engines.Multiphysics.MonteCarlo` |
| `VectorField` | `CSharpNumerics.Numerics.Objects` |
