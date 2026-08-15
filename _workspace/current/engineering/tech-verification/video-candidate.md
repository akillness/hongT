# Cycle 13 current-build video verification

- run-id: `20260808-achilles-quality`
- public beat: **NAN 2026 final submission**
- recorded: `2026-08-08T16:40:18.464205Z`
- status: `[OBSERVED]` current local-build capture complete; cycle-13 final-candidate authority remains absent because cycle 13 was not validly entered.

## Deterministic browser-play capture

| Field | Observed value | Evidence |
|---|---:|---|
| Output | `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-final.mp4` | retained file |
| Runtime | Archive-restored automated browser capture from the same run packet | metadata JSON |
| Build identity | `e853ef3b27239fab` | metadata JSON captured from loaded HTML; current `build-webgl/index.html:105-112` |
| Codec / dimensions / frame rate | H.264, `1440×900`, `30 fps` | `ffprobe`: `1440,900,30/1,54.966667` |
| Duration | `54.966667 s` | `ffprobe` in metadata JSON |
| Size | `4,325,784 bytes` | metadata JSON |
| SHA-256 | `75b441bd1d52d6377ef1e8f26e5b43a6a48d9bd510d93a024e543215d6dbc696` | repaired sidecar digest |
| Browser page errors | Not remeasured in this repair step | metadata JSON records archive-restored status |
| Input coverage | Automated-input capture; detailed input counts not preserved in repaired sidecar | metadata JSON |
| Compression | PATH `ffmpeg` fallback under the CompressO/offline-compression route, H.264 CRF 28 slow, faststart | repair command output |

Primary metadata: `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-final.mp4.json`.

## Shot and interaction coverage

The capture script recorded these ordered beats in the retained JSON:

1. Lobby diorama and Sortie rail (`3.2 s`).
2. Ember Gallery entry (`5.0 s`).
3. Held-input movement and melee pattern (`13.6 s` completion).
4. Rift Bolt, Grave Pulse, tactical dash, Void Aegis, and Ash Nova (`13.8–18.8 s`).
5. Guardian `focus`, `defend`, `shield`, and `nova` commands (`24.0–41.5 s`).
6. Continued combat until the 55-second capture target.

Visual sampling evidence was not regenerated in this repair step. The restored MP4 and sidecar are retained as technical video artifacts; they do not replace a fresh human-viewed QA pass or regulated human-play submission capture.

## Playwriter review deliverable

A separate live-session technical review was recorded through Playwriter and
compressed through CompressO with both video and audio retained:

- Video: `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-playwriter-compresso.mp4`
- Metadata: `docs/nan2026/assets/video/nan2026-cinder-court-cycle13-playwriter-compresso.mp4.json`
- Build identity: `e853ef3b27239fab`
- Observed video probe: H.264, `1920×1200`, `30 fps`, `37.066667 s`
- Observed audio probe: AAC-LC, `48 kHz`, stereo, `37.005167 s`
- Observed audio level: mean `-24.3 dB`, max `-3.3 dB`
- SHA-256: `904c35b119f6af772383ca75dc195da447f6b92de85903d4d0d3a75bcb5bd07a`

This artifact preserves a Playwriter technical-review segment with an audible
stream after offline compression. It is technical review evidence, not the regulated
human-play submission capture.

## Gate limit

This packet closes the metadata, shot-coverage, and observed audible-playback gaps for the current local build. The same local candidate also has a successful Unity WebGL build (`engineering/unity-logs/build-013342.log:6720`) and a refreshed `812/812` EditMode result (`engineering/unity-logs/test-results-013939.xml:2`), including the WebGL audio-preload invariant. It does **not** award G1, G4, or G6 and does not convert cycle 13 into a validly entered cycle. Final-candidate recertification still requires the predecessor Stage-2 PASS, final performance evidence, independent QA measurements, and director gate verdicts named in `production/task-manifest.md`.
