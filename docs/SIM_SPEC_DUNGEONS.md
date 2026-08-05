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
