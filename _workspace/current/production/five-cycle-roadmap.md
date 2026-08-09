# Five-Cycle Roadmap — `20260808-achilles-quality`

## Program contract

- `[OBSERVED]` Public beat: **NAN 2026 final submission**.
- `[OBSERVED]` Cycle 9 begins at **Stage 2 Phase 2a** under the cycle-8 retrospective. Its Stage 1 concept, worldview, and presentation context are carried only as inputs; Stage 1 is not reopened.
- `[TARGET]` Cycles 10–13 use `_workspace/current/design/trend-survey/achilles-steam-source.md` only as action-RPG play-concept calibration. HongT's Cinder Court worldview and deterministic Unity/WebGL architecture remain the source of truth.
- `[TARGET]` Every cycle uses one operating mode and keeps a Stage 1 → Stage 2 → Stage 3 milestone thread. A stage advances only on measured gate evidence; the roadmap does not pre-award a verdict.
- `[TARGET]` The two human-only actions—YouTube upload and application submission—remain separate pending tasks through cycle 13.

## Gate evidence standard

| Gate | Exact numeric exit condition | Program evidence state |
|---|---|---|
| G1 | 0 un-waived lore violations; 100% shipped strings/effects/scenarios trace to `design/worldview.md` | `[TARGET] pending; each changed player-visible item needs a new QA audit` |
| G2 | 100% mechanics in `design/balance-sheet.md`; matchup win rates 45–55%; TTK within ±15% of target; no pair above 1.3× median combo EV | `[TARGET] pending; no cycle-9 remeasurement yet` |
| G3 | ≥3 independently viable archetypes in band; no archetype >50% optimal-play dominance; ≥5 archetypes tested | `[TARGET] pending; archetype rotation required` |
| G4 | Median immersion ≥4.0/5; effect-feedback latency ≤100 ms; 0 unresolved S1/S2 readability complaints | `[TARGET] pending; cycles 12–13 require scene scoring` |
| G5 | Paid/free win-rate delta ≤5%p; reversal probability ≤30% per activation with cap/cooldown; free parity in 10–20 sessions; every revenue point signed by designer and PM | `[OBSERVED] FAIL at cycle-9 entry because parity was not remeasured after reachability changed` |
| G6 | Telemetry fields emitting; rollback tested once; readiness checklist 100%; p95 frame ≤16.7 ms; long frames <0.5%; memory stable for 30 min; input ≤100 ms | `[TARGET] pending for this program; historical evidence is not a cycle-13 release measurement` |
| G7 | ≥1 loop with period 30–180 s, ≥3 actions, ≥1 reward, and voluntary repeat-rate ≥70% | `[TARGET] pending; lobby-flow change invalidated the prior repeat-rate baseline` |
| G8 | ≥1 element present in ≤2 of ≥5 surveyed titles and QA impression score ≥4/5 | `[OBSERVED]` FAIL: synthetic blind-panel median 3/5 < 4/5; frequency provenance remains unverified and no qualifying human session exists (`qa/gate-measurements.md:180-221`) |

`[TARGET]` Any missing value, method, or evidence path is `FAIL`; it is never inferred from a prior green build or from design intent.
`[TARGET]` Stage transitions are hard gates: Stage 2 begins only after the director records Stage-1 `PASS` (except the recorded cycle-9 Stage-2 re-entry), Stage 3 begins only after Stage-2 `PASS`, and cycle close begins only after Stage-3 `PASS`. A `FIX` loops the failed gate at most twice; `REDO` returns to the previous stage.

### Current ledger disposition — 2026-08-08

- `[OBSERVED]` Cycle 9 is **FIX at Stage 2**. C9-004 owner cutover is complete, but G8 is below band, G5/G2/G3/G7 evidence is incomplete, current S1 count is unknown, and no Stage-2 director `PASS` exists.
- `[OBSERVED]` Cycles 10–13 are **not-validly-entered**. Their design, QA, engineering, document, and local-video artifacts are prework only because cycle 9 never authorized cycle 10 entry.
- `[OBSERVED]` No `REDO` is assigned: no current-cycle director review or failed-loop count supports it. Cycle 13 is additionally `blocked-human` for YouTube upload and NAN application receipt.


