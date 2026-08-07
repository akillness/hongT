// Player-facing presentation preferences (PlayerPrefs-backed, View-only).
// Single read surface so juice systems (GameView hit-stop, CameraRig shake,
// HudView flashes) can gate intensity without cross-file coupling.
using UnityEngine;

namespace CinderCourt.View
{
    public static class ViewPrefs
    {
        const string ReducedMotionKey = "al:reduced-motion";

        // OS hint mirrored by the WebGL shell (BuildScript/WebGlReducedMotionSeed
        // injects a matchMedia("(prefers-reduced-motion: reduce)") probe into
        // index.html that rewrites this localStorage key on EVERY page load).
        // Read through WebGLStorage: jslib/localStorage on WebGL players,
        // PlayerPrefs string fallback in the editor — EditMode behavior only
        // changes when a test plants the hint explicitly.
        const string OsHintKey = "al:os-reduced-motion";

        static int _reducedMotion = -1;   // -1 unread, 0 off, 1 on

        /// <summary>모션 약함: halves shake/flash, disables hit-stop/slow-mo.
        /// Default seeding (integrated-combat-vfx-spec §2.4): while the player
        /// has never touched the lobby toggle (no PlayerPrefs key), the OS
        /// reduced-motion hint decides. HasKey is the discriminator on purpose:
        /// GetInt(key, 0) cannot tell "no choice" from an explicit OFF, and an
        /// explicit choice — including OFF — must always beat the OS hint.</summary>
        public static bool ReducedMotion
        {
            get
            {
                if (_reducedMotion < 0)
                    _reducedMotion = PlayerPrefs.HasKey(ReducedMotionKey)
                        ? PlayerPrefs.GetInt(ReducedMotionKey, 0)
                        : (WebGLStorage.GetString(OsHintKey) == "1" ? 1 : 0);
                return _reducedMotion == 1;
            }
            set
            {
                _reducedMotion = value ? 1 : 0;
                PlayerPrefs.SetInt(ReducedMotionKey, _reducedMotion);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Drops the cached value so the next read re-runs the
        /// HasKey/OS-hint seeding. Test-only (InternalsVisibleTo EditMode):
        /// the boot path reads once per session, exactly like before.</summary>
        internal static void ResetCacheForTests() => _reducedMotion = -1;

        /// <summary>Multiplier for shake amplitude / flash alpha under 모션 약함.</summary>
        public static float MotionScale => ReducedMotion ? 0.4f : 1f;

        /// <summary>Hit-stop and slow-mo are binary — off entirely when reduced.</summary>
        public static bool TimeEffectsAllowed => !ReducedMotion;
    }
}
