# Visual Acceptance Matrix

| Act | Stage | Frozen physical tone | Required primary roles |
|---|---|---|---|
| I | `cinder-span` | charcoal basalt, scorched seams, hot orange embers | vent-underlay, stone-wall-body |
| I | `ember-gallery` | fire-blackened gallery, obsidian, circular vent rhythm | vent-underlay, obsidian-pillar-body, stone-wall-body |
| I | `abyss-chancel` | violet/indigo oath cathedral, pale runes, cold veil | vent-underlay, obsidian-pillar-body, stone-wall-body |
| II | `witness-well` | wet jade well, mineral rings, restrained teal | vent-underlay, obsidian-pillar-body, relic-altar-underlay, stone-wall-body |
| II | `echo-throne` | dark blue granite, silver veins, concentric echo/current | vent-underlay, relic-altar-underlay, tide-current-bed, stone-wall-body |
| II | `ash-verdict` | ash sandstone court, smoke, judgment gold | vent-underlay, relic-altar-underlay, ember-pylon-underlay, stone-wall-body |
| III | `cinder-sluice` | wet iron sluice, grate, rust, blue current | vent-underlay, obsidian-pillar-body, tide-current-bed, stone-wall-body |
| III | `ember-bastion` | iron/ember fortress, warm fire versus cyan Ward | vent-underlay, obsidian-pillar-body, ember-pylon-underlay, stone-wall-body |
| III | `ash-march` | desaturated ash execution road, pale final gold | vent-underlay, relic-altar-underlay, ember-pylon-underlay, ash-wall-band, stone-wall-body |

## Capture gate

Each stage requires entry, normal-combat, active hazard/boss, and close-boundary
evidence. The full set is reviewed at 1920x1080, 1280x720, and 375x667. At minimum,
`ember-gallery`, `echo-throne`, and `ash-march` must show their active signature
hazards at every viewport.

## Pass criteria

- No unrelated floor/background pixels inside the claimed physical hazard interior.
- No white/magenta surfaces, holes, backface disappearance, UV stretch, or visible
  repetition seam during camera movement/rotation/zoom.
- Warning/state edge remains distinguishable from the bed in grayscale.
- Player skill, enemy telegraph, and hazard each retain a distinct silhouette and
  semantic primary color when overlapping.
- Furniture does not enter the active decision footprint; no StoneWall duplicate
  furniture ring; visible active telegraph emphasis remains bounded.
- All nine stages are distinguishable without HUD text from material/value/accent.
- Browser console and page errors are zero.
