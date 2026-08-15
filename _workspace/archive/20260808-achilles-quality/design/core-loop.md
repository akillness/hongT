# Core Loop — Cinder Court Five-Cycle Model (G7)

- `[OBSERVED]` Active program run-id: `20260808-achilles-quality`; public beat: **NAN 2026 final submission**.
- `[OBSERVED]` Cycle 9 preserves the shipped loop and enters Stage 2/Phase 2a; cycles 10–13 test the action-identity directions in [concept.md](concept.md).
- `[TARGET]` G7 requires at least one 30–180 s loop, ≥3 actions, ≥1 reward event, and ≥70% voluntary re-entry. The model below makes those requirements directly measurable.

Status convention: every prose block and table row is marked. A status on a loop heading applies to its formula and action list.

## `[TARGET]` Mandatory loop L1 — Read → Commit → Turn → Adjudicate

```yaml
loop_id: L1-court-exchange
period_band_s: [30, 90]
nominal_period_s: 60
minimum_actions: 4
reward_events_min: 1
repeat_rate_proxy_min: 0.70
measurement_start: WaveStarted
measurement_end: next WaveStarted | StageCleared | GameOver
public_beat: NAN 2026 final submission
```

| Status | Phase | Nominal budget | Required player action | Observable result |
|---|---|---:|---|---|
| `[TARGET]` | **Read** | 4–10 s | Identify active attacker/formation, hazard phase, guardian position, and one safe lane. | First deliberate move or target lock occurs after at least one relevant cue. |
| `[TARGET]` | **Commit** | 8–22 s | Choose committed strike, lantern dodge, or (future) Witness Guard; spend recovery, oil, or position. | Action event plus a non-zero opportunity cost. |
| `[TARGET]` | **Turn** | 8–25 s | Reposition host/guardian into a current or wall, break a pylon, contest an altar, or use a pillar/guardian angle. | `HazardPulse`, `PylonDown`, `AltarBlessing`, displacement, or guardian-state trace. |
| `[TARGET]` | **Adjudicate** | 5–20 s | Finish the wave objective and collect/choose a result. | XP/oil/pickup/growth/clear reward event; next loop can start. |

`[TARGET]` Nominal formula: `7 + 15 + 20 + 18 = 60 s`. A valid segment must be `30 ≤ period ≤ 90 s`, contain ≥4 distinct phase actions, and include ≥1 reward event. A segment outside the band is recorded, not trimmed to pass.

`[TARGET]` Reward cadence: one immediate reward per loop (XP, oil, pickup, pylon removal, altar oil, or clear) and one authored choice at least every two loops (growth offer, equipment comparison, preparation, guardian decision, or boss phase answer).

## `[OBSERVED]` Shipped subloops that feed L1

### Current rhythm

```yaml
subloop_id: tide-current
mechanical_period_s: 6.0
telegraph_s: 0.8
active_s: 3.2
rest_s: 2.0
L1_target_s: [30, 60]
```

- `[OBSERVED]` Actions: read the band → choose crossing/against-current route → lure or separate enemies → strike after displacement.
- `[OBSERVED]` Reward: ordinary kill/XP/pickup; the current itself deals no damage, so formation break is the tactical payoff.
- `[TARGET]` A valid L1 current segment contains at least five six-second cycles or ends earlier only on `StageCleared`/`GameOver`; ≥40% of successful segments should record enemy displacement before a player kill.

### Pylon priority

```yaml
subloop_id: ember-pylon
L1_target_s: [30, 60]
required_actions: [read_aura, choose_approach, strike_pylon, clear_unsealed_host]
reward_event: PylonDown
```

- `[OBSERVED]` A live pylon reduces all damage received by enemies in its aura; basic/combo strikes, not skills, damage the pylon; destruction is permanent for the run and emits `PylonDown`.
- `[TARGET]` The pylon segment passes when `PylonDown` precedes at least one newly unsealed enemy kill and the whole segment remains within 30–60 s.

