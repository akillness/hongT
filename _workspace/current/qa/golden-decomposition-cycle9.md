# Cycle 9 dungeon golden decomposition

Run-id: `20260809-dungeon-fun-authorship`

This ledger records each independently implemented wave before the combined Cycle 9 golden is accepted. Runtime truth is the Unity EditMode digest; standalone .NET float tails are not substituted.

## W1 — aimed Launcher finisher knockback

Status: **MEASURED**

Method: run `bash tools/unity_batch.sh tests` after enabling aimed knockback only for Dungeon + Launcher. Compare the 12 dungeon stage rows in `Assets/Tests/EditMode/DungeonGoldenDigestTests.cs`; Arena and Prologue are controls. Evidence: `_workspace/current/engineering/unity-logs/test-results-165028.xml` (850 total, 849 passed, 0 failed, 1 skipped).

### Stage-row delta

| Stage | Before | After | Delta |
|---|---|---|---|
| cinder-span | `3950|4|15|3|142|(running)|948.149841|546.9919` | same | 0 |
| abyss-chancel | `3150|3|14|1|142|(running)|1151.919|525.907` | same | 0 |
| ember-gallery | `4350|4|16|3|142|(running)|932.6403|514.9418` | `4600|4|16|4|142|(running)|934.673157|521.9911` | score +250, relics +1, final X +2.032857, final Y +7.0493 |
| witness-well | `4350|4|16|3|142|(running)|932.6403|514.9418` | `4600|4|16|4|142|(running)|934.673157|521.9911` | score +250, relics +1, final X +2.032857, final Y +7.0493 |
| echo-throne | `2250|3|11|1|136|(running)|1191.14087|728.4469` | same | 0 |
| ash-verdict | `3400|3|14|2|142|(running)|1248.78381|567.032` | same | 0 |
| ash-march | `3850|4|16|1|82|(running)|957.0417|573.282959` | same | 0 |
| cinder-sluice | `2600|3|13|0|136|(running)|963.5111|617.6645` | `2600|3|13|0|136|(running)|966.1087|624.271851` | final X +2.5976, final Y +6.607351 |
| ember-bastion | `1650|3|9|1|128|(running)|863.109558|632.852051` | same | 0 |

The remaining three dungeon rows are the repeated default-dungeon digest asserted by Momentum, Companion Autonomy, and Companion Skill controls. Each moved identically:

`3350|3|13|3|71.5|(running)` → `3600|3|13|4|112|(running)`

Delta per repeated row: score +250, relics +1, health +40.5. Identical movement proves those companion-less tests execute the shared W1 dungeon scenario rather than separate companion behavior.

### Cross-mode controls

- Arena digest remains `6600|4|21|4|90`.
- Prologue digest remains `5500|3|18|6|73|prologue-clear` in companion controls; the dedicated kiter row also remains unchanged.
- Charged attack and Neutral/Retreat/Spin finisher tests remain on the legacy knockback origin.

Result: **4 of 12 dungeon rows moved; 8 of 12 stayed byte-identical.** This is narrower than the pre-implementation estimate that all 12 were expected movers and is the measured W1 baseline for later decomposition.

## W2 — gimmick kill credit and oil refund

Status: **NOT IMPLEMENTED / NOT MEASURED**

## W4 — enemy behavior differentiation

Status: **BLOCKED BY DESIGN**. The four concrete archetype behaviors are not authored; no implementation or golden movement may be recorded yet.

## Additivity check

Pending W2 and W4. Final acceptance must assert `W1 delta + W2 delta + W4 delta == combined Cycle 9 delta` for every field of every row.
