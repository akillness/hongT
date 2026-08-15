# Cinder Court — Five-Cycle Product Concept

- `[OBSERVED]` Active program run-id: `20260808-achilles-quality`; the next public beat is the **NAN 2026 final submission**.
- `[OBSERVED]` Cycle 8 directs cycle 9 to re-enter **Stage 2 / Phase 2a** as a retune, not a concept reset; G5 parity, duplicate implementation ownership, and G8 impression proof are the first obligations (`../retrospectives/cycle-8-retrospective.md`).
- `[TARGET]` This document is the PRD direction for cycles 9–13. Cycle 9 preserves the shipped concept; cycles 10–13 may change mechanics only through the repository’s deterministic amendment and gate process.

Status convention: every row or paragraph begins with `[OBSERVED]`, `[INFERENCE]`, or `[TARGET]`. Tables inherit the status in their first column.

## Product sentence

`[TARGET]` **Descend through the Cinder Court as the Lantern Reaver, recover testimony held by guardian echoes, and turn each deterministic courtroom hazard against a coordinated host before committing to a readable strike, dodge, or guard verdict.**

`[TARGET]` The identity is not “another broad action RPG.” It is **a learnable court**: enemy order, hazard phase, guardian placement, and boss escalation are deterministic enough to study, while build choices change which evidence the player acts on.

## Source-of-truth links

- `[OBSERVED]` Lore, vocabulary, color, and stage traceability: [worldview.md](worldview.md).
- `[OBSERVED]` Mandatory 30–180 second play model and reward cadence: [core-loop.md](core-loop.md).
- `[TARGET]` Camera, impact, readability, and reduced-motion contract: [presentation-spec.md](presentation-spec.md).
- `[OBSERVED]` External play-concept calibration and explicit non-copy boundary: [benchmark analysis](trend-survey/achilles-analysis.md).
- `[OBSERVED]` Implemented numeric truth remains in `docs/SIM_SPEC*.md`, `Assets/Scripts/Sim`, and `balance-sheet.md`; this PRD does not override those files.

## Shipped foundation versus proposed identity

| Status | Surface | Product fact or direction | Testable boundary |
|---|---|---|---|
| `[OBSERVED]` | Court route | Nine logical stages form record → testimony → judgment/enforcement, with six deterministic simulation anchors and court-function hazards. | Same stage/config/input sequence must reproduce the same digest. |
| `[OBSERVED]` | Player kit | Dungeon play has a 58/58/87 three-hit chain, 190 px/0.22 s invulnerable dash costing 8 oil with 1.6 s base cooldown, four oil skills, charge follow-through, growth choices, and guardian commands. | `docs/SIM_SPEC_HACKSLASH.md` and live sim constants remain numeric truth. |
| `[OBSERVED]` | Enemy order | Hard and Nightmare enable an eight-slot holding ring, flank priority, and 3/4 simultaneous attack tokens; Story and Normal retain their prior chase behavior. | Difficulty profile values and deterministic group-plan tests own the evidence. |
| `[OBSERVED]` | Court tactics | Vents damage, pillars block, altars reward a hold, currents push player and enemies, pylons protect enemies until struck down, and ash walls damage both sides. | Hazard event traces and balance bands own the evidence. |
| `[OBSERVED]` | Build authorship | Players combine three permanent equipment slots, three meta-stat axes, three in-run growth offers, four skills, and guardian roster/preparation choices. | Equal-skill paid/free delta must remain ≤5%p; free parity remains 10–20 sessions. |
| `[OBSERVED]` | Boss identity | Four court-role archetypes map to stages and use two or three deterministic phases with distinct cadence, speed, reach, damage, telegraph, escort, and health vectors. | Phase vectors are table-driven; no random move selection is required. |
| `[TARGET]` | Action identity | A combat exchange must ask the player to choose **Committed Strike**, **Lantern Dodge**, or **Witness Guard**; each answer gives up another option for a visible interval. | In cycle-10 test encounters, ≥80% of incoming threat windows must expose at least two viable answers, while no answer exceeds 60% selection share across ≥20 recorded exchanges. |
| `[TARGET]` | Enemy coordination | Attack-token ownership, flank pressure, guardian placement, and hazards should read as one “court order,” not as unrelated systems. | On Hard/Nightmare, ≥70% of multi-enemy waves must contain one verified token handoff and one flank/ring reposition; 0 simultaneous telegraph frames may exceed the existing cap of 3. |
| `[TARGET]` | Build signature | A build must change the preferred answer to at least two encounter types without creating a universal best path. | ≥3 QA archetypes independently remain in 45–55% win-rate band; no archetype exceeds 50% dominance; no dominant pair exceeds 1.3× median EV. |
| `[TARGET]` | Boss duel | Every boss phase should test one action commitment and one room rule, then leave a punish window the player can name. | Each phase lasts ≥2.17 s; ≥70% of testers correctly name the intended action/environment answer after one fight; impression target ≥4/5. |

