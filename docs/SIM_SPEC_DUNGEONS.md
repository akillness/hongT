# Abyssal Lantern — Dungeon Expansion (Executor Wing) Frozen Spec

// FROZEN CONTRACT AMENDMENT #5 — 2026-08-05 cycle-2 (run-id 20260805-dungeon-gimmicks).
// SIM_SPEC.md(아레나)·SIM_SPEC_CAMPAIGN.md(캠페인)·SIM_SPEC_HACKSLASH.md(v0.2.0)는
// 전부 유지된다. 이 문서는 신규 스테이지 3종과 신규 기믹 3종이 **추가**하는
// 규칙만 정의한다. 근거: _workspace/current/design/dungeon-roster-spec.md,
// balance-sheet.md, pm/negotiation-record.md entries 1-4, QA 밴드
// (qa/benchmark-notes.md §Derived bands).
// 원장(ledger): #1=SIM_SPEC_CAMPAIGN(무번호), #2=SIM_SPEC_HACKSLASH,
// #3=Companion Hold/Recall, #4=Ember Rest Preparation (둘 다 HACKSLASH 문서 내).
// 구 achilles-visual-overhaul-spec §S의 "AMENDMENT #3 게이트" 후보 중 S4(전용
// 웨이브 수)·S5(Ember Rest 방 경계 확장)는 본 #5가 이행(discharge)한다. §S의
// 수용 기준(additive 계약 + 결정론 EditMode 테스트 + 기존 모드 Digest 불변
// 증명)의 증거: pre/post 스탠드얼론 다이제스트 비교(_workspace/current/qa/
// golden-digests-cycle2.md, HEAD 719a587 대비 12행 바이트 동일) + EditMode
// 고정 테스트(DungeonGoldenDigestTests — 골든 리터럴 커밋).
// REVISION v1.1 — 2026-08-05 Stage 2 리튠. 근거: 사용자 플레이테스트 "기믹이
// 안 보인다" → 근본 원인 = 배치 기하(세 기믹 전부 전투 수렴점 (768,604)에
// 닿지 않음). 스펙: _workspace/current/design/gimmick-retune-spec.md.
// 미출시 콘텐츠 수치 개정 — 기존 6스테이지·아레나·프롤로그 계약 불변.
// 아래 본문 수치는 전부 v1.1이 진실이다.
// REVISION v1.2 — 2026-08-05 캠페인 재미 패스 (사용자 요청: "전 스테이지를
// 게임스럽게"). 스펙: _workspace/current/design/campaign-fun-pass-spec.md.
// 캠페인 아크 = 기믹 계보(예고→숙달): 각 논리 스테이지가 지배 기믹 1개의
// 정체성을 갖는다. 변경: ash-march 앵커에 pylon(768,520) 추가(피날레 수렴)
// + 논리 스테이지 1·3·4·5의 HazardOverride 교체(뷰 데이터).
// **계약 변경 고지**: v1.2부터 "기존 6스테이지 배치 불변"은 더 이상 성립하지
// 않는다 — 논리 스테이지 오버라이드는 사용자 요청으로 의도 변경되었다.
// 골든 중 스테이지 1·3·4·5·8 행의 이동은 회귀가 아니라 이 개정의 산물이다.
// 잔여 불변 안전망: arena-hack · arena-frozen · prologue · 스테이지 0·2·6·7 ·
// 클래식 앵커 0..2 — 이 행들이 움직이면 진짜 회귀다.

## Stages (anchor 증분 — CampaignStages 3..5)

| index | id | 이름 | 보스 표기 | 보스 비주얼 | W | 기믹 |
|---|---|---|---|---|---|---|
| 3 | cinder-sluice | 재의 수문 | Sluice Keeper | BossCommander | 8 | current×2 + vent×2 + pillar×1 |
| 4 | ember-bastion | 불씨 요새 | Bastion Sentinel | BossCommander | 8 | pylon×3 + pillar×2 + vent×1 |
| 5 | ash-march | 재의 행진 | Ash Magistrate | BossMonarch | 9 | wall×2(좌·우, 반주기 오프셋) + altar×1 + vent×2 |

- 웨이브 1..W 아레나 규칙 그대로, 웨이브 W+1 보스 웨이브(기존 계약).
  호위 `min(8, 3+2·idx)` = 8 (자동). 파편 로테이션 `idx % 3` = 0/1/2 (자동).
- 던전 적 HP 곡선(핵앤슬래시 계약) 그대로: `86 + min(140,(w−1)·11)`.

### 배치 테이블 (심 좌표 px)

