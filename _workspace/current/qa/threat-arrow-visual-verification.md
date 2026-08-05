# Threat arrow (§3.6 #9) — live visual verification

Target: <https://akillness.github.io/hongT/> · gh-pages `c51096c` · source `96a55bb`
· wasm 9,514,490 B · data 28,072,164 B (live artifacts byte-identical to the
local `build-webgl/` produced from `96a55bb`).

Closes the `[INFERENCE]` item carried by
`_workspace/current/qa/deployed-release-verification.md`: the idle threat arrow
was previously reported as "logic unit-pinned, visual confirmation outstanding".
It is now `[OBSERVED]` on the live build.

## Why the first attempt failed

The arrow is authored `Color(1, 0.83, 0.45, 0.7)` — the same pale gold as the
Lantern Reaver's blade and the enemy HP bars. At native exposure on the lava
floor of Cinder Span it cannot be told apart by eye, and a fixed screen crop
does not work because the camera follows the player. Three headless runs also
ended in death (spawn-adjacent enemies, then a vent hazard), so several capture
bursts contain only the 재강하 dialog.

## Method

Player located per frame by her lantern glow (the brightest saturated orange
blob in the field, with HUD bars and the 소리 button masked out). Frame-to-frame
centroid delta then classifies each frame as MOVING or STATIC **objectively**,
instead of trusting the input that was sent. Prologue courtyard was used rather
than Cinder Span: the dark backdrop separates gold cleanly from the floor.

Capture: 14 frames, ~520 ms apart, **no keyboard input at all** — so no hazard
walk and no death. Enemy contact still nudges the player, which is what
produces both regimes inside one burst.

## Result 1 — idle gating `[OBSERVED]`

Count of *elongated* gold blobs (size >= 40 px, elongation >= 1.8) per frame:

| regime | frames | tracker delta | elongated gold blobs |
|---|---|---|---|
| MOVING | 5 (`pro_01`–`pro_05`) | 34–65 px/frame | **1** — the enemy HP bar at (796,287), 95x5 |
| STATIC | 2 (`pro_07`, `pro_08`) | 0–2 px/frame | **2** — that HP bar **+ a new blob at (715,575)** |

`STATIC = MOVING + 1`, which is exactly what the feature requires. The extra
blob sits on the ground plane just off the player's feet and is absent from
every moving frame.

Evidence: `threat-arrow-evidence/moving-frames-no-arrow.png` (5 moving frames),
`threat-arrow-evidence/idle-static-frames.png` (`pro_06`–`pro_08`).

## Result 2 — it is a LineRenderer, not a blade `[OBSERVED]`

Geometry of the extra blob, identical in both static frames:

- centroid (715,576), principal-axis length ~75 px, offset from the player
  lantern dx=-22 dy=+225 (i.e. on the ground, not in a hand)
- width 10.0 px at the end nearest the player, 5.1 px at the far end —
  a **taper**, matching `startWidth 0.07` / `endWidth 0.02`
- flat unlit fill, no shading gradient, no hilt, no texture
- byte-identical geometry across the two frames, so it is not an animation pose

Evidence: `threat-arrow-evidence/arrow-6x-taper.png` (6x nearest-neighbour).

## Result 3 — it points at the nearest enemy `[OBSERVED]`

This was the property most at risk. A single frame showed the stub pointing
lower-left while enemies were visibly upper-right, which looks like an inverted
direction vector. An enemy-sprite census was attempted first and **discarded as
unusable** — brightening the dark courtyard yielded 28 candidate blobs against a
HUD count of 적 4, i.e. mostly rubble and architecture.

Decisive test instead: reposition the player so the enemy geometry flips, idle,
and read the stub bearing.

| player x | stub bearing | taper (tail/tip) |
|---|---|---|
| 514 (left of the pack) | **+16.0°** | 9.7 / 5.3 |
| 860 (ran past, right) | **-169.0°** | 7.6 / 5.5 |
| 889 (right) | **-176.0°** | 9.9 / 5.6 |

The bearing inverts by ~175° when the player crosses to the far side of the
enemies. A blade rigged to a body cannot invert bearing based on where its owner
stands relative to enemies; a "point at nearest enemy" vector must.

Independent confirmation of Result 1 in the same run: the stub was absent in
exactly the three frames where the tracker still showed drift (`ab_L_1` 515,
`ab_L_2` 515->429, `ab_R_2` 889->932).

The earlier "wrong direction" reading was one ambiguous frame under an
iso-projected direction (`ViewWorld.ToWorld` maps sim y to world -z), not a
defect.

## Still `[INFERENCE]` — combo poses (attack2 / attack3)

Not verified live, and the blocker is now precisely identified.

16 frames were captured in Cinder Span with Space pressed every other frame.
The run **was** in the dungeon — stage banner and the SHIFT/Q/E/R/F skill row are
both present, and `death_frac` 0.36–0.37 is below the 0.60 death-dialog
threshold, so these are live combat frames, not a death screen.

Reading the three combo pips directly (`HudView` renders filled pips
`(1,0.83,0.45,0.95)` vs empty `(1,1,1,0.14)`) across the band located at
x[700..833] y[949..964]:

    all 16 frames: cells=[0.01, 0.00, 0.03]  lit=0

**Combo tier never left 0.** The Space presses did not land chained hits, so
`ComboIndex` stayed 0 and `ResolveActionValue(Attack, 0, false)` correctly
returned the plain attack value 5 — the substates were never *requested*. This
is an unmet precondition in the capture, not evidence of a defect, and not a
metric failure.

What is known about that path without a live capture:
- animator states `attack2`, `attack3`, `cast` are present in the shipped
  `build-webgl.data.unityweb` (byte scan of the decompressed archive)
- `ActorView.ResolveActionValue` is pinned exhaustively by 9 `PoseResolveTests`
  cases, and the clip-table alignment by 4 `ClipTableTests` cases

Landing a 3-hit chain needs sustained melee range without dying, which headless
CDP input did not achieve across four attempts. Left as a human-judgement item.

## Gate

EditMode 195/195 on `96a55bb`'s tree (`unity-logs/test-results-175105.xml`),
0 failures. Live smoke: lobby, prologue sortie, Cinder Span drop — 0 console
errors, 0 warnings on every route.
