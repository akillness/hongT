# Architecture Contract — five-cycle Achilles-quality program

## Program identity

- `[OBSERVED]` Active run-id: `20260808-achilles-quality`.
- `[OBSERVED]` Public beat: **NAN 2026 final submission**.
- `[OBSERVED]` Cycle 9 re-enters Stage 2 Phase 2a; it does not reopen concept work, and G8 impression score is the entry blocker (`production/task-manifest.md`, `retrospectives/cycle-8-retrospective.md`).
- `[TARGET]` Cycles 10–13 may use the captured Achilles: Legends Untold Steam page only to calibrate action-RPG play concepts: committed strikes, readable evade/guard response, coordinated enemies, named boss phases, and a climactic finish.
- `[TARGET]` Greek mythology, names, story, art, layout, assets, and progression are excluded; every shipped string/effect/scenario remains traceable to HongT Cinder Court's court-room/lantern worldview and generated-asset provenance.

## Immutable boundary

- `[OBSERVED]` `Assets/Scripts/Sim/CinderCourt.Sim.asmdef` has `noEngineReferences: true`; deterministic simulation remains pure C#.
- `[OBSERVED]` `Assets/Scripts/Sim/SimTypes.cs` declares itself a FROZEN CONTRACT and exposes the fixed-step snapshot/input/event interfaces.
- `[OBSERVED]` `CinderSim.Tick(in SimInput)` advances the simulation at `SimConfig.FixedStep`; presentation consumes snapshots and `SimEvents` after the tick batch in `GameView.Update`/`DispatchEvents`.
- `[TARGET]` Do not change `SimTypes.cs`, existing `ICinderSim`/snapshot members, `SimInput`, `SimEvents`, fixed-step ordering, or serialized campaign fields merely to improve presentation.
- `[TARGET]` A sim change is admissible only when QA proves that a view/data-owned slice cannot satisfy the gate, the deterministic digest is deliberately repinned, both sim and view callers migrate in one cutover, and the frozen spec is amended in the same approved change.
- `[TARGET]` No Unity API, wall clock, browser API, random source, or asset lookup may enter `CinderCourt.Sim`.

## Existing seams that must survive

| Seam | Current source evidence | Contract for cycles 9–13 |
|---|---|---|
| Deterministic event fan-out | `[OBSERVED]` `GameView.DispatchEvents` reads one `SimEvents` mask and routes impact, `CameraRig`, `VfxDirector`, `AudioDirector`, HUD, digest, and `OnRunEvents`. | `[TARGET]` Add or retune presentation by consuming existing events/snapshots here; do not make presentation gate simulation. |
| Impact hierarchy | `[OBSERVED]` `ImpactBudget.Resolve` merges Finisher > Kill > Light, uses a light refractory, preserves a stronger live stop, and lets `ViewPrefs.TimeEffectsAllowed` suppress the time channel. | `[TARGET]` Keep `ImpactBudget` the sole table for hit-stop/punch arbitration; no second hit-feel constants in `GameView`, `CameraRig`, or `ActorView`. |
| Accessibility/preferences | `[OBSERVED]` `ViewPrefs` owns reduced motion and difficulty persistence; `GameView`, `CameraRig`, and VFX paths already read it. | `[TARGET]` Every new shake, flourish, afterimage, slow-motion, or flash path must reuse the appropriate `ViewPrefs` gate and preserve a readable non-motion channel. |
| Enemy coordination | `[OBSERVED]` `DifficultySpec.For` owns incoming damage, cooldown, attack-token, group-AI, ring-radius, and flank-bias data; `CinderSim.PlanEnemyGroup` deterministically grants tokens with distance plus id tie-breaks and no RNG. | `[TARGET]` First retune only `DifficultyProfile` data and existing planner comparisons; no behaviour-tree/runtime package and no per-tick collection allocation. |
| Growth truth | `[OBSERVED]` `ProgressionGuide` derives lock reason, next target, prices, mastery, and hazards from `CampaignData`, `StageCatalog`, and existing spec tables without owning a rule. | `[TARGET]` All player-facing growth guidance must read this seam; no duplicate tier, cost, unlock, or hazard table in a screen. |
| Stage/boss presentation | `[OBSERVED]` `StageCatalog.StageEntry` owns logical stage presentation data; `GameBootstrap.BossArchetypePrefab` resolves Warden/Tactician/Sovereign resource prefabs; `GameView.RentBoss` creates one archetype boss per run and falls back to the ordinary visual. | `[TARGET]` Additive presentation reads `StageCatalog`, `BossArchetype`, `BossPhase`, and existing events; asset absence must retain the current fallback. |
| Performance degradation | `[OBSERVED]` `PostFxGate` watches actual frame deltas in a fixed 120-sample ring and disables bloom/vignette for the session after its trip condition; its source explicitly says this is not a substitute for real p95 measurement. | `[TARGET]` Preserve one-way degradation and collect independent G6 measurements in `engineering/perf-budget.md`. |
| Fixed-cap presentation storage | `[OBSERVED]` enemy views are pooled; damage numbers use 16 slots; VFX uses fixed burst/spark/warning/shard arrays; enemy attack grants use the preallocated `_mayAttack` array. | `[TARGET]` New combat feedback must reuse these pools or declare a fixed cap and eviction rule; steady-state combat must not allocate. |

