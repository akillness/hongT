// W11 — browser IME bridge for the companion command console.
//
// WHY: emscripten's keyboard path registers keydown/keypress/keyup only. A
// browser IME (Hangul 2-beolsik, Kana, Pinyin) composes into a focused
// *editable element*; a WebGL canvas is not one, so on the deployed build the
// pre-edit syllable never appears and the composed text never arrives. The
// only surface a browser will run an IME against is a real <input>, so this
// creates one off-screen, focuses it while the console is open, and forwards
// the DOM composition events into Unity.
//
// The Unity side sets WebGLInput.captureAllKeyboardInput = false before
// opening, so Unity's own handler ignores keys while the hidden input holds
// focus. That is what keeps this from becoming a second writer — Enter and
// Escape therefore have to come back through this bridge too (kinds 5/6).
//
// Event kinds must match CinderCourt.View.ConsoleImeEvent:
//   0 compositionstart  1 compositionupdate  2 compositionend
//   3 insert            4 delete-backward
//   5 submit (Enter)    6 cancel (Escape)
mergeInto(LibraryManager.library, {
  $CinderIme: {
    el: null,
    cb: 0,
    open: false,
    composing: false,

    send: function (kind, text) {
      var cb = CinderIme.cb;
      if (!cb) return;
      var value = text || "";
      var size = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(size);
      stringToUTF8(value, buffer, size);
      try {
        {{{ makeDynCall('vii', 'cb') }}}(kind, buffer);
      } finally {
        _free(buffer);
      }
    },

    ensure: function () {
      if (CinderIme.el) return CinderIme.el;
      var el = document.createElement("input");
      el.type = "text";
      el.id = "cinder-ime-input";
      el.autocomplete = "off";
      el.autocapitalize = "off";
      el.spellcheck = false;
      el.setAttribute("autocorrect", "off");
      el.setAttribute("aria-hidden", "true");
      el.setAttribute("tabindex", "-1");

      // Off-screen but REAL. display:none, visibility:hidden and zero size all
      // make the element unfocusable, which would defeat the entire bridge;
      // a transparent 1px box is focusable and invisible. font-size 16px stops
      // iOS Safari from zooming the page when the field takes focus.
      var s = el.style;
      s.position = "fixed";
      s.left = "0px";
      s.bottom = "0px";
      s.width = "1px";
      s.height = "1px";
      s.padding = "0";
      s.border = "0";
      s.outline = "none";
      s.opacity = "0";
      s.zIndex = "-1";
      s.fontSize = "16px";
      s.background = "transparent";
      s.color = "transparent";
      s.caretColor = "transparent";
      s.pointerEvents = "none";   // never steal a tap meant for the canvas

      el.addEventListener("compositionstart", function () {
        CinderIme.composing = true;
        CinderIme.send(0, "");
      });

      el.addEventListener("compositionupdate", function (e) {
        CinderIme.composing = true;
        CinderIme.send(1, e.data || "");
      });

      el.addEventListener("compositionend", function (e) {
        CinderIme.composing = false;
        CinderIme.send(2, e.data || "");
        el.value = "";
      });

      el.addEventListener("input", function (e) {
        // Mid-composition input events carry the pre-edit, which
        // compositionupdate already delivered. "insertCompositionText" is the
        // commit's own input event — Chrome fires it before compositionend,
        // Firefox after — and taking it too would insert the syllable twice.
        if (e.isComposing || CinderIme.composing) { return; }
        if (e.inputType === "insertCompositionText") { el.value = ""; return; }
        // Deletions are driven off keydown (see below): the field is kept
        // empty, so the browser has nothing to delete and reports nothing.
        if (e.inputType && e.inputType.indexOf("delete") === 0) { el.value = ""; return; }
        var data = e.data;
        if (data === null || data === undefined) data = el.value;
        el.value = "";
        if (data) CinderIme.send(3, data);
      });

      el.addEventListener("keydown", function (e) {
        // keyCode 229 is the universal "this key belongs to the IME" marker;
        // older Safari does not set isComposing on the keydown that opens a
        // composition. Enter/Escape during composition must stay with the IME
        // (they commit / cancel the syllable) — swallowing them here would
        // submit the console mid-word.
        if (e.isComposing || e.keyCode === 229 || CinderIme.composing) return;
        if (e.key === "Enter") { e.preventDefault(); CinderIme.send(5, ""); return; }
        if (e.key === "Escape" || e.key === "Esc") { e.preventDefault(); CinderIme.send(6, ""); return; }
        if (e.key === "Backspace") {
          // The console buffer owns the text and the field is kept empty
          // between commits, so the delete has to be driven from the key.
          e.preventDefault();
          CinderIme.send(4, "");
        }
      });

      el.addEventListener("blur", function () {
        // Clicking the canvas moves focus away while the console is still
        // open; without this the next keystroke goes nowhere (Unity is not
        // capturing either). Re-take it on the next task so a real close,
        // which clears `open` first, is not fought over.
        if (!CinderIme.open) return;
        setTimeout(function () {
          if (CinderIme.open && CinderIme.el) {
            try { CinderIme.el.focus({ preventScroll: true }); } catch (err) { CinderIme.el.focus(); }
          }
        }, 0);
      });

      document.body.appendChild(el);
      CinderIme.el = el;
      return el;
    }
  },

  CinderImeOpen__deps: ['$CinderIme', 'malloc', 'free'],
  CinderImeOpen: function (callback) {
    try {
      CinderIme.cb = callback;
      CinderIme.open = true;
      CinderIme.composing = false;
      var el = CinderIme.ensure();
      el.value = "";
      try { el.focus({ preventScroll: true }); } catch (err) { el.focus(); }
      return 1;
    } catch (e) {
      // No DOM, sandboxed document, CSP — fall back to Unity's own keyboard
      // path rather than leaving the console dead.
      CinderIme.open = false;
      CinderIme.cb = 0;
      return 0;
    }
  },

  CinderImeClose__deps: ['$CinderIme'],
  CinderImeClose: function () {
    try {
      CinderIme.open = false;
      CinderIme.composing = false;
      CinderIme.cb = 0;
      if (CinderIme.el) {
        CinderIme.el.value = "";
        CinderIme.el.blur();
      }
      // Hand focus back so Unity sees the keyboard again (Enter reopens the
      // console) even before captureAllKeyboardInput is restored.
      var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");
      if (canvas && canvas.focus) canvas.focus();
    } catch (e) { /* nothing to unwind */ }
  }
});
