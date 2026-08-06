# 지형 셸 조사 — 플레이 영역 확장 여유와 가장자리 검정의 정체

**목적**: (a) 플레이어가 authored world 밖을 보기 전까지 플레이 영역을 얼마나 키울 수 있는가, (b) 가장자리에 보이는 검정이 정확히 무엇인가를 근거로 확정한다.
**범위**: 조사·계산·문서화만. **코드 수정 0건. Unity 미기동.**
**표기**: `[OBSERVED]` 직접 읽음/측정함 / `[INFERENCE]` 추론.
**리비전**: HEAD = `3e2e3a1` (조사 시작 시점 `5383577`, 그 위 docs 커밋 1건).

> ⚠️ **인용 기준 고정 — 조사 중 병행 편집 발생**
> `[OBSERVED]` 조사 도중 `Assets/Editor/SceneBuilder.cs`가 **작업 트리에서 +37줄 수정**되었다(`git diff --stat`). 내용은 본 문서 §5.2의 1·2번 권고를 그대로 구현한 것이다(§5.4 참조).
> **본 문서의 `SceneBuilder.cs:*` 인용은 전부 `HEAD`(`3e2e3a1`) 기준**이며, `git show HEAD:Assets/Editor/SceneBuilder.cs`로 15개 줄 전수 재검증했다. 현재 작업 트리 줄번호는 §5.4에 매핑해 두었다.
> `Assets/Scripts/Sim/CinderSim.cs` 역시 작업 트리에서 수정되어 있으며(dungeon 타원 클램프), 그 인용만 **작업 트리 현재 내용** 기준이다(§0.2·§6).
> 그 외 인용 파일(`CameraRig.cs`, `GameDirector.cs`, `ViewWorld.cs`, `StageCatalog.cs`, `SimTypes.cs`, `ActorView.cs`, `PostFxGate.cs`, 씬, `Assets/Settings/**`, `ProjectSettings/**`, FBX/prefab/머티리얼/텍스처)은 `git status` 기준 **미수정**이다.

---

## 0. 요약 (먼저 읽을 것)

### 0.1 두 질문에 대한 답

**Q1. 얼마나 키울 수 있나?**
`[OBSERVED]` **지형은 이미 sim world보다 크다.** 세 스테이지 전부 apron(최외곽 바닥판)이 **17.00 × 15.77~16.26 world u = 1700 × 1577~1626 sim u**이고, sim world는 1536 × 1024다. 아레나 중심(768,604) 기준으로 apron 안에 머무는 최대 반경은 **halfWidth 850 / halfHeight 696**(cinder-span 기준) — 현행 520/270 대비 **1.63× / 2.58×**. 즉 **바닥 부족은 확장의 제약이 아니다.**

**Q2. 검정은 무엇인가? → (a) 카메라 clear colour다.**
`[OBSERVED]` `Assets/Editor/SceneBuilder.cs:29-30` / `Assets/Scenes/CinderCourt.unity:446-447` 이 결정한다. 단, **"바닥이 모자라서"가 아니다.** 프레임을 192×128로 레이캐스팅해 측정한 결과 **하늘(지평선 위) 픽셀 0.0%** — 모든 픽셀이 y=0 평면을 far clip 안에서 맞힌다. 검정 픽셀은 전부 **"지면 평면 위이지만 apron 사각형 밖"**인 좌표다. Dungeon 3:2 calm에서 **프레임의 18.9%**, 16:9 bigwave에서 **51.7%**.

원인은 형상 불일치다: apron은 **고정 직사각형**, 카메라 지면 발자국은 **원경으로 벌어지는 사다리꼴**. 3:2 calm에서 근경 폭 1544 sim u는 apron(1700)이 덮지만 **원경 폭 2678 sim u는 못 덮는다**(§3.1).

### 0.2 Main의 4가지 가설에 대한 판정

| 가설 | 판정 | 근거 |
|---|---|---|
| (a) 카메라 clear colour | **✅ 확정** | `SceneBuilder.cs:29` `clearFlags = SolidColor`; `:30` `backgroundColor (0.043,0.035,0.06)` = `#0B090F`. 씬에 직렬화됨: `CinderCourt.unity:446` `m_ClearFlags: 2`, `:447` 동일 색 |
| (b) 지오메트리 부재 | **✅ 동시 성립 (a의 원인)** | clear colour가 *보이는* 이유가 그 픽셀에 지오메트리가 없어서다. 단 "apron이 작아서"가 아니라 **"apron이 사각형이고 프러스텀은 사다리꼴이라서"** |
| (c) unlit backface | **❌ 기각** | apron 로컬 normal `(0,0,1)`, `Lcl Rotation(-90,0,0)` 적용 후 world normal **(0, +1, 0) = 위쪽**. 3스테이지 전부 동일. 머티리얼 `_Cull: 2`(Back)이므로 위에서 보는 면이 정상 렌더 |
| (d) fog | **❌ 기각 (존재하지 않음)** | `CinderCourt.unity:17` `m_Fog: 0`. 프로젝트 어디에도 활성 fog 없음 |
| (추가) apron이 bounds대로 배치 안 됨 | **❌ 기각** | prefab root scale `(1,1,1)`, `GameDirector.cs:121`이 position만 설정(rotation/scale 미변경). FBX Lcl Translation 크기와 prefab local position 크기 일치(5.1123185) → import scale 1:1 |
| (추가) 26° 지평선 너머를 봄 | **❌ 기각** | Dungeon pitch는 26°가 아니라 **55°**(`CameraRig.cs:218` `PlaceOrbit(55f, ...)`). 26°는 Prologue 전용(`CameraRig.cs:19`). pitch 55°/FOV 42° → 상단 광선 하향각 34° > 0이므로 **지평선이 프레임에 없다**(sky 0.0%) |

### 0.3 그래서 art인가 카메라 한 줄인가

`[INFERENCE]` **카메라/셰이딩 쪽 한 줄이 맞다.** art로 풀려면 16:9 bigwave까지 덮는 데 프리팹 **2.31× 확대**가 필요하고(§5.1), 그러면 apron 텍스처가 2.31× 늘어나 텍셀 밀도가 그만큼 떨어진다. 반면 §5.2/§5.3은 `SceneBuilder.cs` 3~5줄이다.

---

## 1. 스테이지별 지형 실측 범위

### 1.0 측정 방법 (재현 절차)

`[OBSERVED]` `Assets/Art/Terrain/*.fbx` 3개는 **Kaydara FBX Binary, version 7400**(헤더 매직 + offset 23의 uint32 확인). Python으로 바이너리 노드 트리를 파싱해 `Objects/Geometry/Vertices`(float64 배열)와 대응 `Objects/Model`의 `Lcl Translation/Rotation/Scaling`을 읽고, `Connections`의 `OO` 엣지로 Geometry→Model을 이었다. 각 정점에 `S → R(XYZ euler, deg) → T` 를 적용해 prefab-local AABB를 냈다. **파일은 읽기만 했다.**

