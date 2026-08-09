using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class BuildScriptWebGlPostprocessTests
    {
        private string _outputDirectory;

        [SetUp]
        public void SetUp()
        {
            _outputDirectory = Path.Combine(Path.GetTempPath(), "CinderCourt-WebGlPostprocess-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_outputDirectory);
            var buildDirectory = Path.Combine(_outputDirectory, "Build");
            Directory.CreateDirectory(buildDirectory);
            File.WriteAllText(Path.Combine(buildDirectory, "build-webgl.loader.js"), "loader fixture");
            File.WriteAllText(Path.Combine(buildDirectory, "build-webgl.data.unityweb"), "data fixture");
            File.WriteAllText(Path.Combine(buildDirectory, "build-webgl.framework.js.unityweb"), "framework fixture");
            File.WriteAllText(Path.Combine(buildDirectory, "build-webgl.wasm.unityweb"), "wasm fixture");
            File.WriteAllText(Path.Combine(_outputDirectory, "index.html"), StockTemplateHtml);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_outputDirectory))
                Directory.Delete(_outputDirectory, true);
        }

        [Test]
        public void PolishIndexHtml_ResyncsResponsiveBackingStoreBeforeLoader_AndIsIdempotent()
        {
            InvokePolishIndexHtml(_outputDirectory);
            var firstPassHtml = File.ReadAllText(Path.Combine(_outputDirectory, "index.html"));
            InvokePolishIndexHtml(_outputDirectory);

            var html = File.ReadAllText(Path.Combine(_outputDirectory, "index.html"));
            Assert.That(html, Is.EqualTo(firstPassHtml),
                "a repeated postprocess must preserve the complete cache-busted WebGL shell");
            const string autoMatchDisabled = "config.matchWebGLToCanvasSize = false;";
            const string devicePixelRatioCap = "devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),";
            const string resizeFunction = "function setUnityBackingStoreSize()";
            const string queuedResizeFunction = "function queueUnityBackingStoreSize()";
            const string resizeCall = "setUnityBackingStoreSize();";
            const string resizeListener = "window.addEventListener(\"resize\", queueUnityBackingStoreSize);";
            const string orientationListener = "window.addEventListener(\"orientationchange\", queueUnityBackingStoreSize);";
            const string visualViewportListener = "window.visualViewport.addEventListener(\"resize\", queueUnityBackingStoreSize);";
            const string viewportLockRule =
                "html, body { width: 100%; height: 100%; overflow: hidden; background: #050812; }";
            const string canvasDisplayRule =
                "#unity-canvas { display: block; background: #050812; touch-action: none; }";
            const string footerHeightRule = "#unity-footer { height: 38px; }";
            const string desktopCanvasRule =
                "#unity-canvas.unity-responsive {\n        width: min(1280px, 100vw, calc((100vh - 38px) * 1280 / 853));\n        height: auto;\n        aspect-ratio: 1280 / 853;";
            const string stableViewportCanvasRule =
                "@supports (height: 100svh) {\n        #unity-canvas.unity-responsive {\n          width: min(1280px, 100vw, calc((100svh - 38px) * 1280 / 853));";
            const string fullViewportMediaQuery =
                "@media (max-width: 500px), (max-height: 500px) and (orientation: landscape) {";
            const string fullViewportContainerRule =
                "position: fixed; width: 100vw; height: 100vh; height: 100dvh;";
            const string fullViewportCanvasRule =
                "#unity-canvas.unity-responsive {\n          width: 100%; height: 100%; aspect-ratio: auto;";
            const string hiddenFooterRule = "#unity-footer { display: none; }";
            const string fullViewportMediaRule =
                "@media (max-width: 500px), (max-height: 500px) and (orientation: landscape) {\n" +
                "        #unity-container.unity-desktop {\n" +
                "          left: 0; top: 0; transform: none;\n" +
                "          position: fixed; width: 100vw; height: 100vh; height: 100dvh;\n" +
                "          padding: env(safe-area-inset-top) env(safe-area-inset-right)\n" +
                "                   env(safe-area-inset-bottom) env(safe-area-inset-left);\n" +
                "          box-sizing: border-box; background: #050812;\n" +
                "        }\n" +
                "        #unity-canvas.unity-responsive {\n" +
                "          width: 100%; height: 100%; aspect-ratio: auto;\n" +
                "        }\n" +
                "        #unity-footer { display: none; }\n" +
                "      }";
            const string safeAreaPaddingRule =
                "padding: env(safe-area-inset-top) env(safe-area-inset-right)";
            const string unityMobileContainerRule =
                "#unity-container.unity-mobile {\n" +
                "        position: fixed; width: 100vw; height: 100vh; height: 100dvh;\n" +
                "        padding: env(safe-area-inset-top) env(safe-area-inset-right)\n" +
                "                 env(safe-area-inset-bottom) env(safe-area-inset-left);\n" +
                "        box-sizing: border-box; background: #050812;";
            const string loadingBarHidden = "document.querySelector(\"#unity-loading-bar\").style.display = \"none\";";
            const string startupErrorBanner = "unityShowBanner(message, \"error\");";

            Assert.That(CountActiveOccurrences(html, autoMatchDisabled), Is.EqualTo(1),
                "the postprocessor must add exactly one active auto-match setting beside Unity's commented stock example");
            Assert.That(CountOccurrences(html, devicePixelRatioCap), Is.EqualTo(1),
                "Unity's own rendering DPR must remain capped exactly once");
            Assert.That(CountOccurrences(html, resizeFunction), Is.EqualTo(1),
                "the explicit backing-store helper must be injected only once");
            Assert.That(CountOccurrences(html, queuedResizeFunction), Is.EqualTo(1),
                "the resize queue helper must be injected only once");
            Assert.That(CountOccurrences(html, resizeListener), Is.EqualTo(1),
                "window resize must register one resync listener");
            Assert.That(CountOccurrences(html, orientationListener), Is.EqualTo(1),
                "orientation changes must register one resync listener");
            Assert.That(CountOccurrences(html, visualViewportListener), Is.EqualTo(1),
                "visual viewport layout changes must register one resync listener");
            Assert.That(CountOccurrences(html, resizeCall), Is.EqualTo(2),
                "the backing store must be initialized once before loading and once from the resize queue");
            Assert.That(html, Does.Contain(viewportLockRule),
                "the document must stay viewport-sized and non-scrollable or centering can create unequal outer gutters and body overflow");
            Assert.That(html, Does.Contain(canvasDisplayRule),
                "the canvas must be block-level or its inline baseline gap can push the 38px footer outside the viewport");
            Assert.That(html, Does.Contain(footerHeightRule),
                "the footer height must remain the same 38px reserved by desktop canvas sizing or top and bottom space diverge");
            Assert.That(html, Does.Contain(desktopCanvasRule),
                "desktop sizing must preserve 1280:853 while subtracting the footer before fitting height, preventing bottom clipping and asymmetric vertical space");
            Assert.That(html, Does.Contain(stableViewportCanvasRule),
                "supporting browsers must fit against stable viewport height with the same footer subtraction so browser chrome cannot reintroduce shell overflow");
            Assert.That(html, Does.Contain(fullViewportMediaQuery),
                "short landscape phones must enter the full-viewport layout even when their width exceeds the narrow-phone breakpoint");
            Assert.That(CountOccurrences(html, fullViewportContainerRule), Is.EqualTo(2),
                "both desktop-UA fallback and Unity mobile containers need 100vh then 100dvh sizing or one mobile path will leave gaps or crop the canvas");
            Assert.That(html, Does.Contain(fullViewportCanvasRule),
                "the mobile canvas must consume its fixed viewport container instead of retaining desktop letterboxing");
            Assert.That(html, Does.Contain(hiddenFooterRule),
                "the footer must not consume mobile viewport height or force the full-screen canvas beyond the visible area");
            Assert.That(html, Does.Contain(fullViewportMediaRule),
                "the low-landscape breakpoint must bind fixed viewport sizing, a full-size canvas, and footer removal in one cascade block or declarations can silently target the wrong layout");
            Assert.That(html, Does.Contain(unityMobileContainerRule),
                "Unity's mobile-UA container must own the dynamic viewport sizing or the non-desktop class can still gap or overflow despite a correct desktop fallback");
            Assert.That(CountOccurrences(html, safeAreaPaddingRule), Is.EqualTo(2),
                "both desktop-UA fallback and Unity mobile containers must retain notch insets or one classification path can place gameplay under cutouts");
            var stableViewportRuleOffset = html.IndexOf(stableViewportCanvasRule, StringComparison.Ordinal);
            var fullViewportMediaOffset = html.IndexOf(fullViewportMediaQuery, StringComparison.Ordinal);
            Assert.That(stableViewportRuleOffset, Is.LessThan(fullViewportMediaOffset),
                "the full-viewport media rule must follow the 100svh desktop override or the later desktop width can override mobile 100% sizing and crop short screens");
            Assert.That(CountOccurrences(html, "touch-action: none;"), Is.EqualTo(1),
                "the Unity canvas must disable browser touch gestures exactly once after repeated postprocessing");
            Assert.That(html, Does.Match(@"#unity-canvas\s*\{[^}]*touch-action\s*:\s*none\s*;"),
                "the touch gesture override must be scoped to Unity's canvas across responsive layouts");
            Assert.That(html, Does.Contain("canvas.getBoundingClientRect();"));
            Assert.That(html, Does.Contain("var scale = Math.min(window.devicePixelRatio || 1, 2);"),
                "manual backing-store sizing must cap DPR independently of Unity's config");
            Assert.That(html, Does.Contain("canvas.width = Math.max(1, Math.round(rect.width * scale));"));
            Assert.That(html, Does.Contain("canvas.height = Math.max(1, Math.round(rect.height * scale));"));
            Assert.That(html, Does.Not.Contain("alert(message);"),
                "a loader startup failure must not block behind a browser alert");
            Assert.That(CountOccurrences(html, loadingBarHidden), Is.EqualTo(1),
                "the loading indicator must be hidden once when startup fails");
            Assert.That(CountOccurrences(html, startupErrorBanner), Is.EqualTo(1),
                "the startup error banner must not be duplicated by a repeated postprocess");

            var buildResources = new[]
            {
                "build-webgl.loader.js",
                "build-webgl.data.unityweb",
                "build-webgl.framework.js.unityweb",
                "build-webgl.wasm.unityweb",
            };
            const string cacheVersionPattern = @"\?v=(?<cacheVersion>[A-Za-z0-9][A-Za-z0-9._-]*)(?=[""'])";
            string sharedCacheVersion = null;
            for (var resourceIndex = 0; resourceIndex < buildResources.Length; resourceIndex += 1)
            {
                var resource = buildResources[resourceIndex];
                var cacheBustedUrls = Regex.Matches(html, Regex.Escape(resource) + cacheVersionPattern);
                Assert.That(cacheBustedUrls.Count, Is.EqualTo(1),
                    $"{resource} must be routed through exactly one versioned URL");
                Assert.That(CountOccurrences(html, resource), Is.EqualTo(1),
                    $"{resource} must not retain an unversioned or duplicate URL after repeated postprocessing");

                var cacheVersion = cacheBustedUrls[0].Groups["cacheVersion"].Value;
                if (resourceIndex == 0)
                    sharedCacheVersion = cacheVersion;
                else
                    Assert.That(cacheVersion, Is.EqualTo(sharedCacheVersion),
                        $"{resource} must share one per-build cache version with the other WebGL resources");
            }

            File.WriteAllText(Path.Combine(_outputDirectory, "Build", "build-webgl.wasm.unityweb"),
                "wasm fixture for the next build");
            InvokePolishIndexHtml(_outputDirectory);

            var rebuiltHtml = File.ReadAllText(Path.Combine(_outputDirectory, "index.html"));
            Assert.That(CountActiveOccurrences(rebuiltHtml, autoMatchDisabled), Is.EqualTo(1),
                "rebuilding a resource must preserve the active WebGL auto-match disable");
            Assert.That(CountOccurrences(rebuiltHtml, resizeFunction), Is.EqualTo(1),
                "rebuilding a resource must preserve the backing-store helper");
            Assert.That(CountOccurrences(rebuiltHtml, resizeListener), Is.EqualTo(1),
                "rebuilding a resource must preserve the backing-store resize listener");
            Assert.That(rebuiltHtml, Does.Match(@"#unity-canvas\s*\{[^}]*touch-action\s*:\s*none\s*;"),
                "rebuilding a resource must preserve Unity-canvas touch gesture suppression");
            Assert.That(rebuiltHtml, Does.Not.Contain("alert(message);"),
                "rebuilding a resource must retain non-blocking loader failures");

            string rebuiltSharedCacheVersion = null;
            for (var resourceIndex = 0; resourceIndex < buildResources.Length; resourceIndex += 1)
            {
                var resource = buildResources[resourceIndex];
                var cacheBustedUrls = Regex.Matches(rebuiltHtml, Regex.Escape(resource) + cacheVersionPattern);
                Assert.That(cacheBustedUrls.Count, Is.EqualTo(1),
                    $"{resource} must retain exactly one versioned URL after a newly built resource changes");
                Assert.That(CountOccurrences(rebuiltHtml, resource), Is.EqualTo(1),
                    $"{resource} must not retain an obsolete or duplicate URL after a newly built resource changes");

                var cacheVersion = cacheBustedUrls[0].Groups["cacheVersion"].Value;
                if (resourceIndex == 0)
                    rebuiltSharedCacheVersion = cacheVersion;
                else
                    Assert.That(cacheVersion, Is.EqualTo(rebuiltSharedCacheVersion),
                        $"{resource} must share the rebuilt cache version with every other WebGL resource");
            }

            Assert.That(rebuiltSharedCacheVersion, Is.Not.EqualTo(sharedCacheVersion),
                "changing any WebGL build resource must invalidate all four resource URLs together");

            const string cacheVersionMarkerPrefix = "/* CinderCourt WebGL build cache version: ";
            const string cacheVersionMarkerSuffix = " */";
            const string staleCacheVersion = "stale-build-version";
            var rebuiltMarker = cacheVersionMarkerPrefix + rebuiltSharedCacheVersion + cacheVersionMarkerSuffix;
            Assert.That(CountOccurrences(rebuiltHtml, rebuiltMarker), Is.EqualTo(1),
                "the rebuilt shell must expose one cache version marker for postprocess self-repair");

            var corruptedHtml = rebuiltHtml
                .Replace(rebuiltMarker, cacheVersionMarkerPrefix + staleCacheVersion + cacheVersionMarkerSuffix)
                .Replace("build-webgl.loader.js?v=" + rebuiltSharedCacheVersion,
                    "build-webgl.loader.js?v=" + staleCacheVersion);
            File.WriteAllText(Path.Combine(_outputDirectory, "index.html"), corruptedHtml);
            InvokePolishIndexHtml(_outputDirectory);

            var repairedHtml = File.ReadAllText(Path.Combine(_outputDirectory, "index.html"));
            Assert.That(CountOccurrences(repairedHtml, rebuiltMarker), Is.EqualTo(1),
                "postprocessing must repair the cache marker to the current build version exactly once");
            Assert.That(repairedHtml, Does.Not.Contain(staleCacheVersion),
                "postprocessing must remove stale cache marker and resource tokens");
            for (var resourceIndex = 0; resourceIndex < buildResources.Length; resourceIndex += 1)
            {
                var resource = buildResources[resourceIndex];
                Assert.That(CountOccurrences(repairedHtml, resource), Is.EqualTo(1),
                    $"{resource} must retain exactly one URL after cache-token self-repair");
                Assert.That(CountOccurrences(repairedHtml, resource + "?v=" + rebuiltSharedCacheVersion), Is.EqualTo(1),
                    $"{resource} must be repaired to the current shared build version");
            }

            var functionOffset = html.IndexOf(resizeFunction, StringComparison.Ordinal);
            var queueFunctionOffset = html.IndexOf(queuedResizeFunction, StringComparison.Ordinal);
            var queuedResyncOffset = html.IndexOf(resizeCall, queueFunctionOffset, StringComparison.Ordinal);
            var resizeListenerOffset = html.IndexOf(resizeListener, StringComparison.Ordinal);
            var orientationListenerOffset = html.IndexOf(orientationListener, StringComparison.Ordinal);
            var visualViewportListenerOffset = html.IndexOf(visualViewportListener, StringComparison.Ordinal);
            var initialCallOffset = html.LastIndexOf(resizeCall, StringComparison.Ordinal);
            var loaderOffset = html.IndexOf("createUnityInstance(canvas, config", StringComparison.Ordinal);
            Assert.That(functionOffset, Is.GreaterThan(-1));
            Assert.That(queueFunctionOffset, Is.GreaterThan(functionOffset),
                "the resize queue must be defined after the backing-store helper");
            Assert.That(queuedResyncOffset, Is.GreaterThan(queueFunctionOffset),
                "each resize event route must invoke the backing-store helper");
            Assert.That(queuedResyncOffset, Is.LessThan(resizeListenerOffset),
                "the event listeners must route through the complete resync helper");
            Assert.That(orientationListenerOffset, Is.GreaterThan(resizeListenerOffset));
            Assert.That(visualViewportListenerOffset, Is.GreaterThan(orientationListenerOffset));
            Assert.That(initialCallOffset, Is.GreaterThan(visualViewportListenerOffset),
                "startup sizing must occur after every responsive listener is registered");
            Assert.That(initialCallOffset, Is.LessThan(loaderOffset),
                "the backing store must be initialized before Unity's loader starts");
        }

        private static void InvokePolishIndexHtml(string outputDirectory)
        {
            var buildScriptType = FindBuildScriptType();
            Assert.That(buildScriptType, Is.Not.Null, "the editor build script must be loaded for EditMode tests");

            var method = buildScriptType.GetMethod("PolishIndexHtml", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "the WebGL shell postprocessor must remain callable by the build pipeline");
            method.Invoke(null, new object[] { outputDirectory });
        }

        private static Type FindBuildScriptType()
        {
            const string typeName = "CinderCourt.EditorTools.BuildScript";
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index += 1)
            {
                var type = assemblies[index].GetType(typeName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static int CountOccurrences(string value, string token)
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

        private static int CountActiveOccurrences(string value, string token)
        {
            var activeCount = 0;
            var offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                var lineStart = value.LastIndexOf('\n', offset);
                var firstNonWhitespace = lineStart + 1;
                while (firstNonWhitespace < offset && char.IsWhiteSpace(value[firstNonWhitespace]))
                    firstNonWhitespace += 1;

                if (firstNonWhitespace + 1 >= offset ||
                    value[firstNonWhitespace] != '/' ||
                    value[firstNonWhitespace + 1] != '/')
                    activeCount += 1;

                offset += token.Length;
            }

            return activeCount;
        }

        private const string StockTemplateHtml = @"<!DOCTYPE html>
<html lang=""en-us""><head>
<title>Unity Web Player | Cinder Court</title>
<link rel=""shortcut icon"" href=""TemplateData/favicon.ico"">
<link rel=""stylesheet"" href=""TemplateData/style.css"">
</head><body>
<canvas id=""unity-canvas""></canvas>
<div id=""unity-loading-bar""></div>
<script>
var canvas = document.querySelector(""#unity-canvas"");
var buildUrl = ""Build"";
var loaderUrl = buildUrl + ""/build-webgl.loader.js"";
var config = {
    dataUrl: buildUrl + ""/build-webgl.data.unityweb"",
    frameworkUrl: buildUrl + ""/build-webgl.framework.js.unityweb"",
    codeUrl: buildUrl + ""/build-webgl.wasm.unityweb"",
    showBanner: unityShowBanner,
};
// By default, Unity keeps WebGL canvas render target size matched with
// the DOM size of the canvas element (scaled by window.devicePixelRatio)
// config.matchWebGLToCanvasSize = false;
if (false) {
    canvas.style.width = ""1280px"";
    canvas.style.height = ""853px"";
}
document.querySelector(""#unity-loading-bar"").style.display = ""block"";
createUnityInstance(canvas, config, () => {})
    .then((unityInstance) => {
        unityInstance.SetFullscreen(1);
    }).catch((message) => {
        alert(message);
    });
</script></body></html>";
    }
}
