# Achilles: Legends Untold — Play-Concept Benchmark Analysis

- `[OBSERVED]` Program run-id: `20260808-achilles-quality`; public beat: **NAN 2026 final submission**.
- `[OBSERVED]` Primary evidence is the Scrapling Steam capture in [achilles-steam-source.md](achilles-steam-source.md), refreshed 2026-08-09 from the Korean store page with HTTP 200.
- `[OBSERVED]` This is a **single developer-authored storefront source**, not an independent mechanics audit or hands-on playtest. Store claims calibrate product direction; they do not prove runtime behavior, tuning quality, or player reception causes.
- `[TARGET]` Cycle 9 preserves Cinder Court’s existing concept during **Stage 2 / Phase 2a** and uses this source only to calibrate G8/G5 questioning. Directions derived here begin in cycles 10–13 and require HongT’s own approval, deterministic amendment, tests, and gate evidence.

Status convention: every paragraph/table row is marked `[OBSERVED]`, `[INFERENCE]`, or `[TARGET]`.

## Source claim inventory

| Status | Storefront claim | Evidence location | Confidence boundary |
|---|---|---|---|
| `[OBSERVED]` | The product positions itself as a punishing action RPG with precise, stamina-based strike, timed dodge/block/counter, abilities, special attacks, and crafted throwables. | `achilles-steam-source.md#Gameplay pillars stated by the developer` | Developer marketing claim; timings and balance are not supplied. |
| `[OBSERVED]` | The store emphasizes weapon choice, a constellation-shaped skill tree, gear upgrades, and shaping a personal warrior build. | Same source, pillar 2 | Breadth is stated; dominance, parity, and actual build diversity are not measured. |
| `[OBSERVED]` | The store promises one-on-one confrontations with named opponents and larger boss/ambush encounters. | Same source, pillar 3 | Encounter composition and duel-time share are unspecified. |
| `[OBSERVED]` | The store describes handcrafted locations, secrets, side quests, and environmental identity. | Same source, pillar 4 | It does not quantify tactical environment interaction. |
| `[OBSERVED]` | The store names a group-AI system whose enemies flank, bait, adapt, coordinate, and use the environment. | Same source, pillar 5 | No algorithm, determinism, cue, or fairness measurement is provided. |
| `[OBSERVED]` | Store media/description uses readable silhouettes, impact framing, opponent confrontation, location-specific color, and occasional zoom/slow-motion finishes. | `achilles-steam-source.md#Presentation language` | Media language is not permission to copy a frame, pose, camera path, UI, art, or animation. |
| `[OBSERVED]` | The refreshed page reports 56 recent reviews at 77% positive and 2,093 purchaser reviews at 78% positive. | `achilles-steam-source.md#Store promise` | These aggregate ratings do not isolate which pillar caused sentiment. |

## What the benchmark is useful for

| Status | Play-concept question | Cinder Court translation | What is not transferred |
|---|---|---|---|
| `[INFERENCE]` | Do attacks feel like decisions because another defense becomes unavailable? | Make HongT’s shipped strike windows, oil-paid dash, and proposed timed Witness Guard visibly exclusive. | No stamina bar, input map, counter animation, numerical timing, or combat text is copied. |
| `[INFERENCE]` | Does enemy coordination create a readable combat puzzle? | Present existing Hard/Nightmare attack tokens, ring slots, flank priority, guardian angle, and court hazards as deterministic court procedure. | No named AI technology, proprietary behavior, formation, code, or animation is copied. |
| `[INFERENCE]` | Can players explain why their build fights differently? | Explain current equipment/stats/growth/skills/guardians through Force/Mobility/Testimony verdict axes and verify three viable archetypes. | No constellation layout, weapon roster, skill names, UI composition, or progression curve is copied. |
| `[INFERENCE]` | Does a boss confrontation test mastery rather than only health throughput? | Bind each court-role phase to one action answer and one stage hazard/guardian answer, with measurable punish windows. | No named hero, beast, mythology, boss anatomy, moveset, arena, dialogue, or finisher is copied. |
| `[INFERENCE]` | Does a location change tactics rather than merely palette? | Keep vents, pillars, altars, currents, pylons, and ash walls two-sided/decision-bearing and trace them to court functions. | No handcrafted layout, ruin style, location name, prop set, quest, secret, or art direction is copied. |
| `[INFERENCE]` | Can impact staging clarify stakes without obscuring play? | Use HongT’s existing boss role intro, phase pips, court colors, bounded hit feedback, and reduced-motion static equivalents. | No slogan, shot reproduction, slow-motion timing, camera move, pose, typography, or asset is copied. |

## Current HongT comparison