`[OBSERVED]` 검증 앵커 — 계산된 prefab-local 변환이 `Assets/Resources/Terrain/terrain-echo-throne.prefab`에 직렬화된 자식 Transform과 일치한다:

| 자식 | FBX `Lcl Translation` | prefab `m_LocalPosition` |
|---|---|---|
| `terrain-echo-throne-slab-001` | `(-5.112318, 0, 0)` | `(5.1123185, 0, 0)` (prefab L120) |
| `terrain-echo-throne-slab-002` | `(-0.184782, 0, -4.557970)` | `(0.1847826, 0, -4.5579705)` |
| 전 자식 `Lcl Scaling` | `0.5279502868652344` | `0.5279503` (prefab L121) |

`[OBSERVED]` X 부호만 반전(Unity FBX 임포터의 좌수/우수 변환), **크기는 완전 보존** → `globalScale: 1`(`terrain-echo-throne.fbx.meta:67`)이 실제로 1:1임을 확인. 따라서 FBX에서 계산한 bounds를 그대로 prefab-local bounds로 쓸 수 있다.

`[OBSERVED]` apron 지오메트리는 정점 4개짜리 quad다. cinder-span 원시 정점: `(±16.1, ±14.9333, 0)`. × 0.5279503 = `±8.5 × ±7.884`.

### 1.1 실측 결과

`[OBSERVED]` prefab-local AABB (world units, root scale 1):

| 스테이지 | 메시 수 | apron X | apron Z | union Y | 슬래브 union |
|---|---:|---|---|---|---|
| `terrain-cinder-span` | 94 | −8.500 … +8.500 (**17.000**) | −7.884 … +7.884 (**15.768**) | −0.001 … 0.187 | 14.043 × 12.812 |
| `terrain-abyss-chancel` | 53 | −8.500 … +8.500 (**17.000**) | −8.007 … +8.007 (**16.014**) | −0.001 … 2.387 | 14.043 × 13.058 |
| `terrain-echo-throne` | 6 | −8.500 … +8.500 (**17.000**) | −8.130 … +8.130 (**16.261**) | −0.001 … 0.000 | 14.043 × 13.304 |

`[OBSERVED]` 세 스테이지 apron의 X 폭이 **정확히 동일(17.000)**하고 Z만 미세하게 다르다.

### 1.2 배치: 코드 경로와 sim 좌표 변환

`[OBSERVED]` 배치 코드 경로 (`Assets/Scripts/View/GameDirector.cs`):

- `SetStageTerrain(string stageId)` — L105-122. L116 `Resources.Load<GameObject>("Terrain/terrain-" + stageId)`, L118 `Instantiate(prefab)`, **L121 `transform.position = ViewWorld.ToWorld(768f, 512f, 0f)`**. rotation/scale 미설정 → prefab 값(identity / 1) 유지.
- 호출부: L267 `SetStageTerrain(entry.TerrainId)` (Dungeon 진입), L88/L217/L232 `SetStageTerrain(null)` (Lobby/Arena/Prologue → 지형 없음).
- `ApplyStageDressing(string stageId)` — L130-179. cinder-span 프리팹 자식을 `StageCatalog.DressingFor()` 테이블대로 복제. L163 `ViewWorld.ToWorld(placement.SimX, placement.SimY, 0f)`.

`[OBSERVED]` 좌표 매핑 (`Assets/Scripts/View/ViewWorld.cs:9,11-12`): `Scale = 0.01f`, `ToWorld(simX, simY, h) = (simX*0.01, h, -simY*0.01)`.

`[OBSERVED]` **지형 앵커는 아레나 중심이 아니다.** L121은 `(768, 512)` = world `(7.68, 0, -5.12)` = **world 중심**. 아레나 중심은 `(768, 604)` = world `(7.68, 0, -6.04)`. **sim Y로 92 u (0.92 world u) 어긋나 있다.**

`[OBSERVED]` 산술 — apron world/sim 범위:

```
world X = 7.68 ± 8.50           → −0.820 … 16.180
sim  X  = world X / 0.01        → −82.0 … 1618.0        (폭 1700)

cinder-span:  world Z = −5.12 ± 7.884 → −13.004 … +2.764
              sim  Y  = −world Z / 0.01 → −276.4 … 1300.4   (깊이 1577)
abyss-chancel: sim Y −288.7 … 1312.7                        (깊이 1601)
echo-throne:   sim Y −301.0 … 1325.0                        (깊이 1626)
```

`[OBSERVED]` sim world는 1536 × 1024 (`SimTypes.cs:156`). apron은 **X로 좌우 82 u씩, Y로 위아래 276~301 u씩 world를 넘어선다.**

`[OBSERVED]` 부수 확인 — `StageCatalog.cs:177`의 "15.36-unit plate" 주석은 apron(17.00)이 아니라 `CourtBackdrop`(15.36) 얘기다. 드레싱 배치 8개 극단값(x 160~1420, y 160~985)은 **전부 apron 안**이다.

---

## 2. 지형 가장자리 너머에 렌더되는 것 — 확정

### 2.1 결정하는 줄

`[OBSERVED]` `Assets/Editor/SceneBuilder.cs`:
```
29:            camera.clearFlags = CameraClearFlags.SolidColor;
30:            camera.backgroundColor = new Color(0.043f, 0.035f, 0.06f);
```
`[OBSERVED]` 씬에 직렬화된 실제 값 — `Assets/Scenes/CinderCourt.unity:446-447`: `m_ClearFlags: 2` (2 = SolidColor), `m_BackGroundColor: {r: 0.043, g: 0.035, b: 0.06, a: 1}` = **`#0B090F`**.

`[OBSERVED]` 런타임에 이를 바꾸는 코드는 없다. `Assets/Scripts` 전체에서 `clearFlags` / `backgroundColor` 검색 결과 히트 0 (`CameraRig.cs:46`의 주석 언급뿐). `CameraRig.SetProfile`(L57-105)은 `orthographic`/`fieldOfView`/transform만 만진다.

### 2.2 다른 후보들이 기각되는 이유

`[OBSERVED]` **fog 없음** — `CinderCourt.unity:17` `m_Fog: 0`. (참고: `:18` FogColor `(0.5,0.5,0.5)`, `:21` LinearFogEnd `300` — 켜도 far clip 80보다 커서 무효.)

`[OBSERVED]` **skybox 도달 불가** — `CinderCourt.unity:29` `m_SkyboxMaterial`은 Unity 기본 Default-Skybox(`guid: 0000...f000...`)를 가리키지만 `clearFlags: 2`는 skybox 패스를 건너뛴다. `Assets/` 안에 Skybox 셰이더를 쓰는 `.mat`은 0건.

