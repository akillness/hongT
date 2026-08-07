# VFX 레인 — W9 / V2 / V3 / W16-적용 구현 리포트

작성: 2026-08-07 · 레인: VFX/연출 · 기준 스펙: `_workspace/archive/20260805-visible-impact/design/deep-interview-vfx-terrain-command-hardening.md` §Lane V, `_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md` W9

> **동시 세션 고지 [OBSERVED]** — W9 작업 후 다른 세션이 `CameraRig.cs`를 커밋
> `324c26b feat(camera): pull the dungeon orbit in, derive the follow clamp`에
> 함께 실어 갔다. 이 레인은 커밋하지 않았다. 확인 결과:
> `git diff HEAD -- Assets/Scripts/View/CameraRig.cs`가 비어 있어 **커밋본은 내가
> 작성한 내용과 바이트 동일**하고, 해당 커밋의 자체 변경은 궤도 *거리*
> (20/24.5 → 17.5/21.5)와 FollowClamp 분수뿐이라 FOV 기준선과 충돌하지 않는다
> (Dungeon 42 / PrologueReveal 42 / Lobby 36 / Arena `BaseFov*_aspectWiden` 전부
> `BaseFovForProfile()`과 일치 재확인). **다만 신규 테스트
> `CameraFlourishTests.cs`는 그 커밋에 포함되지 않아, 현재 `CameraRig.cs`는
> 테스트 없이 커밋된 상태다** — 통합 시 함께 스테이징 필요.

---

## 0. 착수 시점 실측 — 과제 3건 중 2건은 이미 일부 랜딩되어 있었다

브리핑은 W9·V2·V3을 모두 미구현으로 전제했으나, 워킹트리 실측 결과는 달랐다.
[OBSERVED]

| 과제 | 착수 시점 상태 | 근거 |
|---|---|---|
| W9 카메라 연출 | **미구현.** 셰이크는 2D Perlin 오프셋뿐, FOV/롤 채널 없음 | `CameraRig.cs` `ShakeOffset` — x/y 오프셋만 |
| V2 벤트 fill | **랜딩됨.** CycleT 비례 fill 디스크 존재 | 커밋 `8c9daf8` 계열, `VfxDirector.SyncHazards` EmberVent 분기 |
| V3 원소 파티클 | **랜딩됨.** 4종 풀링 ParticleSystem + Emit(count) 존재 | `VfxDirector.BuildElementParticles`, 커밋 `685605f` 시점 |

따라서 이번 작업은 **W9 신규 구현 + V2/V3의 미이행 계약 조항 마감**이다.
V2/V3에서 실제로 남아 있던 결손은 아래 §2·§3에 결손별로 명시했다.

---

## 1. W9 — 카메라 연출 강화 (신규 구현)

### 1.1 설계 근거

셰이크는 "얼마나 세게 맞았나"만 답한다. 위치 오프셋에는 무게 채널이 없어서
피니셔와 보스 페이즈 전환이 *진폭만 다른 같은 떨림*으로 읽힌다. FOV 펀치(돌리
느낌) + 2° 미만 롤은 **위치를 건드리지 않는 두 번째 채널**이라 셰이크와 경합하지
않고 합성된다.

전 채널을 하드 바운드로 묶은 이유는 취향이 아니다. 이 게임은 텔레그래프를
카메라 프레임에 그리며, 그중 AOE 크라운은 심의 판정 타원에 맞춰져 있다
(`SkillShapeVocabularyTests`). **무한정 커지는 연출은 히트박스에 대해 거짓말하는
카메라다.**

### 1.2 변경 파일 (경로:라인)

| 위치 | 내용 |
|---|---|
| `Assets/Scripts/View/CameraRig.cs:34-61` | W9 상수/상태 필드 블록 (`MaxFlourishFov` 등 + `_placedRotation`) |
| `Assets/Scripts/View/CameraRig.cs:156` | `Awake` — `_placedRotation` 초기화 |
| `Assets/Scripts/View/CameraRig.cs:186-187` | `SetProfile` — 프로파일 전환 시 라이브 flourish 폐기 |
| `Assets/Scripts/View/CameraRig.cs:245-261` | `OnEvents` W9 트리거 체인 (셰이크 체인과 **분리**) |
| `Assets/Scripts/View/CameraRig.cs:272-293` | `public void Flourish(fov, roll, duration)` — 클램프 + 비-스톰프 |
| `Assets/Scripts/View/CameraRig.cs:301-308` | `BaseFovForProfile()` — 프로파일별 무연출 기준 FOV |
| `Assets/Scripts/View/CameraRig.cs:317-350` | `ApplyFlourish()` — 엔벨로프·셰이크 합성 클램프·복원 |
| `Assets/Scripts/View/CameraRig.cs:394,407,515` | `_placedRotation` 기록 (Arena / Lobby / `PlaceOrbit`) |
| `Assets/Scripts/View/CameraRig.cs:489-492` | `LateUpdate` 말미 `ApplyFlourish()` 호출 |

### 1.3 파라미터 표 [TARGET] — 전부 실측 전 목표치

