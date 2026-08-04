# Three.js Runtime Animation Asset Contract

**Status:** CURRENT NORMATIVE CONTRACT
**Date:** 2026-07-29  
**Repository:** Abyssal-Lantern
**Runtime File:** `battle-realtime-three.js`

---
## 0. 2026-07-29 NATURAL JOINT-MOTION AMENDMENT

This section is normative and supersedes any conflicting inventory count, line
number, or “locked” statement in the observational appendix below. The runtime
library now owns 11 promoted source characters, 121 embedded action clips
(110 retargeted plus 11 authored fallbacks), and the registry at
`assets/motion/ingame/characters/registry.json` is the machine-readable source
of truth.

### Source and mesh boundary

- `assets/mesh/character/` and `assets/mesh/enemy/` are authoring inputs, not
  directly playable actors.
- A character source cannot be one fused OBJ whose unrelated costume, body, and
  accessory islands are treated as one rigid part. The Blender authoring pass
  partitions visible faces into semantic body regions, binds those regions to
  the adjacent parent/child joint chain, and exports a skinned GLB.
- Stage architecture and props use separately addressable mesh nodes and
  placements. A merged terrain OBJ is never the default runtime draw path.
- Simulation translation owns actor movement. Every promoted locomotion clip is
  in-place (`inPlaceRootMotion: true`); animation may articulate joints but may
  not displace the gameplay root.

### Skinning and joint articulation

1. Every rendered character mesh must have an enabled armature modifier and a
   `def-humanoid-v1` skeleton with the expected DEF joint chain.
2. Each vertex weight sum must be finite and normalized. A vertex may use at
   most four influences; influences must stay on one adjacent anatomical chain.
   Orphan vertices, disconnected chain influence, and whole-body single-bone
   collapse are release blockers.
3. Elbow, knee, shoulder, hip, wrist, and ankle test poses must deform the
   adjacent semantic regions without leaving detached islands, collapsing the
   limb cross-section, or moving unrelated torso/head geometry.
4. Every action track uses finite, normalized local quaternions with `LINEAR`
   glTF interpolation. Quaternion signs must be continuity-normalized so adjacent
   keys take the shortest rotation path.
5. Source motion is sampled at 24 fps and baked into the exported action. Runtime
   playback must not synthesize key poses from a merged mesh or replace
   joint motion with whole-object rotation.

### Action and transition ownership

- The canonical library is exactly `idle`, `move`, `run`, `hit`, `bighit`,
  `attack`, `critical`, `avoid`, `defence`, `die`, and `show`, named
  `<assetId>::<action>::v01`. The commander may additionally author
  `attack_melee` and `attack_ranged`.
- `idle`, `move`, and `run` loop. All other actions are mixer-finished one-shots.
- Locomotion changes and one-shot entry/recovery use `AnimationAction` fades;
  they never hard-snap between ordinary poses. A live one-shot queues an
  incompatible beat by presentation priority. `die` alone is a terminal hard
  cut.
- The mixer advances once per presented simulation tick at 60 Hz. Repeated or
  regressing snapshots consume no animation time; a new stage tick zero resets
  the animation clock.
- Reduced-motion preserves pose meaning and facing direction while removing
  decorative easing. It does not disable necessary joint articulation.

### Release evidence

The release gate is not a mesh-count check. It requires all of the following:

1. Blender rig report: expected armature, enabled modifiers, semantic region
   coverage, normalized bounded weights, and joint deformation samples.
2. Registry/manifests: current model SHA-256, 11 unique action names, motion
   lineage, rights receipt, and `runtimeEligible: true`.
3. Runtime tests: promoted model loading, namespaced action routing, fixed-tick
   mixer advancement, action queuing, crossfade recovery, and browser playback.
4. Visual review at rest and extreme joint poses for every promoted actor.

---


## 1. ASSET LOADER & FORMAT CONTRACT

### Loader: GLTFLoader (Three.js)
- **Import:** `battle-realtime-three.js:9` → `import { GLTFLoader } from "./vendor/loaders/GLTFLoader.js"`
- **Instance:** `battle-realtime-three.js:714` → `const gltfLoader = new GLTFLoader()`
- **Cache:** `gltfCache` (Map, module-level, shared across all RealtimeBattle instances)
- **Caching Strategy:** Promise-based; same URL fetched once, result reused
  - `loadGltf(path)` → `battle-realtime-three.js:736-749`
  - Cache miss → `gltfLoader.load(url, resolve, undefined, reject)`
  - Cache hit → returns cached promise

### File Format
- **Supported:** GLB (binary glTF 2.0)
- **Alternative Supported:** Embedded GLTF `.glb` containing geometry + animations
- **Not Supported:** FBX, DAE, or external `.gltf` text format

### URL Resolution: `modelUrl(path)` (Line 729)
```
1. If path is absolute or has ./ / ../ prefix → use as-is
2. If path starts with assets/ → prepend ./
3. Otherwise → prepend MODEL_ROOT (./assets/images/battle/glb/)
```