`[OBSERVED]` **backface 아님** — FBX `LayerElementNormal/Normals` 파싱 결과 3스테이지 apron 전부 로컬 normal `(0.00, 0.00, 1.00)`. `Lcl Rotation (-90.00000933, 0, 0)` 적용 → **world normal (0.000, +1.000, −0.000)**. 위를 향한다. 머티리얼 `_Cull: 2`(Back) → 위에서 보면 front face.

`[OBSERVED]` **far clip 아님** — 카메라 far clip 80 (`CinderCourt.unity:469`). Dungeon 3:2 calm에서 카메라→apron 원경 코너 거리 최대 **24.71 u**. 프러스텀 상단 광선이 지면과 만나는 slant distance는 **23.25 u** (bigwave 28.72 u, portrait bigwave 63.18 u) — 전부 80 미만.

`[OBSERVED]` **apron 미배치/오배치 아님** — §1.0 검증 앵커 + `GameDirector.cs:121` position-only.

`[OBSERVED]` **지평선 너머 아님** — Dungeon pitch **55°** (`CameraRig.cs:218`), FOV 42° (`CameraRig.cs:100`). 상단 광선 하향각 = 55 − 21 = **34°** > 0. 하단 = 55 + 21 = 76°. 두 광선 모두 지면을 향한다.

### 2.3 그럼 왜 검정인가 — 형상 불일치

`[OBSERVED]` **측정**: Dungeon 프레임을 192×128 = 24,576 픽셀로 레이캐스팅했다. 각 픽셀 광선을 `PlaceOrbit(55, d, ArenaCenter)` 카메라 기저 (`forward=(0,−sin55,cos55)`, `up=(0,cos55,sin55)`, `right=(1,0,0)`)로 만들고 y=0 평면과 교차시킨 뒤, 그 점이 apron 사각형 / CourtBackdrop 사각형 안인지 판정했다.

| 프로파일 | aspect | `_aspectWiden` | dist | **apron** | **검정** | **하늘** |
|---|---|---:|---:|---:|---:|---:|
| Dungeon calm | 3:2 (1280×853) | 1.000 | 17.00 | 81.1% | **18.9%** | **0.0%** |
| Dungeon bigwave | 3:2 | 1.000 | 21.00 | 57.2% | **42.8%** | **0.0%** |
| Dungeon calm | 16:9 | 1.000 | 17.00 | 69.4% | **30.6%** | **0.0%** |
| Dungeon bigwave | 16:9 | 1.000 | 21.00 | 48.3% | **51.7%** | **0.0%** |
| Dungeon calm | portrait 0.462 | 2.200 | 37.40 | 43.8% | **56.2%** | **0.0%** |
| Dungeon bigwave | portrait 0.462 | 2.200 | 46.20 | 35.6% | **64.4%** | **0.0%** |

`[OBSERVED]` **하늘 0.0%** — 검정 픽셀 100%가 지면 평면 위 좌표다. `[INFERENCE]` 즉 이건 "authored world 밖을 본다"가 아니라 **"지면이 있어야 할 곳에 지오메트리가 없다"**이고, 그래서 clear colour가 그대로 나온다. (b)가 (a)를 노출시키는 구조.

`[OBSERVED]` **원인 — 사각형 vs 사다리꼴**. 3:2 calm 기준:

| 지면 위치 | 카메라 가시 폭 | apron 폭 | 부족분 |
|---|---:|---:|---:|
| 근경 (world z −12.32) | 1544 sim u | 1700 | **0 (덮음)** |
| 아레나 중심 (z −6.04) | ~2100 sim u | 1700 | 약 400 |
| 원경 (z +4.86) | 2678 sim u | 1700 | **978** |

`[OBSERVED]` Z 방향도 부족: 가시 z 범위 −12.319 … **+4.855**, cinder-span apron z 범위 −13.004 … **+2.764**. 근경은 0.685 u 여유가 있지만 **원경에서 2.09 world u (209 sim u)가 비어 있다.**

`[OBSERVED]` apron 원경 모서리가 화면에서 어디인지: 3:2 calm에서 **프레임 92.6% 높이**(cinder-span), bigwave에서 **86.1%**. `[INFERENCE]` 즉 화면 상단 7~14% 띠 + 상단 좌우 삼각형이 검정이다. 이는 "지평선"처럼 보이지만 지평선이 아니라 **바닥판의 끝**이다.

### 2.4 검정이 눈에 띄는 이유 (대비)

`[OBSERVED]` apron 머티리얼은 전부 **URP/Unlit** (shader `guid: 650dd9526735d5b46b79224bc6e94025` = `com.unity.render-pipelines.universal@73b4c4ff130e/Shaders/Unlit.shader`), `_BaseColor (1,1,1,1)`. `[INFERENCE]` 따라서 화면 색 = 알베도 텍스처 값 그대로. 조명/앰비언트가 밝혀주지 않는다.

`[OBSERVED]` `magick <png> -resize 1x1!` 로 잰 평균 알베도:

| 텍스처 | 평균 sRGB | hex |
|---|---|---|
| `Assets/Art/Terrain/terrain-cinder-span-textures/albedo-mat-cinder-span-apron.png` (1024²) | 21.4 / 19.0 / 18.7 % | `#373030` |
| `Assets/Art/Terrain/terrain-abyss-chancel-textures/albedo-mat-abyss-chancel-apron.png` (1024²) | 21.5 / 22.6 / 26.7 % | `#373A44` |
| `Assets/Art/Terrain/terrain-echo-throne-textures/albedo-mat-echo-throne-apron.png` (1024²) | 33.4 / 29.9 / 26.1 % | `#554C43` |
| `Assets/Art/Textures/cinder-court-backdrop.png` (1536×1024) | 7.6 / 10.0 / 13.0 % | `#131A21` |
| **카메라 clear** | 4.3 / 3.5 / 6.0 % | **`#0B090F`** |

`[OBSERVED]` cinder-span apron(`#373030`, 8bit 55,48,48) vs clear(`#0B090F`, 11,9,15) — **채널당 약 5×**. `[INFERENCE]` 둘 다 어두워서 WCAG 대비비는 1.54:1로 낮지만, 인접한 하드 에지이므로 "바닥이 끊긴 자리"로 또렷이 읽힌다.

### 2.5 다른 프로파일의 검정 (Dungeon만의 문제가 아니다)

`[OBSERVED]` Prologue/Arena/Lobby는 `SetStageTerrain(null)`이라 **지형이 없고 `CourtBackdrop`만** 있다. CourtBackdrop은 `CinderCourt.unity:302-304`에서 rotation `(0.7071068,0,0,0.7071068)` = Euler(90,0,0), position `(7.68, −0.01, −5.12)`, scale `(15.36, 10.24, 1)` → world X 0…15.36, Z −10.24…0 = **sim 0…1536, 0…1024** 정확히 sim world.

