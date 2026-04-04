using UnityEngine;
using System;
using System.IO;

namespace QuantumCircuitViz.Export
{
    /// <summary>
    /// Captures screenshots and renders to file.
    /// Supports supersampled (high-res) capture via RenderTexture.
    /// </summary>
    public static class ScreenshotUtility
    {
        /// <summary>
        /// Capture the screen at the given super-sample multiplier and save
        /// to the supplied path (defaults to Desktop/QuantumCircuitViz_{timestamp}.png).
        /// </summary>
        /// <param name="superSize">Resolution multiplier (1 = native, 2 = 4× pixels, etc.)</param>
        /// <param name="filePath">Optional absolute file path.  If null a timestamped
        /// PNG is placed on the user's desktop.</param>
        /// <returns>The absolute path that was written.</returns>
        public static string Capture(int superSize = 2, string filePath = null)
        {
            if (superSize < 1) superSize = 1;

            if (string.IsNullOrEmpty(filePath))
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                filePath = Path.Combine(desktop, $"QuantumCircuitViz_{stamp}.png");
            }

            // Sanitize path
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            ScreenCapture.CaptureScreenshot(filePath, superSize);
            Debug.Log($"[QuantumCircuitViz] Screenshot saved → {filePath}");
            return filePath;
        }

        /// <summary>
        /// Capture a specific camera to a Texture2D (synchronous, single frame).
        /// Useful for programmatic thumbnails or export.
        /// </summary>
        public static Texture2D CaptureCamera(Camera cam, int width = 1920, int height = 1080)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            RenderTexture.active = null;
            rt.Release();
            UnityEngine.Object.Destroy(rt);

            return tex;
        }

        /// <summary>
        /// Capture a camera and write the result directly to a PNG file.
        /// </summary>
        public static string CaptureCameraToPNG(Camera cam, int width = 1920, int height = 1080, string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                filePath = Path.Combine(desktop, $"QuantumCircuitViz_{stamp}.png");
            }

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var tex = CaptureCamera(cam, width, height);
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
            UnityEngine.Object.Destroy(tex);

            Debug.Log($"[QuantumCircuitViz] Camera capture saved → {filePath}");
            return filePath;
        }
    }
}
