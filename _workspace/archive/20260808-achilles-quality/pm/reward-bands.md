# Reward Bands — cycle 9 Stage 2 retune

- `run-id: 20260808-achilles-quality` [OBSERVED]
- `public-beat: NAN 2026 final submission` [TARGET]
- `entry-state: Stage 2 / Phase 2a` [OBSERVED: `retrospectives/cycle-8-retrospective.md` §다음 사이클 진입 결정]
- `gate-status: FIX — T5 session-path telemetry and QA replay are not yet measured` [OBSERVED]

## Canonical G5 YAML

```yaml
run_id: 20260808-achilles-quality
build_model:
  monetization_mode: disabled
  real_money_purchase_path: false
  paid_power_shortcut: false
comeback:
  reversal_probability_max: 0.30
  activation_cap: "2 peril activations per dungeon run"
  cooldown_or_pity: "re-arm only after health recovers to >=50%; no simultaneous stacking; slot 0 wins"
  paths:
    free_milestone: "equipped sigil peril clause at <35% health"
    paid: disabled
steady:
  parity_sessions_band: [10, 20]
  parity_track: "weapon=T5 AND lantern=T5 AND cloak=T5"
  session_unit: "one eligible dungeon leg settled by clear or defeat after reachable-lobby proof"
  paid_shortcut: disabled
fairness:
  paid_free_winrate_delta_max_pp: 5
  current_build_identity_delta_pp: 0
  qa_measurement: pending
```

- The three harness thresholds are copied without override: reversal probability `<=0.30`, parity in `10–20` sessions, and paid/free win-rate delta `<=5%p`. [OBSERVED: `skill://game-studio-harness/references/quality-gates.md#g5`]
- The peril availability limits are existing signed values, not cycle-9 tuning: two activations per run, `35%` opening threshold, `50%` re-arm hysteresis, and no stacking. [OBSERVED: `pm/negotiation-record.md`, entry 8]
- `current_build_identity_delta_pp: 0` is an identity comparison, not a completed playtest: the paid state is disabled, so the paid cohort is the free cohort until a paid path exists. [INFERENCE]
- G5 does not pass from that identity alone; QA still has to audit that no paid power state can be constructed and replay the free session paths below. [TARGET]

## Submission-build economy boundary

| Surface | Submission-build rule | Status |
|---|---|---|
| Real-money offer, checkout, premium currency, paid continue | Must not be exposed or callable. | [TARGET] disabled |
| Equipment and sigils | Spend only earned relics; these are progression sinks, not revenue points. | [OBSERVED: `ProgressionGuide.EquipCosts`, `ProgressionGuide.SigilCost`] |
| Comeback power | Reachable only through play by owning/equipping a sigil; no purchase activation exists. | [OBSERVED: negotiation entry 8; paid path remains unverified by QA] |
| Future monetization | May be described only as disabled design space; enabling any point requires a new designer/PM negotiation and a new G5 measurement. | [TARGET] |

## T5 session-path measurement contract

### Problem and correction

Cycle 8 changed the deployed spend surface from `0%` to `100%` reachable, so a denominator that counted sessions while the player could not reach equipment spending is no longer comparable. [OBSERVED: `retrospectives/cycle-8-retrospective.md`, G4 and D-9]

Cycle 9 therefore measures progression from live state transitions, not `required relics / average relics`. [TARGET] The quotient model is invalid because in-run equipment drops change the tier state directly and can replace a `2`-relic or `16`-relic purchase depending on the tier reached. [OBSERVED: `ProgressionGuide.EquipCosts`; `CinderSim.CollectPickup`; `qa/test-plan.md` T5.5b]

### Eligible session denominator

A progression session is counted only when all conditions below hold. [TARGET]

1. The measured path has proven `PrologueDone=true` and a reachable SANCTUM/equipment spend surface before its first counted leg. [TARGET]
2. The leg mode is Dungeon, not Arena, Prologue, or Training. [TARGET]
3. The leg settles by `stage-clear` or defeat. Record whether it returns to the lobby or continues through Ember Rest; both are counted, but only the lobby return opens a spend opportunity. [TARGET]
4. A manual abandon is excluded from the canonical replay because it forfeits all run earnings and performs no campaign save. [OBSERVED: `GameDirector` abandonment contract]
5. A defeat remains included because its `sim.Relics` are banked, even though equipment keeps its pre-run baseline. [OBSERVED: `GameDirector.OnRunEvents` GameOver branch]

`session_index=1` is the first qualifying dungeon leg after the reachable-lobby precondition is proven. [TARGET] Prologue time, menu-only visits, training trials, and unreachable pre-cycle-8 legs are not placed in this denominator. [TARGET]

