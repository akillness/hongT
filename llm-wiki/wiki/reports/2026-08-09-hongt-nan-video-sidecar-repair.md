# HongT NAN video sidecar repair (2026-08-09)

- tags: hongt, unity, nan2026, video, compresso, playwriter, evidence
- status: durable report

## Summary

`[OBSERVED]` `docs/nan2026/assets/video/` had no current MP4 deliverables while
NAN 2026 markdown still described cycle-13 video artifacts. The repair restored
technical video files from `_workspace/archive/20260808-achilles-quality/submission/video/`,
recompressed them with the offline FFmpeg/CompressO route, regenerated sidecar
JSON from `ffprobe` + SHA-256, and synchronized the markdown/PDF claims.

## Current repaired artifacts

- `[OBSERVED]` `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-final.mp4`:
  H.264, 1440x900, 30 fps, 54.966667 s, 4,325,784 bytes,
  SHA-256 `75b441bd1d52d6377ef1e8f26e5b43a6a48d9bd510d93a024e543215d6dbc696`.
- `[OBSERVED]` `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-playwriter-compresso.mp4`:
  H.264, 1920x1200, 30 fps, 37.066667 s; AAC-LC 48 kHz stereo,
  37.005167 s; 3,655,623 bytes,
  SHA-256 `904c35b119f6af772383ca75dc195da447f6b92de85903d4d0d3a75bcb5bd07a`.
- `[OBSERVED]` Both sidecars set `submissionUse:false`; human YouTube upload and
  NAN form submission remain human-only blockers.

## Verification

- `[OBSERVED]` `node tools/docs/build-nan2026-pdf.mjs` succeeded after markdown
  synchronization and produced PDFs: 41,292; 110,943; 147,619; 96,096 bytes.
- `[OBSERVED]` Local markdown link audit over six `docs/nan2026/*.md` files found
  23 local links and 0 missing targets.
- `[OBSERVED]` Stale video values (`55.00`, old SHA prefixes `d991`/`8928`,
  old audio levels) were removed from `docs/nan2026/` and the current production
  verification notes.
- `[OBSERVED]` A fresh Unity EditMode attempt was blocked by another Unity
  instance holding the project lock and produced no result XML. Retained latest
  successful XML remains `_workspace/current/engineering/unity-logs/test-results-013939.xml`
  with 812/812 passed, but this repair did not produce a fresh Unity pass.

## Reusable rule

`[TARGET]` Treat video MP4, sidecar JSON, markdown, PDF, and gate notes as one
evidence set. If any member changes, re-run `ffprobe`/SHA and search for stale
duration, hash, audio, and contact-sheet claims before regenerating PDFs.