**Result:** All relative paths resolve to `./assets/images/battle/glb/` unless absolute

---

## 2. CHARACTER IDENTIFIER & MODEL PATH CONTRACT

### Model Root Directory
```
const MODEL_ROOT = "./assets/images/battle/glb/";  // battle-realtime-three.js:121
```

### Character Identifiers by Type

#### BOSS ACTORS (10 models)
**Source:** `battle-realtime-three.js:127-138` → `BOSS_MODELS` object  
**Lookup:** `entity.bossId` (string) → model path (relative to MODEL_ROOT)

| bossId | Model Path | File |
|--------|-----------|------|
| s1-cinder-warden | bosses/cinder-warden.glb | `./assets/images/battle/glb/bosses/cinder-warden.glb` |
| s2-veil-tactician | bosses/veil-tactician.glb | `./assets/images/battle/glb/bosses/veil-tactician.glb` |
| s3-gate-sovereign | bosses/gate-sovereign.glb | `./assets/images/battle/glb/bosses/gate-sovereign.glb` |
| s4-tide-warden | bosses/tide-warden.glb | `./assets/images/battle/glb/bosses/tide-warden.glb` |
| s5-pack-herald | bosses/pack-herald.glb | `./assets/images/battle/glb/bosses/pack-herald.glb` |
| s6-requiem-choir | bosses/requiem-choir.glb | `./assets/images/battle/glb/bosses/requiem-choir.glb` |
| s7-lantern-tyrant | bosses/lantern-tyrant.glb | `./assets/images/battle/glb/bosses/lantern-tyrant.glb` |
| s8-bridge-colossus | bosses/bridge-colossus.glb | `./assets/images/battle/glb/bosses/bridge-colossus.glb` |
| s9-veiled-concordat | bosses/veiled-concordat.glb | `./assets/images/battle/glb/bosses/veiled-concordat.glb` |
| s10-abyss-regent | bosses/abyss-regent.glb | `./assets/images/battle/glb/bosses/abyss-regent.glb` |

#### ENEMY ARCHETYPES (4 models → 4 entity kinds)
**Source:** `battle-realtime-three.js:144-149` → `ENEMY_MODELS` object  
**Lookup:** `entity.kind` (string) → model path

| kind | Model Path | File |
|------|-----------|------|
| rusher | enemies/scout.glb | `./assets/images/battle/glb/enemies/scout.glb` |
| flanker | enemies/shade.glb | `./assets/images/battle/glb/enemies/shade.glb` |
| guardian | enemies/guard.glb | `./assets/images/battle/glb/enemies/guard.glb` |
| ranged | enemies/possessed.glb | `./assets/images/battle/glb/enemies/possessed.glb` |

#### COMPANION ACTORS (9 models)
**Source:** `battle-realtime-three.js:152-162` → `COMPANION_MODELS` object  
**Lookup:** `entity.companionId` (string) → model path

| companionId | Model Path | File |
|-------------|-----------|------|
| ember-cohort | companions/ember-cohort.glb | `./assets/images/battle/glb/companions/ember-cohort.glb` |
| rift-lens | companions/rift-lens.glb | `./assets/images/battle/glb/companions/rift-lens.glb` |
| veil-vanguard | companions/veil-vanguard.glb | `./assets/images/battle/glb/companions/veil-vanguard.glb` |
| anchor-shard | companions/anchor-shard.glb | `./assets/images/battle/glb/companions/anchor-shard.glb` |
| throne-echo | companions/throne-echo.glb | `./assets/images/battle/glb/companions/throne-echo.glb` |
| dawnless-crown | companions/dawnless-crown.glb | `./assets/images/battle/glb/companions/dawnless-crown.glb` |
| pack-warden | companions/pack-warden.glb | `./assets/images/battle/glb/companions/pack-warden.glb` |
| lantern-reaver | companions/lantern-reaver.glb | `./assets/images/battle/glb/companions/lantern-reaver.glb` |
| requiem-warden | companions/requiem-warden.glb | `./assets/images/battle/glb/companions/requiem-warden.glb` |

#### COMMANDER (1 model)
**Source:** `battle-realtime-three.js:164, 205`
```
const COMMANDER_MODEL = "commander/dusk-warden.glb";
export const COMMANDER_MESH_ROOT = COMMANDER_MODEL;
```

**File:** `./assets/images/battle/glb/commander/dusk-warden.glb`

#### VFX/PROPS & TERRAIN
- **VFX:** `battle-realtime-three.js:212-219` → `VFX_MODELS` (9 one-shot effect GLBs + lifetimes in ticks)
- **Props:** `battle-realtime-three.js:230-236` → `PROP_MODELS` (5 authored modifier props for reward cards)
- **Equipment Tiers:** `battle-realtime-three.js:246-252` → `EQUIPMENT_TIER_MODELS` (T1-T5 gear portraits)
- **Terrain:** Stage-specific terrain GLBs (11 stages, via `stageWorldFor()`)