## Equipment and sigil owner decision

### Survivor

- `[TARGET]` **`LobbyView` is the sole surviving equipment/sigil screen and mutation owner.**
- `[OBSERVED]` `LobbyView.BuildEquipTab` and `BuildSigilTab` are the reachable Sanctum purchase/equip surfaces and already raise `LobbyCallbacks.OnBuyEquip`, `OnBuySigil`, and `OnEquipSigil` to `GameDirector`.
- `[OBSERVED]` `MetaScreenView.BuildEquipTab` and `BuildSigilTab` are read-only replicas; their own equipment footnote sends purchasing back to the Lobby Sanctum, while their labels depend on `LobbyView` vocabulary.
- `[INFERENCE]` Keeping `LobbyView` avoids inventing a second mutation callback contract and is the smaller, lower-risk cutover; keeping `MetaScreenView` instead would require migrating every purchase/equip interaction before deleting the already-reachable path.

### Clean migration

1. `[TARGET]` In `LobbyView.BuildMapPanel`, route the `MetaScreenButton`/“정비” action to `SelectRail(RailSanctum)` and `SelectTab(1)` rather than `MetaScreenView.TabEquip`.
2. `[TARGET]` In `LobbyView.OpenMetaScreen`, route the compatibility entry to the same Sanctum equipment tab; remove the misleading meta-screen name in the same cutover rather than leaving an alias.
3. `[TARGET]` In `MetaScreenView`, delete equipment/sigil fields, builders, refreshers, selectors, tab constants, and `LobbyView` vocabulary dependencies; retain only map and controls responsibilities.
4. `[TARGET]` In `MetaScreenView.Build`, make rebuilding idempotent by destroying the prior `_root` before creating another canvas, or prove the component is strictly single-build and replace the method with an enforceable guard; the current comment clears only `_contentFrames` and does not remove the old canvas.
5. `[TARGET]` Update `MetaScreenLayoutTests`, `LobbyLayoutTests`, and `ProgressionNavigationTests` in the same change; preserve `LobbyEconomyTests` and `SigilTests` coverage on the surviving callback path.
6. `[TARGET]` Browser verification must prove one equipment surface, one sigil surface, one click path to each purchase/equip action, persistence after refresh/reload, and full containment at the 375×667 support floor plus the existing phone/letterbox/editor matrix.

## Five-cycle source boundary

| Cycle | Smallest admissible source boundary | Exit evidence |
|---|---|---|
| 9 | `[TARGET]` Resolve Lobby/Meta ownership, remove the duplicate read-only implementation, make the retained meta build idempotent, measure G8/G6, and produce the joined five-pilot G5 evidence rows by deterministic test/session export outside frozen sim contracts before any combat retune. | `[TARGET]` One owner/caller map; updated focused UI tests; cycle-9 Unity/WebGL evidence; G8 measured; G5 rows include stage, spend reachability, pre/post tiers and relics, run/first-clear/pact components, rank transitions, purchases, paid guards, and peril activations. |
| 10 | `[TARGET]` If G8/action-feel evidence is below band, tune existing `ImpactBudget`, event fan-out, and measured animation clip trims only; preserve sim interfaces. | `[TARGET]` Contact-frame evidence, focused impact/animation tests, input/effect latency ≤100ms, impression score ≥4/5. |
| 11 | `[TARGET]` If coordination evidence is outside G2/G3/G7 bands, retune the existing `DifficultySpec` group data and deterministic `PlanEnemyGroup`; clarify growth only through `ProgressionGuide` plus the surviving Lobby screen. | `[TARGET]` Deterministic group-AI tests/digests, ≥5 archetypes, distinct strategies, matchup/TTK/loop evidence, no duplicated growth rule. |
| 12 | `[TARGET]` Improve boss phase/finish presentation only through `StageCatalog`, existing archetype resources, `BossPhase` snapshot, `BossPhase2` transition event, and current camera/audio/VFX/HUD seams. | `[TARGET]` Three named boss fixtures, fallback fixture, phase-boundary feedback, reduced-motion fixture, browser readability/immersion evidence. |
| 13 | `[TARGET]` Fix only measured stability/performance defects; add production/runtime emission for the joined QA fields only if the ops telemetry contract still requires it after the cycle-9 external evidence export, and keep it outside frozen sim contracts and the player hot path. | `[TARGET]` p95 frame ≤16.7ms, long-frame <0.5%, stable 30-minute memory soak, input ≤100ms, complete telemetry/release evidence. |