### Ash-wall schedule

```yaml
subloop_id: ash-wall
mechanical_period_s: 23.0
telegraph_s: 1.5
L1_target_s: [46, 92]
required_cycles: [2, 4]
```

- `[OBSERVED]` The two phased walls always preserve a safe corridor and damage player and enemies; an altar and pylon compete for the same center-space decision in Ash March.
- `[TARGET]` Actions: read edge/phase → collect or hold during safe time → route the host into the advancing wall → leave/guard the altar lane → claim damage/kill/oil reward.
- `[TARGET]` The former 22.5/23 s mechanical cycle is not itself a G7 loop because it is below 30 s. G7 measures 2–4 wall cycles as one 46–92 s court exchange.

### Wave combat and reward

```yaml
subloop_id: wave-clear
observed_spawn_rule: min(20, 3 + floor(wave * 1.2))
observed_intermission_s: 2.15
L1_target_s: [30, 90]
reward_events: [kill_xp, pickup, LevelUp, WaveStarted]
```

- `[OBSERVED]` A wave ends after its queue and living host are empty; the next wave begins after a 2.15 s intermission.
- `[TARGET]` The interval from one `WaveStarted` to the next is the default L1 measurement seam. Waves under 30 s pair with the following wave; waves over 90 s are a pacing failure unless they are a boss segment.

## `[TARGET]` Boss loop L2 — Read office → answer phase → claim punish

```yaml
loop_id: L2-boss-verdict
period_band_s: [30, 180]
phase_floor_s: 2.17
minimum_actions: 3
reward_events_min: 1
answer_identification_min: 0.70
impression_min_5: 4.0
```

| Status | Step | Required action | Evidence |
|---|---|---|---|
| `[TARGET]` | Read office | Identify boss role, phase pip, guardable/evade-only cue, room rule, and escort/token state. | Intro/phase event plus player-facing cue trace. |
| `[TARGET]` | Answer phase | Commit strike/dodge/guard and apply the room rule or guardian angle. | Action + boss/hazard/guardian event sequence. |
| `[TARGET]` | Claim punish | Deal focused boss damage during a named opening; reset before next pattern. | Boss HP delta within the punish interval. |
| `[TARGET]` | Adjudicate | Cross phase or defeat; receive equipment/guardian/stage reward. | Phase event, equipment drop, or `StageCleared`. |

- `[OBSERVED]` Current bosses use deterministic two/three-phase profiles keyed by stage and court role. Current dungeon health multiplication was tuned so a fast reference kill lasts about 19.28 s, below G7’s 30 s minimum when treated alone.
- `[TARGET]` Therefore the G7 boss segment starts at boss-wave `WaveStarted`, including escort/position setup, and ends at `StageCleared`/`GameOver`; target duration is 30–180 s without artificially extending health. Each live phase remains ≥2.17 s.
- `[TARGET]` Boss-duel quality requires ≥70% of testers to name both the intended action answer and room-rule answer after one fight; spectacle recall alone does not pass.

## `[TARGET]` Meta loop L3 — Prepare a verdict build

```yaml
loop_id: L3-preparation
period_band_s: [120, 180]
minimum_actions: 3
reward_events_min: 1
choice_cadence_loops: 2
```

- `[OBSERVED]` Existing authorship surfaces include weapon/lantern/cloak tiers, attack/vitality/swiftness growth, four skills, guardian roster/hold/recall, and room-local preparation offers.
- `[TARGET]` L3 actions: inspect next court function → compare at least two owned options → choose a Force/Mobility/Testimony expression → enter the next room → validate it in L1.
- `[TARGET]` Reward: chosen loadout/preparation plus the next-room behavioral change. By wave 3 the run must contain ≥2 decisions from different systems; by boss entry the player must be able to state one favored and one sacrificed answer.

## `[TARGET]` Action accounting

