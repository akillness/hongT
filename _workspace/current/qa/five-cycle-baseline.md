# Five-Cycle QA Baseline — 20260808-achilles-quality

- run-id: `20260808-achilles-quality`
- public beat: `NAN 2026 final submission`
- QA horizon: cycles 9–13
- entry: cycle 9, Stage 2 Phase 2a retune
- benchmark boundary: Achilles: Legends Untold calibrates action-RPG play concepts only; HongT keeps the Cinder Court worldview, deterministic 60 Hz simulation, current content, names, art, layout, and assets.

## Status language

- `[OBSERVED]`: a value is present in a cited repository artifact or captured session.
- `[INFERENCE]`: a conclusion follows from observed artifacts but was not directly measured as a session outcome.
- `[TARGET]`: a required future measurement or threshold; it is not a measured result.
- `UNKNOWN`: no measured value + method + evidence path exists yet. An unknown never counts as a pass.

## Measured facts at program start

| Area | Measured fact | Method | Evidence |
|---|---|---|---|
| Cycle entry | `[OBSERVED]` Cycle 8 orders Stage 2 re-entry; the active task manifest now records Phase 2a, G8 as a failed entry condition, and G5 parity as failed pending measurement. | Retrospective and manifest audit. | `_workspace/current/retrospectives/cycle-8-retrospective.md:184-197`; `_workspace/current/production/task-manifest.md:1-18` |
| G6 regression | `[OBSERVED]` EditMode 808/808 passed; mutation sweep caught 14/14; the recorded WebGL build had 0 errors and the four-state lobby browser smoke had 0 console/page errors. | Cycle-8 runner and browser artifacts. | `_workspace/current/qa/gate-measurements.md:138-150`; `_workspace/current/qa/mutation-sweep-cycle8.json`; `_workspace/current/qa/lobby-rail-smoke/` |
| Campaign breadth | `[OBSERVED]` the runtime contract and test source describe nine ordered logical stages; the cycle-2 browser run cleared all nine (`clearedMask 255→511`). | Contract/test audit and browser playthrough. | `docs/SIM_SPEC_CAMPAIGN.md:16-18`; `Assets/Tests/EditMode/StageCatalogTests.cs:35-39`; `_workspace/current/qa/playtest-report.md:14-18` |
| Difficulty mechanics | `[OBSERVED]` Story/Normal/Hard/Nightmare use damage multipliers 0.65/1.00/1.35/1.70, attack-cooldown multipliers 1.22/1.00/0.84/0.70, and attack-token caps 2/unlimited/3/4; isolated 1,800-tick observations recorded minimum attack gaps 93/75/64/54 ticks. | Deterministic isolated scenario. | `_workspace/current/design/balance-sheet.md:83-115` |
| G5 income sample | `[OBSERVED]` three completed expansion runs produced base run income 18, 17, and 18 relics; first-clear bonuses were 6, 8, and 10. Bonus/base ratios were reported as 33–59%, above the old 25% wording. | Browser autoplay, campaign localStorage read-back. | `_workspace/current/qa/playtest-report.md:14-18,29-37`; `_workspace/current/pm/reward-bands.md:15-21` |
| G5 access denominator | `[OBSERVED]` the lobby repair changed spend-UI reachability from 0% to 100%; cycle 8 therefore invalidated the prior sessions-to-T5 denominator and required remeasurement. | Canvas geometry plus four-state deployed-browser smoke. | `_workspace/current/qa/gate-measurements.md:27-40,114-123` |
| G8 frequency half | `[OBSERVED]` the novelty scorecard has candidates at 0–2 appearances in pools of 6–17 comparable titles, which meets only the frequency half of G8. Every listed candidate still has `미측정` impression score. | Scorecard audit. | `_workspace/current/design/novelty-scorecard.md:1-17,21-49,53-98` |
| G7 existing play | `[OBSERVED]` the three expansion gimmick loops were completed in a browser autoplay; the same report explicitly leaves voluntary repeat rate unmeasured. | Browser autoplay. | `_workspace/current/qa/playtest-report.md:20-32` |
| Determinism safety net | `[OBSERVED]` arena, prologue, invariant campaign anchors, and new dungeon stages have golden-digest tests; Hard and Nightmare identical-input snapshot determinism also has a test. | Test-source audit; latest aggregate result is the cycle-8 808/808 artifact. | `Assets/Tests/EditMode/DungeonGoldenDigestTests.cs:120-239`; `Assets/Tests/EditMode/DifficultyGroupAiTests.cs:369-374`; `_workspace/current/qa/gate-measurements.md:138-145` |
| Attack presentation baseline | `[OBSERVED]` a PlayMode capture records attack windup/contact/follow-through at normalized times 0.22/0.53/0.78 and verifies the mirrored swing window against the real simulation. | Unity MCP PlayMode trace and screenshots. | `_workspace/current/qa/swing-motion/mcp-playmode-verification.md:125-146` |
| Achilles source | `[OBSERVED]` the captured Steam page promises stamina-based dodge/block/strike/counter combat, build authorship, named duels, environmental identity, and coordinated enemies. | HTTP 200 Korean-locale source capture. | `_workspace/current/design/trend-survey/achilles-steam-source.md` |
| NAN test-count drift | `[OBSERVED]` NAN README and overview still say 166/166 while current cycle-8 evidence says 808/808. | Exact-string document audit. | `docs/nan2026/README.md:15`; `docs/nan2026/01-game-overview.md:150-155`; `_workspace/current/qa/gate-measurements.md:138-145` |
| NAN campaign drift | `[OBSERVED]` NAN documents say six logical stages and “sixth/final”; current contract and test source say nine ordered logical stages. | Document-to-contract audit. | `docs/nan2026/01-game-overview.md:23-29,90-107,194-200`; `docs/SIM_SPEC_CAMPAIGN.md:16-18`; `Assets/Tests/EditMode/StageCatalogTests.cs:35-39` |