`[OBSERVED]` 동일 레이캐스팅:

| 프로파일 | aspect | 덮임 | **검정** |
|---|---|---:|---:|
| Prologue (ortho 3.6, pitch 26°, dist 12) | 3:2 | 62.5% | **37.5%** |
| Prologue | 16:9 | 62.5% | **37.5%** |
| Prologue | portrait 0.462 (size 7.92) | 28.1% | **71.9%** |
| Arena (씬 베이크 카메라, FOV 32, pitch 44°, pos (7.68,11.8,−17.6)) | 3:2 | 73.3% | **26.7%** |
| Arena | 16:9 | 67.7% | **32.3%** |

`[OBSERVED]` **CourtBackdrop은 세 apron 전부의 안쪽에 완전히 포함된다** (0…15.36 ⊂ −0.82…16.18, −10.24…0 ⊂ −13.00…2.76). `[INFERENCE]` 따라서 Dungeon에서 백드롭은 커버리지에 1픽셀도 기여하지 않는다 — apron이 항상 덮고 있고 apron이 위(y≈−0.001 vs −0.01)에 있다.

---

## 3. 카메라 지면 발자국 — 산술

### 3.1 Dungeon (perspective)

`[OBSERVED]` 상수: `CameraRig.cs:100` `fieldOfView = 42f`; `:218` `PlaceOrbit(55f, _dungeonDistance * _aspectWiden, focus)`; `:37-38,101` 기본 거리 17; `:109` bigWave 21; `:137` `_aspectWiden = Clamp(1.5 / max(0.5, aspect), 1, 2.2)`; `:21` `ArenaCenter = ViewWorld.ToWorld(768, 604)` = `(7.68, 0, −6.04)`.

`[OBSERVED]` `PlaceOrbit` (L240-245): `rotation = Euler(pitch,0,0)`, `position = focus − rotation * Vector3.forward * distance`. `Euler(55,0,0) * (0,0,1) = (0, −sin55, cos55) = (0, −0.81915, 0.57358)`.

**3:2 calm (dist 17, `_aspectWiden` = 1) 전체 산술:**

```
[1] 카메라 위치
    pos = (7.68, 0, −6.04) − (0, −0.81915, 0.57358)·17
        = (7.68, +13.9256, −15.7906)
    h = 13.9256

[2] 상/하단 광선 하향각
    half_v = 42/2 = 21°
    top    = 55 − 21 = 34°       (>0 ⇒ 지평선 프레임 밖)
    bottom = 55 + 21 = 76°

[3] 지면까지 수평거리
    d_far  = h / tan(34°) = 13.9256 / 0.67451 = 20.646
    d_near = h / tan(76°) = 13.9256 / 4.01078 =  3.472

[4] world z
    z_far  = −15.7906 + 20.646 = +4.855   → sim Y = −485.5
    z_near = −15.7906 +  3.472 = −12.319  → sim Y = 1231.9
    깊이 = 17.174 world u = 1717 sim u

[5] 수평 half-FOV  (aspect = 1280/853 = 1.50059)
    half_h = atan(tan(21°) · 1.50059) = atan(0.38386·1.50059)
           = atan(0.57604) = 29.94°

[6] 각 지면 모서리에서의 반폭 (카메라 forward축 투영거리 t × tan(half_h))
    원경: t = (−h)(−0.81915) + 20.646(0.57358) = 11.407 + 11.842 = 23.249
          hw_far  = 23.249 · tan(29.94°) = 23.249 · 0.57604 = 13.392
    근경: t = 11.407 + 3.472(0.57358) = 11.407 + 1.992 = 13.399
          hw_near = 13.399 · 0.57604 =  7.718

[7] 가시 지면 사다리꼴 (world)
    원경변: x  7.68 ± 13.392 = −5.712 … 21.072   (폭 26.784 u = 2678 sim u)
    근경변: x  7.68 ±  7.718 = −0.038 … 15.398   (폭 15.436 u = 1544 sim u)
    z: −12.319 … +4.855
```

**전체 프로파일 표** (`[OBSERVED]`, 위와 동일 절차):

| aspect | widen | tier | dist | cam h | 지면 z | 깊이 (sim) | 근경 폭 (sim) | 원경 폭 (sim) |
|---|---:|---|---:|---:|---|---:|---:|---:|
| 3:2 | 1.00 | calm | 17.00 | 13.926 | −12.319…+4.855 | 1717 | 1544 | **2678** |
| 3:2 | 1.00 | bigwave | 21.00 | 17.202 | −13.796…+7.418 | 2121 | 1907 | **3309** |
| 16:9 | 1.00 | calm | 17.00 | 13.926 | −12.319…+4.855 | 1717 | 1829 | **3173** |
| 16:9 | 1.00 | bigwave | 21.00 | 17.202 | −13.796…+7.418 | 2121 | 2259 | **3920** |
| portrait 0.462 | 2.20 | calm | 37.40 | 30.636 | −19.853…+17.928 | 3778 | 1046 | 1814 |
| portrait 0.462 | 2.20 | bigwave | 46.20 | 37.845 | −23.103…+23.568 | 4667 | 1292 | 2241 |

`[OBSERVED]` **이 표가 "얼마나 많은 world가 존재해야 하는가"의 답이다.** 3:2 calm만 봐도 원경에서 **2678 sim u** 폭이 필요한데 apron은 1700이다.

`[OBSERVED]` **`_aspectWiden`은 문제를 키운다.** portrait에서 거리를 2.2× 늘려(17→37.4) 카메라를 더 뒤/위로 보내므로 지면 발자국이 3778 sim u 깊이까지 벌어진다. `CameraRig.cs:146-147` 주석대로 Dungeon은 FOV가 아니라 **거리**를 곱하기 때문이다.

### 3.2 Prologue (orthographic)

`[OBSERVED]` 상수: `CameraRig.cs:18` `PrologueOrthoSize = 3.6f`; `:19` `ProloguePitch = 26f`; `:20` `PrologueDistance = 12f`; `:89-91` ortho + `PlaceOrbit(26, 12, ArenaCenter)`; `:144` `orthographicSize = 3.6 * _aspectWiden`.

**3:2 (widen 1) 전체 산술:**

