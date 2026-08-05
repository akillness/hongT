# Motion Clip Survey — 뒹굴기 / 날아가기 / 발차기

**Scope:** read-only survey of authored motion, its wiring, and the sim state behind three requested
moves: 뒹굴기 (roll/dodge), 날아가기 (launch/knockback fly), 발차기 (kick).
**Repo:** `~/orca/workspaces/HongT/main` (Unity 6000.5.6f1 / URP / WebGL, Cinder Court)
**Survey commit:** assignment pinned HEAD `5383577`; HEAD advanced to `3e2e3a1` during the survey and
`Assets/Scripts/Sim/CinderSim.cs` was modified in the working tree by a sibling agent.
**All CinderSim.cs citations below were re-verified against the live file after that edit** — the
sibling's +14 lines landed below the cited regions and every line number here still resolves.
Other cited files (`ActorView.cs`, `GameView.cs`, `SimTypes.cs`, `HackTypes.cs`, `InputAdapter.cs`,
test fixtures, `CharacterImportPipeline.cs`) were untouched in `git status`.

**Bottom line up front:**

| Move | Sim support | View support | Art | Verdict |
|---|---|---|---|---|
| 뒹굴기 roll/dodge | **YES — complete** (i-frame dash) | **YES — wired** | `Dodging.fbx` (a sidestep, not a roll) | **Already shipping, but dungeon-only.** Cheapest win in the repo. |
| 날아가기 launch | **Enemies only** | **YES — wired via heuristic** | `Receive Uppercut To The Face.fbx` | **Half-built.** Player can never be launched; detection is a guess, not a flag. |
| 발차기 kick | **NONE** | **NONE** | **NONE** | **Missing at all three layers.** Only move of the three needing new art. |

Four authored clips are **DEAD** — `hit`, `critical`, `defence`, `show`. They are imported, they have
animator states, and nothing in the codebase can ever select them. That is ~1.04 MB of paid-for
motion sitting dark, and two of the four (`defence`, `critical`) are close cousins of what the user
is asking for.

---

## 1. Full inventory of authored motion

### 1a. Every file under `Assets/Art/Motion/`

30 files total: 14 `.fbx` + 14 `.fbx.meta` + 1 `.controller` + 1 `.controller.meta`.
No subdirectories (`[OBSERVED]` `find Assets/Art -type d` — `Assets/Art/Motion` has no children).
`.meta` files omitted from the table below; sizes/mtimes from `stat`.

| # | File | Size (bytes) | mtime | Table row |
|---|---|---|---|---|
| 1 | `Body Block.fbx` | 337,040 | 2026-08-04 15:16 | 8 `defence` |
| 2 | `Dodging.fbx` | 238,400 | 2026-08-04 15:16 | 7 `avoid` |
| 3 | `Dying.fbx` | 400,544 | 2026-08-04 15:16 | 9 `die` |
| 4 | `Hook Punch.fbx` | 256,464 | 2026-08-05 15:43 | 11 `attack2` |
| 5 | `Illegal Elbow Punch.fbx` | 258,048 | 2026-08-04 15:16 | 6 `critical` |
| 6 | `Mutant Roaring.fbx` | 386,992 | 2026-08-04 15:16 | 10 `show` |
| 7 | `Punching.fbx` | 213,120 | 2026-08-04 15:16 | 5 `attack` |
| 8 | `Receive Uppercut To The Face.fbx` | 221,760 | 2026-08-04 15:16 | 4 `bighit` |
| 9 | `Running.fbx` | 219,600 | 2026-08-04 15:16 | 2 `run` |
| 10 | `Standing 2H Magic Attack 01.fbx` | 299,712 | 2026-08-05 15:43 | 13 `cast` |
| 11 | `Standing Melee Combo Attack Ver. 2.fbx` | 346,944 | 2026-08-05 15:43 | 12 `attack3` |
| 12 | `Standing React Small From Left.fbx` | 195,200 | 2026-08-04 15:16 | 3 `hit` |
| 13 | `Unarmed Idle.fbx` | 253,008 | 2026-08-04 15:16 | 0 `idle` |
| 14 | `Walking.fbx` | 228,240 | 2026-08-04 15:16 | 1 `move` |
| — | `CinderActor.controller` | 28,321 | 2026-08-05 15:47 | *(generated output)* |

`[OBSERVED]` Total FBX payload 3,855,072 bytes (~3.68 MB).
`[OBSERVED]` Three clips carry a 2026-08-05 15:43 mtime — `Hook Punch`, `Standing 2H Magic Attack 01`,
`Standing Melee Combo Attack Ver. 2`. These are exactly the three View-only substates (rows 11–13),
i.e. the most recent motion authoring pass added the combo/cast variants. The controller was rebuilt
four minutes later at 15:47.

`[OBSERVED]` The mapping is **1:1 and total** — 14 FBX files, 14 table rows, no orphan FBX on disk and
no table row without a file. `ReimportClips` would throw `missing clip fbx` on a gap
(`Assets/Editor/CharacterImportPipeline.cs:242-243`).

### 1b. The clip table — every `(name, sourceClip, loop)` tuple

