# Revenue Forecast — 20260808-achilles-quality

- `status: submission build / monetization disabled` [OBSERVED]
- `public-beat: NAN 2026 final submission` [TARGET]
- `forecast-window: cycles 9–13` [TARGET]
- `financial-forecast: zero by product decision, not missing data` [TARGET]

## PRD frame

### Problem

The build has a persistent relic economy and power progression, but it has no approved live monetization surface. [OBSERVED: `ProgressionGuide`, `CampaignData`, and `pm/revenue-map.md`] Calling earned-currency sinks “revenue” would overstate the product and obscure G5 fairness. [INFERENCE]

Cycle 8 also changed the spend UI from unreachable to reachable in the deployed layout, invalidating the prior session denominator. [OBSERVED: `retrospectives/cycle-8-retrospective.md`, G4/D-9] Cycle 9 must therefore establish a computable free-progression baseline before any later concept work is judged. [TARGET]

### Objective

Ship a non-pay-to-win NAN 2026 submission in which every power-affecting input is earned through play, the G5 free path is measured against the `10–20` session band, and every monetization field remains disabled. [TARGET]

Use Achilles: Legends Untold only to calibrate action-RPG play concepts—committed attacks, readable response, build authorship, coordinated enemies, named duels, and finish impact—while retaining HongT Cinder Court’s courtroom/lantern worldview and deterministic Unity/WebGL architecture. [TARGET: `design/trend-survey/achilles-steam-source.md` §Adaptation boundary]

### Segments

| Segment | Need | Submission-build value | Status |
|---|---|---|---|
| First-session player | Understand the lobby-to-dungeon path and reach progression spending. | A reachable SANCTUM and one earned-currency economy. | [OBSERVED] Cycle-8 reachability is measured. [TARGET] Cycle-9 economy replay is pending. |
| Steady free player | See power progress without a hidden pay wall. | All three equipment slots can reach T5 through play; target `10–20` eligible sessions. | [TARGET] |
| Mastery player | Re-enter deterministic combat for pact/trial challenge. | Optional pact multiplier and one-time training mastery reward remain free. | [OBSERVED] |
| Jury/judge at NAN 2026 | Read a coherent action-RPG promise quickly. | Original HongT worldview plus legible combat/build evidence, without a store or premium prompt. | [TARGET] |

### Value and non-goals

- Player value is legible mastery and authored build progression, not purchase acceleration. [TARGET]
- Product value for this window is submission quality, completion, replay intent, and evidence quality; it is not cash conversion. [TARGET]
- Adding checkout, ads, premium currency, a paid continue, a paid comeback activation, or a paid power shortcut is outside cycles 9–13 unless the director explicitly re-scopes and designer/PM negotiation is signed first. [TARGET]
- Greek mythology, Achilles names, narrative, art, layouts, and assets are not product inputs. [TARGET: benchmark adaptation boundary]

## Financial forecast: deliberately zero

```yaml
run_id: 20260808-achilles-quality
monetization:
  mode: disabled
  currency_sku_count: 0
  real_money_offer_count: 0
  paid_power_shortcut_count: 0
  paid_comeback_path_count: 0
forecast:
  window_cycles: [9, 10, 11, 12, 13]
  gross_revenue_floor: 0
  gross_revenue_expected: 0
  gross_revenue_peak: 0
  purchase_conversion_expected: 0
  arppu: null
  predictability: "exact while monetization remains disabled"
fairness:
  paid_free_winrate_delta_max_pp: 5
  paid_free_identity_delta_pp: 0
  qa_audit: pending
```

`ARPPU` is `null`, not `0`, because there are no paying users or purchase events in the product model. [TARGET] Gross revenue and conversion are `0` because the approved release has zero offers, not because an uninstrumented store is assumed to underperform. [TARGET]

Any observed nonzero offer, purchase, gross-revenue, or paid-power field is a release-blocking scope breach. [TARGET]

## Session rhythm without monetization

| Beat | Earned/progression event | Revenue event | Forecast interpretation |
|---|---|---|---|
| Lobby entry | Player sees balances and can reach equipment/sigil spending. | None. | [TARGET] Verify reachability; do not call a relic purchase a conversion. |
| Dungeon attempt | Run produces deterministic relic and rank outcomes. | None. | [OBSERVED] Measure session-path inputs. |
| Defeat | In-run relics bank; equipment ranks do not. | No paid revive/continue. | [OBSERVED] The free recovery payout is live. [TARGET] Paid continue remains disabled. |
| First clear | Three later stages may grant `+6/+8/+10` once. | None. | [OBSERVED] Model as a one-time progression peak. |
| Cleared-stage pact replay | In-run relic payout may be `x2`; first-clear bonus is not doubled. | None. | [OBSERVED] Model as a free replay accelerator. |
| Training mastery | `+2` relics once. | None. | [OBSERVED] Keep outside canonical T5 route and measure separately. |
| T5 reached | Power track saturates for that slot set. | No upsell. | [TARGET] Record completion and subsequent replay behavior. |

