# AMENDMENT — Companion Autonomy Sim Revision

**Status: IMPLEMENTED 2026-08-07 in commit `b9a728c`.** EditMode 316/316 passed
(evidence `_workspace/current/engineering/unity-logs/test-results-080205.xml`).
The five constants landed exactly as proposed; **one deliberate divergence** is
recorded in §"Divergences from this proposal" below. Read-only analysis of
`/Users/jangyoung/orca/workspaces/HongT/main`. Amends `docs/SIM_SPEC_HACKSLASH.md`
§4, §12, §13, and DRAFT Amendment #6 only as specified. Preserves all frozen
`Arena`, `Prologue`, and zero-companion `Dungeon` behavior.

## Divergences from this proposal (as built)

**D-1. `CompanionBehavior` was NOT extended with `Engage = 2`.** The proposal made
engagement a third enum member. As built, `CompanionBehavior` stays the frozen
Amendment #3 pair `{Follow, Hold}` and engagement is exposed as a derived per-slot
flag, `IHackSnapshot.CompanionEngagedAt(slot)`, alongside
`CompanionTargetIdAt(slot)`. Reason: `CompanionBehavior` is the **commanded**
surface (hold/recall come from player input and are persisted), while engagement is
**derived every tick** from geometry. Folding a derived state into the commanded
enum would let a command be read back as a derived one, and would put a value into
persistence that no command can ever produce. Tests 4/8/13 were retargeted onto
`CompanionEngagedAt` accordingly.

**D-2. Engagement can be true while the lock reads 0.** Pursuit runs before the
swing inside one tick, so a slot may close the gap and finish its own target in the
same tick; the lock is then released immediately so the snapshot never publishes a
lock on a corpse. "Engaged implies a live lock" is therefore NOT an invariant —
see `llm-wiki/wiki/hongt-companion-autonomy-tick-order-trap.md`.

**D-3. Digest scope, confirmed by measurement.** Arena, Prologue and
companion-less Dungeon digests are byte-identical to pre-amendment and are pinned
as literals in `CompanionAutonomyTests`. Runs *with* companions do change their
digest (1-companion cinder-span: kills 15→14, relics 3→4, hp 118→112 on the
standard 1800-tick script). That was approved as decision D2/D3 of
`_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md`.

## Summary

Today the companion is a *leash-bound turret*: rigidly pinned to an 80 px anchor
behind the player and can only strike whatever falls inside a 200 px radius
measured **from its own anchored position**. It never closes distance, never
breaks the leash to finish a target, never returns on its own. This amendment
proposes a bounded autonomy layer — independent target acquisition with target
lock, a leash-limited pursuit state, and automatic return-to-anchor —
implemented entirely with counter/modular arithmetic so the RNG-free determinism
contract (§13) holds.

## In Scope

- Frozen contract quotation and current-implementation audit ([OBSERVED] below).
- A numeric before/after table for companion follow/engage/pursue/return.
- Determinism-preservation argument (no RNG, deterministic tie-break, fixed tick order).
- The list of new seed-fixed Digest assertions required before promotion.
- Draft FROZEN wording (`§4` replacement text + `§13` addendum).
- Risk and rollback plan.

## Out of Scope