## Cycle 9 — Evidence-debt retune and ownership cutover

- `[TARGET]` **Operating mode:** `reprioritization`.
- `[TARGET]` **Focus:** honor the recorded Stage 2 re-entry before adding Achilles-directed scope.
- `[TARGET]` **Next public beat:** NAN 2026 final submission; checkpoint is a first-time-user run on live GitHub Pages without external instructions.
- `[OBSERVED]` **Current disposition:** `FIX`; remain at C9 Stage 2. See `retrospectives/cycle-9-retrospective.md`.

### Milestone thread

1. `[OBSERVED]` **Stage 1 context carried, not reopened:** current HongT concept/worldview/presentation remain unchanged; cycle-8 evidence supplies context only.
2. `[TARGET]` **Stage 2 Phase 2a entry:** QA collects G8 impression score first, then runs ≥5-archetype exploit/fun discovery and remeasures the G5 T5 parity denominator. Engineering names the single surviving equipment/sigil implementation before any balance edit. Missing G8 or G5 evidence remains `FAIL`.
3. `[TARGET]` **Stage 2 Phases 2b–2d:** designer retunes only measured failures; PM and designer sign every touched revenue/balance number and record the measurement condition; programmer applies data-owned changes and closes or explicitly defers each defect; QA reruns G2, G3, G5, G7, and G8.
4. `[TARGET]` **Stage 3:** only after the director records Stage-2 `PASS`, verify changed presentation, Unity/WebGL stability, telemetry, rollback, performance, and live first-time-user behavior for G1, G4, and G6.

### Exit evidence

- `[TARGET]` Stage 2 exit requires current G2, G3, G5, G7, and G8 values, methods, and evidence paths; Stage 3 exit requires the same for G1, G4, and G6. G8 survey frequency plus impression score ≥4/5, G5 10–20-session parity measurement, one implementation owner, ≥5-archetype report, current loop repeat-rate, and 0 open S1 defects are explicit cycle-9 blockers.
- `[TARGET]` If any required measurement is missing or outside its band, cycle 9 exits `FIX` or `REDO`; no Achilles-inspired implementation starts as a workaround.

## Cycle 10 — Action and combat readability

- `[TARGET]` **Operating mode:** `gdd-to-backlog`.
- `[TARGET]` **Focus:** translate the Achilles calibration into HongT-native committed strike, evade/guard response, enemy coordination, boss-phase readability, and finish-impact slices without changing frozen contracts by imitation.
- `[TARGET]` **Next public beat:** NAN 2026 final submission; checkpoint is an internal action-combat playtest build.
- `[OBSERVED]` **Current disposition:** `not-validly-entered`; existing artifacts are prework only. See `retrospectives/cycle-10-retrospective.md`.

### Milestone thread

1. `[TARGET]` **Stage 1:** designer maps each proposed action verb to HongT's existing combat and worldview; QA defines ≥5 action archetypes and benchmark cases; PM marks every reward/revenue coupling; designer and PM sign the round-1 numeric bounds; programmer records additive-interface, telemetry, and resource budgets before building the playable slice. The slice includes animation/resource provenance and a numeric G7 loop model.
2. `[TARGET]` **Stage 2:** only after the director records Stage-1 `PASS`, QA adversarially tests hit windows, evade/guard response, boss phases, coordinated enemies, TTK, combo EV, dominant strategies, voluntary loop repeat, novelty, and fun discovery. Designer/PM negotiate only from measurements; programmer implements data-owned or additive changes and preserves digest invariants.
3. `[TARGET]` **Stage 3:** only after the director records Stage-2 `PASS`, programmer profiles frame/input/memory and movement paths; designer/programmer perform hit-feedback and climax presentation pass; QA scores immersion/readability and runs Unity/WebGL regression; PM validates reward rhythm telemetry.

