# HongT — AMENDMENT #8 companion signature skills: what the gates had to be

Repo: `~/orca/workspaces/HongT/main` (Unity 6000.5.6f1 / URP / WebGL).
Landed in `d0fe934`; spec text in `docs/SIM_SPEC_HACKSLASH.md` §"Frozen Contract
Amendment #8"; gates in `Assets/Tests/EditMode/CompanionSkillTests.cs`.
Companion to `hongt-companion-autonomy-tick-order-trap.md` — read that first, its
tick-order traps decide how every assertion here must be measured.

## [OBSERVED] Superseding a frozen non-goal is a one-line, explicit act

Amendment #3 froze "No companion skills, equipment, persistence, or cooldowns" as
an explicit non-goal. A8 narrows exactly that one line to "no companion equipment
or persistence" and says so in the amendment body. Every other #3 clause
(hold/recall semantics, untargetability, neutral damage) stays in force and is
still gated by `HackSimTests`. A non-goal that is silently outgrown leaves two
contradicting frozen documents; naming the superseded sentence keeps one.

## [OBSERVED] "Each X has its own Y" is only testable as pairwise distinctness

The requirement was "one unique skill per companion". The gate that actually
holds it is `CompanionSkill_TableIsPairwiseDistinctOnEveryAxis`: the four
archetypes must differ on **all four** of cooldown, radius, damage scale and
target count.

| archetype | skill | cooldown | radius | ×player dmg | max targets | min auto | knockback |
|---|---|---|---|---|---|---|---|
| Scout | Volley | 6.0 s | 240 | 0.55 | 3 | 2 | 0 |
| Shade | Hex | 8.0 s | 260 | 0.40 | 8 | 2 | 0 |
| Possessed | Quake | 9.0 s | 170 | 0.70 | 6 | 2 | 90 |
| EmberCohort | Flare | 7.0 s | 200 | 1.10 | 1 | 1 | 0 |

Distinctness on a single axis is satisfiable by four re-skins of one skill.
Requiring all four forces four genuinely different shapes, and it is a pure table
assertion — no simulation needed, so it can never go flaky.

## [OBSERVED] The damage ledger is what pins a damage number

`DamageEnemy` applies a raw float with no modifiers, so in a **quiet** run (no
player attack/skill input, one companion) the only damage sources are the swing
`W` and the skill `S`. Therefore every positive per-tick health delta must be
`min(before, k)` for `k ∈ {W, S, S+W}` — measured across all four archetypes for
1800 ticks: **0 violations**. Two things fall out of that single invariant:

- it pins the skill damage *number*, not just "damage happened";
- it proves the skill is **neutral** (A8.6). Any §2.4 elemental matchup or
  GuardianResonance scaling applied to the skill would land off all three values.

The level curve must be folded in or the check is nonsense:
`playerDamage = config.PlayerDamage × (1 + LevelDamageBonus × (level−1))` with no
growth points and no extraction bonus. A quiet run reaches level 2 partway, which
is why an early probe saw `exactSkill=0` and looked like a bug.

## [OBSERVED] Which positions a cast used — the pairing that makes nearest-first checkable

A cast uses **the companion position AFTER this tick's movement** against **the
enemy positions from the END OF THE PREVIOUS TICK** (`UpdateEnemies` runs after
`UpdateCompanion`). So the hit set is exactly reconstructible from outside:
sample living enemies *before* `Tick`, read `CompanionXAt/YAt` *after* it, sort by
the iso metric, and the struck set must be the `min(MaxTargets, inRadius)`
nearest — verified over 19 casts, zero mismatches. Any other pairing of those two
timestamps reports false violations.

Corollary for the auto-threshold check: the companion can travel up to
`PlayerSpeed × PursuitSpeedScale × step ≈ 3.8 px` in the tick, so count targets
with a 4 px margin instead of pretending it stood still.

## [OBSERVED] A corpse cannot be shoved