**바운드 (하드 상한):**

| 상수 | 값 | 의미 |
|---|---|---|
| `MaxFlourishFov` | 4.0° | FOV 델타 절대 상한. 아레나 가장자리 ~7% 이동 후 복귀 |
| `MaxFlourishRoll` | 1.5° | 롤 절대 상한 |
| `MaxFlourishDuration` | 0.30 s | 지속 상한 (브리핑 "≤0.3s급" 준수) |
| `FlourishAttack` | 0.25 | 어택 구간 비율 — 피크가 임팩트 프레임에 착지 |
| `ShakeLoadReference` | 0.09 | "풀 로드" 기준 셰이크 진폭 (게임 내 최대치 = BossPhase2) |

**이벤트별 요청값 (클램프 전 요청, `OnEvents` 체인 순서대로):**

| 이벤트 | FOV 펀치 | 롤 | 지속 | 의도 |
|---|---|---|---|---|
| `BossPhase2` | **+3.5°** | −1.4° | 0.30 s | 프레임이 열림 — 방이 나보다 커진다 |
| `BossSpawned` | +2.6° | 0° | 0.28 s | 등장, 롤 없이 순수 개방 |
| `ComboFinisher` | **−2.2°** | +0.9° | 0.18 s | 프레임이 조여듦 — 대상을 끌어당김 |
| `NovaCast` | −1.6° | +0.6° | 0.22 s | 폭발 순간 압축 |
| `LevelUp` | −1.2° | 0° | 0.24 s | 약한 긍정 비트 |

부호 규약: **음수 FOV = 프레임이 닫힘, 양수 = 열림.** 이 부호가 "피니셔"와
"보스 등장"을 가르는 유일한 의미 채널이라 테스트로 고정했다.

> 브리핑의 "크리티컬"에 대응하는 `Critical` 이벤트는 심에 없다. 이 게임의
> 크리티컬은 `SimEvents.ComboFinisher` (`Assets/Scripts/Sim/SimTypes.cs`, `1 << 21`)
> 이며 해당 비트가 그 비트를 담당한다. [OBSERVED]

**합성/엔벨로프 규칙:**

- 엔벨로프: 어택 25% `SmoothStep(0→1)`, 릴리즈 75% `SmoothStep(1→0)`. 양 끝 팝 없음.
- 셰이크 합성 클램프: `composite = envelope × (1 − 0.5 × shakeLoad)`.
  풀 로드 셰이크 중에는 flourish가 **절반**으로 감쇠 — 합산이 아니다.
- 비-스톰프: 라이브 flourish보다 약한 요청은 무시 (`Punch`와 동일 규칙).
- FOV는 **매 프레임 기준값 + 델타**로 기록. `fieldOfView`를 누적 변형하지 않으므로
  `ApplyAspect`/`SetProfile`이 소유한 기준선을 드리프트시킬 수 없다.
- 롤은 `_placedRotation * Quaternion.Euler(0,0,roll)` **후곱**. 프레임 간 누적 불가.

### 1.4 텔레그래프·HUD 침해 여부

- HUD는 스크린 스페이스 오버레이 → FOV·롤 양 채널 모두 영향 없음. [INFERENCE]
- 텔레그래프: 최대 4°/0.3 s 이내 복귀. 판정은 심 소유이며 이 레이어는 전부 장식.
  `Assets/Scripts/Sim/` 무수정. [OBSERVED]
- `Prologue`는 직교 투영이라 FOV 채널이 비활성(`BaseFovForProfile` 기본 분기),
  롤만 적용.

### 1.5 GameView 훅 — **추가하지 않음**

`GameView.cs:381`이 이미 전체 이벤트 마스크로 `Rig.OnEvents(events)`를 호출한다.
W9 트리거를 그 체인 안에 넣어 **GameView 수정 0줄**로 끝냈다. UI 레인과의 공유
파일 충돌 위험 제거. [OBSERVED]

---

## 2. V2 — 벤트 fill 임박도 (기랜딩 + 결손 2건 마감)

### 2.1 결손 ① 링 텔레그래프가 reduced-motion에서 게이팅되지 않았다 — **실결함**

`VfxDirector.cs` EmberVent 분기의 링 알파가 `Mathf.PingPong(Time.time * 6f, 1f)`로
**무조건** 진동했다. TideCurrent 엣지(`!ViewPrefs.ReducedMotion` 게이트)와
AshWall 경계선은 이미 게이팅되어 있었고, 벤트 링만 남아 있었다. 6 Hz는 접근성
계약이 배제하려는 대역 그 자체다.

수정 (`Assets/Scripts/View/VfxDirector.cs:1180-1196`): reduced-motion에서 알파를
**펄스 천장(1.0)에 고정**. 골 부분이 아니라 피크에 고정한 이유 — 경고가 가장
필요한 사용자에게 더 *조용해지면* 안 된다.

### 2.2 결손 ② fill 콘트라스트가 정작 중요한 구간에서 무너졌다