```
[1] 카메라 위치
    Euler(26,0,0)·(0,0,1) = (0, −sin26, cos26) = (0, −0.43837, 0.89879)
    pos = (7.68,0,−6.04) − (0,−0.43837,0.89879)·12 = (7.68, +5.2605, −16.8255)
    up  = Euler(26,0,0)·(0,1,0) = (0, cos26, sin26) = (0, 0.89879, 0.43837)

[2] 뷰 반높이/반폭
    halfH = orthoSize            = 3.6
    halfW = orthoSize · aspect   = 3.6 · 1.50059 = 5.4021

[3] 상/하단 변의 지면 교차 (ortho ⇒ 광선은 전부 forward 평행)
    하단: P = pos + up·(−3.6) = (7.68, 5.2605−3.2356, −16.8255−1.5781)
             = (7.68, 2.0249, −18.4036)
          t = −2.0249 / (−0.43837) = 4.619
          z = −18.4036 + 4.619·0.89879 = −14.252
    상단: P = pos + up·(+3.6) = (7.68, 8.4961, −15.2474)
          t = −8.4961 / (−0.43837) = 19.381
          z = −15.2474 + 19.381·0.89879 = +2.172

[4] 가시 지면 직사각형 (ortho ⇒ 좌우 폭 일정)
    x: 7.68 ± 5.4021 = 2.278 … 13.082     → sim X  228 … 1308  (폭 1080)
    z: −14.252 … +2.172                   → sim Y −217 … 1425  (깊이 1642)

[5] 클립 검사: t 범위 4.62 … 19.38 ⊂ [0.5, 80]  ✅
```

| aspect | widen | orthoSize | halfW | 가시 sim X | 가시 sim Y | 검정 |
|---|---:|---:|---:|---|---|---:|
| 3:2 | 1.00 | 3.600 | 5.402 | 228 … 1308 (1080) | −217 … 1425 (1642) | 37.5% |
| 16:9 | 1.00 | 3.600 | 6.400 | 128 … 1408 (1280) | −217 … 1425 (1642) | 37.5% |
| portrait 0.462 | 2.20 | 7.920 | 3.659 | 402 … 1134 (732) | −1203 … 2411 (3613) | 71.9% |

`[OBSERVED]` **Prologue의 부족은 Z축이다.** 가시 sim Y −217…1425 vs CourtBackdrop 0…1024 → 근경 **401 sim u**, 원경 **217 sim u** 부족. X는 backdrop(0…1536)이 전부 덮는다.
`[OBSERVED]` portrait에서 하단 변 t = **−4.24** (카메라 뒤) → near clip 0.5에 잘린다. 레이캐스팅에서 clipped 픽셀 3,648개(11.9%)로 관측.

### 3.3 기타 프로파일

`[OBSERVED]` **Arena** — `CameraRig.cs:78`이 `_basePosition/_baseRotation`(Awake에 씬 카메라에서 캡처)을 복원. 씬 값: pos `(7.68, 11.8, −17.6)` (`CinderCourt.unity:497`), rot `(0.37460658,0,0,1)` = **Euler(44,0,0)** (`:496`), FOV 32 (`:470`) × `_aspectWiden` (`CameraRig.cs:141`).
`[OBSERVED]` **Lobby** — L166-171, FOV 36 (L83), `ArenaCenter + Euler(18,yaw,0)·(0,2.6,−9.5)`. 24초 주기 yaw ±6°.
`[OBSERVED]` **PrologueReveal** — L191-195, 2.2초 동안 pitch 26→55, dist 9.4→17 (L194), FOV 42 (L96). `[INFERENCE]` 스윕 종료 상태가 §3.1의 Dungeon calm과 동일하므로 스윕 후반에 검정이 점증한다.

---

## 4. skybox / backdrop / fog 볼륨 인벤토리

`[OBSERVED]`

| 항목 | 존재? | 위치 / 값 |
|---|---|---|
| Skybox 머티리얼 | **실효 없음** | `CinderCourt.unity:29` Unity 기본 Default-Skybox. `clearFlags: 2`가 패스 스킵. `Assets/`에 Skybox `.mat` 0건 |
| RenderSettings fog | **꺼짐** | `CinderCourt.unity:17` `m_Fog: 0`; `:18` FogColor `(0.5,0.5,0.5)`; `:19` mode 3(Exp2); `:20` density 0.01; `:21-22` linear 0…300 |
| 앰비언트 | 있음 | `SceneBuilder.cs:61-62` `AmbientMode.Flat`, `(0.32,0.30,0.42)` = `#524C6B`. 씬 `:23` `m_AmbientSkyColor`, `:27` `m_AmbientMode: 3`. `[INFERENCE]` apron은 Unlit이라 무영향 |
| 백드롭 메시 | 있음 | `CourtBackdrop` — `SceneBuilder.cs:65-81`, 씬 `:231,302-304`. Quad, sim world 정확히 1개분. 머티리얼 `Assets/Art/Materials/CourtBackdrop.mat` (URP/Unlit, `_Cull: 2`, `_BaseMap` = `cinder-court-backdrop.png` 1536×1024) |
| URP 전역 볼륨 프로파일 | 있음 | `Assets/Settings/DefaultVolumeProfile.asset`. 19개 컴포넌트. Vignette `intensity 0`, Bloom `intensity 0`, **`OasisFogVolumeComponent` `Density: 0`** |
| 씬 PostVolume | 있음 (**깨짐**) | `CinderCourt.unity:514` GameObject, `:532` `m_IsGlobal: 1`, `:536` `sharedProfile` → `CinderPostProfile` |
| **CinderPostProfile** | **비어 있음** | `Assets/Settings/CinderPostProfile.asset:15-17` `components: [{fileID: 0}, {fileID: 0}]`. 파일 전체 YAML 문서 **1개** — Bloom/Vignette 서브에셋이 없다 |

### 4.1 🔴 Vignette와 Bloom은 현재 죽어 있다

`[OBSERVED]` `SceneBuilder.cs:131-142`는 Bloom(intensity 0.55, threshold 1.05, scatter 0.6)과 Vignette(intensity 0.22, smoothness 0.45, color `(0.02,0.02,0.05)`)를 `profile.Add<>()`로 추가한다. 그러나 디스크의 `CinderPostProfile.asset`에는 **null 레퍼런스 2개만** 남아 있고 컴포넌트 서브에셋이 직렬화되지 않았다.

`[INFERENCE]` `BuildPostProfile()`이 `EditorUtility.SetDirty(profile)`(L143)만 하고 `AssetDatabase.AddObjectToAsset`을 하지 않아, 배치 모드 밖에서는 `AssetDatabase.SaveAssets()`(L112, `Application.isBatchMode` 가드 안)가 안 돌아 서브에셋이 유실된 것으로 보인다.

`[INFERENCE]` **§5의 vignette 옵션에 직접 영향**: "vignette 추가"는 신규 기능이 아니라 **기존 의도의 복구**다.

### 4.2 렌더 파이프라인 (WebGL 실제 경로)

`[OBSERVED]` `ProjectSettings/GraphicsSettings.asset:49` `m_CustomRenderPipeline` → `guid: 4b83569d67af61e458304325a23e5dfd` = `Assets/Settings/PC_RPAsset.asset`.
`[OBSERVED]` `ProjectSettings/QualitySettings.asset:130` **`WebGL: 0`** → 레벨 0 = `name: Mobile` (`:10`) → `customRenderPipeline` `guid: 5e6cbd92db86f4b18aec3ed561671858` = `Mobile_RPAsset`.

