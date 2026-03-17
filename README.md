# Nuclear Fallout ML Simulator — Unity Asset

**Monte Carlo nuclear fallout simulation powered by [CSharpNumerics](https://csnumerics.com/) GeoEngine, with ML clustering, probability mapping, and Cesium 3D globe visualization.**

Pipeline: **Physics (Gaussian Plume/Puff) → Monte Carlo (N scenarios) → ML Clustering (K-Means / ClusteringGrid) → Probability Map → Export (GeoJSON / CZML / Binary)**

## Architecture

All simulation, Monte Carlo, clustering, and probability computations are delegated to **CSharpNumerics GeoEngine**. This Unity asset provides:
- Inspector UI and runtime UI for configuring parameters
- Cesium 3D globe overlay rendering
- The bridge between CSharpNumerics results and Unity / CesiumForUnity

```
┌─────────────────────────────────────────────────────────────┐
│                    USER INPUT (Unity Inspector / UI)         │
│  Geo coordinates, Wind vector, MC iterations, Emission rate │
│  Radioisotope, Stability class, Grid settings, Time range   │
└──────────────────────┬──────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────┐
│  CSharpNumerics.Engines.GIS.Scenario.RiskScenario            │
│  Fluent API:                                                 │
│   .ForGaussianPlume(emissionRate)                            │
│   .FromSource(pos).WithWind(speed, dir)                      │
│   .WithStability(class).WithMaterial(Cs137)                  │
│   .WithVariation(wind, jitter, emission, stability weights)  │
│   .OverGrid(GeoGrid).OverTime(start, end, step)             │
│   .RunMonteCarlo(N, seed)                                    │
│   .AnalyzeWith(ClusteringGrid + KMeans, SilhouetteEvaluator)│
│   .Build(threshold)                                          │
└──────────────────────┬───────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────┐
│  CSharpNumerics Results                                      │
│  • RiskScenarioResult.ProbabilityAt(pos, time)               │
│  • RiskScenarioResult.ProbabilityMapAt(timeIndex)            │
│  • ScenarioClusterResult.DominantCluster                     │
│  • MonteCarloScenarioResult.ScenarioMatrix                   │
└──────────────────────┬───────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────┐
│  VISUALIZATION (CesiumForUnity)                              │
│  • Probability heatmap overlay on 3D globe                   │
│  • Cumulative probability view                               │
│  • Cluster visualization                                     │
│  • Time-stepping via SetTimeIndex()                          │
└──────────────────────┬───────────────────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────────────────┐
│  EXPORT (CSharpNumerics + CSV helper)                        │
│  • GeoJSON (GeoJsonExporter) — probability contour polygons  │
│  • CZML (CesiumExporter) — Cesium globe visualization        │
│  • Binary (UnityBinaryExporter) — Unity runtime data         │
│  • CSV (custom) — full grid data for external analysis       │
└──────────────────────────────────────────────────────────────┘
```

## Requirements

- **Unity** 2021.3+
- **CSharpNumerics** v2.6.3+ (NuGet: `CSharpNumerics`, .NET Standard 2.1)
- **CesiumForUnity** 1.0+ (for 3D globe visualization)

## Installation

1. Clone this repository into your Unity project's `Assets/` folder
2. Install **CSharpNumerics**:
   - Download from [csnumerics.com](https://csnumerics.com/) or NuGet: `CSharpNumerics`
   - Place `CSharpNumerics.dll` in `Assets/Plugins/`
   - Or use [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) and search `CSharpNumerics`
3. Install [CesiumForUnity](https://cesium.com/platform/cesium-for-unity/) via Unity Package Manager
4. Add `CESIUM_AVAILABLE` to Scripting Define Symbols if using Cesium globe

## Quick Start

1. Add `FalloutSimulationManager` component to a GameObject
2. Add `CesiumFalloutRenderer` component (needs a Cesium Georeference in scene)
3. Configure parameters in the Inspector:
   - **Source coordinates** (lat/lon of the event)
   - **Emission rate** (kg/s) and **radioisotope** (Cs137, I131, Sr90)
   - **Wind speed** (m/s) and **direction vector** (X=east, Y=north)
   - **Monte Carlo iterations** (100–10,000 recommended)
   - **Stability class** (A–F, Pasquill-Gifford)
   - **K-Means cluster counts** to evaluate (e.g. "3,5")
4. Enter Play Mode and click **RUN SIMULATION**
5. Results are visualized on the Cesium globe and exported to the configured directory

## Project Structure

```
Assets/NuclearFalloutML/
├── Runtime/
│   └── Scripts/
│       ├── Core/
│       │   ├── FalloutSimulationManager.cs   # RiskScenario pipeline orchestrator
│       │   ├── SimulationConfig.cs           # Inspector-friendly parameters
│       │   ├── GeoCoordinate.cs              # GeoCoordinateFactory bridge
│       │   ├── GeoGrid.cs                    # GeoGridFactory bridge
│       │   └── SimulationResults.cs          # Result extension helpers
│       ├── Physics/
│       │   └── GaussianPuffModel.cs          # → PlumeSimulator (CSharpNumerics)
│       ├── MonteCarlo/
│       │   └── MonteCarloEngine.cs           # → PlumeMonteCarloModel (CSharpNumerics)
│       ├── ML/
│       │   └── ClusteringBridge.cs           # → ClusteringGrid + KMeans (CSharpNumerics)
│       ├── Probability/
│       │   └── ProbabilityMapGenerator.cs    # → ProbabilityMap (CSharpNumerics)
│       ├── Visualization/
│       │   ├── CesiumFalloutRenderer.cs      # Cesium globe overlay from results
│       │   └── FalloutColorMapper.cs         # Color mapping utilities
│       ├── Export/
│       │   └── FalloutExporter.cs            # CSV helper + SaveAll (delegates to CSharpNumerics)
│       ├── Numerics/
│       │   └── NumericsEngine.cs             # MonteCarloSimulator bridge
│       └── UI/
│           └── SimulationUIController.cs     # Runtime UI controller
├── Editor/
│   └── Scripts/
│       └── FalloutSimulationEditor.cs        # Custom Inspector
└── package.json
```

## CSharpNumerics GeoEngine — Core API

The entire simulation pipeline is built on **CSharpNumerics** (NuGet: `CSharpNumerics`, v2.6.3+, by Göran Bäcklund / backlundtransform).

### RiskScenario Fluent API

```csharp
using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Grid;
using CSharpNumerics.Physics.Materials;
using CSharpNumerics.ML;
using CSharpNumerics.Numerics;

var result = RiskScenario
    .ForGaussianPlume(5.0)                           // emission rate kg/s
    .FromSource(new Vector(0, 0, 50))                // source position
    .WithWind(10, new Vector(1, 0, 0))               // wind speed & direction
    .WithStability(StabilityClass.D)                  // Pasquill-Gifford
    .WithMaterial(Materials.Radioisotope("Cs137"))    // radioisotope
    .WithVariation(v => v
        .WindSpeed(8, 12)                             // MC variation range
        .WindDirectionJitter(15)                      // direction σ
        .EmissionRate(3, 7)                           // emission range
        .SetStabilityWeights(d: 0.6, c: 0.2, e: 0.2))
    .OverGrid(new GeoGrid(-500, 500, -500, 500, 0, 100, 10))
    .OverTime(0, 3600, 60)                            // time range
    .RunMonteCarlo(1000)                              // N scenarios
    .AnalyzeWith(
        new ClusteringGrid().AddModel<KMeans>(g => g.Add("K", 3, 5)),
        new SilhouetteEvaluator())
    .Build(threshold: 1e-6);

// Query results
double prob = result.ProbabilityAt(new Vector(100, 50, 0), timeSeconds: 600);
var probMap = result.ProbabilityMapAt(timeIndex: 0);

// Export
result.ExportGeoJson("output/plume.geojson");
result.ExportCesium("output/plume.czml");
result.ExportUnity("output/plume.bin");
```

### Key Namespaces

| Namespace | Types |
|-----------|-------|
| `CSharpNumerics.Engines.GIS.Scenario` | `RiskScenario`, `RiskScenarioResult`, `MonteCarloScenarioResult`, `ScenarioVariation`, `TimeFrame` |
| `CSharpNumerics.Engines.GIS.Simulation` | `PlumeSimulator`, `PlumeMode` |
| `CSharpNumerics.Engines.GIS.Analysis` | `ScenarioClusterAnalyzer`, `ProbabilityMap`, `ScenarioClusterResult`, `TimeAnimator` |
| `CSharpNumerics.Engines.GIS.Grid` | `GeoGrid`, `GridSnapshot`, `GeoCell` |
| `CSharpNumerics.Engines.GIS.Export` | `GeoJsonExporter`, `CesiumExporter`, `UnityBinaryExporter` |
| `CSharpNumerics.Engines.GIS.Coordinates` | `GeoCoordinate`, `Projection`, `ProjectionType` |
| `CSharpNumerics.Physics.Enums` | `StabilityClass` (A–F) |
| `CSharpNumerics.Physics.Materials` | `Materials.Radioisotope()` |
| `CSharpNumerics.ML` | `ClusteringGrid`, `KMeans`, `SilhouetteEvaluator` |
| `CSharpNumerics.Numerics` | `Vector`, `Matrix`, `ScalarField`, `VectorField` |
| `CSharpNumerics.Statistics.MonteCarlo` | `MonteCarloSimulator`, `MonteCarloResult` |

## Configuration Parameters

| Parameter | Range | Default | Description |
|-----------|-------|---------|-------------|
| Source Latitude | −90..90 | 55.605 | Event latitude (WGS84) |
| Source Longitude | −180..180 | 13.004 | Event longitude (WGS84) |
| Emission Rate | kg/s | 5.0 | Mass emission rate |
| Stack Height | 0–2000 m | 50 | Effective release height |
| Radioisotope | string | Cs137 | Material (Cs137, I131, Sr90) |
| Wind Speed | 0.5–50 m/s | 10 | Mean wind speed |
| Wind Direction | vector | (1,0) | Wind direction (X=east, Y=north) |
| Stability Class | A–F | D | Pasquill-Gifford stability |
| MC Iterations | 10–100,000 | 1,000 | Number of scenarios |
| K-Means Counts | csv | "3,5" | Cluster counts to evaluate |
| Grid Extent | meters | 500 | Half-extent from source |
| Grid Step | 1–100 m | 10 | Grid cell size |
| Time Range | seconds | 0–3600 | Simulation time span |
| Prob Threshold | double | 1e-6 | Exceedance threshold |

## Export Formats

| Format | Engine | Description |
|--------|--------|-------------|
| **GeoJSON** | `GeoJsonExporter` | Probability contour polygons — QGIS, Mapbox |
| **CZML** | `CesiumExporter` | Cesium globe time-dynamic visualization |
| **Binary** | `UnityBinaryExporter` | Unity runtime data loading |
| **CSV** | Custom helper | Grid data (index, x, y, z, probability) |

## Physics Model

CSharpNumerics `PlumeSimulator` implements the **Gaussian Plume/Puff** atmospheric dispersion model:

$$C(x,y,0) = \frac{Q}{\pi \sigma_y \sigma_z u} \exp\left(-\frac{y^2}{2\sigma_y^2}\right) \exp\left(-\frac{H^2}{2\sigma_z^2}\right)$$

Supports `PlumeMode.SteadyState` and `PlumeMode.Transient` with configurable puff release duration.

## License

See [LICENSE](LICENSE) file
