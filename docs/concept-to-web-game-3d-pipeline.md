# Concept → T-Pose → 3D Mesh → Rigging → Motion → Audio Pipeline

> **Web Game 3D Character Production Pipeline**
> Abyssal Surge — Abyssal-Command
> Date: 2026-07-28

이 문서는 **컨셉 이미지 → T-Pose 이미지 → 3D Mesh 생성 → 리깅(Rigging) → 모션 애니메이션 → 오디오(SFX/BGM/음성)** 까지의 전체 파이프라인을 프롬프트와 함께 상세히 정의한다. Three.js/WebGL 기반 웹 게임(Abyssal Surge) 관점에서 각 단계의 출력물이 어떻게 런타임에 통합되는지 명시한다.

**사용 가능한 툴/스킬셋:**
- `god-tibo-imagen` (gti CLI) — 이미지 생성
- `Blender` + `Rodin Bridge` (Hyper3D Rodin) — 3D 메시 생성
- `Motion Previs Studio` (v4) — 모션 분석
- `Blender Python Scripting` — 리깅/애니메이션/텍스처 베이킹
- `ElevenLabs API` — TTS(나레이션), SFX(효과음), BGM(배경음)
- `Three.js 런타임` — 최종 GLB 로딩, 상태 머신 기반 애니메이션 재생

---

## 목차