기존: `fillColor.a = hazard.Telegraphing ? 0.5f : 0.16f` (2단 알파, 색상 고정).
fill이 커질수록 링과 **같은 엠버 색조·비슷한 명도**로 겹쳐서, "얼마나 임박했나"를
읽는 유일한 단서인 fill 선단이 링 안으로 녹아 없어졌다.

수정 (`Assets/Scripts/View/VfxDirector.cs:1213-1233`): 명도와 색조를 사이클과 함께
램프.

| 채널 | urgency 0 (사이클 시작) | urgency 1 (텔레그래프/사이클 말) |
|---|---|---|
| R | 1.00 | 1.00 |
| G | 0.42 | **0.72** |
| B | 0.18 | **0.42** |
| A | **0.20** (기존 0.16) | **0.62** (기존 0.50) |

텔레그래프 시 링은 (1, 0.42, 0.18) — fill (1, 0.72, 0.42)보다 명확히 어둡다.
그레이스케일에서도 경계가 살아남는다. 값 대비를 레버로 쓰는 것은 이 파일이 이미
채택한 원칙(§S1 주석)이다.

### 2.3 reduced-motion 처리

fill의 *성장 자체*는 게이팅하지 않았다. 벤트 주기 전체에 걸친 **단조 램프**이며
진동이 아니다 — 임의 시점을 정지 화면으로 잘라도 임박도가 그대로 읽힌다. 이것이
"정적 표시 유지" 계약을 만족하는 형태다. 코드 주석에 근거를 남겼다.

---

## 3. V3 — 원소별 풀링 파티클 (기랜딩 + 셰이더 시드 계약 마감)

### 3.1 결손 ① 시드 머티리얼 계약이 미이행 상태였다

스펙 §V3은 `RuntimeMaterialSeeds.Seed()`에 `particle-additive-seed.mat` 블록 추가를
요구한다. 기랜딩 코드는 이를 우회해 `ViewWorld.MakeAdditive`(= URP/Unlit 투명 시드
클론)를 쓰고 있었다. 핑크 렌더는 피했지만 **대가**가 있었다 — URP/Unlit은 파티클
정점 색상을 무시하므로 `colorOverLifetime`이 동작하지 않고, 버스트가 사이즈
커브에 가려진 채 **풀 알파로 팝아웃**했다.

**추가 파일/블록:**

| 위치 | 내용 |
|---|---|
| `Assets/Editor/RuntimeMaterialSeeds.cs:23` | `ParticleAssetPath = "Assets/Resources/Materials/particle-additive-seed.mat"` |
| `Assets/Editor/RuntimeMaterialSeeds.cs:35,60` | `Seed()`가 파티클 시드를 호출하고 결과를 반환에 반영 |
| `Assets/Editor/RuntimeMaterialSeeds.cs:78-127` | `SeedParticleAdditive()` 신규 |
| `Assets/Scripts/View/ViewWorld.cs:95-127` | `MakeParticleAdditive(Color)` — 시드 클론 전용 |
| `Assets/Scripts/View/VfxDirector.cs:356` | 런타임이 시드 클론만 사용 |

**빌드 훅 기배선 확인 [OBSERVED]:** `Assets/Editor/BuildScript.cs:35`가
`RuntimeMaterialSeeds.Seed()`를 호출한다. 신규 블록은 그 안에 들어가므로 추가
배선 불필요.

**셰이더 프로퍼티 실검증 [OBSERVED]:** 세팅한 프로퍼티명 전부를 실제 패키지
셰이더에서 확인했다 —
`Library/PackageCache/com.unity.render-pipelines.universal@73b4c4ff130e/Shaders/Particles/ParticlesUnlit.shader`
: `_Surface`(L23) `_Blend`(L24) `_ColorMode`(L36) `_Cull`(L25)
`_SrcBlend`/`_DstBlend`/`_SrcBlendAlpha`/`_DstBlendAlpha`(L28-31) `_ZWrite`(L32)
`_BaseColor`(L6). 블렌드는 `MakeAdditive`와 동일한 SrcAlpha/One + One/One.

**`new Material(Shader.Find(...))` 금지 준수:** 런타임 경로에는 `Shader.Find`가
없다. `Shader.Find`는 에디터 전용 시드 생성기 안에만 있다.

**폴백에 대한 명시적 정당화:** 시드 `.mat`은 Unity를 돌려야 생성되는데 이 레인은
Unity 실행이 금지되어 있다. 따라서 `MakeParticleAdditive`는 애셋 부재 시
`MakeAdditive`(기검증 경로)로 강등한다 — 강등 시 잃는 것은 정점 색상 페이드뿐이고,
이는 **이번 작업 전의 동작과 정확히 동일**하다. 즉 폴백은 회귀가 아니라 현상 유지다.
`SeedParticleAdditive`가 셰이더 부재 시 `true`를 반환하는 것도 같은 이유 — 장식
레이어가 빌드를 깨서는 안 된다.

### 3.2 결손 ② 에이기스가 이름과 반대로 움직이고 있었다

