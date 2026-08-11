---

description: "Execution tasks for the nine-stage campaign gimmick visual remaster"
---

# Tasks: Nine-Stage Gimmick Visual Remaster

**Input**: Design documents in `specs/001-stage-gimmick-visual-remaster/`  
**Requirements SSOT**: Ouroboros Seed `seed_ad5567a58b85`  
**Scope**: 9 campaign stages, 33 effective stage/hazard bindings, View-only

## Format: `[ID] [P?] [Story] Description`

- **[P]** tasks own different files or are read-only and may run in parallel.
- Generation calls are never parallel even when code/tests are.
- Tests for changed behavior are written and observed failing before implementation.
- Existing dirty work is preserved; no task authorizes reset, checkout, broad staging,
  or edits to Sim, Fx, Env originals, `GameDirector`, or `StageCatalog`.

## Phase 1: Setup and Evidence Freeze

**Purpose**: Freeze the shared-worktree boundary and verify required tools before edits.

- [x] T001 Record current HEAD, `git status`, and SHA-256 hashes for `Assets/Scripts/View/GameView.cs`, `Assets/Scripts/View/VfxDirector.cs`, `Assets/Scripts/View/EnvironmentBuilder.cs`, `Assets/Scripts/View/GameDirector.cs`, `Assets/Scripts/Sim/**`, `Assets/Resources/Fx/**`, and `Assets/Resources/Textures/Env/**` under `_workspace/current/engineering/hazard-texture-gen/preflight.md`
- [x] T002 [P] Record installed `gti --help`, Node version, auth-file presence, and sanitized provider capability evidence under `_workspace/current/engineering/hazard-texture-gen/tool-preflight/`
- [x] T003 [P] Implement `tools/qa/capture_stage_hazard_matrix.mjs` from the proven `_workspace/current/qa/amendment17c-smoke/drive_three_acts.mjs` sequence with configurable viewports/phases, `GameFlowAgentAPI` plus HUD red-bar in-stage assertions, four required capture moments, console/page error collection, and rAF perf mode; record current Unity/EditMode/WebGL/capture inventory in `_workspace/current/engineering/hazard-texture-gen/baseline.md`, then run perf mode against the untouched existing build for 600 post-warmup intervals on `echo-throne` at 1280x720 and write median, p95, and >33.3ms ratio to `_workspace/current/engineering/hazard-texture-gen/perf-baseline.json`

**Checkpoint**: Current shared state and protected paths have recoverable evidence.

---

## Phase 2: Foundational Contracts and Failing Tests

**Purpose**: Lock mapping, import, lifecycle, and declutter behavior before implementation.

**⚠️ CRITICAL**: Implementation tasks T009-T016 start only after their corresponding
tests exist and have been observed failing for the intended reason.

- [x] T004 [P] Add a source-derived 33-pair campaign coverage test, exact stage/kind role expectations, unknown/non-campaign rejection, and path-token tests in `Assets/Tests/EditMode/StageHazardVisualCatalogTests.cs`
- [x] T005 [P] Add importer policy, <=1024, opacity, alpha-role, wrap, mipmap, compression, non-readable, and WebGL override tests in `Assets/Tests/EditMode/HazardTextureImportTests.cs`
- [x] T006 [P] Add stage-context lifecycle, safe fallback, separate physical surface slot, canonical state-layer preservation, Current aspect, AshWall UV crop/right-origin, reduced-motion, and material-cache tests in `Assets/Tests/EditMode/StageHazardVfxTests.cs`
- [x] T007 [P] Add StoneWall no-furniture and bounded per-hazard decision-space dressing tests to `Assets/Tests/EditMode/EnvironmentBuilderTests.cs`
- [x] T008 Run the four targeted EditMode fixtures and save the expected pre-implementation failures under `_workspace/current/engineering/hazard-texture-gen/red-tests/`

**Checkpoint**: Tests fail only for absent hazard catalog/importer/surface/context and
the known StoneWall furniture defect.

---

## Phase 3: User Story 1 — Integrated, Readable Gimmicks (Priority: P1) 🎯 MVP

**Goal**: Every actual gimmick has an opaque physical surface beneath the existing
semantic state layer, with no stale stage context or duplicate StoneWall dressing.

**Independent Test**: Load one stage containing each of the seven hazard kinds,
inspect the built renderer/material hierarchy, cross a boundary, and verify the
underlay/body covers the unrelated floor while the original warning/HP/channel read
remains visible.

### Implementation for User Story 1

