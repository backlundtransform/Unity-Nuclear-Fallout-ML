# Engineering Toolbox

Interactive multiphysics demonstration tool for **students and teachers**. Visualize heat transfer, electrostatics, pipe flow, and beam stress with real-time heatmaps, vector field overlays, and material comparison.

Built on [CSharpNumerics](https://csnumerics.com/) Multiphysics Engine.

## Quick Start

1. Attach `DemoSimulation` to any empty GameObject
2. Enter Play Mode
3. Use keyboard to switch modules, materials, and time steps

## Modules

| Module | Physics | Visualization |
|--------|---------|---------------|
| Heat Transfer | 2D transient heat equation | Animated heatmap |
| Electrostatics | 2D Poisson equation | Potential heatmap + E-field vectors |
| Pipe Flow | 1D Hagen-Poiseuille | Velocity profile plot |
| Beam Stress | 1D Euler-Bernoulli | Deflection, moment, shear curves |

## Controls

| Key | Action |
|-----|--------|
| `1-4` | Select module (Heat / Electric / Pipe / Beam) |
| `Space` | Play / pause timeline |
| `←` / `→` | Step backward / forward |
| `R` | Reset simulation |
| `M` | Cycle material |
| `V` | Toggle vector field overlay |
| `F12` | Screenshot |
