// Player-facing presentation preferences (PlayerPrefs-backed, View-only).
// Single read surface so juice systems (GameView hit-stop, CameraRig shake,
// HudView flashes) can gate intensity without cross-file coupling.
using UnityEngine;

namespace CinderCourt.View
{
    public static class ViewPrefs
    {
        const string ReducedMotionKey = "al:reduced-motion";

        static int _reducedMotion = -1;   // -1 unread, 0 off, 1 on

        /// <summary>모션 약함: halves shake/flash, disables hit-stop/slow-mo.</summary>
        public static bool ReducedMotion
        {
            get
            {
                if (_reducedMotion < 0)
                    _reducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, 0);
                return _reducedMotion == 1;
            }
            set
            {
                _reducedMotion = value ? 1 : 0;
                PlayerPrefs.SetInt(ReducedMotionKey, _reducedMotion);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Multiplier for shake amplitude / flash alpha under 모션 약함.</summary>
        public static float MotionScale => ReducedMotion ? 0.4f : 1f;

        /// <summary>Hit-stop and slow-mo are binary — off entirely when reduced.</summary>
        public static bool TimeEffectsAllowed => !ReducedMotion;
    }
}