스펙: "에이기스 = 시안 **흡수** 플래시". 기랜딩 값은 `startSpeed 2.0` + 반경 0.12
구 → 바깥으로 터지는 퍼프. 흡수가 아니라 방출이다. 킷 내 유일하게 **자기 이름과
모순되는 모션**이었다.

수정: `startSpeed −2.0` + `shapeRadius 0.55` + `radiusThickness 0`(셸 방출).
입자가 시전자에게 **수렴**한다 — 워드의 eruption crown이 쓰는 것과 같은 문법 반전
(킷의 모든 것은 바깥으로 자라고, 방어 스킬만 조여든다).

### 3.3 결손 ③ 중력이 4종 공통 0.6이었다

지면 공명(파동)과 흡수 플래시(에이기스)에 낙하 중력은 틀린 값이다.
`BuildElementParticles`에 `gravity`/`shapeRadius` 파라미터를 추가해 원소별로 분리.

### 3.4 파티클 시스템 파라미터 표 [TARGET]

**공통** (`Assets/Scripts/View/VfxDirector.cs:258-357`):

| 항목 | 값 |
|---|---|
| 시스템 수 | 4 (Awake에서 사전 생성, GameObject 스폰 0) |
| `emission.enabled` | **false** — `Emit(count)`만 |
| `main.maxParticles` | **96** / 시스템 (하드 예산) |
| `simulationSpace` | World |
| 렌더 모드 | Billboard, 그림자 캐스트/수신 Off |
| 머티리얼 | `MakeParticleAdditive` 시드 클론 |
| 노이즈 | strength 0.35 / freq 1.8 / scroll 0.6 / **Low** quality / damping on |
| 사이즈 곡선 | 0.35 → 1.0 (t=0.18) → 0 |
| 회전 | z ∈ [−π, +π] rad·s⁻¹ |
| **색상 곡선 (신규)** | 알파 1.0 → 1.0 (t=0.45) → 0.0 |

**원소별:**

| 시스템 | 색상 (RGBA) | 사이즈 | 수명 | 속도 | 중력 | 셰이프 R | 두께 |
|---|---|---|---|---|---|---|---|
| `BoltSparks` (볼트 관통) | 0.75, 0.55, 1.00, 0.90 | 0.050 | 0.35 s | +2.6 | **0.35** | 0.12 | 1 |
| `PulseRipple` (파동 틱) | 0.35, 0.85, 0.50, 0.80 | 0.045 | 0.50 s | +1.4 | **0.0** | 0.12 | 1 |
| `NovaDebris` (노바 낙하) | 0.953, 0.42, 0.20, 0.90 | 0.060 | 0.70 s | +3.2 | **0.9** | 0.12 | 1 |
| `AegisFlash` (흡수) | 0.56, 0.85, 1.00, 0.85 | 0.050 | 0.40 s | **−2.0** | **0.0** | **0.55** | **0** |

**Emit 수 (통상 / reduced-motion — 전부 정확히 절반):**

| 호출 지점 | 통상 | reduced |
|---|---|---|
| `VfxDirector.cs:386` NovaCast → NovaDebris | 26 | 13 |
| `VfxDirector.cs:468` WardCast → AegisFlash | 12 | 6 |
| `VfxDirector.cs:569` PylonDown → NovaDebris | 18 | 8 |
| `VfxDirector.cs:847` BoltCast → BoltSparks | 14 | 7 |
| `VfxDirector.cs:1931` Pulse 0.5 s 틱 → PulseRipple | 10 | 5 |

> `PylonDown` 18→8만 정확한 절반이 아니다(9가 아님). 기랜딩 값이며 이번 범위에서
> 건드리지 않았다. [OBSERVED]

**기존 문법 보존:** LineRenderer 링/스파크/크랙 팬/eruption crown/쿼드 스코치는
전부 그대로다. 파티클은 증강이다. [OBSERVED]

---

## 3.5 W16-적용 — 지형 애니메이션 (플립북 UV 데칼)

### 3.5.1 조사 결과 — 브리핑의 "화산·빙하" 축은 이 게임에 존재하지 않는다 [OBSERVED]

`Assets/Resources/Terrain/`와 `StageCatalog.AllEntries`를 실측했다.

1. **지형 프리팹은 스테이지별이 아니다.** `StageCatalog.DressingLibraryTerrainId
   = "cinder-span"` — 9개 스테이지 전부가 **하나의** 드레싱 라이브러리를 공유한다.
   `Resources/Terrain/`에 프리팹이 3개(`cinder-span`/`ember-gallery`(부재)/…) 있으나
   런타임이 로드하는 것은 `terrain-cinder-span` 하나뿐이다.
2. **빙하 스테이지가 없다.** 9개 스테이지는 전부 재/불씨/메아리 계열이다
   (재의 다리, 불씨 회랑, 서약의 성당, 증언의 우물, 메아리 왕좌, 재의 판결,
   재의 수문, 불씨 요새, 재의 행진). 화산 대 빙하라는 축 자체가 없다.
3. **대신 실재하는 축은 스테이지 액센트 색의 한난이다.** 이것이 이미 조명
   (`StageMood`), 바닥 틴트, 보스 색을 모두 구동한다.

