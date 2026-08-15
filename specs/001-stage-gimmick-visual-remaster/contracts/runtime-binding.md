# Runtime Binding Contract

## Stage context

- `GameView.Begin` sets `VfxDirector.SetStageContext(_logicalStageId)` only after
  `_logicalStageId` is assigned.
- Empty, unknown, Prologue, Arena, and Training context resolves no stage-specific
  binding and preserves the existing visual fallback.
- `GameView.EndRun` and `VfxDirector.ClearTransient` clear context and cached
  run-scoped views. A subsequent run cannot reuse the previous stage profile.
- `GameDirector`, `StageCatalog`, Sim state, events, geometry, timing, and damage are
  outside this contract and unchanged.

## Naming and resolution

Physical resource files use:

```text
Assets/Resources/Textures/Hazards/<stageId>-<hazard-token>-<role>.png
```

Runtime extensionless paths use:

```text
Textures/Hazards/<stageId>-<hazard-token>-<role>
```

Tokens are stable lower-case kebab-case: `ember-vent`, `obsidian-pillar`,
`relic-altar`, `tide-current`, `ember-pylon`, `ash-wall`, `stone-wall`.

## Composition order

1. Stage environment floor.
2. Stage-specific opaque/near-opaque physical primary surface.
3. Existing #17c fill, mask, warning edge, or HP/state band.
4. Interactable/body core.
5. Actors/projectiles and transient VFX.

Primary interiors target alpha 1.0 and may feather only their outer 6-10 pixels.
State edges remain thin and brighter than beds. Skill primary colors/silhouettes are
never stage-tinted.

## Hazard mapping

- `EmberVent`: `underlay`; fit at 1.08x radius; existing fill and warning ring above.
- `ObsidianPillar`: `body` plus restrained contact surface; exact radius from state.
- `RelicAltar`: `underlay` channel/sigil; existing channel state above.
- `TideCurrent`: `bed`; exact `HalfW*2` x `HalfH*2`; existing edge/chevrons above.
- `EmberPylon`: `underlay` aura and body mapping; existing HP band above.
- `AshWall`: `band`; exact home-edge-to-front region; cull-safe; existing front edge
  above; fixed-density UV crop/reveal rather than full-width stretch.
- `StoneWall`: `body` and contact footprint; exact segment half-vector/radius; Repeat
  mapping along world length.

## Cache and lifecycle

- Resource lookup occurs when context changes or a hazard view is constructed, never
  in the per-frame hazard update loop.
- A resource path is loaded at most once per resolver lifetime, including misses.
- Materials are reused per stage/kind/role where state mutation permits; stateful
  materials remain per existing hazard view and are destroyed by existing cleanup.
- Context change destroys/rebuilds stale hazard views before a new binding is used.

## Fallback

Missing resources return an explicit miss. `VfxDirector` keeps the prior primitive,
color, and #17c mask; it does not assign null to an existing valid texture and does
not use white/magenta debug materials. Required-primary misses fail EditMode and
manifest validation even though runtime remains safe.