- Any code edit, test edit, or FROZEN-file edit. This turn was read-only by contract.
- Companion HP/targetability (companions stay untargetable — §4, Amendment #3 non-goals).
- Companion skills, equipment, persistence, cooldown UI, new `SimEvents`/`SimInput`.
- Any change to `Arena`, `Prologue`, plain-campaign, or Amendment #4 `GuardianResonance`.

## [OBSERVED] — Current frozen contract

`docs/SIM_SPEC_HACKSLASH.md` §4 "동료 (1슬롯 동행)", combat bullet, verbatim:

> - 전투: 플레이어로부터 80 px 오프셋 추종, 1.1 s마다 200 px 내 최근접 적에게
>   **플레이어 피해의 60%** (상성 무원소). 피격 대상 아님(untargetable).

Amendment #3 constrains this: `CompanionBehavior.Hold` locks the companion's
coordinates (only follower movement is skipped; attack logic still runs);
`CompanionBehavior.Follow` is the default; Recall resumes the 80 px-offset
follower path and must not teleport.

Amendment #4 already carves a temporary, room-local exception for cadence/range/
damage via `GuardianResonance`: `cadence = max(0.5, 1.1×(1−0.10m))`,
`range = 200 + 20m`, `damage = ordinary × (1 + 0.10m)`.

Amendment #6 (DRAFT) adds 0..3 slots, per-archetype tuples, lateral fan-out
`{0, +64, −64}` px, with `ember-cohort` pinned to the §4 tuple so the legacy
single-companion digest is bit-identical.

§13: "전 모드 RNG 금지. 정예 판정·추출·동료 공격 주기 전부 모듈러/카운터 산술."

## [OBSERVED] — Current Sim implementation

`Assets/Scripts/Sim/HackTypes.cs` §4 companion constants:
`CompanionFollowOffset=80`, `CompanionAttackInterval=1.1`,
`CompanionAttackRange=200`, `CompanionDamageScale=0.6`,
`CompanionAttackDisplay=0.25`. Fan-out `{0, 64, -64}`; per-archetype tuples
Scout `0.85/240/0.50`, Shade `1.30/260/0.65`, Possessed `1.45/150/0.80`,
EmberCohort/default → §4 tuple.

`Assets/Scripts/Sim/CinderSim.cs:1360-1407` `UpdateCompanionSlot(slot, dt)` is the
entire autonomy surface: (1) `Follow` steps toward `(player.X − 80·facing,
player.Y + fanout)` at `_playerSpeed`, Y scaled by `SimConfig.YMoveScale`, with
overshoot clamp; `Hold` skips movement, attack logic still runs. (2) show/timer
decay; early-return while swinging. (3)
`NearestEnemyIndex(_companionX[slot], _companionY[slot], _companionAttackRange[slot])`
— **range measured from the companion's own position**, no way to reduce it.
(4) no target → face player, return; else face target, reset cadence, deal
`_playerDamage * _companionDamageScale[slot]`.

**[CONFIRMED] tie-break** (`CinderSim.cs:1041` `NearestEnemyIndex`): the winner
is chosen by `best < 0 || squared < bestSquared` — strict `<`, so **first index
wins ties** (scan order over `_enemies`). A7.1's lock only retains that index;
no new ordering decision.

**[CONFIRMED] tick order** (`CinderSim.cs:751-752`): per step the sequence is
`CastSkills → UpdatePlayer → UpdateCompanionBehavior → UpdateCompanion →
UpdateEnemies → UpdateBossPhase`. Companion update runs **after player movement
and before enemy update**, exactly as the follower does today; A7 keeps this slot.

**Consequence [INFERENCE]:** no pursuit state, no target lock, no leash, no
return logic — precisely the four things "autonomy" requires.

## [OBSERVED] — View-only gaze (G1)

`Assets/Scripts/View/GameView.cs` (~L560-586, `SyncViews`) computes a
View-side companion gaze: nearest living enemy inside
`CompanionAttackRange` sets `gazeYaw` (iso-weighted, snapped to 22.5°); a
squared-distance test `< CompanionFollowOffset² × 2.25` (= (80×1.5)² = 14 400 px²,
a **120 px** rest radius) decides `restIdle`. It only **reads** snapshot state
and calls `view.SyncCompanion(...)` — never writes sim. This proposal
deliberately keeps gaze View-only so it cannot enter the digest.

## Proposed revision (A7)

Four additive Sim behaviors, all `Dungeon`-gated, all per-slot, all driven by the
existing per-slot arrays:

- **A7.1 Target lock.** A companion that acquires a target keeps it
  (`_companionTargetId[slot]`) until the target dies, leaves `LeashRadius`, or
  `TargetLockSeconds` elapses. Removes the per-tick target thrash.
- **A7.2 Independent pursuit.** When the locked target is outside attack range but
  inside `LeashRadius` (measured from the **anchor**), behavior becomes `Engage`:
  the companion moves toward the target at `PursuitSpeedScale × _playerSpeed`
  instead of toward the anchor.
- **A7.3 Automatic return.** When the target is lost or the anchor distance
  exceeds `LeashRadius`, behavior returns to `Follow` (never a teleport — same
  rule as Amendment #3 recall).
- **A7.4 Acquisition radius ≠ attack range.** Acquisition uses `AcquireRadius`
  from the **anchor**; attack still uses per-archetype range from the companion's
  own position, so §4/D6.3 damage geometry is untouched.

`Hold` continues to dominate: a held companion never pursues (movement skipped,
attack logic unchanged) — Amendment #3 preserved verbatim.

### Numeric table (before → after)

| Quantity | Before [OBSERVED] | After [TARGET] | Note |
|---|---|---|---|
| Follow offset | 80 px behind facing | **80 px (unchanged)** | anchor frozen (§4, D6.4) |
| Slot fan-out | `{0, +64, −64}` px | **unchanged** | D6.4 |
| Attack range (ember/fallback) | 200 px from companion | **200 px (unchanged)** | §4 gate |
| Per-archetype range | 240 / 260 / 150 px | **unchanged** | D6.3 gate |
| Cadence | 1.1 s (0.85/1.30/1.45) | **unchanged** | §13 counter arithmetic |
| Damage | `playerDamage × 0.60` (×0.50/0.65/0.80) | **unchanged** | §4 gate |
| Follow move speed | `_playerSpeed` (218 base) | **unchanged** in `Follow` | digest parity |
| **Acquire radius** | — (= attack range implicitly) | **`CompanionAcquireRadius = 300` px** from anchor | new |
| **Leash radius** | — (∞ tether, 0 slack) | **`CompanionLeashRadius = 320` px** from anchor | new; > acquire so an acquired target is always reachable |
| **Pursuit speed** | — | **`CompanionPursuitSpeedScale = 1.05` × `_playerSpeed`** | must exceed 1.0 to close on a player-speed foe; capped low so it cannot lead |
| **Target lock duration** | 0 (re-picked each swing) | **`CompanionTargetLockSeconds = 2.0` s** (120 ticks) | integral at 1/60 |
| **Return dwell** | — | **`CompanionReturnGraceSeconds = 0.35` s** | prevents leash-edge oscillation |
| Behavior enum | `{Follow, Hold}` | **unchanged** — see divergence D-1 | engagement ships as a derived per-slot flag, not an enum member |
| Gaze radius (View) | `80 × 1.5 = 120` px | **unchanged, still View-only** | must not enter digest |

Chosen relations, not tastes: `AcquireRadius (300) < LeashRadius (320)` guarantees
a companion never locks a target it is forbidden to reach; `LeashRadius (320) =
4 × FollowOffset (80)` keeps the leash in anchor units; `PursuitSpeedScale 1.05`
gives a 10.9 u·s⁻¹ closing margin on a 218 u·s⁻¹ player without leading the formation.

### Determinism preservation

1. **No RNG.** Every new quantity is a compile-time constant compared against
   accumulated fixed-step floats — the arithmetic class §13 already mandates.
2. **Tie-break is index order.** [CONFIRMED] `NearestEnemyIndex` uses strict `<`
   (first-wins); A7.1 lock only retains that index.
3. **Tick order unchanged.** [CONFIRMED] `UpdateCompanion` stays at
   `CinderSim.cs:752`, after `UpdatePlayer`, before `UpdateEnemies`; slot loop
   `0 → count−1` adds no cross-slot coupling.
4. **Zero-companion parity is structural.** `_companionCount == 0` skips the loop.
5. **Single-companion parity is behavioral, not structural** — the one real digest
   risk. A legacy `ember-cohort` run diverges the instant an enemy sits between
   200 and 300 px of the anchor (companion now pursues). **Declared, intentional**
   — unlike #3/#4/#6, A7 cannot claim single-companion digest parity. See Risks.
6. **No new `SimEvents`/`SimInput`.** `Engage` is inferred from state, exposed
   read-only on the snapshot; migrated snapshots default to `Follow`.
7. **Float determinism.** Pursuit reuses the existing normalize-and-overshoot-clamp
   step form verbatim, including `SimConfig.YMoveScale` on Y.

### Required new deterministic tests (`Assets/Tests/EditMode/HackSimTests.cs`, `CompanionAutonomy_*`)

| # | Test | Assertion |
|---|---|---|
| 1 | `ZeroCompanionRunsPreserveLegacyDigests` | Arena / Prologue / zero-companion Dungeon digests byte-identical to pre-amendment |
| 2 | `IdenticalInputsYieldIdenticalDigest` | same config + scripted input → digest-equal at ticks 60/600/3600 |
| 3 | `NoEnemyInAcquireRadiusReproducesLegacyFollower` | all enemies beyond 300 px → follower coords + cadence bit-identical to §4 |
| 4 | `EngagesOnlyWithinLeash` | anchor-distance 310 → `Engage`; 330 → stays `Follow` (boundary exact) |
| 5 | `TargetLockSurvivesANearerLateArrival` | nearer enemy mid-lock does not steal the target until expiry/death/leash-exit |
| 6 | `ReturnsToAnchorWithoutTeleport` | after target death, per-tick displacement ≤ `_playerSpeed × dt` (+Y scale), ends at anchor+fanout |
| 7 | `HoldSuppressesPursuit` | held companion with in-leash target does not move yet still swings on cadence |
| 8 | `RecallWinsAndCancelsEngage` | simultaneous hold+recall → `Follow`, `Engage` cleared, no teleport |
| 9 | `PerSlotEngageIsIndependentAndDeterministic` | 3-slot run: each slot may differ; digest reproducible across two runs |
| 10 | `GuardianResonanceRangeStillAppliesOnTopOfAcquire` | Amendment #4 `range = 200+20m` modifies **attack** range only |
| 11 | `RestartResetsBehaviorAndTarget` | `Restart` → `Follow`, target cleared, positions snapped to anchor+fanout |
| 12 | `InertInArenaAndPrologue` | acquire/leash/pursuit constants have zero effect outside Dungeon |
| 13 | `SnapshotMigrationDefaultsToFollow` | snapshot lacking the field → `Follow`, never `Engage` |

Tests 1, 2, 3, 9 are the digest gates; the rest are behavioral gates.

### Draft FROZEN contract wording

Replacement for `docs/SIM_SPEC_HACKSLASH.md` §4 combat bullet:

> - 전투: 플레이어로부터 80 px 오프셋 추종(D6.4 팬아웃 가산). 앵커 기준 **300 px
>   취득 반경** 내 최근접 적을 표적으로 **락**(2.0 s 또는 사망/리시 이탈까지 유지)하고,
>   사거리 밖이면 앵커 기준 **320 px 리시** 안에서 `_playerSpeed × 1.05`로 **추격**한다.
>   표적 상실 또는 리시 초과 시 0.35 s 유예 후 앵커로 **자동 복귀**(텔레포트 금지).
>   공격은 자기 위치 기준 사거리(§4 200 px / D6.3 아키타입 값) 내 표적에게 아키타입
>   케이던스마다 **플레이어 피해의 60%**(D6.3 스케일, 상성 무원소). 피격 대상 아님.

New subsection, appended after Amendment #6:

> ## Frozen Contract Amendment #7 — Companion Autonomy (PROPOSAL)
> **Status: PROPOSAL — not implemented, not frozen.** Amends §4, §12, §13 and
> Amendment #6 only. Arena, Prologue, zero-companion Dungeon remain byte-identical.
> - **A7.1 Scope.** Independent acquisition, leashed pursuit, automatic return.
>   Hold/Recall (#3) keep absolute precedence. No HP/targetability/skill/
>   persistence/SimInput/SimEvents change.
> - **A7.2 Constants (`HackSpec`, append-only).** `CompanionAcquireRadius=300f`,
>   `CompanionLeashRadius=320f`, `CompanionPursuitSpeedScale=1.05f`,
>   `CompanionTargetLockSeconds=2.0f`, `CompanionReturnGraceSeconds=0.35f`.
>   Invariant: `AcquireRadius < LeashRadius`.
> - **A7.3 Behavior enum.** `CompanionBehavior` gains `Engage = 2` (append-only;
>   `Follow = 0` migration default). `Engage` is sim-derived, never commanded.
> - **A7.4 Determinism.** No RNG. Ties resolve by enemy array index. Slot order
>   and tick slot unchanged. Per-slot state only.
> - **A7.5 Declared digest break.** Single-companion Dungeon runs need NOT
>   reproduce their pre-amendment digest. Re-bless single-companion baselines in
>   the landing commit; retain old values as commented history.
> - **A7.6 View.** Companion gaze (`GameView`, 120 px) stays View-only.

### Risks and rollback

| Risk | Severity | Mitigation |
|---|---|---|
| Single-companion digest baselines break (A7.5) | High | Land constant + baseline re-bless in one commit; keep old digests as comments; test #1 guards zero-companion/Arena/Prologue |
| Companion outruns/leads the player at ×1.05 | Medium | Leash 320 px hard-caps the lead; test #4 pins boundary; single constant to retune |
| Leash-edge oscillation (Engage↔Follow per tick) | Medium | `ReturnGraceSeconds = 0.35` hysteresis + acquire<leash gap; test #6 asserts monotone return |
| Target lock ignores imminent threat | Low | 2.0 s cap and leash-exit break the lock |
| Interaction with Amendment #4 range | Medium | A7 scopes the offer to **attack** range only; test #10 |
| Interaction with Amendment #6 3-slot fan-out | Medium | Fan-out anchor-relative, pursuit per-slot, unsynchronised cadences; test #9 |
| Amendment #6 itself still DRAFT | High (process) | A7 must not be frozen before #6 is promoted |
| Perf: extra per-slot distance checks | Negligible | ≤3 slots × 1 extra Hypot/tick |

**Rollback:** the entire change is gated by the constants. Setting
`CompanionAcquireRadius = CompanionLeashRadius = CompanionAttackRange`,
`CompanionTargetLockSeconds = 0`, `CompanionPursuitSpeedScale = 1.0` reduces A7
exactly to the §4/#6 follower — no code removal. Full rollback = revert the single
commit and restore commented baselines. Tag `pre-companion-autonomy-<date>` before
landing (CLAUDE.md §5).

## Verification (before sign-off)

- `Unity -batchmode -runTests -testPlatform EditMode` — all Arena/Campaign
  regressions + `CompanionSlots_*` stay green; the 13 `CompanionAutonomy_*` pass.
- Diff recorded digests: only single-companion Dungeon baselines may move; any
  Arena/Prologue/zero-companion movement is a hard fail.
