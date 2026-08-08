// Browser-IME composition state for the companion command console.
// Pure C# (no UnityEngine) so the composition rules are testable without a
// scene, a browser, or a WebGL build.
//
// WHY THIS EXISTS — emscripten's keyboard path only wires
// keydown/keypress/keyup, so a browser IME (Hangul, Kana, Pinyin) never
// reaches the Unity canvas: the *pre-edit* syllable a Korean typist watches
// while composing ("ㄱ" -> "가" -> "각") has no delivery route at all.
// WebGLHangulIme puts a real, focusable, off-screen <input> in front of the
// canvas and forwards the DOM composition events; this class turns that event
// stream into console text.
//
// The contract that matters: a composition is a *replaceable tail*, never an
// append. Every compositionupdate replaces the whole pre-edit — appending
// instead is exactly the duplication bug the console already paid for once
// (see _workspace/current/qa/command-console-hangul-duplication.md: "한" ->
// "한한"). Only compositionend moves characters into the committed buffer.
namespace CinderCourt.View
{
    /// <summary>Composition tail in front of a <see cref="CommandConsoleBuffer"/>.
    /// The buffer holds committed text only; <see cref="Text"/> is what the
    /// console field displays (committed + the live pre-edit).</summary>
    public sealed class CommandConsoleImeComposition
    {
        readonly CommandConsoleBuffer _committed;
        readonly int _characterLimit;

        string _composition = string.Empty;
        bool _composing;

        /// <param name="committed">Existing console buffer — kept as the single
        /// owner of committed text so the non-IME keystroke path and this one
        /// can never disagree.</param>
        /// <param name="characterLimit">Cap on committed + pre-edit together,
        /// mirrors InputField.characterLimit.</param>
        public CommandConsoleImeComposition(CommandConsoleBuffer committed, int characterLimit)
        {
            _committed = committed;
            _characterLimit = characterLimit > 0 ? characterLimit : int.MaxValue;
        }

        /// <summary>True between compositionstart and compositionend.</summary>
        public bool IsComposing => _composing;

        /// <summary>The live pre-edit, "" when not composing.</summary>
        public string Composition => _composition;

        /// <summary>Committed text only — what a submit would send.</summary>
        public string CommittedText => _committed.Text;

        /// <summary>What the console field shows: committed text plus the
        /// in-progress syllable.</summary>
        public string Text => _composition.Length == 0 ? _committed.Text : _committed.Text + _composition;

        public int Length => _committed.Length + _composition.Length;

        /// <summary>Drops the pre-edit and the committed text (console session
        /// boundary). The caller clears the buffer too when it owns it.</summary>
        public void Clear()
        {
            _composition = string.Empty;
            _composing = false;
        }

        /// <summary>compositionstart — the IME opened a new syllable.</summary>
        public bool BeginComposition()
        {
            var changed = _composition.Length > 0;
            _composition = string.Empty;
            _composing = true;
            return changed;
        }

        /// <summary>compositionupdate — REPLACES the pre-edit with the IME's
        /// current guess. "ㄱ" -> "가" -> "각" is three replacements, not three
        /// appends.</summary>
        public bool UpdateComposition(string preedit)
        {
            _composing = true;
            var next = Sanitize(preedit, RoomForComposition());
            if (next == _composition) return false;
            _composition = next;
            return true;
        }

        /// <summary>compositionend — the IME committed <paramref name="final"/>
        /// (empty when the composition was cancelled). The pre-edit is gone
        /// either way.</summary>
        public bool EndComposition(string final, int frame)
        {
            var hadComposition = _composition.Length > 0;
            _composition = string.Empty;
            _composing = false;
            // The commit is ONE event from ONE source, so it goes in whole:
            // routing it through Feed would let the buffer's same-frame echo
            // guard eat the second half of a real "ㅋㅋ" commit.
            var appended = _committed.AppendComposed(Sanitize(final, RoomForCommit()));
            return appended || hadComposition;
        }

        /// <summary>Composition abandoned without a commit (ESC). The committed
        /// text is untouched — the console stays open.</summary>
        public bool CancelComposition()
        {
            var changed = _composition.Length > 0 || _composing;
            _composition = string.Empty;
            _composing = false;
            return changed;
        }

        /// <summary>A non-IME insertion: direct ASCII typing or a paste. Any
        /// live pre-edit commits first — that is what the browser does when
        /// something else writes into the field.</summary>
        public bool Insert(string text, int frame)
        {
            var changed = false;
            if (_composing || _composition.Length > 0) changed = EndComposition(_composition, frame);
            return _committed.AppendComposed(Sanitize(text, RoomForCommit())) || changed;
        }

        /// <summary>Backspace. While composing it eats the pre-edit only —
        /// a mistyped syllable must never chew into text the player already
        /// committed. (Real IMEs handle this themselves and send a shorter
        /// compositionupdate; this path is the fallback for the browsers that
        /// deliver the key instead.)</summary>
        public bool DeleteBackward(int frame)
        {
            if (_composition.Length > 0)
            {
                _composition = _composition.Substring(0, _composition.Length - 1);
                return true;
            }
            if (_composing)
            {
                // Composing with an empty pre-edit: the key belongs to the IME,
                // not to the committed text.
                return false;
            }
            return _committed.Feed(CommandConsoleBuffer.Backspace, frame);
        }

        /// <summary>Commits whatever pre-edit is live and returns the committed
        /// text. Used when the console closes: a syllable the player finished
        /// typing should not vanish because they hit Enter one beat early.</summary>
        public string Flush(int frame)
        {
            if (_composing || _composition.Length > 0) EndComposition(_composition, frame);
            return _committed.Text;
        }

        int RoomForCommit() => _characterLimit - _committed.Length;

        // The pre-edit sits in front of the committed text, so it competes for
        // the same 60 characters.
        int RoomForComposition() => _characterLimit - _committed.Length;

        /// <summary>Control characters never reach the console (the IME can
        /// deliver a stray \n on some Windows builds), and nothing longer than
        /// the remaining room is kept.</summary>
        static string Sanitize(string value, int room)
        {
            if (string.IsNullOrEmpty(value) || room <= 0) return string.Empty;
            var builder = new System.Text.StringBuilder(value.Length);
            for (var i = 0; i < value.Length && builder.Length < room; i++)
            {
                var c = value[i];
                if (char.IsControl(c)) continue;
                builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
