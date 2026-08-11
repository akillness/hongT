# Feature Specification: Nine-Stage Gimmick Visual Remaster

**Feature Branch**: `001-stage-gimmick-visual-remaster`  
**Created**: 2026-08-11  
**Status**: Frozen from Ouroboros Seed `seed_ad5567a58b85`  
**Input**: Apply the approved three-act tone bible to all nine campaign stages,
integrate gimmick surfaces with stage materials, remove background leakage and
visual clutter, and preserve deterministic gameplay and AMENDMENT #17c.

## User Scenarios & Testing

### User Story 1 - Integrated, Readable Gimmicks (Priority: P1)

As a player, I can identify the occupied area and state of every campaign gimmick
without seeing unrelated floor art through it or losing actors, skills, and enemy
telegraphs in stacked overlays.

**Why this priority**: The current translucent hazard beds make the arena noisy and
directly interfere with play focus.

**Independent Test**: Enter each campaign stage, trigger every present hazard, move
across and around its boundary, and compare fixed gameplay and close-up captures at
desktop and mobile resolutions.

**Acceptance Scenarios**:

1. **Given** any of the nine campaign stages, **When** a hazard is idle, warning, or
   active, **Then** its physical bed covers the unrelated base floor while a thin
   state edge remains immediately readable.
2. **Given** a hazard, enemy attack, and player skill on the same screen, **When**
   effects overlap, **Then** their silhouettes and semantic colors remain distinct.
3. **Given** stage dressing around a hazard, **When** the player reads the safe and
   unsafe space, **Then** props and duplicate rings do not occupy or obscure the
   decision area.

---

### User Story 2 - Exact Stage Tone Across Three Acts (Priority: P2)

As a player and art reviewer, I can distinguish all nine stages through coherent
material, color, and secondary VFX treatment while the global skill language stays
stable.

**Why this priority**: Strong concept art already exists, but runtime gimmicks use a
generic cyan/orange debug-overlay vocabulary that collapses stage identity.

**Independent Test**: Review one entry, one normal-combat, and one boss/hazard frame
for each stage against the frozen tone matrix and a grayscale readability pass.

**Acceptance Scenarios**:

1. **Given** Act 1, **When** reviewing its three stages, **Then** basalt/ember,
   fire-blackened gallery, and violet oath-cathedral materials are distinct.
2. **Given** Act 2, **When** reviewing its three stages, **Then** wet jade witness,
   blue-granite echo current, and smoky-gold ash judgment are distinct.
3. **Given** Act 3, **When** reviewing its three stages, **Then** iron sluice,
   ember/iron bastion, and desaturated execution march are distinct.
4. **Given** Nova, Ward, Dash, Bolt, or Pulse, **When** cast in any stage, **Then**
   its canonical silhouette and semantic color remain recognizable and stage color
   appears only in surfaces, secondary particles, decals, and afterglow.

---

### User Story 3 - Reproducible and Regression-Safe Delivery (Priority: P3)

As a maintainer, I can reproduce every accepted generated texture, verify its Unity
import/runtime binding, and prove the remaster did not modify simulation, #17c
resources, dirty user work, WebGL size, browser stability, or performance.

**Why this priority**: Generated art without provenance or regression evidence is
not maintainable and can silently break WebGL presentation.

**Independent Test**: Follow the quickstart from dry-run through generation hashes,
Unity tests/build, and browser smoke; compare protected paths and performance data.

**Acceptance Scenarios**:

1. **Given** a generated hazard texture, **When** its provenance record is opened,
   **Then** prompt, provider, inputs, output, post-process, SHA-256, import policy,
   consumer, and acceptance decision are present.
2. **Given** a fresh build, **When** regression and browser gates run, **Then** Sim
   digests and #17c tests pass, protected files are unchanged, build size is at most
   120 MB, browser errors are zero, and frame performance is not worse.

### Edge Cases

- A stage requests a texture role not used by any of its actual hazards.
- A stage-specific texture is missing or fails to load at runtime.
- A gti output is RGB although the role requires an alpha mask.
- A generated underlay contains transparent interior pixels or visible edge seams.
- A current or ash-wall band crosses the camera frustum or is viewed from its back.
- Multiple pylons, vents, or an altar share overlapping footprints.
- Reduced-motion mode disables animation but must preserve a static warning edge.
- Prologue, Arena, or Training instantiate shared View code after the campaign pass.
- The worktree changes externally while generation or implementation is running.

## Requirements

### Functional Requirements