| Status | Action class | Counts once when | Does not count when |
|---|---|---|---|
| `[TARGET]` | Read | Player changes target/route after a visible cue. | A cue appears while input remains unchanged. |
| `[TARGET]` | Strike | A swing commits and hits host/pylon/boss or creates a punish. | Held attack repeats into empty space. |
| `[TARGET]` | Dodge | Dash changes threat outcome or crosses a court zone. | Dash occurs with no threat/route relation (“panic”). |
| `[TARGET]` | Guard | Future stance receives a guardable hit or intentionally contests a cue. | Button is held outside a cue; guard is not implemented in cycle 9. |
| `[TARGET]` | Environment | Hazard/pylon/altar/pillar changes position, damage, protection, or oil. | Player merely stands near scenery. |
| `[TARGET]` | Guardian | Hold/recall/skill changes target angle, zone, or cadence. | Passive follower damage only. |
| `[TARGET]` | Reward | XP/oil/drop/choice/clear changes available play. | Score text with no gameplay/progression consequence. |

## `[TARGET]` Fun hypotheses and telemetry

| Status | Hypothesis | Required fields | Pass target |
|---|---|---|---|
| `[TARGET]` | Deterministic retry becomes deliberate. | run/stage/difficulty, loop start/end, death/retry, action class, panic-dodge marker | Voluntary re-entry ≥70%; panic-dodge share declines on retry. |
| `[TARGET]` | Court machinery is used against the host. | hazard kind/phase, actor displacement, pylon/altar event, kill credit | ≥40% successful L1 loops contain one tactical environment event. |
| `[TARGET]` | Coordination reads as fair turn-taking. | difficulty, attack-token owner/handoff, ring slot, flank, damage cue source | ≥80% damage attributable by testers; ≤3 simultaneous high-salience cues. |
| `[TARGET]` | Builds change verbs, not only DPS. | build axes, action shares, encounter type, result | ≥3 viable archetypes use distinct primary answers; no >50% dominance. |
| `[TARGET]` | Boss phases teach authored answers. | phase duration, cue, selected answer, room interaction, post-fight response | Phase ≥2.17 s; answer identification ≥70%; impression ≥4/5. |

`[TARGET]` Event telemetry contract: `loop_id`, `segment_id`, `stage_id`, `difficulty`, `started_at`, `ended_at`, `duration_s`, `actions[]`, `reward_events[]`, `hazard_events[]`, `guardian_events[]`, `attack_token_handoffs`, `damage_cue_source`, `retry_index`, and `result`. Missing fields make the corresponding claim unmeasured, not passed.

## `[TARGET]` Five-cycle loop evolution

| Status | Cycle | Loop change | Proof before exit |
|---|---|---|---|
| `[TARGET]` | 9 | Measure current L1 without changing combat numbers. | G8 impression ≥4/5 measured at entry; L1 repeat proxy ≥70%; G5 remeasurement. |
| `[TARGET]` | 10 | Prototype explicit strike/dodge/guard commitment in bounded fixtures. | ≥80% threat windows offer ≥2 answers; no answer >60% share; deterministic replay. |
| `[TARGET]` | 11 | Couple attack-token handoff, guardian angle, and two-sided hazards. | ≥70% eligible waves show handoff + reposition; telegraph cap ≤3. |
| `[TARGET]` | 12 | Make L3 build expression predict L2 boss answers. | ≥3 viable archetypes; answer identification ≥70%; combo EV ≤1.3× median. |
| `[TARGET]` | 13 | Freeze mechanics; polish pacing, accessibility, effects, and telemetry for NAN. | G7 final, G4/G6/G1 final; no unresolved S1/S2 readability defects. |

## `[TARGET]` Presentation and lore handoff

- `[TARGET]` [presentation-spec.md](presentation-spec.md) owns how Read/Commit/Turn/Adjudicate appears without changing simulation timing.
- `[TARGET]` [worldview.md](worldview.md) owns why every hazard, role, reward, and action term belongs to the court.
- `[TARGET]` [concept.md](concept.md) owns cycle scope and prevents cycle-9 benchmark-driven mechanic changes.