**Total Character Models:** 23 (10 bosses + 4 enemies + 9 companions + 1 commander)

---

## 3. ANIMATION CLIP NAMING & STRUCTURE CONTRACT

### Clip Naming Convention

**Format:** `<assetId>::<actionKey>::v01`

**Examples from manifest:**
```
unarmed-core::idle::v01
unarmed-core::move::v01
unarmed-core::run::v01
unarmed-core::hit::v01
unarmed-core::attack::v01
```

**Parser:** `actionKeyFromClipName(name)` → `battle-realtime-three.js:920-924`
- Splits name on `::`
- Takes second part (index 1) if available, else first part
- Validates against `RIG_ACTION_KEYS` allowlist
- Returns key or null if not recognized

### Animation Action Keys (11-13 per character)

**Core Library:** `battle-realtime-three.js:267-270` → `RIG_ACTION_KEYS`
```javascript
[
  "idle", "move", "run",           // Locomotion (looping)
  "hit", "bighit", "attack",       // Combat (one-shot)
  "critical", "avoid", "defence",  // Reactions (one-shot)
  "die", "show",                   // Exit states (one-shot)
  "attack_melee", "attack_ranged"  // Commander-only (one-shot)
]
```

**Locomotion (looping):** `battle-realtime-three.js:271`
```
["idle", "move", "run"]
```
- Set to `THREE.LoopRepeat` with `Infinity` repetitions
- Never hold final pose

**Combat (one-shot):** All others
- Set to `THREE.LoopOnce` with 1 repetition
- `clampWhenFinished = true` (holds last frame)

**Commander-specific:** `attack_melee`, `attack_ranged`
- Presence optional; fallback to `attack` / `critical` if missing

### Animation Clip Requirements

Per `battle-realtime-three.js:262-266`:
- Rigged character GLBs MUST embed 11-clip action library named `<assetId>::<action>::v01`
- VFX and terrain GLBs carry NO actions (unrigged)
- Non-pipeline GLBs tolerate bare clip names (`idle`, `attack` without `::` delimiter)

**Source:** `assets/motion/ingame/manifest.json`
- **Schema Version:** 1
- **Generator:** `scripts/retarget-ingame-motion-blender.py`
- **Overlay Animation Pack:** `assets/motion/ingame/unarmed-core.glb` (9 delta clips, quaternion-only)

---

## 4. SKELETON & RIG COMPATIBILITY CONTRACT

### Target Skeleton (Runtime)

**Name:** `def-humanoid-v1`  
**Total Bones:** 24 DEF-* bones (Blender Rigify convention)  
**Base Chain:** Spine (spine → spine.001 → spine.002 → spine.003 → spine.004 → spine.005)

**All Target Bone Names** (from manifest):
```
DEF-spine, DEF-spine.001, DEF-spine.002, DEF-spine.003, DEF-spine.004, DEF-spine.005,
DEF-shoulder.L, DEF-upper_arm.L, DEF-forearm.L, DEF-hand.L,
DEF-shoulder.R, DEF-upper_arm.R, DEF-forearm.R, DEF-hand.R,
DEF-thigh.L, DEF-shin.L, DEF-foot.L, DEF-toe.L,
DEF-thigh.R, DEF-shin.R, DEF-foot.R, DEF-toe.R
```

### Source Skeleton (Motion Bench)

**Name:** `mixamo-37`  
**Total Bones:** 37 mixamorig:* bones  
**Mapped to Target:** 22 bones (feet, hands, some fingers excluded)

**Unmapped Source Bones** (15 bones not retargeted):
```
mixamorig:HeadTop_End, mixamorig:LeftToe_End, mixamorig:RightToe_End,
mixamorig:RightHandIndex1/2/3/4, mixamorig:RightHandPinky1/2/3/4,
mixamorig:RightHandThumb1/2/3/4
```

**Unmapped Target Bones** (2 bones not sourced):
```
DEF-pelvis.L, DEF-pelvis.R
```

### Bone Mapping Details

**Source → Target Mapping** (22 bones):
Per `assets/motion/ingame/manifest.json`'s `boneMapping` field  
Used by `retarget-ingame-motion-blender.py` to generate quaternion deltas

**Root Motion Handling:**
- Source hips travel recorded: `sourceRootTravel: {x, y, z}`
- Exported root deviation: `exportedRootDeviation: {x, z}` (Y-up convention)
- Example (Walking.fbx): `sourceRootTravel: {x: -0.000002, y: 0.000015, z: 145.083625}`

### Loop vs. One-Shot Expectations

Per `buildActions()` → `battle-realtime-three.js:931-950`:

**Looping (locomotion):**
- `setLoop(THREE.LoopRepeat, Infinity)`
- Never holds final pose

**One-shot (combat/reactions):**
- `setLoop(THREE.LoopOnce, 1)`
- `clampWhenFinished = true`
- Holds last pose until next action

---

## 5. OVERLAY ANIMATION SYSTEM (Retargeting Layer)