### State recurrence

For eligible session `s`, record the pre-run state

`S_s = (relics, weaponTier, lanternTier, cloakTier, clearedMask, sigilsOwned)`.
[TARGET]

Apply the shipped state transitions in this order. [TARGET]

1. **Run reward:** `run_relics = RunDigest.Relics`. It already includes collected relic motes and duplicate-extraction payouts; do not add those again. [OBSERVED: `CinderSim.CollectPickup`, `CinderSim.CompleteExtraction`, `RunDigest.Relics`]
2. **Settlement multiplier:** on a clear, add `run_relics * (pact ? 2 : 1)`; on defeat, add `run_relics`; a first-clear bonus is never multiplied. [OBSERVED: `GameDirector.PersistDungeonClear`, `PactRelicMultiplier=2`, GameOver branch]
3. **One-time additions:** add only the live reward that actually fired: `+6/+8/+10` for first clears of cinder-sluice/ember-bastion/ash-march, or `+2` for training mastery outside the baseline route. [OBSERVED: `GameDirector.FirstClearRelicBonus`; `HackSpec.TrainingMasteryRelics`]
4. **Direct rank progression:** on clear, update each equipment tier to `max(preRunTier, ICampaignSnapshot finalTier)`; on defeat, do not bank rank steps. [OBSERVED: `GameDirector.PersistDungeonClear` and GameOver branch]
5. **Lobby spend policy:** when the leg returns to a reachable lobby, repeatedly buy the cheapest affordable next equipment step, tie-breaking `weapon -> lantern -> cloak`, until no equipment step is affordable. When Ember Rest continues directly, defer all purchases and carry the balance/tiers into the next leg. Read the next cost from `ProgressionGuide.EquipCosts[currentTier]`; never inline the ladder in the measurement tool. [TARGET]
6. **Track isolation:** spend `0` relics on sigils during the canonical T5 track. Record any actual sigil spend as a protocol deviation rather than silently subtracting it. [TARGET]
7. Set `S_(s+1)` to the resulting persisted state. `N_T5` is the smallest eligible `s` for which all three tiers equal `ProgressionGuide.EquipCap`. [TARGET]

The recurrence is computable without an estimated exchange rate: prices, tier cap, run relics, first-clear status, pact state, final run tiers, and persisted relic balance all come from shipped authorities. [OBSERVED]

The live price ladder is `2/4/7/11/16`, and the three first-clear grants are `6/8/10`. [OBSERVED: parsed from `ProgressionGuide.EquipCosts` and `GameDirector.FirstClearRelicBonus`] One slot therefore costs `40`, three slots cost `120`, first-clear grants total `24`, and the no-rank-drop purchase remainder after those grants is `96`. [INFERENCE: arithmetic] This `96` is an audit invariant, not the session answer, because each recorded rank transition removes its actual next-step purchase from the remaining path. [TARGET]

### Deterministic route matrix

| Axis | Required paths | Purpose |
|---|---|---|
| Pilot | melee-rusher, kiter, skill-spammer, companion-commander, pacifist-dodger | [TARGET] Preserve the existing five-archetype progression comparison. |
| Stage choice before nine clears | `ProgressionGuide.NextTarget`; retry the same uncleared stage after defeat. | [TARGET] Follow the build's enforced progression order. |
| Stage choice after nine clears and before T5 | Replay the nine `StageCatalog` entries in catalog order. | [TARGET] Avoid selecting a favorable farm stage by hand. |
| Route A | Return to the lobby after every settled leg; pact off; no mastery reward. | [TARGET] Maximum spend-opportunity cadence. |
| Route B | Continue every available Ember Rest successor; pact off; no mastery reward. | [TARGET] Minimum spend-opportunity cadence under the shipped chain. |
| Route C | Route A with pact on for every eligible cleared-stage replay. | [TARGET] Bound the signed free `x2` accelerator. |
| Route D | Route B with pact on for every eligible cleared-stage replay. | [TARGET] Combine chain cadence and the free accelerator. |
| Route E | Route A with the one-time `+2` mastery reward recorded at its actual occurrence. | [TARGET] Isolate the signed out-of-run addition. |
| Repeat count | Continue until T5 or session 21; report the exact first T5 session or `>20`. | [TARGET] Detect both early and late band exits. |

Per pilot, report `N_T5` for routes A–E, plus minimum, median, and maximum. [TARGET] A route is inside the G5 band only when `10 <= N_T5 <= 20`; `N_T5 < 10`, `N_T5 > 20`, or no T5 by session 20 is a measured failure, not a value to round into range. [TARGET]