그래서 3계열을 **액센트 온도**에 매핑했다. 이것이 이 저장소에 실재하는 축에
맞는 정직한 해석이고, 브리핑의 의도(화산=따뜻/빙하=차가움/이동=중립)를 보존한다.

| 스테이지 | 액센트 | 바닥 warmth (r−b) | 계열 |
|---|---|---|---|
| cinder-span | #F25A2B | +0.217 | **Lava** |
| ember-gallery | #F26E33 | +0.208 | **Lava** |
| abyss-chancel | #8F66FF | −0.150 | **Ice** |
| witness-well | #73C7FF | −0.183 | **Ice** |
| echo-throne | #73C7FF | −0.183 | **Ice** |
| ash-verdict | #DEC768 | +0.121 | **Lava** |
| cinder-sluice | #3FA8C8 | −0.179 | **Ice** |
| ember-bastion | #E88A2E | +0.202 | **Lava** |
| ash-march | #B8B0A4 | **+0.006** | **Shift** |

중립 밴드 ±0.05. ash-march만 밴드 안(+0.006)이고 나머지는 최소 0.121로 여유가
크다 — 테스트가 이 마진을 고정한다.

### 3.5.2 자산 계약 (asset-lane 매칭 필요) [TARGET]

| 항목 | 기대 스펙 |
|---|---|
| 경로 | `Assets/Resources/Terrain/terrain-fx-{lava,ice,shift}-sheet` |
| 그리드 | **4×4 = 16 프레임**, 균등, **행 우선, 좌상단이 프레임 0** |
| 해상도 | ≤1024 (256 px 셀) |
| 내용 | **그레이스케일/알파 패턴만.** 색은 런타임이 스테이지 틴트로 입힌다 |
| 임포트 | wrapMode **Clamp** 권장 (셀 경계 bilinear 블리드 방지). 코드가 강제하지 않는다 — 공유 애셋 임포터 설정을 런타임이 덮어쓰는 것은 다른 사용처를 깨뜨린다 |

> **"그레이스케일 시트 + 스테이지 틴트" 분리가 이 설계의 핵심이다.** 3장으로
> 9개 스테이지를 덮으면서 어느 스테이지도 이질적인 팔레트로 읽히지 않는다.
> abyss-chancel의 보라색 "ice"는 문자 그대로의 빙하가 아니라 스테이지 색을 입은
> 결정 표류로 읽힌다.

**시트 부재 시 완전 무동작**: `Resources.Load` null → 머티리얼 null → 데칼 생성
0개 → `Animate` 즉시 반환. 3계열 전부 부재면 컴포넌트가 스스로 `enabled = false`.

### 3.5.3 배치 규칙 — 구조적 판별, 위치 계산 아님

`EnvironmentBuilder`는 **두 종류**의 자식에게 `env-floor-NNN` 이름을 준다:

| | 앰비언트 바닥 슬래브 | 해저드 링 가구 |
|---|---|---|
| 이름 | `env-floor-000`~ | `env-floor-500`~ |
| 자식 | **`piece-NN`** (코드 쿼드) | **`part-NN`** (라이브러리 클론 메시) |
| 배치 필터 | `NearAnyHazard(FloorSkipMargin)` **통과** | 해저드를 **둘러싸도록** 배치 |

따라서 판별자는 이름 접두사가 아니라 **자식의 종류**다 (`TerrainFlipbook.PanelOf`).
이 규칙의 안전 속성이 여기서 따라 나온다 — 앰비언트 슬래브는 이미 모든 텔레그래프
디스크로부터 이격되어 배치되므로, **엠버색 lava 플립북이 벤트 경고를 위장할 수
없다.** 이것이 `EnvironmentBuilder`가 자신의 틴트 주석에서 "보이지 않는 것보다
나쁜 실패"라고 지목한 바로 그 실패 모드다.

### 3.5.4 함정 2건 — 실측으로 잡았다 [OBSERVED]

1. **`StaticBatchingUtility.Combine`이 `sharedMesh`를 갈아치운다.**
   `EnvironmentBuilder.Build`는 마지막에 Combine을 호출하고, 이는 모든 MeshFilter의
   `sharedMesh`를 **결합 메시**로 교체한 뒤 서브메시 범위를 렌더러로 옮긴다.
   초안은 패널의 메시를 재사용했는데 — 그랬다면 바닥 타일 한 장 위에 **결합된
   스테이지 전체가 그려진다.** 수정: `env-quad`와 정점 단위로 동일한 XZ 쿼드를
   자체 저작 (`TerrainFlipbook.QuadMesh`).
2. **바닥 틴트는 공유 머티리얼에 없다.** `_floorMaterial = MakeLit(Color.white, null)`
   — §E7 머티리얼 예산 때문에 스테이지 색은 **렌더러별 MaterialPropertyBlock**의
   `_BaseColor`에 있다. 초안은 `sharedMaterial.color`를 읽었는데, 그것은 모든
   스테이지에서 **흰색**이라 9개 스테이지가 한 계열로 붕괴했을 것이다. 수정:
   `TryFloorTint`가 `GetPropertyBlock`으로 읽는다 (읽기용 스크래치 블록은 애니메이션용
   `_block`과 **분리** — 같은 블록을 쓰면 바닥의 `_BaseColor`가 데칼로 딸려 간다).

