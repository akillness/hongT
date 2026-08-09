# NAN 2026 document verification — cycle 13 candidate draft

- run-id: `20260808-achilles-quality`
- public beat: **NAN 2026 final submission**
- recorded: `2026-08-08`
- status: `[OBSERVED]` Markdown/PDF synchronization complete; public YouTube upload and application submission remain human-only.

## Candidate claims reconciled

| Claim | Candidate value | Source of truth |
|---|---:|---|
| Logical campaign stages | 9 | `Assets/Scripts/View/StageCatalog.cs` `AllEntries` and derived `ValidClearMask` |
| EditMode tests | 812/812, failures 0 | `_workspace/current/engineering/unity-logs/test-results-013939.xml` |
| Current play-video artifacts | Deterministic H.264 `1440×900`, 30 fps, 54.9667 s; Playwriter+CompressO-route H.264 `1920×1200`, 30 fps + AAC-LC 48 kHz stereo, 37.0667 s video / 37.0052 s audio | `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-final.mp4.json`; `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-playwriter-compresso.mp4.json`; `_workspace/current/engineering/tech-verification/video-candidate.md` |
| Public play URL | `https://akillness.github.io/hongT/` | deployed Playwriter smoke session; not yet a cycle-13 candidate recertification |

## Markdown audit

Updated `00-submission-guide.md`, `README.md`, `01-game-overview.md`, `02-ai-tech.md`, `03-team-roles.md`, and `04-scene-synopsis.md` from stale six-stage / 166-test language to the candidate evidence above. A repository search over `docs/nan2026/` for `166/166`, `166개`, `6개 스테이지`, `6개 캠페인`, `6단계`, and `프롤로그와 6` returned no matches after the update. Video status now distinguishes the automated-input continuous current-build capture from the separate Playwriter human-session technical review, and both sidecars explicitly prohibit regulated-submission use.

The overview now names all nine logical stages and correctly treats **Ash March** as the terminal stage. It no longer describes Ash Verdict as terminal.

## PDF regeneration

Exact command:

```bash
node tools/docs/build-nan2026-pdf.mjs
```

Observed result: success. Generated outputs:

| PDF | Bytes |
|---|---:|
| `docs/nan2026/pdf/00-submission-guide.pdf` | 41,292 |
| `docs/nan2026/pdf/01-game-overview.pdf` | 110,943 |
| `docs/nan2026/pdf/02-ai-tech.pdf` | 147,619 |
| `docs/nan2026/pdf/03-team-roles.pdf` | 96,096 |

The generator reported Apple SD Gothic Neo for body text, D2Coding for code, and rebuilt the five diagram PDFs (`ai-pipeline`, `capture-authenticity`, `core-loop`, `range-asymmetry`, `team-lanes`).

Supporting scene evidence was regenerated separately as
`docs/nan2026/pdf/04-scene-synopsis.pdf` (8,368,890 bytes). A local-link audit
over the six NAN markdown files found 24 local links and 0 missing targets.
Rendered inspection covered every graded PDF plus all four scene-synopsis
pages; extracted PDF text confirms the current `812/812` test claim.

## NHN submission mirror regeneration

Exact command:

```bash
node tools/docs/build-submission-pdf.mjs --dir docs/submission-nhn --all
```

Observed result: success. Apple SD Gothic Neo and D2Coding were selected; the
five generated files were 49,102 (`00`), 114,783 (`01`), 151,099 (`02`),
83,863 (`03`), and 117,843 bytes (`04`). Extracted text from `01` and `02`
confirms the current `812/812` claim and
`test-results-013939.xml` source path.

## Current local WebGL build

`bash tools/unity_batch.sh build` succeeded on Unity 6000.5.6f1:
`result=Succeeded`, `size=105082008`, `errors=0`, `warnings=6`,
`time=00:00:29.6721360`. The current local asset identity is
`e853ef3b27239fab`; evidence:
`_workspace/current/engineering/unity-logs/build-013342.log:6720`.

## Submission blockers

1. **YouTube upload:** `blocked-human`; the agent has no public channel authorization and must not claim completion.
2. **NAN application and consent submission:** `blocked-human`; requires the human operator and a retained receipt.
3. **Final candidate recertification:** metadata and local audible playback are observed for restored technical video artifacts; the `AudioPreloadTests` invariant, current full EditMode suite, and local build success are retained evidence for build `e853ef3b27239fab`. A fresh Unity test attempt on `2026-08-09T02:03:21Z` was blocked by another Unity instance holding the project lock, so it produced no result XML. Deployed-build parity, final performance/QA evidence, valid Stage-2 entry, and the regulated human-play take remain absent. These documents are synchronized to the latest observed source/test/video facts, not a final G1–G8 PASS claim.
