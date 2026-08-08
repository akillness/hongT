// W11 — managed side of the browser IME bridge (Assets/Plugins/WebGL/hangul_ime.jslib).
//
// Outside a WebGL player this class is inert: Open returns false and the
// console keeps its existing Keyboard.onTextInput path untouched. That is not
// a convenience — the editor has no DOM, and EditMode tests must exercise the
// same console code without a browser.
using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CinderCourt.View
{
    /// <summary>DOM composition events, as the console consumes them.
    /// Values are the wire contract with hangul_ime.jslib — do not renumber.</summary>
    public enum ConsoleImeEvent
    {
        CompositionStart = 0,
        CompositionUpdate = 1,
        CompositionEnd = 2,
        Insert = 3,
        DeleteBackward = 4,
        Submit = 5,
        Cancel = 6,
    }

    public static class WebGLHangulIme
    {
        /// <summary>True while the hidden browser input owns the keyboard.
        /// When this is true the caller must NOT also subscribe
        /// Keyboard.onTextInput: one writer, always.</summary>
        public static bool Active { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        static Action<ConsoleImeEvent, string> _sink;

        delegate void ImeEventCallback(int kind, IntPtr payload);

        [DllImport("__Internal")] static extern int CinderImeOpen(ImeEventCallback callback);
        [DllImport("__Internal")] static extern void CinderImeClose();

        // Rooted for the lifetime of the app: emscripten keeps the raw function
        // pointer, so a collected delegate would be a dangling call.
        static readonly ImeEventCallback Callback = Dispatch;

        [AOT.MonoPInvokeCallback(typeof(ImeEventCallback))]
        static void Dispatch(int kind, IntPtr payload)
        {
            // Reverse P/Invoke: an exception escaping here tears down the
            // runtime, and a dead console is better than a dead game.
            try
            {
                var sink = _sink;
                if (sink == null) return;
                var text = payload == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringUTF8(payload) ?? string.Empty);
                sink((ConsoleImeEvent)kind, text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebGLHangulIme] event {kind} failed: {e.Message}");
            }
        }
#endif

        /// <summary>Focuses the hidden browser input and routes its composition
        /// events to <paramref name="sink"/>. Returns false outside WebGL and
        /// whenever the bridge could not install, so the caller can keep the
        /// Unity keyboard path it already has.</summary>
        public static bool Open(Action<ConsoleImeEvent, string> sink)
        {
            if (sink == null) return false;
#if UNITY_WEBGL && !UNITY_EDITOR
            _sink = sink;
            // Unity captures the keyboard regardless of DOM focus by default,
            // which would deliver every keystroke to BOTH the hidden input and
            // Keyboard.onTextInput. Handing focus arbitration back to the
            // browser is what makes the hidden input the single source.
            WebGLInput.captureAllKeyboardInput = false;
            var opened = CinderImeOpen(Callback) != 0;
            if (!opened)
            {
                WebGLInput.captureAllKeyboardInput = true;
                _sink = null;
            }
            Active = opened;
            return opened;
#else
            return false;
#endif
        }

        /// <summary>Blurs the hidden input and gives the keyboard back to Unity.
        /// Safe to call when never opened.</summary>
        public static void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Active) CinderImeClose();
            WebGLInput.captureAllKeyboardInput = true;
            _sink = null;
#endif
            Active = false;
        }
    }
}
