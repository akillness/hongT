# HUD atlas — EditMode batchmode verification

[OBSERVED] Ran `Unity -batchmode -runTests -testPlatform EditMode` against an
isolated git worktree (`/private/tmp/hongt-head`, checked out at this
commit's tree) instead of the shared project directory — `Temp/UnityLockfile`
in the shared directory is held by a live interactive Editor (PID 16568) per
CLAUDE.md §5, and a worktree gets its own `Temp/`/`Library/` so the two Unity
processes never contend for the same lockfile or on-disk cache.

Command (repeated with and without `-nographics` — identical result both
ways, ruling out a headless-graphics-only cause for the 2 failures below):

```
Unity -batchmode -projectPath <worktree> -runTests -testPlatform EditMode \
  -testResults .../editmode-results.xml -logFile .../editmode-run.log
```

## Result: 274/276 passed

Raw NUnit3 summary: `testcasecount="276" result="Failed(Child)" passed="274"
failed="2"` — full XML committed alongside this file as
`hud-atlas-editmode-results.xml`.

All 14 `HudLayoutTests` passed, including:
- `PhoneDungeon_SkillRow_DoesNotCoverReadouts` / `DesktopDungeon_SkillRow_DoesNotCoverReadouts`
  — the §U1 overlap graders that specifically cover `_shieldRect`, which
  this pass resized 200x24 -> 190x28 (bare Text -> backed Panel).
- `ResetRunUi_ReseedsHealthBarForNewRun` — asserts
  `HealthFill().fillAmount` actually changes across a `Sync()` call, i.e.
  the gauge's visible fill still animates after wiring the new fill/frame
  sprites onto the same `Image.Type.Filled` component.

The 2 failures are pre-existing and unrelated to this session's change:
- `HudIconIntegrationTests.ApplyIcon_SetsSprite_AndMaterial`
- `HudIconIntegrationTests.GlowShader_RendersWithoutCompileErrors`

Both assert a custom URP glow `Material`/shader compiles under Editor
batchmode (`Hidden/InternalErrorShader` / `shader.isSupported == False`),
reproduced identically with and without `-nographics`, in
`HudIconIntegration.cs` (the older skill/equip/pickup icon glow system) —
a file this session never touched.

## Additional scratch verification (not committed)

A throwaway reflection-based EditMode test fixture
(`_ScratchHudAtlasVerifyTests.cs`, deleted after the run) exercised the two
new behaviors that had no existing assertion coverage:

- `DashCard_FrameSprite_SwapsOnReadyState` — drove `SyncDungeon` with
  `dashCooldown` 1.4 -> 0 -> 1.4 and asserted the dash card's frame `Image`
  sprite name goes `hud-skill-card-frame` -> `hud-skill-card-frame-ready` ->
  `hud-skill-card-frame`. **Passed.**
- `ShieldPanel_TogglesActive_AndCarriesFrame` — asserted the new
  `_shieldPanel` starts `SetActive(false)`, carries a `Frame` child with
  `hud-shield-readout-frame` sprite and `Image.Type.Sliced`, activates when
  `SyncDungeon` reports shield > 0, and deactivates again at shield == 0.
  **Passed.**

Both scratch tests passed on the first run; no fix-and-rerun cycle was
needed. Real Play Mode / WebGL rendering has not been visually inspected
this session — only geometry, sprite wiring, and dynamic-value assertions
through EditMode.
