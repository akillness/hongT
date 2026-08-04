# AGENTS.md

The operating contract for this repository lives in **[CLAUDE.md](CLAUDE.md)**.

This file exists so non-Claude runtimes (Codex, Gemini, OpenCode, gjc, jeo)
resolve the same rules instead of a second, drifting copy. Read `CLAUDE.md` and
follow it verbatim; do not duplicate its rules here.

Quick facts for delegated agents:

- Engine: Unity 6000.5.6f1 + URP, target WebGL, deploy
  <https://akillness.github.io/hongT> (relative URLs only).
- `Assets/Scripts/Sim/` = pure C# deterministic simulation
  (no `UnityEngine` usage). `Assets/Scripts/View/` = presentation, reads sim
  state, never writes it.
- Files marked `// FROZEN CONTRACT` may not be edited by delegated agents.
- Stage only your own files with explicit pathspecs; never `git add -A`.