The financial rhythm has no peaks or troughs: floor, expected value, and peak are all `0` throughout the five-cycle window. [TARGET] The progression rhythm is intentionally non-flat and must be evaluated separately through the session recurrence in `pm/reward-bands.md`. [TARGET]

## Five-cycle release plan

All cycles preserve the existing concept in cycle 9; cycles 10–13 are PRD targets, not shipped claims or approved balance changes. [TARGET]

| Cycle | PRD quality slice | Achilles calibration only | PM acceptance signal | Monetization state |
|---|---|---|---|---|
| 9 — parity truth | Stage 2 Phase 2a re-entry; join reachable-lobby sessions to prices, rewards, rank drops, and purchases; G8 impression score is an entry condition rather than another carry-over. | No concept import; preserve current HongT concept. | `N_T5` computable for every required route; G5 and G8 have evidence paths. [TARGET] | Disabled. [TARGET] |
| 10 — committed response | Clarify strike commitment and readable evade/guard response within existing deterministic timing. | Pillar 1: precise combat verbs. | Retry/clear traces and impression feedback identify the intended response without a new paid recovery lever. [TARGET] | Disabled. [TARGET] |
| 11 — build authorship | Make existing equipment, sigils, stats, and companion choices read as distinct builds without changing prices unnegotiated. | Pillar 2: build authorship. | At least three distinct viable strategies remain inside G2/G3 while T5 stays in G5 band. [TARGET] | Disabled. [TARGET] |
| 12 — coordinated pressure | Improve enemy coordination and environmental tactics through deterministic rules and current worldview. | Pillars 4–5: handcrafted identity and coordinated enemies. | G3 archetype viability and G4 readability remain green across the fixed route matrix. [TARGET] | Disabled. [TARGET] |
| 13 — named confrontation | Strengthen named boss-phase and climactic finish presentation using HongT characters and assets. | Pillar 3 plus presentation language. | G8 impression median reaches `>=4/5` with `0` new worldview violations. [TARGET] | Disabled. [TARGET] |

No cycle above authorizes a new price, reward amount, paid path, or balance override. [TARGET] Any such proposal re-enters `pm/negotiation-record.md` before implementation. [TARGET]

## Build authority choices

- Prices and caps are read from `ProgressionGuide`; PM artifacts must not clone them into runtime or claim authority over them. [TARGET]
- Reward settlement is read from `GameDirector` and `RunDigest`; PM forecasts must decompose the fired components instead of using a guessed average. [TARGET]
- Persistence truth is `CampaignData`/`CampaignStore`; a screenshot or endpoint balance alone cannot reconstruct a session path. [OBSERVED]
- The build remains deterministic and static-WebGL; session evidence may be local/test-exported rather than sent to a remote analytics service. [OBSERVED: `ops/telemetry-contract.md`]

## Forecast telemetry

The PM forecast consumes the session row defined in `pm/reward-bands.md` and additionally requires these release-level fields. [TARGET]

```yaml
release_identity:
  run_id: string
  build_id: string
  cycle: int
submission_quality:
  eligible_sessions_to_t5: int
  t5_band_result: "inside|early|late|not-reached"
  repeat_after_t5: bool
  g8_impression_score: float
monetization_guard:
  offers_shown: 0
  purchase_attempts: 0
  successful_purchases: 0
  gross_revenue: 0
  paid_continue_used: false
  paid_comeback_used: false
  paid_power_applied: false
```

`offers_shown`, purchase fields, and paid-power booleans are guard assertions, not growth KPIs. [TARGET] `eligible_sessions_to_t5`, repeat behavior, and G8 score are quality/retention proxies and must not be translated into money without a separately approved monetization model. [TARGET]

The current local telemetry contract does not emit this joined release row. [OBSERVED: current `ops/telemetry-contract.md` has only last-run digest and campaign endpoint fields] The five-cycle forecast becomes verifiable only after QA exports the row from deterministic sessions or equivalent test evidence. [TARGET]

## Decision gates

| Decision | Rule | State |
|---|---|---|
| Cycle-9 G5 | Pass only with measured `N_T5` per required route, comeback cap evidence, and paid-path absence audit. | [TARGET] pending |
| Future revenue discussion | May begin only after the submission build is complete and a director-approved scope explicitly permits monetization research. | [TARGET] disabled |
| Any power-affecting monetization | Requires free milestone path, `<=30%` reversal, `<=5%p` paid/free delta, `10–20` free parity, and signed designer/PM entry. | [TARGET] |
| Any cycle-10–13 balance change | Requires a new signed negotiation entry; benchmark language alone is not approval. | [TARGET] |