### 3.5.5 변경 파일

| 위치 | 내용 |
|---|---|
| `Assets/Scripts/View/TerrainFlipbook.cs` | **신규 전체** (자기완결 드라이버 + 애니메이터) |
| `Assets/Tests/EditMode/TerrainFlipbookTests.cs` | **신규** 7 테스트 |

**기존 파일 수정 0건.** `GameDirector`/`SceneBuilder`/`EnvironmentBuilder` 무수정,
**GameView 훅도 없다.** 드라이버는 `[RuntimeInitializeOnLoadMethod]`로 자체 부팅하고
`StageEnvironment` 루트를 0.5 s 간격 폴링으로 발견한다. 이 파일 하나를 지우면 기능이
완전히 사라지며 다른 어떤 줄도 건드릴 필요가 없다.

### 3.5.6 파라미터 [TARGET]

| 항목 | 값 | 근거 |
|---|---|---|
| 재생 | 12 fps (16 프레임 = 1.333 s 루프) | 스트로브로 안 읽힐 만큼 길고, 1024에 256 px 셀이 들어갈 만큼 짧다 |
| 데칼 상한 | **8** | 레이아웃이 6~10장을 내므로 최악을 묶으면서 스테이지가 비지 않는다 |
| 리프트 | 0.006 world | 55° 피치에서 z-fighting은 이기고, 떠 있는 카드로는 안 읽힌다 |
| 알파 | Lava 0.32 / Ice 0.30 / Shift 0.22 | 해저드 텔레그래프 밴드(0.5~1.0) **아래**로 고정 — 테스트가 강제 |
| 블렌드 | Lava·Ice = **가산**, Shift = **알파** | 용암·서리는 발광 현상, 표류하는 재는 아니다 |
| 위상 | 패널 좌표 해시 | 결정론적이며 전체가 한 몸처럼 맥동하지 않는다 |
| 머티리얼 | 계열당 1개 (스테이지당 1개만 생성) | 시드 클론(`MakeUnlit`/`MakeAdditive`)만 |

**per-frame 할당 0**: `MaterialPropertyBlock` 1개 재사용, `Vector4`는 스택 구조체,
프레임 동일성 조기 반환으로 60 Hz Update가 데칼당 초당 60회가 아니라 **12회만**
`SetPropertyBlock`을 호출한다.

**§E6/§E7 예산 무영향**: 데칼은 `StageEnvironment`가 **아니라** 별도 루트
`StageTerrainFx` 아래에 산다 — `StageMood`가 별도 루트인 것과 동일한 근거
(`EnvironmentBuilderTests.Budget_VerticesMaterialsAndLights`가 그 루트의 머티리얼·
라이트 수와 자식 이름 어휘를 게이팅한다). 장식 레이어가 그 게이트를 깨서는 안 된다.

### 3.5.7 reduced-motion

**프레임레이트 절반(6 fps).** 브리핑이 허용한 두 선택지 중 "감소"를 택했다 —
이 레이어는 정보량이 0인 앰비언트 지면 텍스처라 접근성 목표는 *모션 감소*이고,
완전 정지는 레이어의 존재 이유 자체를 없앤다.

---

## 4. reduced-motion 폴백 목록

| 대상 | reduced-motion 동작 |
|---|---|
| W9 flourish (FOV+롤) | **전면 비활성** (`Flourish` 첫 줄 조기 반환). 이벤트 경로도 동일 |
| 벤트 링 텔레그래프 | 6 Hz 진동 → **알파 1.0 고정** (신규 수정) |
| 벤트 fill 임박도 | 성장 유지(단조 램프, 진동 아님) — 정적 표시 계약 만족 |
| 원소 파티클 5개 호출 지점 | Emit 수 절반 |
| W16 지형 플립북 | 12 fps → **6 fps** (정지 아님 — §3.5.7) |
| 기존 셰이크 / `Punch` / `FocusPulse` | 기존대로 비활성 (미변경) |

---

## 5. 절대 제약 준수 확인 [OBSERVED]

- VFX Graph / compute / 스레드: 미사용. 빌트인 `ParticleSystem`만.
- 신규 머티리얼: `MakeUnlit` / `MakeAdditive` / `MakeParticleAdditive`(시드 클론) 경유만.
- `ViewWorld.Scale = 0.0125f` 불변.
- `Assets/Scripts/Sim/` **무수정** (`git status` 상 내 변경 없음).
- per-frame 할당: `Gradient`는 Awake 1회 생성. Emit 경로 무할당.
- **GameView.cs 수정 0줄.**
- 금지 파일 무수정: `SceneBuilder.cs` / `EnvironmentBuilder.cs` / `graphify-out/*` /
  `HudView*.cs` / `StageCatalog.cs` / `CampaignStore.cs` / 로비 UI / `tools/audio/**` /
  `tools/blender/**`.