## The three committed decisions

### 1. Committed Strike

- `[OBSERVED]` The current chain already commits 0.30 s, 0.30 s, then 0.42 s per swing; the third hit carries 1.5× base damage and knockback. Dash can cancel the chain by spending its oil/cooldown budget.
- `[TARGET]` Keep those shipped windows in cycle 9. In cycle 10, expose attack recovery as a visible ember arc and prohibit a free transition from an active hit to Witness Guard. Dash cancel remains the paid escape: 8 oil and its live cooldown.
- `[TARGET]` A completed three-hit chain or charged follow-through must create one of two authored results: break a pylon/formation or claim a boss punish. “Swing because the button is ready” is not sufficient loop value.

### 2. Lantern Dodge

- `[OBSERVED]` The current dash travels 190 px over 0.22 s, is invulnerable throughout, costs 8 oil, and starts at a 1.6 s base cooldown before growth modifiers.
- `[TARGET]` Dodge is the space-making answer: it escapes wide court attacks and crosses currents/walls, but it spends the same oil economy used by skills and cannot become permanent invulnerability. The existing swiftness cooldown floor of ×0.55 remains a hard lower bound unless a signed amendment replaces it.
- `[TARGET]` QA records “necessary dodge,” “optional dodge,” and “panic dodge”; panic share should fall below 35% after one encounter retry without reducing threat damage.

### 3. Witness Guard

- `[OBSERVED]` The shipped dungeon has a duration shield skill and a `Defence` presentation action, but no player-timed guard/counter rule is established in the current simulation contract.
- `[TARGET]` Cycle 10 may prototype **Witness Guard** as a future HongT rule: 0.50 s stance, first 0.16 s as the counter window, 0.34 s ordinary guard tail, 0.35 s whiff recovery, and 12 oil committed on entry. Counter negates the guardable hit and opens a 0.60 s punish; tail reduces one guardable hit by 65%; unguardable threats retain a broken-ring cue and require dodge or spacing.
- `[TARGET]` A guardable contact must show a shape cue for ≥0.30 s. Any existing attack below that cue floor is evade-only until retuned; reduced motion may remove movement but never the cue shape or duration.
- `[TARGET]` This is a proposal, not a cycle-9 balance edit. It requires a new signed balance/PM record, deterministic digest coverage, accessibility review, and a director Stage-1 decision before implementation.

## Enemy coordination as court procedure

- `[INFERENCE]` The current ring/tokens already create turn-taking and flanks, but without presentation the player can read it as crowd drift rather than authored coordination.
- `[TARGET]` Use future HongT role language only: the enemy holding the attack token is the **Acting Bailiff**; ring holders are the **Witness Line**; a pylon-protected group is under **Sealed Testimony**. These are behavior labels, not new mythology or assets.
- `[TARGET]` Token handoff gets a ≤0.20 s floor chevron and short audio tick; flank priority gets a side-origin chevron; only the active attacker receives the high-salience ember cue. Waiting enemies remain lower contrast, preserving the cap of 3 simultaneous high-salience telegraphs.
- `[TARGET]` Environmental tactics are valid only when two-sided: current or wall displacement may harm the host, pylon destruction must alter target priority, altar occupancy must compete with safe spacing, and guardian hold/recall must create a different angle rather than passive extra damage.

## Build authorship rules

- `[TARGET]` A build is defined by a **verdict triangle**, not item rarity: `Force` favors committed chains/pylon break; `Mobility` favors dodge routing/current control; `Testimony` favors guardian placement/guard timing. Existing equipment, stats, growth offers, skills, and companions are the ingredients; the triangle is the explanation layer.
- `[TARGET]` By the end of wave 3, the player must have made ≥2 consequential choices from different systems; by the boss, the results panel must name the two most-used answers and one underused answer without recommending a “best” build.
- `[TARGET]` Reward purchases may accelerate expression but never cross the G5 bands: comeback reversal ≤30% per capped activation, free parity 10–20 sessions, paid/free equal-skill delta ≤5%p, and every balance-touching revenue point signed by designer and PM.