| stage | placements |
|---|---|
| cinder-sluice | current(768,470, push +200, phase 0) · current(768,740, push −200, phase 3.0) · vent(500,604, phase 0.9) · vent(1030,604, phase 2.1) · pillar(768,604) |
| ember-bastion | pylon(560,500) · pylon(980,700) · pylon(768,430) · pillar(640,650) · pillar(900,560) · vent(768,604, phase 0.6) |
| ash-march | wall(left, phase 0) · wall(right, phase 11.5) · altar(768,604) · vent(560,760, phase 0.6) · vent(980,450, phase 1.8) |

## Gimmick 1: tide-current (잿물 해류) — 주기 푸시 레인

- 필드: `{x, y, halfW: 520, halfH: 110, period: 6.0 s, telegraph: 0.8 s, active: 3.2 s, pushX: ±200 u/s, pushY: 0}`.
- **판정은 축정렬 사각 밴드** (`|x−hx| ≤ halfW && |y−hy| ≤ halfH`) — 아이소
  가중 없음(원형 아님). 유일한 사각 해저드.
- 사이클 `t = fmod(stageTime + phase, period)`:
  `[0, 0.8)` 텔레그래프 → `[0.8, 4.0)` 활성 푸시 → `[4.0, 6.0)` 휴지.
- 활성 중 밴드 내 **플레이어와 적 전원**에게 `push × dt` 위치 가산.
  적용 순서: 자체 이동(+클램프) → **해류 가산 → 아레나 재클램프** → 필러
  푸시아웃. 대시 중에도 동일(대시 변위 → 클램프 → 해류 → 필러).
- 피해 0. 동료(companion)는 미적용(추종 오프셋 계약 보존).
- **독트린 변경**: "기믹은 플레이어 리스크 단독" 원칙을 이 기믹부터
  대칭(플레이어+적)으로 확장한다. 기존 3기믹 의미는 불변.
- 이벤트: 활성 진입 경계에서 `HazardPulse`.
- 활성 판정이 읽는 `stageTime`은 직전 틱 값(해저드 시계는 UpdateHazards에서
  갱신) — 1틱 지연은 결정론적이며 계약의 일부다.

## Gimmick 2: ember-pylon (불씨 방벽주) — 적 보호 파괴 오브젝트

- 필드: `{x, y, radius: 30(몸통), auraRadius: 280, hp: 300}`.
- 살아있는 파일런의 오라(아이소 원, auraRadius) 내 적이 **받는 모든 피해
  ×0.40** (기본공격·콤보·스킬·해류벽 포함, DamageEnemy 단일 지점 적용).
  오라 중첩은 비가산 — 1개 이상이면 ×0.40 한 번.
- 플레이어 타격 규칙: 기본공격(아레나 루프)·콤보 스윙이 파일런을 타격 가능.
  판정 = 전방 판정(`dx·facing ≥ −18`) AND `dist²(iso) ≤ (사거리 160 + 몸통 30)²`.
  1스윙(attackId) 1피격 규칙 동일. 스킬(Q/E/R)은 파일런에 무효(전술 명료성).
- 콤보 스윙이 파일런만 맞혀도 `landed`(피니셔 이벤트 조건) 성립.
- hp ≤ 0 → 영구 파괴(런 내 리스폰 없음), `SimEvents.PylonDown` 발생.
  점수/XP/드롭 없음 — 보상은 실드 해제 그 자체.
- 이동 차단 없음, 적 이동·피해에 불간섭(오라 외 효과 없음).

## Gimmick 3: ash-wall (재의 장벽) — 시간표 침식 벽

- 필드: `{edge: left(x₀ 248) | right(x₁ 1288), depthMax: 560, phase}` + 전역 상수
  `{rest: 4.5, telegraph: 1.5, advance: 7.0, hold: 3.0, recede: 7.0}` (주기 23.0 s),
  전진속도 80 px/s, `tickDamage: 10`, `tickPeriod: 0.6 s`.
- 에지 인코딩: `HazardConfig.PushX = +1`(좌벽, x₀에서 우로 전진) / `−1`
  (우벽, x₁에서 좌로 전진) — 신규 필드 없이 기존 필드 재사용.
  팩토리 `Wall(phase)` = 좌벽, `Wall(phase, fromRight: true)` = 우벽.
- 사이클 `t = fmod(stageTime + phase, 23.0)`:
  `[0,4.5)` 휴지 → `[4.5,6)` 텔레그래프 → `[6,13)` 전진 `depth = (t−6)·80`
  → `[13,16)` 유지 `depth = 560` → `[16,23)` 후퇴 `depth = 560 − (t−16)·80`.