`Assets/Editor/CharacterImportPipeline.cs:34-51`. Array index **is** the animator condition value
(`BuildController` at `:301-308` adds one any-state transition per row with condition
`action Equals i`), so the row order is a hard contract.

| Idx | `action` | `file` (source clip) | `loop` | Source line |
|---|---|---|---|---|
| 0 | `idle` | `Unarmed Idle` | **true** | `CharacterImportPipeline.cs:36` |
| 1 | `move` | `Walking` | **true** | `:37` |
| 2 | `run` | `Running` | **true** | `:38` |
| 3 | `hit` | `Standing React Small From Left` | false | `:39` |
| 4 | `bighit` | `Receive Uppercut To The Face` | false | `:40` |
| 5 | `attack` | `Punching` | false | `:41` |
| 6 | `critical` | `Illegal Elbow Punch` | false | `:42` |
| 7 | `avoid` | `Dodging` | false | `:43` |
| 8 | `defence` | `Body Block` | false | `:44` |
| 9 | `die` | `Dying` | false | `:45` |
| 10 | `show` | `Mutant Roaring` | false | `:46` |
| 11 | `attack2` | `Hook Punch` | false | `:48` — View-only, combo 2nd |
| 12 | `attack3` | `Standing Melee Combo Attack Ver. 2` | false | `:49` — View-only, combo 3rd |
| 13 | `cast` | `Standing 2H Magic Attack 01` | false | `:50` — View-only, skill cast |

Rows 0–10 align index-for-index with the frozen
`ActorAction { Idle, Move, Run, Hit, BigHit, Attack, Critical, Avoid, Defence, Die, Show }`
(`Assets/Scripts/Sim/SimTypes.cs:12`). Rows 11–13 continue the animator's integer past the enum;
`SimActionCount = 11` (`:55`) marks the boundary.

`[OBSERVED]` All 14 states exist in the built controller — `grep 'm_Name:' CinderActor.controller`
returns `idle, move, run, hit, bighit, attack, critical, avoid, defence, die, show, attack2,
attack3, cast` plus the `action` parameter. **The animator is complete. The gap is upstream, in who
sets the integer.**

---

## 2. WIRED vs DEAD

An `ActorAction` reaches the animator through
`ActorView.ResolveActionValue` (`Assets/Scripts/View/ActorView.cs:334-343`), called from `Apply`
at `:505-506`, which pushes `SetInteger("action", …)` at `:509`.
A clip is **WIRED** only if some live code path produces its index.

`[OBSERVED]` Exhaustive grep of `Assets/Scripts/Sim` for `ActorAction.*` shows the sim emits **only
six** of its eleven enum values: `Idle`, `Move`, `Run`, `Attack`, `Avoid`, `Die`.
`ActorAction.Hit`, `.BigHit`, `.Critical`, `.Defence`, `.Show` **appear nowhere in the sim**.
A repo-wide grep for `ActorAction.(Show|Critical|Defence|Hit)\b` across all of `Assets/` returns
only `ActorView.cs:450` (a facing-yaw check that reads `Critical`, never assigns it) and
`PoseResolveTests.cs:329-347` (test-only diagnostic strings).

| Idx | Clip | Status | Driver / citation |
|---|---|---|---|
| 0 | `idle` | **WIRED** | `CinderSim.cs:511` init; `:1342`, `:1353`, `:1400`, `:1437`, `:1459` player; `:1690`, `:1710` enemy |
| 1 | `move` | **WIRED** | `CinderSim.cs:1342`, `:1437` player; `:1686` non-boss enemy |
| 2 | `run` | **WIRED** *(boss only)* | `CinderSim.cs:1686` — `enemy.State.IsBoss ? ActorAction.Run : ActorAction.Move`. Player never runs. |
| 3 | `hit` | **DEAD** | No emitter anywhere. Player damage (`CinderSim.cs:1522` `DamagePlayer`) sets no action; enemy damage (`DamageEnemy`) sets no action. The View flags hits by **color flash only** (`ActorView.cs:144`), never by pose. |
| 4 | `bighit` | **WIRED — recently revived** | `ActorView.cs:338` `if (knockbackLive) return (int)ActorAction.BigHit;` Driver: `ActorView.cs:153-158` — a damage frame whose positional speed exceeds 300 px/s sets `_knockbackTime = HackSpec.ComboKnockbackTime`. **Confirmed: the sim never emits BigHit; this is a View-side heuristic.** See §2a. |
| 5 | `attack` | **WIRED** | `CinderSim.cs:1336` (arena), `:1423` (dungeon combo), `:1635` (enemy) |
| 6 | `critical` | **DEAD** | Never assigned. Only read at `ActorView.cs:450` inside a facing-yaw condition — reachable code that can never be true for this value. `Illegal Elbow Punch.fbx` (258 KB) is unreachable. |
| 7 | `avoid` | **WIRED** *(dungeon only)* | `CinderSim.cs:794` `SetPlayerAction(ActorAction.Avoid, true)` inside `CastDash`. Reached only via `CastDungeonSkills` (`:744-747`), which `CastSkills` calls only when `_dungeon` (`:687-691`); the prologue returns early at `:681-684` and the arena branch (`:693-700`) has no dash. |
| 8 | `defence` | **DEAD** | No emitter. Void Aegis (the shield skill) sets `_shield`/`_shieldTime`/`_castInvuln` (`CinderSim.cs:877-879`) but **no action** — the block clip never plays for the game's own block mechanic. `Body Block.fbx` (337 KB) unreachable. |
| 9 | `die` | **WIRED** | `CinderSim.cs:1570` player; `:1798`, `:2275` enemy |
| 10 | `show` | **DEAD** | No emitter. No boss-intro / spawn-roar path sets it. `Mutant Roaring.fbx` (387 KB) unreachable. |
| 11 | `attack2` | **WIRED** *(dungeon only)* | `ActorView.cs:339-340` `comboTier == 1 ? Attack2Value`. Tier from `GameView.cs:404` — `if (_isDungeon) _playerView.SetComboTier(((IHackSnapshot)_sim).ComboIndex);` |
| 12 | `attack3` | **WIRED** *(dungeon only)* | `ActorView.cs:339-340` `: Attack3Value` for tier ≥ 2. Same `GameView.cs:404` gate. |
| 13 | `cast` | **WIRED** *(dungeon only, idle-body only)* | `ActorView.cs:341` `if (castPoseLive && action == ActorAction.Idle) return CastValue;` Window opened by `FlashCastGlow` at `:414` (`_castPoseTime = 0.30f`), called from `GameView.cs:373`. |

