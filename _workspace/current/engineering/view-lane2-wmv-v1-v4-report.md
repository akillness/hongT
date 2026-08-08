# View 레인 2 리포트 — W-MV 게이트 활성화 (MV-2~MV-6) / V1 시전 동기화 / V4 URP 포스트 + 프레임 워치독

**작성**: 2026-08-08 · **레인**: View(2) · **브랜치**: `akillness/main` (워킹트리, 미커밋)
표기 규약은 CLAUDE.md §4 (`[OBSERVED]` / `[INFERENCE]` / `[TARGET]`).

---

## 0. 요약

- **W-MV 게이트 ON.** `GameView.DungeonProgression`(신규, 뷰 단일 출처)이
  `DungeonProgressionConfig.Everything` = #13 + #14 + #15. 벽 링·카메라 추종
  창·재의 벽 커튼이 전부 **심이 클램프할 반축**을 따라간다.
- **핵심 실측 반전 — 시드 문서의 MV-2 지시("심 스냅샷을 EnvironmentBuilder에
  주입")는 순서상 불가능하다.** `GameDirector.SetStageEnvironment(entry.Id)`는
  `_game.Begin(config, …)`보다 **먼저** 호출된다(`GameDirector.cs:434` vs `:437`
  — 개정 전 라인). 환경이 지어질 때 심 인스턴스는 아직 없다. 팀리드가 허용한
  대안대로 **`DungeonBoundsSpec.Resolve`를 양쪽이 함께 통과**시켜 같은 값을
  보장한다(§1.2).
- **MV-4 판정: 궤도 거리 17.5 / 21.5는 유지, 추종 클램프만 확장.** 시드 문서가
  말한 "20 / 24.5"는 **이미 낡았다** — 다른 레인이 프레임 단색화(24-step 지배
  버킷 61%) 대응으로 17.5 / 21.5로 당겨놓았다(`CameraRig.cs:97-98`). 해석적
  프레이밍 산술상 확장 플레이필드에서도 플레이어는 화면을 벗어나지 않는다(§3).
- **MV-5에서 시드 문서에 없던 실결함 1건 발견·수정**: Zone C **북측 림 테라스**
  (sim y −70..270, +0.8 u 융기 데크)가 확장 링(y 204)·플레이어 도달(y 212)과
  **겹친다**. 프리즘 충전용 장식 위를 플레이어가 걸어 들어가게 된다. 두 y축
  테라스가 링 성장분만큼 안쪽 변을 후퇴시키도록 고쳤다(§2.4).
- **MV-6에서 두 번째 하드코딩 발견**: `VfxDirector.WallSpanWorld`가
  `ArenaHalfHeight` 상수였다. 재의 벽은 심에서 **전 높이 압살 판정**이므로
  540 px 커튼은 확장 플레이필드에서 296 px 모자라 **회피 가능해 보이지만 실제로는
  아닌 틈**을 그린다. 게시값 추종으로 전환(§2.5). 미니맵은 **존재하지 않는다**
  (검색 결과 HudView의 `520` 리터럴은 전부 UI 픽셀 폭).
- **V1은 이미 절반 구현되어 있었다** (`ActorView.FlashCastGlow` — vfx 레인 선행).
  결손 2건을 메웠다: **reduced-motion 게이트 없음**, **Attack/Critical 액션 프레임
  미연결**(스킬 시전 이벤트만 물려 있었음). §4.
- **V4도 절반 있었다** (`PostFxGate` 모바일 정적 차단 + `SceneBuilder`의 볼륨
  프로파일). 결손 2건: **런타임 워치독 없음**, **던전 한정 아님**. 둘 다 구현.
  프로파일 sub-asset 수정 이후 포스트가 *처음으로 실제 렌더된다*는 점 때문에
  던전 한정화는 선택이 아니라 필수였다(§5.2).
- **검증**: 대역외 Roslyn 컴파일 4개 어셈블리 **0 error**. 기하는 Python으로
  수식을 그대로 재현해 수치 확인. **Unity 미실행**(지시대로) — 최종 PASS는
  오케스트레이터 실측.

---

## 1. MV-2 — 데이터 흐름

### 1.1 변경 전 [OBSERVED]

`EnvironmentLayout`(EnvironmentBuilder.cs 하단, 순수 레이아웃 코어)의
`const double HalfW = SimConfig.ArenaHalfWidth` / `HalfH = ArenaHalfHeight`,
`StopE = (ArenaHalfWidth − EnemyMarginClamp) / HalfW`. **컴파일 타임 상수**라
심이 554 × 418로 클램프해도 링은 520 × 270에 남는다 → 플레이어가 벽을 통과한다.

### 1.2 변경 후 — 실제 순서와 단일 출처

```
GameDirector.SetStageEnvironment(entry.Id)        ← 환경 빌드 (먼저)
  └ GameView.DungeonPlayfield(out hw, out hh)     ← GameView.cs:98
      └ DungeonBoundsSpec.Resolve(GameView.DungeonProgression.Bounds, …)
  ├ EnvironmentBuilder.Build(stageId, hw, hh)     ← GameDirector.cs:259
  ├ _rig.SetPlayfield(hw, hh)                     ← :262   (MV-4)
  ├ VfxDirector.SetPlayfield(hw, hh)              ← :263   (MV-6)
  └ PostFxGate.SetStageActive(true)               ← :265   (V4)

GameDirector … _game.Begin(config, …)             ← 심 생성 (나중)
  └ new CinderSim(in config, GameView.DungeonProgression)   ← GameView.cs:249
      └ DungeonBoundsSpec.Resolve(config.Bounds, …)  (심 생성자, 1회 해석)
```

**두 경로가 같은 `DungeonProgressionConfig` 필드를 같은 `Resolve` 함수에 넣는다.**
스냅샷을 읽지 않는 대신 *결의 함수를 공유*하는 방식이며, 이것이
`DungeonBoundsViewSyncTests.TheViewAndTheSimResolveTheSameHalfAxes`가 고정하는
불변식이다. 심 스냅샷(`BoundsHalfWidth`/`BoundsHalfHeight`)은 런타임 진단용으로
남고, 뷰 기하의 입력은 아니다 — 존재하지 않는 값을 읽을 수 없기 때문이다.

**비던전 경로 불변의 구조적 근거 3중**:
1. `SetStageEnvironment`는 던전 라우트에서만 stageId를 받는다(나머지 4개 호출은
   전부 `null`, `GameDirector.cs:123/286/302/364`). null 분기는 동결값으로
   되돌린다(`:245-249`).
2. `EnvironmentBuilder.Build(stageId)` 1인자 오버로드(`:87`)가 동결 쌍을 전달 —
   아레나/프롤로그/로비/훈련장·기존 테스트 전부 이 경로.
3. 심은 `Mode != Dungeon`이면 progression을 통째로 낙하한다(sim 레인 D3).

### 1.3 변경 파일 — MV-2 / MV-3

| 파일:라인 | 변경 |
|---|---|
| `Assets/Scripts/View/EnvironmentBuilder.cs:49-60` | `EnemyStopE`/`PlayerStopE`는 **동결 파생 유지**(§E8 계약 상수), 신규 `EnemyStopEFor(hw)`/`PlayerStopEFor(hw)`가 `DungeonBoundsSpec`에 위임 (MV-3) |
| `:87-88` | `Build(stageId)` → 동결 쌍 위임 |
| `:98` | 신규 `Build(stageId, halfWidth, halfHeight)` |
| `:592-613` | `const HalfW/HalfH` → `static _halfW/_halfH`(기본 동결) + `internal HalfW/HalfH` 프로퍼티, `StopE`는 활성 반축 파생 |
| `:964-978` | `Compute(stageId)` 오버로드 + `Compute(stageId, hw, hh)` — **첫 두 문장이 반축 대입**, 축소 요청은 심과 같은 규칙으로 거부 |
| `Assets/Scripts/View/GameView.cs:91-99` | `DungeonProgression`(단일 출처) + `DungeonPlayfield(out,out)` |
| `Assets/Scripts/View/GameView.cs:249` | `.All` → `DungeonProgression`(= `.Everything`) — **게이트 ON** |
| `Assets/Scripts/View/GameDirector.cs:243-265` | null 분기 동결 복원 + 던전 분기에서 반축 전파 |

**정밀도 주의 [OBSERVED]**: `StopE`를 `DungeonBoundsSpec.EnemyStopE(float)`로
직접 부르면 float 왕복 때문에 동결 입력에서 `0.95384615384615383`(double) 대신
`0.95384615659713745`가 나와 링 전 모듈이 ~1e-9 e 어긋난다. 레이아웃 코어는 전부
double이므로 **같은 식을 double로 평가**하도록 남겼다(`:613`, 주석에 명시).
동결 입력에서 개정 전과 **문자 그대로 같은 식**이다.

---

## 2. MV-4 / MV-5 / MV-6

### 2.1 [OBSERVED] 확장 기하 (Python으로 구현 수식 그대로 재현)

| | 반축 | x 범위 | y 범위 |
|---|---|---|---|
| 동결 링(StopE 0.9538462) | 496 × 257.54 | 272.00..1264.00 | 346.46..861.54 |
| 확장 링(StopE 0.9566787) | 530 × 399.89 | 238.00..1298.00 | **204.11..1003.89** |
| 확장 플레이어 도달 | 520 × 392.36 | **248.00..1288.00** | 211.65..996.35 |

플레이트(sim 0..1536 × 0..1024) 안에 링 4변 전부 ✓. 플레이어 x가 기믹 span
248..1288과 **정확히 일치** ✓ (sim 레인 §6-B.4 재확인).

### 2.2 MV-5 — Zone A 바닥 패널

동결 후보 행은 y 604 및 604±108, 즉 **정지 반경 257.5 중 |dy| ≤ 108 (42%)**만
덮는다. 확장 링(정지 반경 400)에서는 그대로 두면 중앙 1/3만 무늬가 있다.

두 단계로 고쳤다:

1. **표를 절대 좌표 → 중심 오프셋 × 반축 배율로 재작성**
   (`EnvironmentBuilder.cs:1176-1178, 1199-1213`). 동결에서 배율이 정확히
   `1.0`이므로 `768 + (−368)×1.0 = 400.0` — 기존 리터럴을 비트 단위로 재현한다
   [OBSERVED, Python: mid row `[400.0, 496.0, …, 1136.0]`, outer row y `496.0/712.0`].
2. **확장 전용 추가 행 2개** (`:1250-1275`, `AddFloorExpansionRows`).
   정지 반높이의 ±0.72, x는 그 행의 타원 반현에서 패널 최악 반폭 75 px을 뺀 뒤
   0.88을 곱한 범위에 5슬롯. **`_halfH > ArenaHalfHeight`일 때만 실행**되므로
   동결 후보 목록은 손대지 않는다.
   [OBSERVED] 확장 시 행 y = 316.08 / 891.92, 최악 패널 모서리 e = **0.9552**
   ≤ StopE 0.9567 → §E3 "전부 e ≤ EnemyStopE 내접" 유지.
3. `want`(6..10)를 링 **면적비**로 스케일(`:1219-1221`). 확장 면적비 1.649 →
   9..16장. 같은 장수를 넓은 바닥에 뿌리면 밀도가 떨어지기 때문.

[TARGET] 커버리지: |dy| 도달 범위가 동결 42% → 확장 72%.

### 2.3 MV-4 — 카메라 판단 근거

**시드 문서의 "20 / 24.5"는 낡았다.** 현행은 `DungeonCalmDistance 17.5` /
`DungeonCrowdDistance 21.5`(`CameraRig.cs:97-98`). 다른 레인이 프레임의 24-step
지배 색 버킷 61% 대응으로 당긴 값이고, 그 판단에는 측정이 붙어 있다.

**[OBSERVED, 해석적 — pitch 55°, FOV 42, D 17.5]**

| 항목 | 값 |
|---|---|
| 초점 평면 가로 반폭 | 10.08 u |
| 초점에서 **먼 쪽** 지면 가시 | 11.22 u |
| 초점에서 **가까운 쪽** 지면 가시 | **6.46 u** ← 빡빡한 쪽 |

| | clampZ | reachZ | 최대 이탈 | clamp/reach |
|---|---|---|---|---|
| 동결 | 2.215 | 3.154 | 0.94 u | 0.702 |
| 확장(클램프 미조정 시) | 2.215 | 4.904 | **2.69 u** | 0.452 |
| 확장(**본 변경**) | 3.429 | 4.904 | **1.48 u** | 0.699 |

**판정**:
- **궤도 거리는 건드리지 않는다.** 최대 이탈 1.48 u ≪ 6.46 u — 확장
  플레이필드에서도 플레이어는 프레임 한가운데 근처에 남는다. 거리를 늘리면
  다른 레인의 측정 기반 당김을 프레이밍 이득 없이 되돌리는 셈이다.
- **추종 클램프는 반축에서 파생하도록 전환한다** (`CameraRig.cs:137-165`).
  `const` 그대로 두면 `0.55`/`0.75`라는 분수가 말하는 바가 거짓이 된다 —
  실효 비율이 0.70 → 0.45로 떨어지고 플레이어가 프레임 위/아래를 탄다.
- **알려진 대가(결함 아님)**: 플레이어가 한쪽 극단에 서면 **반대편 경계벽이
  화면 밖으로 나간다**. 1.7배 넓어진 바닥에서 추종 카메라가 하는 일 그대로다.
- 축소 요청은 동결값으로 클램프(심과 동일 규칙), 비던전 프로파일 전환 시 동결
  복귀(`:266-275`) — 정적 필드가 아레나/로비로 새지 않게.

### 2.4 MV-5 부수 발견 — Zone C 북측 림 충돌 (**시드 문서에 없음**)

`AddZoneC`의 4개 테라스 주석은 "링에서 멀리 떨어져 있다(가장 가까운 변 e ≥ 1.3)"
라고 단언하지만 **동결 링 기준으로만 참**이다.

| 테라스 | span | 확장 링 | 판정 |
|---|---|---|---|
| 북측 림 | y −70..**270** | 링 상단 y 204.1, 플레이어 도달 y **211.65** | **충돌** — +0.8 u 융기 데크 안으로 걸어 들어감 |
| 남측 에이프런 | y **1010**..1570 | 링 하단 y 1003.89 | 6 px 스침(플레이어는 996.35까지라 미도달) |

수정(`EnvironmentBuilder.cs:1602-1625`): 두 y축 테라스의 **바깥 변은 고정**
(프러스텀 경계라 안 움직인다), **안쪽 변만 링 성장분 `RingGrowthY()`(`:622-632`)
만큼 후퇴**. 동결에서 성장분이 정확히 0(두 항이 동일 식) → 1290/560, 100/340
리터럴 그대로 [OBSERVED]. x축 날개(x −1740..20 / 1500..3260)는 확장 링이
x 238..1298이라 손댈 필요 없음.

[OBSERVED] 확장 결과: 북측 림 y −70..**127.65**(링 204.1보다 안쪽 아님 ✓),
남측 에이프런 y **1024**..1570. 에이프런은 **플레이트 하단 1024에서 멈춘다** —
그 이상 후퇴시키면 플레이트 끝과 에이프런 사이가 맨 VoidFloor가 되고, 그것이
§E8 커버리지 게이트가 재는 유일한 값이기 때문(`PlateBottomY`, `:617`).

### 2.5 MV-6 — 하드코딩 반축 수색 결과

| 후보 | 판정 |
|---|---|
| `VfxDirector.cs:109`(구) `WallSpanWorld = ArenaHalfHeight * 2 * Scale` | **실결함.** 재의 벽은 심에서 전 높이 압살 — 커튼이 296 px 모자라면 *안전해 보이지만 안전하지 않은* 틈을 그린다. `:116-132`에서 게시값 추종 + 축소 클램프 + 테스트 seam |
| `CameraRig.cs:117-119` `FollowClampX/Z` | MV-4에서 처리 |
| `HudView.cs:1195/1197/1859/1860/1884` 의 `520` | **UI 픽셀 폭** — 무관. (그리고 HudView는 webgl-lane 소유라 무접촉) |
| 미니맵 | **존재하지 않음** — 저장소 전체에 미니맵 뷰 없음 |
| `StageCatalog.cs:277` 주석의 `520×270` | 기믹 span 설명 주석. 기믹은 FROZEN이고 확장은 정확히 그 span에 맞췄으므로 여전히 정확 |

---

## 3. V1 — 시전 동기화

### 3.1 실측: 이미 있었던 것 / 없던 것 [OBSERVED]

`ActorView.FlashCastGlow`(RightHand 본 앵커, 수렴 0.16→0.055 u, 0.12 s)와
`UpdateCastGlow`는 **이미 구현되어 있었고**, `GameView.cs:511`(구)에서 스킬
원소 이벤트로 호출되고 있었다. 결손:

1. **reduced-motion 게이트 없음.** 수렴·증광하는 구체는 이 pref가 억제하려는
   바로 그 부류다(GameView의 대시 잔상 선례가 같은 게이트를 쓴다).
2. **Attack/skill 중 skill만.** 스펙 문구는 "Attack/skill 액션 프레임".

### 3.2 변경 (`Assets/Scripts/View/ActorView.cs`)

| 라인 | 변경 |
|---|---|
| `:739-755` | `FlashCastGlow` = `ArmCastGlow` + §M/#4 캐스트 포즈. **포즈를 앵커 가드 밖으로 hoist** — 비휴머노이드 리그나 reduced-motion에서도 포즈는 나와야 한다(기존엔 RightHand 본이 없으면 포즈까지 조용히 취소됐다) |
| `:757-770` | `SwingGlowSeconds 0.10` / `SwingGlowColor`(ember #f3592c) / `_swingGlowArmed` |
| `:772-777` | `ArmCastGlow` — **reduced-motion 조기 반환** |
| `:234-244` | `SyncPlayer`: Attack/Critical **선행 에지**에서 1회 arm |
| `:1039-1043` | 풀 반납 시 에지 래치·글로우 소거 |

**0.10 s인 이유**: 심의 Attack 프레임은 ActionTime 0에 열리고 히트 윈도가
0.10에 열린다(바로 아래 스윙 트레일이 같은 수치를 읽는다). 0.10 s 수렴은
**심 자신의 프레임에 방출 에지를 정확히 얹는다** — "수렴 → 방출"의 방출이
판정 개시와 일치.

**`FlashCastGlow`를 그대로 쓰지 않은 이유**: 그 함수는 0.30 s 캐스트 **포즈**도
arm한다. 스윙마다 캐스트 포즈를 걸면 포즈가 스윙보다 오래 살아 회수 구간까지
시전 자세를 잡는다. V1은 심 프레임 앞의 장식이지 두 번째 포즈 권위가 아니다.

**판정 불변 [OBSERVED]**: 추가 코드는 `state.Action`을 **읽기만** 한다. 심 호출
0건, `SimEvents` 비트 추가 0건, `_swingTrail.emitting` 조건 무변경.

---

## 4. V4 — URP 포스트 + 프레임 워치독

### 4.1 실측: 이미 있었던 것 / 없던 것 [OBSERVED]

`Assets/Scripts/View/PostFxGate.cs`(모바일이면 `renderPostProcessing = false`)와
`SceneBuilder.BuildPostProfile`(Bloom 0.55/threshold 1.05, Vignette 0.22)이 이미
있었다. 결손 2건, 둘 다 게이트 문구("프로파일 수치 첨부 없이 PASS 불가")에 직결:

1. **런타임 워치독 없음** — 판단이 전부 빌드 타임/플랫폼 정적.
2. **던전 한정 아님** — 볼륨이 global이고 카메라 플래그가 항상 켜져 있다.
   그리고 `SceneBuilder.cs:285-291`의 주석이 스스로 적어둔 대로, Bloom/Vignette
   서브에셋이 `{fileID: 0}`으로 직렬화돼 **포스트는 지금까지 무동작이었다**.
   그 수정 이후 처음으로 실제 렌더되므로, global로 두면 **아레나·프롤로그·로비의
   외양이 아무도 요청하지 않은 채 바뀐다**(둘은 계약 동결 프레이밍). 던전
   한정화는 선택이 아니라 필수였다.

### 4.2 워치독 파라미터 [TARGET] (`PostFxGate.cs`)

| 상수 | 값 | 근거 |
|---|---|---|
| `FrameBudgetSeconds` | 1/60 (16.67 ms) | 스펙 §V4 게이트 |
| `WindowFrames` | 120 (≈2 s @60 Hz) | GC 히치 한 번이 창을 못 끌 만큼 길고, 웨이브 하나 안에 잡힐 만큼 짧게 |
| `OverBudgetFraction` | 0.05 | p95 정의 |
| `OverBudgetTrip` | **7** (= ⌊120×0.05⌋ + 1) | 6/120은 p95 *선상*이라 트립 금지, 7이 첫 초과 |
| `WarmupSeconds` | 3.0 | 씬 빌드·셰이더 워밍·최초 `StaticBatchingUtility.Combine`는 정상 상태 비용이 아님 |
| `HoldSeconds` | 1.5 | 이 안에 풀리면 히치이지 티어가 아님 |
| `StallCeilingSeconds` | 0.5 | **초과 샘플은 세지 않고 버린다** — 백그라운드 탭·alt-tab·동기 에셋 로드의 수 초짜리 delta는 렌더 비용이 아니며, 세면 포커스를 잃은 모든 빌드가 강등된다 |

**p95를 정렬 없이 재는 법**: "p95가 예산 초과" ≡ "창의 5% 초과가 예산 초과".
초과 **플래그** 링버퍼 + 러닝 카운트 1개로 **프레임당 O(1), Awake 이후 할당 0**.
매 프레임 float 120개를 정렬하는 워치독은 자기 오버헤드를 재게 된다.

**강등은 세션 내 단방향**: 조용해지자마자 되켜면 방금 나온 스톨로 다시 들어가
진동하고, 그건 효과가 없는 것보다 나쁘게 읽힌다.

**측정은 포스트가 실제로 켜져 있는 동안만** — 꺼진 상태로 잰 창은 효과가 쓸
여유를 "여유 있음"으로 보고한다. 기존 "desktop p95 10.0 ms" 수치가 정확히 그
실수를 이미 한 번 했다(무동작 프로파일을 측정).

**리포트 노출**: `PostFxGate.Current`(`OffByPlatform`/`Measuring`/`Holding`/
`Degraded`), `OverBudgetInWindow`, `SamplesInWindow`, `DebugLine`(전이 시에만
생성 — 매 프레임 문자열 결합 없음). 강등 시 `Debug.Log(DebugLine)`.
**HUD 디버그 배선은 하지 않았다** — `HudView*`는 webgl-lane 소유. §7 후속 항목.

### 4.3 던전 한정 배선

`PostFxGate.SetStageActive(bool)` ← `GameDirector.SetStageEnvironment`
(`:248` false / `:265` true). 이미 "던전 진입 / 그 외" 분기 위에 정확히 올라탄다.
스테이지가 바뀌면 워밍업·창을 리셋(이전 창의 샘플은 사라진 씬의 것).

### 4.4 최종 PASS는 이 레인이 내릴 수 없다

**본 레인은 실측 프레임 시간을 첨부하지 않는다.** Unity 실행이 금지되어 있고,
WebGL p95는 브라우저에서만 나온다. 여기서 한 일은 **게이트를 코드로 집행하고
그 판정을 보고 가능하게** 만든 것이다. §V4 PASS는 오케스트레이터의 라이브 빌드
스모크에서 `PostFxGate.Current`가 `Holding`으로 남고 `Degraded` 로그가 없음을
확인해야 성립한다.

---

## 5. 변경 파일 전체 (경로:라인)

### 수정

| 파일:라인 | 항목 |
|---|---|
| `Assets/Scripts/View/EnvironmentBuilder.cs:49-60` | MV-3 stop-line `*For` 오버로드 |
| `:87-98` | MV-2 `Build` 오버로드 |
| `:592-632` | 활성 반축 정적 + `StopE` + `PlateBottomY`/`TerraceMinDepth`/`RingGrowthY()` |
| `:964-978` | `Compute` 오버로드 |
| `:1170-1221` | MV-5 바닥 표 오프셋화 + 면적비 `want` |
| `:1250-1275` | MV-5 `AddFloorExpansionRows` |
| `:1602-1625` | MV-5 Zone C 테라스 후퇴 |
| `Assets/Scripts/View/GameView.cs:82-99` | `DungeonProgression` 단일 출처 + `DungeonPlayfield` |
| `Assets/Scripts/View/GameView.cs:249` | **게이트 ON** (`.All` → `.Everything`) |
| `Assets/Scripts/View/GameDirector.cs:243-265` | 반축·포스트 배선, 비던전 동결 복원 |
| `Assets/Scripts/View/CameraRig.cs:110-165` | MV-4 추종 클램프 파생화 + `SetPlayfield` |
| `Assets/Scripts/View/CameraRig.cs:266-275` | 비던전 프로파일에서 동결 복귀 |
| `Assets/Scripts/View/VfxDirector.cs:107-132` | MV-6 재의 벽 span 추종 + 테스트 seam |
| `Assets/Scripts/View/ActorView.cs:234-244, 739-777, 1039-1043` | V1 |
| `Assets/Scripts/View/PostFxGate.cs` (전면 재작성, 22행 → 222행) | V4 워치독 + 던전 한정 |

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/Tests/EditMode/DungeonBoundsViewSyncTests.cs` (+`.meta` guid `4f170f4d6f2040e78ec1216b12ed0902`) | W-MV 뷰 테스트 10종 |
| `Assets/Tests/EditMode/PostFxWatchdogTests.cs` (+`.meta` guid `e5afac0d715b449883b607c53e3bea5b`) | V4 워치독 테스트 6종 |

**무접촉**: `Assets/Scripts/Sim/**`, `HudView*`, `Assets/Plugins/WebGL/**`,
`tools/**`, `graphify-out/**`, `Data/**`, `Assets/Editor/SceneBuilder.cs`,
FROZEN 파일 전부. 커밋·스테이징·push 없음. 신규 머티리얼 0, VFX Graph 0,
compute 0, 프레임 루프 할당 0(워치독 링버퍼는 Awake 1회).

---

## 6. 추가 테스트 (16종)

### `DungeonBoundsViewSyncTests` — 동결 재현 4 / 확장 기하 6 (요구: 각 ≥2)

| # | 테스트 | 검증 |
|---|---|---|
| 1 | `FrozenHalfAxes_ReproduceTheConstantLayoutExactly` | **9스테이지 전부**, 암묵(1인자) vs 명시(3인자) 동결 레이아웃이 모듈별 이름·좌표·높이·yaw·조각수 **정확 일치**(`:R` 왕복 문자열, 허용오차 없음) |
| 2 | `FrozenHalfAxes_PinTheShippedFloorRowsAndTerraceSpans` | 바닥 행 y ⊆ {496, 604, 712}, 남측 에이프런 피벗 1290, 북측 림 100 |
| 3 | `FrozenHalfAxes_ReproduceTheShippedStopConstants` | MV-3 `*For(frozen)` == 무인자 프로퍼티 == SimConfig 파생 |
| 4 | `FrozenHalfAxes_LeaveTheCameraFollowWindowWhereItWas` | 동결 clampX/Z 핀 + **축소 요청 거부** |
| 5 | `ExpandedHalfAxes_MoveTheWallRingOutWithTheClamp` | 확장 타원 대비 모든 링 모듈 e가 확장 StopE 근방(밀림 예산 내). **동결 반축으로 남아 있으면 y축 모듈이 e 0.62로 잡혀 실패** |
| 6 | `ExpandedRing_StaysOnThePlateAndKeepsThePlayerInside` | 링 4변 ⊂ 플레이트, 플레이어 도달 ⊂ 링, 9스테이지 측정 밀림 ≤ 58 px |
| 7 | `ExpandedFloor_IsNotBareOutsideTheFrozenRows` | 확장 행이 실제로 배치됨 + 전 패널이 확장 정지 타원 내접 |
| 8 | `ExpandedRing_DoesNotCollideWithTheZoneCTerraces` | 북측 림 ≤ 링 상단 **및** < 플레이어 도달, 남측 에이프런 ≥ 링 하단 **및** ≤ 플레이트 하단(void 밴드 금지) |
| 9 | `ExpandedHalfAxes_ScaleTheCameraFollowWindowByTheSameFraction` | clamp/reach 비율 보존(±0.02), clamp < reach, 최대 이탈 < 6.46 u |
| 10 | `AshWallCurtain_SpansTheActivePlayfieldNotTheFrozenOne` | MV-6 동결 핀 + 확장 추종 + 축소 거부 |
| — | `TheViewAndTheSimResolveTheSameHalfAxes` | **게이트 검증**: 뷰/심 `Resolve` 결과 일치 + `Bounds.Active`·`AdaptiveWaves`·`GradedLoot` 전부 ON (누가 `.All`로 되돌리면 여기서 실패) |

TearDown이 레이아웃 코어·VfxDirector 정적을 동결로 복원하고, 리그 테스트는
`finally`에서 카메라 클램프를 복원한다 — `DungeonFramingAndMoodTests`에 확장
플레이필드가 새지 않도록.

### `PostFxWatchdogTests` — 6종

`TheBudgetIsTheSpecsSixtyHertzFrame` / `TheTripPointIsTheP95OfTheWindow`(6은
트립 안 함, 7은 함) / `APartialWindowNeverReachesAVerdict`(1..119 전량 스윕) /
`AHealthyWindowNeverBreaches` / `TheHoldWindowIsLongerThanAnySingleHitch` /
`TheStartingVerdictIsMeasuringNotHolding`.

**이 스위트가 하지 않는 일**: 실제 프레임 시간 측정. 순수 결정 함수
(`WindowBreaches`)의 산술만 고정한다 — 라이브 측정의 대체물이 아니다.

---

## 7. 검증 [OBSERVED]

**Unity 배치모드 미실행**(지시). 대신:

1. **대역외 Roslyn 컴파일** (`csc.dll` 4.10.0, 참조 = `*.csproj` HintPath 전량 +
   `Library/ScriptAssemblies/*.dll`, 소스는 저장소 원본, 스크래치에서만 빌드):

   | 어셈블리 | 결과 |
   |---|---|
   | `CinderCourt.Sim` (10 소스) | **0 error** |
   | `CinderCourt.View` (43 소스, 410 참조) | **0 error** (기존 경고 2건 — `HudView._skillRowRect`, `VfxDirector._playerTransform` — 무변) |
   | `Assembly-CSharp-Editor` (16 소스) | **0 error** |
   | `CinderCourt.Tests.EditMode` (신규 2종 포함) | **0 error** ※ |

   sim-lane2(보스 다양화)·webgl-lane(한글 IME)·asset-lane2의 **동시 변경이
   워킹트리에 들어온 상태에서** 컴파일했다 — 본 레인과 충돌 없음.

   ※ **다른 레인의 진행 중 상태 1건 [OBSERVED, 본 레인 소관 아님]**:
   `Assets/Tests/EditMode/MetaScreenLayoutTests.cs(98,19)`가
   `MetaScreenView.ApplyLayout`을 호출하는데 `Assets/Scripts/View/MetaScreenView.cs`
   에 그 멤버가 아직 없다 (`error CS1061`). 두 파일 모두 다른 레인이 이 세션에서
   동시 편집 중이며(`git diff --stat`: MetaScreenView +13, MetaScreenLayoutTests
   +74/−5), 본 레인은 어느 쪽도 건드리지 않았다. 이 한 파일을 제외하면 테스트
   어셈블리는 **0 error**다. 오케스트레이터가 통합 시점에 해당 레인의 완료
   여부를 확인해야 한다.

2. **기하 수치 검증**: 구현 수식(StopE, 반축 배율, `RingGrowthY`, 확장 행 반현,
   카메라 프러스텀 지면 범위)을 Python으로 그대로 재현해 §2.1/§2.2/§2.3 표의
   모든 수를 산출·대조. 동결 입력에서 리터럴 완전 재현 확인.

**하지 못한 것**: EditMode 실행, 렌더, 프레임 시간. 신규 16종은 **컴파일까지만**
확인됐다.

---

## 8. 오케스트레이터 후속 항목

| # | 항목 |
|---|---|
| O-1 | **EditMode 전량 실행.** 특히 `EnvironmentBuilderTests`(§E8 7행, 동결 경로라 불변이어야 함), `DungeonFramingAndMoodTests`, `DungeonGoldenDigestTests`(심 골든 — 뷰 변경은 심에 닿지 않음), 신규 16종 |
| O-2 | **§V4 최종 PASS.** 라이브 WebGL 빌드에서 던전 진입 후 `PostFxGate.Current == Holding`이 유지되고 `postfx: DEGRADED` 로그가 없음을 확인. 나오면 그 자체가 게이트 작동 증거이므로 **강등된 채 배포해도 계약 위반이 아니다** — 다만 리포트에 수치를 남길 것 |
| O-3 | **§E8 커버리지 게이트 재측정.** 테라스 후퇴는 동결 경로에서 no-op이지만, 던전 실행 프레임에서 남측 에이프런이 플레이트 하단에 정확히 맞닿는다. 32×32 그리드에서 seam sliver가 나오는지 확인(안 나오도록 설계했으나 미측정) |
| O-4 | **W-MV 체감 확인.** 넓어진 세로(506→802 px)에서 (a) 플레이어가 프레임 밖으로 안 나가는지, (b) 반대편 벽이 사라지는 것이 허용 가능한지 — §2.3의 "알려진 대가"에 대한 사람 판단 |
| O-5 | **HUD 워치독 노출.** `PostFxGate.Current`/`DebugLine`을 디버그 오버레이에 붙이는 것은 `HudView*`(webgl-lane 소유)라 하지 않았다 |
| O-6 | **D-B(초반 난이도 하향) 재확인.** 게이트 ON으로 sim 레인의 #13이 실제로 살아났다 — 웨이브 1-6 적 HP가 86 고정이고 스폰이 6→12로 는다. 디자이너/PM 판단 대기 항목이 이제 라이브다 |
| O-7 | **V1 스윙 글로우 시각 밀도.** 스윙마다(쿨 0.48 s) 0.10 s 글로우가 뜬다. 데스크톱 스모크에서 과한지 확인 — 과하면 `SwingGlowSeconds`/색 알파를 낮추는 것이 최소 조정 |
| O-8 | **다른 레인 미완 1건**: `MetaScreenLayoutTests` ↔ `MetaScreenView.ApplyLayout` 미싱 (§7 ※). 본 레인 소관 아님, 통합 전 확인 필요 |

## 9. Git 상태

커밋·스테이징·push **없음**. 파괴적 작업 없음. 다른 레인 소유 파일 무접촉
(`git status --short` 시작·종료 확인: 본 레인이 만든 변경은 ActorView·CameraRig·
EnvironmentBuilder·GameDirector·GameView·PostFxGate·VfxDirector + 신규 테스트 2종
및 그 `.meta`뿐).