| Status | Axis | Observed HongT foundation | Gap to test, not assume |
|---|---|---|---|
| `[OBSERVED]` | Committed strike | Dungeon has a 0.30/0.30/0.42 s three-hit chain, 58/58/87 base sequence, charge follow-through, and dash cancel with cost/cooldown. | `[TARGET]` Measure whether players perceive opportunity cost; cycle 10 must not change cycle-9 numbers merely to resemble the benchmark. |
| `[OBSERVED]` | Dodge | Dash is 190 px/0.22 s, invulnerable, costs 8 oil, and has 1.6 s base cooldown with a ×0.55 growth floor. | `[TARGET]` Distinguish deliberate route/threat dodge from panic use. |
| `[OBSERVED]` | Guard | The current kit has a duration shield skill and Defence animation state, but no established timed player guard/counter rule. | `[TARGET]` Prototype Witness Guard only after a director-approved Stage-1 amendment; do not relabel the current shield as proof. |
| `[OBSERVED]` | Coordination | Hard/Nightmare use deterministic attack tokens (3/4), an eight-slot holding ring, and flank bias 0.75. Story/Normal retain prior behavior. | `[TARGET]` Verify players can identify the active attacker and token handoff; source claims do not answer HongT readability. |
| `[OBSERVED]` | Build authorship | Three equipment slots, three meta axes, three in-run growth axes, four skills, guardian roster/commands, and room preparation already exist. | `[TARGET]` Prove ≥3 independently viable strategies, no >50% dominance, and ≤1.3× median pair EV. |
| `[OBSERVED]` | Boss duel | Stage-keyed court-role profiles provide two/three phases and deterministic vectors for cadence, movement, reach, damage, telegraph, escorts, and health. | `[TARGET]` Prove phase duration ≥2.17 s, ≥70% answer identification, and impression ≥4/5. |
| `[OBSERVED]` | Environment | Six court hazards alter damage, pathing, oil, protection, and/or both-side displacement/damage on fixed schedules. | `[TARGET]` Prove ≥40% of successful 30–90 s loops use a recognized tactical environment event. |
| `[OBSERVED]` | Presentation/accessibility | Event-based hit feedback, boss/clear beats, speaker palettes, reduced-motion preference, and WebGL post-effect watchdog exist. | `[TARGET]` Prove ≤100 ms feedback, cue cap ≤3, 375×667 readability, and 0 unresolved S1/S2 complaints. |

## Product principles derived without copying

1. `[TARGET]` **Commitment must be local to HongT’s resources.** Strike recovery, lantern oil, dash cooldown, guardian position, and deterministic timing are sufficient; do not import a stamina economy.
2. `[TARGET]` **Coordination must be inspectable.** The same config and input sequence must preserve the same enemy order; presentation can reveal token ownership but never secretly alter it.
3. `[TARGET]` **Build authorship is behavioral.** A valid build changes preferred action/environment answers in at least two encounter types; larger numbers alone do not pass.
4. `[TARGET]` **Boss memory belongs to court office + room rule.** A player should remember why the Warden/Tactician/Sovereign/Keeper/Sentinel/Magistrate role acted and what court machinery answered it.
5. `[TARGET]` **Environment is a rules surface.** A room’s color is supporting evidence; tactical interaction and worldview traceability are the claim.
6. `[TARGET]` **Impact has an accessibility twin.** Any zoom, shake, trail, flash, hit-stop, or slow-motion accent must have a static shape/text/audio equivalent that preserves timing and information.

## Five-cycle use of the benchmark

| Status | Cycle | Permitted use | Required proof | Explicit prohibition |
|---|---|---|---|---|
| `[TARGET]` | 9 — baseline | Ask whether the current deterministic court already has a striking identity. | G8 impression measured at entry: ≥4/5; G5 parity remeasurement; duplicate ownership named. | No action-system or visual overhaul derived from the source. |
| `[TARGET]` | 10 — committed verdicts | Use “action opportunity cost” as a design question for HongT strike/dodge/guard fixtures. | ≥80% threats expose ≥2 valid answers; no answer >60% selection; deterministic replay. | No stamina import, copied timings, animations, moves, or UI. |
| `[TARGET]` | 11 — ordered host | Use “coordinated enemies” as a readability/fairness question. | ≥70% coordinated waves show handoff + reposition; cues ≤3; ≥80% damage attribution. | No named AI system, behavior cloning, or random adaptation. |
| `[TARGET]` | 12 — authored build/duel | Use build explanation and duel mastery as outcome questions. | ≥3 viable archetypes; phase ≥2.17 s; ≥70% answer identification; impression ≥4/5. | No skill-tree shape, gear roster, named opponents, mythology, arena, or finisher reproduction. |
| `[TARGET]` | 13 — NAN submission | Use storefront clarity as a communication audit: can a first player state HongT’s promise from the build? | G4/G6/G1 final; 0 S1/S2 readability complaints; all visible content traceable. | No slogan, screenshot composition, trailer shot, typography, prose, art, or asset transfer. |

## Originality quarantine

- `[TARGET]` The external title, setting, mythology, named characters, legendary creatures, slogans, prose, art, screenshots, interface, level layouts, boss anatomy, animation, audio, prompts, code, and assets remain outside HongT production.
- `[TARGET]` Only abstract, widely applicable play questions are admitted: commitment, defense choice, coordinated pressure, build explanation, duel mastery, environment tactics, and readable impact.
- `[TARGET]` Every admitted direction is renamed in current/future Cinder Court terms, expressed through HongT’s deterministic architecture, and linked to numeric acceptance in [concept.md](../concept.md), [core-loop.md](../core-loop.md), [presentation-spec.md](../presentation-spec.md), and [worldview.md](../worldview.md).
- `[TARGET]` If a proposed element cannot be explained without showing or naming the benchmark, it is rejected.

## Evidence limitations and next measurement

- `[OBSERVED]` The source supplies no stamina values, input windows, enemy-AI algorithm, build win rates, boss phase durations, accessibility behavior, or performance data.
- `[INFERENCE]` It therefore supports **which questions to ask**, not **which numbers to ship**.
- `[TARGET]` Cycle 9 QA must measure the current Cinder Court G8 now instead of carrying the unknown forward: the submitted novelty candidate must appear in ≤2 of ≥5 surveyed comparable titles **and** achieve QA impression ≥4/5. Cycles 10–13 may cite this analysis only beside HongT-owned prototype/playtest evidence.