- Git: 커밋·스테이징·push 전부 미수행. 워킹트리 변경만.

**이 레인이 변경한 파일 전체 (7개):**

```
M Assets/Editor/RuntimeMaterialSeeds.cs           V3 시드
M Assets/Scripts/View/CameraRig.cs                W9   (324c26b로 타 세션이 커밋함)
M Assets/Scripts/View/VfxDirector.cs              V2 + V3
M Assets/Scripts/View/ViewWorld.cs                V3 시드 클론 헬퍼
? Assets/Scripts/View/TerrainFlipbook.cs          W16-적용 (신규)
? Assets/Tests/EditMode/CameraFlourishTests.cs    W9 테스트 (신규)
? Assets/Tests/EditMode/TerrainFlipbookTests.cs   W16 테스트 (신규)
```

W16은 **기존 파일 수정 0건**이다 — `GameDirector`/`SceneBuilder`/
`EnvironmentBuilder` 무수정, GameView 훅 없음 (§3.5.5).

---

## 6. 검증

### 6.1 수행한 검증 [OBSERVED]

Unity 배치모드 실행이 금지되어 있으므로, **실제 Unity/URP 어셈블리를 참조하는
대역외 Roslyn 컴파일**로 세 어셈블리를 전부 검증했다. 소스는 저장소 원본 그대로,
참조는 `*.csproj`의 `HintPath` 전량 + `Library/ScriptAssemblies/*.dll`.
스크래치 디렉터리에서만 빌드했고 저장소에는 산출물을 남기지 않았다.

| 어셈블리 | 결과 |
|---|---|
| `CinderCourt.View` (36 소스 + Sim, 422 참조) | **Build succeeded, 0 errors** |
| `Assembly-CSharp-Editor` (17 소스, 435 참조) | **Build succeeded, 0 errors** |
| `CinderCourt.Tests.EditMode` (신규 테스트 2종 포함, 438 참조) | **Build succeeded, 0 errors** |

URP 파티클 셰이더 프로퍼티명 9종을 실제 패키지 셰이더 파일에서 대조 확인 (§3.1).
W16의 두 함정(§3.5.4)은 `EnvironmentBuilder` 소스 실독으로 확인했다 —
Combine 호출 지점과 `_floorMaterial = MakeLit(Color.white, null)` + MPB 틴트 경로.

### 6.2 신규 EditMode 테스트

`Assets/Tests/EditMode/CameraFlourishTests.cs` — 7개 테스트. 취향이 아니라
**바운드 계약**만 고정한다.

1. `Flourish_ClampsEveryChannelWhateverIsRequested` — `Flourish(400, −400, 30)`도 상한 내로. 부호 의미 보존.
2. `Flourish_IsRefusedEntirelyUnderReducedMotion` — API·이벤트 양 경로 + FOV 무변화.
3. `Flourish_WeakerRequestCannotCutAStrongerOneShort` — 비-스톰프.
4. `Flourish_StaysInsideTheBudgetAcrossTheWholeEnvelope` — 엔벨로프 21개 지점 스윕.
5. `Flourish_ReachesFullStrengthAtThePeakAndReturnsToZero` — 피크 실효성 + 정확 복원.
6. `Flourish_DoesNotSurviveAProfileSwitch` — 로비가 줌인된 채 열리지 않을 것.
7. `Flourish_ScalesDownWhileAShakeIsCarryingTheFrame` — 합성 클램프 (합산 아님, 상쇄도 아님).

`Time.deltaTime`이 EditMode에서 통제 불가라 타이머를 직접 심고 1회 감산을
보상한다 — 프레임레이트 독립. `RenderSettings` fog(전역)와 reduced-motion pref를
SetUp/TearDown에서 스냅샷·복원한다.

`Assets/Tests/EditMode/TerrainFlipbookTests.cs` — 7개 테스트.

1. `Placement_SelectsAmbientSlabsAndRejectsHazardFurniture` — `piece-` vs `part-`
   판별. 해저드 링 가구를 장식하면 벤트 텔레그래프를 감싼 바위에 애니메이션 엠버가
   올라간다.
2. `TintSource_SurvivesTheRealBuildIncludingStaticBatching` — **9개 스테이지를
   실제로 빌드**해 MPB 틴트를 읽고 계열을 대조. §3.5.4의 두 함정이 재발하면 여기서
   터진다.
3. `ThemeBand_KeepsEveryStageClearOfTheNeutralEdge` — 밴드 경계 마진 측정.
4. `FrameWindow_TilesTheSheetExactlyOnceWithNoOverlap` — 16셀 완전 분할.
5. `FrameWindow_FrameZeroIsTheTopLeftCell` — UV 원점이 좌하단인데 시트는 좌상단
   우선이라, 뒤집히면 눈으로만 잡힌다.
6. `Sheets_EveryPlayableThemeHasItsOwnDistinctPath`
7. `Tint_HoldsAmbientAlphaBelowTheTelegraphBand` — 알파가 텔레그래프 밴드로
   올라가지 못하게 고정.

### 6.3 **후속 검증 필요 항목 (미수행)**

