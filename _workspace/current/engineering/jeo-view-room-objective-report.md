# LANE REPORT — View / room objective chip (owner: jeo)

Commit `37ecbc5 feat(view): per-room objective on the dungeon route + HUD objective chip`
Deploy `gh-pages 458d362`, run 2026-08-07.

## Scope delivered

Spec source: `_workspace/current/design/deep-interview-cinder-court-dungeon-revival.md`
— the requirement *"each room needs a distinct objective"* + *"the HUD must expose the
current room objective"*. This is the View-only slice of that spec; the reward deck,
Ember Rest prep state and the two-phase Tribunal Arena remain Sim-owned and are NOT
part of this change.

| File | Change |
|---|---|
| `Assets/Scripts/View/StageCatalog.cs` | `StageEntry.RoomObjective` + `StageCatalog.ObjectiveFor(stageId)`; all 6 room constructors carry their own objective line |
| `Assets/Scripts/View/HudView.cs` | objective chip (top center, y −108), `SyncRoomObjective(objective, bossAlive)`, `RoomObjectiveReadout` QA seam; cleared by `ResetRunUi`, relabel forced by `RefreshDungeonStage` |
| `Assets/Scripts/View/GameView.cs` | resolves the objective once per `Begin`, syncs it each frame with `_sim.BossAlive`; arena/prologue resolve to `""` |
| `Assets/Scripts/View/ActorView.cs` | pose-window arm/decay ordering fix (below) |
| `Assets/Tests/EditMode/RoomObjectiveTests.cs` | 18 new EditMode cases |

Placement `[OBSERVED]`: the chip is parked at top-center y −108 because the boss bar
occupies the −58..−104 band (`HudView` boss-bar block); the two can never overlap.
Both the chip panel and its label are `raycastTarget == false`, which the mobile
layout contract (c) requires and `Chip_NeverInterceptsPointerInput` pins directly.

## Incidental defect found and fixed — ActorView pose windows

`[OBSERVED]` Adding the new fixture made `SwingPacingTests.
EnemyChaseStep_NeverOpensTheLaunchWindow_ButAComboLaunchDoes` fail in 2 of 3 runs
while the same suite at HEAD passed 3 of 3.

`[OBSERVED]` Cause: `ActorView.Apply` armed `_knockbackTime` (0.18 s,
`HackSpec.ComboKnockbackTime`) in `SyncEnemy`/`SyncPlayer` and then subtracted
`Time.deltaTime` from it inside the *same* call. `Time.timeScale = 0` does not zero
`Time.deltaTime` in EditMode, so a single long editor frame drained the whole window
before `ResolveActionValue` ever read it — the launch reaction depended on frame
length, not on the sim. `_castPoseTime` (0.30 s) and `_roarTime` (1.1 s) had the
identical ordering.

Fix: each window now skips exactly one decay — the sync that armed it — which is the
rule the hit flash a few lines below in the same method already followed
(*"the frame that arms it must not immediately spend a delta against it"*). The
armed flags are cleared in `ResetForPool` so a pooled actor cannot inherit a grant.

## Verification `[OBSERVED]`

- EditMode, clone `/tmp/hongt-build`, `bash tools/unity_batch.sh tests`:
  **383/383 passed, 0 failed**, green on 4 consecutive runs
  (`test-results-103834 / 105220 / 105246 / 105304.xml`). Baseline at HEAD was 365/365.
- Mutation check: forcing `SyncRoomObjective`'s `active` to `false` failed exactly the
  6 HUD chip tests (`test-results-104021.xml`) — the gates bite, they are not tautologies.
- Pre-change baseline for the flake: HEAD-only sources in the same clone passed
  365/365 three times (`test-results-104250 / 104525 / 104545.xml`).
- WebGL build: `bash tools/unity_batch.sh build` →
  `[BuildWebGL] result=Succeeded size=70619948 errors=0 warnings=8` (`build-105606.log`).
- Deploy: `bash tools/deploy/deploy_pages.sh …` → `gh-pages 458d362`.
- Live `https://akillness.github.io/hongT/` — all HTTP 200, sizes equal to the local
  build: index 9317 B, loader 48106 B, wasm 10478554 B, data 36189852 B,
  framework 79052 B.

## Not done / carried forward

- No in-browser screenshot of the chip in a live dungeon room yet; the HUD behaviour is
  currently pinned by EditMode gates only.
- Reward deck, Ember Rest prep state and the two-phase Tribunal Arena from the same
  interview spec are untouched (Sim + `CampaignStore` scope, different lane).