## Defect dispositions

| ID | Finding | Recommendation | Reason |
|---|---|---|---|
| ENG-01 | `[OBSERVED]` Equipment/sigil presentation exists in both `LobbyView` and `MetaScreenView`, with only Lobby mutable. | `[TARGET] FIX in cycle 9` by the clean Lobby-owner cutover above. | `[OBSERVED]` Cycle-8 D-6 expires in cycle 9; each data/string change otherwise has two presentation surfaces and two G1/G4 audit paths. |
| ENG-02 | `[OBSERVED]` `MetaScreenView.Build` can create a new root after merely clearing its rect list. | `[TARGET] FIX with ENG-01` using idempotent teardown or an enforceable single-build guard. | `[INFERENCE]` Repeated build can retain an abandoned canvas/object graph; the fix is local and testable. |
| ENG-03 | `[OBSERVED]` G8 impression score and current-cycle contact/latency evidence are absent. | `[TARGET] DEFER combat constant changes until QA measures the cycle-9 baseline.` | `[OBSERVED]` The retrospective makes G8 a Stage-2 entry condition; tuning first would erase the baseline and violate measurement-led retune. |
| ENG-04 | `[OBSERVED]` Normal difficulty has unlimited attacks while Hard/Nightmare use deterministic tokens/rings/flank bias. | `[TARGET] DEFER any coordination retune until ≥5-archetype evidence identifies a band failure.` | `[OBSERVED]` `DifficultySpec` already exposes the intended data seam; no source defect is proven yet. |
| ENG-05 | `[OBSERVED]` Both boss phase boundaries reuse frozen `SimEvents.BossPhase2`; the view must read `BossPhase` to distinguish them. | `[TARGET] DEFER a new event; consume the existing event plus snapshot in cycle 12.` | `[OBSERVED]` `CinderSim.UpdateBossPhase` records this as deliberate compatibility, so a new event would change the frozen surface unnecessarily. |
| ENG-06 | `[OBSERVED]` Archetype bosses are intentionally unpooled and destroyed after one boss per run. | `[TARGET] DEFER pooling unless the 30-minute soak proves retained growth or spawn hitch.` | `[OBSERVED]` `GameView.RentBoss` documents the cross-archetype contamination risk of pooling; no measured defect exists. |
| ENG-07 | `[OBSERVED]` Current storage emits the last-run digest and campaign state, not joined QA rows for stage, spend reachability, pre/post tiers/relics, reward components, transitions, purchases, paid guards, and peril activations. | `[TARGET] FIX the cycle-9 G5 evidence gap now with deterministic test/session export outside frozen sim contracts; DEFER only production/runtime emission to cycle 13 if ops still requires it.` | `[OBSERVED]` The PM/QA evidence contract cannot be reconstructed from existing endpoints, and five joined pilot routes are a cycle-9 prerequisite rather than a cycle-13 follow-up. |
| ENG-08 | `[OBSERVED]` Cycle-8 reports a green suite/build/browser result, but the active five-cycle run has no p95/input/30-minute-soak measurement packet. | `[TARGET] FIX the evidence gap before a cycle-13 G6 PASS; do not infer performance from EditMode success or build success.` | `[OBSERVED]` G6 requires measured values, methods, and paths; `PostFxGate` is only a runtime degradation guard. |

## Gate rules

- `[TARGET]` G6 performance gates are explicit and non-negotiable: p95 frame time ≤16.7ms, long frames <0.5%, memory stable over a 30-minute soak, and input-to-feedback ≤100ms.
- `[TARGET]` Every gate claim records measured value, measurement method, and evidence path; a missing triple is FAIL, not “carried.”
- `[TARGET]` Any open S1 defect blocks every gate; two failed FIX loops force a director scope decision before a third attempt.