**Tally: 10 WIRED, 4 DEAD.** Dead payload: `hit` 195,200 + `critical` 258,048 + `defence` 337,040 +
`show` 386,992 = **1,177,280 bytes (~1.12 MB)** of authored motion that cannot play.

`[INFERENCE]` The four dead clips are not random. `hit` and `defence` are *reaction* poses and the
sim has no reaction vocabulary at all — it models damage as a number plus a grace timer, never as a
state the body enters. That is the same structural gap that blocks 날아가기.

### 2a. `bighit` revival — confirmed

`[OBSERVED]` The user's recollection is correct and the mechanism is a **velocity heuristic in the
View**, not a sim signal. Full path:

1. Sim pushes enemies via `Knockback(ref enemy, distance, time)` — `CinderSim.cs:950-965`, which sets
   `enemy.KnockX/KnockY` to `distance/time` px/s and `enemy.KnockTime = time`.
2. Callers: combo finisher `CinderSim.cs:1492` (`ComboKnockbackDistance` 120 px / `ComboKnockbackTime`
   0.18 s) and Ash Nova `CinderSim.cs:865` (`AshNovaKnockback` 120 px, same 0.18 s).
3. Integration: `CinderSim.cs:1608-1621`, riding on top of the chase.
4. **`EnemyState` (`SimTypes.cs:51-65`) publishes no knockback field** — only X/Y move.
5. `ActorView.SyncEnemy` (`:153-158`) reconstructs the signal: on a damage frame, if
   `sqrt(stepX² + stepY²) / Time.deltaTime > 300f`, set `_knockbackTime = HackSpec.ComboKnockbackTime`.
6. `ResolveActionValue` (`:338`) returns `BigHit`, outranked only by `Die` (`:337`).

The 300 px/s gate is deliberate and documented at `ActorView.cs:145-152`: launch is
120 px / 0.18 s ≈ **667 px/s**, chase is ≤ 128 px/s, and a *fixed pixel* gate would misfire during
batched catch-up ticks on slow frames. The threshold sits between the two at any frame rate.

`[INFERENCE]` This works but is fragile by construction — it infers sim intent from published
position deltas. Any future sim move that displaces an enemy faster than 300 px/s without being a
launch (a pull, a teleport, a shove) will play the uppercut-reaction clip.

---

## 3. Sim state for the three requested moves

### 3.1 뒹굴기 (roll / dodge) — **FULLY SUPPORTED, dungeon-gated**

`[OBSERVED]` The dash **is** an i-frame dodge and it is complete end-to-end.

| Property | Value | Citation |
|---|---|---|
| Distance | 190 px | `HackTypes.cs:255` `DashDistance` |
| Duration | 0.22 s | `HackTypes.cs:256` `DashTime` (→ 863.6 px/s, ~4× the 218 u/s walk) |
| Cooldown | 1.6 s | `HackTypes.cs:257` `DashCooldownSeconds` |
| Charge cost | 8 | `HackTypes.cs:258` `DashCost` |
| **Invulnerability** | **whole duration** | `CinderSim.cs:1531-1534` — `if (_dashTime > 0f \|\| _castInvuln > 0f) return;` inside `DamagePlayer`, **before** the grace-window burn |
| Movement authority | total | `CinderSim.cs:1294-1298` — `if (_dashTime > 0f) { UpdateDash(deltaTime); return; }` — no steering, no swing, no contact |
| Direction | input vector, else facing | `CinderSim.cs:769-781` |
| Combo interaction | cancels the swing | `CinderSim.cs:792-793` — `_comboSwing = -1` |
| Pose | `ActorAction.Avoid` → `Dodging.fbx` | `CinderSim.cs:794` |
| Exit | back to `Idle` | `CinderSim.cs:1397-1401` |
| Input | **Shift**, dungeon profile only | `InputAdapter.cs:79-80` |