`[OBSERVED]` 따라서 **WebGL 빌드의 실제 파이프라인은 Mobile_RPAsset + Mobile_Renderer**:

| 항목 | Mobile (WebGL 실사용) | PC (에디터 기본) |
|---|---|---|
| RenderScale | **0.8** (`Mobile_RPAsset:29`) | 1 (`PC_RPAsset:29`) |
| RequireOpaqueTexture | **0** (`:23`) | 1 |
| RequireDepthTexture | **0** (`:22`) | 1 |
| RenderingMode | **0 = Forward** (`Mobile_Renderer:48`) | 2 = Deferred (`PC_Renderer:56`) |
| RendererFeatures | **`[]`** (`Mobile_Renderer:29`) | SSAO 1개 (`PC_Renderer:71`) |
| ShadowCascades | 1 (`:58`) | 4 |

`[INFERENCE]` **§5 비용 산정은 Forward + RenderScale 0.8 + 오파크/뎁스 텍스처 없음 기준으로 해야 한다.** 에디터에서 본 화면(Deferred+SSAO)은 WebGL과 다르다.

`[OBSERVED]` `PostFxGate.cs:18-20`은 `Application.isMobilePlatform`일 때만 post를 끈다. `[INFERENCE]` WebGL 데스크톱은 false이므로 post 경로는 살아 있다 — **다만 §4.1대로 프로파일이 비어서 실질 효과가 0**이다.

`[OBSERVED]` **fog 셰이더 변이는 이미 빌드에 포함된다** — `GraphicsSettings.asset:56` `m_FogStripping: 0`(Automatic 아님), `:65-67` `m_FogKeepLinear/Exp/Exp2: 1`. `[INFERENCE]` fog를 켜도 **변이 재빌드나 용량 증가가 없다.**

`[OBSERVED]` URP/Unlit 셰이더는 fog를 지원한다(`MixFog`). `[INFERENCE]` 지형·백드롭 전부 Unlit이므로 **fog를 켜면 즉시 적용된다.**

---

## 5. 외곽을 채우는 방법 — 비용순 랭킹

### 5.0 목표 수치

`[OBSERVED]` 검정을 0으로 만들려면 필요한 지면 판 크기 (프러스텀 4모서리를 지면에 투영한 AABB):

| 프로파일 | 필요 판 (world u) | apron 대비 배율 |
|---|---|---|
| 3:2 calm | 26.78 × 17.17 | 1.58× / 1.09× |
| 3:2 bigwave | 33.09 × 21.21 | 1.95× / 1.35× |
| 16:9 calm | 31.73 × 17.17 | 1.87× / 1.09× |
| **16:9 bigwave** | **39.20 × 21.21** | **2.31× / 1.34×** |
| 21:9 bigwave | 51.45 × 21.21 | 3.03× / 1.34× |
| portrait bigwave | 22.41 × 46.67 | 1.32× / **2.96×** |

`[OBSERVED]` 전 프로파일 커버 = **51.4 × 57.4 world u (5145 × 5738 sim u)**, apron 대비 3.03× / 3.64×.
`[INFERENCE]` 데스크톱(3:2 + 16:9, 양 tier)만 목표로 하면 **39.20 × 25.08 u**로 충분하다.

### 5.1 랭킹

---

#### 🥇 1위 — 카메라 clear colour + linear fog를 팔레트에 맞춤 (최저 비용, 최고 ROI)

**바꿀 파일**: `Assets/Editor/SceneBuilder.cs` (L29-30 근처, L61-62 뒤에 3줄 추가) → 재빌드 후 `Assets/Scenes/CinderCourt.unity` 재생성.

`[INFERENCE]` 구체안:
- `camera.backgroundColor`를 스테이지 팔레트 쪽으로: 현행 `#0B090F`(charcoal) 유지하되 ember 방향으로 살짝 — 예 `(0.055, 0.040, 0.052)`. 또는 `index.html:14,17`의 `#050812`와 일치시켜 캔버스 레터박스와 경계가 사라지게 한다.
- `RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;`
- `RenderSettings.fogColor = camera.backgroundColor;` ← **이게 핵심.** fog color = clear color면 apron 끝이 배경으로 녹아 하드 에지가 사라진다.
- `RenderSettings.fogStartDistance / fogEndDistance`: `[OBSERVED]` 카메라→apron 원경 코너 24.71 u, 원경 중앙 23.20 u, 아레나 중심 17.00 u. `[INFERENCE]` start 16 / end 25 면 전투 영역(≤17)은 맑고 apron 끝(23~25)에서 완전 소멸.

**WebGL 비용**: `[OBSERVED]` fog 변이는 이미 포함(`GraphicsSettings.asset:56,65-67`) → **용량 증가 0**. `[INFERENCE]` Forward 경로에서 `MixFog`는 픽셀당 lerp 1회 — RenderScale 0.8 캔버스에서 측정 불가 수준. **추가 드로우콜 0, 추가 트라이앵글 0.**

**한계**: `[INFERENCE]` 검정이 사라지는 게 아니라 **"의도된 안개 속 어둠"으로 읽히게** 만든다. bigwave 51.7% 같은 극단에서는 화면 절반이 안개색이라 허전함은 남는다.

---

#### 🥈 2위 — Vignette 복구 (버그 수정, 실질 무료)

**바꿀 파일**: `Assets/Editor/SceneBuilder.cs:122-145` (`BuildPostProfile`) — `profile.Add<>()` 뒤에 `AssetDatabase.AddObjectToAsset(component, profile)` 추가 + `AssetDatabase.SaveAssets()`를 배치 가드 밖으로. 결과물 `Assets/Settings/CinderPostProfile.asset`.

`[OBSERVED]` 현재 이 프로파일은 컴포넌트 0개다(§4.1). 의도된 값은 이미 코드에 있다: intensity 0.22, smoothness 0.45, color `(0.02,0.02,0.05)` = `#05050D`.

`[INFERENCE]` vignette가 어둡히는 곳이 정확히 검정이 몰린 프레임 가장자리라, 1위와 합치면 "바닥이 끊긴 자리"가 "화면이 어두워지는 자리"와 겹쳐 경계가 더 흐려진다. **1위와 직교하며 합산 효과가 있다.**

**WebGL 비용**: `[OBSERVED]` `PC_Renderer:40`/`Mobile_Renderer:32` 둘 다 `postProcessData` 이미 참조 → 셰이더 이미 포함. `[INFERENCE]` vignette는 uber post 패스 내부 곱셈 몇 개. **단, RenderScale 0.8 + `m_IntermediateTextureMode: 0`(Auto) 환경에서 post를 켜면 중간 렌더타겟이 강제될 수 있다** — 이건 실측 필요.

