// Deterministic text buffer behind the companion command console.
// Pure C# (no UnityEngine) so the editing rules are testable without a scene.
//
// WHY THIS EXISTS — the console used to duplicate every Hangul syllable.
// Two independent writers could reach the same uGUI InputField:
//   1. the field's own IMGUI path (InputField.OnUpdateSelected -> KeyPressed ->
//      Append), which on macOS/Windows still receives the IME-committed
//      character, and
//   2. HudView mirroring UnityEngine.InputSystem.Keyboard.onTextInput into
//      InputField.text by hand (needed because activeInputHandler:1 kills the
//      legacy Input.inputString stream this project's uGUI field would read).
// HudView now makes the field readOnly so writer (1) is structurally dead, and
// routes every keystroke through this buffer, which additionally drops a
// character that is delivered twice inside the SAME frame — the signature of a
// duplicated event source. Two identical characters from a human (or from OS
// key repeat, <=33 Hz) always land in different frames, so real "ㅋㅋ"/"ll"
// input is never suppressed.
namespace CinderCourt.View
{
    /// <summary>Single writer for the command console text. Frame numbers are
    /// injected (Time.frameCount at the callsite) so tests stay deterministic.</summary>
    public sealed class CommandConsoleBuffer
    {
        public const char Backspace = '\b';
        public const char Delete = (char)127;
        const char Escape = (char)27;

        readonly int _characterLimit;
        readonly System.Text.StringBuilder _text = new System.Text.StringBuilder();

        bool _hasAccepted;
        int _lastAcceptedFrame;
        char _lastAcceptedChar;

        /// <param name="characterLimit">Hard cap, mirrors InputField.characterLimit.</param>
        public CommandConsoleBuffer(int characterLimit)
        {
            _characterLimit = characterLimit > 0 ? characterLimit : int.MaxValue;
        }

        public string Text => _text.ToString();
        public int Length => _text.Length;

        public void Clear()
        {
            _text.Length = 0;
            _hasAccepted = false;
            _lastAcceptedFrame = 0;
            _lastAcceptedChar = '\0';
        }

        /// <summary>Feeds one character from the text-input stream.
        /// Returns true when the buffer changed.</summary>
        public bool Feed(char c, int frame)
        {
            // Enter/ESC/Tab drive the console itself (submit, cancel) and are
            // handled by HudView against the Keyboard device — never text.
            if (c == '\n' || c == '\r' || c == '\t' || c == Escape) return false;

            if (c == Backspace || c == Delete)
            {
                if (_text.Length == 0) return false;
                _text.Length -= 1;
                // A deletion breaks the duplicate window: the next identical
                // character is a genuine retype, not an echo of this one.
                _hasAccepted = false;
                return true;
            }

            if (char.IsControl(c)) return false;
            if (_text.Length >= _characterLimit) return false;
            if (IsSameFrameEcho(c, frame)) return false;

            _text.Append(c);
            _hasAccepted = true;
            _lastAcceptedFrame = frame;
            _lastAcceptedChar = c;
            return true;
        }

        bool IsSameFrameEcho(char c, int frame)
            => _hasAccepted && _lastAcceptedChar == c && _lastAcceptedFrame == frame;
    }
}
