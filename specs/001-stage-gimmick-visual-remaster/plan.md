# Implementation Plan: Nine-Stage Gimmick Visual Remaster

**Feature directory**: `001-stage-gimmick-visual-remaster`  
**Date**: 2026-08-11  
**Spec**: [spec.md](spec.md)  
**Requirements SSOT**: Ouroboros Seed `seed_ad5567a58b85`

## Summary

Remaster the presentation of every actual campaign hazard in the nine `StageCatalog`
stages without changing simulation. Each stage receives generated, stage-specific
physical hazard surfaces under `Resources/Textures/Hazards`; `GameView` supplies the
logical stage context to `VfxDirector`; a View-only catalog/resolver binds opaque
underlays or bodies beneath the existing #17c state signals; and
`EnvironmentBuilder` removes redundant dressing around hazard decision space.

`god-tibo-imagen` (`gti`) is the only generative image tool. Generation is serial,
starts with a reference-input dry-run and one real smoke, and records hashes and
consumer mappings. Tests prove the actual stage/hazard bindings, importer policy,
opaque coverage, safe fallbacks, protected-path integrity, WebGL limits, and browser
readability.

## Technical Context

**Language/Version**: C# for Unity 6000.5.6f1; Bash/Node 22 for the generation driver  
**Primary Dependencies**: Unity URP 17.5, `Resources.Load`, existing View material and
primitive helpers, installed `gti` private Codex backend  
**Storage**: PNG assets and JSON provenance in the repository; no runtime database  
**Testing**: Unity EditMode tests, import-only/build scripts, existing WebGL Playwright
smoke primitives plus `tools/qa/capture_stage_hazard_matrix.mjs`, deterministic
image inspection  
**Target Platform**: WebGL deployment at the existing relative-URL site  
**Project Type**: Unity game with deterministic Sim and separate View presentation  
**Performance Goals**: preserve the recorded frame baseline; no per-frame texture
loads, material creation, or managed allocations from the new resolver  
**Constraints**: View-only; textures <=1024; environment vertices <=60k; environment
unique materials <=8; WebGL build <=120 MB; zero browser console/page errors  
**Scale/Scope**: exactly 9 campaign stages, 7 hazard kinds, and 33 unique effective
stage/hazard bindings; Prologue/Arena/Training are fallback-only regression scope

## Constitution Check

*GATE: pass before Phase 0 and re-check after Phase 1.*

| Gate | Design evidence | Status |
|---|---|---|
| Deterministic Sim is immutable | No planned edit under `Assets/Scripts/Sim`; all geometry and timing are read from `HazardState` | PASS |
| Presentation remains View-only | Context enters at `GameView.Begin/EndRun`; resolver and binding live under `Assets/Scripts/View` | PASS |
| Readability precedes decoration | Opaque bed/body -> thin existing state edge -> core -> actors/VFX; dressing is reduced, not expanded | PASS |
| WebGL budgets are hard gates | 512 default, 1024 only for measured band/body needs; cached resources; build/size/perf checks | PASS |
| Generated assets are reproducible | gti dry-run, reference smoke, serial backoff, per-file provenance/hash/import/consumer evidence | PASS |
| Dirty worktree is preserved | `GameDirector`, Sim, Fx, Env originals remain untouched; explicit path checks before/after | PASS |

Post-design re-check: PASS. No exception or constitution violation is required.

## Stage/Hazard Scope

The effective campaign configurations yield 33 unique bindings. Runtime
coverage is validated against the catalog/config source; this table is an acceptance
matrix, not a second simulation truth.

| Act | Stage | Actual hazard kinds |
|---|---|---|
| I | `cinder-span` | EmberVent, StoneWall |
| I | `ember-gallery` | EmberVent, ObsidianPillar, StoneWall |
| I | `abyss-chancel` | EmberVent, ObsidianPillar, StoneWall |
| II | `witness-well` | EmberVent, ObsidianPillar, RelicAltar, StoneWall |
| II | `echo-throne` | EmberVent, RelicAltar, TideCurrent, StoneWall |
| II | `ash-verdict` | EmberVent, RelicAltar, EmberPylon, StoneWall |
| III | `cinder-sluice` | EmberVent, ObsidianPillar, TideCurrent, StoneWall |
| III | `ember-bastion` | EmberVent, ObsidianPillar, EmberPylon, StoneWall |
| III | `ash-march` | EmberVent, RelicAltar, EmberPylon, AshWall, StoneWall |