**주의**: `[OBSERVED]` `SceneBuilder.cs:39-41` 주석이 desktop p95 10.0 ms / gate 16.7 ms를 근거로 든다. 그런데 §4.1대로 프로파일이 비어 있었으므로 **그 측정은 post가 실질 off인 상태의 값일 가능성이 높다**. `[INFERENCE]` 복구 후 재측정 필요.

---

#### 🥉 3위 — 링/백드롭 메시 1장 (중간 비용, 시각적으로 가장 확실)

**바꿀 파일**: `Assets/Editor/SceneBuilder.cs` (L64-81 `CourtBackdrop` 블록 옆에 두 번째 quad) → 씬 재생성. 또는 `Assets/Scripts/View/GameDirector.cs:105-122` `SetStageTerrain`에서 지형과 함께 생성.

`[INFERENCE]` 구체안: `CourtBackdrop`과 같은 방식의 Quad를, position `(7.68, −0.02, −5.12)`(apron 아래), scale `(40, 26, 1)`, rotation Euler(90,0,0). URP/Unlit + clear colour에 가까운 단색 또는 저해상도 노이즈 타일. 40 × 26 u면 §5.0의 데스크톱 요구(39.20 × 25.08)를 덮는다.

**WebGL 비용**: `[OBSERVED]` Quad = **2 트라이앵글, 4 정점, 드로우콜 +1**. `[INFERENCE]` 오버드로우는 프레임 100% 1회분 — Forward + RenderScale 0.8(=1024×682 유효 픽셀)에서 unlit 단색 풀스크린 1패스. 텍스처를 쓰면 그만큼 대역폭. 단색이면 사실상 clear 한 번 더 하는 비용.

**장점**: `[INFERENCE]` 유일하게 **실제로 무언가를 그리는** 옵션이라 "빈 공간"이 "먼 바닥"이 된다. 스테이지 accent(`StageCatalog.cs:104,110,116,122,128,134` — cinder-span `#F2592B`, abyss-chancel `#8F66FF`, echo-throne `#73C7FF`, ash-verdict `#DEC769`)로 살짝 틴트하면 스테이지 정체성도 살릴 수 있다.

---

#### 4위 — 지형 평면 확장 (고비용, 아트 리스크)

**바꿀 파일**: (A) `Assets/Scripts/View/GameDirector.cs:118-121` 에 `_stageTerrain.transform.localScale` 추가, 또는 (B) `Assets/Art/Terrain/*.fbx` 3개를 DCC에서 apron quad만 키워 재수출 + `Assets/Resources/Terrain/*.prefab` 재생성.

**(A) 프리팹 전체 스케일**: `[OBSERVED]` 16:9 bigwave 커버에 **2.31×** 필요.
`[INFERENCE]` 치명적 부작용 — **슬래브/피처/드레싱이 전부 2.31× 커진다.** 액터 스케일 기준(폴백 캡슐 `(0.5, 0.9, 0.5)`, `ActorView.cs:74`)이 상대적으로 1/2.31로 쪼그라들어 스케일 감각이 붕괴한다. apron 텍셀 밀도도 2.31× 하락(1024² 텍스처가 39.2 u를 덮음 = 26 텍셀/u, 현재 60). **비추천.**

**(B) apron quad만 확대 후 재수출**: `[OBSERVED]` apron은 정점 4개 quad이므로 기하학적으로는 사소하다. `[INFERENCE]` 그러나 (i) FBX 3개 재작업 + 임포트, (ii) 3개 albedo 텍스처(각 1024², 1.9~2.3 MB)를 넓은 영역에 맞춰 재작업하거나 UV 타일링 도입, (iii) `StageCatalog.cs:175-178`의 드레싱 스케일 근거("15.36-unit plate")가 흔들림. **가장 오래 걸리는 옵션.**

**WebGL 비용**: `[OBSERVED]` (A)는 트라이앵글 증가 0(스케일은 공짜). (B)도 정점 4개 유지. `[INFERENCE]` 비용은 런타임이 아니라 **텍스처 예산과 아트 시간**이다.

---

#### 5위 — Dungeon 카메라 거리/FOV 축소 (무료지만 게임플레이 영향)

**바꿀 파일**: `Assets/Scripts/View/CameraRig.cs:37-38,101,109` (거리 17/21) 또는 `:100` (FOV 42).

`[OBSERVED]` 거리를 17→13으로 줄이면 발자국이 대략 13/17 = 0.76×로 축소. `[INFERENCE]` 검정 18.9% → 한 자릿수.
`[OBSERVED]` **그러나** `:100` 주석이 FOV 42를 "original-verified combat FOV"로 명시하고, `:36` 주석이 거리 tier를 spec §10으로 못박는다. `:134-137`은 portrait 보정 근거를 상세히 남겼다. `[INFERENCE]` **검증된 게임플레이 값이므로 시각 문제 해결용으로 건드리면 안 된다.** 완결성을 위해 나열만 한다.

---

### 5.2 권고

`[INFERENCE]` **1위 + 2위를 한 커밋으로**, 파일은 **`Assets/Editor/SceneBuilder.cs` 하나**:

1. `BuildPostProfile()`(L122-145)에 `AddObjectToAsset` 추가 → vignette 0.22 부활 (**버그 수정**).
2. L62 뒤에 fog 3~4줄 추가: `fog = true`, `Linear`, `fogColor = camera.backgroundColor`, `start 16 / end 25`.
3. 선택: L30 `backgroundColor`를 `#050812`(`index.html:14`)에 맞춰 캔버스와 연속되게.

`[OBSERVED]` 근거: 추가 드로우콜 0, 추가 트라이앵글 0, 셰이더 변이 재빌드 0(`GraphicsSettings.asset:56,65-67`), 코드 5줄 이내, `Assets/Scripts/Sim/**` 무관, FROZEN CONTRACT 무관.

`[INFERENCE]` 그래도 bigwave/16:9의 넓은 빈 화면이 허전하면 **3위(백드롭 quad 1장, +1 드로우콜)**를 추가한다. **4위는 하지 말 것** — 비용 대비 이득이 가장 나쁘고 스케일 감각을 깬다.

### 5.3 아직 남은 것 (3·4·5위 미적용)

`[INFERENCE]` §5.4대로 1·2위가 들어가면 하드 에지는 사라지지만 **빈 면적 자체는 그대로**다(16:9 bigwave 51.7%). 그 화면이 허전하다는 판단이 서면 3위(백드롭 quad, +1 드로우콜)를 추가한다. 4·5위는 여전히 비추천.

---

### 5.4 구현 현황 (조사 종료 시점)

`[OBSERVED]` 본 보고서 작성 중 `Assets/Editor/SceneBuilder.cs`에 **§5.2의 1·2번이 이미 반영**되었다(작업 트리, 미커밋, +37/−0). 본 레인의 편집이 아니다.

