# Resource Manifest — active five-cycle program with carried cycle-2 history

## Program identity

- `[OBSERVED]` Active run-id: `20260808-achilles-quality`.
- `[OBSERVED]` Public beat: **NAN 2026 final submission**.
- `[TARGET]` Achilles: Legends Untold is a play-concept benchmark only; no Greek name, story, image, model, sound, layout, or other third-party asset enters HongT.
- `[TARGET]` Cycle 9 requests no mechanic/resource change; future resources remain proposal-only until the owning stage and director decision approve them.

## Carried cycle-2 baseline

- `[OBSERVED]` The cycle-2 three-stage extension imported no new runtime asset and reused existing Resources terrain, dressing, character, icon, audio, and font paths.

| Use | Carried asset | Disposition |
|---|---|---|
| Terrain | `[OBSERVED]` `terrain-abyss-chancel`, `terrain-cinder-span`, `terrain-echo-throne` | `[OBSERVED]` Existing prefabs reused. |
| Dressing | `[OBSERVED]` `terrain-cinder-span` child library | `[OBSERVED]` Existing parts reused through code-owned placement tables. |
| Boss fallback | `[OBSERVED]` `shadow-commander-boss`, `broken-court-monarch-boss` | `[OBSERVED]` Existing prefabs plus material-property tint. |
| Card glyphs | `[OBSERVED]` `skill-dash`, `skill-ward`, `skill-strike` | `[OBSERVED]` Existing `Resources/Icons` textures. |
| Gimmick VFX | `[OBSERVED]` pooled code-generated quads/cylinders/rings in `VfxDirector` | `[OBSERVED]` No imported mesh or texture required. |
| Companion reward | `[OBSERVED]` `scout-echo` resolves from the existing scout resource plus view tint | `[OBSERVED]` No new character resource required. |

## Active audited inventory

| Resource lane | Source paths/symbols | Active use | Five-cycle decision |
|---|---|---|---|
| Player/enemy prefabs | `[OBSERVED]` `Assets/Resources/Characters/{human-command-boss,lantern-reaver,ember-cohort,scout,shade,guard,possessed}.prefab`; `GameBootstrap.Awake/LoadEnemy` | `[OBSERVED]` Player fallback, ordinary enemies, companion/echo bases. | `[TARGET]` Reuse; no imported Achilles asset. |
| Boss archetype prefabs | `[OBSERVED]` `Assets/Resources/Characters/{s1-cinder-warden,s2-veil-tactician,s3-gate-sovereign}.prefab`; `GameBootstrap.BossArchetypePrefab` | `[OBSERVED]` Warden/Tactician/Sovereign model selection with cached `Resources.Load`. | `[TARGET]` Reuse for cycle-12 phase/finish presentation; retain fallback on missing resource. |
| Boss fallback prefabs | `[OBSERVED]` `shadow-commander-boss.prefab`, `broken-court-monarch-boss.prefab`; `StageCatalog.BossPresentation`, `GameView.RentBoss` | `[OBSERVED]` Generic stage visuals and fallback when an archetype resource is absent. | `[TARGET]` Preserve; do not delete after reskin validation. |
| Terrain prefabs | `[OBSERVED]` `Assets/Resources/Terrain/{terrain-abyss-chancel,terrain-cinder-span,terrain-echo-throne}.prefab`; `GameDirector.SetStageTerrain` | `[OBSERVED]` Logical-stage terrain swap with the base court retained beneath. | `[TARGET]` Reuse; cycle-11 coordination changes are data/sim-owned, not new terrain. |
| Terrain effect sheets | `[OBSERVED]` `terrain-fx-{shift,ice,lava}-sheet.png`; `TerrainFlipbook` | `[OBSERVED]` Existing terrain-effect animation sheets. | `[TARGET]` Reuse only if an approved HongT-native cue maps to an existing effect; otherwise record a new asset request before import. |
| Stage-entry scenes | `[OBSERVED]` `Assets/Resources/Scenes/scene-stage-entry-{cinder-span,ember-gallery,abyss-chancel,witness-well,echo-throne,ash-verdict,cinder-sluice,ember-bastion,ash-march}.png` plus generic `scene-stage-entry.png` | `[OBSERVED]` Nine logical-stage entry frames plus fallback. | `[TARGET]` Preserve nine-stage source truth; stale six-stage release copy is not an asset contract. |
| Boss/transition scenes | `[OBSERVED]` `scene-boss-entry.png`, `scene-boss-entry-ash-march.png`, `scene-transition.png`, `scene-ember-rest.png`, `scene-intro.png` | `[OBSERVED]` Boss loading/transition/brand/reward presentation. | `[TARGET]` Cycle 12 first reuses these; a new scene image requires worldview/provenance and WebGL-size review. |
| Combat audio | `[OBSERVED]` `Assets/Resources/Audio/cue-{strike,hit,kill,nova,ward,wave,pickup,gameover}.mp3`; `AudioDirector` | `[OBSERVED]` Existing event-driven strike/hit/kill/skill/wave/boss-phase layers. | `[TARGET]` Cycle 10 retunes routing/levels only after QA evidence; no new audio is required by the smallest slice. |
| UI/loot audio | `[OBSERVED]` `cue-{click,toast,loot-fine,loot-epic,footstep,lore}.mp3` | `[OBSERVED]` Lobby, loot, movement, and lore cues. | `[TARGET]` Reuse for growth clarity and owner consolidation. |
| Music | `[OBSERVED]` `bgm-{intro,lobby,stage,loading}.mp3` and `cue-bgm.mp3`; `AudioDirector.SetBgmContext` | `[OBSERVED]` Existing route contexts. | `[TARGET]` Reuse; boss-phase improvement does not require a new music state unless approved separately. |
| HUD/action icons | `[OBSERVED]` `Assets/Resources/Icons/skill-{strike,dash,ward,nova,bolt,pulse,aegis}.png`, equipment/pickup/stat/HUD textures | `[OBSERVED]` Existing action, growth, loot, and HUD vocabulary. | `[TARGET]` Reuse; the Lobby survivor remains the only equipment/sigil presentation owner. |
| Lobby rail icons | `[OBSERVED]` `ui-{sanctum,sortie,map,codex,abandon}.png` | `[OBSERVED]` Current navigation rail. | `[TARGET]` Reuse during duplicate-owner cutover; no second equipment/sigil icon vocabulary. |
| Korean font | `[OBSERVED]` `Assets/Resources/Fonts/HudKorean.otf` plus built-in legacy fallback in view code | `[OBSERVED]` Player-visible Korean text. | `[TARGET]` Any new boss/guard/growth string must pass existing glyph coverage before release. |

