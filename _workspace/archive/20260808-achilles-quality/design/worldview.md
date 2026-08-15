# Worldview — Cinder Court (G1 source of truth)

- `[OBSERVED]` Active program run-id: `20260808-achilles-quality`; public beat: **NAN 2026 final submission**.
- `[OBSERVED]` This consolidation preserves the court/lantern lineage previously recorded in this file, `deep-interview-cinder-court-dungeon-revival.md`, `StoryCatalog`, and `StageCatalog`.
- `[TARGET]` Cycle 9 preserves this worldview during **Stage 2 / Phase 2a** evidence gathering; cycles 10–13 may add only terms and mechanics traceable to the rules below.

Status convention: all prose and rows are explicitly marked. A heading carrying a status applies that status to its full subsection unless a row overrides it.

## `[OBSERVED]` Premise

The **Cinder Court** is a prison made from memory. A betrayed oath sealed the Lantern Reaver’s missing testimony inside **guardian echoes**. The Reaver descends with a lantern, restores those echoes, and exposes the corrupt court office that turned judgment into erasure.

The lantern is not generic magic. It is the player’s instrument for revealing testimony, paying for decisive acts, and carrying recovered truth between chambers. The court is not a kingdom or pantheon. It is a failed institution whose archive, witnesses, judgment, and enforcement machinery became physical rooms and hazards.

## `[TARGET]` Player fantasy

The player is an investigator and executor in the same body. Each exchange follows a court verb:

1. **Read** the testimony: telegraph, formation, hazard phase, guardian angle.
2. **Commit** a verdict: strike, lantern dodge, or future Witness Guard.
3. **Turn** the court: move the host into currents/walls, break sealed testimony at a pylon, hold an altar, or place/recall a guardian echo.
4. **Adjudicate**: claim oil, experience, equipment, memory, relics, or the next phase.

This preserves HongT’s original worldview because combat verbs are explanations of the court’s machinery, not imported mythology.

## `[OBSERVED]` Spatial lineage: nine stages in three acts

| Status | Act | Stages | Institutional meaning |
|---|---|---|---|
| `[OBSERVED]` | I — Record | `cinder-span` 재의 다리 (Cinder Span) → `ember-gallery` 불씨 회랑 (Ember Gallery) → `abyss-chancel` 서약의 성당 (Abyss Chancel) | Entry records, oaths, and testimony accumulate before judgment. |
| `[OBSERVED]` | II — Testimony/Judgment | `witness-well` 증언의 우물 (Witness Well) → `echo-throne` 메아리 왕좌 (Echo Throne) → `ash-verdict` 재의 판결 (Ash Verdict) | Testimony is examined, distorted, and converted into judgment. |
| `[OBSERVED]` | III — Enforcement | `cinder-sluice` 재의 수문 (Cinder Sluice) → `ember-bastion` 불씨 요새 (Ember Bastion) → `ash-march` 재의 행진 (Ash March) | Records are erased, false testimony is fortified, and judgment becomes a marching sentence. |

`[OBSERVED]` The nine logical stages resolve through six deterministic simulation anchors. Logical reuse is acceptable only when player-visible naming, story, lighting, hazards, and boss role still trace to the logical stage’s institutional function.

## `[OBSERVED]` Hazard lineage: court function made physical

| Status | Hazard | Court meaning | Shipped tactical meaning |
|---|---|---|---|
| `[OBSERVED]` | Ember vent | Entry examination | Periodic warning then player damage. |
| `[OBSERVED]` | Obsidian pillar | Hardened oath | Blocks and pushes player and enemies out of occupied space. |
| `[OBSERVED]` | Relic altar | Testimony stand | Rewards uninterrupted presence with lantern oil. |
| `[OBSERVED]` | Tide current | Record erasure | Pushes player and enemies on a fixed six-second schedule. |
| `[OBSERVED]` | Ember pylon | Sealed false testimony | Reduces damage to protected enemies until struck down; its destruction is the reward event. |
| `[OBSERVED]` | Ash wall | Advancing sentence | Telegraphs, advances, holds, and recedes while damaging player and enemies. |

`[TARGET]` No decorative hazard may become mechanically authoritative. If a hazard changes damage, position, targeting, or reward, its shape and story must name the same court function.

## `[OBSERVED]` Stage epithet lineage