### Purpose
**Retarget one unarmed motion pack onto all 23 character rigs dynamically**

### Overlay Pack Source
**File:** `assets/motion/ingame/unarmed-core.glb` (189 KB, animation-only)
**Clips:** 9 quaternion-delta clips, no skeleton/skin
**Bones:** 24 DEF-* bone references (rig compatibility), 22 animated via MAPPING_ROWS
**Design Doc:** `_workspace/current/overlay-architecture.md`
```
idle (loop), move (loop), run (loop), hit, bighit, attack, critical, avoid, defence
```

### Delta Encoding Format
The overlay GLB stores **rest-relative quaternion deltas**, not absolute poses:

```
delta[X][t] = inverse(target_rig_rest[X]) * absolute_retargeted[X][t]
```

Where:
- `target_rig` = `commander/dusk-warden.glb` (reference rig used during offline retargeting)
- `delta[X][t]` = quaternion for bone X at frame t in the overlay GLB
- `absolute_retargeted[X][t]` = the retargeted bone-local absolute rotation

The postprocess pipeline (`postprocess_rest_relative_deltas()` in the retarget script) converts absolute quaternions to this rest-relative delta form before export.

### Runtime Adaptation Math

For a character C with rest pose quaternion `C_rest[X]` (from GLB `nodes[].rotation`):

```
adapted_clip[X][t] = C_rest[X] * delta[X][t]
```

This works because:
1. All 24 compatible characters use the same `def-humanoid-v1` skeleton hierarchy
2. `C_rest[X]` is the bone's rest orientation relative to its parent in character C's GLB
3. The overlay delta encodes retargeted motion relative to the reference rig's rest
4. Pre-multiplying by `C_rest[X]` shifts the motion into character C's bone-local space

### Rest Pose Extraction

Read from each character GLB's `nodes[]` array (`battle-realtime-three.js:775-790`):
```javascript
function restQuatsFromGLB(gltf) {
  const rest = {};
  for (const node of gltf.scene?.users?.gltfJson?.nodes ?? []) {
    if (node.name?.startsWith("DEF-")) {
      const [x, y, z, w] = node.rotation ?? [0, 0, 0, 1];
      rest[node.name] = normalizeQuat([x, y, z, w]);
    }
  }
  return rest;
}
```

### Caching Strategy

```javascript
adaptedOverlayEntriesByModel: Map<string, AdaptedEntry[]>
  key:   modelPath (e.g. "enemies/scout.glb")
  value: [{ clip: THREE.AnimationClip, source: "overlay" }, ...]  (9 entries)
```

- Adapted entries computed once per unique model path
- Cache survives for session lifetime
- Max 24 unique models — no eviction needed

### Loading Functions

**Constants** (`battle-realtime-three.js:717-718`):
```javascript
const OVERLAY_ANIMATION_PATH = "assets/motion/ingame/unarmed-core.glb";
const OVERLAY_ACTION_KEYS = ["idle","move","run","hit","bighit","attack","critical","avoid","defence"];
```

**Loader** (`battle-realtime-three.js:774-800`):
- `loadOverlayDeltaEntries()` — loads pack on first request, caches promise
- Extracts 9 clips, normalizes quaternion tracks

**Normalization** (`battle-realtime-three.js:760-772`):
- `normalizeOverlayDeltaClip(clip)` — ensures shortest-path quaternion continuity
- Detects & corrects flipped (>180°) quaternion sign jumps

**Adapter** (`battle-realtime-three.js:802-843`):
- `adaptOverlayEntries(modelPath, instance, deltaEntries)` — composes deltas with model's rest pose
- Caches adapted entries per model path in `adaptedOverlayEntriesByModel` Map
- Returns array of `{clip, source: "overlay"}` entries

### Fallback Logic

**If overlay load fails:**
- Simulation continues using authored clips only
- `warnedOverlayLoadFailure` set once
- No retry; failure is permanent in session

**Fallback Actions** (no overlay needed):
```
"die", "show", "attack_melee", "attack_ranged"
```

### Bone Chain

**22 Mapped Bones** (same as manifest `MAPPING_ROWS`):
```
DEF-spine (.001-.005), DEF-shoulder.L/R, DEF-upper_arm.L/R,
DEF-forearm.L/R, DEF-hand.L/R, DEF-thigh.L/R, DEF-shin.L/R,
DEF-foot.L/R, DEF-toe.L/R
```

**Not animated by overlay:** DEF-pelvis.L, DEF-pelvis.R (runtime-synthesized)

**Error recovery** — 4 scenarios:
| Scenario | Behavior |
|----------|----------|
| Overlay GLB not found | `warn` once, fall back to base clips |
| Overlay GLB malformed | `warn` once, fall back to base clips |
| Character GLB has no DEF skeleton | No overlay applied (unrigged model) |
| Character GLB has extra non-DEF bones | Only DEF-* bones get overlay delta; others use base clip |

---

## 6. MODEL LOADING & INSTANTIATION CONTRACT

### Actor Types & Their Loaders