## Unknowns: no baseline value yet

| ID | Unknown measurement | Exact method required | Blocking effect |
|---|---|---|---|
| U1 | `UNKNOWN` G8 impression median for every submitted novelty candidate. | Before any cycle-9 retune, run at least 5 scored sessions per candidate; each tester scores the five-dimension rubric in `qa/test-plan.md`, and QA records every raw ballot plus the median in `qa/gate-measurements.md#g8`. | `[TARGET]` Stage 2 entry is blocked until at least one candidate has median ≥4/5 and its frequency is ≤2 of ≥5 titles. This is not eligible for another carry-over. |
| U2 | `UNKNOWN` post-rail sessions to all three T5 equipment slots. | Execute the PM contract exactly: five pilots (`melee-rusher`, `kiter`, `skill-spammer`, `companion-commander`, `pacifist-dodger`) × routes A–E; count eligible settled Dungeon legs only; use live prices/rewards/rank transitions; stop at T5 or session 21. | `[TARGET]` every pilot-route `N_T5` must be 10–20; entry 17 remains escalated and unsigned until designer review plus QA replay. |
| U3 | `UNKNOWN` paid-path absence audit; PM’s current-build identity delta is 0 pp only by inference. | Audit all submission-build offer/checkout/premium/continue/power constructors and the 24 browser sessions; prove `paid_offer_visible=false` and `paid_power_applied=false`. If a paid state is constructible, run paired equal-skill paths. | `[TARGET]` G5 needs a measured absolute delta ≤5 pp; disabled-path identity may count as 0 only after the absence audit. |
| U4 | `UNKNOWN` comeback immediate-reversal probability. | For each opportunity in the five-pilot × nine-stage × reference-loadout grid, replay identical config/input with all peril clauses off and with only activation `i` enabled. A reversal is all-off defeat → isolated-activation clear. | `[TARGET]` `reversals/opportunities` ≤30%, ≤2 activations/run, re-arm only at health ≥50%, and no simultaneous clauses. |
| U5 | `UNKNOWN` per-archetype clear/win rates for the four difficulty tiers. | Execute 20 distinct deterministic input scripts per archetype × tier on the declared stage set; record wins/20, TTK and dominant-policy share. Repeats of one identical deterministic script do not increase N. | `[TARGET]` G2/G3 need 45–55% matchup win rate, ≥3 independently viable strategies, ≥5 tested archetypes, and no archetype >50% optimal-play dominance. |
| U6 | `UNKNOWN` wave TTK distribution and combo-pair EV after current difficulty/amendment changes. | Export wave-start, wave-clear, damage and reward events for the simulation matrix; compare each wave bucket to targets 12/22/34 s ±15%; enumerate every pair and divide its EV by median pair EV. | `[TARGET]` G2 blocks at any TTK outside ±15% or pair >1.3× median EV. |
| U7 | `UNKNOWN` action-feedback latency from input to readable motion and from hit event to audiovisual response in shipped WebGL. | Browser Performance trace at 60 Hz; 30 trials for each current action/mode (attack, Dungeon dash/skills, Arena skills, companion commands). Use event timestamps, not video adjectives; add guard/counter only if a later cycle adopts a deterministic contract. | `[TARGET]` each spot-check must be ≤100 ms; failures are G4 evidence. |
| U8 | `UNKNOWN` structured immersion median across Dungeon, Prologue, Training and Arena. | Five-dimension 1–5 rubric, ≥5 independent scored sessions per mode; retain ballots and screenshots/video. | `[TARGET]` per-scene median and aggregate median must be ≥4.0/5; unresolved readability complaints of S1/S2 must be 0. |
| U9 | `UNKNOWN` mandatory-loop and voluntary-repeat evidence after the lobby route changed. | Segment L1 from `WaveStarted` to next `WaveStarted`/`StageCleared`/`GameOver`; require 30–90 s, ≥4 actions, ≥1 reward. Measure ≥10 unprompted human re-entry decisions. For boss L2, include setup from boss-wave `WaveStarted` to terminal. | `[TARGET]` L1 repeat ≥70%; L2 30–180 s, every phase ≥2.17 s, intended action + room-rule answer identification ≥70%. Harness minima (30–180 s, ≥3 actions, ≥1 reward) remain the outer gate. |
| U10 | `UNKNOWN` G6 final performance and ops proof. | Capture p95 frame, long-frame %, input latency and 30-minute memory slope; audit telemetry fields; execute rollback once; complete release checklist. | `[TARGET]` p95 ≤16.7 ms, long frames <0.5%, input ≤100 ms, stable memory over 30 minutes, telemetry complete, rollback tested once, checklist 100%. |
| U11 | `UNKNOWN` duplicate equipment/sigil implementation owner and survivor. | Programmer names the single owner and QA runs the same purchase/equip/sigil transitions through both prior entry surfaces, comparing one canonical saved state and one rendered outcome. | `[TARGET]` cycle-9 cannot close while two implementations can diverge. Cycle-8 assigned this item an expiry of cycle 9. |
| U12 | `UNKNOWN` current open S1/S2 count. | Materialize and audit `qa/defect-register.md`; reproduce every open row on the cycle-9 candidate. | `[TARGET]` any open S1 blocks every gate; G4 additionally requires 0 unresolved readability complaints at S1/S2. |
| U13 | `UNKNOWN` NAN 2026 public packet truth after cycle-9. | Compare every numeric/runtime claim in the Markdown and regenerated PDFs with the accepted gate artifacts; record mismatches as defects. | `[TARGET]` NAN final submission is blocked while 166/166 or six-stage claims remain in published artifacts. |