**Search results for the requested terms** (`Assets/Scripts/Sim/**`):
- `dodge`/`roll` — **zero matches.** The feature is named "dash" throughout.
- `iframe`/`invuln` — `_castInvuln` (`CinderSim.cs:158`) plus the `_dashTime` guard at `:1531`.
  There is no boolean `Invulnerable` field; invulnerability is *implied* by two timers.

**So what is actually missing?** Not the mechanic — the **reach** and the **read**:
1. `[OBSERVED]` Dash is unavailable in the prologue (`CinderSim.cs:681-684` early-returns before any
   skill) and in the arena (`:693-700` handles only Nova/Ward). Two of three modes have no dodge.
2. `[OBSERVED]` It is gated behind 8 charge — a resource. A defensive move that can be *unaffordable*
   reads as unreliable.
3. `[INFERENCE]` `Dodging.fbx` is a Mixamo **sidestep/lean**, not a 뒹굴기 (ground roll). The user
   asking for "뒹굴기" while a dodge already exists suggests the *clip* is what fails to read, not the
   mechanic. Worth confirming visually before commissioning art.

### 3.2 날아가기 (launch / knockback fly) — **enemies only; player cannot be launched**

`[OBSERVED]` Constants and plumbing:

| Symbol | Value | Citation |
|---|---|---|
| `ComboKnockbackDistance` | 120 px | `HackTypes.cs:247` |
| `ComboKnockbackTime` | 0.18 s | `HackTypes.cs:248` |
| `AshNovaKnockback` | 120 px | `HackTypes.cs:285` |
| `Knockback(ref Enemy, …)` | pushes away from player | `CinderSim.cs:950-965` |
| Applied — combo finisher | index == `ComboLength-1` (== 2) | `CinderSim.cs:1490-1493` |
| Applied — Ash Nova | all in radius | `CinderSim.cs:865` |
| Per-tick integration | `KnockX/Y * step`, arena-clamped | `CinderSim.cs:1608-1621` |
| Storage | `Enemy.KnockX/KnockY/KnockTime` | `CinderSim.cs:65-66` |
| Reset on spawn | zeroed | `CinderSim.cs:2063-2065` |

**The blocking fact:** `[OBSERVED]` `PlayerState` (`SimTypes.cs:37-49`) contains
`X, Y, Facing, Health, AttackCooldown, DamageCooldown, WardTime, Moving, Action, ActionTime, AttackId`
— **no knockback fields.** `KnockX/KnockY/KnockTime` exist only on the private `Enemy` struct
(`CinderSim.cs:65-66`), which is not a published type. **The player can never be launched by anything.**
`SimTypes.cs` carries `// FROZEN CONTRACT` at line 1, so adding player knockback there is a
contract amendment, not a patch.

Second gap: `[OBSERVED]` even for enemies the launch is **unpublished** — `EnemyState`
(`SimTypes.cs:51-65`) has no knockback member, which is precisely why the View resorts to the
300 px/s velocity guess (§2a).

Third: `[INFERENCE]` 120 px over 0.18 s is a *stagger-shove*, not 날아가기. For reference, 120 px is
under a quarter of `ArenaHalfWidth` (520) and the player walks 218 u/s — the launch covers in 0.18 s
what walking covers in 0.55 s. There is also no vertical component: `Knockback` writes X/Y only
(`:962-963`), and Y is the isometric ground axis, scaled by `SimConfig.YMoveScale` at `:1612`. **No
airborne state exists anywhere in the sim.**

### 3.3 발차기 (kick) — **nothing at any layer**

`[OBSERVED]` Grep for `kick`/`Kick` across `Assets/Scripts/Sim/**` — **zero matches.** Also zero in
`docs/SIM_SPEC.md` and `docs/SIM_SPEC_HACKSLASH.md`.

`[OBSERVED]` The complete attack vocabulary:

| Path | Mechanism | Citation |
|---|---|---|
| Arena basic | one `Attack`, 5 frames @ 12 fps | `CinderSim.cs:1332-1338`, frames `:44-47` |
| Dungeon combo | 3-hit chain, **all three emit `ActorAction.Attack`** | `CinderSim.cs:1411-1425` |
| Combo timings | swing `{0.30, 0.30, 0.42}` s | `HackTypes.cs:243` |
| Combo active windows | from `{0.10, 0.10, 0.14}` to `{0.22, 0.22, 0.30}` | `HackTypes.cs:244-245` |
| Combo damage | `{1.0, 1.0, 1.5}` × 58 base | `HackTypes.cs:242` |
| Skills | Bolt / Pulse / Nova / Aegis — **emit no action at all** | `CinderSim.cs:748-763` |

`[OBSERVED]` **There is exactly one attack type in the sim.** The three combo hits are distinguished
*only* in the View, by `ComboIndex` → `SetComboTier` → `ResolveActionValue`
(`GameView.cs:404` → `ActorView.cs:339-340`). Skills produce damage with no pose whatsoever — which
is exactly why the `cast` substate had to be invented off the *glow* event (`ActorView.cs:410-414`).

