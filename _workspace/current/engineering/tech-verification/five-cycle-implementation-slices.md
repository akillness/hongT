# Five-Cycle Implementation Slices — cycles 9–13

## Scope and sequencing

- `[OBSERVED]` Active run-id: `20260808-achilles-quality`; public beat: **NAN 2026 final submission**.
- `[OBSERVED]` Cycle 9 is Stage 2 Phase 2a re-entry with G8 impression score missing and G5 joined-pilot evidence missing; it is not a concept restart.
- `[TARGET]` No runtime code is changed in this audit pass.
- `[TARGET]` Cycle 9 makes no mechanic/balance change; it may remove the expired duplicate UI implementation and add editor/test evidence export outside the frozen simulation.
- `[TARGET]` Cycles 10–13 implement only a slice whose prerequisite measurement is outside its gate; a passing baseline yields “no source change,” not a ceremonial retune.
- `[TARGET]` Achilles calibrates action concepts only; HongT's worldview, deterministic 1/60 simulation, stage identities, assets, and progression remain original.

## Slice 9A — one equipment/sigil owner

**Disposition: `[TARGET] FIX in cycle 9`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[TARGET]` `Assets/Scripts/View/LobbyView.cs`: `BuildMapPanel`, `OpenMetaScreen`, `SelectRail`, `SelectTab`, `BuildEquipTab`, `BuildSigilTab`, `Refresh`; `Assets/Scripts/View/MetaScreenView.cs`: `Build`, `BuildEquipTab`, `RefreshEquipDetail`, `BuildSigilTab`, `RefreshSigilDetail`, tab constants/fields; `Assets/Scripts/View/GameDirector.cs`: existing `LobbyCallbacks` wiring remains unchanged. |
| Current behaviour | `[OBSERVED]` Lobby Sanctum is the mutable equipment/sigil surface and raises buy/equip callbacks to `GameDirector`; Meta has read-only equipment/sigil replicas and sends purchasing back to Sanctum. |
| Proposed behaviour | `[TARGET]` `LobbyView` survives as sole equipment/sigil presentation and mutation owner. “정비” and the public navigation entry open `RailSanctum` + equipment tab. `MetaScreenView` retains map/controls only; all duplicated equipment/sigil builders, fields, refreshers, vocabulary dependencies, and tabs are deleted in one cutover. |
| Allocation/perf risk | `[INFERENCE]` Net UI object count and refresh work decrease. The cutover must also make `MetaScreenView.Build` idempotent by removing a previous `_root` before rebuilding or enforcing one build; otherwise repeated attach/build can retain a canvas graph. |
| Test surface | `[TARGET]` Update `Assets/Tests/EditMode/MetaScreenLayoutTests.cs`, `LobbyLayoutTests.cs`, `ProgressionNavigationTests.cs`; retain `LobbyEconomyTests.cs` and `SigilTests.cs` as mutation truth. Add an assertion that one build/rebuild leaves one meta root and exactly one equipment/sigil surface. |
| Unity verification | `[TARGET]` Build Lobby, click rail/map/정비 paths, buy every equipment slot through T5 boundary, unlock/equip/flip/unequip sigils, refresh/re-enter, and prove one callback per click plus persistence. |
| Browser verification | `[TARGET]` At 375×667 and existing phone/letterbox/editor fixtures, prove the selected Sanctum panel is fully contained, purchase controls are reachable, meta map/controls still open/close, no click falls through, and reload preserves the mutation. |

## Slice 9B — joined G5 evidence export

**Disposition: `[TARGET] FIX in cycle 9; production telemetry may be deferred, evidence may not`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[TARGET]` New editor-only `Assets/Editor/FiveCycleEvidenceRecorder.cs`, patterned after observed `GoldenDigestRecorder.Record`; read existing `CinderSim`, `CampaignData`, `CampaignStore` pure mutation helpers, `ProgressionGuide`, and QA-owned deterministic route fixtures without changing `SimTypes.cs`. Optional focused schema test: `Assets/Tests/EditMode/FiveCycleEvidenceRecorderTests.cs`. |
| Current behaviour | `[OBSERVED]` `WebGLStorage.WriteRunDigest` emits last-run route/score digest and `CampaignStore.Save` emits current campaign state; neither reconstructs joined per-pilot before/after rows. |
| Proposed behaviour | `[TARGET]` An editor/test export runs QA's five pilots × routes A–E and writes one joined local row per required observation: stage, spend reachability, pre/post tiers and relics, run/first-clear/pact reward components, rank transitions, purchases, paid guards, and peril activations. The export includes run-id, route/pilot id, deterministic digest, and source revision/build identity supplied by the harness. |
| Allocation/perf risk | `[OBSERVED]` The proposed exporter is Editor-only and outside released WebGL/player frames. `[TARGET]` Do not add per-tick JSON/string writes to `CinderSim` or `GameView`. |
| Test surface | `[TARGET]` Assert required columns, five pilots × routes A–E coverage, stable row ordering, valid before/after joins, and digest reproducibility. Missing route or field fails the export. |
| Unity verification | `[TARGET]` Run the focused exporter/test in batchmode and record command, row count, digest set, and artifact path; QA/PM validate the joined rows and negotiation signatures. |
| Browser verification | `[TARGET]` None required for generating deterministic joined rows; separately spot-check that one recorded purchase/reward route matches released WebGL labels and persisted state. |

