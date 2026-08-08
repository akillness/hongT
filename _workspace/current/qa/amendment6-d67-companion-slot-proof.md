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

## [OBSERVED] Unity EditMode run — 259/259 pass

Run in the clone `/tmp/hongt-unity-test` (same Unity 6000.5.6f1, `Assets/Scripts/Sim/`
and `Assets/Tests/` rsynced from the repo working tree, so the editor session the
operator has open on the repo was never disturbed):

```
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath /tmp/hongt-unity-test \
  -runTests -testPlatform EditMode \
  -testResults /tmp/hongt-unity-test/results.xml \
  -logFile /tmp/hongt-unity-test/tests.log
exit 0 — total=259 passed=259 failed=0 skipped=0 duration=3.04 s
```

Artifacts copied into the repo:
`_workspace/current/engineering/unity-logs/test-results-145745.xml` (the
matching `tests-145745.log` sits next to it but stays untracked — `*.log` is
gitignored). All 20 companion-related cases pass, including the 8 new
`CompanionSlots_*` ones; `CinderCourt.Tests.EditMode` compiles clean (warnings
only, all pre-existing `FindObjectsByType` obsolescence notices), which also
settles the open question about `internal NormalizeCompanionSlots` — the tests
reach it only through the public `CompanionSlots()` surface, so no
`InternalsVisibleTo` is required by the Unity asmdef either.

### Why the earlier attempts produced no `results.xml`

Two independent mistakes, neither of them a licensing or editor-lock problem
(the earlier "editor holds the project" reading was wrong):

1. The Unity process was launched with `nohup … &` from a tool call that
   returns immediately; the process group was torn down ~1 s later. The
   `Logs/upm.log` line `parent process [54277] is no longer running` is that
   teardown, not a crash.
2. Once run in the foreground it exited 0 but still wrote no results, because
   `-quit` was passed alongside `-runTests`. The test runner is asynchronous;
   `-quit` closes the editor at the end of the first update loop, before the
   run finishes. **`-runTests` must not be combined with `-quit`** — the runner
   exits by itself (0 = all pass, 2 = failures).

A third, unrelated failure appeared on the first successful run:
`BuildScriptWebGlPostprocessTests.PolishIndexHtml_ResyncsResponsiveBackingStore…`
threw `FileNotFoundException: WebGL social preview was not copied`, because the
clone had no `docs/branding/cinder-court-link-preview.png`
(`BuildScript.SocialPreviewSource`, outside the rsynced `Assets/` subtree).
Copying that file in made the suite fully green; nothing in the repo tree was
at fault.

