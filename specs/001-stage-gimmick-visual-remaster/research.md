# Research: Nine-Stage Gimmick Visual Remaster

## Decision 1 — Inject stage context through `GameView`

**Decision**: `GameView.Begin` assigns `_logicalStageId` and immediately calls
`VfxDirector.SetStageContext`; `EndRun` and `ClearTransient` clear the context.

**Rationale**: `GameView` already owns the logical stage lifecycle and calls
`SyncHazards`. This avoids the later act-cinematic work in `GameDirector`, preserves
`BuildHazardView(in HazardState)`, and keeps Sim/events untouched.

**Rejected**: Add stage ID to `HazardState` or `SyncHazards` (changes contracts or
propagates presentation data through Sim); patch `GameDirector.SetStageEnvironment`
(unnecessary shared-file risk).

## Decision 2 — Separate catalog from cached resource resolution

**Decision**: `StageHazardVisualCatalog` owns immutable profile/binding data;
`StageHazardTextureResolver` validates stage scope, builds resource paths, caches
loads, and returns an explicit missing result.

**Rationale**: The catalog is pure presentation data and easy to exhaustively test.
The resolver contains Unity IO and cache lifetime. `VfxDirector` remains the
composition owner.

**Rejected**: Hard-code every path inside the `switch` in `BuildHazardView`; it is
harder to audit, test, and prove complete.

## Decision 3 — Use stage-specific physical primaries, shared state masks

**Decision**: Every actual stage/hazard pair has a stage-specific opaque primary
surface. Existing #17c sheets and runtime geometry remain the state layer; shared
grayscale trims may be derived when they do not carry stage color.

**Rationale**: One generic atlas would not satisfy nine-stage identity; fully unique
state animation sheets would waste WebGL budget and risk changing semantic reads.

**Rejected**: Tint the current transparent primitives (does not stop base-floor
leakage); replace all #17c masks (breaks the frozen visual language); generate every
minor trim uniquely (excessive generation/material budget with little readability
gain).

## Decision 4 — Treat band hazards as VFX-only surfaces

**Decision**: `TideCurrent` and `AshWall` receive opaque VFX beds/bands exactly on
their Sim footprints and retain bright animated edges. They receive no new
`EnvironmentBuilder` furniture.

**Rationale**: Existing layout code intentionally omits furniture because the bands
consume lanes/safe corridors. The bug is transparency and one-sided coverage, not
missing props.

**Rejected**: Add rails/debris around the bands; this would clutter the only escape
space and could misrepresent the occupied region.

## Decision 5 — Dedicated hazard importer

**Decision**: Add `HazardTextureImportPipeline`; do not extend the Env importer.

**Rationale**: Env images are opaque Repeat textures and their importer forces no
alpha. Hazard roles require both opaque Clamp surfaces and alpha-preserving trims,
plus Repeat for bodies and declared band roles.

**Rejected**: Put hazards below `Textures/Env`; alpha masks would be destroyed and
the existing 18 protected images would enter the change surface.

## Decision 6 — Installed gti contract and failure policy

**Observed 2026-08-11**: `gti` is installed, Node is v22.19.0, Codex auth is present,
and the installed CLI accepts repeatable `--image <path>`, `--dry-run`, `--debug`,
`--size`, and providers `private-codex|codex-cli|auto`.

**Decision**: Probe `private-codex` first because repository evidence says the
`codex-cli` provider has historically rejected image inputs and size selection.
Dry-run and smoke use the same three references and sanitized debug output. The
batch does not silently drop references or switch image tools.

**Failure policy**: Retry transient 429/5xx errors serially at 15/30/60/120/240
seconds. If the installed provider rejects reference images, authentication is
invalid, or one smoke cannot be generated, stop only the generation-dependent lane,
record the blocker, and continue non-generation tests/code that remain valid. Do not
adopt a different generator.

## Decision 7 — Asset resolution and budget

**Decision**: Default accepted textures to 512px. Permit 1024px for `current-bed`,
`ashwall-band`, and long `stonewall-body` only after visual inspection demonstrates
512px stretch. Cache each resource once and reuse the existing small set of materials.

**Rationale**: The project has a 1024 ceiling and 120 MB WebGL budget. The camera
pitch and dark physical surfaces do not justify 1024 for every disc.

**Rejected**: 1024 for all outputs (unnecessary size); one material per hazard
instance (material budget and lifecycle risk).

## Decision 8 — Mapping dynamic bands

**Decision**: Current uses a wide physical texture and leaves direction to existing
chevrons. AshWall reveals a maximum-span texture at fixed texel density using
`_BaseMap_ST` scale/offset; the right-origin wall mirrors the offset. StoneWall body
uses Repeat with world-length tiling.

**Rationale**: Stretching a square image across current, growing AshWall, or arbitrary
wall length creates the UV defects explicitly forbidden by the Seed.

## Decision 9 — Verification strategy

**Decision**: Automated gates cover the source-derived binding inventory, importer
flags, opaque interior pixels, resource loading/fallback, no per-frame construction,
dressing caps, existing regression suites, protected diffs, and build size. Browser
and vision review cover tone, leak/seam/UV, grayscale contrast, and clutter at three
viewports.

**Rationale**: Pixel opacity alone cannot prove on-camera coverage; screenshots alone
cannot prove every manifest and importer setting. Both are required.

The browser harness is a committed feature tool,
`tools/qa/capture_stage_hazard_matrix.mjs`, not an unnamed scratch script. It must
derive its input sequence from the proven three-act driver, assert both
`GameFlowAgentAPI` active-wave state and the HUD red-health-bar crop (`R-G > 20` at
x=100..420, y=28..52), and fail lobby captures. Its performance phase records 600
post-warmup `requestAnimationFrame` intervals for `echo-throne` at 1280x720 under
the same Chromium/SwiftShader flags used for baseline and final comparison.