**Characters (rigged):**
- `instantiateActorModel(relPath, targetHeight)` → `battle-realtime-three.js:1050-1069`
- `instantiateStageNpc(npc)` → `battle-realtime-three.js:1130-1175` (ambient NPCs)

**Terrain (unrigged):**
- `instantiateTerrainModel(relPath)` → `battle-realtime-three.js:1071-1077`

**VFX (unrigged):**
- `instantiateVfxModel(relPath)` → `battle-realtime-three.js:1280-1286`

**Props (unrigged):**
- `instantiateStageProp(prop)` → `battle-realtime-three.js:1086-1111`

### Sizing Contract

**Target Heights** (world units, Y-axis extent after uniform scale):
Per `battle-realtime-three.js:56-63`
```
boss: WORLD_SCALE * 0.9,        // 12.6 units
commander: WORLD_SCALE * 0.8,   // 11.2 units
companion: WORLD_SCALE * 0.65,  // 9.1 units
enemy: WORLD_SCALE * 0.7,       // 9.8 units
pickup: WORLD_SCALE * 0.15,     // 2.1 units
projectile: WORLD_SCALE * 0.09  // 1.26 units
```

**Sizing Functions:**
- `fitHeight(object3d, targetHeight)` → `battle-realtime-three.js:860-873`
  - Uniform scale to match target Y-extent
- `fitFootprint(object3d, targetHalfExtent)` → `battle-realtime-three.js:875-881`
  - Uniform scale to match horizontal footprint (terrain only)

### Serialized Instantiation

**Reason:** Prevent main-thread starvation when multiple actors spawn same tick  
Per `battle-realtime-three.js:952-960`
```
instantiationQueue = Promise.resolve();  // Chain work
instantiationBusy = true/false;
serializeInstantiation(work) { … }
```

---

## 7. SKELETON CLONING & ANIMATION BUILDING

### Skeleton Cloning
**Utility:** `SkeletonUtils.clone()` (Three.js built-in)  
Per `battle-realtime-three.js:725-727`:
- Each rendered instance owns its skeleton
- `disposedSkeletons` WeakSet prevents double-disposal

### Animation Mixer & Actions

**Creation:** `instantiateActorModel()` creates mixer
```javascript
const mixer = new THREE.AnimationMixer(instance);
```

**Action Building:** `buildActions(mixer, clipEntries)` → `battle-realtime-three.js:931-950`
- Iterates gltf.animations array
- Extracts action key from clip name via `actionKeyFromClipName()`
- Creates THREE.AnimationAction for each valid clip
- Sets loop mode (repeat vs. once) based on action type
- Stores action source (base, overlay, or fallback)

**Return:** `{ actions: {}, actionSources: {} }` map

### Overlay Composition in instantiateActorModel()

**When overlay is available**, the loading flow merges base and adapted overlay clips:

```javascript
const [gltf, overlayEntries] = await Promise.all([
  loadGltf(modelUrl(relPath)),
  loadOverlayDeltaEntries().then((deltaEntries) =>
    deltaEntries ? adaptOverlayEntries(relPath, instance, deltaEntries) : []
  ),
]);

const allClips = overlayEntries.length
  ? [...gltf.animations, ...overlayEntries]
  : gltf.animations;

const mixer = new THREE.AnimationMixer(instance);
const { actions, actionSources } = buildActions(mixer, allClips);
```

**First-match wins:** `buildActions()` iterates the concatenated array in order.
When it encounters a duplicate action key (e.g. "idle" from both base and overlay),
it skips the second occurrence. Since overlay entries appear after base entries,
**overlay replaces base** on the 9 covered action keys.

**Fallback-only keys** (`die`, `show`, `attack_melee`, `attack_ranged`) have no overlay
entries, so base clips win by default.

**Cache:** `adaptedOverlayEntriesByModel` (Map, keyed by modelPath) prevents recomputation
on multi-instance spawns of the same character type.

---

## 8. ANIMATION PLAYBACK CONTRACTS

### Loop Behavior
```javascript
// Locomotion (idle, move, run)
action.setLoop(THREE.LoopRepeat, Infinity);

// Combat (attack, hit, critical, etc.)
action.setLoop(THREE.LoopOnce, 1);
action.clampWhenFinished = true;  // Hold last frame
```

### Animation Mixer Update
- Called at 60 Hz (`SIM_TICK_RATE` = 60, line 15)
- Max delta clamped: 6 ticks (100ms) → `MAX_ANIMATION_TICK_DELTA = 6` (line 16)
- Never skips frames; always updates incrementally

### Playback Integration
**AnimationMixer instance:** Owned by each actor record in the scene  
**Clip actions:** Mapped by key (idle/attack/etc.)  
**Playback trigger:** From snapshot events (defense-run-simulation.js layer)

### Mesh-Size-Aware Motion Profile (2026-07-30 amendment)

