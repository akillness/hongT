# HongT lobby sortie touch-target pass

[OBSERVED] `Assets/Tests/EditMode/LobbyLayoutTests.cs` measured the initial lobby’s route controls at the project’s 390×844 portrait reference scale (0.488 CSS px per canvas unit). `강하`, `서약`, training tier, and training entry were 84×28 u = 41.0×13.7 CSS px. `재훈련` was 112×44 u = 54.7×21.5 CSS px. All violate the 44 CSS px minimum in `docs/SIM_SPEC_HACKSLASH.md` §9.

[INFERENCE] Enlarging only an invisible hit area in a scroll list is not an accessibility repair: at the original 70 u card pitch, adjacent route targets overlap and clicks become ambiguous. The visual card, action target, and scroll pitch must change as one layout unit.

[TARGET] `Assets/Scripts/View/LobbyView.cs` applies a phone-only sortie layout when the responsive lobby stacks: 92×92 u route actions, 106 u cards, 112 u pitch (prologue: 112 u card / 112×92 u action). The stage scroll content height tracks the current pitch. Desktop keeps the compact 68 u / 70 u layout.

[OBSERVED] `LobbyView.ApplyLobbyLayoutForTest(390, 844)` injects layout geometry into EditMode, where `Screen` is not a valid phone-layout source. `LobbyLayoutTests.PrimarySortieActions_ClearThe44CssPxTouchFloor` passed (3/3 focused fixture, `/tmp/hongT-ui-touch/lobby-layout-results.xml`) for prologue/retraining, all descents, a revealed pact, all training tiers, and all training entries.

[OBSERVED] The same full isolated EditMode run passed 595/596. Its sole failure is unrelated generated environment texture absence (`DungeonFramingAndMoodTests`: `Textures/Env/cinder-sluice-floor` missing), not a lobby layout failure; result `/tmp/hongT-ui-touch/test-results.xml`.