Quake's knockback is only assertable on hits that **survived** the cast. Measured:
a shoved living enemy clears ~6.8–7.3 px in the cast tick, an unshoved one drifts
~1.5 px on its own legs, and an enemy killed by the same cast moves 0.00. The
first probe counted "away from the companion" over all hits and reported a 1/1
split that looked like a directional bug; it was the corpse. Filter on
`!now.Dead`, then assert both `travelled > 4` and "distance from the cast origin
increased".

## [OBSERVED] A8 starved a pre-existing test that was passing on one lucky tick

`CompanionAutonomy_HoldPinsEverySlotAndSuppressesPursuit` asserted "a held slot
still swings". Measured against HEAD, slot 1 had a living target in range on
**exactly 1 tick out of 600**, and swung exactly once. With A8, a sibling slot's
Flare killed the shared target on that very tick (skills resolve before swings;
slots resolve in index order), so the slot got 0 swings — and the run digest was
**identical** (same kills, same score, same hp), because the kill simply moved
from a swing to a skill.

The fix was not to weaken the assertion. The player now **stands still** after the
hold, so enemies keep converging on the pinned slots (in-range ticks per slot go
122/57/436 instead of 1/1/85, swings 4/3/8), and a new `inRangeTicks > 20` guard
fails *loudly and differently* if the scenario is ever starved again. Lesson: an
existence assertion (`swings > 0`) in an emergent scenario needs a companion
assertion that the scenario actually offered the opportunity, or it silently
degrades into a luck test.

## [OBSERVED] Mutation testing is the only proof the gates bite

Six mutants, each killed by **behaviour** tests and not merely by the table test:

| mutant | killed by (besides the table test) |
|---|---|
| Flare cap 1→3 | `StrikesTheNearestTargetsUpToTheArchetypeCap`, `DamageIsNeutral…` |
| Quake knockback 90→0 | `OnlyQuakeShovesAndItShovesAwayFromTheCompanion` |
| Volley threshold 2→1 | `AutoFiresOnlyWithEnoughTargetsInRadius`, `CommandBypassesTheAutoThreshold` |
| free opening cast | `CooldownStartsFullAndNoSlotCastsBeforeIt`, `RestartRefillsTheCooldown`, +5 |
| Hex damage 0.40→0.80 | `DamageIsNeutralAndScalesWithPlayerDamage`, +2 |
| drop the commanded-cast path | `CommandBypassesTheAutoThreshold`, `CooldownStartsFullAnd…` |

Reusable harness: `/tmp/mutate.sh` — back up the file, apply one anchored
replacement, run `tools/unity_batch.sh tests`, parse `failed=` out of the results
XML, restore. One mutant per run; a batch can mask a survivor.

## [OBSERVED] Two build/verification traps that cost real time

1. **Sync the WHOLE source tree to the batchmode clone, not one subtree.** The
   editor lock forces builds/tests into `/tmp/hongt-build`. Rsyncing
   `Assets/Tests/` but not `Assets/Scripts/` produced 7 failures that looked like
   my regression and were actually *another session's* parser-vocabulary edit
   present in the tests but missing from the parser. Always rsync
   `Assets/Scripts/` **and** `Assets/Tests/` together, then re-check.
2. **The sim compiles standalone under `dotnet`.** `Assets/Scripts/Sim/**` has no
   `UnityEngine` reference, so a `net8.0` Exe csproj (`EnableDefaultCompileItems=
   false`, `Compile Include="probe.cs"` + `sim/*.cs`, `NoWarn CS0414;CS0169;
   CS0649`, delete `AssemblyInfo.cs`) runs invariant experiments in ~0.5 s instead
   of a ~2 min Unity batch. Populate one copy from the working tree and one from
   `git show HEAD:` to diff pre/post-amendment behaviour — that is how the
   "starved test" cause above was proven rather than guessed.

## [OBSERVED] Release state at time of writing

EditMode **351/351**, 0 failed
(`_workspace/current/engineering/unity-logs/test-results-084658.xml`). WebGL build
0 CS errors, 67.3 MB reported / 47 MB on disk. Deployed gh-pages
`cb0d344 → 5313d1c`; live asset sizes match the local build byte-for-byte
(loader 48106, wasm 10475928, data 36189474).