| §5.2 항목 | 상태 | 작업 트리 위치 |
|---|---|---|
| 2. Vignette 복구 (`AddObjectToAsset`) | ✅ 반영됨 | `SceneBuilder.cs:163` (Bloom), `:173` (Vignette), `:180` `AssetDatabase.SaveAssets()` — 배치 가드 밖으로 이동 |
| 1. fog = clear colour | ✅ 반영됨 | `SceneBuilder.cs:79-83` — `fog = true`, `FogMode.Linear`, `fogColor = camera.backgroundColor`, `start 16f`, `end 25f` (권고값 그대로) |
| 3. `backgroundColor` → `#050812` | ⬜ 미반영 | `SceneBuilder.cs:30` 원값 유지 |

`[OBSERVED]` HEAD → 작업 트리 줄번호 이동 (본문 인용은 전부 HEAD 기준):

| 대상 | HEAD | 작업 트리 |
|---|---:|---:|
| `clearFlags` / `backgroundColor` | 29 / 30 | 29 / 30 (불변) |
| `RenderSettings.ambientLight` | 62 | 62 (불변) |
| `CreatePrimitive(Quad)` (CourtBackdrop) | 65 | 86 |
| `AssetDatabase.SaveAssets()` (배치 가드) | 112 | 133 |
| `BuildPostProfile()` 시그니처 | 122 | 143 |
| `vignette.intensity.Override(0.22f)` | 140 | 176 |
| `EditorUtility.SetDirty(profile)` | 143 | 179 |

`[미검증]` 이 변경의 실제 렌더 결과는 확인하지 않았다(Unity 미기동). 남은 검증 항목:
1. 씬 재생성 후 `CinderCourt.unity`의 `m_Fog: 0` → `1`, `m_FogColor`가 `(0.043,0.035,0.06)`로 바뀌는지.
2. `CinderPostProfile.asset`의 `components:`가 `{fileID: 0}` 2개 → 실제 Bloom/Vignette 서브에셋 2개로 바뀌는지.
3. §4.1·§5.1-2위 지적대로, post가 실제로 켜진 상태에서 WebGL(Forward + RenderScale 0.8) 프레임 비용 **재측정** — 기존 "p95 10.0 ms"는 post가 죽어 있던 빌드의 값이다.

---

## 6. 확장 여유 — 종합

`[OBSERVED]` apron 안에 머무는 최대 아레나 반경 (중심 768,604 고정):

| 스테이지 | max halfWidth | 현행 520 대비 | max halfHeight | 현행 270 대비 |
|---|---:|---:|---:|---:|
| cinder-span | 850.0 | **1.63×** | 696.4 | **2.58×** |
| abyss-chancel | 850.0 | 1.63× | 708.7 | 2.62× |
| echo-throne | 850.0 | 1.63× | 721.0 | 2.67× |

`[OBSERVED]` 면적 (`SimTypes.cs:158` 520 × 270 기준):

```
바운딩 박스 1040 × 540        = 561,600 u²
L1 다이아몬드 (2ab)           = 280,800 u²   (박스의 50%)
L2 타원     (πab)            = 441,080 u²   (박스의 79%)   ← 타원/다이아 = 1.571× (+57%)
apron 한계 타원 (π·850·696.4) = 1,859,634 u²  (현행 타원의 4.22×)
apron 사각형 1700 × 1576.8    = 2,680,560 u²  (현행 타원은 그 16.5%)
```

`[OBSERVED]` 마진 클램프 반영 시 실효값 — 플레이어(`PlayerMarginClamp 34`, `SimTypes.cs:166`): halfW 816 / halfH 679.4. 적(`EnemyMarginClamp 24`, `:177`): 826 / 684.4.

`[OBSERVED]` 이동 시간 (`PlayerSpeed 218`, `SimTypes.cs:161`; `YMoveScale 0.68`, `:169`):

| 아레나 | X 횡단 | Y 횡단 |
|---|---:|---:|
| 현행 1040 × 540 | 4.77 s | 3.64 s |
| apron 한계 1700 × 1393 | 7.80 s | **9.40 s** |

`[INFERENCE]` **결론**: 지형은 제약이 아니다 — 1.63× / 2.58× 여유가 이미 바닥에 깔려 있다. 진짜 제약은 두 가지다.
1. **카메라 발자국** — 확장하면 플레이어가 더 자주 사다리꼴 가장자리로 가므로 §5의 검정 문제가 **더 눈에 띈다**. §5.2를 먼저 하는 게 순서상 맞다.
2. **이동 시간** — Y로 2.58× 키우면 종단 9.4초. `[INFERENCE]` 218 u/s 기준으로 체감이 늘어질 수 있으므로 속도/카메라 tier와 함께 봐야 한다.

`[OBSERVED]` 부수 확인: `SimTypes.cs:157` `ArenaY = 604`인데 `GameDirector.cs:121` 지형 앵커는 sim Y **512**. **92 u 어긋남.** `[INFERENCE]` 지형을 604로 옮기면 apron 원경 여유가 92 u 늘고 근경이 92 u 준다. §3.1에서 부족한 쪽은 원경(209 u)이므로 **앵커를 604로 맞추면 원경 부족이 209 → 117 u로 줄어든다.** `GameDirector.cs:121` 한 줄이고, 드레싱 좌표는 `ToWorld`로 절대 배치되므로 영향 없다.

---

## 7. 미검증 / 이 레인의 권한 밖

- `[미검증]` 실제 렌더 스크린샷을 찍지 않았다(Unity 미기동 제약). 검정 비율은 문서화된 카메라 수식 + 지오메트리 AABB로부터의 **해석적 레이캐스팅** 결과다. 그림자, 드레싱 프롭의 원경 실루엣, post 처리 후 색은 반영되지 않았다.
- `[미검증]` §5.1 2위의 "중간 렌더타겟 강제" 여부와 post 복구 후 실제 프레임 비용은 실측이 필요하다.
- `[미검증]` `CinderPostProfile.asset`이 비어 있는 원인은 §4.1에서 추정만 했다. 에디터를 열어 재현하지 않았다.
- `[미검증]` `Library/` 캐시가 아니라 소스 에셋만 읽었으므로, 임포터가 실제로 산출한 메시 bounds는 §1.0의 교차검증(FBX↔prefab transform 일치)으로 간접 확인했을 뿐 직접 조회하지 않았다.
- `[OBSERVED]` 본 레인은 이 파일 1개 외에 저장소에 **쓰기 0건**이다. `Assets/` 하위 수정 0건, git 조작 0건, Unity 미기동. §5.4의 `SceneBuilder.cs` 변경은 **본 레인의 편집이 아니다** — 조사 중 병행 세션이 §5.2 권고를 반영한 것이고, 여기서는 사실만 기록했다.