### Exit evidence

- `[TARGET]` Stage 2 exit requires current G2, G3, G5, G7, and G8 values, methods, and evidence paths; Stage 3 exit requires the same for G1, G4, and G6. Action-specific evidence includes deterministic digests, effect feedback ≤100 ms, p95 ≤16.7 ms, and 0 open S1/S2 readability complaints.
- `[TARGET]` A visually stronger attack with no measured gameplay, fairness, novelty, worldview, immersion, or operations proof remains pending, not complete.

## Cycle 11 — Balance, progression, and growth

- `[TARGET]` **Operating mode:** `gdd-to-backlog`.
- `[TARGET]` **Focus:** make growth, equipment, companion/build identity, comeback bounds, free-path parity, and steady rewards coherent and measurable.
- `[TARGET]` **Next public beat:** NAN 2026 final submission; checkpoint is an internal progression/fairness playtest build.
- `[OBSERVED]` **Current disposition:** `not-validly-entered`; existing artifacts are prework only. See `retrospectives/cycle-11-retrospective.md`.

### Milestone thread

1. `[TARGET]` **Stage 1:** designer first validates the Achilles/current genre composition-controls-rules survey; QA refreshes benchmark calibration while defining new/steady/comeback/optimizer/accessibility archetypes; designer audits every growth mechanic into the balance sheet and worldview; PM maps all revenue/reward points and parity sessions; designer and PM sign round-1 couplings; programmer validates and builds the one-owner equipment/sigil path with telemetry/resource evidence.
2. `[TARGET]` **Stage 2:** only after the director records Stage-1 `PASS`, QA runs scripted matchups, progression simulations, exploit hunts, paid/free deltas, reversal probability, T5 parity sessions, loop repeat, and fun-discovery sessions. Designer retunes measured outliers; designer and PM sign every coupled number; programmer applies data-driven changes; QA re-verifies.
3. `[TARGET]` **Stage 3:** only after the director records Stage-2 `PASS`, programmer verifies save compatibility, telemetry emission, rollback, performance, and WebGL persistence; QA runs full progression regression and G1/G4 review of growth feedback; PM completes the forecast using emitted fields rather than assumed conversion.

### Exit evidence

- `[TARGET]` Stage 2 exit requires current G2, G3, G5, G7, and G8 values, methods, and evidence paths; Stage 3 exit requires the same for G1, G4, and G6. All mechanics must be represented in the balance sheet, every touched revenue point must be signed, and save/telemetry evidence paths must exist.
- `[TARGET]` Unmeasured retention, conversion, impression, or fun claims remain hypotheses and cannot satisfy a gate.

## Cycle 12 — Immersion, presentation, resources, and fun

- `[TARGET]` **Operating mode:** `gdd-to-backlog`.
- `[TARGET]` **Focus:** harvest scene readability, animation/VFX/SFX impact, environmental identity, narrative consistency, novelty, emergent fun, and submission-video shot coverage.
- `[TARGET]` **Next public beat:** NAN 2026 final submission; checkpoint is a candidate capture build with a measured scene matrix.
- `[OBSERVED]` **Current disposition:** `not-validly-entered`; existing artifacts are prework only. See `retrospectives/cycle-12-retrospective.md`.

### Milestone thread