- [x] T009 [P] [US1] Implement nine `StageToneProfile` records, 33 source-aligned `HazardSurfaceBinding` records, role/token validation, and no-gameplay-data guarantees in `Assets/Scripts/View/StageHazardVisualCatalog.cs`
- [x] T010 [P] [US1] Implement extensionless path resolution, campaign allow-listing, positive/negative `Resources.Load<Texture2D>` caching, and explicit fallback results in `Assets/Scripts/View/StageHazardTextureResolver.cs`
- [x] T011 [P] [US1] Implement role-driven hazard import policy and WebGL overrides without touching the Env importer in `Assets/Editor/HazardTextureImportPipeline.cs`
- [x] T012 [US1] Add `SetStageContext`, context reset, cached surface/material lifetime, and an independent physical `Surface` slot in `Assets/Scripts/View/VfxDirector.cs` (depends on T009-T010)
- [x] T013 [US1] Wire `Vfx.SetStageContext(_logicalStageId)` after stage assignment and clear it during run teardown in `Assets/Scripts/View/GameView.cs` without modifying `GameDirector.cs` (depends on T012)
- [x] T014 [US1] Map opaque Vent/Altar underlays and Pillar body/contact surfaces below their existing fill/ring/channel layers in `Assets/Scripts/View/VfxDirector.cs` (depends on T012)
- [x] T015 [US1] Map TideCurrent exact-aspect bed, EmberPylon aura/body, StoneWall repeated body/contact, and cull-safe AshWall fixed-density crop/reveal while preserving chevron/front-edge/HP semantics in `Assets/Scripts/View/VfxDirector.cs` (depends on T012)
- [x] T016 [US1] Explicitly skip StoneWall furniture and reduce only proven duplicate decision-space dressing in `Assets/Scripts/View/EnvironmentBuilder.cs`
- [x] T017 [US1] Run targeted catalog/import/VFX/environment tests and existing `VfxRuntimeSheetTests`, `SkillShapeVocabularyTests`, `EnvironmentBuilderTests`, `StageDressingTests`, `DungeonFramingAndMoodTests`, and `DungeonGoldenDigestTests`; save green evidence under `_workspace/current/engineering/hazard-texture-gen/runtime-tests/`

**Checkpoint**: With placeholder/missing resources, all modes retain safe existing
visuals; with test textures, all seven kinds compose in the required hierarchy.

---

## Phase 4: User Story 2 — Exact Stage Tone Across Three Acts (Priority: P2)

**Goal**: Generate and import the 33 required physical primary textures in the frozen
nine-stage tone matrix and bind them without changing skill primary language.

**Independent Test**: Review one entry, combat, active signature hazard, and boundary
frame per stage against `contracts/visual-acceptance-matrix.md`, including grayscale.

### Generation and Integration for User Story 2

- [x] T018 [P] [US2] Implement the 33-entry stage/hazard prompt matrix, repeatable `--image` references, dry-run/smoke/generate modes, serial retry/backoff, resume behavior, and deterministic output validation in `tools/gen_hazard_textures.sh`
- [x] T019 [P] [US2] Create the complete provenance skeleton conforming to `contracts/stage-hazard-asset-manifest.schema.json` in `docs/provenance/stage-hazard-textures.json`
- [x] T020 [US2] Run `tools/gen_hazard_textures.sh --dry-run` for all 33 prompts and save sanitized request evidence under `_workspace/current/engineering/hazard-texture-gen/dry-run/` (depends on T018)
- [x] T021 [US2] Run one real private-provider `cinder-span-ember-vent-underlay` smoke with floor, stone, and Fx `--image` references; visually inspect it and record provider/model/output/debug evidence before batch generation (depends on T020)
- [x] T022 [US2] Generate Act I assets serially into `Assets/Resources/Textures/Hazards/` with 15/30/60/120/240-second retry backoff and eight-second success gaps (depends on T021)
- [x] T023 [US2] Generate Act II assets serially into `Assets/Resources/Textures/Hazards/` using the same accepted tool/provider/reference contract (depends on T022)
- [x] T024 [US2] Generate Act III assets serially, including generated cinder-sluice StoneWall and fixed-density AshWall/Current band sources, into `Assets/Resources/Textures/Hazards/` (depends on T023)
- [x] T025 [US2] Deterministically resize/derive only declared roles, validate opaque interior/edge behavior/mode/dimensions, compute SHA-256, and complete every accepted/rejected record in `docs/provenance/stage-hazard-textures.json` (depends on T024)
- [x] T026 [US2] Run Unity import-only and all hazard resource/import/binding tests; reject any missing pair, >1024 texture, wrong wrap/alpha, flat albedo, or unbound consumer and save evidence under `_workspace/current/engineering/hazard-texture-gen/import/` (depends on T025)

**Checkpoint**: All 33 effective pairs resolve accepted, imported, stage-specific
physical textures; skill silhouettes and semantic primary colors are unchanged.

---

## Phase 5: User Story 3 — Reproducible and Regression-Safe Delivery (Priority: P3)

**Goal**: Prove provenance completeness, protected-path integrity, full regression,
WebGL budget, browser stability, no leaks/UV defects, and readability for all stages.

**Independent Test**: Follow `quickstart.md` from manifest validation through a fresh
build and screenshot matrix using only repository artifacts.

### Verification for User Story 3