Per-character differentiation is a **function of the fitted mesh size**, never a
per-kind constant. `motionProfileFor(targetHeight)` derives the profile from the
same height `fitHeight()` scales the GLB to, relative to
`MOTION_PROFILE_REFERENCE_HEIGHT` (the standard enemy silhouette, 1.7u):

```javascript
heightRatio      = targetHeight / MOTION_PROFILE_REFERENCE_HEIGHT
locomotionRate   = clamp(heightRatio ** -0.5,  0.70, 1.20)
oneShotRate      = clamp(heightRatio ** -0.35, 0.72, 1.15)
reactionArcScale = clamp(heightRatio ** -0.5,  0.60, 1.25)
```

Rules:
- The profile is applied only as `action.setEffectiveTimeScale(...)` on the
  mixer (`crossfadeToAction()` and both `triggerAction()` play paths). No clip
  is re-authored, `inPlaceRootMotion: true` is preserved, and the simulation
  digest is untouched.
- `motionPlaybackRate(profile, key)` selects `locomotionRate` for
  idle/move/run and `oneShotRate` for every combat beat.
- A record with no profile (unrigged/degraded actor) plays at rate 1.0.

### Directional Hit Reaction Routing (2026-07-30 amendment)

Reactions resolve as a **direction x damage-level matrix**. Direction is
measured in the target's own frame from the attacker's rendered position:

| Resolved direction | Relative angle to target facing |
|---|---|
| `front` | \|a\| <= 45 deg |
| `right` | 45 deg < a < 135 deg |
| `left` | -135 deg < a < -45 deg |
| `back` | \|a\| >= 135 deg |

- Clip keys are `hit_<direction>` / `bighit_<direction>`; a rig that does not
  ship them falls back deterministically to the flat `hit` / `bighit` key, so
  the routing is safe ahead of the directional retarget pass.
- Directional keys inherit the beat priority and fade envelope of their flat
  parent (`baseBeatKey()`), so direction never changes how a beat competes for
  the single one-shot slot.
- Entry point: `RealtimeBattle#triggerHitReaction(record, attackerRecord,
  heavy, nowMs)`, used by every damage-bearing event
  (`WEAPON_FIRED` critical, `SKILL_RESOLVED_DAMAGE`, `CRITICAL_HIT`,
  `ENEMY_ATTACK`, `MELEE_IMPACT`, `PROJECTILE_IMPACT`, `COMMANDER_DAMAGED`,
  `COMPANION_DAMAGED`).
- Verified by `tests/stage-framing-and-motion-profile.test.mjs`.

---

## 9. VALIDATION & TESTING CONTRACTS

### Test Files

**Main animation contract test:**
- `tests/ingame-motion-pack.test.mjs` (596 lines)
  - Validates manifest structure
  - Verifies nine clip quaternion deltas
  - Tests overlay composition with each compatible mesh
  - Checks runtime behavior (overlay precedence, fallback recovery)

**Runtime presentation tests:**
- `tests/combat-presentation-contract.test.mjs` (48 KB)
  - Verifies actor model loading, animation action mapping
  - Checks GLTFLoader is mocked correctly in test

**Asset manifest test:**
- `tests/release-closure.test.mjs`
  - Verifies all runtime asset paths exist

### Validation Commands

**Motion pack integrity** (before release):
```bash
node tests/ingame-motion-pack.test.mjs
```

**Combat presentation** (before release):
```bash
node tests/combat-presentation-contract.test.mjs
```

**Asset manifest audit:**
```bash
node tests/release-closure.test.mjs
```

---

## 10. ASSET MANIFEST & CHARACTER IDENTIFIERS

### Defense Asset Manifest
**File:** `assets/defense-asset-manifest.json` (208 KB)  
**Generator:** `scripts/build-defense-asset-manifest.mjs`  
**Purpose:** Audit trail of all runtime-retained assets (23 character models + terrain + vfx + props)

### Motion Manifest
**File:** `assets/motion/ingame/manifest.json` (25 KB JSON)  
**Generator:** `scripts/retarget-ingame-motion-blender.py`  
**Contract:** Schema v1 (immutable)

**Key Fields:**
- `sourceBoneNames[]` — all 37 Mixamo bones
- `targetBoneNames[]` — all 24 DEF-* target bones
- `boneMapping{}` — source → target quaternion delta map
- `clipOverrides{}` — 9 clips with frame ranges, FPS, durations, source SHA256
- `fallbackActions` — ["die", "show", "attack_melee", "attack_ranged"]
- `compatibleMeshes[]` — 24 character GLB paths (all characters + commander)
- `runtimeEligible: true/false` — gate validation result

**Gate Errors:** None (all 24 characters pass compatibility checks)

---

## 11. EXPECTED ASSET REFERENCES

### Complete Character Inventory for Direct Reference

**All 23 characters by full path:**

