# 자산 레인 리포트 — W12 (BGM/SFX) + W13 (캐릭터) + W14 (무기) + W15/W16 (추가 승인분)

**날짜**: 2026-08-07 · **레인**: asset-lane · **시드**: `_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md` §5/§8 (FROZEN, D7/D8) + 오케스트레이터 추가 승인(task #6)

`git tag -f pre-asset-lane-20260807` 생성 후 시작. 다른 레인(sim/ui/vfx)이 동시에
`Assets/Scripts/**`, `Assets/Editor/**`, `Assets/Scenes/**`를 수정 중임을 `git status`로
확인했고, 이 레인은 그 파일들을 전혀 건드리지 않았다 — 아래 산출물은 전부 신규 파일이거나
`tools/audio/gen_sfx.py` 1건의 비파괴적 확장(신규 cue 2종 추가)뿐이다.

## W12 — BGM + SFX

### SFX 4종 [OBSERVED]
`tools/audio/gen_sfx.py`(ElevenLabs sound-generation, 22 s 상한, 기존 도구)에 신규 cue
2종을 추가하고 실행:

```bash
python3 tools/audio/gen_sfx.py click footstep
```

| SFX | 상태 | 파일 |
|---|---|---|
| 버튼 클릭 | **신규 생성** | `Assets/Resources/Audio/cue-click.mp3` (8821 bytes, 0.5s) |
| 발자국 | **신규 생성** | `Assets/Resources/Audio/cue-footstep.mp3` (8821 bytes, 0.5s) |
| 아이템 획득 | 기존 재사용 | `Assets/Resources/Audio/cue-pickup.mp3` (이미 존재, `AudioDirector.cs:57`에서 로드 중) |
| 공격 스윙 | 기존 재사용 | `Assets/Resources/Audio/cue-strike.mp3` (이미 존재, `AudioDirector.cs:52`에서 로드 중) |

`docs/provenance/audio.json`에 click/footstep 프롬프트·바이트수 기록 완료(기존 8종 항목 보존).
API 키는 `ELEVENLABS_API_KEY` env로 리포지토리 루트 `.env.game-audio`(gitignored)에서 로드,
커밋/로그에 노출하지 않았다.

### BGM 세트 [OBSERVED]
D7 결정: 기존 sound-generation 22 s 상한은 BGM에 부적합 → **ElevenLabs Music API
(`POST https://api.elevenlabs.io/v1/music`) 신규 경로**를 `tools/audio/gen_bgm.py`로
구현. WebSearch/WebFetch로 2026-08 기준 공식 API 레퍼런스(`compose` 엔드포인트, 요청
필드 `prompt`/`music_length_ms`(3000–600000)/`force_instrumental`, 응답
`application/octet-stream` raw audio)를 확인 후 작성. `gen_sfx.py`와 동일한 키 해석
순서(`$ELEVENLABS_API_KEY` → `../Abyssal-Surge/.env.game-audio`)를 재사용.

```bash
python3 tools/audio/gen_bgm.py intro lobby loading stage
```

| 트랙 | 길이 | 파일 | 바이트 |
|---|---|---|---|
| 인트로 | 20000 ms | `Assets/Resources/Audio/bgm-intro.mp3` | 321,037 |
| 로비 | 45000 ms | `Assets/Resources/Audio/bgm-lobby.mp3` | 720,606 |
| 로딩 | 15000 ms | `Assets/Resources/Audio/bgm-loading.mp3` | 239,953 |
| 스테이지 | 60000 ms | `Assets/Resources/Audio/bgm-stage.mp3` | 960,515 |

프롬프트/엔드포인트/`force_instrumental:true`(보이스 금지, 2026-08-04 사용자 지시 준수)
전량 `docs/provenance/bgm.json`에 기록. API 실패 없음 — Abyssal-Surge
`audio/elevenlabs/loops/` 폴백 조사는 **불필요**했다(4트랙 전부 1차 시도 성공).

**블로커**: 없음.

**코드 연동 필요 항목** (Assets/Scripts 미수정, 다음 레인/세션 몫):
- `Assets/Scripts/View/AudioDirector.cs`는 현재 `Audio/cue-bgm` 단일 루프만 로드(`:62`).
  스테이지 흐름별(`Audio/bgm-intro|lobby|loading|stage`) 전환 로직이 없다 — `GameDirector.State`
  전이(`EnterLobby`/던전 진입/로딩 컷씬)에 맞춰 BGM 트랙을 바꾸는 배선이 필요.
- `Audio/cue-click`, `Audio/cue-footstep`도 `AudioDirector.OnEvents`에 매핑되어 있지 않다
  (버튼 클릭·이동 tick 이벤트가 sim에 없을 수 있음 — UI 레인 확인 필요).

## W13 — 캐릭터 GLB → FBX 파이프라인

### Manifest 확인 [OBSERVED]
`/Users/jangyoung/orca/Abyssal-Surge/assets/defense-asset-manifest.json`(읽기 전용, 수정 없음)
파이썬으로 조회:

- `assets/mesh/character/human-command-boss-character/**` 전 파일:
  `disposition:delete`, `runtimeReference:false` → **사용 금지**.
- `assets/mesh/character/broken-court-monarch-boss-character/**` 전 파일: 동일하게
  `delete`/`false` → **사용 금지**.
- 유일한 retain/`runtimeReference:true` 행: `assets/motion/ingame/characters/human-command-boss/model.glb`
  와 `assets/motion/ingame/characters/broken-court-monarch-boss/model.glb`
  (각 옆 `manifest.json`도 retain).

### broken-court-monarch-boss — 이미 완료 [OBSERVED]
`Assets/Art/Characters/broken-court-monarch-boss.fbx`(8.1 MB, `_workspace/current/engineering/reskin/broken-court-monarch-boss.json` 2026-08-04 생성)와
`Assets/Resources/Characters/broken-court-monarch-boss.prefab`이 **이전 세션에서 이미
리스킨·임포트 완료**되어 있음을 확인. 이번 사이클에서는 재작업 불필요
(시드 §8 "이미 임포트됨, 재임포트 불필요" 기록과 일치).

### human-command-boss — 신규 실행 [OBSERVED]
`CharacterRoster.cs:8-18`에 `human-command-boss`가 없음을 확인(시드 W13 관측과 일치).
기존 파이프라인(`tools/blender/reskin_character.py`, `docs/character-asset-pipeline.md`
동일 계열 — mixamo 표준 스켈레톤 자동웨이트 재바인딩)을 그대로 사용, mesh-glb swap은
불필요(retain된 model.glb가 이미 semantic node 이름을 가진 authored mesh를 내장 —
`reskin_all.sh`가 blockout으로 분류한 scout/shade/possessed류와 다름):

```bash
blender -b --factory-startup --python-exit-code 1 \
  -P tools/blender/reskin_character.py -- \
  --glb /Users/jangyoung/orca/Abyssal-Surge/assets/motion/ingame/characters/human-command-boss/model.glb \
  --out Assets/Art/Characters/human-command-boss.fbx \
  --report _workspace/current/engineering/reskin/human-command-boss.json \
  --max-tris 25000
```

**검증 수치** (`_workspace/current/engineering/reskin/human-command-boss.json`):

| 항목 | 값 | 계약 |
|---|---|---|
| finalTriCount | 7,711 | ≤25,000 (CLAUDE.md §1) ✅ |
| 텍스처 | 3종, 전부 1024×1024 | ≤1024 (CLAUDE.md §1) ✅ |
| heatOrphans / rigidResidual | 0 / 0 | 본-히트 스키닝 100% 해결 |
| nonNormalizedVertices | 0 | 가중치 정규화 정상 |
| removedBones | `DEF-pelvis.L`, `DEF-pelvis.R` | 매핑 안 된 본 제거(계약대로) |
| bones | 22 (Unity Humanoid 표준명) | `BONE_MAP` 전체 매핑 |

출력: `Assets/Art/Characters/human-command-boss.fbx` (5.8 MB). Provenance:
`docs/provenance/human-command-boss-reskin.json`.

**블로커**: 없음(리스킨 단계).

**코드 연동 필요 항목** (Unity 에디터 실행 금지 — 프로젝트 락, `tools/unity_batch.sh` 배치모드
자동화만 허용되는데 이번 작업은 배치모드 실행 대상이 아니므로 스킵):
- `Assets/Art/Characters/human-command-boss.fbx`는 아직 `.meta` 없음(미임포트) — Unity가
  열릴 때 자동 임포트되지만, Humanoid 아바타 설정·`Assets/Resources/Characters/human-command-boss.prefab`
  생성(broken-court-monarch-boss.prefab과 동일 패턴)은 사람/다음 세션 몫.
- `Assets/Scripts/Sim/CharacterRoster.cs`의 `AllIds`에 `"human-command-boss"` 추가 필요
  (현재 8종에 없음) — sim 레인 코드 변경 사항이라 이 레인에서 건드리지 않음.
- 플레이어 배정(`human-command-boss` = 플레이어) 배선은 `GameBootstrap.cs`/`GameView.cs`
  쪽 로직 확인 필요(현재 플레이어 prefab은 `Resources.Load<GameObject>("Characters/lantern-reaver")`
  하드코딩, `GameBootstrap.cs:22`).

## W14 — 무기 외형 3종 (단검·활·해머)

### 배경 [OBSERVED]
기존 `docs/provenance/weapon-reskin.json`(2026-08-06)이 "image→3D 툴 부재 + Abyssal-Surge에
단검/활/해머 소스 메시 없음"을 블로커로 기록해둔 상태였다. D8(시드 §5, 2026-08-07 동결)이
이 블로커를 **Blender 절차적 저폴리 오써링**으로 해소하도록 확정 — 신규 스크립트
`tools/blender/gen_weapon_props.py` 작성(기존 `convert_equip_props.py`의 소켓 공간·
tier 컬러 컨벤션 재사용: basic=차콜, fine=1.22배 스케일+엠버 emissive).

```bash
blender -b --factory-startup --python-exit-code 1 \
  -P tools/blender/gen_weapon_props.py -- --outdir Assets/Art/Props
```

**검증 수치** (스크립트 출력, tri 예산 800/mesh):

| 자산 | tri | 상태 |
|---|---|---|
| `equip-weapon-dagger-basic.fbx` | 46 | ✅ |
| `equip-weapon-dagger-fine.fbx` | 46 | ✅ |
| `equip-weapon-bow-basic.fbx` | 200 | ✅ |
| `equip-weapon-bow-fine.fbx` | 200 | ✅ |
| `equip-weapon-hammer-basic.fbx` | 64 | ✅ |
| `equip-weapon-hammer-fine.fbx` | 64 | ✅ |

전부 `Assets/Art/Props/`에 배치, 총 620 tri(예산 6×800=4800 대비 대폭 여유).
Provenance: `docs/provenance/weapon-props-procedural.json`.

기존 배치된 `Resources/Props/equip-weapon-{basic,fine}.fbx/.mat/.prefab`(이미 임포트·
프리팹화됨)을 대조해 네이밍·위치 규약을 확인:
`Assets/Resources/Props/equip-weapon-{basic,fine}.prefab` — FBX 임포트 + 머티리얼
(`equip-weapon-{basic,fine}.mat`, m_CastShadows:0) + 프리팹 래핑 3단계로 구성되어 있었다.

**블로커**: 없음(메시 생성 단계).

**코드 연동 필요 항목** (Unity 에디터 실행 금지 동일 사유로 스킵):
- 신규 6개 FBX는 `.meta`/`.mat`/`.prefab` 미생성. 기존
  `Assets/Resources/Props/equip-weapon-{basic,fine}.mat/.prefab`을 템플릿으로 임포트 후
  머티리얼·섀도우 설정(`m_CastShadows:0`) 복제 필요.
- `Assets/Scripts/View/ActorView.cs:392-393` `AttachEquipProps`가 현재
  `Resources/Props/equip-weapon-{basic,fine}`만 로드(아키타입 차원 없음) — 3종 중 어느
  아키타입을 로드할지 결정하는 selector가 없다. `docs/provenance/weapon-reskin.json`이
  이미 동일 갭을 기록해둔 것과 같은 항목이며, 이번 세션에서도 미해결(코드 변경 필요).

## W15 — 잿불 휴식(Ember Rest) 팝업 배경 일러스트

### 실행 [OBSERVED]
`gti --dry-run` 선행 후 `gti --provider codex-cli`(private-codex는 이 머신에서 지속 HTTP
429 — `docs/provenance/scene-synopsis-art.json`/`env-stage-textures.md`에 이미 기록된
사실이라 재시도하지 않고 codex-cli 직행)로 생성:

```bash
gti --provider codex-cli --prompt "Cinematic dark fantasy 2.5D beat-em-up interlude scene: Ember Rest preparation chamber, 3/4 ground-level camera, a single floor lantern igniting warm ember-orange light at the center of a charcoal-stone chamber, faint spectral-cyan glyphs glowing on the walls, a dim archway door to the next room barely visible in the background shadow, quiet contemplative mood, painterly game concept art, no text, no logo, no UI, no characters" \
  --output Assets/Resources/Scenes/scene-ember-rest.png
```

기존 UI 스프라이트 규약 확인: `Assets/Resources/Scenes/scene-{intro,stage-entry,transition,
boss-entry}.png` 4종이 이미 `CutsceneView.cs:54`의 `Resources.Load<Sprite>("Scenes/" + name)`
패턴을 쓰고 있어 동일 위치·네이밍(`scene-ember-rest.png`)으로 배치.

**텍스처 예산 확인/리사이즈** [OBSERVED]: 생성 직후 1536×1024(2,183,861 bytes) — 기존
scene-*.png 4종과 동일한 gti 기본 출력 크기지만, 4종은 이미 Unity에 임포트되어
`.meta`에 `maxTextureSize: 1024`가 박혀 있는 반면 신규 파일은 아직 임포트 전이라 그
안전장치가 없다. 오케스트레이터 지시(2026-08-07)에 따라 원본 파일 자체를 1024×683으로
리사이즈(LANCZOS, 비율 유지) — 965,148 bytes. Unity 임포트 이후 별도 클램프 불필요.

Provenance: `docs/provenance/scene-ember-rest.json` (프롬프트·해상도 변경 이력·follow-up 전부 기록).

### 최종 매칭 라운드 (2026-08-08) [OBSERVED] — ui-lane 정본 계약과 대조
ui-lane이 실제로 읽는 파일은 `scene-ember-rest.png`가 아니라 별도 경로였다. 두 자산을
모두 유지하되 역할을 분리했다:

| 자산 | 경로 | 역할 | 해상도 |
|---|---|---|---|
| 컷씬 마스터 | `Assets/Resources/Scenes/scene-ember-rest.png` | `CutsceneView.cs:54` 컷씬 규약 위치(형제 4종과 동일 패밀리). 오케스트레이터 지시로 **유지**(중복 아님) | 1024×683 |
| **HUD 배경(실제 소비 자산)** | **`Assets/Resources/Icons/ui-ember-rest-bg.png`** | `HudView.BuildEmberRestPanel`이 실제로 로드할 스프라이트. 패널(620×420, 종횡비 1.4762)에 정확히 맞춘 파생본 | **1024×694** |

`ui-ember-rest-bg.png`는 재생성 없이 기존 `scene-ember-rest.png`(1024×683)에서 파생:
① 종횡비 맞춤(LANCZOS 업스케일 1.6% + 폭 센터크롭 1024), ② **중앙~하단 평탄화** — 오퍼
카드 3장(`HudView.cs` offer 오프셋 y≈-88..-216)과 버튼 2개(y≈18..110)가 얹히는 하단
~60% 구간을 패널 자체의 배경색(`Color(0.02,0.05,0.06)`)으로 부드럽게 페이드(40%~80%
지점, x^1.6 이징 + 0.6px 가우시안 블러로 이음새 완화), 상단 ~40%는 아치·시안 문양을
분위기용으로 유지. 육안 검토 완료(이음새 없음, 하단 평탄 확인).

**`.meta` 수기 작성** [OBSERVED]: Unity 미실행이라 ui-lane 지시대로 기존 `Icons/`
스프라이트(`hud-meters-panel-bg.png.meta`)를 템플릿으로 복사, `textureType: 8`(Sprite),
`spriteMode: 1`(Single) 확인 유지, spriteBorder는 9-slice가 아니므로 0,0,0,0으로 조정,
신규 GUID(`c3eeb10907f74160aebb56debcc53c12`) 발급 — Assets/ 전체 `.meta` 대상 충돌
검사 완료(중복 없음). Unity 에디터가 실제로 프로젝트를 열 때까지 검증 미완이라 follow-up
표기.

Provenance 갱신: `docs/provenance/scene-ember-rest.json`(두 자산 역할 분리·파생 과정·
.meta 근거 전부 기록).

### 코드 연동 필요 항목
`Assets/Scripts/View/HudView.cs` `BuildEmberRestPanel`(:825-864)은 현재 배경이 단색
`Image`(620×420, `Color(0.02,0.05,0.06,0.96)`)뿐이고 스프라이트를 전혀 로드하지 않는다 —
`Resources.Load<Sprite>("Icons/ui-ember-rest-bg")`를 읽어오는 `Image` 컴포넌트를 패널
배경에 추가하는 코드 변경이 필요(ui-lane 몫, 기존 자산의 단순 교체가 아니라 신규 연동
지점).

## W16 — 지형 애니메이션 플립북 스프라이트 시트 3종

**리비전 노트 [OBSERVED]**: vfx-lane이 소비 코드를 완성하며 계약을 확정했고(오케스트레이터
경유 전달), 최초 산출물(rev1: `Assets/Resources/Textures/Env/`, 컬러)이 그 계약과
불일치해 아래로 교체했다.

| 항목 | rev1(폐기) | rev2(확정, 현재) |
|---|---|---|
| 경로 | `Assets/Resources/Textures/Env/terrain-fx-*.png` | **`Assets/Resources/Terrain/terrain-fx-*.png`** |
| 픽셀 포맷 | RGB 컬러 | **8-bit 그레이스케일(PNG mode L), 색 없음** |
| shift 시트 | 포함 | 포함 (양쪽 리비전 모두) |
| 그리드/루프 | 4×4, row-major, 완전 루프 | 동일 (변경 없음) |

rev1 3파일(`Assets/Resources/Textures/Env/terrain-fx-{lava,ice,shift}-sheet.png`)은 **삭제**했다
(vfx-lane 코드가 그 경로를 로드하지 않아 남겨둘 이유 없음 — `docs/provenance/terrain-fx-sheets.json`
`deletedSuperseded`에 기록).

**그레이스케일 변환**: 기존 컬러 hero 텍스처(용암/빙결/조류 3종, 변경 없이 재사용)를
`tools/gen_terrain_fx_sheets.py`의 `load_pattern_base()`에서 픽셀별 `max(R,G,B)` →
0-255 대비 스트레치로 변환. 표준 루미넌스(`0.299R+0.587G+0.114B`) 대신 max-channel을
쓴 이유: 루미넌스는 파란 채널 가중치(0.114)가 낮아 ice/shift 테마의 시안 발광 요소가
lava의 주황 발광 요소보다 상대적으로 어둡게 죽는다 — max-channel은 색상과 무관하게
"이 텍셀에서 가장 밝은 채널이 얼마나 밝은가"를 균일하게 잡아내 3테마 모두 일관된
"발광부 vs 어두운 바탕" 대비를 만든다. 변환 후 3개 시트를 전부 육안 검토(Read 이미지
프리뷰) — lava는 어두운 바탕에 밝은 균열선, ice는 결정 텍스처 위 명확한 대각선 스윕
밴드, shift는 명확한 수직 드리프트 밴드로 확인, **재생성 불필요 판단**(품질 충분).

애니메이션 변환 로직(펄스/롤/스윕) 자체는 컬러 버전과 동일한 위상 파라미터화를 그대로
그레이스케일 numpy 배열 연산으로 재작성했을 뿐 — 그리드·루프 계약은 변경 없음.

### 도구 선택 [OBSERVED]
지시된 1순위 도구 `ppgen`(perfectpixel) 확인: `which ppgen` → 미설치. 이전 세션도 동일하게
기록해둔 사실(`docs/provenance/scene-synopsis-art.json:39`)이라 재조사 없이 폴백 진행.
**폴백**: god-tibo-imagen(gti)으로 테마당 seamless-tileable "hero" 텍스처 1장만 생성하고,
전체 N×N 그리드를 gti에 한 번에 그리게 하지 않았다 — 생성 모델이 균일 정렬 그리드를
정확히 그리는 것은 신뢰할 수 없다는 선례가 이미 이 저장소에 있음
(`docs/character-asset-pipeline.md:99`, v2 아틀라스가 생성된 라벨 텍스트 때문에 반려됨).
대신 신규 `tools/gen_terrain_fx_sheets.py`(PIL)로 hero 텍스처 위에 테마별 결정론적
애니메이션 변환을 적용해 프레임을 조립 — 정렬 오차 없음, 루프도 위상(phase)이 프레임
0에서 정확히 닫히도록 수학적으로 보장.

```bash
gti --provider codex-cli --prompt "<테마별 프롬프트>" --output _workspace/current/engineering/icons/terrain-fx-hero/<theme>-hero.png
python3 tools/gen_terrain_fx_sheets.py --hero <hero.png> --theme {lava,ice,shift} \
  --out Assets/Resources/Terrain/terrain-fx-<theme>-sheet.png
```

### 그리드 스펙 (vfx-lane 소비 계약, 확정) [OBSERVED]

| 항목 | 값 |
|---|---|
| 경로 | **`Assets/Resources/Terrain/terrain-fx-{lava,ice,shift}-sheet.png`** |
| 그리드 | **4×4** (16프레임) |
| 시트 크기 | 1024×1024 px (WebGL ≤1024 상한, CLAUDE.md §1 충족 — 리사이즈 불필요, 4×256=1024로 애초에 정확히 생성됨) |
| 프레임 크기 | 256×256 px |
| 프레임 순서 | row-major, index = row×4 + col, frame 0 = 좌상단 |
| 픽셀 포맷 | 8-bit 그레이스케일(PNG mode L), 색 없음 |
| 루프 | **완전 루프.** 각 테마 변환이 `phase = frame_index / 16`(또는 사인 기반 위상)로 파라미터화되어 frame15→frame0 전환이 frame N→N+1과 동일하게 이어짐. 별도의 "16번째 = 프레임0 복제" 프레임 없음 — 재생 시 index 15에서 바로 0으로 순환 |
| 임포트 | wrapMode **Clamp**(`wrapU/V/W: 1`) — **적용 완료**. Unity 미실행이라 기존 `abyss-chancel-floor.png.meta`를 템플릿으로 손수 `.meta` 3종 작성(wrapMode만 Repeat→Clamp로 변경, `textureType:0` 유지, `maxTextureSize:1024`), GUID 3종 신규 발급·충돌검사 완료. Editor 미검증이라 follow-up으로 표기(에디터 오픈 시 재확인 권장) |

### 테마-스테이지 매칭 조사 [OBSERVED]
`tools/gen_env_textures.sh`의 `STAGES` 테이블(§per-stage concept clause 원본)과
`Assets/Scripts/View/StageCatalog.cs`를 대조:

- **lava** ↔ `cinder-span`("재의 다리"): "weathered charcoal basalt block masonry veined
  with dull orange ember cracks" — 프롬프트를 이 팔레트에 명시적으로 맞춤. `EmberGalleryHazards`
  (vent 계열)도 이 스테이지.
- **ice** ↔ `echo-throne`("메아리 왕좌"): "regal dark blue granite ... silver-blue veining
  ... pale blue echo rings" — 프롬프트를 이 팔레트에 명시적으로 맞춤. 이 스테이지가
  `HazardConfig.Current(768f, 604f, 120f, 0.3f)`(`StageCatalog.cs:147`)를 이미 갖고 있음.
- `abyss-chancel`(보라/인디고)은 화산·빙하 어느 쪽도 아니라 대상에서 제외.
- 저장소 내 스테이지 이름 자체에 "화산"/"빙하"가 문자 그대로 존재하지는 않음 — cinder-span/
  echo-throne이 가장 근접한 테마 매치이며, 위 프롬프트들이 실제로 그 두 스테이지의 팔레트를
  타깃팅했다.
- **shift**는 스테이지 고정이 아니라 기존 `HazardKind.TideCurrent`(`EnvironmentBuilder.cs:809`,
  `VfxDirector.cs:128/142/1248/1445`) 범용 마커로 설계 — echo-throne(§:147)과 §:485 앵커가
  이미 이 hazard를 사용 중.

Provenance: `docs/provenance/terrain-fx-sheets.json` (hero 프롬프트 3종, 조립 스크립트,
그리드 스펙, 테마-스테이지 조사, 텍스처 예산 검증 전부 기록).

### 코드 연동 [OBSERVED — 최종 라운드에서 확정 완료]
vfx-lane이 컨슈머 코드를 완성했다(§3.5.2 계약). 자산 쪽 `.meta`(wrapMode Clamp)까지
수기로 작성 완료 — 남은 항목은 Unity 에디터가 실제로 열릴 때의 임포트 검증뿐.

## 산출물 전체 경로 목록 (최종)

```
Assets/Art/Characters/human-command-boss.fbx
Assets/Art/Props/equip-weapon-dagger-basic.fbx
Assets/Art/Props/equip-weapon-dagger-fine.fbx
Assets/Art/Props/equip-weapon-bow-basic.fbx
Assets/Art/Props/equip-weapon-bow-fine.fbx
Assets/Art/Props/equip-weapon-hammer-basic.fbx
Assets/Art/Props/equip-weapon-hammer-fine.fbx
Assets/Resources/Audio/cue-click.mp3
Assets/Resources/Audio/cue-footstep.mp3
Assets/Resources/Audio/bgm-intro.mp3
Assets/Resources/Audio/bgm-lobby.mp3
Assets/Resources/Audio/bgm-loading.mp3
Assets/Resources/Audio/bgm-stage.mp3
tools/audio/gen_bgm.py                                  (신규 스크립트)
tools/audio/gen_sfx.py                                  (수정: click/footstep cue 추가)
tools/blender/gen_weapon_props.py                        (신규 스크립트)
_workspace/current/engineering/reskin/human-command-boss.json
docs/provenance/audio.json                                (수정: click/footstep 기록 추가)
docs/provenance/bgm.json                                  (신규)
docs/provenance/human-command-boss-reskin.json             (신규)
docs/provenance/weapon-props-procedural.json               (신규)
Assets/Resources/Scenes/scene-ember-rest.png                (신규, W15 — 컷씬 마스터, 유지)
Assets/Resources/Icons/ui-ember-rest-bg.png                  (신규, W15 최종 — HUD 실제 소비 자산, 1024×694)
Assets/Resources/Icons/ui-ember-rest-bg.png.meta             (신규, W15 최종 — 수기 작성, Sprite/spriteMode:1)
Assets/Resources/Terrain/terrain-fx-lava-sheet.png            (신규, W16 최종 — 확정 경로, 그레이스케일)
Assets/Resources/Terrain/terrain-fx-lava-sheet.png.meta       (신규, W16 최종 — 수기 작성, wrapMode Clamp)
Assets/Resources/Terrain/terrain-fx-ice-sheet.png             (신규, W16 최종 — 확정 경로, 그레이스케일)
Assets/Resources/Terrain/terrain-fx-ice-sheet.png.meta        (신규, W16 최종 — 수기 작성, wrapMode Clamp)
Assets/Resources/Terrain/terrain-fx-shift-sheet.png           (신규, W16 최종 — 확정 경로, 그레이스케일)
Assets/Resources/Terrain/terrain-fx-shift-sheet.png.meta      (신규, W16 최종 — 수기 작성, wrapMode Clamp)
tools/gen_terrain_fx_sheets.py                                (신규 스크립트, W16, 그레이스케일 변환 포함)
_workspace/current/engineering/icons/terrain-fx-hero/lava-hero.png   (W16 중간 산출물)
_workspace/current/engineering/icons/terrain-fx-hero/ice-hero.png    (W16 중간 산출물)
_workspace/current/engineering/icons/terrain-fx-hero/shift-hero.png  (W16 중간 산출물)
docs/provenance/scene-ember-rest.json                          (신규, W15 — 두 자산 역할 분리·.meta 근거 포함)
docs/provenance/terrain-fx-sheets.json                         (신규, W16 — 경로/포맷/.meta 변경 이력 포함)
```

**삭제됨** (W16 rev1, vfx-lane 계약과 불일치해 폐기 — `docs/provenance/terrain-fx-sheets.json`
`deletedSuperseded`에도 기록):
```
Assets/Resources/Textures/Env/terrain-fx-lava-sheet.png   (삭제)
Assets/Resources/Textures/Env/terrain-fx-ice-sheet.png    (삭제)
Assets/Resources/Textures/Env/terrain-fx-shift-sheet.png  (삭제)
```

## 미해결 블로커

없음. 5개 과제(W12/W13/W14/W15/W16) 전부 완료, W15/W16은 소비 레인(ui-lane/vfx-lane)의
정본 계약과 최종 매칭까지 완료.

## 코드 연동 필요 항목 종합 (다음 레인/세션)

1. `AudioDirector.cs` — 스테이지 흐름별 BGM 전환(`bgm-intro/lobby/loading/stage`), click/footstep
   cue를 트리거할 이벤트 매핑.
2. `CharacterRoster.cs` — `"human-command-boss"` 추가.
3. `GameBootstrap.cs`/`GameView.cs` — 플레이어 prefab을 `human-command-boss`로 배정(현재
   `lantern-reaver` 하드코딩).
4. Unity 에디터 오픈 시 임포트 **검증**(사람 또는 다음 배치모드 세션) — human-command-boss
   FBX → Humanoid 프리팹, 무기 6종 FBX → 머티리얼+프리팹(기존 `equip-weapon-{basic,fine}`
   템플릿 참조). `ui-ember-rest-bg.png`·`terrain-fx-*-sheet.png` 3종은 `.meta`를 이미 수기로
   작성해뒀으니(Sprite/spriteMode:1, wrapMode Clamp) 신규 임포트가 아니라 **그 설정이
   실제로 반영됐는지 재확인**만 필요.
5. `ActorView.AttachEquipProps` — 무기 아키타입(단검/활/해머) selector 추가.
6. `HudView.BuildEmberRestPanel` — `Resources.Load<Sprite>("Icons/ui-ember-rest-bg")`를
   배경으로 로드하는 `Image` 추가(신규 연동 지점, 기존 자산 교체 아님).
7. ~~`VfxDirector.cs` 플립북 컨슈머~~ — **vfx-lane이 이미 완성**(§3.5.2 계약 확정 통해 확인).

이 레인은 다른 세션이 수정 중인 `Assets/Scripts/**`, `Assets/Editor/**`, `Assets/Scenes/**`,
`graphify-out/**`를 전혀 건드리지 않았다(`git status --short`로 시작/종료 시 확인).