The total is generated and asserted from current source during task T004 so the plan
cannot silently drift from effective stage overrides or pact extras. `cinder-sluice`
gets `StoneWall` through `DungeonLayoutSpec.Compose`, even though its anchor array
does not list a wall directly.

## Implementation Phases

### Phase 0 — Preflight and generation probe

1. Record HEAD, target-file status and hashes, protected-path diff, current test/build
   evidence, and the existing nine-stage screenshots without changing the worktree.
2. Implement `tools/qa/capture_stage_hazard_matrix.mjs` from the proven three-act
   driver, then collect the pinned rAF baseline against the untouched existing build.
3. Validate installed `gti`, Node, auth-file presence, and actual CLI grammar. The
   installed version uses repeatable `--image <path>` references.
4. Dry-run a representative `cinder-span-ember-vent-underlay` request with floor,
   stone, and existing Fx reference images, then perform one real private-provider
   smoke. A provider that cannot consume the required image references is not an
   acceptable batch fallback; stop the generation lane and retain its evidence.
5. Generate all accepted assets serially with 15/30/60/120/240-second retry backoff
   and an eight-second courtesy interval. Analysis and code/test work may proceed in
   parallel, but no image requests may overlap.

### Phase 1 — Contracts and tests first

1. Add catalog/resolver tests for all actual bindings, non-campaign null context,
   path normalization, load caching, and missing-file fallback.
2. Add importer tests for opacity/alpha, sRGB, wrap, mipmap, compression, and size.
3. Add structural VFX tests that prove every hazard kind receives the required
   texture role while preserving the existing `BuildHazardView(in HazardState)`
   reflection seam and state materials.
4. Tighten environment tests to cap per-hazard dressing and ensure `StoneWall` no
   longer falls through the pylon-furniture default.

### Phase 2 — Generated resources and import pipeline

1. Create `Assets/Resources/Textures/Hazards/` and generate one stage-specific
   physical primary surface for every actual stage/hazard combination. Optional
   body/trim derivatives are added only where the consumer uses them.
2. Use deterministic post-processing only to resize, make band edges tile-safe,
   derive grayscale/alpha masks, and validate dimensions/mode/opacity. The gti
   source remains recorded and no other image model is used.
3. Add a dedicated importer keyed by the filename role:
   - `underlay`, `bed`, `band`, `body`, `albedo`: sRGB, mipmapped, opaque, max 1024.
   - `trim`, `rim`, `edge`, `mask`, `decal`: alpha preserved, max 512/1024.
   - `body` and declared dynamic tiled bands: Repeat; other underlays: Clamp.

### Phase 3 — View-only runtime mapping

1. Add `StageHazardVisualCatalog` for the nine stage tone profiles, resource roles,
   tints, mapping scale and opacity class. It does not own gameplay geometry.
2. Add `StageHazardTextureResolver` with cached `Resources.Load<Texture2D>`, campaign
   stage allow-listing, role lookup, and null-safe fallback.
3. Add `VfxDirector.SetStageContext(string)` and clear it from `ClearTransient`.
   `GameView.Begin` calls it after assigning `_logicalStageId`; `EndRun` clears it.
   `GameDirector` is not modified.
4. Preserve `BuildHazardView(in HazardState)` and bind surfaces inside it:
   - Vent: opaque crater underlay below fill/ring; existing time-to-eruption fill and
     warning ring remain above it.
   - Pillar/StoneWall: stage-stone body albedo plus a restrained opaque contact bed;
     collision dimensions remain sourced from the hazard record.
   - Altar: opaque carved channel/sigil bed below its live channel read.
   - TideCurrent: VFX-only opaque bed exactly matching the Sim band, with the existing
     bright edge/chevrons above it; no environment furniture is added.
   - EmberPylon: opaque scorched aura and stage body albedo; HP band remains canonical.
   - AshWall: cull-safe opaque swallowed band plus a bright front edge; use fixed
     texel-density UV crop/reveal rather than stretching as the band grows.
