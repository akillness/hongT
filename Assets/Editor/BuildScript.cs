using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class BuildScript
    {
        const string SocialPreviewFile = "cinder-court-link-preview.png";
        const string SocialPreviewSource = "docs/branding/" + SocialPreviewFile;
        const string BuildCacheVersionMarkerPrefix = "/* CinderCourt WebGL build cache version: ";
        const string BuildCacheVersionMarkerSuffix = " */";
        static readonly string[] WebGlBuildResources =
        {
            "build-webgl.loader.js",
            "build-webgl.data.unityweb",
            "build-webgl.framework.js.unityweb",
            "build-webgl.wasm.unityweb",
        };


        public static void BuildWebGL()
        {
            var originalWebGlDefines = ExcludeEditorToolingFromWebGl();
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
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                // Build-scoped strip only: put the tracked ProjectSettings
                // define set back no matter how BuildPlayer ends, so batch
                // builds never leave WebGL-group churn in a tracked file.
                RestoreWebGlDefines(originalWebGlDefines);
            }
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
        /// MCP editor tooling (com.ivanmurzak.unity.mcp packages + the NuGet
        /// DLLs its resolver installs under Assets/Plugins/NuGet) must never
        /// ship in the WebGL player:
        ///  - the resolver marks runtime-capable DLLs anyPlatform=1 on every
        ///    domain reload (NuGetPluginConfigurator.ConfigureDll), which
        ///    would IL2CPP ~17 MB of SignalR/Json/R3/Roslyn-adjacent managed
        ///    code into the wasm and reference socket APIs WebGL cannot run;
        ///  - the UNITY_MCP_READY define (installed project-wide by the
        ///    resolver) would compile the MCP Runtime asmdef into the player.
        /// Hand-editing the .meta files does not survive the resolver's
        /// convergence pass, so both levers are re-asserted here at build
        /// time, immediately before BuildPlayer. Editor targets are left
        /// untouched — the MCP tooling keeps working in the editor.
        /// </summary>
        static string ExcludeEditorToolingFromWebGl()
        {
            var webgl = NamedBuildTarget.WebGL;
            var defines = PlayerSettings.GetScriptingDefineSymbols(webgl);
            var kept = new List<string>();
            var stripped = false;
            foreach (var define in defines.Split(';'))
            {
                if (define == "UNITY_MCP_READY") { stripped = true; continue; }
                if (define.Length > 0) kept.Add(define);
            }
            if (stripped)
            {
                PlayerSettings.SetScriptingDefineSymbols(webgl, string.Join(";", kept));
                Debug.Log("[BuildWebGL] stripped UNITY_MCP_READY from the WebGL define set (build-scoped)");
            }

            var excluded = 0;
            foreach (var importer in PluginImporter.GetAllImporters())
            {
                if (importer == null) continue;
                if (!importer.assetPath.StartsWith("Assets/Plugins/NuGet/", StringComparison.Ordinal))
                    continue;

                var dirty = false;
                if (importer.GetCompatibleWithAnyPlatform())
                {
                    if (!importer.GetExcludeFromAnyPlatform("WebGL"))
                    {
                        importer.SetExcludeFromAnyPlatform("WebGL", true);
                        dirty = true;
                    }
                }
                else if (importer.GetCompatibleWithPlatform(BuildTarget.WebGL))
                {
                    importer.SetCompatibleWithPlatform(BuildTarget.WebGL, false);
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    excluded++;
                }
            }
            if (excluded > 0)
                Debug.Log($"[BuildWebGL] excluded {excluded} NuGet tooling DLL(s) from the WebGL player");
            return defines;
        }

        static void RestoreWebGlDefines(string defines)
        {
            var webgl = NamedBuildTarget.WebGL;
            if (PlayerSettings.GetScriptingDefineSymbols(webgl) != defines)
                PlayerSettings.SetScriptingDefineSymbols(webgl, defines);
        }

        /// <summary>
        /// Release skin + mobile-layout spec (#10-#13) for the stock template:
        ///  - title prefix drop, 192px touch icon (deployed from web/);
        ///  - STATIC viewport meta with viewport-fit=cover (safe-area lives in
        ///    CSS; the stock UA-gated JS meta only existed for mobile UAs and
        ///    is superseded by the static one — its inserted copy also gets
        ///    viewport-fit=cover for belt-and-braces);
        ///  - social preview relative OG/Twitter metadata + image copy;
        ///  - devicePixelRatio cap 2 (3x phones would render 1170x2532);
        ///  - fixed backing store: Unity 6's automatic canvas-resize path
        ///    recurses on a full-viewport portrait canvas, so the backing
        ///    store is explicitly sized before the loader and after responsive
        ///    viewport, orientation, and fullscreen resizes;
        ///  - viewport-locked page, block canvas, and explicit 38px footer;
        ///  - responsive desktop canvas: preserves 1280:853 (~3:2), subtracts
        ///    the footer from 100vh/100svh, and stays centered in the viewport;
        ///  - full-viewport fill for narrow or low landscape viewports, with
        ///    100dvh enhancement and mobile safe-area padding;
        ///  - brand background #050812 (canvas + page).
        /// </summary>
        static void PolishIndexHtml(string outputDir)
        {
            var indexPath = Path.Combine(outputDir, "index.html");
            if (!File.Exists(indexPath))
                throw new FileNotFoundException("WebGL build did not produce index.html", indexPath);
            var html = File.ReadAllText(indexPath);
            html = html.Replace("<title>Unity Web Player | ", "<title>");

            const string faviconTag = "<link rel=\"shortcut icon\" href=\"TemplateData/favicon.ico\">";
            if (!html.Contains("apple-touch-icon"))
                html = html.Replace(faviconTag,
                    faviconTag + "\n    <link rel=\"apple-touch-icon\" href=\"app-icon-192.png\">");

            if (!html.Contains("name=\"twitter:card\""))
            {
                const string headEnd = "</head>";
                if (html.Contains(headEnd))
                    html = html.Replace(headEnd, $"{SocialHeadBlock}\n  {headEnd}");
            }

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

            html = VersionWebGlBuildAssetUrls(outputDir, html);

            // OS reduced-motion hint (integrated-combat-vfx-spec §2.4): the
            // shell mirrors matchMedia("(prefers-reduced-motion: reduce)")
            // into localStorage before the loader so ViewPrefs can seed its
            // default for players who never touched the lobby toggle. An
            // explicit toggle (PlayerPrefs key) always wins over the hint.
            html = WebGlReducedMotionSeed.Inject(html);

            // Unity 6.0.5's automatic canvas-matching loop overflows its WASM
            // stack when the responsive CSS makes a narrow portrait viewport
            // fill the canvas. Disable Unity's auto-match loop; the backing
            // store is set from the rendered canvas rect immediately before
            // the loader starts, after the UA-specific class is in place.
            const string backingStoreMarker = "/* CinderCourt fixed WebGL backing store */";
            if (!html.Contains(backingStoreMarker))
                html = html.Replace(
                    "// By default, Unity keeps WebGL canvas render target size matched with",
                    "/* CinderCourt fixed WebGL backing store */\n" +
                    "      config.matchWebGLToCanvasSize = false;\n\n" +
                    "      // By default, Unity keeps WebGL canvas render target size matched with");

            // Fixed desktop sizing -> class-driven responsive CSS (spec #12).
            html = html.Replace(
                "        canvas.style.width = \"1280px\";\n" +
                "        canvas.style.height = \"853px\";",
                "        canvas.classList.add(\"unity-responsive\");");
            // Whitespace-variant fallback (template indentation differs
            // between Unity versions).
            html = html.Replace("canvas.style.width = \"1280px\";", "canvas.classList.add(\"unity-responsive\");");
            html = html.Replace("canvas.style.height = \"853px\";", "");

            const string backingStoreCall = "setUnityBackingStoreSize();";
            if (!html.Contains(backingStoreCall))
                html = html.Replace(
                    "document.querySelector(\"#unity-loading-bar\").style.display = \"block\";",
                    "function setUnityBackingStoreSize() {\n" +
                    "        var rect = canvas.getBoundingClientRect();\n" +
                    "        var scale = Math.min(window.devicePixelRatio || 1, 2);\n" +
                    "        canvas.width = Math.max(1, Math.round(rect.width * scale));\n" +
                    "        canvas.height = Math.max(1, Math.round(rect.height * scale));\n" +
                    "      }\n" +
                    "      var backingStoreResizeRequest = 0;\n" +
                    "      function queueUnityBackingStoreSize() {\n" +
                    "        if (backingStoreResizeRequest)\n" +
                    "          window.cancelAnimationFrame(backingStoreResizeRequest);\n" +
                    "        backingStoreResizeRequest = window.requestAnimationFrame(function() {\n" +
                    "          backingStoreResizeRequest = 0;\n" +
                    "          setUnityBackingStoreSize();\n" +
                    "        });\n" +
                    "      }\n" +
                    "      window.addEventListener(\"resize\", queueUnityBackingStoreSize);\n" +
                    "      window.addEventListener(\"orientationchange\", queueUnityBackingStoreSize);\n" +
                    "      if (window.visualViewport)\n" +
                    "        window.visualViewport.addEventListener(\"resize\", queueUnityBackingStoreSize);\n" +
                    "      setUnityBackingStoreSize();\n\n" +
                    "      document.querySelector(\"#unity-loading-bar\").style.display = \"block\";");

            const string startupErrorMarker = "unityShowBanner(message, \"error\");";
            if (!html.Contains(startupErrorMarker))
                html = html.Replace(
                    "alert(message);",
                    "document.querySelector(\"#unity-loading-bar\").style.display = \"none\";\n" +
                    "                unityShowBanner(message, \"error\");");

            File.WriteAllText(indexPath, html);
            CopySocialPreview(outputDir);
            VerifyWebGlShell(indexPath, outputDir, html);
        }
        static string VersionWebGlBuildAssetUrls(string outputDir, string html)
        {
            var currentVersion = ComputeWebGlBuildVersion(outputDir);
            var currentMarker = BuildCacheVersionMarkerPrefix + currentVersion + BuildCacheVersionMarkerSuffix;
            var markerStart = html.IndexOf(BuildCacheVersionMarkerPrefix, StringComparison.Ordinal);
            if (markerStart >= 0)
            {
                var markerEnd = html.IndexOf(BuildCacheVersionMarkerSuffix,
                    markerStart + BuildCacheVersionMarkerPrefix.Length, StringComparison.Ordinal);
                if (markerEnd < 0)
                    throw new InvalidDataException("WebGL build cache marker is malformed");

                var previousMarker = html.Substring(
                    markerStart,
                    markerEnd + BuildCacheVersionMarkerSuffix.Length - markerStart);
                html = html.Replace(previousMarker, currentMarker);
            }
            else
            {
                const string buildUrlDeclaration = "var buildUrl = \"Build\";";
                if (!html.Contains(buildUrlDeclaration))
                    throw new InvalidDataException("WebGL index does not declare its Build URL");

                html = html.Replace(buildUrlDeclaration, currentMarker + "\n      " + buildUrlDeclaration);
            }

            for (var i = 0; i < WebGlBuildResources.Length; i++)
                html = SetWebGlBuildResourceVersion(html, WebGlBuildResources[i], currentVersion);

            return html;
        }

        static string SetWebGlBuildResourceVersion(string html, string resource, string version)
        {
            var pattern = Regex.Escape(resource) + @"(?:\?v=[A-Za-z0-9][A-Za-z0-9._-]*)?(?=[""'])";
            var resourcePattern = new Regex(pattern);
            if (resourcePattern.Matches(html).Count != 1)
                throw new InvalidDataException($"WebGL index must reference '{resource}' exactly once");

            return resourcePattern.Replace(html, resource + "?v=" + version, 1);
        }


        static string ComputeWebGlBuildVersion(string outputDir)
        {
            var combinedResourceHashes = new byte[WebGlBuildResources.Length * 32];
            for (var i = 0; i < WebGlBuildResources.Length; i++)
            {
                var resourcePath = Path.Combine(outputDir, "Build", WebGlBuildResources[i]);
                if (!File.Exists(resourcePath))
                    throw new FileNotFoundException("WebGL build resource is missing", resourcePath);

                byte[] resourceHash;
                using (var resourceStream = File.OpenRead(resourcePath))
                using (var resourceHasher = SHA256.Create())
                    resourceHash = resourceHasher.ComputeHash(resourceStream);

                Buffer.BlockCopy(resourceHash, 0, combinedResourceHashes, i * resourceHash.Length, resourceHash.Length);
            }

            byte[] combinedHash;
            using (var combinedHasher = SHA256.Create())
                combinedHash = combinedHasher.ComputeHash(combinedResourceHashes);

            const string hex = "0123456789abcdef";
            var versionCharacters = new char[16];
            for (var i = 0; i < versionCharacters.Length / 2; i++)
            {
                var value = combinedHash[i];
                versionCharacters[i * 2] = hex[value >> 4];
                versionCharacters[i * 2 + 1] = hex[value & 0x0f];
            }

            return new string(versionCharacters);
        }
        static int CountOccurrences(string value, string token)
        {
            var count = 0;
            var offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count += 1;
                offset += token.Length;
            }

            return count;
        }


        
        static void VerifyWebGlShell(string indexPath, string outputDir, string html)
        {
            var requiredMarkers = new[]
            {
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, viewport-fit=cover\">",
                "canvas.classList.add(\"unity-responsive\");",
                "devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2)",
                "window.localStorage.setItem(\"" + WebGlReducedMotionSeed.StorageKey + "\",",
                WebGlReducedMotionSeed.MediaQueryProbe,
                "config.matchWebGLToCanvasSize = false;",
                "function queueUnityBackingStoreSize()",
                "window.addEventListener(\"resize\", queueUnityBackingStoreSize);",
                "window.addEventListener(\"orientationchange\", queueUnityBackingStoreSize);",
                "window.visualViewport.addEventListener(\"resize\", queueUnityBackingStoreSize);",
                "html, body { width: 100%; height: 100%; overflow: hidden;",
                "#unity-canvas { display: block;",
                "#unity-footer { height: 38px; }",
                "width: min(1280px, 100vw, calc((100vh - 38px) * 1280 / 853));",
                "@supports (height: 100svh)",
                "width: min(1280px, 100vw, calc((100svh - 38px) * 1280 / 853));",
                "aspect-ratio: 1280 / 853;",
                "@media (max-width: 500px), (max-height: 500px) and (orientation: landscape)",
                "#unity-container.unity-desktop {\n" +
                "          left: 0; top: 0; transform: none;\n" +
                "          position: fixed; width: 100vw; height: 100vh; height: 100dvh;\n" +
                "          padding: env(safe-area-inset-top) env(safe-area-inset-right)\n" +
                "                   env(safe-area-inset-bottom) env(safe-area-inset-left);\n" +
                "          box-sizing: border-box; background: #050812;\n" +
                "        }",
                "width: 100%; height: 100%; aspect-ratio: auto;",
                "#unity-footer { display: none; }",
                "#unity-container.unity-mobile {",
                "padding: env(safe-area-inset-top) env(safe-area-inset-right)",
                "canvas.getBoundingClientRect();",
                "unityShowBanner(message, \"error\");",
                "<meta property=\"og:image\" content=\"./cinder-court-link-preview.png\">",
                "<meta name=\"twitter:image\" content=\"./cinder-court-link-preview.png\">",
            };

            var currentVersion = ComputeWebGlBuildVersion(outputDir);
            var currentMarker = BuildCacheVersionMarkerPrefix + currentVersion + BuildCacheVersionMarkerSuffix;
            if (CountOccurrences(html, currentMarker) != 1)
                throw new InvalidDataException($"WebGL index cache marker is stale or malformed: {indexPath}");

            for (var i = 0; i < WebGlBuildResources.Length; i++)
            {
                var resource = WebGlBuildResources[i];
                var resourceUrl = resource + "?v=" + currentVersion;
                if (CountOccurrences(html, resource) != 1 || CountOccurrences(html, resourceUrl) != 1)
                    throw new InvalidDataException($"WebGL index cache-bust contract is not atomic for '{resource}': {indexPath}");
            }
            for (var i = 0; i < requiredMarkers.Length; i++)
            {
                if (!html.Contains(requiredMarkers[i]))
                    throw new InvalidDataException($"WebGL index contract missing '{requiredMarkers[i]}': {indexPath}");
            }

            var previewPath = Path.Combine(outputDir, SocialPreviewFile);
            if (!File.Exists(previewPath))
                throw new FileNotFoundException("WebGL social preview was not copied", previewPath);
        }

        static void CopySocialPreview(string outputDir)
        {
            var source = Path.GetFullPath(SocialPreviewSource);
            if (!File.Exists(source))
            {
                Debug.LogWarning($"[BuildWebGL] social preview source missing: {source}");
                return;
            }

            var destination = Path.Combine(outputDir, SocialPreviewFile);
            File.Copy(source, destination, true);
        }

        /// <summary>Head block: viewport lock, centered desktop canvas/footer,
        /// full-viewport mobile/low-landscape layout, and safe-area CSS.
        /// Kept as one idempotently injected block for generated index pages.</summary>
        const string ViewportHeadBlock =
