// Export is handled natively by CSharpNumerics GeoEngine exporters:
//
//   GeoJsonExporter.Save(riskResult, path)    — GeoJSON with probability polygons
//   CesiumExporter.Save(riskResult, path)     — CZML for Cesium globe
//   UnityBinaryExporter.Save(riskResult, path)— Binary for Unity runtime
//
// The RiskScenarioResult also exposes convenience methods:
//   riskResult.ExportGeoJson(path)
//   riskResult.ExportCesium(path)
//   riskResult.ExportUnity(path)
//
// FalloutSimulationManager calls these directly in RunSimulation().
//
// This helper class provides additional CSV export not covered by
// CSharpNumerics, plus a unified SaveAll entry point.

using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

using CSharpNumerics.Engines.GIS.Scenario;
using CSharpNumerics.Engines.GIS.Analysis;
using CSharpNumerics.Engines.GIS.Export;
using NuclearFalloutML.Core;

namespace NuclearFalloutML.Export
{
    /// <summary>
    /// Supplementary export utilities.
    /// Core GeoJSON / CZML / binary export is delegated to CSharpNumerics exporters.
    /// This class adds CSV grid export and a convenience SaveAll method.
    /// </summary>
    public static class FalloutExporter
    {
        /// <summary>
        /// Export probability grid to CSV for analysis in Python / R / Excel.
        /// CSharpNumerics does not include a CSV exporter, so we provide one here.
        /// </summary>
        public static string ExportCsv(RiskScenarioResult result, int timeIndex = 0)
        {
            var probMap = result.ProbabilityMapAt(timeIndex: timeIndex);
            var snapshots = result.Snapshots;
            if (snapshots == null || snapshots.Count == 0) return "";

            var grid = snapshots[0].Grid;
            int cellCount = grid.CellCount;

            var sb = new StringBuilder();
            sb.AppendLine("cell_index,x,y,z,probability");

            for (int i = 0; i < cellCount; i++)
            {
                var pos = grid.CellCentre(i);
                double prob = probMap.At(pos);
                if (prob < 1e-10) continue;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F2},{2:F2},{3:F2},{4:E4}",
                    i, pos.X, pos.Y, pos.Z, prob));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Save all export formats to the given directory.
        /// Delegates GeoJSON, CZML, and binary to CSharpNumerics;
        /// adds CSV from this helper.
        /// </summary>
        public static void SaveAll(RiskScenarioResult result, string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            // CSharpNumerics native exporters
            result.ExportGeoJson(Path.Combine(outputDir, "fallout_probability.geojson"));
            result.ExportCesium(Path.Combine(outputDir, "fallout_plume.czml"));
            result.ExportUnity(Path.Combine(outputDir, "fallout_plume.bin"));

            // Supplementary CSV
            string csv = ExportCsv(result);
            if (!string.IsNullOrEmpty(csv))
                File.WriteAllText(Path.Combine(outputDir, "fallout_data.csv"), csv);

            Debug.Log($"[FalloutExporter] Exported all results to: {outputDir}");
        }
    }
}