이 레인은 Unity를 실행하지 않았다. 오케스트레이터 통합 실행 시 필요:

1. **EditMode 전량 실행** — 특히 신규 `CameraFlourishTests` 7건. 컴파일은 검증했으나
   **런타임 실행은 미검증**이다.
2. **`RuntimeMaterialSeeds.Seed()` 1회 실행** →
   `Assets/Resources/Materials/particle-additive-seed.mat` 생성 확인.
   미생성 시 파티클은 조용히 기존 경로로 강등되며(회귀 아님), 콘솔에
   `[MaterialSeeds] particle-additive-seed ready` 로그 부재로 식별 가능.
3. **WebGL 빌드 후 파티클 렌더 확인** — 시드 계약의 실제 목적. 분홍/불투명 여부.
   §3.1 계약이 옳다면 정상이어야 하나 **실측 전이다.**
4. **데스크톱 스모크 스크린샷** (스펙 §Lane V 인수 조건):
   - 피니셔 순간 FOV 펀치 — 텔레그래프 링 왜곡 없음 확인
   - 보스 페이즈 전환 — 셰이크와 동시 발생 시 합성 클램프 체감
   - 벤트 텔레그래프 — 링/fill 경계 대비, reduced-motion 정지 상태
   - 4종 원소 임팩트 — 특히 에이기스 수렴 방향
5. **`_placedRotation` 회귀 확인** — Arena 프로파일에서 `Awake`가 `Camera.main` 부재로
   조기 반환하면 `_baseRotation`이 영 쿼터니언이 된다(기존 동작). W9는 이 경우
   `_placedRotation` 필드 이니셜라이저(identity)에 의존하므로 신규 위험은 아니나,
   Arena 프로파일 스모크에서 롤 이상 유무를 확인해 두면 좋다. [INFERENCE]
6. **성능**: 16.67 ms/frame 목표. 파티클 4×96 상한 + LineRenderer 기존 부하
   + W16 데칼 8장(각 4정점, 투명 패스). V4(URP 포스트)는 이번 범위 제외 —
   프로파일 게이트 선행 필요(스펙 §V4).

**W16 전용 후속 검증:**

7. **`Renderer.HasPropertyBlock()` / `GetPropertyBlock()`이
   `StaticBatchingUtility.Combine` 이후에도 스테이지 틴트를 돌려주는지** — 이것이
   W16의 **단일 최대 미검증 가정**이다. 소스상 Combine은 메시만 건드리고 프로퍼티
   블록은 손대지 않지만 **실행으로 확인하지 못했다.** 실패 시 9개 스테이지가 한
   계열로 붕괴하며, `TintSource_SurvivesTheRealBuildIncludingStaticBatching`이
   정확히 그 지점에서 실패한다.
8. **시트 3종 전달 후** 그리드가 4×4인지 확인. 다르면
   `TerrainFlipbook.GridCols/GridRows/FramesPerSecond` 3개 상수만 고치면 된다
   (asset-lane과 §3.5.2 표를 매칭).
9. **데칼 렌더 스모크** — z-fighting, 패널 밖으로 넘치지 않는지, 55° 피치에서
   지면으로 읽히는지. 특히 가산 블렌드 lava가 벤트 텔레그래프와 혼동되지 않는지
   (배치상 이격되어 있으나 **눈으로 확인 필요**).
10. **드라이버 수명** — 로비↔던전 왕복 시 데칼 생성/파괴가 누수 없이 도는지.
    `TerrainFlipbookDriver`는 `DontDestroyOnLoad`라 씬 전환에 살아남는다.

### 6.4 미해결 / 사람 판단 항목

- §2.2 fill 색상·알파 램프 값은 **측정 없는 [TARGET]**이다. 근거는 색조 분리 논리이며
  실제 플레이 프레임 대비 측정은 하지 않았다. QA 밴드 6(콘트라스트) 재측정 권장.
- §1.3 이벤트별 FOV/롤 값도 전부 [TARGET]. 스모크 후 튜닝 여지 있음.
  바운드(§1.3 상한 표)는 계약이고, 이벤트별 요청값은 취향이다.
- `PylonDown` Emit 18→8 (절반=9 아님)은 기랜딩 값으로 두었다. 의도인지 확인 필요.
- **W16의 "화산·빙하" 해석은 사람 판단 항목이다.** §3.5.1대로 이 저장소에는 빙하
  스테이지가 없고 지형 라이브러리도 스테이지별이 아니다. 액센트 온도 매핑은
  실재하는 축에 맞춘 해석이지 브리핑의 문자 그대로가 아니다. 진짜 화산/빙하
  스테이지를 원한다면 그것은 **스테이지 콘텐츠 결정**(신규 액센트·해저드·드레싱
  라이브러리)이지 VFX 레인 작업이 아니다.
- W16 알파·색조 램프 값(§3.5.6)은 전부 측정 없는 [TARGET]. 스모크 후 튜닝 여지.
- 데칼 상한 8과 12 fps도 [TARGET]. 프로파일 결과에 따라 조정 가능.