### Comeback measurement

For every peril activation opportunity in the `5 pilots x 9 stages x reference loadouts` grid, replay the same config and input trace with all peril clauses off, then with only activation `i` enabled. [TARGET] Attribute an instant reversal only when the all-off run fails and the isolated-activation run clears. [TARGET]

`reversal_probability = reversal_activations / isolated_activation_opportunities`.
[TARGET]

Report the population ratio and confirm all three limits: ratio `<=0.30`, no more than two activations in a run, and no simultaneous clauses. [TARGET] Deterministic paired replays replace RNG sampling; they do not waive the numeric cap. [INFERENCE]

## Build authorities — no PM-owned shadow constants

| Decision surface | Runtime authority | PM use |
|---|---|---|
| Equipment costs and T5 cap | `Assets/Scripts/View/ProgressionGuide.cs` — `EquipCosts`, `EquipCap` | Read at measurement time. [TARGET] |
| Sigil cost | `Assets/Scripts/View/ProgressionGuide.cs` — `SigilCost` | Record deviations; do not count sigils toward equipment T5. [TARGET] |
| Equipment purchase mutation | `Assets/Scripts/View/GameDirector.cs` — `TryBuyEquip` | Reuse the same transaction semantics. [TARGET] |
| In-run relics and rank drops | `RunDigest.Relics` plus `ICampaignSnapshot` final tiers | Capture the live result; never infer from kill count. [TARGET] |
| First-clear reward | `GameDirector.FirstClearRelicBonus` | Read fired reward and first-clear bit. [TARGET] |
| Pact multiplier | `GameDirector.PactRelicMultiplier` and the armed run state | Measure off/on routes separately. [TARGET] |
| Persistent economy state | `CampaignData` written by `CampaignStore` | Snapshot before run, after settlement, and after purchases. [TARGET] |

`pm/reward-bands.md` is the measurement contract, not a second balance authority. [TARGET]

## Required telemetry/evidence row

The static WebGL build may keep telemetry local, but the QA export for every eligible session must contain these fields. [TARGET]

```yaml
identity:
  run_id: string
  build_id: string
  pilot_id: string
  route_id: "A|B|C|D|E"
  session_index: int
eligibility:
  prologue_done: bool
  spend_surface_reachable: bool
  mode: string
  settlement_reason: string
  spend_opportunity_reached: bool
progression_before:
  relics: int
  weapon_tier: int
  lantern_tier: int
  cloak_tier: int
  cleared_mask: int
reward_components:
  stage_id: string
  run_relics: int
  pact_armed: bool
  pact_multiplier: int
  first_clear: bool
  first_clear_relics: int
  training_mastery_relics: int
  rank_transitions: [{slot: string, from: int, to: int, source: string, grade: string}]
spend:
  equipment_purchases: [{slot: string, from: int, to: int, cost: int}]
  sigil_relics_spent: int
progression_after:
  relics: int
  weapon_tier: int
  lantern_tier: int
  cloak_tier: int
  t5_reached: bool
fairness:
  paid_offer_visible: false
  paid_power_applied: false
comeback:
  activations: int
  activation_rows: [{tick: int, clause: string, health_before: float, max_health: float, isolated_reversal: bool}]
```

The current `last-run` digest has score/wave/kills/relics/health/reason, and the campaign save has endpoint tiers/relics, but neither alone identifies stage, reachability, first-clear component, pact component, rank-transition source, or purchases. [OBSERVED: `ops/telemetry-contract.md`; `WebGLStorage.WriteRunDigest`; `CampaignData`] Until QA captures the joined row through a test/session artifact, T5 remains `pending`, not PASS. [TARGET]

## Prior cycle block retained as history

The following was the canonical cycle-2 block and is retained so the 2026-08-05 contradiction remains auditable. [OBSERVED]

```yaml
comeback:
  first_clear_bonus: {cinder-sluice: 6, ember-bastion: 8, ash-march: 10}
  bonus_vs_run_income_max: 0.25
  paths: [first-clear]
steady:
  parity_sessions_band: [10, 20]
fairness:
  paid_free_winrate_delta_max_pp: N/A
```

- The old `0.25` bonus ratio was contradicted by observed `17–18` relic full-run income and is superseded as a comeback definition; the first-clear grants remain live one-time income in the recurrence above. [OBSERVED: `qa/playtest-report.md` lines 33–37]
- The old `12–18` claim was an inference rather than a reachable-lobby session measurement and is superseded by the cycle-9 protocol. [OBSERVED: prior revision of this file; cycle-8 D-9]