## Cycle-9 entry blockers and first probes

These are the first probes because they are reproducible without changing the concept. Achilles-derived direction begins in cycles 10–13.

| Priority | Probe | Reproduction | Required output |
|---|---|---|---|
| P0 | G8 has no impression value. | Open `_workspace/current/design/novelty-scorecard.md`; every submitted candidate’s impression cell is `미측정`. Cross-check `_workspace/current/retrospectives/cycle-8-retrospective.md:191-195`. | `qa/gate-measurements.md#g8`: candidate, ≥5 raw ballots, median, method, session/video paths. Entry remains blocked until median ≥4/5. |
| P0 | G5 parity denominator changed and was not rerun. | Open `_workspace/current/qa/gate-measurements.md:114-123`; it explicitly records UI reachability 0%→100% and “측정 필요”. | `qa/gate-measurements.md#g5`: fresh-save session ledger and first T5 session per slot; all claimed paths inside 10–20. |
| P0 | Cycle-9 G5 measurement contract is unsigned and its joined telemetry row is absent. | Inspect `pm/negotiation-record.md:383-410`: entry 17 is `escalated`, `pending`, `signed: []`. Inspect `pm/reward-bands.md:128-177`: neither last-run digest nor campaign endpoint alone contains the required joined fields. | Designer/PM signatures plus QA’s five-pilot × routes A–E replay, exact `N_T5`, comeback pairs and paid-path absence audit. Until then G5 is FAIL. |
| P0 | Canonical QA evidence files are absent at audit start. | `test -f _workspace/current/qa/exploit-register.md; test -f _workspace/current/qa/defect-register.md; test -f _workspace/current/qa/regression-matrix.md` returned no matching artifacts during the audit. | The required registers, populated or explicitly zero-row with build/session identity. Missing evidence path means FAIL. |
| P1 | Duplicate equipment/sigil implementation can drift. | Exercise an identical buy/equip/sigil action through each surviving entry surface; reload and compare save blob, costs, tiers, active sigil and visible result. | One named implementation owner plus identical-state evidence, or a reproducible divergence defect. |
| P1 | NAN public claims are stale. | `grep -R "166/166\|6개 논리 스테이지\|6번째이자 마지막" docs/nan2026` and compare with cycle-8 808/808 and nine-stage contract/test evidence above. | Corrected Markdown, regenerated PDFs and a post-generation exact-string audit. This QA file records the blocker; docs remain outside QA ownership. |
| P1 | Action feel has pose snapshots but no shipped latency distribution. | Run 30 attack/dodge/guard/skill trials in each browser mode and capture input, sim event, animation onset, effect onset and audio onset timestamps. | CSV/JSON trace plus p50/p95 per action/mode; every G4 spot-check ≤100 ms. |

