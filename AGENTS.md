# CSharpNumerics.Visualization — Agent Guide

## Project Overview

This repository contains **Unity assets for scientific visualization**, built on [CSharpNumerics](https://csnumerics.com/) — an open-source C# scientific computing library. Each asset demonstrates a real-world application (nuclear fallout simulation, fluid dynamics, orbital mechanics, etc.) and serves as a showcase for the CSharpNumerics library.

**Key principle:** CSharpNumerics handles all computation. Unity assets handle visualization, UI, and user interaction.

## Repository Structure

```
CSharpNumerics.Visualization/
├── AGENTS.md                          ← You are here
├── README.md                          ← Project overview and asset catalog
├── LICENSE
├── .gitmodules                        ← Declares CSharpNumerics submodule
│
├── External/
│   └── CSharpNumerics/                ← Git submodule (READ-ONLY for this repo)
│       ├── AGENTS.md                  ← CSharpNumerics own agent guide
│       └── Numerics/Numerics/         ← Source code and .csproj
│
├── Tools/
│   └── Build-CSharpNumerics.ps1       ← Builds DLL from submodule source
│
└── Assets/
    ├── Plugins/
    │   └── CSharpNumerics/
    │       └── CSharpNumerics.dll     ← Built artifact (Unity references this)
    │
    ├── NuclearFalloutML/              ← Asset #1: Nuclear Fallout Simulation
    │   ├── package.json
    │   ├── Runtime/
    │   │   ├── NuclearFalloutML.Runtime.asmdef
    │   │   └── Scripts/
    │   └── Editor/
    │       ├── NuclearFalloutML.Editor.asmdef
    │       └── Scripts/
    │
    └── [AssetName]/                   ← Future assets follow same pattern
        ├── package.json
        ├── Runtime/
        │   ├── [AssetName].Runtime.asmdef
        │   └── Scripts/
        └── Editor/
            ├── [AssetName].Editor.asmdef
            └── Scripts/
```

## CSharpNumerics Submodule

CSharpNumerics is included as a **git submodule** at `External/CSharpNumerics/`. This is a separate, independently maintained repository.

### Rules

1. **Never modify files inside `External/CSharpNumerics/`** from this project.
2. If you need a CSharpNumerics API change, note it — the change must be made in the CSharpNumerics repo separately.
3. To understand available CSharpNumerics APIs, read `External/CSharpNumerics/AGENTS.md` and explore the source under `External/CSharpNumerics/Numerics/Numerics/`.
4. The submodule is pinned to a specific commit. Only update it intentionally.

### Common Submodule Commands

```powershell
# Clone repo with submodule
git clone --recurse-submodules <repo-url>

# Initialize submodule after clone (if you forgot --recurse-submodules)
git submodule update --init --recursive

# Update submodule to latest CSharpNumerics main branch
git submodule update --remote External/CSharpNumerics

# Check which commit the submodule is pinned to
git submodule status
```

### Building the DLL

The Unity project references `CSharpNumerics.dll` as a precompiled library. Build it from the submodule source:

```powershell
# Build using the provided script
.\Tools\Build-CSharpNumerics.ps1

# Or manually:
dotnet build External/CSharpNumerics/Numerics/Numerics/CSharpNumerics.csproj `
    -c Release -f netstandard2.1
# Then copy the DLL to Assets/Plugins/CSharpNumerics/
```

Alternatively, download from NuGet:
```powershell
# Install CSharpNumerics NuGet package and extract DLL
nuget install CSharpNumerics -OutputDirectory tmp
# Copy the netstandard2.1 DLL to Assets/Plugins/CSharpNumerics/
```

## Asset Architecture

Each asset is an independent **Unity Package** (UPM) with its own assembly definitions. Assets share CSharpNumerics via the DLL in `Assets/Plugins/CSharpNumerics/`.

### Assembly Structure per Asset

```
[AssetName]/
├── package.json                    ← UPM package manifest
├── Runtime/
│   ├── [AssetName].Runtime.asmdef  ← Runtime assembly definition
│   └── Scripts/
│       ├── Core/                   ← Manager, config, data classes
│       ├── Physics/                ← Physics model documentation/bridges
│       ├── Numerics/               ← CSharpNumerics wrapper utilities
│       ├── Visualization/          ← Unity rendering (Cesium, etc.)
│       ├── UI/                     ← Runtime UI controllers
│       └── Export/                 ← Export helpers
└── Editor/
    ├── [AssetName].Editor.asmdef   ← Editor assembly (references Runtime)
    └── Scripts/
        └── *Editor.cs              ← Custom inspectors
```

### Assembly Reference Rules

- `[AssetName].Runtime.asmdef` → references `CSharpNumerics.dll` (precompiled)
- `[AssetName].Editor.asmdef` → references `[AssetName].Runtime`
- Assets do NOT reference each other (they are independent packages)
- Optional platform SDKs (e.g., CesiumForUnity) are gated with `#if` defines via `versionDefines`

### Creating a New Asset

1. Create folder: `Assets/[AssetName]/`
2. Create `package.json` (see NuclearFalloutML as template)
3. Create `Runtime/` folder with `[AssetName].Runtime.asmdef`:
   ```json
   {
       "name": "[AssetName].Runtime",
       "rootNamespace": "[AssetName]",
       "references": [],
       "precompiledReferences": ["CSharpNumerics.dll"],
       "versionDefines": [
           {
               "name": "com.cesium.unity",
               "expression": "1.0.0",
               "define": "CESIUM_AVAILABLE"
           }
       ]
   }
   ```
4. Create `Editor/` folder with `[AssetName].Editor.asmdef`:
   ```json
   {
       "name": "[AssetName].Editor",
       "rootNamespace": "[AssetName].Editor",
       "references": ["[AssetName].Runtime"],
       "includePlatforms": ["Editor"]
   }
   ```
5. Implement scripts following the Core → Visualization → UI → Export pattern

## Design Principles

1. **CSharpNumerics does the math.** Unity assets are thin wrappers that provide:
   - Inspector/UI for parameter configuration
   - Visualization (3D rendering, heatmaps, globe overlays)
   - Export integration (GeoJSON, CZML, CSV)

2. **Each asset is self-contained.** Users can install one asset without needing others.

3. **Conditional dependencies.** Optional integrations (Cesium, etc.) are gated with `#if CESIUM_AVAILABLE` or similar defines configured via `versionDefines` in the `.asmdef`.

4. **Async-friendly.** Simulations use `async/await` and `Task.Run` to avoid blocking Unity's main thread.

## CSharpNumerics API Quick Reference

Read `External/CSharpNumerics/AGENTS.md` for the full guide. Key namespaces used by assets:

| Namespace | Used For |
|-----------|----------|
| `CSharpNumerics.Numerics` | Vector, Matrix, ScalarField, VectorField, ODE solvers |
| `CSharpNumerics.Statistics.MonteCarlo` | MonteCarloSimulator, stochastic analysis |
| `CSharpNumerics.ML` | KMeans, ClusteringGrid, SilhouetteEvaluator |
| `CSharpNumerics.Physics` | Physical models, materials, stability classes |
| `CSharpNumerics.Engines.GIS` | GeoGrid, PlumeSimulator, RiskScenario, export |

### Discovering APIs

When implementing a new asset, explore the CSharpNumerics source to find relevant APIs:

```powershell
# Search for relevant classes
Get-ChildItem External/CSharpNumerics/Numerics/Numerics -Recurse -Filter "*.cs" |
    Select-String "public class|public interface" |
    Where-Object { $_.Line -match "YourKeyword" }

# Read section READMEs for examples
Get-ChildItem External/CSharpNumerics/Numerics/Numerics -Recurse -Filter "README.md"
```

## Build and Validation

```powershell
# 1. Build CSharpNumerics DLL
.\Tools\Build-CSharpNumerics.ps1

# 2. Open in Unity and verify no compilation errors
# 3. Enter Play Mode and test each asset independently
```

## File Conventions

- **Namespaces match folder path:** `NuclearFalloutML.Core` → `NuclearFalloutML/Runtime/Scripts/Core/`
- **One MonoBehaviour per file**, file name matches class name
- **Config classes** are `[Serializable]` with `[Header]`, `[Range]`, `[Tooltip]` attributes for Inspector UX
- **Documentation-only .cs files** are used for modules fully delegated to CSharpNumerics (contain only XML doc comments explaining the delegation)