## Five-cycle concept evolution

| Cycle | Entry and concept stance | Deliverable hypothesis | Exit evidence |
|---|---|---|---|
| `[TARGET]` **9 — Court Baseline** | Enter Stage 2/Phase 2a; preserve the current concept and all shipped combat numbers. | The existing deterministic hazard/guardian identity is already striking when players can reach it and score it. | G8 is measured now, not carried: candidate appears in ≤2 of ≥5 surveyed comparable titles **and** QA impression is ≥4/5; G5 parity is remeasured; duplicate equipment/sigil ownership is named. |
| `[TARGET]` **10 — Committed Verdicts** | Re-enter Stage 1 only if the director approves the guard amendment; otherwise remain a no-code prototype. | Explicit strike/dodge/guard opportunity costs create legible mastery without importing another game’s resource model. | ≥80% threat windows expose ≥2 valid answers; no answer >60% share; guard cue ≥0.30 s; digest and G2 bands hold. |
| `[TARGET]` **11 — Ordered Host** | Stage 2 retune of group AI, guardian angles, and environment composition. | Players feel surrounded by a court procedure, not random crowding. | ≥70% coordinated waves show token handoff + reposition; telegraph cap ≤3; ≥3 viable build archetypes and ≥5 QA archetypes tested. |
| `[TARGET]` **12 — Authored Build, Named Duel** | Stage 2 development of verdict-triangle explanations and boss phase assignments. | Players can describe why their build changed a duel answer and why each court role fights differently. | ≥70% answer-identification; phase ≥2.17 s; no pair >1.3× median EV; boss impression ≥4/5. |
| `[TARGET]` **13 — NAN Final Judgment** | Stage 3 presentation, accessibility, performance, and G1 audit; no late mechanic expansion. | The first external player understands the court, acts, and sees a complete verdict without external documentation. | G4 median ≥4/5, feedback latency ≤100 ms, 0 unresolved S1/S2 readability complaints, G6 p95 ≤16.7 ms/long frames <0.5%/input ≤100 ms, G1 0 violations and 100% traceability. |

## Fun hypotheses and falsification

| Status | Hypothesis | Measure | Falsified when |
|---|---|---|---|
| `[TARGET]` | Learning a deterministic room converts failure into intention. | On immediate retry, panic-dodge share falls and voluntary loop re-entry is ≥70%. | Repeat rate <70% or panic share does not improve. |
| `[TARGET]` | Two-sided hazards create authorship rather than passive avoidance. | ≥40% of successful 30–90 s loops include one credited enemy displacement/pylon break/altar timing choice. | Hazard interaction occurs in <25% of clears or correlates only with damage taken. |
| `[TARGET]` | Coordination becomes fair when attack ownership is readable. | ≥80% of damage events are attributed by testers to a cue they recall; high-salience cues never exceed 3 at once. | Unattributed damage >20% or cue crowding breaches the cap. |
| `[TARGET]` | Build identity changes decisions, not merely numbers. | Three archetypes choose different primary answers while remaining in G2/G3 bands. | One answer/build dominates >50% optimal play or EV >1.3× median. |
| `[TARGET]` | A court-role boss is memorable because room rule and action rule meet. | Boss impression ≥4/5 and ≥70% can name both rules after one fight. | Score <4/5 or players remember spectacle but cannot state the interaction. |

## Non-goals and originality boundary

- `[TARGET]` Do not copy external mythology, character names, prose, slogans, interface layouts, level layouts, boss anatomy, art, prompts, code, or assets.
- `[TARGET]` Do not replace lantern oil with a borrowed stamina economy; commitment must emerge from HongT’s current oil, cooldown, movement, guardian, and deterministic timing rules.
- `[TARGET]` Do not randomize enemy coordination or hazards to manufacture variety; build variety must survive identical encounter seeds.
- `[TARGET]` Do not add a cycle-9 mechanic to satisfy a benchmark. Cycle 9 proves the current game; benchmark-derived directions begin at cycle 10 and remain proposals until their gates pass.
