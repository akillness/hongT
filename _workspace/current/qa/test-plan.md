# QA Test Plan — Cycle 2: New Dungeons + Gimmicks (run-id 20260806-dungeon-gimmicks)

2026-08-05 · game-qa · Stage 1 Phase 1a. Companion: `qa/benchmark-notes.md` (calibration
bands), designer novelty survey (`design/trend-survey/`).

## Scope & invariants under test

- Cycle 2 adds NEW dungeons beyond the existing 6 catalog stages (cinder-span,
  ember-gallery, abyss-chancel, witness-well, echo-throne, ash-verdict), each with a
  distinct gimmick. All new mechanics: deterministic (NO RNG), data-driven placement
  (`HazardConfig[]` / `StageEntry.HazardOverride`), WebGL-safe.
- Frozen contract: SIM_SPEC.md + amendments stay intact. Same config + same input
  sequence ⇒ same `RunDigest` (Score, Wave, Kills, Relics, HealthRemaining, Reason).
- View additions (telegraph visuals, reduced-motion gating) must NEVER change sim
  outcomes.
- Test vehicle: EditMode NUnit against `CinderSim` directly (existing convention:
  `CampaignSimTests`, `HackSimTests`, `CinderSimTests`). No PlayMode dependency for
  gate numbers; capture/immersion checks run on the deployed WebGL build.

## A) Archetype rotation set (G3 requirement: ≥5 archetypes)

Each archetype is a pure deterministic bot `SimInput f(ISimSnapshot sim, int tick)` —
polled-state fields only, no RNG, no view access. Reference stat/equip grids below;
"reference ranks" = weapon/lantern/cloak 2/1/3 (existing test convention) and 5/5/5.

| # | Archetype | Concrete input pattern (SimInput fields) | What it stresses |
|---|---|---|---|
| 1 | **melee-rusher** | Every tick: `MoveX/MoveY` = normalized vector toward nearest living enemy (iso metric dy×1.42); `AttackQueued=true`; `DashQueued=true` when iso distance > 150 px (closes gap); never `WardQueued`/`NovaQueued`. | TTK ceiling, contact-damage exposure, whether new gimmicks punish melee uptime disproportionately (G3 viability). |
| 2 | **kiter** | Existing `CampaignSimTests.BotInput` pattern: retreat vector when nearest enemy < 260 px, approach when > 420 px, hold band otherwise; `AttackQueued=NovaQueued=WardQueued=true` every tick (queues no-op while on cooldown). | Baseline clear bot (already proven to clear stage 1 at 5/5/5); the control archetype for digest regressions. |
| 3 | **skill-spammer** | Hold anchor at combat-plane center (768,604): `MoveX/MoveY` only to re-center when pushed > 60 px; every tick `NovaQueued=BoltQueued=PulseQueued=WardQueued=true`; `AttackQueued` only while `Charge` is full. | Cooldown economy vs gimmick cadence (does a stationary caster get farmed by vents? altar synergy), oil-burst interaction (relic-altar +18 oil). |
| 4 | **companion-commander** | Config with `CompanionId="ember-cohort"`. Player runs kiter movement with wider band (retreat < 320 px). Every 300 ticks alternate `CompanionHoldQueued=true` (one tick, at current player position — near new-gimmick anchor when within 200 px) / `CompanionRecallQueued=true`. | Companion pathing vs blockers (pillar-class gimmicks), hold/recall determinism near hazard zones (HackSimTests §companion invariants extended to new gimmicks). |
| 5 | **pacifist-dodger** | Never `AttackQueued`/skills. Orbit the combat plane perimeter clockwise: waypoints (348,434)→(1188,434)→(1188,774)→(348,774), `MoveX/MoveY` toward next waypoint; `DashQueued=true` on the first tick any hazard with `telegraphing==true` covers the player position (iso metric). Run capped at 60×120 ticks; record survival time + hazard hits taken. | Telegraph fairness in isolation: with perfect information, hazard damage taken should be ≈0 when telegraph ≥ band (benchmark-notes band 1). Any unavoidable hit = S1 defect (undodgeable by design). |

Rotation discipline: every gate measurement below runs all 5 archetypes × reference
ranks on every NEW stage, plus kiter on all 6 existing stages (regression control).
Per-archetype results land in `qa/playtest-report.md` (per-archetype table).

