using System;
using UnityEngine;

namespace NuclearFalloutML.Visualization
{
    /// <summary>
    /// Maps scalar values to colors for fallout visualization.
    /// </summary>
    public static class FalloutColorMapper
    {
        /// <summary>
        /// Map probability (0-1) to a red-yellow-green color with alpha.
        /// </summary>
        public static Color ProbabilityToColor(double probability)
        {
            float p = Mathf.Clamp01((float)probability);
            if (p < 0.001f) return Color.clear;

            // Green (low) → Yellow → Orange → Red (high)
            Color color;
            if (p < 0.25f)
                color = Color.Lerp(new Color(0, 0.8f, 0), Color.yellow, p / 0.25f);
            else if (p < 0.5f)
                color = Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (p - 0.25f) / 0.25f);
            else
                color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (p - 0.5f) / 0.5f);

            color.a = Mathf.Lerp(0.3f, 0.85f, p);
            return color;
        }

        /// <summary>
        /// Map dose rate (mSv) to a color using logarithmic scale.
        /// </summary>
        public static Color DoseToColor(double doseMSv, double maxDose)
        {
            if (doseMSv <= 0 || maxDose <= 0) return Color.clear;

            // Log scale normalization
            double logDose = Math.Log10(Math.Max(doseMSv, 1e-6));
            double logMax = Math.Log10(Math.Max(maxDose, 1e-6));
            double logMin = logMax - 4; // 4 orders of magnitude range

            float t = Mathf.Clamp01((float)((logDose - logMin) / (logMax - logMin)));

            // Blue → Cyan → Green → Yellow → Red
            Color color;
            if (t < 0.25f)
                color = Color.Lerp(new Color(0, 0, 0.5f), Color.cyan, t / 0.25f);
            else if (t < 0.5f)
                color = Color.Lerp(Color.cyan, Color.green, (t - 0.25f) / 0.25f);
            else if (t < 0.75f)
                color = Color.Lerp(Color.green, Color.yellow, (t - 0.5f) / 0.25f);
            else
                color = Color.Lerp(Color.yellow, Color.red, (t - 0.75f) / 0.25f);

            color.a = Mathf.Lerp(0.2f, 0.9f, t);
            return color;
        }

        /// <summary>
        /// Map cluster label to a distinct color.
        /// </summary>
        public static Color ClusterToColor(int clusterLabel, int totalClusters)
        {
            if (clusterLabel < 0) return new Color(0.5f, 0.5f, 0.5f, 0.3f); // Noise = grey

            float hue = (float)clusterLabel / Math.Max(totalClusters, 1);
            return Color.HSVToRGB(hue, 0.8f, 0.9f);
        }
    }
}
