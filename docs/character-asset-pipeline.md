# Character Asset Pipeline — Body Source → Cartoon Texture → Rig → Animation → Per-Character Albedo Bake


이 문서는 Abyssal Surge 캐릭터 리소스를 재생성할 때 사용하는 재현 가능한 파이프라인이다. 목표는 **캐릭터 본체만 genuine T-pose로 생성**하고, 지형·발판·무기·손에 든 소품은 처음부터 제외한 뒤, 별도 후보 lane에서 텍스처·리깅·애니메이션을 검증하는 것이다.

상태 표기: `[OBSERVED]`는 이 저장소에서 확인한 결과, `[TARGET]`은 통과 조건, `[BLOCKED]`는 현재 환경에서 아직 수행할 수 없는 단계다.

## 1. 절대 계약

1. Rodin 입력은 캐릭터 본체만 생성한다.
2. `terrain`, `floor`, `pedestal`, `platform`, `rocks`, `weapons`, `shields`, `held props`, `equipment`가 포함된 결과는 후보로도 통과시키지 않고 재생성한다. 융합된 marching-cubes 메시에 사후 본/오브젝트 회전을 적용해 제거하지 않는다.
3. T-pose 조건 메쉬, 생성 결과, 텍스처, 리그, 애니메이션은 `assets/images/battle/glb/` 밖의 별도 candidate lane에만 저장한다.
4. 텍스처는 캐릭터 본체의 active UV를 통해 Base Color에 매핑한다. UV가 없는 메시는 실패시킨다.
5. 실제 런타임 GLB 승격 전에는 rights receipt, T-pose, 지형/무기 부재, skin weight, 11개 clip, GLB export, Three.js 및 Canvas fallback 검증이 모두 필요하다.
6. 런타임 ID와 기존 배포 GLB는 자동으로 덮어쓰지 않는다.

## 2. 현재 lane layout

현재 cycle: `_workspace/20260726-stage1b-cinder-pressure-agency/`

```text
_workspace/20260726-stage1b-cinder-pressure-agency/engineering/asset-pipeline/
├── asset-lanes.json                         # lane validator policy
├── action-pipeline.json                     # 11-action timing/NLA contract
├── concept-input/                           # Rodin/gti reference and texture candidates
├── tpose-conditions/                        # Rodin BoundingBox ControlNet condition GLBs
├── rodin-candidates/                        # Rodin 생성 결과 (현재 commander 1종만 존재)
├── all-mesh-texture-candidates{,-v2}/       # Blender texture-mapped candidate GLBs
└── runtime-candidates/                      # 승격 직전 후보 lane
    ├── cartoon-texture/                     # 공유 toon 표면/노멀을 입힌 후보
    ├── rigged-lower-mesh/                   # `<id>_pedestal`을 본체에 스킨한 후보
    ├── wholebody-motion/                    # 상·하체가 모두 움직이는 clip 후보
    └── character-albedo/                    # 캐릭터별 cartoon albedo를 구운 후보 (승격 직전 마지막 stage)
```

- Concept/reference input: `assets/images/battle/pilot/`
- Deployed runtime lane: `assets/images/battle/glb/`
- Validator: `scripts/validate-asset-lanes.py`
- Policy: `_workspace/20260726-stage1b-cinder-pressure-agency/engineering/asset-pipeline/asset-lanes.json`

`runtime-candidates/`를 비롯한 대부분의 candidate lane은 `.gitignore` 대상(로컬 생성물)이다. 따라서 clean checkout에서는 lane 의존 테스트가 skip되고, 런타임에 실제로 실린 바이트는 `assets/images/battle/glb/character-build-provenance.json`으로만 검증된다.

A candidate `.glb`/image requires a sibling `.provenance.json` with `runtimeEligible: false` until promotion review. The validator intentionally allows missing candidate directories for a clean baseline.


## 3. Rodin Bridge prompt

### Positive prompt

```text
Generate a game-ready humanoid character source mesh in a genuine T-pose:
full body centered, arms extended horizontally, feet separated, neutral pose,
clean silhouette, topology suitable for skinning and animation. Character body
only; exclude terrain, floor, pedestal, rocks, platform, weapons, shields, held
props, equipment, debris, and background geometry. No text, logos, or watermark.
```

### Negative prompt

```text
terrain, floor, pedestal, rocks, platform, weapon, shield, sword, staff,
held prop, equipment, debris, background geometry, text, logo, watermark
```

`rodin-tpose-regen.py` stores this contract in the generated plan, uses a measured per-character blockout as the `bbox` condition, and never writes directly to `assets/images/battle/glb/`.

```bash
blender -b -P scripts/rodin-tpose-regen.py -- --plan-only
blender -P scripts/rodin-tpose-regen.py -- --submit --only cinder-warden
```

The submit step requires GUI Blender and an authenticated Rodin browser session. A downloaded result must first be copied into `runtime-candidates/<category>/`; no direct runtime replacement is allowed.

## 4. Cartoon texture generation and mapping

The texture prompt must request a **reference atlas**, not a perspective character illustration:

```text
Create a clean, game-ready cartoon-render albedo texture reference sheet for a
humanoid dark-fantasy character. Use an orthographic UV-atlas style layout with
separated front, side, back, limb, hand, leg, boot, cloth, and cloak material
panels; flat readable color blocks; blackened steel; ash-gray cloth; muted silver
edge accents; restrained cyan emissive accents; hand-painted cel-shaded wear.
This is a texture sheet/reference only, not a character illustration. No 3D
render, perspective, floor, terrain, pedestal, rocks, weapon, shield, held prop,
equipment, character silhouette, scene background, text, logo, or watermark.
```

`god-tibo-imagen` dry-run validation:

```bash
gti --prompt "<texture prompt>" \
  --image assets/images/battle/pilot/dusk-warden-idle-gti-refstyle.png \
  --size 1024x1024 --dry-run
```

The generated candidates used for this record are:

- `[OBSERVED]` v2 was rejected as a reference candidate after visual review found generated section labels; it remains non-runtime evidence only.
- `[OBSERVED]` v3: `_workspace/20260726-stage2-balance-agency/engineering/asset-pipeline/concept-input/dusk-warden-cartoon-albedo-v3.png`
- `[OBSERVED]` v3 SHA-256: `0f269d2f0de0b54c314697bda9ed9a6b629c6d84d61be1a536361b97e0c0668d`
- `[OBSERVED]` v3 provenance: adjacent `.provenance.json`, `runtimeEligible: false`
- `[OBSERVED]` v3 visual review shows an unlabelled atlas-like reference with front/side/back armor, arms, hands, legs, boots, cloth, and cloak panels.
- `[TARGET]` Blender must unwrap or project the body UVs and bake/pack the atlas into the candidate body material; the generated 2D atlas is not assumed to match arbitrary Rodin UV islands.

Mapping command:

```bash
python3 scripts/apply-cartoon-texture-blender.py \
  --glb <candidate-body.glb> \
  --texture _workspace/20260726-stage2-balance-agency/engineering/asset-pipeline/concept-input/dusk-warden-cartoon-albedo-v3.png \
  --asset-id dusk-warden \
  --out _workspace/20260726-stage2-balance-agency/engineering/asset-pipeline/texture-candidates/glb/dusk-warden.glb \
  --report _workspace/20260726-stage2-balance-agency/engineering/asset-pipeline/texture-candidates/reports/dusk-warden.json
```

The script preserves the source GLB, modifies only the body material, explicitly connects active UV coordinates to the image texture, and fails when the body has no UV map. Terrain/pedestal and weapon meshes are not recolored or promoted by this pass.

## 5. Character-only rigging and animation

Use `scripts/rig-character-asset-blender.py`, not the retired proxy/previs path. The current rig is a deform-only skeleton with Rigify-compatible `DEF-` names. It fits a horizontal-arm source with `--arm-fit tpose`, preserves the natural bind pose, applies adjacent DEF weights, keeps weld distance at zero by default, partitions faces into semantic torso/arm/leg skinned regions, and authors the action library.

```bash
blender -b -P scripts/rig-character-asset-blender.py -- \
  --glb <clean-tpose-source.glb> \
  --asset-id dusk-warden \
  --category commander \
  --arm-fit tpose \
  --rest-pose natural \
  --weld-distance 0.0 \
  --out _workspace/20260726-stage1b-cinder-pressure-agency/engineering/asset-pipeline/rig-candidates/dusk-warden.glb \
  --report _workspace/20260726-stage1b-cinder-pressure-agency/engineering/asset-pipeline/rig-candidates/dusk-warden.json \
  --budgets-json _workspace/20260726-stage1b-cinder-pressure-agency/engineering/asset-pipeline/action-pipeline.json

```

The source-name audit rejects objects containing terrain/weapon/prop tokens; a pre-existing named `_pedestal` split is removed before export. It does not pretend that a fused mesh can be safely cleaned by name, so visual review remains mandatory.

The canonical action set is:

```text
idle move run hit bighit attack critical avoid defence die show
```

`build-motion-prompt-batch.py` creates concept/reference prompts for these 11 actions. It never creates a runtime animation or GLB:

```bash
python3 scripts/build-motion-prompt-batch.py \
  --asset-id dusk-warden \
  --out _workspace/20260726-stage1b-cinder-pressure-agency/engineering/asset-pipeline/motion-prompts/dusk-warden.json

```
The canonical Dusk Warden references are validated with their adjacent `.provenance.json` files. For an isolated fixture or another character, repeat `--concept-input PATH`; every supplied input must exist, remain outside the runtime GLB/model lanes, and declare `runtimeEligible: false`.


The prompt packet preserves the concept vocabulary `hunt → extract → materialize → capture → assault`; Blender NLA remains the authoring step and runtime clip verification is required.

## 5-bis. Per-character cartoon albedo bake