## Five-cycle QA program

| Cycle | QA purpose | Required measurements before exit | Concept boundary |
|---|---|---|---|
| 9 | Restore Stage-2 evidence integrity. | G8 entry score first; G5 parity/comeback/fairness; ≥5 archetype rotation; G2 TTK/EV; G7 repeat-rate; duplicate-owner probe; canonical registers. | `[TARGET]` Preserve the current Cinder Court concept. No Achilles-driven feature substitution. |
| 10 | Calibrate committed action feel. | Attack/dodge/guard/skill input and feedback latency, cancellation exploit probes, telegraph recognition, deterministic tick parity. | `[TARGET]` Borrow only the concept of readable commitment and response; do not copy stamina values, Greek content, moves, layouts or assets. |
| 11 | Validate build identity and coordinated pressure. | Distinct-strategy viability, companion/group-AI dominance, build-pair EV, difficulty-tier clear rates, growth fairness. | `[TARGET]` Use HongT equipment, sigils, companion and difficulty systems as the implementation vocabulary. |
| 12 | Validate named encounters and environmental tactics. | Boss-phase recognition, telegraph/readability, hazard counterplay, safe-spot/exploit census, per-scene immersion. | `[TARGET]` Keep courtroom/lantern story, deterministic hazards and existing named entities. |
| 13 | Submission-quality impact and operations. | Full G1/G4/G6 final, 30-minute soak, rollback drill, release checklist, NAN claim audit, four-mode browser regression. | `[TARGET]` The final packet must describe the measured HongT build, never Achilles media or claims. |

## Non-negotiable gate thresholds

```yaml
g1: {unwaived_lore_violations: 0, player_visible_traceability_pct: 100}
g2: {mechanics_covered_pct: 100, matchup_win_rate_band: [0.45, 0.55], ttk_tolerance: 0.15, dominant_pair_ev_vs_median_max: 1.3}
g3: {archetypes_tested_min: 5, independently_viable_min: 3, optimal_play_dominance_max: 0.50}
g4: {immersion_median_min: 4.0, feedback_latency_ms_max: 100, unresolved_readability_s1_s2: 0}
g5: {paid_free_delta_pp_max: 5, comeback_reversal_probability_max: 0.30, free_parity_sessions_band: [10, 20], signed_revenue_points_pct: 100}
g6: {p95_frame_ms_max: 16.7, long_frame_ratio_max_exclusive: 0.005, memory_soak_minutes: 30, input_latency_ms_max: 100, telemetry_fields_pct: 100, rollback_drills_min: 1, release_checklist_pct: 100}
g7: {loop_period_s_band: [30, 180], actions_per_loop_min: 3, rewards_per_loop_min: 1, voluntary_repeat_rate_min: 0.70}
g8: {comparable_titles_min: 5, comparable_frequency_max: 2, impression_median_min: 4.0}
```

Source of all thresholds: `skill://game-studio-harness/references/quality-gates.md`. No adjective-only verdict can override this block.