## Slice 10A — measured action-contact alignment

**Disposition: `[TARGET] DEFER until cycle-9 G8/contact/input baseline; implement only if below band`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[TARGET]` `Assets/Scripts/View/ImpactBudget.cs`: existing constants and `Resolve`; `Assets/Scripts/View/GameView.cs`: `DispatchEvents`; `Assets/Editor/CharacterImportPipeline.cs`: `Clips`, `ClipTrims`, `ReimportClips`; `Assets/Scripts/View/ActorView.cs`: existing action/flash application; `CameraRig.OnEvents/Flourish`, `AudioDirector.OnEvents`, and `VfxDirector.OnEvents` remain shared consumers. |
| Current behaviour | `[OBSERVED]` Light/Kill/Finisher impact tiers are mutually ordered, light feedback has a 0.14s refractory, stronger live hit-stop is never shortened, and reduced motion suppresses the time channel. The base `attack` import is trimmed to frames 16–28 so contact lies inside the sim active window; `attack2`/`attack3` currently have no `ClipTrims` row. |
| Proposed behaviour | `[TARGET]` First measure each chain contact frame against `SimEvents.EnemyHit`. If combo clips miss contact, append measured trim rows without reordering `Clips`. If impact scoring is below G8/G4, change one `ImpactBudget` tier/refractory value at a time within the approved presentation cap and keep event fan-out single-sourced. No new sim event or attack rule is part of this slice. |
| Allocation/perf risk | `[OBSERVED]` `ImpactPulse` is a value type and current audio/VFX/number feedback is pooled. `[TARGET]` No per-hit instantiate, material clone, LINQ, string formatting, or temporary collection. Importer trim changes have no player-frame allocation. |
| Test surface | `[TARGET]` `ImpactBudgetTests.cs`, `ClipTableTests.cs`, `CharacterRosterAnimationTests.cs`, `CameraFlourishTests.cs`, `PresentationFeedbackTests.cs`; add contact-order assertions for every edited clip/tier and preserve reduced-motion assertions. |
| Unity verification | `[TARGET]` Record attack1/2/3 against one survivor, one kill, a crowd, and a combo finisher at fixed 60Hz; report contact-frame offset, tier selected, refractory behaviour, and deterministic digest unchanged. |
| Browser verification | `[TARGET]` Released WebGL keyboard and touch trials report input-to-first feedback ≤100ms, no camera buzz under crowd overlap, no frozen clock after interruption/end, and G8 impression median ≥4/5 across N≥5. |

## Slice 10B — Witness Guard proposal boundary

**Disposition: `[TARGET] DEFER; not part of the default source plan`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[OBSERVED]` Existing readable responses are `CinderSim.CastDash` → `ActorAction.Avoid`/`SimEvents.DashUsed` and `CinderSim.CastVoidAegis` → `ActorAction.Defence`/`SimEvents.WardCast`; `CharacterImportPipeline.Clips` already maps `avoid` and `defence`. |
| Current behaviour | `[OBSERVED]` HongT already has dodge and ward/block presentation; there is no observed `Witness Guard` rule on the frozen input/event surface. |
| Proposed behaviour | `[TARGET]` Do not implement Witness Guard from the Achilles benchmark. It remains a HongT-native design proposal for cycle 10 only after a director-approved Stage-1 amendment, exact deterministic rule/spec, a readable enemy cue of at least 0.30s, and digest/test acceptance. |
| Allocation/perf risk | `[INFERENCE]` A new combat rule would add state/update branches and presentation, but its cost is unknown because no approved rule exists. |
| Test surface | `[TARGET]` If approved, the amendment must name all sim branches, edge cases, digest fixtures, cue latency, reduced-motion presentation, and migration of both sim/view callers before code starts. |
| Unity/browser verification | `[TARGET]` No verification is claimable until the proposal is approved and implemented; current dash/ward paths remain the baseline. |