1. `[TARGET]` **Stage 1:** designer first validates the Achilles/current genre composition-controls-rules survey, then updates presentation intent and worldview traceability per scene; QA refreshes benchmark calibration and defines scene scoring/fun-observation protocol; PM audits reward/visibility couplings and signs round-1 bounds with the designer; programmer audits animation/VFX/SFX/3D resource ownership, WebGL budgets, telemetry, and provenance before the first build; the video owner defines a 30–60-second shot list from actual browser play.
2. `[TARGET]` **Stage 2:** only after the director records Stage-1 `PASS`, QA measures G8 impression and discovery notes across ≥5 archetypes while testing readability under real combat; designer develops only the strongest measured HongT-native striking element; PM checks that presentation does not obscure rewards or introduce paid advantage; programmer implements within pools and resource budgets.
3. `[TARGET]` **Stage 3:** only after the director records Stage-2 `PASS`, QA scores all shipped scenes for median immersion ≥4.0/5 and checks effect feedback ≤100 ms with 0 unresolved S1/S2 readability complaints; programmer runs Unity/WebGL visual, performance, memory, console, and resource checks; a candidate 30–60-second browser-play video is captured without marking YouTube upload complete.

### Exit evidence

- `[TARGET]` Stage 2 exit requires current G2, G3, G5, G7, and G8 values, methods, and evidence paths; Stage 3 exit requires the same for G1, G4, and G6. Resource manifest/provenance and candidate-video metadata must match the actual build.
- `[TARGET]` A polished screenshot or video cannot substitute for balance, archetype, fairness, loop, novelty, worldview, immersion, latency, console, performance, or operations evidence.

## Cycle 13 — Final stability and submission readiness

- `[TARGET]` **Operating mode:** `public-beat-readiness`.
- `[TARGET]` **Focus:** freeze scope, verify the Unity/WebGL candidate, synchronize video/docs/wiki content to measured reality, and leave human-only submission actions explicit.
- `[TARGET]` **Next public beat:** **NAN 2026 final submission**.
- `[OBSERVED]` **Current disposition:** `not-validly-entered`; technical evidence remains `FIX` and human upload/submission remain blocked. See `retrospectives/cycle-13-retrospective.md`.

### Milestone thread

1. `[TARGET]` **Stage 1:** no new feature concept. Designer validates the Achilles/current genre survey; QA refreshes the ≥5-title benchmark calibration; director freezes candidate scope and audits the source packet; designer checks worldview/presentation traceability; PM audits final revenue/reward/parity/forecast fields and signs round-1 couplings with the designer; programmer and QA inventory build, resource, regression, and telemetry evidence; docs/video/wiki owners compare every public metric and claim with the candidate build.
2. `[TARGET]` **Stage 2:** only after the director records Stage-1 `PASS`, QA performs the final ≥5-archetype exploit, balance, fun, novelty, growth, and first-time-user runs; designer/PM resolve only measured blockers; programmer changes only release blockers and records every defect disposition. G2/G3/G5/G7/G8 are remeasured against the candidate.
3. `[TARGET]` **Stage 3:** only after the director records Stage-2 `PASS`, run the exact Unity import/test/build, 30-minute soak, input/performance, rollback, browser console, live gh-pages, video-capture, document/PDF, link, and wiki synchronization checks. QA remeasures G1/G4/G6. Human operator then acts only after the Stage-3 `PASS` and owns YouTube upload and application submission.

### Exit evidence

- `[TARGET]` G1–G8 each have a candidate-build value, method, timestamp, and evidence path; release checklist is 100%; p95 frame ≤16.7 ms, long frames <0.5%, memory stable for 30 minutes, input/effect feedback ≤100 ms, and browser console/page errors are 0.
- `[TARGET]` `docs/nan2026/README.md`, generated PDFs, local video metadata, and any public wiki text contain no stale test counts, build sizes, features, or links.
- `[TARGET]` YouTube upload and application submission remain `blocked-human / pending` until a human supplies the URL and submission receipt. Technical readiness must not be relabeled as those actions being complete.

## Cycle transition rule

`[TARGET]` Each retrospective records measured G1–G8 values, unresolved risks, the next entry stage, and the NAN 2026 final-submission thread. A failed gate loops at most twice as `FIX`; a third failure forces a director scope decision. No cycle silently carries a missing measurement forward.