`[INFERENCE]` A kick is therefore the **only** one of the three moves that needs work at all three
layers: sim (a distinct attack identity), View (a substate), and art (a new clip). It is also the
only one requiring new FBX.

---

## 4. The animation contract for a NEW clip

### 4.1 `docs/RUNTIME_ANIMATION_CONTRACT.md` does not govern this project

`[OBSERVED]` The document is **inherited from a different codebase** and must not be used as the
acceptance bar for a Unity clip. Evidence:

| Contract says | This repo is | Citation |
|---|---|---|
| Runtime file `battle-realtime-three.js` | **File does not exist** (`find` → no results) | `RUNTIME_ANIMATION_CONTRACT.md:6` |
| Repository "Abyssal-Lantern" | Cinder Court | `:5` |
| **"Not Supported: FBX"** | **100% FBX** — all 14 clips | `:96` |
| Format GLB / glTF 2.0 | FBX → Unity Humanoid | `:94-96` |
| Naming `<assetId>::<action>::v01` | bare `action` names (`take.name = action`) | `:185` vs `CharacterImportPipeline.cs:252` |
| Rig `def-humanoid-v1`, 24 DEF-* Rigify bones | Unity Mecanim Humanoid, Mixamo-named bones | `:247-258` vs `CharacterImportPipeline.cs:22-23, 244` |
| Source of truth `assets/motion/ingame/registry.json` | **Path does not exist** | `:15-16` |
| Three.js `AnimationMixer`, `THREE.LoopOnce` | `AnimatorController` any-state transitions | `:291-300` |

`[OBSERVED]` `git log` on the doc → single commit `7fc22c0 "foundation: Unity Cinder Court contract,
frozen sim spec, pipelines"` — it arrived with the foundation import and was never revised for Unity.

`[INFERENCE]` Two ideas in it are still *conceptually* live because the Unity pipeline independently
enforces them: the 11-action canonical library (`:52-55`), and in-place root motion — "Simulation
translation owns actor movement… animation may articulate joints but may not displace the gameplay
root" (`:28-30`). That principle is enforced for real by `lockRootPositionXZ` below. Treat the rest
as historical.

### 4.2 What actually enforces a new clip

**A. Importer settings — `CharacterImportPipeline.ReimportClips()`, `:236-269`.**
Applied automatically to every table row; a new clip inherits them by being added to the table.

| Requirement | Line |
|---|---|
| File must exist at `Assets/Art/Motion/<file>.fbx` — else `missing clip fbx` throw | `:240-243` |
| `animationType = Human` (Mecanim Humanoid) | `:244` |
| `avatarSetup = CreateFromThisModel` | `:245` |
| At least one take — else `no animation takes` throw | `:249-250` |
| **Only take[0] is used**; renamed to the action name | `:251-252` |
| `loopTime` / `loopPose` = the tuple's `loop` flag | `:253-254` |
| **`lockRootRotation`, `lockRootHeightY`, `lockRootPositionXZ` = true** — in-place; *"sim owns displacement"* | `:255-257` |
| `keepOriginalOrientation/PositionY/PositionXZ` = true | `:258-260` |
| Materials **not** imported | `:247` |
| Resulting avatar must be non-null, `isValid`, `isHuman` — else `invalid clip avatar` throw | `:263-266` |
| Clip must be findable by name, excluding `__preview__` | `:271-280` |

**The in-place rule is the sharpest constraint for these three moves.** A roll that travels, a
launch that arcs, and a kick that lunges will all have their root displacement **stripped**. The sim
must produce the travel or the move will look like it is running on a treadmill.

**B. Controller generation — `BuildController()`, `:282-320`.**
- One state per row, keyed **by action name** — names must be unique (`:290-297`).
- Default state `idle` (`:298`).
- Any-state transition per row, condition `action Equals <row index>`, `hasExitTime = false`,
  `duration = 0.08f`, `canTransitionToSelf = false` (`:301-308`).
- Every action except `idle`/`move`/`run`/`die` gets a return-to-idle at `exitTime = 0.95`,
  `duration = 0.1` — **conditioned on `action Equals 0`** (`:310-318`).
  `[INFERENCE]` A one-shot therefore only *returns* once the driver has already set the integer back
  to 0. A View-owned substate must clear its own window or the pose sticks.
- `die` deliberately has no return (clamped) (`:312`).

**C. `ClipTableTests.cs`** — the alignment validator (reflection-based; `CharacterImportPipeline`
lives in the predefined `Assembly-CSharp-Editor`, so it cannot be bound at compile time — see
`:28-36`).
- Row *i* must name `ActorAction` *i*, driven off `Enum.GetValues` (`:89-111`).
- `SimActionCount` must equal the enum length (`:114-124`).
- Rows 11/12/13 must be `attack2`/`attack3`/`cast` (`:128-155`).
- Action names must be unique and non-empty (`:160-192`).

**D. `PoseResolveTests.cs`** — exhaustive pinning of `ResolveActionValue` (compile-time bound via
`InternalsVisibleTo`, `:32-36`).
- Identity for every sim-emitted action (`:12-13`).
- Ladder 5 / 11 / 12 for tiers 0 / 1 / 2 (`:55-60`).
- **Both branch guards**: the cast branch must not mask a sim-asserted reaction; the combo branch
  must not capture `Critical` (`:17-21`).