## Slice 11A — deterministic enemy coordination retune

**Disposition: `[TARGET] DEFER until ≥5-archetype G2/G3/G7 evidence proves a failure`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[TARGET]` `Assets/Scripts/Sim/DifficultySpec.cs`: `DifficultyProfile`, `For`, `RingTarget`, `RingSlotOf`; `Assets/Scripts/Sim/CinderSim.cs`: `PlanEnemyGroup`, `MayAttackThisTick`, existing enemy update only; `Assets/Tests/EditMode/DifficultyGroupAiTests.cs`, `DungeonGoldenDigestTests.cs`, relevant `HackSimTests.cs`. |
| Current behaviour | `[OBSERVED]` Story uses two attack tokens without group AI, Normal preserves unlimited pre-amendment attacks, Hard uses three tokens/group AI/ring 1.55/flank 0.75, and Nightmare uses four tokens/group AI/ring 1.35/flank 0.75. `PlanEnemyGroup` is deterministic, bosses bypass tokens, live swings hold tokens, and off-cooldown candidates are ranked by distance/flank bias with id tie-break. |
| Proposed behaviour | `[TARGET]` Retune only an evidenced `DifficultyProfile` field first: token count, cooldown multiplier, ring radius, or flank bias. Change planner code only if a deterministic fixture proves the current selection rule—not its numbers—causes the defect. Preserve boss bypass, fixed scan order, id tie-break, no RNG, and `SimConfig.EnemyCap`. |
| Allocation/perf risk | `[OBSERVED]` `_mayAttack` is preallocated to enemy cap; planning performs bounded nested scans. `[TARGET]` No per-tick resize under supported cap, list/sort/LINQ/delegate allocation, navigation package, or physics query. |
| Test surface | `[TARGET]` Assert token ceilings, boss bypass, held-token rotation, flank preference, stable id tie, story/normal compatibility, maximum-cap scan, and golden digest. QA supplies ≥5 archetypes, 45–55% matchup band, ±15% TTK, distinct strategies, and no >50% dominance. |
| Unity verification | `[TARGET]` Deterministic fixtures replay identical digest and grant order; Game View shows no orbit deadlock, attack pile-up, or unreadable simultaneous commitment. |
| Browser verification | `[TARGET]` Released WebGL at all four difficulty ids demonstrates readable approach/commit/recovery and maintains frame/input gates in the maximum-enemy route. |

## Slice 11B — growth clarity through one truth seam

**Disposition: `[TARGET] FIX duplicate presentation in 9A; DEFER additional copy/layout changes until five-pilot evidence identifies a misunderstanding`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[TARGET]` `Assets/Scripts/View/ProgressionGuide.cs`: `NextTarget`, `LockReasonFor`, `StageSubLine`, price/cap/order constants; `Assets/Scripts/View/LobbyView.cs`: `Refresh`, `TargetSuffix`, surviving `BuildEquipTab`/`BuildSigilTab`; `GameDirector.TryBuyEquip` and existing sigil mutations remain rule execution. |
| Current behaviour | `[OBSERVED]` `ProgressionGuide` is a pure derived-state seam over campaign/stage/spec data and owns no second rule; Lobby currently reads it, while Meta duplicates read-only equipment/sigil details. |
| Proposed behaviour | `[TARGET]` After 9A, display next target, lock reason, current→next tier, exact relic cost, and post-purchase delta only on the surviving Sanctum path, reading `ProgressionGuide`/sim-derived values. Do not add a second cost, cap, unlock, sigil order, hazard, or mastery table. If pilots already understand the path, make no further source change. |
| Allocation/perf risk | `[OBSERVED]` Lobby refresh is transition-driven and built once. `[TARGET]` Continue assigning existing `Text`/button state only; do not rebuild canvases or allocate per frame. |
| Test surface | `[TARGET]` `ProgressionNavigationTests.cs` exhaustive save/stage sweeps, `LobbyEconomyTests.cs`, `SigilTests.cs`, and `LobbyLayoutTests.cs` support-floor interaction/containment. Add before/after purchase label assertions only for changed copy. |
| Unity verification | `[TARGET]` From blank, mid, and capped saves, verify next action, exact price, disabled reason, purchase debit/tier transition, and persistence without an external document. |
| Browser verification | `[TARGET]` Five pilots × routes A–E produce joined spend-reachability/pre-post rows; every control remains reachable at 375×667 and after reload. G5 stays FAIL until PM signatures and joined evidence exist. |

