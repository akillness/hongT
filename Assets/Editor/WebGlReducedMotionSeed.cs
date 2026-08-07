// Pure string transform: mirrors the OS "prefers-reduced-motion" media query
// into localStorage from the WebGL shell, BEFORE the Unity loader boots
// (integrated-combat-vfx-spec §2.4 자동 감지). Deliberately free of
// UnityEditor/UnityEngine references so the EditMode test — and a plain
// dotnet scratch run — can exercise the transform directly.
//
// Contract with the View layer (ViewPrefs.ReducedMotion):
//  - the shell rewrites "al:os-reduced-motion" on EVERY load, so the hint
//    tracks the live OS setting;
//  - ViewPrefs consults the hint only when the player never made an explicit
//    lobby choice (no "al:reduced-motion" PlayerPrefs key). An explicit
//    toggle always wins over the OS hint.
using System.IO;

namespace CinderCourt.EditorTools
{
    public static class WebGlReducedMotionSeed
    {
        /// <summary>localStorage key the shell writes and ViewPrefs reads
        /// (via WebGLStorage/storage.jslib). Must stay in sync with
        /// ViewPrefs.OsHintKey.</summary>
        public const string StorageKey = "al:os-reduced-motion";

        public const string MediaQueryProbe =
            "window.matchMedia(\"(prefers-reduced-motion: reduce)\").matches";

        /// <summary>First script statement of the stock template — present in
        /// every Unity WebGL shell revision this project has built.</summary>
        const string CanvasAnchor = "var canvas = document.querySelector(\"#unity-canvas\");";

        public const string Fragment =
            "/* CinderCourt OS reduced-motion hint (integrated-combat-vfx-spec §2.4):\n" +
            "         mirrored into localStorage on every load, before the Unity loader,\n" +
            "         so ViewPrefs can seed its default when the player never chose.\n" +
            "         An explicit lobby toggle (PlayerPrefs key) always wins. */\n" +
            "      try {\n" +
            "        window.localStorage.setItem(\"" + StorageKey + "\",\n" +
            "          " + MediaQueryProbe + " ? \"1\" : \"0\");\n" +
            "      } catch (e) { /* storage unavailable: C# default (off) applies */ }";

        /// <summary>Injects the mirror fragment immediately after the canvas
        /// lookup. Idempotent: a shell that already carries the storage key is
        /// returned unchanged, so repeated postprocessing never duplicates it.</summary>
        public static string Inject(string html)
        {
            if (html.Contains(StorageKey))
                return html;
            if (!html.Contains(CanvasAnchor))
                throw new InvalidDataException(
                    "WebGL index does not declare its canvas; cannot seed the reduced-motion hint");

            return html.Replace(CanvasAnchor, CanvasAnchor + "\n\n      " + Fragment);
        }
    }
}