- 밴드: 좌벽 `x ∈ [248, 248+depth)`, 우벽 `x ∈ (1288−depth, 1288]` (y 전체).
  depth > 0인 동안 활성. **양벽 모두 중심(768)을 넘어 침식**(좌 최대 x808,
  우 최대 x728) — 중앙 제단 캠핑을 주기적으로 해산시킨다.
- **탈출 불가 상태 없음**: ash-march 위상(0, 11.5) 기준 양벽 동시 침식
  구간의 depth 합 = 440 상수 → 안전 회랑 항상 ≥ 600px (단독 hold 시 480px).
- 피해 틱: 전역 틱 인덱스 `k = floor((stageTime+phase)/0.6)`, k 증가 프레임에
  밴드 내 **플레이어**(Ward 무효화·grace 소모 — 기존 DamagePlayer 규칙)와
  **적 전원**(DamageEnemy — 처치 크레딧·점수·XP·드롭 정상, 파일런 오라 규칙
  통과) 각 10 피해.
- QA 밴드 검증: 10 ≤ 0.30×100 (단일 히트 상한), telegraph 1.5 s ≥ 0.8 s
  (light 티어 하한). 동시 텔레그래프: sluice 최대 2 · bastion 1 · march
  최대 2 (예산 ≤3 PASS — 산술은 gimmick-retune-spec.md).
- 이벤트: 텔레그래프 진입 경계에서 `HazardPulse` 1회/사이클.

## Ember Rest 확장

- 룸 인덱스 검증 `1..5` → `1..8` (`CampaignSpec.MaxEmberRestRoomIndex = 8`).
  기존 1..5 동작 불변(경계 확장만).

## SimTypes/CampaignTypes 증분 (동결 해제 목록 — 이 목록 외 수정 금지)

- `SimTypes.cs` — `SimEvents.PylonDown = 1 << 22`.
- `CampaignTypes.cs`:
  - `HazardKind`: `TideCurrent = 3`, `EmberPylon = 4`, `AshWall = 5`.
  - `HazardConfig`: 필드 `HalfW, HalfH, PushX, PushY, Hp` 추가(기존 필드
    의미 불변, 기존 팩토리 불변) + 팩토리 `Current(x, y, pushX, phase)`,
    `Pylon(x, y)`, `Wall(phase)` / `Wall(phase, fromRight)` (PushX ±1 = 에지).
  - `HazardState`: 필드 `Active(bool), FrontX(float), Hp(float)` 추가.
  - `CampaignSpec` 상수 추가(v1.1 값): Current*(HalfW 520, HalfH 110,
    Period 6.0, Telegraph 0.8, Active 3.2, Push 200), Pylon*(BodyRadius 30,
    AuraRadius 280, Hp 300, AuraDamageTakenMult 0.40), Wall*(EdgeX 248,
    EdgeRightX 1288, DepthMax 560, Rest 4.5, Telegraph 1.5, Advance 7.0,
    Hold 3.0, Recede 7.0, Speed 80, TickDamage 10, TickPeriod 0.6),
    `MaxEmberRestRoomIndex 8`.
  - `CampaignStages`: id 3종·anchor 테이블 3종·`Build` 분기 3종 추가.
    `ForIndex` 범위 주석 0..5.
- `CinderSim.cs`(계약 구현체): `HazardRuntime`에 `Hp, Tick, LastHitAttack`
  추가, `UpdateHazards` 분기(current 이벤트/wall 텔레그래프·틱), `ApplyCurrents`
  (플레이어·대시·적), `StrikePylons`(아레나 공격 루프·콤보 스윙),
  `PylonAuraMultiplier`(DamageEnemy 단일 지점), `Publish` 신규 kind 상태,
  `BeginEmberRest` 경계.

## 진행도/보상 (뷰 레인 — 심 무관, 근거 negotiation-record)

- `StageCatalog.ValidClearMask` `0x3F` → `0x1FF` (비트 6-8 추가, 기존 의미 불변).
- 첫클리어 유물 보너스(뷰 지급): sluice +6 / bastion +8 / march +10.
- ash-march 첫클리어 동료: `scout-echo` (기존 추출 변형 재사용).
- localStorage 키·스키마 불변 — `cleared[]`에 신규 id 추가만(R8).

## Determinism

전 기믹 RNG 금지 — 위상/모듈러/카운터 산술만. 해저드 배열이 없거나 신규
kind가 없는 기존 config의 다이제스트는 **바이트 동일**해야 한다(R1-R3 골든).
동일 config+입력 → 동일 Digest·스냅샷.
## AMENDMENT #5b — 2026-08-12 뷰 레이어 스케일링 (심 불변)