`scripts/bake-character-albedo.py`는 승격 직전 마지막 stage다. 이전까지 24개 캐릭터 중 23개는 **albedo 아트가 전혀 없었다**: 공유 256 px detail tile(`abyssal-toon-surface-subtle-v01.png`) × 캐릭터별 `baseColorFactor` 한 값이 전부였고, 그래서 cel ramp 아래에서 실루엣이 뭉개져 보였다.

이 stage는 원본 아틀라스를 복원하는 대신 **메시가 실제로 쓰는 UV 언랩 위에 직접 굽는다**:

- 삼각형을 UV 공간에 barycentric rasterize → 각 texel의 로컬 위치·노멀 확보
- 기존 `baseColorFactor`를 mid anchor로 shadow/body/lit/rim 4-band + accent(sash·boot·crown) 구성
- 공유 detail tile을 낮은 진폭으로 곱해 표면 grain 유지
- island 바깥으로 12 texel **dilation** → 필터링/밉맵이 UV **이음새(seam)**에서 배경을 샘플링하지 못하게 함
- albedo가 텍스처로 들어갔으므로 `baseColorFactor`는 `[1,1,1,1]`로 되돌려 이중 착색을 막는다

commander(`dusk-warden`)만 copy-through다: 이미 authored albedo 아틀라스를 갖고 있고 배포 바이트가 `tests/commander-guard-pose.test.mjs`에 고정돼 있다.

```bash
python3 scripts/bake-character-albedo.py            # candidate lane 굽기
python3 scripts/bake-character-albedo.py --check    # 재실행 결과가 바이트 동일한지 검증
python3 scripts/promote-character-assets.py         # 런타임 lane 승격 + provenance 기록
```

계약은 `tests/character-albedo-bake.test.mjs`가 지킨다: 24개 아틀라스 해시가 서로 다를 것, 1024², 배경 대비 채워진 비율, UV vertex texel 주변 12 texel 무배경(seam padding), provenance 해시 일치, `--check` 재현성.

## 6. Verification checklist

| Check | Required evidence | Status |
|---|---|---|
| Rodin positive/negative prompt recorded | generated plan `promptContract` | `[OBSERVED]` code contract added |
| T-pose condition stored separately | `tpose-conditions/<asset>-tpose-condition.glb` | `[TARGET]` requires GUI Blender/Rodin |
| No terrain/pedestal | visual audit + object/mesh report | `[TARGET]` |
| No weapon/held prop | visual audit + object/mesh report | `[TARGET]` |
| Cartoon texture provenance | image + adjacent provenance JSON | `[OBSERVED]` Dusk Warden v3 candidate (concept lane, `runtimeEligible:false`) |
| Active UV mapping | Blender report `activeUvMap`, `uvLayerCount` | `[TARGET]` |
| Per-character albedo on the mesh's own UVs | `character-build-provenance.json` `albedoBake` + `tests/character-albedo-bake.test.mjs` | `[OBSERVED]` 23 baked, commander copy-through |
| UV seam padding | 12-texel dilation asserted from the shipped PNG | `[OBSERVED]` |
| Body-only skinning | rig report, weighted vertex counts, T-pose deviation ≤12° | `[TARGET]` |
| 11 action clips | NLA/GLB clip census | `[OBSERVED]` 24/24 in `runtimeContract.animationNames` (commander 13) |
| Runtime safety | lane validator + GLB validator + browser/Canvas fallback | `[TARGET]` |

Current environment status:

- `[OBSERVED]` Blender 5.1.2가 `/Applications/Blender.app`에 설치돼 있고, rig/clip 단계는 headless python 스크립트(`scripts/bind-static-lower-mesh.py`, `scripts/author-wholebody-clips-blender.py`)로 실제 실행됐다. GUI 기반 Rodin handoff와 육안 리뷰는 여전히 사람 몫이다.
- `[OBSERVED]` 캐릭터 본체 24종 중 Rodin bridge를 거친 것은 commander 1종뿐이고, 나머지 23종은 `scripts/tpose_blockout.py`의 parametric blockout이다. 근거: `character-build-provenance.json`의 `bodyOrigin`, `_workspace/20260726-stage1b-cinder-pressure-agency/qa/character-tech-provenance-review.md`.
- `[OBSERVED]` The generation CLI is authenticated and dry-run validated; the Dusk Warden v3 candidate was generated without promoting it to runtime.


## 7. Useful references

- [Hyper3D Rodin](https://hyper3d.ai/)
- [god-tibo-imagen source](https://github.com/NomaDamas/god-tibo-imagen)
- [god-tibo-imagen npm](https://www.npmjs.com/package/god-tibo-imagen)
- [Blender Rigify source](https://github.com/blender/blender-addons/tree/main/rigify)
- [Khronos glTF](https://www.khronos.org/gltf/)
- [glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)
- [glTF Validator](https://github.com/KhronosGroup/glTF-Validator)
- Local Rodin planning tool: `scripts/rodin-tpose-regen.py`
- Local texture mapping tool: `scripts/apply-cartoon-texture-blender.py`
- Local rigging tool: `scripts/rig-character-asset-blender.py`
- Local motion prompt tool: `scripts/build-motion-prompt-batch.py`
