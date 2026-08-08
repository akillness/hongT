# W6 asset: stage boss trio reskin (s1-cinder-warden / s2-veil-tactician / s3-gate-sovereign)

Safety tag: `pre-asset-lane2-20260808` (created before any write). Abyssal-Surge was
read-only for this pass -- `git -C ../../../Abyssal-Surge status --short` confirmed
no changes attributable to this session (unrelated QA-lane diffs pre-existed).
Assets/Scripts/** untouched, no commits made.

## 1. Manifest check [OBSERVED]

`../../../Abyssal-Surge/assets/defense-asset-manifest.json`, all three IDs:

| id | rows | retain rows | usable |
|---|---|---|---|
| s1-cinder-warden | 23 | 1 (`glb/base_basic_pbr.glb`) | yes |
| s2-veil-tactician | 23 | 1 (`glb/base_basic_pbr.glb`) | yes |
| s3-gate-sovereign | 23 | 1 (`glb/base_basic_pbr.glb`) | yes |

For each id, only `assets/mesh/boss/<id>/glb/base_basic_pbr.glb` is
`disposition:retain, runtimeReference:true`. The other 22 rows per id (fbx/obj
variants, `textureBasicPack/*.png`, `*.raw.png`) are all `disposition:delete,
runtimeReference:false` and were **not** used (per instructions, delete-only
siblings are skipped, not substituted for the retained file). All three retained
GLBs exist on disk (12.7MB / 10.7MB / 12.5MB) and were confirmed byte-present
before use.

## 2. Blocker found and resolved: no motion-library donor rig [OBSERVED]

Unlike `human-command-boss` (which had a retained rig at
`assets/motion/ingame/characters/human-command-boss/model.glb`), **none of the
three retained boss GLBs contain an armature** -- each is a single unrigged mesh
node named `model` (0 skins, 1 node). This is the standard shape of a
Meshy/Rodin-style static output, not the fused/blockout case the pipeline docs
already cover.

`find assets/motion -iname '*s1-cinder-warden*'` (and s2/s3 equivalents) returned
**empty** -- there is no motion-library counterpart for any of the three ids, so
`tools/blender/reskin_all.sh`'s pattern (donor armature = same character's own
motion-library rig, mesh swapped from the higher-quality `assets/mesh/...`
source) doesn't apply directly; there is no "own rig" to borrow.

**Resolution:** all `assets/motion/ingame/characters/*/model.glb` skeletons share
the same `def-humanoid-v1` bone topology (`DEF-spine`, `DEF-thigh.L`, etc. --
identical to `reskin_character.py`'s `BONE_MAP`), so any existing rigged donor is
structurally compatible; only the proportions differ. `guard/model.glb` was
tried first (the pipeline's "clean generic donor" per `reskin_all.sh`'s own
comment) but failed the script's fatal gate for all three meshes:

| id | guard donor armFit | verdict |
|---|---|---|
| s1-cinder-warden | 0.40 | FATAL (<0.7) |
| s2-veil-tactician | 0.35 | FATAL (<0.7) |
| s3-gate-sovereign | 0.42 | FATAL (<0.7) |

Diagnosis: guard's own skeleton bounding box is anomalous (skeletonHeight 1.13 vs
skeletonSpan 2.23, a ~2:1 ratio that doesn't hold for any other rig checked).
Switched the donor to `shadow-commander-boss/model.glb` (skeletonHeight 1.73,
skeletonSpan 1.57 -- proportions matching the already-successful
human-command-boss/scout/lantern-reaver reskins), which resolved all three:

| id | shadow-commander-boss donor armFit | verdict |
|---|---|---|
| s1-cinder-warden | 0.86 | pass |
| s2-veil-tactician | 0.76 | pass |
| s3-gate-sovereign | 0.91 | pass |

**This is a judgment call flagged for review, not silently assumed final:** the
three bosses now share a donor skeleton borrowed from an unrelated existing boss
(shadow-commander-boss) rather than a character-specific rig, because no
character-specific rig exists in the source repo. Documented per-id in
`docs/provenance/{id}-reskin.json` under `donorRigDeviation`.

## 3. Reskin results [OBSERVED]

Command pattern (repeated per id, donor fixed at shadow-commander-boss):

```
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 \
  -P tools/blender/reskin_character.py -- \
  --glb .../Abyssal-Surge/assets/motion/ingame/characters/shadow-commander-boss/model.glb \
  --mesh-glb .../Abyssal-Surge/assets/mesh/boss/<id>/glb/base_basic_pbr.glb \
  --out Assets/Art/Characters/<id>.fbx \
  --report _workspace/current/engineering/reskin/<id>.json \
  --max-tris 25000
