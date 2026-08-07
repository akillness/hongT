# Weapon Reskin Plan — dagger / bow / hammer (skin-only)

2026-08-06 · Lane C · skin-only reskin (balance later, per locked decision #4).
Sim/balance UNCHANGED — this touches only prop meshes, materials, icons, and the
`AttachEquipProps` prefab-load convention.

## Tool status [OBSERVED]

- `gti` (god-tibo-imagen): present at
  `/Users/jangyoung/.nvm/versions/node/v22.19.0/bin/gti`; `--dry-run` prints a
  valid request shape.
- Default provider returns **HTTP 429** (private Codex backend rate limit) on
  every call, even after a 45 s backoff. **WORKAROUND FOUND**: `--provider
  codex-cli` succeeds — all 3 concept PNGs generated 2026-08-06 ~23:45–23:49 KST
  (`_workspace/current/design/assets/weapon-concepts/weapon-{dagger,bow,hammer}-concept.png`,
  valid PNG 1024², recorded in `docs/provenance/weapon-reskin.json`).
- `ppgen` (perfectpixel): **ABSENT** from PATH (`command -v ppgen` → empty). The
  2D-sprite/icon step therefore has no dedicated tool; icons can be cut from the
  concept art directly if/when the archetypes are wired.
- **STILL BLOCKED (mesh)**: no image→3D tool present (meshy/tripo/trellis absent)
  and Abyssal-Surge ships no dagger/bow/hammer source mesh, so archetype 3D props
  cannot be produced this session. `convert_equip_props.py` only re-binds the two
  retained blade/relic GLBs. Concept art is the deliverable this pass.

## Current weapon presentation [OBSERVED]

The weapon is a single equip-prop attached to the humanoid right hand; there is
no per-archetype weapon today — only two tier bands (basic / fine).

- Meshes: `Assets/Art/Props/equip-weapon-basic.fbx`,
  `Assets/Art/Props/equip-weapon-fine.fbx`
- Materials: `Assets/Resources/Props/equip-weapon-basic.mat`,
  `Assets/Resources/Props/equip-weapon-fine.mat`
  (fine: `_BaseColor {0.42,0.4,0.46}`, `_EmissionColor {1.52,0.56,0.272, a1.6}`)
- Prefabs: `Assets/Resources/Props/equip-weapon-{basic,fine}.prefab`
- Icon: `Assets/Resources/Icons/equip-weapon.png`,
  raw master `_workspace/current/engineering/icons/raw/equip-weapon.png`
- Load convention (`ActorView.AttachEquipProps`, L326-349):
  `Resources.Load<GameObject>($"Props/equip-{slot}-{band==2?"fine":"basic"}")`
  where `slot∈{weapon,lantern,cloak}`, band from tier. **A missing prefab is a
  no-op (tint floor), so adding archetype prefabs is non-breaking.**
- Socket pose (`ApplyPropPose` slot 0): grip at origin, blade along +Y, palm
  offset `(0.03,0.04,0)`, rotation `(0,0,-90)`. New meshes must be normalized by
  `tools/blender/convert_equip_props.py` to the same grip-at-origin / blade-+Y
  convention or the pose breaks.

## Reference source [OBSERVED]

`~/orca/Abyssal-Surge/assets/mesh/` contains **no** weapon/blade/dagger/bow/
hammer/sword/axe mesh (searched — 0 hits). Available meshes are characters and
two generic props (`prop-sprite-sheet-single-object.03/.05`). So a weapon reskin
cannot re-bind an existing source weapon mesh; each archetype must be generated
(concept → mesh) or modeled, then normalized through `convert_equip_props.py`.

## Archetype spec (skin-only)

| Archetype | Concept prompt (for `gti`) | Ember accent |
|---|---|---|
| Dagger | dark fantasy obsidian curved ritual dagger, glowing ember-orange edge, bone hilt, stylized low-poly game weapon prop, plain dark bg, single object centered | edge emission `{1.52,0.56,0.27}` |
| Bow | dark fantasy recurve ash-wood bow, glowing ember-orange string, dark metallic limbs, stylized low-poly game weapon prop, plain dark bg, single object centered | string emission |
| Hammer | dark fantasy heavy basalt warhammer, molten lava core cracking through the stone head, iron shaft, stylized low-poly game weapon prop, plain dark bg, single object centered | core emission, higher intensity |

## Output path contract (when tools are available)

- Concept: `_workspace/current/design/assets/weapon-concepts/weapon-<type>-concept.png`
  (`gti --prompt "<above>" --output <path> --size 1024x1024`)
- Icon: `ppgen -provider god-tibo-imagen -desc "equip-weapon-<type>|..." -json`
  → `Assets/Resources/Icons/equip-weapon-<type>.png` (raw master mirrored under
  `_workspace/current/engineering/icons/raw/`)
- Mesh: `blender -b -P tools/blender/convert_equip_props.py -- --blade <glb>
  --outdir Assets/Art/Props` → `equip-weapon-<type>-{basic,fine}.fbx`
- Material: `Assets/Resources/Props/equip-weapon-<type>-{basic,fine}.mat`
  (clone the existing fine mat's ember emission)
- Prefab: `Assets/Resources/Props/equip-weapon-<type>-{basic,fine}.prefab`
- Provenance: append each generation to `docs/provenance/weapon-reskin.json`
  (mirror `docs/provenance/lantern-reaver-reskin.json` shape: input, output,
  meshSource, prompt, tool).

## Wiring note (deferred — needs a decision)

The current `AttachEquipProps` loads only `equip-weapon-{basic,fine}`; it has no
archetype dimension. Making the 3 archetypes reachable at runtime requires a
selection source (which archetype the player wields). That is a **behavior/UI
decision**, not part of the skin-only asset pass, so it is intentionally left
out here — the asset files can land first (non-breaking, unreferenced) and the
selection wiring is a separate follow-up once the archetype source is decided.

## Blockers to clear before execution

1. `gti` HTTP 429 quota — retry when reset (concept art gate).
2. `ppgen` not installed — install perfectpixel or substitute an icon path
   through `gti` directly (icon gate).
3. Archetype selection source undecided — gates runtime wiring only, not asset
   creation.