- **FR-001**: The implementation MUST cover exactly the nine campaign stages in
  `StageCatalog`; Prologue, Arena, and Training MUST remain visually unchanged.
- **FR-002**: Each stage MUST follow the palette/material/tone contract frozen in
  Seed `seed_ad5567a58b85`.
- **FR-003**: Every actual vent, pillar, altar, current, pylon, ash wall, and stone
  wall presentation MUST resolve a stage-specific surface role where applicable.
- **FR-004**: Hazard interiors MUST use opaque or near-opaque physical underlays;
  transparency MAY be used only for feathered edges, trims, state glow, and VFX.
- **FR-005**: Existing Sim hazard geometry, events, radii, timing, damage, and frozen
  contracts MUST remain unchanged.
- **FR-006**: Existing #17c skill shapes, semantic colors, VFX state transitions,
  `Assets/Resources/Fx/` assets, and fallbacks MUST remain unchanged.
- **FR-007**: Stage-specific RGB underlay/body assets MUST be separate from shared
  geometry, grayscale trim masks, and fallbacks.
- **FR-008**: New textures MUST use the path
  `Assets/Resources/Textures/Hazards/<stageId>-<hazard>-<role>.png` and MUST be at
  most 1024 px.
- **FR-009**: A dedicated hazard texture importer MUST preserve the declared alpha,
  wrap, mipmap, compression, sRGB, and size policy per texture role.
- **FR-010**: Texture generation MUST use `gti`, run a dry-run and one real smoke
  generation before the batch, execute serially with backoff, and retain evidence.
- **FR-011**: Runtime lookup MUST fall back safely when a stage-specific asset is
  absent without creating white/magenta quads or modifying gameplay.
- **FR-012**: Dressing changes MUST affect only presentation density, contrast, and
  duplicate visual rings; they MUST NOT move Sim hazards.
- **FR-013**: Current dirty `GameDirector.cs` and unrelated user changes MUST be
  avoided or preserved by an additive, line-level merge with before/after evidence.
- **FR-014**: The final WebGL output MUST be at most 120 MB with zero browser
  console/page errors and no recorded frame-performance regression.
- **FR-015**: Generation, import, mapping, regression, visual, and performance
  evidence MUST be recorded before completion.

### Key Entities

- **StageVisualProfile**: Stage ID, act, concept, palette/material rules, environment
  elements, player secondary VFX tone, and enemy/hazard tone.
- **HazardSurfaceBinding**: Stage ID, hazard kind, role, resource path, mapping scale,
  opacity class, wrap mode, tint behavior, and fallback.
- **GeneratedTextureProvenance**: Tool/provider, prompt, reference inputs, attempts,
  output path, post-processing, hash, dimensions, mode, importer, consumer, and
  acceptance decision.
- **VisualEvidence**: Stage, viewport, capture moment, artifact path, leakage result,
  tone result, readability result, and reviewer decision.
- **RegressionEvidence**: Protected-path diff, Unity test/build output, browser logs,
  build size, and performance comparison.

## Success Criteria

### Measurable Outcomes

- **SC-001**: All nine stages have entry, normal-combat, boss/hazard, and close
  boundary evidence and are distinguishable according to the frozen tone matrix.
- **SC-002**: Across 1920x1080, 1280x720, and 375x667 review captures there are zero
  white gaps, unrelated floor leaks, stretched UVs, or visible repetition seams in
  covered gimmick areas.
- **SC-003**: Grayscale and live review find every hazard state, enemy attack, and
  player skill immediately distinguishable; no dressing occupies active decision
  space.
- **SC-004**: Every accepted texture is at most 1024 px and has complete gti and
  provenance evidence linked to its runtime consumer.
- **SC-005**: All #17c, VFX, environment, import, and golden-digest regression tests
  pass, with no diff under `Assets/Scripts/Sim` or `Assets/Resources/Fx`.
- **SC-006**: Fresh WebGL output is at most 120 MB, browser console/page errors are
  zero, and the agreed baseline frame metric does not regress.

## Assumptions

- Existing stage floor/stone images and stage-entry frames are authoritative visual
  references, not replacement targets.
- Shared meshes and pooled runtime geometry remain; differentiation comes from
  surface textures, material hierarchy, restrained secondary VFX, and dressing.
- The current branch remains `akillness/main`; Spec Kit uses a feature directory
  rather than creating or switching branches in the dirty shared worktree.
- gti provider health is established by the required dry-run and smoke generation;
  the undocumented backend is not introduced as a runtime dependency.