## Slice 12A — phase-specific boss presentation and finish

**Disposition: `[TARGET] DEFER until cycle-12 Stage/G4/G8 evidence; no new sim interface`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[TARGET]` `Assets/Scripts/View/StageCatalog.cs`: `StageEntry`, `BossPresentation`; `GameBootstrap.BossArchetypePrefab`; `GameView.RentBoss`, `ApplyBossPresentation`, `DispatchEvents`; `HudView.ShowBossIntro`, boss-phase sync, `ShowStageClear`; `VfxDirector.OnEvents`; `CameraRig.OnEvents/Flourish`; `AudioDirector.OnEvents`. Read existing `IHackSnapshot.BossPhase` on transition. |
| Current behaviour | `[OBSERVED]` Warden/Tactician/Sovereign resolve dedicated prefabs with generic fallback. Both phase boundaries raise the frozen `BossPhase2` transition bit; HUD reads 1-based `BossPhase`, VFX finds the live boss and emits one red burst, camera applies one bounded flourish/shake, and audio plays one low-menace cue. Stage clear already writes digest, layers wave/pickup audio, shows completion story, and shows the final-stage clear ceremony. |
| Proposed behaviour | `[TARGET]` On the existing transition bit, read current `BossPhase` and archetype/stage presentation to select phase-specific HongT-native HUD text/tint and existing pooled burst parameters; keep camera/audio within current bounded APIs unless scoring proves one channel insufficient. At boss/final clear, reuse existing StageCleared/ComboFinisher paths for a climactic but non-gating finish. Never add a phase event merely to distinguish P2/P3. |
| Allocation/perf risk | `[OBSERVED]` One boss is unpooled per run; ordinary actors and bursts are pooled. `[TARGET]` Resolve renderer/property blocks at rent or transition, not per frame; no material clone, `Resources.Load`, renderer discovery, or new object creation on each phase tick. |
| Test surface | `[TARGET]` `BossVarietyTests.cs`, `PresentationFeedbackTests.cs`, `CameraFlourishTests.cs`, boss flash/yield tests, stage catalog tests, and deterministic digest. Add Warden/Tactician/Sovereign P2/P3 where applicable, generic fallback, reduced-motion, and interrupted-end fixtures. |
| Unity verification | `[TARGET]` Capture each archetype entrance, all supported phase boundaries, boss death/final clear, fallback prefab, and reduced motion; report HUD phase, asset id, cue, camera/VFX channel, and no gameplay pause/free hits. |
| Browser verification | `[TARGET]` At 375×667 and desktop, boss name/phase/health remain readable, transition feedback begins ≤100ms after the event, immersion median ≥4/5, G8 impression ≥4/5 where claimed, and frame/memory gates remain green. |

## Slice 13A — measured stability closure

**Disposition: `[TARGET] FIX only measured defects; DEFER speculative optimization`**

| Required facet | Contract |
|---|---|
| Exact files/symbols | `[TARGET]` `Assets/Scripts/View/PostFxGate.cs`: fixed window/degradation/debug line; `GameView` pools/reset/end-run paths; `VfxDirector.ClearTransient` plus pickup/hazard cleanup; `ActorView.OnDestroy`; surviving `MetaScreenView.Build`; `WebGLStorage`/`CampaignStore` transitions. Optional runtime evidence emission is additive to View/ops code only if `ops/telemetry-contract.md` requires it after cycle-9 external export. |
| Current behaviour | `[OBSERVED]` Fixed pools bound ordinary combat effects; post FX degrades one-way on its own window; boss and some transition resources instantiate/destroy; the active run lacks p95/input/30-minute memory evidence. |
| Proposed behaviour | `[TARGET]` Run the full budget first. Fix the first measured retention/hitch at its owner, preserving pool caps and fallbacks. Runtime evidence, if required, buffers event-level rows and writes at run/purchase/reward transitions—not ticks. No “cleanup” source change without a failing measurement. |
| Allocation/perf risk | `[TARGET]` Stable combat and soak loops have no unbounded collection growth or per-frame persistence; any new buffer has a fixed cap, explicit overflow rule, and reset point. |
| Test surface | `[TARGET]` `PostFxWatchdogTests.cs`, WebGL texture/build postprocess tests, repeated route/reset fixture, pool-cap/eviction assertions, persistence round-trip, and any focused regression for the measured owner. |
| Unity verification | `[TARGET]` Focused tests + deterministic digest + WebGL build record exact Unity version, result, warnings/errors, and size. |
| Browser verification | `[TARGET]` Released mixed-route packet proves p95 frame ≤16.7ms, long frames <0.5%, stable memory over 30 minutes, input ≤100ms, zero page/console errors, and tested rollback/release checklist. |

## Defect decision ledger

| Defect | Decision | Closure condition |
|---|---|---|
| Duplicate Lobby/Meta equipment+sigil | `[TARGET] FIX 9A` | `[TARGET]` One presentation/mutation owner and one browser-reachable path; duplicate code removed. |
| Repeated Meta build can retain root | `[TARGET] FIX 9A` | `[TARGET]` Rebuild leaves one root/canvas and soak shows no root growth. |
| G5 joined evidence unreconstructable from current endpoints | `[TARGET] FIX 9B now; DEFER only optional production emission` | `[TARGET]` Five pilots × routes A–E joined rows plus PM/designer signatures exist. |
| G8/action contact not measured | `[TARGET] DEFER 10A` | `[TARGET]` Baseline exists; implement only if below band, then median impression ≥4/5 and latency ≤100ms. |
| Witness Guard lacks approved deterministic rule | `[TARGET] DEFER 10B` | `[TARGET]` Director Stage-1 amendment, ≥0.30s cue, digest and both-lane migration plan exist. |
| Enemy coordination retune not proven necessary | `[TARGET] DEFER 11A` | `[TARGET]` ≥5-archetype evidence identifies exact failed field/branch; deterministic acceptance frozen before edit. |
| Growth copy/layout defect beyond duplication not measured | `[TARGET] DEFER 11B` | `[TARGET]` Joined pilot evidence names a misunderstanding/reachability failure; one derived truth seam remains. |
| Boss P2/P3 share one transition bit/cue | `[TARGET] DEFER new event; use snapshot in 12A` | `[TARGET]` Phase-specific view evidence passes without frozen interface change. |
| Boss unpooled | `[TARGET] DEFER` | `[TARGET]` Pool only if spawn/soak evidence proves hitch/retention and an archetype-safe key/reset is specified. |
| G6 p95/input/soak missing | `[TARGET] FIX measurement before any G6 PASS` | `[TARGET]` Measured value + method + evidence path for all four numeric rows. |

## Final verification order

1. `[TARGET]` Freeze the exact slice and prerequisite evidence; do not combine UI debt, balance tuning, and performance optimization in one revision loop.
2. `[TARGET]` Run focused tests for changed symbols, then deterministic digest when sim/data changed.
3. `[TARGET]` Exercise the changed route in Unity Game View with reduced motion off/on where presentation changed.
4. `[TARGET]` Build released WebGL and exercise keyboard/touch at the support floor plus desktop.
5. `[TARGET]` Record frame/input/memory/browser evidence in `engineering/perf-budget.md` and the cycle verification artifact.
6. `[TARGET]` QA independently verifies the values; a missing measurement or unresolved S1 remains FAIL.