#### Bosses (10)
```
./assets/images/battle/glb/bosses/cinder-warden.glb
./assets/images/battle/glb/bosses/veil-tactician.glb
./assets/images/battle/glb/bosses/gate-sovereign.glb
./assets/images/battle/glb/bosses/tide-warden.glb
./assets/images/battle/glb/bosses/pack-herald.glb
./assets/images/battle/glb/bosses/requiem-choir.glb
./assets/images/battle/glb/bosses/lantern-tyrant.glb
./assets/images/battle/glb/bosses/bridge-colossus.glb
./assets/images/battle/glb/bosses/veiled-concordat.glb
./assets/images/battle/glb/bosses/abyss-regent.glb
```

#### Enemies (4)
```
./assets/images/battle/glb/enemies/scout.glb
./assets/images/battle/glb/enemies/shade.glb
./assets/images/battle/glb/enemies/guard.glb
./assets/images/battle/glb/enemies/possessed.glb
```

#### Companions (9)
```
./assets/images/battle/glb/companions/ember-cohort.glb
./assets/images/battle/glb/companions/rift-lens.glb
./assets/images/battle/glb/companions/veil-vanguard.glb
./assets/images/battle/glb/companions/anchor-shard.glb
./assets/images/battle/glb/companions/throne-echo.glb
./assets/images/battle/glb/companions/dawnless-crown.glb
./assets/images/battle/glb/companions/pack-warden.glb
./assets/images/battle/glb/companions/lantern-reaver.glb
./assets/images/battle/glb/companions/requiem-warden.glb
```

#### Commander (1)
```
./assets/images/battle/glb/commander/dusk-warden.glb
```

#### Overlay Motion Pack (1)
```
./assets/motion/ingame/unarmed-core.glb
./assets/motion/ingame/manifest.json
```

---

## 12. IMPLEMENTATION CHECKLIST FOR BENCH RETARGETING

- [ ] **Verify motion pack manifest** (`assets/motion/ingame/manifest.json`)
  - [ ] Schema version = 1
  - [ ] `runtimeEligible = true`
  - [ ] All 24 compatible meshes present & accessible
  - [ ] Gate errors empty

- [ ] **Verify all 23 character GLBs exist**
  - [ ] 10 bosses under `./assets/images/battle/glb/bosses/`
  - [ ] 4 enemies under `./assets/images/battle/glb/enemies/`
  - [ ] 9 companions under `./assets/images/battle/glb/companions/`
  - [ ] 1 commander at `./assets/images/battle/glb/commander/dusk-warden.glb`

- [ ] **Verify each character GLB structure**
  - [ ] Contains SkinnedMesh with DEF-* bones (24-bone skeleton)
  - [ ] Embeds 11-13 animation clips named `<assetId>::<actionKey>::v01`
  - [ ] All RIG_ACTION_KEYS covered OR fallback authors present (die, show, attack_melee, attack_ranged)
  - [ ] Skeleton has root bone `DEF-spine` (main spine chain)

- [ ] **Verify overlay pack (`assets/motion/ingame/unarmed-core.glb`)**
  - [ ] Contains exactly 9 clips (idle, move, run, hit, bighit, attack, critical, avoid, defence)
  - [ ] All clips quaternion-only (rotation deltas, no position tracks)
  - [ ] No skeleton/skin required (pure delta format)
  - [ ] Delta encoding validated: rest-relative format per `_workspace/current/overlay-architecture.md`
  - [ ] Quaternion normalization passes shortest-path check (no >180° sign flips)
- [ ] **Verify manifest bone mapping**
  - [ ] `sourceBoneNames` has 37 mixamorig:* bones
  - [ ] `targetBoneNames` has 24 DEF-* bones
  - [ ] `boneMapping` covers all 22 mapped bones (quantized quaternion tracks)
  - [ ] Source/target mismatch detection in place (unmapped arrays populated)

- [ ] **Verify clip override records**
  - [ ] 9 entries, one per action (idle, move, run, hit, bighit, attack, critical, avoid, defence)
  - [ ] Each entry has: frame range, FPS, duration, root travel, source SHA256
  - [ ] Loop flags correct (idle/move/run = true, others = false)

- [ ] **Verify fallback action assignment** (if overlay missing)
  - [ ] Characters with missing attack_melee fall back to attack
  - [ ] Characters with missing attack_ranged fall back to critical
  - [ ] All characters author die & show (no retarget fallback)

- [ ] **Verify runtime compatibility**
  - [ ] `tests/ingame-motion-pack.test.mjs` passes
  - [ ] All 24 meshes in `compatibleMeshes` array tested
  - [ ] Overlay composition cache working (`adaptedOverlayEntriesByModel` populated)
  - [ ] Adaptation math: `C_rest * delta` produces correct absolute rotations (spot-check 3 characters)
  - [ ] Rest pose extraction (`restQuatsFromGLB`) reads `nodes[].rotation` correctly
  - [ ] First-match wins: overlay clips supersede base clips on duplicate action keys
  - [ ] Fallback-only keys (die, show, attack_melee, attack_ranged) use base clips when overlay absent

---

## 13. COMPATIBILITY RISKS & CONSTRAINTS