## Cycle resource plan

| Cycle | Required resource change | Resource budget and provenance |
|---|---|---|
| 9 | `[TARGET]` None; delete duplicated Meta equipment/sigil UI construction while retaining existing Lobby assets. | `[TARGET]` Net runtime object count should fall; no import/provenance change. |
| 10 | `[TARGET]` None for the minimum action-feel slice; reuse current attack clips, impact VFX pools, camera, and audio cues. | `[TARGET]` Witness Guard remains proposal-only behind a director Stage-1 amendment, deterministic digest coverage, and a readable cue of at least 0.30s; any required new icon/animation must be separately listed before implementation. |
| 11 | `[TARGET]` None for deterministic group-AI/growth clarity; use `DifficultySpec`, `ProgressionGuide`, Lobby labels, and existing enemy prefabs. | `[TARGET]` No model, terrain, icon, or sound import without measured evidence that current readability fails. |
| 12 | `[TARGET]` Reuse three boss archetype prefabs, stage/boss frames, MPB tint, existing cue set, HUD frame, and VFX pools. | `[TARGET]` A new finish asset is optional, not assumed; if approved, record generator/source, license/provenance, import settings, fallback, size, and reduced-motion behaviour before use. |
| 13 | `[TARGET]` None for performance/stability and external evidence export. | `[TARGET]` Optional production telemetry must be code/data only and cannot add a player-frame asset load. |

## WebGL constraints and evidence

- `[OBSERVED]` `Assets/Tests/EditMode/WebGlTextureCapTests.cs` enforces default and WebGL texture importer maximums of 1024.
- `[OBSERVED]` `engineering/unity-logs/build-185425.log` reports the retained WebGL build at 104,803,048 bytes with zero build errors.
- `[TARGET]` Retain the carried ≤120MB WebGL build-size budget and report the actual size after any approved resource change; the active build currently has 15,196,952 bytes of headroom against the decimal 120,000,000-byte ceiling.
- `[TARGET]` Every newly approved texture is capped at 1024 for both default and WebGL import settings, has a fallback path, and is exercised in the released browser.
- `[TARGET]` Every generated/imported asset records source/generator, license or usage basis, generation parameters when available, owning stage/symbol, and exact `.meta` settings in its lane report.
- `[TARGET]` Asset review includes Unity import, focused asset tests, WebGL build size/errors, browser missing-resource warnings, visual readability at 375×667, and a 30-minute mixed-route memory check.

## Resource defect dispositions

| ID | Finding | Recommendation | Reason |
|---|---|---|---|
| RES-01 | `[OBSERVED]` `StageCatalog` retains generic boss resource ids while `GameView.RentBoss` first asks `GameBootstrap.BossArchetypePrefab` for the three reskins. | `[TARGET] DEFER removal of generic prefabs; document both as archetype-first plus fallback.` | `[OBSERVED]` The fallback is intentional and keeps a build playable when a reskin is missing. |
| RES-02 | `[OBSERVED]` Only ash-march has a stage-specific boss-entry frame; other bosses use the generic frame. | `[TARGET] DEFER new boss frames until cycle-12 G4/G8 scoring proves the generic frame insufficient.` | `[INFERENCE]` Missing visual variety is not yet a measured readability defect, and new art is not needed for the smallest source slice. |
| RES-03 | `[OBSERVED]` Cycle-8 source truth is nine stages and 808/808 EditMode tests, while QA reports stale NAN-facing six-stage/166-test copy elsewhere. | `[TARGET] FIX release-facing copy in its owning lane; do not remove three stage resources to match stale text.` | `[OBSERVED]` Runtime resource inventory and retained test XML are the stronger source evidence. |
| RES-04 | `[OBSERVED]` No active-run p95/input/30-minute memory evidence exists for these resources. | `[TARGET] FIX the evidence gap before G6 PASS; keep new imports deferred until a baseline exists.` | `[OBSERVED]` A successful build and texture-cap test do not prove runtime frame or memory stability. |
