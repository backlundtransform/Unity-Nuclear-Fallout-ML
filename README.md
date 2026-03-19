# CSharpNumerics.Visualization — Scientific Visualization for Unity

**Unity assets for scientific visualization, powered by [CSharpNumerics](https://csnumerics.com/) — an open-source C# scientific computing library.**

Each asset demonstrates a real-world scientific application with interactive 3D visualization in Unity. All computation is handled by CSharpNumerics; the assets provide Unity UI, rendering, and export capabilities.

## Assets

| Asset | Description | Status |
|-------|-------------|--------|
| **[NuclearFalloutML](Assets/NuclearFalloutML/)** | Monte Carlo nuclear fallout simulation with ML clustering, probability mapping, and Cesium 3D globe visualization | ✅ Available |
<!-- | **[FluidDynamics](Assets/FluidDynamics/)** | CFD visualization | 🚧 Planned | -->

## Getting Started

### Requirements

- **Unity** 2021.3+
- **CSharpNumerics** v2.6.3+ (included as git submodule, or via [NuGet](https://www.nuget.org/packages/CSharpNumerics))
- **CesiumForUnity** 1.0+ (optional, for 3D globe visualization)

### Installation

```bash
# Clone with submodule
git clone --recurse-submodules https://github.com/backlundtransform/CSharpNumerics.Visualization.git

# Build CSharpNumerics DLL from source
.\Tools\Build-CSharpNumerics.ps1
```

Then open the project in Unity. See each asset's README for usage instructions.

## Architecture

```
CSharpNumerics.Visualization/
├── AGENTS.md                          # Agent/developer workflow guide
├── External/
│   └── CSharpNumerics/                # Git submodule (independent repo)
├── Tools/
│   └── Build-CSharpNumerics.ps1       # Builds DLL from submodule
└── Assets/
    ├── Plugins/CSharpNumerics/        # Built DLL (all assets reference this)
    ├── NuclearFalloutML/              # Asset #1 — see its own README
    └── [FutureAsset]/                 # Each asset is a self-contained UPM package
```

- **CSharpNumerics** is a git submodule at `External/CSharpNumerics/` — fully independent with its own repo, NuGet releases, and development cycle.
- **Each asset** is an independent Unity Package (UPM) with its own assembly definitions, sharing CSharpNumerics via the precompiled DLL.
- **Assets do not depend on each other** and can be installed individually.

See [AGENTS.md](AGENTS.md) for the full developer/agent workflow guide, including how to create new assets.

## License

See [LICENSE](LICENSE) file
