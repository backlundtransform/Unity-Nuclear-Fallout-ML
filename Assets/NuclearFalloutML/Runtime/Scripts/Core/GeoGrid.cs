// This module is provided by CSharpNumerics.Engines.GIS.Grid.GeoGrid.
// See: https://csnumerics.com/docs/Csharpnumerics/Simulation%20Engines/Geo%20Engine/
//
// Usage:
//   using CSharpNumerics.Engines.GIS.Grid;
//   var grid = new GeoGrid(xMin: -500, xMax: 500, yMin: -500, yMax: 500,
//                           zMin: 0, zMax: 100, step: 10);
//   int totalCells = grid.CellCount;
//   Vector centre = grid.CellCentre(5, 10, 3);
//
// Geo-referenced grid from lat/lon:
//   using CSharpNumerics.Engines.GIS.Coordinates;
//   var sw = new GeoCoordinate(59.32, 18.06);
//   var ne = new GeoCoordinate(59.34, 18.10);
//   var geoGrid = GeoGrid.FromLatLon(sw, ne, altMin: 0, altMax: 100, step: 50);
//
// GridSnapshot holds scalar values for one time step:
//   var snapshot = new GridSnapshot(grid, values, time: 60.0, timeIndex: 1);
//   double val = snapshot[5, 10, 0];
//   double maxVal = snapshot.Max();
//   var hotCells = snapshot.CellsAbove(threshold: 1e-6);

using CSharpNumerics.Engines.GIS.Grid;
using CSharpNumerics.Engines.GIS.Coordinates;

namespace NuclearFalloutML.Core
{
    /// <summary>
    /// Factory for creating CSharpNumerics GeoGrids from SimulationConfig.
    /// </summary>
    public static class GeoGridFactory
    {
        /// <summary>
        /// Create a GeoGrid from SimulationConfig parameters.
        /// </summary>
        public static GeoGrid FromConfig(SimulationConfig config)
        {
            return new GeoGrid(
                xMin: -config.GridExtentMeters,
                xMax: config.GridExtentMeters,
                yMin: -config.GridExtentMeters,
                yMax: config.GridExtentMeters,
                zMin: 0,
                zMax: config.GridAltitudeMaxMeters,
                step: config.GridStepMeters);
        }

        /// <summary>
        /// Create a geo-referenced GeoGrid from lat/lon bounding box.
        /// </summary>
        public static GeoGrid FromLatLonBounds(
            double swLat, double swLon,
            double neLat, double neLon,
            float altMax, float step)
        {
            var sw = new GeoCoordinate(swLat, swLon);
            var ne = new GeoCoordinate(neLat, neLon);
            return GeoGrid.FromLatLon(sw, ne, altMin: 0, altMax: altMax, step: step);
        }
    }
}
