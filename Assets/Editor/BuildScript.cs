// WebGL build entry. Unity -batchmode -executeMethod CinderCourt.EditorTools.BuildScript.BuildWebGL
// Output: build-webgl/ (gitignored). GitHub Pages friendly: gzip + decompression
// fallback (no server config needed), relative template paths.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class BuildScript
    {
        public static void BuildWebGL()
        {
            var output = "build-webgl";
            // Transparent-variant seed must exist BEFORE the player build, or
            // URP strips _SURFACE_TYPE_TRANSPARENT and all runtime transparent
            // materials (vents, ward shell, pickups) render opaque on WebGL.
            RuntimeMaterialSeeds.Seed();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // Pages: no Content-Encoding config
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 853;
            PlayerSettings.companyName = "HongT";
            PlayerSettings.productName = "Abyssal Lantern — Cinder Court";
            PlayerSettings.SetIl2CppCompilerConfiguration(
                NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(
                NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/CinderCourt.unity" },
                target = BuildTarget.WebGL,
                locationPathName = output,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[BuildWebGL] result={summary.result} size={summary.totalSize} " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings} time={summary.totalTime}");
            if (summary.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw new Exception("WebGL build failed");
            }
            PolishIndexHtml(output);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Release skin for the stock template: drop the "Unity Web Player |"
        /// title prefix and link the 192px touch icon (deployed from web/).
        /// </summary>
        static void PolishIndexHtml(string outputDir)
        {
            var indexPath = Path.Combine(outputDir, "index.html");
            if (!File.Exists(indexPath)) return;
            var html = File.ReadAllText(indexPath);
            html = html.Replace("<title>Unity Web Player | ", "<title>");
            const string faviconTag = "<link rel=\"shortcut icon\" href=\"TemplateData/favicon.ico\">";
            if (!html.Contains("apple-touch-icon"))
                html = html.Replace(faviconTag,
                    faviconTag + "\n    <link rel=\"apple-touch-icon\" href=\"app-icon-192.png\">");
            File.WriteAllText(indexPath, html);
        }
    }
}
