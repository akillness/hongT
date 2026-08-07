# HongT — AMENDMENT #9 momentum gauge: scoping a buff so the frozen contract survives

Repo: `~/orca/workspaces/HongT/main` (Unity 6000.5.6f1 / URP / WebGL).
Spec: `docs/SIM_SPEC_HACKSLASH.md` §"Frozen Contract Amendment #9".
Gates: `Assets/Tests/EditMode/MomentumTests.cs` (14 tests).
Read `hongt-companion-signature-skills-amendment-8.md` first — its damage-ledger
and mutation-harness technique is reused here verbatim.

## [OBSERVED] A passive player buff cannot be digest-neutral — so scope it, don't dilute it

A7 and A8 could keep the arena/prologue digests frozen because they only touched
companions. A9 multiplies **player** melee damage, so some digest had to move.
The resolution is scope, not compromise: the gauge is gated to `Dungeon`, exactly
where the §12.1 input depth (charge, finisher variants, growth) already lives.
Result: arena `6600/4/21/4/90` and prologue `5500/3/18/6/73` stay byte-identical,
and the dungeon row moves in **one term only** — `3350/3/13/3/89.5 → .../71.5`.
Score, wave, kills and relics are unchanged, which is the part that proves nothing
else drifted; the player just trades differently on the way to the same result.

Re-pin the moved literal in every fixture that carries it
(`CompanionAutonomyTests.cs`, `CompanionSkillTests.cs`) with a comment naming the
amendment, so the move stays a decision instead of becoming drift.

## [OBSERVED] The dungeon gate is single-sourced, and mutation testing proved the second guard is dead

Ten mutants, one Unity run each; nine killed by behaviour tests. The tenth —
**drop the `!_dungeon` early-out inside `GainMomentum`** — survived, and it is a
genuinely **equivalent mutant**: `UpdateCombo` is the only path that reaches
`SwingCombo`/`ReleaseCharge` (the only two `GainMomentum` call sites), and it is
already dungeon-gated at `CinderSim.cs` `UpdatePlayer`:

```
if (_dungeon) { UpdateCombo(deltaTime, in input); return; }
```

Arena and prologue use a different, older single-swing path entirely. So the inner
guard is defence in depth for a future non-dungeon melee path and no black-box test
can distinguish its removal. Report it as equivalent with the reason — that is a
legitimate mutation outcome, and claiming 10/10 would be a lie.

Killed mutants worth keeping: `perHit 9→3`, `perKill 14→0`, `decay 12→4`,
`grace 1.6→0.4`, `hurt 25→0`, flat tier multipliers, `threshold 30→20`,
"skills ride the buff", "tier cue level-triggered".

## [OBSERVED] A scripted wander cannot test a melee mechanic — you need a homing input

The A7/A8 script (`MoveX` flipping every 120 ticks, attack every 30) landed
**2 melee hits in 1800 ticks** and never left tier 0. Nothing about the gauge was
observable. The fix is a `Hunt(sim, tick, cadence)` helper that walks at the
nearest living enemy each tick and swings on a cadence; with it the same 1800
ticks produce 34 hits spanning **all four tiers**, which is what makes the damage
ledger meaningful. Any future melee-facing amendment should reuse `Hunt`, not the
digest script.

## [OBSERVED] Three measurement traps specific to a decaying gauge

1. **A gain clipped by the ceiling is invisible to a value comparison.** At 100/100
   another hit changes nothing, so `momentum > before` silently reports "no gain"
   and the grace-window measurement stretches. Read the gain off the `EnemyHit`
   event instead (in a no-companion dungeon with no skill input, `EnemyHit` *is* a
   melee hit). Doing so collapsed the measured grace windows from a spread of
   `[98,106,122,149]` to a single deterministic `[98]`.
2. **The grace window is 98 ticks, not 96.** `MomentumGraceSeconds = 1.6` at 60 Hz
   is 96 ticks in exact arithmetic, but repeated `grace -= 1/60` never lands on 0,
   so it takes 97 ticks to expire and the drop is visible on the 98th. Assert the
   exact 98 *and* `>= 96` as the arithmetic floor.
3. **Grace is only measurable in a window with no gain AND no `PlayerDamaged`.**
   Taking damage cancels the grace by design, so a hurt tick inside the window
   invalidates it. Scan a real run for clean windows rather than trying to stage one.

## [OBSERVED] "Wait for state X" fixtures deadlock when the run can end

The first fix for `HackSimTests.Extraction_*` was to let `AssertNextSwingDamage`
tick until `Momentum == 0` so the measured swing stayed a pure statement about the
level/extraction curve. It **starved**: a stationary player surrounded by the
wave-2 pack dies, and `CinderSim.Tick` returns early on `GameOver` — before
`UpdateMomentumDecay` — so the gauge freezes above 0 and the loop never exits.

Working fix: sample `sim.MomentumDamageMultiplier` immediately before the swing and
fold it into the expectation, plus assert `sim.Mode == SimMode.Running` at that
point so a dead-player fixture fails loudly instead of silently. General rule: a
"tick until X" fixture needs an explicit liveness assertion, because a terminal
state can make X unreachable.

## [OBSERVED] What makes "attacking makes you stronger" testable

- **Per-enemy, not per-swing** intake (+9 each, +14 more on the killing blow) —
  crowd-cutting fills fastest, which is the behaviour being rewarded.
- **Discrete tiers** (0/30/60/90 → ×1.00/1.08/1.18/1.30) rather than a curve, so
  the HUD can state the buff and a boundary test can pin inclusivity and totality.
- **Tier 0 must multiply by exactly 1** — that identity is what keeps a
  momentum-less run bit-identical to the pre-amendment sim.
- **Sample the multiplier once per swing**, before that swing's hits feed the
  gauge, so a swing cannot buff its own later targets.
- **Edge-trigger the cue on the tier**, not the value: one `MomentumTierUp` per
  promotion, none on decay, one even when a tick crosses two thresholds.
- The A8 damage-ledger trick generalises: with the only damage sources known, every
  positive health delta must equal `min(before, k)` for a small enumerable set of
  `k`. Extending that set with an **unscaled** Grave Pulse tick is what proves
  A9.6 (skills do not ride the melee buff) — 17 unscaled pulse ticks landed while
  the gauge sat at tier ≥1, 0 mismatches.

## [OBSERVED] Concurrent sessions can commit your uncommitted files

Mid-task, another session's commit `53a1ed4` ("distinct companion command
stances") swept up the still-uncommitted `HudView.cs` momentum-gauge edits along
with its own. `git status` then showed the file clean while the working copy
clearly contained new code. Verify with `git show HEAD:<path> | grep`, treat it as
the other session's work, and do **not** revert. Check `git log --oneline -3`
before assuming your own tree state.

## [OBSERVED] Release state at time of writing

EditMode **365/365**, 0 failed
(`_workspace/current/engineering/unity-logs/test-results-094459.xml`), up from 351.