## B) Per-gimmick determinism checks (blocking, run per new gimmick kind)

Convention: `AssertSameDigest` (all 6 RunDigest fields) + player X/Y within Tolerance —
same as `HackSimTests.AssertSameDigest`.

| Check | Method | Pass condition |
|---|---|---|
| D1 same-instance repeat | Same `CampaignConfig`/`HackConfig` + same scripted input (kiter, 1800–18000 ticks) run twice via fresh `new CinderSim(in config)` | Digests identical (extends `Regression_CampaignConstructor_StillProducesItsOwnDigest` to each new stage id) |
| D2 cross-archetype determinism | Each of the 5 archetype bots run twice per new stage | Digest identical per bot; digests DIFFER across bots (proves inputs actually reach the sim) |
| D3 phase/config sensitivity | Mutate one data field of the new gimmick (phase +0.6 s, or position +50 px) in a test-local config | Digest CHANGES (proves placement is data-driven and live, not baked); simultaneous-telegraph census over one LCM of all periods reports max concurrent `telegraphing==true` ≤ 3 (benchmark band 5) |
| D4 view-independence | Run sim headless vs through `GameView` tick adapter with `ViewPrefs.ReducedMotion` on and off | All four digests identical (reduced motion & view never touch outcomes) |
| D5 float stability | 1800-tick run digest compared against committed golden values per new stage (kiter @ 2/1/3) | Exact equality (int fields) / exact float equality (HealthRemaining — deterministic fixed-step, no tolerance) |

## C) Gate band checks (what QA must measure, and where the number lands)

Thresholds from `skill://game-studio-harness/references/quality-gates.md`; calibration
bands from `qa/benchmark-notes.md`. Every row = measured value + method + evidence path.

