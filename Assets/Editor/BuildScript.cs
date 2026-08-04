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
        /// Release skin + mobile-layout spec (#10-#13) for the stock template:
        ///  - title prefix drop, 192px touch icon (deployed from web/);
        ///  - STATIC viewport meta with viewport-fit=cover (safe-area lives in
        ///    CSS; the stock UA-gated JS meta only existed for mobile UAs and
        ///    is superseded by the static one — its injected copy also gets
        ///    viewport-fit=cover for belt-and-braces);
        ///  - devicePixelRatio cap 2 (3x phones would render 1170x2532);
        ///  - responsive canvas: UA-independent CSS replaces the fixed
        ///    1280x853 sizing — letterbox preserving 1280:853 (~3:2) down to
        ///    500px CSS width, full-viewport fill below (phones);
        ///  - brand background #050812 (canvas + page).
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

            // Static viewport meta + responsive canvas CSS — injected AFTER
            // the TemplateData stylesheet so the brand background (#050812)
            // wins the equal-specificity cascade over its #231F20.
            const string styleTag = "<link rel=\"stylesheet\" href=\"TemplateData/style.css\">";
            if (!html.Contains("viewport-fit=cover"))
                html = html.Replace(styleTag, styleTag + "\n    " + ViewportHeadBlock);

            // The stock template injects its own meta for mobile UAs — align it
            // so a UA-gated duplicate never regresses the safe-area behavior.
            html = html.Replace(
                "'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes'",
                "'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes, viewport-fit=cover'");

            // DPR cap 2 (spec #11): 3x phones would render 1170x2532 native.
            // (Guard on the inserted expression — the stock template contains
            // "config.devicePixelRatio = 1" inside a comment.)
            if (!html.Contains("devicePixelRatio: Math.min"))
                html = html.Replace("showBanner: unityShowBanner,",
                    "showBanner: unityShowBanner,\n" +
                    "        devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),");

            // Fixed desktop sizing -> class-driven responsive CSS (spec #12).
            html = html.Replace(
                "        canvas.style.width = \"1280px\";\n" +
                "        canvas.style.height = \"853px\";",
                "        canvas.classList.add(\"unity-responsive\");");
            // Whitespace-variant fallback (template indentation differs
            // between Unity versions).
            html = html.Replace("canvas.style.width = \"1280px\";", "canvas.classList.add(\"unity-responsive\");");
            html = html.Replace("canvas.style.height = \"853px\";", "");

            File.WriteAllText(indexPath, html);
        }

        /// <summary>Head block: static viewport meta + responsive canvas CSS.
        /// Kept verbatim-shared with build-webgl/index.html (patched in-place
        /// for smoke tests without a rebuild).</summary>
        const string ViewportHeadBlock =
@"<meta name=""viewport"" content=""width=device-width, initial-scale=1, viewport-fit=cover"">
    <style>
      /* mobile-layout spec #10-#13: brand letterbox + responsive canvas */
      html, body { background: #050812; }
      #unity-canvas { background: #050812; }
      /* Desktop / wide: letterbox preserving 1280:853 (~3:2), never overflow
         the viewport (spec #12 — fixes sub-1280 windows + iPadOS desktop UA). */
      #unity-canvas.unity-responsive {
        width: min(1280px, 100vw, calc(100vh * 1280 / 853));
        height: auto;
        aspect-ratio: 1280 / 853;
      }
      /* Narrow viewports (~phones reporting desktop UA): fill (spec #13 —
         3D perspective renders any aspect; camera aspect-widen keeps the
         arena visible; HUD corner-anchors adapt). */
      @media (max-width: 500px) {
        #unity-container.unity-desktop {
          left: 0; top: 0; transform: none;
          position: fixed; width: 100%; height: 100%;
        }
        #unity-canvas.unity-responsive {
          width: 100%; height: 100%; aspect-ratio: auto;
        }
        #unity-footer { display: none; }
      }
      /* Notch safe-area (spec #10): pad the mobile container, not the canvas —
         canvas padding would skew the WebGL pointer mapping. */
      #unity-container.unity-mobile {
        padding: env(safe-area-inset-top) env(safe-area-inset-right)
                 env(safe-area-inset-bottom) env(safe-area-inset-left);
        box-sizing: border-box; background: #050812;
      }
    </style>";
    }
}
