// This module is provided by CSharpNumerics.Engines.GIS.Coordinates.GeoCoordinate.
// See: https://csnumerics.com/docs/Csharpnumerics/Simulation%20Engines/Geo%20Engine/
//
// Usage in Unity scripts:
//   using CSharpNumerics.Engines.GIS.Coordinates;
//   var coord = new GeoCoordinate(59.3293, 18.0686, altitude: 25);
//   double dist = coord.DistanceTo(other);
//
// Projection support:
//   var proj = new Projection(coord, ProjectionType.LocalTangentPlane);
//   Vector local = proj.ToLocal(59.34, 18.08);
//   GeoCoordinate back = proj.ToGeo(local);
//
// Geo-referenced grid:
//   var geoGrid = GeoGrid.FromLatLon(sw, ne, altMin: 0, altMax: 100, step: 50);
//   GeoCoordinate cellGeo = geoGrid.CellCentreGeo(flatIndex);

using CSharpNumerics.Engines.GIS.Coordinates;

namespace NuclearFalloutML.Core
{
    /// <summary>
    /// Re-exports CSharpNumerics.Engines.GIS.Coordinates.GeoCoordinate
    /// for convenience in Unity scripts that import NuclearFalloutML.Core.
    /// </summary>
    public static class GeoCoordinateFactory
    {
        /// <summary>
        /// Create a CSharpNumerics GeoCoordinate from latitude, longitude, altitude.
        /// </summary>
        public static GeoCoordinate Create(double latitude, double longitude, double altitude = 0)
        {
            return new GeoCoordinate(latitude, longitude, altitude: altitude);
        }

        /// <summary>
        /// Create a Projection for converting between WGS84 and local coordinates.
        /// </summary>
        public static Projection CreateProjection(GeoCoordinate origin,
            ProjectionType type = ProjectionType.LocalTangentPlane)
        {
            return new Projection(origin, type);
        }
    }
}