```

| id | source tris | final tris | decimated | heat orphans | non-normalized verts | bones |
|---|---|---|---|---|---|---|
| s1-cinder-warden | 17264 | 17264 | no | 0 | 0 | 22 |
| s2-veil-tactician | 18108 | 18104 | no | 0 | 0 | 22 |
| s3-gate-sovereign | 38657 | **25000** | **yes** (ratio 0.647) | 0 | 0 | 22 |

All three at or under the 25k tri budget. s3-gate-sovereign's source exceeded the
budget by 55% and was decimated (post-skinning collapse, weights preserved);
worth a visual silhouette spot-check given the 35% triangle reduction, flagged in
its provenance file.

FBX-embedded textures (diffuse/metallic-roughness/normal): all downscaled to
1024x1024 by the script's own `MAX_TEX` cap before embed -- within the 1024
texture contract at the embed level.

Output: `Assets/Art/Characters/{s1-cinder-warden,s2-veil-tactician,s3-gate-sovereign}.fbx`
(7.9MB / 7.8MB / 8.2MB). No `.fbx.meta` created yet -- matches the
human-command-boss precedent (Unity import/prefabbing is CharacterImportPipeline's
job, out of scope here).

## 4. Texture placement [OBSERVED]

`texture_diffuse` (image index 1) and `texture_normal` (image index 0) were
extracted directly from each **retained** GLB's binary bufferViews (glTF JSON
chunk + BIN chunk parsed by hand, no Blender round-trip) -- not from the
delete-disposition `textureBasicPack/*.png` siblings, and not from the
FBX-embedded 1024px copies. This matches the resolution convention already
established by `shadow-commander-boss-textures/` and
`broken-court-monarch-boss-textures/` (both ship native 2048x2048 source PNGs
with the import cap applied via `.meta`, not by pre-resizing the file).

| id | diffuse | normal |
|---|---|---|
| s1-cinder-warden | 2048x2048 | 2048x2048 |
| s2-veil-tactician | 2048x2048 | 2048x2048 |
| s3-gate-sovereign | 2048x2048 | 2048x2048 |

Placed at `Assets/Art/Characters/<id>-textures/texture_{diffuse,normal}_001.png`,
matching the human-command-boss placement convention exactly (folder name,
file naming with the `_001` suffix mirroring the Blender `.001` material-name
collision seen in every mesh-swap reskin).

`.meta` files (folder + both PNGs) were hand-authored from
`broken-court-monarch-boss-textures/*.meta` (chosen over
`human-command-boss-textures/*.meta` because the latter leaves the WebGL
platform override at 2048, which does not actually satisfy the repo's "텍스처
≤1024" contract for the WebGL deploy target -- the broken-court-monarch-boss /
shadow-commander-boss template explicitly caps WebGL to 1024 too). Diffuse:
`textureType: 0`, `sRGBTexture: 1`. Normal: `textureType: 1`, `sRGBTexture: 0`.
Both: `DefaultTexturePlatform` and `WebGL` `maxTextureSize: 1024`. All 9 new
GUIDs (3 folders + 6 texture files) verified unique against the rest of
`Assets/` via `grep -rl`.

## 5. Not done (explicitly out of scope per task instructions)

- No `Assets/Art/Characters/Materials/*.mat` created (would require Unity Editor
  or hand-authored `.mat` YAML referencing the new texture GUIDs -- reserved for
  CharacterImportPipeline, same as the human-command-boss precedent).
- No `Assets/Resources/Characters/*.prefab` created.
- `CharacterRoster.Ids` not touched (`Assets/Scripts/**` off-limits for this lane).
- No commit made.

## 6. Files touched

New (all untracked, nothing staged):

```
Assets/Art/Characters/s1-cinder-warden.fbx
Assets/Art/Characters/s1-cinder-warden-textures.meta
Assets/Art/Characters/s1-cinder-warden-textures/texture_diffuse_001.png(.meta)
Assets/Art/Characters/s1-cinder-warden-textures/texture_normal_001.png(.meta)
Assets/Art/Characters/s2-veil-tactician.fbx  (+ textures, same pattern)
Assets/Art/Characters/s3-gate-sovereign.fbx  (+ textures, same pattern)
_workspace/current/engineering/reskin/{s1-cinder-warden,s2-veil-tactician,s3-gate-sovereign}.{json,log}
docs/provenance/{s1-cinder-warden,s2-veil-tactician,s3-gate-sovereign}-reskin.json
_workspace/current/engineering/asset-lane2-boss-reskin-report.md   (this file)
```

## 7. Blocker for orchestrator review

The donor-rig substitution (all three bosses rigged against
`shadow-commander-boss`'s skeleton instead of a character-specific rig, because
none exists in the source repo) is a design-adjacent decision, not just
mechanical execution. It is technically sound (all `def-humanoid-v1` rigs share
bone topology; armFit landed 0.76-0.91, comfortably above the 0.7 fatal gate and
in the same range as prior successful reskins) but changes which body
proportions each new boss silhouette is built on. Recommend a visual check
(screenshot or Unity import) before promoting these three to the roster,
especially s3-gate-sovereign given its 35% decimation.