### OBSERVED Constraints
1. **Skeleton naming is immutable** — All 24 DEF-* bones hardcoded in manifest
2. **Clip naming convention fixed** — Must follow `<assetId>::<actionKey>::v01` or bare name
3. **Loop mode determined by action key** — No per-clip override
4. **Overlay format is quaternion-delta only** — No position or scale tracks
5. **23 characters locked** — Adding/removing requires code edit (no dynamic lookup)
6. **Motion pack is unarmed-only** — Only 9 actions; weapon variants must author attack_melee/attack_ranged

### Risk: Adding New Character
- Must add to BOSS_MODELS / ENEMY_MODELS / COMPANION_MODELS (code edit)
- Must use 24-bone DEF-* skeleton
- Must author all 11 RIG_ACTION_KEYS (or at least the 4 non-fallback ones)
- Or rely on overlay + fallback for missing combat/reaction clips

### Risk: Changing Motion Pack
- Overlay skeleton mapping is frozen in manifest
- Mixamo → DEF-* retargeting fixed; cannot change source/target rig
- If new motions use different source skeleton, must regenerate full manifest

### Risk: Changing Clip Naming
- Parser is fixed to `<assetId>::<actionKey>::v01` format
- Bare clip names tolerated but not preferred
- New names cannot be added without code change to RIG_ACTION_KEYS

---

## 14. CODE REFERENCES (Exact File:Line)

| Concept | File | Line(s) |
|---------|------|---------|
| GLTFLoader import | battle-realtime-three.js | 9 |
| gltfLoader instance | battle-realtime-three.js | 714 |
| gltfCache | battle-realtime-three.js | 715 |
| loadGltf(path) | battle-realtime-three.js | 736-749 |
| modelUrl(path) | battle-realtime-three.js | 729-734 |
| BOSS_MODELS | battle-realtime-three.js | 340-350 |
| ENEMY_MODELS | battle-realtime-three.js | 352-358 |
| COMPANION_MODELS | battle-realtime-three.js | 360-365 |
| COMMANDER_MODEL | battle-realtime-three.js | 366 |
| RIG_ACTION_KEYS | battle-realtime-three.js | 368-370 |
| LOCOMOTION_ACTION_KEYS | battle-realtime-three.js | 372 |
| actionKeyFromClipName(name) | battle-realtime-three.js | 1253-1257 |
| buildActions(mixer, clipEntries) | battle-realtime-three.js | 1264-1283 |
| OVERLAY_ANIMATION_PATH | battle-realtime-three.js | 1065 |
| OVERLAY_ACTION_KEYS | battle-realtime-three.js | 1066 |
| loadOverlayDeltaEntries() | battle-realtime-three.js | 1071-1085 |
| normalizeOverlayDeltaClip(clip) | battle-realtime-three.js | 1087-1111 |
| adaptOverlayEntries(modelPath, instance, deltaEntries) | battle-realtime-three.js | 1163-1176 |
| adaptedOverlayEntriesByModel (caching Map) | battle-realtime-three.js | 1069 |
| restQuatsFromInstance(instance) | battle-realtime-three.js | 1113-1124 |
| warnedOverlayLoadFailure | battle-realtime-three.js | 1067 |
| Overlay architecture design | _workspace/current/overlay-architecture.md | 1-274 |
| instantiateActorModel(relPath, targetHeight) | battle-realtime-three.js | 1383-1414 |
| TARGET_HEIGHT object | battle-realtime-three.js | 374-387 |
| fitHeight(object3d, targetHeight) | battle-realtime-three.js | 1193-1206 |
| SkeletonUtils.clone() | battle-realtime-three.js | 10, 1393 |
| Animation test suite | tests/ingame-motion-pack.test.mjs | 1-596 |
| Combat presentation test | tests/combat-presentation-contract.test.mjs | 1-+ |
| Asset manifest | assets/defense-asset-manifest.json | (208 KB) |
| Motion manifest | assets/motion/ingame/manifest.json | (25 KB) |
| Retained assets list | scripts/defense-runtime-assets.mjs | 1-103 |

---

## 15. SUMMARY

The authoritative contract is the amendment in §0 plus the generated runtime
registry and per-asset manifests.

- **Loader:** Three.js `GLTFLoader`, cloned per actor with its own skeleton.
- **Runtime motion library:** 11 promoted character assets and 121 embedded
  clips in the current registry.
- **Overlay system:** 9-clip unarmed motion pack retargeted onto all 23 character
  rigs via rest-relative quaternion delta composition (§5). Designed at
  `_workspace/current/overlay-architecture.md`
- **Skeleton:** `def-humanoid-v1`, with semantic regions weighted to adjacent
  parent/child joint chains.
- **Playback:** fixed-tick mixer updates, in-place locomotion, ordinary
  crossfades, priority-queued one-shots, and terminal death.
- **Source boundary:** individual semantic character/prop meshes are promoted;
  fused OBJ presentation is not a runtime shortcut.
- **Proof:** Blender deformation reports, registry hashes, targeted Node tests,
  and browser playback must agree before promotion.
