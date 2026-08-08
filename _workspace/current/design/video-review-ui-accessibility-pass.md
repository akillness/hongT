# Video-review UI accessibility pass — sortie controls

2026-08-07 · scope: `Assets/Scripts/View/LobbyView.cs` and its EditMode layout gate only. No `Assets/Scripts/Sim/` changes.

## Evidence

[OBSERVED] The supplied review transcript (`/tmp/ytana/transcript.txt`) frames a satisfying action RPG around legible difficulty choice, growth, equipment, and deliberate combat inputs. It is a review of another game, therefore it is a comparison axis rather than a HongT design authority.

[OBSERVED] `Assets/Tests/EditMode/LobbyLayoutTests.cs` previously measured every primary sortie action at 390×844 portrait. The entire route grammar was below the project’s 44 CSS px minimum on its vertical axis: `재훈련` was 54.7×21.5 CSS px; stage descent/pact/training tier/trial-entry actions were 41.0×13.7 CSS px. The test intentionally preserved that defect as a debt table.

[INFERENCE] A player cannot meaningfully make the video’s advocated choices—difficulty, route, or practice—if their mobile route action is a 13.7 px high target. Enlarging a transparent target alone would create overlapping descent targets in the 70 u scroll pitch, exchanging an accessibility failure for a routing failure.

## Implemented UI pass

[TARGET] At the portrait phone tier, the complete sortie route grammar has an actual 92 u action plate (44.9 CSS px at the audited 0.488 CSS px/u scale), and its scroll cards expand from 68 u / 70 u pitch to 106 u / 112 u pitch. The scroll content grows with that pitch; no route action shares a tap rectangle with the next row.

- The prologue/retraining action expands to 112×92 u and its card grows to 112 u.
- Every dungeon descent and its post-clear pact action expands to 92×92 u; stage status and reward copy move into the remaining left column.
- Training tier choices and trial-entry actions use the same 92×92 u plate and 106 u row.
- Desktop keeps the previous compact 68 u / 70 u grammar; the expansion is applied only when the responsive lobby stacks for the small effective width.
- `LobbyView.ApplyLobbyLayoutForTest(390, 844)` is an injected geometry seam. It keeps the accessibility test honest: EditMode cannot rely on runtime `Screen` dimensions.

## Verification

[OBSERVED] Isolated Unity EditMode execution at `/tmp/hongT-ui-touch`:

- Focused `CinderCourt.Tests.LobbyLayoutTests`: **3/3 passed**, result `/tmp/hongT-ui-touch/lobby-layout-results.xml`.
- Full EditMode execution: **595/596 passed, 1 failed**. The only failure was pre-existing/unrelated `DungeonFramingAndMoodTests.StageTextures_ExistForEveryStage_AndTileWithoutSmearing`, reporting missing generated `Textures/Env/cinder-sluice-floor`; no lobby test failed. Result `/tmp/hongT-ui-touch/test-results.xml`.

[INFERENCE] The pass removes `재훈련`, `강하`, `서약`, `견습`, `숙련`, `판결`, and `수련` from the measured touch-target debt. The remaining debt is Sanctum’s separate dense fixed-height tab/row grammar (`성장`, `장비`, `군단`, `각인`, and three stat `+` controls); this change intentionally does not disguise it with overlapping hit boxes.