@"<meta name=""viewport"" content=""width=device-width, initial-scale=1, viewport-fit=cover"">
    <style>
      /* mobile-layout spec #10-#13: brand letterbox + responsive canvas */
      html, body { width: 100%; height: 100%; overflow: hidden; background: #050812; }
      #unity-canvas { display: block; background: #050812; touch-action: none; }
      #unity-footer { height: 38px; }
      /* Desktop / wide: center the canvas and its footer while preserving
         1280:853 (~3:2) inside the available viewport. */
      #unity-canvas.unity-responsive {
        width: min(1280px, 100vw, calc((100vh - 38px) * 1280 / 853));
        height: auto;
        aspect-ratio: 1280 / 853;
      }
      @supports (height: 100svh) {
        #unity-canvas.unity-responsive {
          width: min(1280px, 100vw, calc((100svh - 38px) * 1280 / 853));
        }
      }
      /* Phones and low landscape viewports fill the visible screen. */
      @media (max-width: 500px), (max-height: 500px) and (orientation: landscape) {
        #unity-container.unity-desktop {
          left: 0; top: 0; transform: none;
          position: fixed; width: 100vw; height: 100vh; height: 100dvh;
          padding: env(safe-area-inset-top) env(safe-area-inset-right)
                   env(safe-area-inset-bottom) env(safe-area-inset-left);
          box-sizing: border-box; background: #050812;
        }
        #unity-canvas.unity-responsive {
          width: 100%; height: 100%; aspect-ratio: auto;
        }
        #unity-footer { display: none; }
      }
      /* Notch safe-area (spec #10): pad the mobile container, not the canvas —
         canvas padding would skew the WebGL pointer mapping. */
      #unity-container.unity-mobile {
        position: fixed; width: 100vw; height: 100vh; height: 100dvh;
        padding: env(safe-area-inset-top) env(safe-area-inset-right)
                 env(safe-area-inset-bottom) env(safe-area-inset-left);
        box-sizing: border-box; background: #050812;
      }
    </style>";
        const string SocialHeadBlock =
@"<meta property=""og:type"" content=""website"">
    <meta property=""og:title"" content=""Abyssal Lantern — Cinder Court"">
    <meta property=""og:description"" content=""A dark fantasy dungeon crawl as the Lantern Reaver through Cinder Court."">
    <meta property=""og:image"" content=""./cinder-court-link-preview.png"">
    <meta name=""twitter:card"" content=""summary_large_image"">
    <meta name=""twitter:title"" content=""Abyssal Lantern — Cinder Court"">
    <meta name=""twitter:description"" content=""A dark fantasy dungeon crawl as the Lantern Reaver through Cinder Court."">
    <meta name=""twitter:image"" content=""./cinder-court-link-preview.png"">";
    }
}