1. [Phase 1: 컨셉 이미지 기획(T-Pose 생성)](#phase-1-컨셉-이미지-기획t-pose-생성)
2. [Phase 2: T-Pose → 3D 메시 생성 (Rodin Bridge)](#phase-2-t-pose--3d-메시-생성-rodin-bridge)
3. [Phase 3: 텍스처 매핑 & 카툰 렌더링](#phase-3-텍스처-매핑--카툰-렌더링)
4. [Phase 4: 리깅(Rigging) & 스키닝](#phase-4-리깅rigging--스키닝)
5. [Phase 5: 모션 애니메이션 — Motion Previs → Blender NLA](#phase-5-모션-애니메이션--motion-previs--blender-nla)
6. [Phase 6: 오디오 — ElevenLabs SFX/BGM/TTS](#phase-6-오디오--elevenlabs-sfxbgmtts)
7. [Phase 7: 런타임 통합 (Three.js GLB 로딩 & 재생)](#phase-7-런타임-통합-threejs-glb-로딩--재생)
8. [검증 체크리스트](#검증-체크리스트)

---

## Phase 1: 컨셉 이미지 기획(T-Pose 생성)

**목표:** 게임 캐릭터의 컨셉 아트(reference image)를 바탕으로, Rodin Bridge의 입력 조건(condition)으로 사용할 T-Pose 이미지 또는 Blockout Mesh를 생성한다.

### 1-A. Reference Style을 위한 gti 프롬프트

`god-tibo-imagen`을 사용해 캐릭터의 **참고 스타일 이미지**를 생성한다. 이 이미지는 이후 텍스처 프롬프트의 refstyle 입력 또는 컨셉 확정 용도로 사용한다.

```bash
# dry-run으로 프롬프트 검증
gti --prompt " \
  Full-body character concept for a dark fantasy defender in genuine T-pose. \
  Arms extended horizontally at shoulder height, palms facing down. \
  Feet shoulder-width apart, facing forward. Neutral expression. \
  Clean silhouette against solid neutral background. \
  Armored dark fantasy style: blackened steel plate, ash-gray cloth, muted silver trim, \
  restrained cyan magical accents, worn battle texture. \
  No weapons, no shield, no terrain, no pedestal, no text, no watermark." \
  --size 1024x1024 \
  --dry-run
```

**실제 생성:**
```bash
# refstyle 참고용 이미지 생성
gti --prompt " \
  Full-body dark fantasy defender T-pose reference. \
  Arms horizontal, palms down, feet apart. \
  Blackened steel armor, ash-gray cloth, muted silver trim, \
  cyan magical eye accent. Worn battle-worn texture. \
  No weapons, no shield, no terrain, no background distractions." \
  --output assets/images/battle/pilot/<character-id>-tpose-refstyle.png
```

### 1-B. UV Atlas (텍스처 시트) 프롬프트

Rodin 생성 전에 텍스처 레퍼런스를 먼저 확보한다. gti로 **UV 아틀라스 스타일의 텍스처 시트**를 생성한다.

```bash
gti --prompt " \
  Create a clean, game-ready cartoon-render albedo texture reference sheet for a \
  humanoid dark-fantasy character. Use an orthographic UV-atlas style layout with \
  separated front, side, back, limb, hand, leg, boot, cloth, and cloak material \
  panels; flat readable color blocks; blackened steel; ash-gray cloth; muted silver \
  edge accents; restrained cyan emissive accents; hand-painted cel-shaded wear. \
  This is a texture sheet/reference only, not a character illustration. No 3D \
  render, perspective, floor, terrain, pedestal, rocks, weapon, shield, held prop, \
  equipment, character silhouette, scene background, text, logo, or watermark." \
  --input assets/images/battle/pilot/<character-id>-tpose-refstyle.png \
  --size 1024x1024 \
  --output _workspace/<run-id>/engineering/asset-pipeline/concept-input/<character-id>-cartoon-albedo.png
```

### 1-C. T-Pose Blockout Mesh (Blender Procedural)

Rodin의 BoundingBox ControlNet 조건으로 사용할 T-Pose Blockout을 Blender로 생성한다. 정확한 바운딩 박스가 있어야 Rodin이 캐릭터 본체만 생성하고 지형/무기를 포함하지 않는다.

**Python 스크립트 (`scripts/tpose_blockout.py`) 실행:**
```bash
blender -b -P scripts/tpose_blockout.py -- \
  --character-height 1.8 \
  --arm-span 1.6 \
  --out _workspace/<run-id>/engineering/asset-pipeline/tpose-conditions/<character-id>-tpose-condition.glb
```

**Blockout 규칙:**
- 캐릭터 본체의 Bounding Box만 포함
- `terrain`, `floor`, `pedestal`, `rocks`, `weapon`, `shield` 절대 금지
- 양팔은 수평, 다리는 어깨 너비, 중립 표정
- 결과는 `tpose-conditions/` 디렉토리에 저장 (절대 런타임에 직접 복사 금지)

---

## Phase 2: T-Pose → 3D 메시 생성 (Rodin Bridge)

**목표:** T-Pose Blockout Mesh를 Rodin(Hyper3D) 브라우저 세션으로 전송하여 게임 레디 3D 캐릭터 메시를 생성한다.

### 2-A. Rodin Positive / Negative Prompt

Rodin Genie / Image-to-3D / Mesh-to-3D에 사용할 정확한 프롬프트 쌍.

**Positive Prompt (절대 계약):**
```text
Generate a game-ready humanoid character source mesh in a genuine T-pose:
full body centered, arms extended horizontally, feet separated, neutral pose,
clean silhouette, topology suitable for skinning and animation. Character body
only; exclude terrain, floor, pedestal, rocks, platform, weapons, shields, held
props, equipment, debris, and background geometry. No text, logos, or watermark.
```

**Negative Prompt (절대 계약):**
```text
terrain, floor, pedestal, rocks, platform, weapon, shield, sword, staff,
held prop, equipment, debris, background geometry, text, logo, watermark
```

### 2-B. Rodin 조건 설정

1. **Condition Type:** BoundingBox ControlNet
2. **Input:** Phase 1-C에서 생성한 `tpose-condition.glb`
3. **Mode:** Mesh-to-3D (또는 Image-to-3D)
4. **Output:** 캐릭터 본체만 포함된 clean T-Pose GLB

### 2-C. Rodin 제출 스크립트

```bash
# 계획만 생성 (GUI Rodin 없이)
blender -b -P scripts/rodin-tpose-regen.py -- --plan-only

# 실제 제출 (GUI Blender + Rodin 브라우저 세션 필요)
blender -P scripts/rodin-tpose-regen.py -- --submit --only <character-id>
```

### 2-D. 다운로드 & Candidate Lane

Rodin 생성 결과는 반드시 다음 규칙을 따라야 한다:

```text
1. 다운로드한 GLB를 candidate lane에 복사
   → _workspace/<run-id>/engineering/asset-pipeline/rodin-candidates/<character-id>.glb

2. provenance JSON 작성
   → _workspace/<run-id>/engineering/asset-pipeline/rodin-candidates/<character-id>.provenance.json
   { "runtimeEligible": false, "bodyOrigin": "rodin", "promptContract": {...} }

3. 육안 검증 (terrain/pedestal/weapon 유무)
4. 통과 시 texture-candidates → rig-candidates → runtime-candidates 순차 승격
```

---

## Phase 3: 텍스처 매핑 & 카툰 렌더링

**목표:** Rodin에서 생성된 GLB 메시에 Phase 1-B에서 생성한 카툰 텍스처 아틀라스를 UV 매핑한다.

### 3-A. 텍스처 적용 스크립트

```bash
python3 scripts/apply-cartoon-texture-blender.py \
  --glb _workspace/<run-id>/engineering/asset-pipeline/rodin-candidates/<character-id>.glb \
  --texture _workspace/<run-id>/engineering/asset-pipeline/concept-input/<character-id>-cartoon-albedo.png \
  --asset-id <character-id> \
  --out _workspace/<run-id>/engineering/asset-pipeline/texture-candidates/glb/<character-id>.glb \
  --report _workspace/<run-id>/engineering/asset-pipeline/texture-candidates/reports/<character-id>.json
```

**스크립트 계약:**
- 원본 GLB 보존 (읽기 전용)
- Body material만 수정
- Active UV 좌표를 이미지 텍스처에 명시적으로 연결
- UV map이 없으면 실패
- Terrain/pedestal/weapon 메시는 건드리지 않음

### 3-B. Per-Character Albedo Bake

모든 캐릭터에 대해 **메시가 실제로 사용하는 UV 언랩 위에 직접 굽는다**. 공유 detail tile × 캐릭터별 `baseColorFactor`를 사용하던 이전 방식을 대체한다.

```bash
# Candidate lane에 굽기
python3 scripts/bake-character-albedo.py

# 재실행 결과가 바이트 동일한지 검증
python3 scripts/bake-character-albedo.py --check

# 런타임 lane 승격 + provenance 기록
python3 scripts/promote-character-assets.py
```

**Albedo Bake 알고리즘:**
1. 삼각형을 UV 공간에 barycentric rasterize
2. 각 texel의 로컬 위치·노멀 확보
3. 기존 `baseColorFactor`를 mid anchor로 shadow/body/lit/rim 4-band 구성
4. 공유 detail tile을 낮은 진폭으로 곱해 표면 grain 유지
5. Island 바깥으로 12 texel dilation → 필터링/밉맵이 UV seam에서 배경을 샘플링하지 못하게 함
6. `baseColorFactor`는 `[1,1,1,1]`로 되돌려 이중 착색 방지

---

## Phase 4: 리깅(Rigging) & 스키닝

**목표:** T-Pose GLB 메시에 Blender deform-only skeleton을 리깅하고, 11개 액션 클립에 대한 동작 계약(authoring contract)을 구성한다.

### 4-A. 리깅 스크립트 실행

```bash
blender -b -P scripts/rig-character-asset-blender.py -- \
  --glb _workspace/<run-id>/engineering/asset-pipeline/texture-candidates/glb/<character-id>.glb \
  --asset-id <character-id> \
  --category commander \
  --rest-pose tpose \
  --out _workspace/<run-id>/engineering/asset-pipeline/rig-candidates/<character-id>.glb \
  --report _workspace/<run-id>/engineering/asset-pipeline/rig-candidates/<character-id>.json \
  --budgets-json _workspace/<run-id>/engineering/asset-pipeline/action-pipeline.json
```

### 4-B. 리깅 규칙

| 항목 | 조건 |
|------|------|
| Skeleton | Deform-only, Rigify 호환 `DEF-` 네이밍 |
| 스키닝 | Body-only, terrain/weapon/prop 이름 감사 후 제거 |
| Rest pose | T-Pose freeze |
| Action library | 11개 clip (아래 참조) |
| Weight 검증 | Weighted vertex counts, T-pose deviation ≤ 12° |
| UV 검증 | Active UV map 존재, `uvLayerCount` ≥ 1 |

### 4-C. 11개 Action Clip 정의

`scripts/build-motion-prompt-batch.py`가 각 액션에 대한 컨셉/리퍼런스 프롬프트를 생성한다.

```bash
python3 scripts/build-motion-prompt-batch.py \
  --asset-id <character-id> \
  --out _workspace/<run-id>/engineering/asset-pipeline/motion-prompts/<character-id>.json
```

**11개 Action Set (절대 계약):**

| # | Action | Intent | Loop | Signature Poses | Keyframe Budget |
|---|--------|--------|------|-----------------|-----------------|
| 1 | `idle` | Guarded breathing + readable weight shift | ✅ | contact → breath-in → breath-out → contact | 120 |
| 2 | `move` | Controlled advance with clear lead foot | ✅ | anticipation → left-step → right-step → settle | 72 |
| 3 | `run` | Urgent pursuit without losing silhouette | ✅ | push-off → left-stride → right-stride → brake | 84 |
| 4 | `hit` | Short readable impact reaction | ❌ | impact → recoil → recover | 54 |
| 5 | `bighit` | Heavy stagger with protected recovery | ❌ | wind-up → impact → stagger → brace → recover | 84 |
| 6 | `attack` | Disciplined close-range strike | ❌ | guard → wind-up → strike → follow-through → recover | 90 |
| 7 | `critical` | Signature extraction-finisher burst | ❌ | focus → charge → release → aftershock → recover | 72 |
| 8 | `avoid` | Compact lateral evade | ❌ | read → dip → clear → recenter | 42 |
| 9 | `defence` | Hold the line, communicate protected timing | ❌ | raise-guard → hold → absorb → lower-guard | 78 |
| 10 | `die` | Controlled collapse with readable terminal pose | ❌ | fail → stagger → kneel → fall → still | 72 |
| 11 | `show` | Concept-aligned materialize + command reveal | ❌ | still → rise → present → command → settle | 96 |

**Transition 규칙:**
- Cross-fade only across compatible contact/recovery boundaries
- Default cross-fade: 8 frames (at 60fps authoring / 30fps runtime)
- Min/Max: 2–18 frames

---

## Phase 5: 모션 애니메이션 — Motion Previs → Blender NLA

**목표:** 참고 영상(Reference Video)에서 포즈/카메라/모션 데이터를 추출하여 캐릭터 애니메이션에 적용한다.

### 5-A. Motion Previs Studio를 이용한 모션 분석

Motion Previs Studio v4를 통해 참고 영상의 포즈, 깊이, 카메라 모션을 추출한다.

#### 5-A-1. Previs 실행

```bash
# Motion Previs Studio dev 서버 실행
cd motion-previs-studio
npm run dev
```

#### 5-A-2. Previs 설정 파라미터

| 파라미터 | 값 | 설명 |
|---------|-----|------|
| Mode | `Actor motion` | 포즈 + 카메라 유지 |
| Control layers | pose, depth, reference | 추출할 레이어 |
| Trim | 0:00–0:05 | 분석할 클립 구간 |
| RANSAC threshold | 0.8 | 카메라 솔버 정밀도 |

#### 5-A-3. Export Bundle 구조

Motion Previs Studio의 export bundle은 다음 파일들을 포함한다:

```
bundle/
├── reference.mp4                    # 원본 참고 영상
├── depth.mp4                        # 정규화된 깊이 시각화
├── pose_high_contrast.mp4           # 포즈 시각화
├── openpose_pose.mp4                # OpenPose 오버레이
├── openpose_keypoints.json          # 프레임별 BODY_25 키포인트
├── camera_motion.json               # 카메라 pan/tilt/zoom/roll
├── blender_import_pose.py           # Blender 아마추어 임포트 스크립트
├── blender_import_camera.py         # 카메라 리그 임포트 스크립트
├── blender_import_scene.py          # 풀 씬 셋업 스크립트
└── bundle_manifest.json             # 메타데이터 + 체크섬
```

### 5-B. Blender NLA에서 애니메이션 Authoring

Motion Previs의 키포인트 데이터를 Blender로 가져와 캐릭터 리그에 적용한다.

#### 5-B-1. 모션 데이터 임포트

```bash
# Motion Previs에서 export한 Blender 스크립트 실행
blender _workspace/<run-id>/engineering/asset-pipeline/rig-candidates/<character-id>.blend \
  -P bundle/blender_import_pose.py

# 전체 씬 임포트 (장면 구성 필요한 경우)
blender <character-id>.blend -P bundle/blender_import_scene.py
```

#### 5-B-2. 전신 액션 Clip Authoring

`scripts/author-wholebody-clips-blender.py`로 상·하체가 모두 움직이는 clip을 생성한다.

```bash
blender -b -P scripts/author-wholebody-clips-blender.py -- \
  --blend _workspace/<run-id>/engineering/asset-pipeline/rig-candidates/<character-id>.blend \
  --asset-id <character-id> \
  --action-pipeline _workspace/<run-id>/engineering/asset-pipeline/action-pipeline.json \
  --out _workspace/<run-id>/engineering/asset-pipeline/wholebody-motion/<character-id>.glb \
  --report _workspace/<run-id>/engineering/asset-pipeline/wholebody-motion/<character-id>.json
```

#### 5-B-3. 저스트/하체 본드 스킨 (선택)

정적 하체(lower mesh)가 필요한 캐릭터는 `scripts/bind-static-lower-mesh.py`를 실행한다.

```bash
blender -b -P scripts/bind-static-lower-mesh.py -- \
  --character-glb <rigged-character>.glb \
  --pedestal-mesh <pedestal-mesh>.glb \
  --out _workspace/<run-id>/engineering/asset-pipeline/rigged-lower-mesh/<character-id>.glb
```

### 5-C. 모션 품질 QA

```bash
# Clip 트랙 인구 조사
node scripts/qa-clip-track-census.mjs

# Idle 트랙 프로브
node scripts/qa-idle-track-probe.mjs

# 모션 가독성 프로브
node scripts/qa-motion-probe.mjs

# 시각 검증 (Three.js 렌더링)
node scripts/qa-visual-verification.mjs
```

**QA 체크리스트:**
- [ ] 각 clip이 NLA 트랙에 존재하는가
- [ ] Clip 길이가 action-pipeline.json의 keyframe budget 내인가
- [ ] Idle loop가 부드럽게 연결되는가
- [ ] Transition cross-fade가 artifact 없이 동작하는가
- [ ] 모든 clip이 Three.js 런타임에서 재생 가능한가

---

## Phase 6: 오디오 — ElevenLabs SFX/BGM/TTS

**목표:** ElevenLabs API를 사용하여 게임에 필요한 모든 오디오 요소를 생성한다.

### 6-A. 환경 설정

`.env.game-audio` 파일 (절대 커밋 금지):
```bash
# ElevenLabs API credentials — NEVER COMMIT THIS FILE
ELEVENLABS_API_KEY=sk_...

# Character voice IDs (obtain from https://www.elevenlabs.io/app/voice-library)
NARRATOR_VOICE_ID=...
HARAM_VOICE_ID=...
MERI_VOICE_ID=...
RAEL_VOICE_ID=...

# Fallback if character-specific IDs unset
ELEVENLABS_VOICE_ID=...
```

### 6-B. 오디오 생성 파이프라인

```bash
# 1-10 스테이지 전체 오디오 생성
node scripts/generate-audio.mjs --stages 1-10

# 특정 스테이지만 생성
node scripts/generate-audio.mjs --stages 1,2,3 --force

# TTS(나레이션)만 생성
node scripts/generate-audio.mjs --only tts --stages 1-10

# 음성 목록 확인
node scripts/generate-audio.mjs --list-voices
```

### 6-C. 오디오 카테고리 및 프롬프트

#### 6-C-1. 배경음악 (BGM)

```text
"cinematic dark fantasy background music, brooding low brass, ambient choir,
cinematic tension loop for {scene_name}"
```

| 파라미터 | 값 |
|---------|-----|
| Model | `elevenlabs_text_to_sound_v2` |
| Duration | 12초 |
| Prompt Influence | 0.6 |

#### 6-C-2. 전투 효과음 (Combat SFX)

| SFX ID | Text Description | Duration | Model |
|--------|-----------------|----------|-------|
| `attack-windup-melee` | "Metal scrape, weapon draw from sheath, sharp whoosh" | 0.20s | text_to_sound_v2 |
| `weapon-fire-melee` | "Impact transient, sharp metal strike, blade-edge contact ping" | 0.055s | text_to_sound_v2 |
| `impact-hit-light` | "Soft flesh hit, muffled thud, air displacement puff" | 0.07s | text_to_sound_v2 |
| `impact-hit-heavy` | "Heavy armor impact, deep metallic clang, resonant shockwave" | 0.12s | text_to_sound_v2 |
| `critical-hit-burst` | "Explosive critical strike, glass shatter, electric crackle burst" | 0.18s | text_to_sound_v2 |
| `hitstop-flash` | "Time freeze stutter, low-frequency rumble, reality snap" | 0.08s | text_to_sound_v2 |

```bash
# 사운드 생성 (ElevenLabs Sound Generation API)
curl -X POST "https://api.elevenlabs.io/v1/sound-generation" \
  -H "xi-api-key: $ELEVENLABS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Heavy armor impact, deep metallic clang, resonant shockwave",
    "duration_seconds": 0.12,
    "prompt_influence": 0.35
  }' \
  --output assets/audio/elevenlabs/sfx/impact-hit-heavy.mp3
```

#### 6-C-3. 적 등장/패배 효과음

| SFX ID | Text Description | Duration | Model |
|--------|-----------------|----------|-------|
| `enemy-spawn-rusher` | "Rushing footsteps, rapid clattering, lightweight armor jangle" | 0.25s | text_to_sound_v2 |
| `enemy-spawn-boss` | "Earth-shaking arrival, colossal footstep, orchestral hit stinger" | 0.80s | text_to_sound_v2 |
| `enemy-defeated-generic` | "Final gasp, armor collapse, dissipating energy hiss" | 0.18s | text_to_sound_v2 |

#### 6-C-4. TTS 나레이션 (Character Voice)

캐릭터별 ElevenLabs TTS 설정:

| Character | Stability | Similarity Boost | Style | Speed | Tone |
|-----------|-----------|------------------|-------|-------|------|
| Narrator | 0.50 | 0.75 | 0.30 | 1.00 | "Detached mission briefing. Terse military broadcast, no emotional color." |
| Haram | 0.40 | 0.70 | 0.65 | 0.95 | "Gruff veteran defender. Weary resolve, low register." |
| Meri | 0.45 | 0.72 | 0.60 | 1.05 | "Focused striker. Sharp, decisive, clipped delivery." |
| Rael | 0.55 | 0.78 | 0.50 | 1.00 | "Analytical support caster. Measured, precise." |

```bash
# TTS 생성 (ElevenLabs Text-to-Speech API)
curl -X POST "https://api.elevenlabs.io/v1/text-to-speech/${VOICE_ID}?output_format=mp3_44100_96" \
  -H "xi-api-key: $ELEVENLABS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model_id": "eleven_multilingual_v2",
    "text": "오늘은 1단계 봉쇄선 전개가 시작됩니다.",
    "voice_settings": {
      "stability": 0.40,
      "similarity_boost": 0.70,
      "style": 0.65,
      "use_speaker_boost": true,
      "speed": 0.95
    }
  }' \
  --output assets/audio/elevenlabs/narration/stage01-opening.mp3
```

### 6-D. 출력 구조

```
assets/audio/elevenlabs/
├── narration/          # TTS 나레이션 (스토리)
│   ├── stage01-opening.mp3
│   └── stage02-arrival.mp3
├── sfx/                # 사운드 이펙트
│   ├── impact-hit-heavy.mp3
│   ├── weapon-fire-melee.mp3
│   └── enemy-spawn-boss.mp3
├── bgm/                # 배경음악
│   ├── stage01-battle.mp3
│   └── boss-battle.mp3
├── ambience/           # 분위기 음악
│   ├── stage01-ambience.mp3
│   └── stage02-ambience.mp3
└── combat/             # 전투 효과음
    ├── attack-windup-melee.mp3
    └── critical-hit-burst.mp3
```

### 6-E. 런타임 통합

`defense-audio.js`에서 기존 procedural 오디오와 ElevenLabs pre-baked SFX를 통합한다:

```javascript
// ex: defense-audio.js — ElevenLabs 통합 지점
// [TARGET] procedural oscillator 대신 pre-baked MP3 로드
// [TARGET] AUDIO_CUES에 elevenlabsSource 필드 추가
// [TARGET] 캐시 미스 시 procedural fallback
```

---

## Phase 7: 런타임 통합 (Three.js GLB 로딩 & 재생)

**목표:** 최종 rigged/animated GLB를 Three.js로 로드하고, 게임 상태 머신에 따라 애니메이션을 전환한다.

### 7-A. GLB 런타임 Lane 배포

```bash
# 모든 캐릭터 일괄 리깅
bash scripts/rig-all-characters.sh

# Albedo bake + 검증
python3 scripts/bake-character-albedo.py
python3 scripts/bake-character-albedo.py --check

# 런타임 lane 승격
python3 scripts/promote-character-assets.py
```

**배포 규칙:**
- Deployed lane: `assets/images/battle/glb/`
- Provenance: `assets/images/battle/glb/character-build-provenance.json`
- 기존 배포 GLB는 자동 덮어쓰기 금지

### 7-B. Three.js GLB 로딩 프롬프트 패턴

```javascript
// ex: battle-realtime-three.js — GLB 캐릭터 로딩 패턴
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const loader = new GLTFLoader();
const animations = {}; // name → AnimationClip map

function loadCharacter(glbPath, characterId) {
  return new Promise((resolve, reject) => {
    loader.load(glbPath, (gltf) => {
      const model = gltf.scene;
      const mixer = new THREE.AnimationMixer(model);

      // NLA clip 등록
      gltf.animations.forEach((clip) => {
        animations[clip.name] = clip;
      });

      // T-pose에서 idle로 초기 전환
      const idleAction = mixer.clipAction(animations['idle']);
      idleAction.play();

      resolve({ model, mixer, animations });
    }, undefined, reject);
  });
}
```

### 7-C. 상태 머신 기반 애니메이션 전환

```javascript
// 전투 FSM과 애니메이션 동기화
const ACTION_TO_CLIP = {
  IDLE:     'idle',
  MOVING:   'move',
  RUNNING:  'run',
  ATTACK:   'attack',
  HIT:      'hit',
  BIGHIT:   'bighit',
  CRITICAL: 'critical',
  AVOID:    'avoid',
  DEFENCE:  'defence',
  DEAD:     'die',
  SHOW:     'show',
};

function transitionAnimation(mixer, fromClip, toClip, crossFadeDuration = 8) {
  const from = mixer.clipAction(fromClip);
  const to = mixer.clipAction(toClip);

  to.reset().play();
  to.crossFadeFrom(from, crossFadeDuration / 60, true); // 60fps authoring
  from.stop();
}

// Combat FSM tick과 연동
// (combat-motion-fsm-spec.md:370-387 event emission ticks)
```

### 7-D. 애니메이션 이벤트 → SFX 동기화

```javascript
// Tick 기반 SFX 트리거 (24-tick attack cycle @ 60fps → 30fps runtime)
const SFX_TIMING = {
  ATTACK_WINDUP_START: { tick: 0,  sfx: 'attack-windup-melee' },
  WEAPON_FIRED:        { tick: 12, sfx: 'weapon-fire-melee' },
  IMPACT_CONTACT:      { tick: 13, sfx: 'impact-hit-heavy' },
  CRITICAL_HIT:        { tick: 14, sfx: 'critical-hit-burst' },
};

function onAnimationTick(currentTick, sfxPlayer) {
  const timing = SFX_TIMING[currentTick];
  if (timing) {
    sfxPlayer.play(timing.sfx);
  }
}
```

### 7-E. GLB+Canvas Fallback 검증

```bash
# GLB validator
npx gltf-validator assets/images/battle/glb/<character-id>.glb

# Three.js 및 Canvas fallback 검증
node --test tests/defense-renderer-contract.test.mjs

# 브라우저 검증
node tests/defense-survivor-browser.cjs
```

---

## 검증 체크리스트

### Phase 1 — 컨셉 → T-Pose 이미지

| Check | Required Evidence | Method |
|-------|------------------|--------|
| T-Pose refstyle 생성 완료 | `assets/images/battle/pilot/<id>-tpose-refstyle.png` | gti CLI |
| UV 아틀라스 생성 완료 | concept-input 디렉토리 이미지 | gti CLI |
| Blockout mesh 생성 완료 | `tpose-conditions/<id>-tpose-condition.glb` | Blender script |

### Phase 2 — 3D 메시 생성 (Rodin)

| Check | Required Evidence | Method |
|-------|------------------|--------|
| Positive/negative prompt 기록 | generated plan `promptContract` | `rodin-tpose-regen.py` |
| T-pose condition 저장 | `tpose-conditions/<id>-tpose-condition.glb` | 파일 존재 |
| No terrain/pedestal | visual audit + object/mesh report | 육안 검증 |
| No weapon/held prop | visual audit + object/mesh report | 육안 검증 |
| Provenance JSON | adjacent `.provenance.json` | `runtimeEligible: false` |

### Phase 3 — 텍스처 매핑

| Check | Required Evidence | Method |
|-------|------------------|--------|
| Cartoon texture provenance | image + adjacent provenance JSON | concept-input |
| Active UV mapping | Blender report `activeUvMap`, `uvLayerCount` | `apply-cartoon-texture-blender.py` |
| Per-character albedo bake | `character-build-provenance.json` `albedoBake` | `bake-character-albedo.py --check` |
| UV seam padding | 12-texel dilation | `bake-character-albedo.py` |

### Phase 4 — 리깅

| Check | Required Evidence | Method |
|-------|------------------|--------|
| Body-only skinning | rig report, weighted vertex counts | `rig-character-asset-blender.py` |
| T-pose deviation ≤ 12° | rig report | `rig-character-asset-blender.py` |
| 11 action clips present | NLA/GLB clip census | `qa-clip-track-census.mjs` |

### Phase 5 — 모션 애니메이션

| Check | Required Evidence | Method |
|-------|------------------|--------|
| Previs pose 추출 완료 | `openpose_keypoints.json` | Motion Previs Studio |
| Previs camera 추출 완료 | `camera_motion.json` | Motion Previs Studio |
| 모든 clip NLA 트랙 존재 | clip census | `qa-clip-track-census.mjs` |
| Transition cross-fade 동작 | visual verification | Three.js preview |
| Idle loop seamless | frame edge probe | `qa-idle-track-probe.mjs` |

### Phase 6 — 오디오 (ElevenLabs)

| Check | Required Evidence | Method |
|-------|------------------|--------|
| API key 설정 완료 | `.env.game-audio` 존재 + 유효 | `--list-voices` |
| TTS 생성 완료 | `assets/audio/elevenlabs/narration/*.mp3` | `generate-audio.mjs` |
| SFX 생성 완료 | `assets/audio/elevenlabs/sfx/*.mp3` | `generate-audio.mjs` |
| BGM 생성 완료 | `assets/audio/elevenlabs/bgm/*.mp3` | `generate-audio.mjs` |
| Fallback procedural 오디오 | `defense-audio.js` oscillator | 트리거 테스트 |

### Phase 7 — 런타임 통합

| Check | Required Evidence | Method |
|-------|------------------|--------|
| GLB validator 통과 | validator report | `npx gltf-validator` |
| Three.js 로딩 성공 | browser console | `battle-realtime-three.js` |
| Canvas fallback 동작 | browser test | `tests/defense-renderer-contract.test.mjs` |
| 11개 애니메이션 전환 | state machine test | `tests/defense-run-simulation.test.mjs` |
| SFX 타이밍 일치 | tick-aligned sfx play | combat FSM 테스트 |
| Provenance 해시 일치 | `character-build-provenance.json` | `promote-character-assets.py` |

---

## 워크플로우 다이어그램

```text
┌─────────────────────────────────────────────────────────────────────┐
│ Phase 1: Concept → T-Pose                                          │
│                                                                     │
│  [Concept Art] ──gti──► [T-Pose Refstyle PNG]                      │
│  [T-Pose Ref] ──Blender──► [T-Pose Blockout GLB] (BoundingBox)     │
│  [gti] ────────► [UV Atlas Texture PNG]                            │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Phase 2: Rodin 3D Mesh Generation                                   │
│                                                                     │
│  [T-Pose Blockout GLB] ──Rodin Bridge──► [Raw T-Pose Mesh GLB]     │
│  [Positive/Negative Prompt Contract]                                 │
│      ↓ 육안 검증 (terrain/weapon 유무)                                │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Phase 3: Texture Mapping + Albedo Bake                              │
│                                                                     │
│  [Raw Mesh GLB] + [UV Atlas PNG]                                    │
│      ──apply-cartoon-texture-blender.py──► [Textured GLB]           │
│      ──bake-character-albedo.py──► [Albedo-baked GLB]               │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Phase 4: Rigging + Skinning                                        │
│                                                                     │
│  [Textured GLB] ──rig-character-asset-blender.py──► [Rigged GLB]   │
│  │ - Deform skeleton (Rigify DEF-)                                  │
│  │ - 11 action clips                                                │
│  │ - Body-only skinning                                             │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Phase 5: Motion Animation                                           │
│                                                                     │
│  [Reference Video] ──Motion Previs Studio──► [Keypoints + Camera]   │
│       ──blender_import_pose.py──► [Blender NLA Tracks]              │
│       ──author-wholebody-clips-blender.py──► [Animated GLB]         │
│       ──qa-clip-track-census.mjs──► [Verification Report]           │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Phase 6: Audio (ElevenLabs)                                         │
│                                                                     │
│  [Sound Plan JSON] ──generate-audio.mjs──► assets/audio/elevenlabs/ │
│  │ - TTS (narration, npc voices)                                    │
│  │ - SFX (combat, enemy, UI events)                                 │
│  │ - BGM (stage, boss, ambience)                                    │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Phase 7: Runtime Integration (Three.js)                             │
│                                                                     │
│  [Animated GLB] ──promote-character-assets.py──► assets/glb/        │
│  [Audio MP3s] ──defense-audio.js──► 게임 내 오디오 큐               │
│  [Action Pipeline JSON] ──battle-realtime-three.js──► FSM 연동      │
│                                                                     │
│  Three.js: GLTFLoader → AnimationMixer → State Machine              │
│  Canvas: fallback adapter (battle-visualizer.js)                    │
│  Audio: Howler.js / Web Audio API → ElevenLabs pre-baked MP3       │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 참고 자료

### 스크립트 인덱스

| 스크립트 | 용도 |
|---------|------|
| `scripts/tpose_blockout.py` | T-Pose Blockout Mesh 생성 (Rodin condition) |
| `scripts/rodin-tpose-regen.py` | Rodin Bridge 제출/계획 생성 |
| `scripts/apply-cartoon-texture-blender.py` | 텍스처 UV 매핑 |
| `scripts/bake-character-albedo.py` | Per-character albedo bake |
| `scripts/promote-character-assets.py` | 런타임 GLB 승격 |
| `scripts/rig-character-asset-blender.py` | 리깅 + 11액션 clip 구성 |
| `scripts/build-motion-prompt-batch.py` | 액션별 모션 프롬프트 배치 생성 |
| `scripts/author-wholebody-clips-blender.py` | 전신 모션 clip authoring |
| `scripts/bind-static-lower-mesh.py` | 정적 하체 본드 스킨 |
| `scripts/generate-audio.mjs` | ElevenLabs 오디오 생성 파이프라인 |
| `scripts/validate-asset-lanes.py` | Asset lane 정책 검증 |

### 외부 툴 참조

- [Hyper3D Rodin](https://hyper3d.ai/) — 3D mesh generation
- [god-tibo-imagen](https://github.com/NomaDamas/god-tibo-imagen) — AI image generation
- [Motion Previs Studio](https://github.com/wassermanproductions/motion-previs-studio) — Pose/camera extraction
- [ElevenLabs API](https://elevenlabs.io/docs) — Text-to-Speech, Sound Generation
- [Blender Rigify](https://github.com/blender/blender-addons/tree/main/rigify) — Rigging addon
- [Khronos glTF](https://www.khronos.org/gltf/) — Runtime 3D format
- [glTF Validator](https://github.com/KhronosGroup/glTF-Validator) — GLB validation
- [Three.js GLTFLoader](https://threejs.org/docs/#examples/en/loaders/GLTFLoader) — WebGL runtime loading

### 문서 참조

- [Character Asset Pipeline (기존)](docs/character-asset-pipeline.md) — 캐릭터 에셋 파이프라인 상세
- [Defense Survivor Design](docs/abyssal-command-defense-survivor-design.md) — 게임 설계 문서
- [Production Cycle](docs/abyssal-surge-production-cycle.md) — 제작 사이클
- [ElevenLabs Audio Pipeline Spec](_workspace/20260726-stage1b-cinder-pressure-agency/engineering/elevenlabs-audio-pipeline-spec.md) — 오디오 파이프라인 상세
- [Subculture Toon Pipeline](_workspace/20260726-stage1b-cinder-pressure-agency/engineering/subculture-toon-pipeline-index.md) — NPR/Toon 렌더링 파이프라인