| Status | Stage | Card epithet | Meaning |
|---|---|---|---|
| `[OBSERVED]` | 재의 다리 (Cinder Span) | 분출구 입문 | The court tests entry rhythm at the bridge. |
| `[OBSERVED]` | 불씨 회랑 (Ember Gallery) | 불씨 윤무 | Rotating vents force the pace of reading records. |
| `[OBSERVED]` | 서약의 성당 (Abyss Chancel) | 흑요석 미로 | Oaths have hardened into sightline and path blockers. |
| `[OBSERVED]` | 증언의 우물 (Witness Well) | 쌍 제단 | Opposing stands demand sustained testimony under pressure. |
| `[OBSERVED]` | 메아리 왕좌 (Echo Throne) | 왕좌의 조류 | Judgment’s wake crosses the chamber; altar timing belongs to its pause. |
| `[OBSERVED]` | 재의 판결 (Ash Verdict) | 판결의 방벽 | Defensive pylons shelter false testimony. |
| `[OBSERVED]` | 재의 수문 (Cinder Sluice) | 해류 숙달 | Erasure is resisted by crossing or exploiting the current. |
| `[OBSERVED]` | 불씨 요새 (Ember Bastion) | 방벽 숙달 | Layered pylons make protection itself the target. |
| `[OBSERVED]` | 재의 행진 (Ash March) | 집행 수렴 | Walls, pylon, altar, and vents become one execution machine. |

`[TARGET]` Localized player-facing epithets remain short court-function noun phrases. `StageEntry.Epithet`/catalog strings must trace to this table or a director-approved successor row.

## `[OBSERVED]` Authority and speaker grammar

| Status | Speaker class | Function | Voice rule |
|---|---|---|---|
| `[OBSERVED]` | Watcher | Ambient court narration | States what the institution is doing; never claims personal memory. |
| `[OBSERVED]` | Boss court role | Active judgment/enforcement | Short sentence-form declarations; role title, not a borrowed proper name. |
| `[OBSERVED]` | Warden/guardian witness | Recovered recollection | Reveals what was erased or falsified; never becomes omniscient. |
| `[TARGET]` | Lantern Reaver | Player intention | Speaks only at irreversible story decisions or final result; combat mastery is conveyed through action, not chatter. |

`[OBSERVED]` Existing boss vocabulary is institutional: Warden, Tactician, Sovereign, Keeper, Sentinel, Magistrate, and Monarch presentation roles. New roles must remain court offices or enforcement functions. External mythic titles and named heroes are prohibited.

## `[OBSERVED]` Color and material language

| Status | Signal | Canonical color family | Meaning |
|---|---|---|---|
| `[OBSERVED]` | Charcoal/blue-black | Ground, stone, inactive court | Space and institutional weight. |
| `[OBSERVED]` | Ember orange | Threat, fire, hostile execution | Incoming action or damage source. |
| `[OBSERVED]` | Spectral cyan | Memory, guardian echo, recoverable truth | Ally, testimony, recall, or memory objective. |
| `[OBSERVED]` | Gold | Reward, completed judgment, earned office | Confirmed gain or verdict. |

`[TARGET]` Color is never the only carrier. Every critical state also needs shape, position, label, or timing: active attacker chevron, broken-ring unguardable mark, pylon body/aura boundary, altar circle/hold progress, and boss phase pip.

## `[OBSERVED]` Meta-progression lineage

### Equipment rises from ash to office

| Status | Slot | T0 | T1 | T2 | T3 | T4 | T5 |
|---|---|---|---|---|---|---|---|
| `[OBSERVED]` | 무기 | 잿날 | 담금날 | 벼림날 | 선고날 | 심판날 | 판결인 |
| `[OBSERVED]` | 랜턴 | 잿등 | 밀랍등 | 서약등 | 기록등 | 증언등 | 진실등 |
| `[OBSERVED]` | 망토 | 잿천 | 무명포 | 증인포 | 기록포 | 선고포 | 집행포 |

`[INFERENCE]` The progression works when it reads as restored authority rather than a generic rarity ladder: tool → record → testimony → office.

### Verdict Pact

`[OBSERVED]` A cleared court may be reopened under a harder optional pact. The pact strengthens the stage’s identity gimmick and pays doubled relics on success; it is opt-in per sortie and is not persisted as permanent campaign state.

`[TARGET]` A pact may intensify schedule, placement, or coordination, but may not contradict the deterministic stage identity or sell power outside the G5 bands.