| Gate | Measurement | Band / threshold | Method | Evidence file |
|---|---|---|---|---|
| **G2** | Stage clear rate ("win-rate") per new stage, kiter + melee-rusher @ 2/1/3 and 5/5/5, 60×300-tick cap | 45–55% band interpreted for PvE as: reference-rank clear must succeed at 5/5/5, be contested at 2/1/3 (clears within cap on ≥1 archetype, fails on ≥1) | Scripted EditMode runs, deterministic — one run per cell IS the population | `qa/gate-measurements.md#g2` |
| **G2** | TTK: median wave-clear ticks per wave per new stage vs balance-sheet target | within ±15% of `design/balance-sheet.md` target | Tick census between `WaveStarted` events | `qa/gate-measurements.md#g2` |
| **G2** | Hazard damage share: (HP lost to new gimmick) / (total HP lost), kiter run | 10–35% (below 10% the gimmick is decorative, above 35% it dominates enemies) | Diff `Player.Health` on `HazardPulse`-family event ticks | `qa/gate-measurements.md#g2` |
| **G2** | Single-hit ceiling | No gimmick hit > 30% max HP (benchmark band 2) | Static audit of new `HazardConfig` damage vs 100+vit×8+cloak floor | `qa/gate-measurements.md#g2` |
| **G3** | Per-archetype viability: clear/survival per new stage, all 5 archetypes | ≥3 archetypes independently viable (clear at 5/5/5 with distinct strategies); no archetype's optimal clear dominates (fastest TTK ≤ 1.5× slowest viable); ≥5 tested | Full rotation matrix (§A) | `qa/playtest-report.md` (per-archetype table) + `qa/gate-measurements.md#g3` |
| **G3** | Pacifist telegraph fairness | 0 unavoidable hazard hits with perfect-information dodger | Archetype 5 run per new gimmick | `qa/gate-measurements.md#g3`, defects → `qa/defect-register.md` |
| **G7** | Loop period: cadence of gimmick-interaction loop (e.g. altar-class hold → buff → fight → return) | period 30–180 s, ≥3 actions/loop, ≥1 reward event/loop | Event-trace segmentation of kiter + skill-spammer runs (AltarBlessing/HazardPulse/EquipDropped timestamps) | `qa/gate-measurements.md#g7` (numeric model mirror: `design/core-loop.md`) |
| **G7** | Repeat-rate proxy ≥70% | testers voluntarily re-enter the loop (quick-retry usage / re-entry within session) | Structured playtest on deployed WebGL build, ≥5 sessions | `qa/playtest-report.md` + `qa/gate-measurements.md#g7` |
| **G8** | Impression score of each new gimmick | median ≥ 4/5 | Structured playtest scoring (same rubric as G4 immersion scoring), ≥5 scored sessions per gimmick | `qa/gate-measurements.md#g8` |
| **G8** | Novelty frequency cross-check | gimmick appears in ≤2 of ≥5 surveyed comparable titles | Designer frequency table (`design/trend-survey/solutions.md` §Frequency Ranking, 11-title pool ⊇ QA's 6); QA verifies the ≥5-title denominator and provenance labels before sign-off | `design/novelty-scorecard.md` + `qa/gate-measurements.md#g8` |

## D) Regression list (must-not-change; any diff = S1, blocks all gates)

| # | Surface | Check | Baseline source |
|---|---|---|---|
| R1 | Existing 6 catalog stages | Kiter golden digest per stage id (cinder-span, ember-gallery, abyss-chancel, witness-well, echo-throne, ash-verdict) @ 2/1/3, 1800 ticks — digests byte-identical to pre-cycle-2 goldens | Golden values recorded from HEAD before first cycle-2 sim change; committed in test source |
| R2 | Arena mode | `GameMode.Arena` frozen-run digest unchanged (existing `HackSimTests` arena regression, 1800 ticks) | Existing test, must stay green untouched |
| R3 | Prologue | Prologue pilot still clears wave 3 with `Digest.Reason == "prologue-clear"` | Existing `HackSimTests` prologue test |
| R4 | Existing gimmick constants | ember-vent {r90, 2.4 s, 0.8 s, dmg 8} · pillar {r40} · altar {r70, 1.2 s, +18, 6 s} and the 6 existing placement tables unchanged | SIM_SPEC_CAMPAIGN §Dungeon gimmicks + StageCatalog hazard tables |
| R5 | EditMode suite | Full suite green (cycle-1 close: 76/76; count only grows) | Unity EditMode run, results XML archived |
| R6 | Companion invariants | Hold/recall near new gimmicks: command sequences produce identical snapshots+digests; commands inert where spec says inert | Existing HackSimTests companion suite extended, not modified |
| R7 | Reduced-motion invariance | ReducedMotion on/off digest equality (D4) also run on one EXISTING stage (abyss-chancel) | New test, guards old content |
| R8 | Campaign progress key | localStorage key `abyssal-lantern:unity:campaign` schema unchanged (`cleared[]`, `equipment{}`) — new stage ids append to `cleared`, never rename | SIM_SPEC_CAMPAIGN §Page flow; renaming orphans every player's save |

## E) Evidence routing (single source: qa/gate-measurements.md)

- `qa/gate-measurements.md#g2` — clear-rate matrix, TTK table, hazard-damage share, single-hit audit (+ commands/tick counts per row)
- `qa/gate-measurements.md#g3` — viability verdict per archetype, dominance ratio, pacifist fairness
- `qa/gate-measurements.md#g7` — loop period/actions/reward measurements; repeat-rate proxy
- `qa/gate-measurements.md#g8` — impression scores; frequency-table verification note
- `qa/playtest-report.md` — per-archetype session table (G3/G7 raw)
- `qa/defect-register.md` — S1–S4 lifecycle; unavoidable-hit and digest-diff findings
- `qa/regression-matrix.md` — R1–R8 status per build
- `qa/exploit-register.md` — any gimmick-abuse loop found during rotation (broadcast to all agents with feedback request, per harness discipline)
- Unity EditMode results XML — archived beside the run (`test-results-*.xml`, same convention as cycle 1) and referenced by path from each measurement row
- Director verdicts consume ONLY these paths (`production/gate-reviews/{stage}-{gate}.md`)

## Open dependencies

- New stage ids + gimmick specs: blocked on designer Stage 1 output (`design/` lane); this
  plan's checks are parameterized by stage id and gimmick kind, so they bind the day specs land.
- `design/balance-sheet.md` TTK targets must exist before G2 TTK rows can be measured
  (sheet audit is itself a G2 requirement).
- Golden digests (R1) must be recorded BEFORE the first cycle-2 sim-touching commit —
  flag to programmer lane at Stage 1 close.