- `comboTier > 0`, never `!= 0` — `-1` is the live tier outside the dungeon (`:22-26`).
- Distinctness and purity (`:27-30`).
- Adjacency: `Attack2Value == simActionCount` (`:281-285`).

**E. `CharacterRosterAnimationTests.cs`** — the rig/deformation validator, run over every
`CharacterRoster.Ids` prefab.
- Shared controller at `Assets/Art/Motion/CinderActor.controller`, same instance for all (`:16-17, 31-32`).
- Animator active/enabled; avatar non-null, `isValid`, `isHuman`; `animator.isHuman` (`:29-36`).
- Integer parameter named `action` (`:37-38, 108-116`).
- `HumanBodyBones.RightHand` must map (`:40-42`).
- ≥1 enabled `SkinnedMeshRenderer`, each with a mesh (`:52-58`).
- **Blended skin weights** — at least one vertex with >1 influence, "to prevent rigid seams" (`:118-136`).
- Baked-mesh vertices and bounds **finite**, non-degenerate (>0.01 on every axis) (`:169-208`).
- `action = Attack` must enter `Base Layer.attack` (`:72-76`).
- **Deformation sanity at normalized times 0.2 / 0.5 / 0.8**: extent radius ≤ **8×** rest,
  bounds volume ≤ **128×** rest (`:138-161`).
- Right hand must move > 0.01 across the clip — no static poses (`:98-99`).

`[INFERENCE]` Only the `attack` state is currently exercised by (E). A new clip is validated for
*rig* and *import*, but nothing pins the new state's playback the way `Base Layer.attack` is pinned.
A new-clip PR should extend (E) with the new state name, or the clip can silently fail to enter.

`[OBSERVED]` No PlayMode tests exist (`Assets/Tests/` contains only `EditMode`), so runtime
verification is manual/browser.

### 4.3 Checklist for any NEW clip

1. Drop `<Name>.fbx` in `Assets/Art/Motion/` — Mixamo-named bones, Mecanim auto-maps
   (`CharacterImportPipeline.cs:22-23`).
2. **Append** a row to `Clips` (`:34-51`) — never reorder, never insert. Rows 0–10 are pinned to the
   frozen enum; new View-only substates go at index ≥ 14.
3. Unique action name; loop `false` for a one-shot.
4. Add the animator literal beside `Attack2Value/Attack3Value/CastValue` (`ActorView.cs:48`).
5. Extend `ResolveActionValue` (`:334-343`) — mind the priority order; the guard that a sim-asserted
   reaction is never masked is, per `PoseResolveTests.cs:17-21`, "the entire safety argument".
6. Update `PoseResolveTests.cs` (substate list + ladder) and `ClipTableTests.cs`
   (`ViewOnlySubstates`).
7. Run `CinderCourt/Import All Characters And Clips` to regenerate the controller.
8. Accept in-place root motion — the sim owns all displacement.

---

## 5. Smallest change per move

### 5.1 뒹굴기 (roll) — **cheapest by a wide margin; possibly zero new art**

The mechanic exists and is wired. Ordered by cost:

**Option A — ungate the dodge (recommended first step).**
- **Sim:** `Assets/Scripts/Sim/CinderSim.cs` — `CastSkills` (`:677-701`). Hoist the
  `input.DashQueued` check above the `_prologue` early-return (`:681-684`) and add it to the arena
  branch (`:693-700`), or simply call `CastDash` before the mode fork. ~3 lines.
  `CinderSim.cs` is **not** itself frozen (header `:1-3` names `SimTypes.cs` as the frozen contract),
  and this adds **no new field, type, or constant** — `DashDistance/DashTime/DashCooldownSeconds/
  DashCost` already live in `HackTypes.cs:255-258`. `[INFERENCE]` If the arena/prologue must not use
  the charge economy, gate on cooldown alone and skip the `_charge >= DashCost` term at `:744`.
- **View:** `Assets/Scripts/View/InputAdapter.cs` — add the Shift latch (`:79-80`) to
  `case Profile.Arena:` (`:64-68`) and `case Profile.Prologue:` (`:69-71`). ~4 lines.
  Touch input already has `QueueDash()` (`:93`); wire a HUD button if the mode shows one.
- **Art:** **none.** `Dodging.fbx` → row 7 → `ActorAction.Avoid` already plays.
- **Tests:** extend `Assets/Tests/EditMode/HackSimTests.cs` / `CinderSimTests.cs` with an
  arena/prologue dash case. No clip-table or pose-resolve change (no new row, no new substate).

**Option B — make it read as a *roll*.**
- **Art:** replace `Assets/Art/Motion/Dodging.fbx` with a Mixamo ground-roll take, keeping the
  filename. **Zero code change** — the table already points at `"Dodging"`
  (`CharacterImportPipeline.cs:43`) and the name is arbitrary. Re-run the import menu item.
- Must clear `CharacterRosterAnimationTests` — a roll is the likeliest clip in the library to trip
  the **8× extent / 128× volume** bounds guard (`:155-160`), since the body leaves its standing
  silhouette.