### Guardian echoes

| Status | id | Court epithet | Origin |
|---|---|---|---|
| `[OBSERVED]` | `ember-cohort` | 첫 서약의 증인 | 재의 다리 첫 판결의 동행 보상. |
| `[OBSERVED]` | `shade-echo` | 성당의 메아리 | 서약의 성당 판결의 메아리. |
| `[OBSERVED]` | `possessed-echo` | 왕좌의 메아리 | 메아리 왕좌 판결의 메아리. |
| `[OBSERVED]` | `scout-echo` | 행진의 메아리 | 재의 행진 판결의 메아리. |
| `[OBSERVED]` | `ember-cohort-echo` | 정예의 잿불 | 정예 추출(잿불 계열)의 메아리. |

`[TARGET]` Guardian roster growth means recovering perspectives from the court. A guardian is never a summoned deity, licensed guest character, or disconnected pet.

## `[TARGET]` Action terms for cycles 10–13

| Status | Term | Worldview meaning | Mechanical boundary |
|---|---|---|---|
| `[TARGET]` | Committed Strike | The Reaver enters a verdict into the record. | Uses the shipped chain windows; active hit cannot freely become guard. |
| `[TARGET]` | Lantern Dodge | The Reaver carries testimony out of an invalid sentence. | Uses oil/cooldown and creates space; not a permanent invulnerability engine. |
| `[TARGET]` | Witness Guard | The Reaver contests a guardable claim at contact. | Proposed timed stance/counter; not implemented in cycle 9. |
| `[TARGET]` | Acting Bailiff | The host member currently authorized to attack. | Presentation of an existing/future deterministic attack token. |
| `[TARGET]` | Witness Line | Host members holding the ring while awaiting a token. | Lower-salience formation state, never a hidden extra attacker. |
| `[TARGET]` | Sealed Testimony | A group protected by a live pylon. | Protection ends when the pylon is destroyed. |

`[TARGET]` These are future HongT terms. They do not authorize code, balance, string, or localization changes by themselves.

## `[TARGET]` Narrative and presentation traceability

Every player-visible content item must answer all five fields:

| Status | Required field | Passing example class | Failure class |
|---|---|---|---|
| `[TARGET]` | Court function | “This wall is an advancing sentence.” | Hazard exists only because the room needed decoration. |
| `[TARGET]` | Memory stake | “This echo preserves erased testimony.” | Reward is an unrelated magic soul. |
| `[TARGET]` | Player verb | Read / commit / turn / adjudicate. | Spectacle with no player decision. |
| `[TARGET]` | Signal family | Charcoal, ember, cyan, or gold plus shape. | New unexplained palette or color-only meaning. |
| `[TARGET]` | Canonical owner | This file + catalog/spec path. | String or effect with no source-of-truth path. |

`[TARGET]` G1 passes only with **0 un-waived lore violations** and **100% traceability** across shipped strings, effects, and scenarios. QA evidence belongs in `../qa/gate-measurements.md#g1`; waivers require director reasoning and expiry.

## `[TARGET]` Five-cycle worldview evolution

| Status | Cycle | Allowed worldview change | Prohibited drift |
|---|---|---|---|
| `[TARGET]` | 9 | None beyond audit corrections; prove the current court identity. | Retheming to satisfy an external benchmark. |
| `[TARGET]` | 10 | Add action terms only after a mechanic is approved. | Borrowed resource names, mythology, or heroic lineage. |
| `[TARGET]` | 11 | Make coordination visible as court procedure. | Enemies described as a foreign army/faction unrelated to the court. |
| `[TARGET]` | 12 | Deepen boss office, testimony, and build-verdict links. | Named external heroes, beasts, or copied duel framing. |
| `[TARGET]` | 13 | Final G1 copy/effect audit and localization consistency. | Late lore expansion before NAN submission. |

## `[TARGET]` Cross-artifact contract

- `[TARGET]` [concept.md](concept.md) may propose play identity but cannot redefine lore without updating this file.
- `[TARGET]` [core-loop.md](core-loop.md) must express Read → Commit → Turn → Adjudicate.
- `[TARGET]` [presentation-spec.md](presentation-spec.md) must use this signal and speaker grammar.
- `[OBSERVED]` The external [benchmark analysis](trend-survey/achilles-analysis.md) is calibration only; this file contains no imported names, mythology, prose, art, or assets.
