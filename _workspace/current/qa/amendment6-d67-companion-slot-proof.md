# AMENDMENT #6 (multi-slot companions) — D6.7 deterministic proof

Scope: add the D6.7 proof tests that gate promoting
`docs/SIM_SPEC_HACKSLASH.md` "Frozen Contract Amendment #6" from DRAFT to
frozen. The sim/view implementation had already landed (`5cbe3bd`); only the
proofs were missing.

Changed files:

- `Assets/Tests/EditMode/HackSimTests.cs` — 8 new `CompanionSlots_*` tests plus
  the `DungeonSlots(string[] companionIds, …)` config helper,
  `AssertArchetypeTuple`, and `AssertSlotRunsAreIdentical` (1800-tick
  tick-by-tick divergence check + digest compare).
- `docs/SIM_SPEC_HACKSLASH.md` — amendment status corrected (the edits it
  describes are applied, not "not yet applied"); D6.7 gains a bullet → test
  proof map. The DRAFT→frozen promotion itself is left to the operator.

## D6.7 bullet → test

| D6.7 bullet | Test |
|---|---|
| config/migration (D6.2) | `CompanionSlots_LegacyIdPromotesAndCompanionIdsWinsWithDedupeAndCap` |
| zero/single-companion parity + Arena/Prologue unchanged | `CompanionSlots_ZeroAndSingleSlotRunsMatchTheLegacySingleIdPath` |
| 2- and 3-slot determinism | `CompanionSlots_TwoAndThreeSlotRunsAreDeterministic` |
| D6.3 archetype tuples | `CompanionSlots_ArchetypeTupleTableMatchesTheD63Gate` |
| D6.4 fan-out geometry | `CompanionSlots_EachSlotHoldsItsLateralFanoutOffTheFollowAnchor` |
| per-slot observed cadence | `CompanionSlots_EachSlotSwingsOnItsOwnArchetypeCadence` |
| global hold/recall, recall-wins tie, restart→Follow, inert modes | `CompanionSlots_GlobalHoldAndRecallCommandEverySlot` |
| D6.5 snapshot back-compat | `CompanionSlots_ScalarSnapshotAliasesSlotZeroAndClampsOutOfRange` |

## [OBSERVED] Test execution

The pure-C# `CinderCourt.Sim` assembly plus the whole `HackSimTests.cs` suite
were compiled and run headlessly against the repository sources (project file
`/tmp/hongt-simtests/SimTests.csproj`, `Compile Include=` pointing at
`Assets/Scripts/Sim/*.cs` and `Assets/Tests/EditMode/HackSimTests.cs`, NUnit
3.14 / NUnit3TestAdapter 4.5):

```
cd /tmp/hongt-simtests && dotnet test --nologo
Passed! - Failed: 0, Passed: 81, Skipped: 0, Total: 81 - HongT.SimTests.dll
cd /tmp/hongt-simtests && dotnet test --filter "FullyQualifiedName~CompanionSlots"
Passed! - Failed: 0, Passed: 8, Skipped: 0, Total: 8
```

This is valid coverage because `Assets/Scripts/Sim/` is UnityEngine-free by
contract (CLAUDE.md §1), so the sim compiles and behaves identically outside
the editor.

## [OBSERVED] Mutation checks (the tests actually bite)

Sandbox `/tmp/hongt-mutate` (copies of the sim sources + the test file; the
repository tree was never mutated). Baseline there: 81/81 pass. Each mutation
was applied alone and reverted afterwards.

| # | Mutation | Killed by |
|---|---|---|
| M1 | `CompanionSlotFanout` slot 1 `64f → 0f` | `…EachSlotHoldsItsLateralFanoutOffTheFollowAnchor` (1 failure) |
| M2 | scout-echo cadence `0.85 → 1.10` | `…ArchetypeTupleTableMatchesTheD63Gate`, `…EachSlotSwingsOnItsOwnArchetypeCadence` (2) |
| M3 | drop the `slots.Contains(id)` dedupe in `NormalizeCompanionSlots` | `…LegacyIdPromotesAndCompanionIdsWinsWithDedupeAndCap` (1) |
| M4 | hold applies to slot 0 only (`\|\| slot > 0` in `UpdateCompanionSlot`) | `…GlobalHoldAndRecallCommandEverySlot` (1) |
| M5 | hold wins the same-tick tie instead of recall | `…GlobalHoldAndRecallCommandEverySlot` + 3 frozen Amendment #3 tests (4) |
| M6 | `CompanionSlotFanout` slot 0 `0f → 8f` (breaks §4 parity) | `…ZeroAndSingleSlotRunsMatchTheLegacySingleIdPath`, `…EachSlotHoldsItsLateralFanout…` + 3 frozen §4/Amendment #3 tests (5) |

M2 initially killed only the table test because the cadence test read its
expectation back from `HackSpec.CompanionStats`; the test was changed to assert
the D6.3 literals (0.85 / 1.30 / 1.45 s) against the observed swing period, and
it now kills M2 as well.

## [OBSERVED] Blocker — Unity batchmode EditMode run

`Unity -batchmode -runTests -testPlatform EditMode` could not be completed:
the operator's Unity Editor is running on this project
(`pid 16568 … -projectpath …/HongT/main`), and both attempts — in the repo and
in the clone `/tmp/hongt-unity-test` — exited during script compilation with no
`results.xml` (clone log tail: `Application is shutting down…` ~30 s in;
`Logs/upm.log`: `parent process … is no longer running`). The editor session was
deliberately left untouched. Re-run once the editor is closed:

```
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath <repo> -runTests -testPlatform EditMode \
  -testResults _workspace/current/engineering/unity-logs/test-results-<hhmmss>.xml \
  -logFile _workspace/current/engineering/unity-logs/tests-<hhmmss>.log -quit
```

[INFERENCE] The Unity run is expected to reproduce the headless result: the new
tests touch only `CinderCourt.Sim` types already referenced by the existing
tests in the same assembly, and no production code was changed in this task.