- `[INFERENCE]` A roll typically travels much further than 190 px of authored motion implies, but
  `lockRootPositionXZ` (`CharacterImportPipeline.cs:257`) strips that — the sim's 190 px over 0.22 s
  supplies the travel. If it reads short, tune `HackSpec.DashDistance` (`HackTypes.cs:255`);
  that is a frozen-amendment file, so it needs a `docs/SIM_SPEC_HACKSLASH.md` update per the header.

**Option C — keep both.** Add `roll` as row 14 (View-only substate) selected when
`Avoid && moveInputMagnitude > 0`, leaving `Dodging` as the standing sidestep. Requires steps 2–7 of
§4.3. Only worth it if both reads are wanted.

### 5.2 날아가기 (launch) — **the honest fix is publishing the flag**

Three tiers, increasing cost:

**Tier 1 — make the existing launch legible (no new art, no frozen edit).**
- **Sim:** none.
- **View:** `Assets/Scripts/View/ActorView.cs`. The `bighit` pose already fires (`:338`). Extend the
  same `_knockbackTime` window at `:153-158` to drive a **lean/blur/trail** so 120 px reads as
  violent. `[INFERENCE]` 120 px / 0.18 s is objectively a shove; presentation is what sells it.
- **Art:** none — `Receive Uppercut To The Face.fbx` is already the pose.

**Tier 2 — replace the heuristic with a real signal (recommended).**
- **Sim:** `Assets/Scripts/Sim/SimTypes.cs` (**FROZEN — needs amendment + `docs/SIM_SPEC.md` update
  per the `:1` header**): add `public float KnockTime;` to `EnemyState` (`:51-65`).
  Then `Assets/Scripts/Sim/CinderSim.cs` — copy `enemy.KnockTime` into the published state wherever
  `EnemyState` is filled (near the `:1608-1621` integration).
- **View:** `Assets/Scripts/View/ActorView.cs` — replace the 300 px/s guess (`:153-158`) with
  `state.KnockTime > 0f`. Deletes the fragile heuristic and its 8-line rationale comment.
- **Art:** none.
- **Tests:** `PoseResolveTests.cs` already covers the `knockbackLive` parameter; add a
  `HackSimTests.cs` case pinning that `KnockTime` publishes.

**Tier 3 — launch the *player* (largest; only if 날아가기 means the player flies).**
- **Sim:** `SimTypes.cs` **FROZEN** — `PlayerState` (`:37-49`) needs `KnockX/KnockY/KnockTime`.
  `CinderSim.cs` — a player-side integrator mirroring `:1608-1621`, called from `UpdatePlayer`
  (`:1289`) ahead of the dash guard at `:1294`, plus a caller in `DamagePlayer` (`:1522`).
  Must decide precedence against the dash i-frames (`:1531`).
  `HackTypes.cs` **FROZEN AMENDMENT** — new distance/time constants + `docs/SIM_SPEC_HACKSLASH.md`.
- **View:** `ActorView.cs` — reuse `_knockbackTime`; `SyncPlayer` (`:116-128`) currently does **no**
  knockback detection at all, so add the read there.
- **Art:** none for the reaction (`bighit` covers it). An **airborne** launch would need a new clip
  *and* a vertical axis the sim does not have (`Knockback` writes X/Y only, `:962-963`) — that is a
  much larger change than the other two moves combined.

### 5.3 발차기 (kick) — **the only move needing all three layers**

Two designs:

**Option A — kick as the combo finisher (smallest real kick).**
- **Sim:** none. `ComboLength` is already 3 (`HackTypes.cs:240`) and the finisher already carries the
  1.5× damage (`:242`) and the 120 px knockback (`CinderSim.cs:1490-1493`).
- **View:** none. Row 12 `attack3` already fires at `comboTier >= 2` (`ActorView.cs:339-340`).
- **Art:** **replace `Assets/Art/Motion/Standing Melee Combo Attack Ver. 2.fbx` with a kick take.**
  Keep the filename (the table string at `CharacterImportPipeline.cs:49` is arbitrary), re-run
  `CinderCourt/Import All Characters And Clips`.
- **Cost: one FBX swap, zero code.** `[INFERENCE]` The mechanics already read as a kick — a heavy
  finisher that knocks the target back is exactly what 발차기 does. This also composes with §5.2:
  kick → launch is one motion.
- **Caveat:** dungeon-only, since `SetComboTier` is behind `if (_isDungeon)` (`GameView.cs:404`).

**Option B — kick as a distinct 4th input.**
- **Sim:** `Assets/Scripts/Sim/SimTypes.cs` (**FROZEN**) — add `public bool KickQueued;` to `SimInput`
  (`:21-35`).
  `Assets/Scripts/Sim/HackTypes.cs` (**FROZEN AMENDMENT** + `docs/SIM_SPEC_HACKSLASH.md`) — kick
  damage / swing / active-window / knockback constants beside the combo block (`:239-248`).
  `Assets/Scripts/Sim/CinderSim.cs` — a `CastKick` beside `CastDash` (`:767`) and a branch in
  `UpdateCombo` (`:1409`). **Design decision:** the frozen `ActorAction` has no `Kick`, so either
  emit `Attack` and let the View disambiguate (the established pattern — see the `cast` precedent at
  `ActorView.cs:410-414`), or emit the unused **`Critical`** (`SimTypes.cs:12`, index 6), which is
  DEAD today and would revive `Illegal Elbow Punch.fbx` for free. `[INFERENCE]` Reusing `Critical`
  is the only path that adds a real move **without touching a frozen file** — the enum value already
  exists and `ResolveActionValue` already forwards it by identity (`ActorView.cs:342`).