5. Cache textures and materials at run/build time; per-frame updates only change
   existing color, transform, UV/ST, and active state.

### Phase 4 — Declutter and tone hierarchy

1. In `EnvironmentBuilder.AddGimmickTerrain`, explicitly skip `StoneWall` as already
   owned by `VfxDirector`, retain existing band-hazard skips, and reduce only the
   duplicate furniture whose projected bounds compete with decision space.
2. Keep player skill primary silhouettes/colors unchanged. Stage accents affect
   hazard surfaces, secondary particles, decals, and afterglow only.
3. Use the frozen nine-stage tone matrix in `contracts/visual-acceptance-matrix.md`.

### Phase 5 — Verification and evidence

1. Run generation manifest/hash/image checks, Unity import-only, targeted EditMode
   tests, existing #17c/VFX/environment/digest tests, then the full suite.
2. Recheck hashes/diffs for Sim, Fx, Env originals, `GameDirector`, and any initially
   dirty target before claiming safety.
3. Build WebGL, measure compressed and uncompressed output, then run all-nine-stage
   browser captures with `tools/qa/capture_stage_hazard_matrix.mjs` at 1920x1080,
   1280x720, and 375x667 including entry, normal combat, active hazard/boss, and
   close boundary views. A capture counts only when `GameFlowAgentAPI` reports an
   active wave and the proven HUD red-bar classifier reports `R-G > 20` in
   x=100..420, y=28..52; a completed script with lobby frames fails.
4. Review live and grayscale crops for zero gaps/leaks/seams/UV stretch, distinct
   skill/enemy/hazard reads, bounded clutter, zero browser errors, and no measured
   performance regression. The pinned browser metric is 600 post-warmup
   `requestAnimationFrame` intervals on `echo-throne` at 1280x720 under the same
   Chromium/SwiftShader configuration: median and p95 frame interval may increase by
   at most 10%, and the >33.3ms interval ratio by at most 5 percentage points.
   An independent verifier signs off the final evidence.

## Project Structure

### Documentation

```text
specs/001-stage-gimmick-visual-remaster/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── runtime-binding.md
│   ├── stage-hazard-asset-manifest.schema.json
│   └── visual-acceptance-matrix.md
└── tasks.md
```

### Source and assets

```text
Assets/
├── Editor/HazardTextureImportPipeline.cs
├── Resources/Textures/Hazards/<stageId>-<hazard>-<role>.png
├── Scripts/View/
│   ├── GameView.cs
│   ├── VfxDirector.cs
│   ├── EnvironmentBuilder.cs
│   ├── StageHazardVisualCatalog.cs
│   └── StageHazardTextureResolver.cs
└── Tests/EditMode/
    ├── StageHazardVisualCatalogTests.cs
    ├── HazardTextureImportTests.cs
    └── StageHazardVfxTests.cs

docs/provenance/stage-hazard-textures.json
tools/gen_hazard_textures.sh
tools/qa/capture_stage_hazard_matrix.mjs
```

Protected and unchanged: `Assets/Scripts/Sim/**`, `Assets/Resources/Fx/**`,
`Assets/Resources/Textures/Env/**`, `Assets/Scripts/View/GameDirector.cs`, and
`Assets/Scripts/View/StageCatalog.cs`.

**Structure Decision**: Extend the existing Unity View, Editor, Resources, tests,
tools, and provenance surfaces. No package, assembly, dependency, or simulation
layer is added.

## Complexity Tracking

No constitution violation is required. The two new View classes isolate static tone
data from cached resource loading; merging them into `VfxDirector` was rejected
because it would make a large shared presentation file own manifest, IO, and mapping
policy, while a generalized asset framework was rejected as unnecessary scope.