이 개정은 **뷰 전용**이며 심 계약(반축, 해저드 반경, 골든)을 0바이트도 바꾸지
않는다. 기믹 배치는 이미 §Gimmick 1-3과 배치 테이블에 확정됐으므로, 본 섹션은
그것을 **화면에 어떻게 표현하는가**만 다룬다.
근거: _workspace/current/design/dungeon-interior-spec.md §2-4 (기하 확장)
+ docs/DUNGEON_GUIDE.md (기믹 톤 통합) + 사용자 요청 "스테이지별 던전구성 전면
업데이트, 오브젝트 크기를 지금의 0.7배로 줄인다".

### 이동 영역 — 심 반축은 이미 출하됐고, 이번 확장은 월드 스케일이다

**[OBSERVED]** 심 클램프 타원은 **이미** `DungeonBoundsSpec.ExpandedHalfWidth
= 735` / `ExpandedHalfHeight = 390`이다 (AMENDMENT #15 §19, 출하 완료 —
`Assets/Scripts/Sim/DungeonProgressionSpec.cs`). 프롤로그·아레나·훈련장은
동결 520×270을 유지한다. **이번 개정은 이 숫자를 건드리지 않는다.**

**[OBSERVED]** 이번 개정의 이동 영역 확대는 심→월드 환산 계수다:
`ViewWorld.Scale` **0.0125 → 0.0150** (×1.2). 던전 바닥이 월드 공간에서
1.2× 커지며, `EnvironmentBuilder.SimToWorld`·`EnvironmentLayout.SimToWorld`가
같은 값을 미러한다 (`DungeonFramingAndMoodTests`가 1e-9로 고정).

### 오브젝트 축소 (0.7×)

**[OBSERVED]** `ActorView.GlobalScale`: 1.00 → **0.70** (전 액터 균일).
`ViewWorld.DungeonObjectScale = 0.70f`가 계수의 단일 출처다.

- `CameraRig.DungeonCalmDistance` 17.5 → **21.0**,
  `DungeonCrowdDistance` 21.5 → **25.8** — 둘 다 ViewWorld.Scale과 같은
  ×1.2라 바닥의 화면상 크기는 유지되고, 0.70× 액터만 상대적으로 작게 읽힌다.
  1.229 티어 비율 유지.
- 해저드 몸체·텔레그래프처럼 **심 반경에서 크기를 얻는 비주얼은 축소 대상이
  아니다** — 축소하면 충돌 발자국과 화면 표현이 어긋난다 (아래 참조).

### 해저드 반경 불변

**[OBSERVED]** §Gimmick 1-3의 모든 해저드 반경은 **심 상수**다 (vent 90,
pillar 40, altar 70, current half 110, pylon 30 body / 280 aura, wall edges).
**[CONTRACT]** 이 반경들은 본 개정으로 변경되지 않는다. 충돌 판정·텔레그래프
타이밍·피해 틱 스케줄·골든 다이제스트가 전부 이 상수에 걸려 있다. 심 반경에서
파생되는 비주얼(경고 링, 몸체 실루엣)은 반경 × ViewWorld.Scale 그대로 두고,
장식 지오메트리만 0.7×를 받는다.

### 골든 다이제스트 — 무이동이 전제다

**[CONTRACT]** 본 개정은 심을 0바이트도 바꾸지 않으므로
`DungeonGoldenDigestTests`는 **재고정 없이 그대로 통과해야 한다.** 골든이
움직였다면 이 개정이 뷰 전용이라는 전제가 깨진 것이고, 그 시점에서 개정이
아니라 결함이다 (CLAUDE.md §4e "골든 무이동은 PASS 조건이 아니라 전제").

### 변경 상수 대장

- `ViewWorld.Scale` 0.0125 → 0.0150 (+ `LegacyScale = 0.01`,
  `LegacyScaleRatio` 파생 유지 — 프롤로그/로비 카메라 보상용).
- `ViewWorld.DungeonObjectScale = 0.70f` (신규).
- `ActorView.GlobalScale` 1.00 → 0.70.
- `EnvironmentBuilder.SimToWorld` / `EnvironmentLayout.SimToWorld` 0.0125 → 0.0150.
- `CameraRig.DungeonCalmDistance` 17.5 → 21.0 / `DungeonCrowdDistance` 21.5 → 25.8.
- 심 상수: **0건 변경.** `DungeonBoundsSpec` 735×390은 선행 출하분이다.