- **View:** `Assets/Scripts/View/InputAdapter.cs` — key latch (`:62-84`) + `QueueKick()` (beside
  `:93`) + `SimInput` field (`:154-158`) + both reset blocks (`:113-117`, `:169-173`).
  `Assets/Scripts/View/GameView.cs` — clear the latch in the tick loop beside `:252`.
  `Assets/Scripts/View/ActorView.cs` — if using a new substate, add the literal at `:48` and a branch
  in `ResolveActionValue` (`:334-343`).
- **Art:** a new kick FBX in `Assets/Art/Motion/`, appended as row 14 (`CharacterImportPipeline.cs`
  after `:50`) — unless reusing `Critical`, in which case `Illegal Elbow Punch.fbx` is already there
  and only the *pose* is wrong (an elbow, not a kick); swap the FBX and keep row 6.
- **Tests:** `ClipTableTests.cs` (`ViewOnlySubstates`), `PoseResolveTests.cs` (substate + ladder),
  `HackSimTests.cs` (kick timing/damage), and — per §4.2(E) — extend
  `CharacterRosterAnimationTests.cs` to enter the new state.

---

## 6. Cross-cutting observations

1. `[OBSERVED]` **The animator is not the bottleneck.** All 14 states and the `action` parameter exist
   in `CinderActor.controller`. Every gap is a missing *driver*.
2. `[OBSERVED]` **The sim has no reaction vocabulary.** It emits 6 of 11 enum values and never a
   damage reaction — `Hit`, `BigHit`, `Defence` are all unemitted. Damage is a number plus a grace
   timer, never a state. This one gap explains three of the four dead clips and is the root cause
   behind 날아가기 being half-built.
3. `[OBSERVED]` **~1.12 MB of authored motion is unreachable.** `critical` (258 KB) and `defence`
   (337 KB) are the closest existing assets to the user's request, and `Critical` is a live enum
   value that `ResolveActionValue` already forwards — the cheapest possible home for a new move.
4. `[OBSERVED]` **Almost everything expressive is dungeon-gated.** `avoid`, `attack2`, `attack3`,
   `cast` all require dungeon mode (`CinderSim.cs:687-691`, `GameView.cs:404`,
   `InputAdapter.cs:72-83`). A player in the prologue or arena has walk, run, punch, die — four poses
   out of fourteen. `[INFERENCE]` "More freedom of movement" may be substantially satisfied by
   ungating what already exists, before any new authoring.
5. `[OBSERVED]` **The FBX filename is not the contract** — the tuple's action string is
   (`CharacterImportPipeline.cs:252` renames take[0] to the action). Any clip can be replaced in
   place by keeping the filename, which makes art-only swaps (§5.1-B, §5.3-A) genuinely zero-code.
6. `[OBSERVED]` **In-place is absolute** (`CharacterImportPipeline.cs:255-257`). Every travelling
   move must have its distance supplied by the sim. This is the single constraint most likely to make
   a newly commissioned roll/kick look wrong on first import.

---

## Verification

- Directory inventory: `find Assets/Art -type d` (no nested motion dirs) + `stat` per file — all 30
  files enumerated, 14 FBX sizes and mtimes measured.
- Clip table: `Assets/Editor/CharacterImportPipeline.cs:34-51` read in full; all 14 rows transcribed.
- Wiring: exhaustive `grep 'ActorAction\.[A-Za-z]+'` over `Assets/Scripts/Sim`, plus targeted
  `grep 'ActorAction\.(Show|Critical|Defence|Hit)\b'` over all of `Assets/` — only test strings and
  one read-only facing check returned.
- Controller: `grep 'm_Name:' CinderActor.controller` → 14 states + `action` parameter confirmed.
- Contract provenance: `git log -- docs/RUNTIME_ANIMATION_CONTRACT.md` → single foundation commit;
  `find . -name battle-realtime-three.js` → no results; `ls assets/motion` → absent.
- Frozen markers: `grep -rn "FROZEN CONTRACT" Assets/Scripts/ Assets/Editor/` → `SimTypes.cs:1`,
  `CampaignTypes.cs:1`, `HackTypes.cs:1` only. `CinderSim.cs:1-3` references the frozen contract but
  is not itself marked frozen.
- **Concurrency:** `Assets/Scripts/Sim/CinderSim.cs` was edited by a sibling agent mid-survey
  (hash `#5F60` → `#87A5`, 2448 → 2462 lines). **Every CinderSim.cs line number cited above was
  re-grepped against the post-edit file and confirmed unchanged**; the sibling's additions landed
  below the cited regions.

**No code was edited. No formatters, linters, or tests were run. Unity was not launched.**
