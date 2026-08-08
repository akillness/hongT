// integrated-combat-vfx-spec §2.4: the WebGL shell must mirror the OS
// "prefers-reduced-motion" media query into localStorage BEFORE the Unity
// loader boots, so ViewPrefs can seed its default for players who never
// touched the lobby toggle. Pure string-transform tests (no UnityEditor/
// UnityEngine): the transform lives in WebGlReducedMotionSeed, resolved by
// reflection like BuildScriptWebGlPostprocessTests resolves BuildScript.
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class WebGlReducedMotionSeedTests
    {
        private const string StorageKey = "al:os-reduced-motion";
        private const string MediaQueryProbe =
            "window.matchMedia(\"(prefers-reduced-motion: reduce)\").matches";
        private const string CanvasAnchor =
            "var canvas = document.querySelector(\"#unity-canvas\");";

        [Test]
        public void Inject_MirrorsOsHintAfterCanvasLookup_BeforeLoaderConfig()
        {
            var html = Inject(StockTemplateHtml);

            Assert.That(CountOccurrences(html, StorageKey), Is.EqualTo(1),
                "the shell must write exactly one OS reduced-motion hint");
            Assert.That(html, Does.Contain(MediaQueryProbe),
                "the hint must come from the prefers-reduced-motion media query (spec §2.4)");
            Assert.That(html, Does.Contain("window.localStorage.setItem(\"" + StorageKey + "\","),
                "the hint must land in localStorage — the store storage.jslib/ViewPrefs read");
            Assert.That(html, Does.Contain("? \"1\" : \"0\""),
                "the hint must serialize to the \"1\"/\"0\" strings ViewPrefs compares against");
            Assert.That(html, Does.Contain("try {").And.Contain("} catch (e)"),
                "storage-less contexts (privacy mode) must not break the boot script");

            var anchorOffset = html.IndexOf(CanvasAnchor, StringComparison.Ordinal);
            var hintOffset = html.IndexOf(StorageKey, StringComparison.Ordinal);
            var configOffset = html.IndexOf("var config = {", StringComparison.Ordinal);
            var loaderOffset = html.IndexOf("createUnityInstance(", StringComparison.Ordinal);
            Assert.That(anchorOffset, Is.GreaterThan(-1), "fixture must keep the stock canvas lookup");
            Assert.That(hintOffset, Is.GreaterThan(anchorOffset),
                "the hint must run after the canvas lookup it is anchored to");
            Assert.That(hintOffset, Is.LessThan(configOffset),
                "the hint must be written before the Unity loader config is even assembled");
            Assert.That(hintOffset, Is.LessThan(loaderOffset),
                "the hint must be written before the Unity loader boots (pre-ViewPrefs read)");
        }

        [Test]
        public void Inject_IsIdempotent_AcrossRepeatedPostprocessing()
        {
            var once = Inject(StockTemplateHtml);
            var twice = Inject(once);

            Assert.That(twice, Is.EqualTo(once),
                "re-running the postprocess must not duplicate or move the hint fragment");
            Assert.That(CountOccurrences(twice, StorageKey), Is.EqualTo(1));
            Assert.That(CountOccurrences(twice, "prefers-reduced-motion"), Is.EqualTo(1));
        }

        [Test]
        public void Inject_WithoutCanvasLookup_FailsTheBuildLoudly()
        {
            var exception = Assert.Throws<TargetInvocationException>(
                () => Inject("<html><body><script>var x = 1;</script></body></html>"));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>(),
                "a template without the canvas anchor must fail the build, not skip the hint");
        }

        [Test]
        public void StorageKey_MatchesTheViewPrefsContract()
        {
            // ViewPrefs.OsHintKey reads this exact literal through
            // WebGLStorage/storage.jslib; a drift here silently kills the
            // whole auto-detection path, so pin both constants.
            var type = FindSeedType();
            Assert.That(type, Is.Not.Null, "the reduced-motion seed transform must exist");
            Assert.That(type.GetField("StorageKey").GetValue(null), Is.EqualTo(StorageKey));
            Assert.That(type.GetField("MediaQueryProbe").GetValue(null), Is.EqualTo(MediaQueryProbe));
        }

        private static string Inject(string html)
        {
            var type = FindSeedType();
            Assert.That(type, Is.Not.Null,
                "the editor assembly must expose CinderCourt.EditorTools.WebGlReducedMotionSeed");
            var method = type.GetMethod("Inject", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null,
                "the reduced-motion transform must remain callable by the build pipeline");
            return (string)method.Invoke(null, new object[] { html });
        }

        private static Type FindSeedType()
        {
            const string typeName = "CinderCourt.EditorTools.WebGlReducedMotionSeed";
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

        private const string StockTemplateHtml = @"<!DOCTYPE html>
<html lang=""en-us""><head>
<title>Cinder Court</title>
</head><body>
<canvas id=""unity-canvas""></canvas>
<div id=""unity-loading-bar""></div>
<script>
      var canvas = document.querySelector(""#unity-canvas"");
      var buildUrl = ""Build"";
      var loaderUrl = buildUrl + ""/build-webgl.loader.js"";
      var config = {
        dataUrl: buildUrl + ""/build-webgl.data.unityweb"",
        showBanner: unityShowBanner,
      };
      document.querySelector(""#unity-loading-bar"").style.display = ""block"";
      createUnityInstance(canvas, config, () => {});
</script></body></html>";
    }
}