- [x] T027 [P] [US3] Add/extend manifest validation for 33 exact pairs, required provenance fields, file hashes, runtime consumers, and rejection of stale/extra mappings in `Assets/Tests/EditMode/StageHazardVisualCatalogTests.cs`
- [x] T028 [P] [US3] Extend `Assets/Tests/EditMode/DungeonFramingAndMoodTests.cs` to verify every stage binds distinct stone/floor resources plus non-flat hazard albedo and positive `_BaseMap_ST`
- [x] T029 [US3] Run `bash tools/unity_batch.sh import-only` and `bash tools/unity_batch.sh tests`; inspect logs and save fresh results under `_workspace/current/engineering/hazard-texture-gen/final-tests/`
- [x] T030 [US3] Recompute protected-file hashes and diffs for Sim, Fx, Env originals, `GameDirector`, and initially dirty targets; document only intended View/Editor/test/tool/resource/provenance changes in `_workspace/current/engineering/hazard-texture-gen/integrity.md`
- [x] T031 [US3] Run a fresh `bash tools/unity_batch.sh build`, verify relative URLs, record compressed/uncompressed WebGL size <=120 MB, and save build logs under `_workspace/current/engineering/hazard-texture-gen/build/`
- [x] T032 [US3] Run the already-implemented `tools/qa/capture_stage_hazard_matrix.mjs` across all nine stages at 1920x1080, 1280x720, and 375x667 with entry/combat/active-hazard-or-boss/close-boundary captures, zero console/page errors, `GameFlowAgentAPI` active-wave assertion, and the proven HUD red-bar classifier (`R-G > 20` at x=100..420, y=28..52); write reports/screenshots under `_workspace/current/qa/stage-hazard-remaster/` and fail any lobby frame
- [x] T033 [US3] Perform independent live/grayscale visual review for tone distinction, zero floor leak/gaps/backface loss/UV stretch/seams, actor-skill-enemy-hazard separation, and bounded clutter; record the acceptance matrix in `_workspace/current/qa/stage-hazard-remaster/report.md`
- [x] T034 [US3] Use `tools/qa/capture_stage_hazard_matrix.mjs --phase perf --stage echo-throne --viewport 1280x720 --frames 660` under the same Chromium/SwiftShader configuration; discard the first 60 rAF intervals, compare the next 600 with T003, require median and p95 <= baseline * 1.10 and >33.3ms ratio <= baseline + 0.05, and record the result in `_workspace/current/engineering/hazard-texture-gen/performance.md`

**Checkpoint**: Every Seed acceptance criterion has fresh, linked evidence.

---

## Phase 6: Polish and Independent Completion Review

**Purpose**: Remove incidental complexity and verify the handoff without widening scope.

- [x] T035 [P] Run a focused code review for material lifetime, `Resources` caching, fallback safety, shared-material mutation, WebGL shader compatibility, and Sim/#17c invariants across `Assets/Scripts/View/StageHazardVisualCatalog.cs`, `Assets/Scripts/View/StageHazardTextureResolver.cs`, `Assets/Scripts/View/GameView.cs`, `Assets/Scripts/View/VfxDirector.cs`, `Assets/Scripts/View/EnvironmentBuilder.cs`, and `Assets/Editor/HazardTextureImportPipeline.cs`
- [x] T036 Apply only review fixes required by the frozen Seed, rerun affected targeted tests, and avoid new abstractions or dependencies
- [x] T037 Run `specs/001-stage-gimmick-visual-remaster/quickstart.md` end-to-end, confirm no unchecked task or missing evidence remains, and obtain independent verifier sign-off

---

## Dependencies and Execution Order

### Phase dependencies

- Phase 1 starts immediately; T002 and T003 are parallel, and T003 owns only the new
  committed QA harness plus generated evidence.
- Phase 2 depends on T001 for frozen path evidence. T004-T007 are parallel; T008 joins them.
- Phase 3 and T018-T019 may start after T008; they own different files.
- Runtime implementation follows T009/T010 -> T012 -> T013/T014/T015. Only one owner edits `VfxDirector.cs`.
- Generation is strictly T020 -> T021 -> T022 -> T023 -> T024 -> T025 -> T026.
- Phase 5 depends on both runtime green T017 and generated/imported assets T026.
- Phase 6 depends on Phase 5 evidence.

### User story independence

- **US1** is testable with fixture textures and safe fallback before the final art batch.
- **US2** is testable by the generated/imported 33-pair matrix and tone captures; it
  uses US1's resolver but does not change gameplay.
- **US3** independently validates reproducibility and non-regression after US1+US2.

### Parallel ownership

- Catalog/resolver, importer/tests, and generation/provenance may be separate agents.
- `VfxDirector.cs` has one implementation owner; `GameView.cs` and
  `EnvironmentBuilder.cs` are patched only after their current hashes match T001.
- Browser capture can prepare harness/config while Unity tests/build run, but captures
  use only the freshly completed build.

## Completion Rule

Do not mark complete until T001-T037 are checked, the 33 source-derived bindings are
present and consumed, all automated and visual gates pass, protected paths match the
recorded boundary, the WebGL build is <=120 MB with zero browser errors, and an
independent verifier confirms the Seed acceptance criteria.
