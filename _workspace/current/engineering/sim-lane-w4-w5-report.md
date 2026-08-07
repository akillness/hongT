# Sim 레인 리포트 — W4 (웨이브 포인트 예산 + DDA) / W5 (등급 드롭 + pity) / W-MV (던전 이동 한계)

**작성**: 2026-08-07 · **레인**: Sim · **브랜치**: `akillness/main` (워킹트리, 미커밋)
표기 규약은 CLAUDE.md §4 (`[OBSERVED]` / `[INFERENCE]` / `[TARGET]`).

---

## 0. 요약

- W4 = **AMENDMENT #13** (스펙 로컬 절 §17), W5 = **AMENDMENT #14** (§18).
  시드 문서가 지칭한 "#10/#11"은 부록 A canonical 원장에서 **이미 점유됨**
  (#10 훈련장·돌발, #11 난이도·적 그룹 AI, #12 던전 환경) → 다음 가용 번호로 배정.
  부록 A 표에 두 행 추가 (`docs/SIM_SPEC_HACKSLASH.md:1113-1114`).
- 두 증보 모두 **신규 opt-in 구조체 `DungeonProgressionConfig` 게이트 뒤**에 있고,
  기존 `CinderSim(in HackConfig)` 생성자가 `default`(둘 다 OFF)를 전달한다.
  추가로 생성자가 `Mode != Dungeon` 이면 progression 을 통째로 낙하 → **D3**
  (아레나/프롤로그 digest 불가침)의 구조적 집행.
- **FROZEN 파일은 한 글자도 수정하지 않았다**: `SimTypes.cs`, `HackTypes.cs`,
  `CampaignTypes.cs` 무변경. 확장은 신규 비동결 파일
  `Assets/Scripts/Sim/DungeonProgressionSpec.cs` (DifficultySpec.cs = AMENDMENT #11
  선례) + `CinderSim.cs`(비동결) 에서만.
- **§13 무개정.** RNG 0건. 예산은 정수 산술, DDA는 정수 누산기, 드롭 롤은 정수
  애벌란치 해시 + 단조 pity 카운터 2개.

---

## 1. 변경 파일 목록 (경로:라인)

### 신규

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Sim/DungeonProgressionSpec.cs` (신규, 397행) | `DungeonProgressionConfig` · `LootGrade` · `WaveBudgetSpec` · `LootGradeSpec` · `IDungeonProgressionSnapshot` |
| `Assets/Scripts/Sim/DungeonProgressionSpec.cs.meta` | guid `dc737d881ada4c448659e3ed0cfdd30e` |
| `Assets/Tests/EditMode/WaveBudgetDdaTests.cs` (신규) | W4 테스트 10종 |
| `Assets/Tests/EditMode/LootGradeTests.cs` (신규) | W5 테스트 10종 |
| `Assets/Tests/EditMode/WaveBudgetDdaTests.cs.meta`, `LootGradeTests.cs.meta` | guid 신규 발급 |

### 수정 — `Assets/Scripts/Sim/CinderSim.cs` (유일한 수정 소스 파일)

| 라인 | 변경 |
|---|---|
| `:19-22` | 클래스 선언에 `IDungeonProgressionSnapshot` 추가 |
| `:90-92` | `_pickupGrades[]` 병렬 배열 (PickupState 가 FROZEN 이라 필드 추가 불가) |
| `:96` | `_pickupGradeView` 게시 리스트 (검증됨) |
| `:98-108` | #13/#14 런 스코프 상태 11개 (`_progression`, `_ddaBand`, `_waveBudget`, `_waveEliteAllowance`, `_waveHitsTaken`, `_waveSeconds`, `_elitesThisWave`, `_finePity`, `_epicPity`, `_dropOrdinal`, `_lastLootGrade`) |
| `:383-388` | 기존 `CinderSim(in HackConfig)` → `: this(in config, default)` 위임 |
| `:390-401` | 신규 오버로드 `CinderSim(in HackConfig, in DungeonProgressionConfig)` + 던전 전용 낙하 |
| `:669-681` | `IDungeonProgressionSnapshot` 프로퍼티 11개 |
| `:2647-2650` | `DamagePlayer` — `_waveHitsTaken += 1` (체력이 실제 깎인 지점) |
| `:3117-3141` | `SpawnPickup` — 등급 산정 + pity 원장 + 병렬 배열 리사이즈 |
| `:3173` | `CollectPickup(pickup.Kind, _pickupGrades[index])` |
| `:3185-3220` | `CollectPickup` 등급 배율 (회복/기름/유물점수/랭크스텝) |
| `:3220-3233` | `RemovePickupAt` — 등급 배열 스왑다운 동기 |
| `:3253-3276` | `StartWave` — 예산 산정 + 웨이브 누산기 리셋 |
| `:3305-3309` | `UpdateWave` — 웨이브 시계 누산 (Running 중에만) |
| `:3333` | `SettleDifficultyBand()` 호출 (웨이브 클리어 시점) |
| `:3339-3353` | `SettleDifficultyBand()` 신규 |
| `:3364-3389` | `SpawnEnemy` — 체력 배율 분기 + 정예 배정 분기 |
| `:3449` | `_elitesThisWave += 1` |
| `:3473-3486` | `ResetCampaignRun` — #13/#14 런 스코프 리셋 11개 |
| `:4282-4290` | `Publish` — `_pickupGradeView` 인덱스 정렬 게시 |

### 수정 — 문서

| 파일 | 변경 |
|---|---|
| `docs/SIM_SPEC_HACKSLASH.md:1113-1114` | 부록 A canonical 원장에 #13/#14 행 추가 (DRAFT) |
| `docs/SIM_SPEC_HACKSLASH.md:1136-1342` | AMENDMENT #13 (§17) + #14 (§18) 전문 |

**다른 세션/레인 소유 파일 무변경**: `Assets/Editor/SceneBuilder.cs`,
`Assets/Scripts/View/EnvironmentBuilder.cs`, `graphify-out/*`, `HudView.cs`,
`VfxDirector.cs`, `CameraRig.cs`, `StageCatalog.cs` — 전부 손대지 않음.
커밋·스테이징·push 없음.

---

## 2. 수치 표

### 2.1 W4 §17.1-§17.2 예산 상수 [TARGET]

| 상수 | 값 |
|---|---|
| `BudgetBase` / `BudgetPerWave` / `BudgetCap` | 100 / 26 / 600 (웨이브 21 도달) |
| `GruntCost` | 16 |
| `MinSpawns` / `MaxSpawns` | 4 / 14 |
| `FullRosterSpend` | 224 (= 14 × 16) |
| `HealthSurplusCap` | 1.7 |
| `ElitePointCost` / `EliteAllowanceCap` | 150 / 3 |

식: `BaseBudget(w) = min(600, 100 + (w-1)·26)` ·
`count = clamp(4, 14, b/16)` ·
`healthMul = 1 + min(1.7, max(0, b-224)/224)` ·
`elites = min(3, b/150)` · 적 HP = `86 × healthMul`.

### 2.2 W4 밴드 0 곡선 [OBSERVED] (스탠드얼론 dotnet 프로브 실행)

| wave | 예산 | 스폰 | 체력배율 | 적 HP | 정예 | (참고) 구식 HP |
|---|---|---|---|---|---|---|
| 1 | 100 | 6 | 1.000 | 86.0 | 0 | 86.0 |
| 2 | 126 | 7 | 1.000 | 86.0 | 0 | 97.0 |
| 3 | 152 | 9 | 1.000 | 86.0 | 1 | 108.0 |
| 5 | 204 | 12 | 1.000 | 86.0 | 1 | 130.0 |
| 8 | 282 | 14 | 1.259 | 108.3 | 1 | 163.0 |
| 10 | 334 | 14 | 1.491 | 128.2 | 2 | 185.0 |
| 14 | 438 | 14 | 1.955 | 168.2 | 2 | 226.0 |
| 21+ | 600 | 14 | 2.679 | 230.4 | 3 | 226.0 |

체력배율은 예산에 대해 **단조**다 (초안의 톱니 — 웨이브 2→3 에서 HP 96.8→90.8 로
떨어지던 결함 — 을 잉여 기준을 `FullRosterSpend` 고정값으로 바꿔 제거).

### 2.3 W4 §17.3 DDA 밴드 [TARGET]

| band | -2 | -1 | 0 | +1 | +2 |
|---|---|---|---|---|---|
| `BandPermille` | 780 | 890 | 1000 | 1120 | 1250 |
| [OBSERVED] wave10 예산 | 260 | 297 | 334 | 374 | 417 |
| [OBSERVED] wave10 체력배율 | 1.161 | 1.326 | 1.491 | 1.670 | 1.862 |
| [OBSERVED] wave10 정예 | 1 | 1 | 2 | 2 | 2 |

### 2.4 W4 §17.4 성과 신호 [TARGET]

| 신호 | +1 | -1 |
|---|---|---|
| `Health/MaxHealth` (웨이브 종료 시) | ≥ `HealthyFraction` 0.75 | < `StruggleFraction` 0.35 |
| 웨이브 소요 | ≤ `FastWaveSeconds` 18 s | ≥ `SlowWaveSeconds` 42 s |
| 웨이브 중 피격 | ≤ `CleanHits` 2 | ≥ `BatteredHits` 9 |

원시 델타 -3..+3 → `StepCap = 1` 클램프 → 웨이브당 최대 1밴드, `[-2,+2]` 클램프.
피격은 **체력이 실제 깎인 것만** (부록 B 의 §3 "실제 깎인 피격" 정의 재사용).
웨이브 시계는 `SimMode.Running` 중에만 누산 — 인터미션 미청구.

### 2.5 W5 §18.2-§18.3 롤·pity [TARGET]

| 상수 | 값 |
|---|---|
| `RollModulus` | 100 |
| `FineThreshold` / `EpicThreshold` | 70 / 92 (원시 22% / 8%) |
| `FinePityLimit` | 5 → **Basic 6연속 불가** |
| `EpicPityLimit` | 18 → **non-Epic 19연속 불가** |
| `BossGrade` | Epic, **원장 밖** (카운터 증가·리셋 모두 안 함) |

우선순위: epic pity > 롤(Epic) > fine pity > 롤(Fine) > Basic.

**[OBSERVED] 실측 4000 드롭**: Basic 2606 (65.2%) / Fine 1011 (25.3%) /
Epic 383 (9.6%). 최대 Basic 연속 **5**, 최대 non-Epic 연속 **18** — 두 상한 모두
정확히 타이트.

### 2.6 W5 §18.4 등급 페이로드 [TARGET]

| 등급 | `GradeValueMul` | `GradeRankSteps` | shard 회복 | flask 기름 | relic 점수 |
|---|---|---|---|---|---|
| Basic | 1.00 | 1 | 18.0 | 35.00 | 250 |
| Fine | 1.45 | 1 | 26.1 | 50.75 | 362 |
| Epic | 2.10 | 2 | 37.8 | 73.50 | 525 |

등급은 **이미 떨어진 kind 의 값만** 배율한다. 어떤 kind 가 떨어지는지(`id%3`,
`id%7` 장비 파편 선례)는 무변경 — 기존 선례와 공존한다.

---

## 3. 결정론 근거

### 3.1 RNG 부재 [OBSERVED]

`DungeonProgressionSpec.cs` 전체에 `System.Random` / `UnityEngine.Random` /
시간·정적 상태 의존이 0건. 구성 요소:

- **예산**: 정수 사칙연산만 (`BaseBudget`, `EffectiveBudget` 은 퍼밀 정수 곱/나눗셈).
- **DDA 밴드**: 고정스텝 누산 상태(HP 분율·웨이브 초·피격 수)에 대한 임계 비교
  3회 → -3..+3 → 클램프. 정수 누산기.
- **드롭 롤**: `(enemyId·73856093 ^ wave·19349663 ^ dropOrdinal·83492791)` 의
  정수 애벌란치 믹스 → `& 0x7fffffff` → `% 100`. 부동소수 0. 선례:
  `EliteSpawnModulus = 7`, 장비 파편 `id % 7`, Ember Rest 오퍼 해시.
- **pity**: 단조 정수 카운터 2개. 리셋 규칙이 전순서.

### 3.2 락스텝 증거 [OBSERVED]

| 증거 | 결과 |
|---|---|
| 4레인 pre/post 다이제스트 (arena / arena-hack / prologue / dungeon-cinder-span, 각 5400틱, 97틱마다 플레이어 좌표·HP·점수·웨이브·처치·유물 + 전체 적 id/좌표/HP/액션 + 전체 픽업 id/kind 덤프) | **224행 완전 동일** — `git show HEAD:Assets/Scripts/Sim/*.cs` 로 뽑은 개정 전 심 vs 현재 워킹트리 심 |
| 게이트 ON 던전 런 2회 (같은 config + 같은 입력, 5400틱) | 밴드·예산·정예배정·피격수·pity·점수·HP 전부 **IDENTICAL** |
| `WaveBudgetDdaTests.AdaptiveWaves_Off_IsLockstepWithTheFrozenConstructor` | 3600틱 전체 필드 락스텝 |
| `LootGradeTests.GradedLoot_Off_IsLockstepWithTheFrozenConstructor` | 3600틱 HP·기름·점수·유물·픽업 id/kind 락스텝 |
| `LootGradeTests.PityLedger_IsRunScoped` | `Restart()` 후 신규 런과 1800틱 락스텝 |

**주의 (CLAUDE.md §4)**: 위 224행 다이제스트는 **스탠드얼론 dotnet 8** 하네스
결과다. 스탠드얼론과 Unity 다이제스트는 float 하위비트가 다르므로(정수 필드는
동일) 이 결과는 **pre/post 추가성 증명 전용**이다. 배포 진실은 Unity 골든
(`DungeonGoldenDigestTests`) 이며, 오케스트레이터의 통합 Unity 실행이 최종 게이트다.
다만 **본 변경은 게이트 OFF 경로에서 코드 경로 자체가 분기하지 않으므로**
(`_progression` 전 필드가 `false`/0), Unity 골든이 움직일 구조적 여지가 없다
[INFERENCE — Unity 실행 미측정].

---

## 4. 추가 테스트 목록

### W4 — `Assets/Tests/EditMode/WaveBudgetDdaTests.cs` (10종, 요구 ≥6)

| # | 테스트 | 검증 |
|---|---|---|
| 1 | `AdaptiveWaves_Off_IsLockstepWithTheFrozenConstructor` | 게이트 OFF == 동결 생성자, 던전 3600틱 |
| 2 | `AdaptiveWaves_ArenaAndPrologue_IgnoreTheProgressionConfig` | D3 — 아레나/프롤로그는 `.All` 을 넘겨도 무장 안 함, 각 1800틱 락스텝 |
| 3 | `BaseBudget_IsMonotoneAndSaturatesAtTheCap` | 60웨이브 단조 + 상한 + 핀 고정값 |
| 4 | `Budget_BuysBodiesFirstThenHitPoints` | 스폰수·체력배율 핀 + 60웨이브 양쪽 단조 + 잉여 상한 |
| 5 | `EliteAllowance_GrowsWithBudgetAndCaps` | 정예 배정 핀 + 상한 |
| 6 | `DifficultyBand_MovesOneStepPerWaveAndClamps` | 신호 3종 격리(각 ±) + 스텝캡 + 양끝 클램프 |
| 7 | `Band_ScalesTheBudgetMonotonically` | 밴드→예산 단조 + 핀 + 범위 밖 클램프 |
| 8 | `AdaptiveWaves_On_SameInputsProduceIdenticalRuns` | **결정론** 5400틱 (밴드/예산/정예/피격) + 600틱 전체 락스텝 |
| 9 | `AdaptiveWaves_On_SimAgreesWithThePureBudgetArithmetic` | 심의 게시값 == 순수 함수 결과, 7200틱, EnemyCap 불변식 |
| 10 | `WaveHitsTaken_CountsOnlyDamageThatCostHealth` | 피격 카운터 증가 틱 ⊆ 체력 감소 틱 |

### W5 — `Assets/Tests/EditMode/LootGradeTests.cs` (10종, 요구 ≥6)

| # | 테스트 | 검증 |
|---|---|---|
| 1 | `GradedLoot_Off_IsLockstepWithTheFrozenConstructor` | 게이트 OFF == 동결 생성자, 3600틱 |
| 2 | `Roll_IsDeterministicInRangeAndNonDegenerate` | 0..99 · 재현성 · 2000샘플 80+버킷 · 음수/0 무예외 |
| 3 | `Resolve_AppliesPityBeforeTheRoll` | 우선순위 사다리 5단 |
| 4 | `Advance_ResetsAndIncrementsTheLedger` | 리셋/증가 규칙 3분기 |
| 5 | `Pity_BoundsBothStreaks_EvenOnTheWorstPossibleRolls` | **항상 roll 0** 최악 입력에서 두 상한이 정확히 타이트 |
| 6 | `Pity_HoldsOverTheRealRollSequence` | 실제 롤 4000드롭 상한 유지 + 3등급 출현 + 비율 밴드 핀 |
| 7 | `GradeTables_ArePinnedAndClamped` | 배율/랭크스텝 핀 + 표 밖 클램프 + 실현 페이로드 핀 |
| 8 | `PickupGrades_StayIndexAlignedWithPickups` | 등급 배열 ↔ 픽업 배열 정렬, 5400틱 |
| 9 | `GradedLoot_On_SameInputsProduceIdenticalLedgers` | **결정론** 5400틱 (pity/등급/점수/HP/기름) |
| 10 | `PityLedger_IsRunScoped` | `Restart()` 가 원장·밴드 소거 + 신규 런과 1800틱 락스텝 |

### 실행 증거 [OBSERVED]

Unity 배치모드는 지시대로 실행하지 않았다. 대신 `Assets/Tests/EditMode/` 중
**UnityEngine/CinderCourt.View 를 참조하지 않는 순수-Sim 테스트 파일 12종**
(`CinderSimTests` · `CompanionAutonomyTests` · `CompanionSkillTests` ·
`DifficultyGroupAiTests` · `HackSimTests` · `LootEconomyTests` · `MomentumTests` ·
`SigilTests` · `TrainingSurgeTests` · `WaveTelegraphTests` + 신규 2종)을
`Assets/Scripts/Sim/*.cs` 와 함께 dotnet 8 + NUnit 3.14 로 컴파일해 실행:

```
Passed!  - Failed: 0, Passed: 232, Skipped: 0, Total: 232, Duration: 1 s
```

즉 **기존 순수-Sim 회귀 212종 + 신규 20종 전부 그린**. 나머지 EditMode 파일
(View/Editor 의존)은 Unity 없이는 실행 불가 → 오케스트레이터 통합 실행 대상.

`dotnet build` 로 `Assets/Scripts/Sim/*.cs` 컴파일: **0 error / 0 warning**.

---

## 5. View 연동 필요 항목 (Sim 레인은 코드 수정하지 않음)

| # | 항목 | 필요한 것 | 접점 |
|---|---|---|---|
| V-1 | **게이트 켜기** | `GameDirector.StartDungeon` 에서 `new CinderSim(config)` → `new CinderSim(config, DungeonProgressionConfig.All)` (또는 옵션/난이도별 부분 활성). **이 한 줄 없이는 두 증보 모두 완전 비활성** | `GameDirector` 던전 진입 경로 |
| V-2 | DDA 밴드 HUD | `IDungeonProgressionSnapshot.DifficultyBand` (-2..+2) · `WaveBudget` · `WaveEliteAllowance` 게시 중. 밴드 변동 시점은 웨이브 클리어 순간 | `HudView` (VFX/UI 레인 소유) |
| V-3 | **드롭 등급 시각화** | `PickupGrades` 가 `Pickups` 와 인덱스 정렬로 게시된다. 등급별 픽업 색/글로우/파티클 티어가 없으면 W5 는 플레이어에게 보이지 않는다 | `GameView` 픽업 뷰 + `VfxDirector` |
| V-4 | 등급 획득 연출 | `LastLootGrade` + 기존 `SimEvents.PickupCollected` 조합으로 Epic 획득 시 강한 피드백 가능. 신규 SimEvent 비트는 **추가하지 않았다** (SimEvents 는 FROZEN SimTypes.cs) | `VfxDirector` / SFX |
| V-5 | pity 게이지 (선택) | `FinePity` / `EpicPity` 게시 중. 노출 여부는 디자인 결정 — 노출하면 "다음 드롭 보장까지 N" UI 가능 | `HudView` |
| V-6 | 스폰 상한 재확인 | 예산 최대 스폰은 14 (`MaxSpawns`)로 `SimConfig.EnemyCap` 20 미만. 뷰의 적 풀/컬링 가정이 20 기준이면 문제 없음 | — |

---

## 6. 미해결 / 사람 판단 필요

| # | 항목 | 상태 |
|---|---|---|
| D-A | **증보 번호 확정** | 본 리포트는 #13/#14 를 배정하고 부록 A 에 DRAFT 로 기록했다. 시드 문서의 "#10/#11" 지칭은 원장과 충돌하므로 무효. 부록 A 의 미결 항목(각인 = "10-b" 제안, D13)과 함께 오퍼레이터 서명 필요 |
| D-B | **초반 난이도 하향** | 게이트 ON 시 웨이브 1-6 의 적 HP 가 86 고정(구식은 86→130 램프)이고 대신 스폰 수가 6→12 로 늘어난다. 5400틱 스크립트 봇 기준 게이트 OFF 런은 웨이브 6 에서 사망(HP 0), 게이트 ON 런은 생존(HP 33, 처치 55 vs 34) [OBSERVED]. **총 위협은 예산이 단조 보장하지만 초반 절대 난이도는 낮아진다.** `BudgetBase` 100 → 상향 또는 `GruntCost` 16 → 하향으로 조정 가능. 튜닝 판단은 디자이너/PM 몫 |
| D-C | DDA 밴드가 빠르게 포화 | `StepCap = 1` + 범위 ±2 이므로 잘하는 플레이어는 5웨이브 안에 +2 도달. 설계 의도이나 밴드 범위 확장(±3) 여지 있음 |
| D-D | Unity 런타임 검증 | 지시대로 Unity 배치모드 미실행. EditMode 전량 + WebGL 빌드는 오케스트레이터 통합 실행 대기 |
| D-E | `EquipShard` Epic 2랭크 | 랭크 상한(`MaxEquipRank`)이 있어 경제 폭주는 없으나, 캠페인 장비 진행 속도가 최대 2배가 된다. 보수적으로 `{1,1,2}` 로 잡았고 `{1,2,3}` 은 채택하지 않음 |

---

---

## 6-B. W-MV — 던전 이동 한계 (AMENDMENT #15, 스펙 §19)

### 6-B.1 1단계 실측 [OBSERVED]

**심 클램프.** `CinderSim.ClampToArena` (`Assets/Scripts/Sim/CinderSim.cs:4262-4287`)
가 유일한 초크포인트다. 던전은 **타원**(`sqrt((lx/hw)²+(ly/hh)²)≤1`),
아레나/프롤로그는 **마름모**(L1). 반축은 **두 경로 모두 동결 상수**
`SimConfig.ArenaHalfWidth 520` / `ArenaHalfHeight 270`(`SimTypes.cs:193`),
중심 (768, 604). 마진: 플레이어 `PlayerMarginClamp 34`, 적 `EnemyMarginClamp 24`,
y 는 마진의 절반.

→ **질문에 대한 답: 그렇다. 던전도 아레나와 같은 520×270 반축을 쓴다.**
모드 차이는 노름(타원 vs 마름모)뿐이다.

| | 반축 | x 범위 | y 범위 | 면적 |
|---|---|---|---|---|
| 플레이어 도달 | 486 × 253 | 282..1254 | 351..857 | 386,284 px² |
| 적 정지선 | 496 × 258 | 272..1264 | 346..862 | — |

**뷰 바닥 (읽기 전용 참조).** `Assets/Editor/SceneBuilder.cs:126-127` 의
`CourtBackdrop` 쿼드 = `SimWorld(1536) × SimWorld(1024)`, sim (768, 512) 중심
→ **sim x 0..1536, y 0..1024**. (`SceneBuilder.cs:257` `SimWorld(px) = px * S`.)

**괴리 수치:**

| 축 | 플레이어 도달 | 그려진 플레이트 | 비율 |
|---|---|---|---|
| 폭 | 972 | 1536 | **63.3%** |
| 높이 | 506 | 1024 | **49.4%** |
| 면적 | 386,284 px² | 1,572,864 px² | **24.6%** |

### 6-B.2 반전 발견 — 벽 링은 이미 클램프를 따라온다 [OBSERVED]

`Assets/Scripts/View/EnvironmentBuilder.cs:542-543`:
`StopE = (ArenaHalfWidth − EnemyMarginClamp) / ArenaHalfWidth = 0.95385`,
`HalfW/HalfH = SimConfig.ArenaHalfWidth/Height` (`:536-537`), 중심
`Cx/Cy = SimConfig.ArenaX/Y` (`:534-535`). 링 반축 = 496 × 257.5.
적 클램프 496 × 258 과 **y 0.5 px 차이**뿐 — `EnvironmentBuilder.cs:36-43` 이
"min 을 보수적으로 x 축 몫으로 잡는다"고 명시한 의도된 차이다.

**따라서 사용자가 느끼는 "맵보다 좁다"는 벽 링 대비가 아니다.** 대비 대상은
(a) 칠해진 1536×1024 플레이트, (b) Zone C 테라스(`EnvironmentBuilder.cs:1307-1313`,
sim x −1740..3260 / y −70..1570 — 프러스텀을 채워 VoidFloor 노출을 줄이려고
**의도적으로 링 밖에** 깐 것)다.

→ **결론: 클램프를 넓히면 벽 링도 반드시 같이 움직여야 한다.** 아니면 플레이어가
벽을 통과해 바깥에 선다. 이것이 이 증보의 하드 뷰 결합이며, 그래서 `#15` 는
`DungeonProgressionConfig.All`(#13+#14)에 **포함시키지 않았다**.

### 6-B.3 확장 상한을 정하는 두 제약 [OBSERVED]

1. **y 축 — 그려진 플레이트.** 중심 y 604, 플레이트 하단 1024 → 아래 여유 **420**.
2. **x 축 — 동결 기믹 span.** 재의 벽은 `CampaignSpec.WallEdgeX 248` ↔
   `WallEdgeRightX 1288`(`CampaignTypes.cs:224-225`) 을 쓸고, **모든** 조류는
   x 768 에 `CurrentHalfW 520`(`CampaignTypes.cs:210`, 배치는 `:291-292`, `:443-444`,
   `StageCatalog.cs:147`, `:485` — 전부 x=768) → **둘 다 정확히 x 248..1288**.
   플레이어 도달이 이 밖으로 나가면 재의 벽은 *발동은 하되 위협이 아닌* 기믹이 된다.
   `PlayerMarginClamp 34` 이므로 `halfWidth ≤ 520 + 34 = 554`.

**x 는 거의 못 늘리고(×1.065 상한) y 는 많이 늘릴 수 있다(×1.55).** 그리고 실측상
부족한 쪽이 y(49.4%)다 — 제약과 필요가 같은 방향이다.

### 6-B.4 2단계 구현 — 권장 수치 [TARGET]

| 상수 | 값 | 배율 | 근거 |
|---|---|---|---|
| `DungeonBoundsSpec.ExpandedHalfWidth` | 554 | ×1.065 | 기믹 span 상한 |
| `DungeonBoundsSpec.ExpandedHalfHeight` | 418 | ×1.548 | 플레이트 하단 여유 420 |

[OBSERVED] 결과 기하:

| | 반축 | x 범위 | y 범위 |
|---|---|---|---|
| 플레이어 도달 | 520 × 401 | **248..1288** | 203..1005 |
| 적 정지선(=벽 링) | 530 × 406 | 238..1298 | 198..1010 |

| 축 | 이전 | 이후 |
|---|---|---|
| 폭 (플레이트 대비) | 63.3% | **67.7%** |
| 높이 (플레이트 대비) | 49.4% | **78.3%** |
| 면적 | 386,284 px² (24.6%) | **655,085 px² (41.6%)** — ×1.696 |

플레이어 x 범위가 기믹 span(248..1288)과 **정확히 일치** — 한 픽셀 여유도 없이 꽉 찬다.
링 4변 전부 플레이트 내부.

### 6-B.5 게이트 설계

`DungeonProgressionConfig.Bounds` (`DungeonBounds { float HalfWidth, HalfHeight }`).
**`HackConfig`(FROZEN)에 필드를 추가하지 않았다** — W4/W5 와 같은 비동결 seam 재사용.

- `default`(두 축 0) → 동결 상수. `All`(#13+#14)은 bounds 를 켜지 않는다.
  `Everything` 이 셋 다 켠다.
- **한쪽 축만 설정된 구조체는 무효**(inert) — 한 축만 조용히 늘어나는 사고 차단.
- **축소 요청은 동결값으로 클램프** — 축소는 해저드·스폰을 플레이필드 밖으로 미는
  별개의 변경이다.
- 반축은 **생성자에서 1회 해석**(`readonly`, 필드 이니셜라이저가 동결 상수) →
  런 도중 플레이필드 불변 = 재현성 조건. 아레나/캠페인 생성자는 **한 줄도 안 바뀐다**.
- 던전이 아니면 progression 전체가 낙하 → 아레나/프롤로그는 마름모 + 동결 반축 그대로.

마진 산술(34 / 24, y 절반)은 **불변** — 넓어진 플레이필드에서도 경계 이격은 같다.

### 6-B.6 스폰·해저드 정합성 검토

| 항목 | 결과 |
|---|---|
| `SimConfig.SpawnPoints` 8점 (`SimTypes.cs:240-245`, x 284..1239 / y 350..840) | 전부 확장 타원 내부 — 스폰 직후 스냅 없음 [OBSERVED, 테스트 W-MV-4] |
| "적이 못 가는 확장 영역" | **구조적으로 불가.** 적과 플레이어가 같은 `ClampToArena` 를 쓰고 적 정지선이 플레이어보다 10 px 바깥 |
| 재의 벽 / 조류 | §6-B.4 수치에서 플레이필드를 **정확히** 덮는다 [OBSERVED, 테스트 W-MV-4] |
| 분출구(vent) | 점 기준 반경이라 확장 영역 외곽에 커버리지 없음. 원래부터 국소 기믹이므로 결함 아님 [INFERENCE] |
| 기둥(pillar) | `ApplyPillars` 는 반경 밀어내기라 플레이필드 크기와 무관 |

### 6-B.7 변경 파일 (W-MV)

| 파일:라인 | 변경 |
|---|---|
| `Assets/Scripts/Sim/DungeonProgressionSpec.cs:37-42` | `DungeonProgressionConfig.Bounds` 필드 |
| `Assets/Scripts/Sim/DungeonProgressionSpec.cs:44-62` | `Any` 갱신, `All`(bounds 제외) / `Everything`(bounds 포함) |
| `Assets/Scripts/Sim/DungeonProgressionSpec.cs:64-88` | `DungeonBounds` 구조체 |
| `Assets/Scripts/Sim/DungeonProgressionSpec.cs:90-166` | `DungeonBoundsSpec` (상수·`Resolve`·`EnemyStopE`/`PlayerStopE`) |
| `Assets/Scripts/Sim/DungeonProgressionSpec.cs:507-518` | 스냅샷에 `BoundsHalfWidth`/`BoundsHalfHeight`/`ExpandedBoundsActive` |
| `Assets/Scripts/Sim/CinderSim.cs:110-115` | `_boundsHalfWidth`/`_boundsHalfHeight` (동결 상수로 필드 초기화) |
| `Assets/Scripts/Sim/CinderSim.cs:408-409` | 생성자 1회 해석 |
| `Assets/Scripts/Sim/CinderSim.cs:690-693` | 스냅샷 프로퍼티 3개 |
| `Assets/Scripts/Sim/CinderSim.cs:4263-4270` | `ClampToArena` 반축 치환 (유일 초크포인트) |
| `Assets/Tests/EditMode/DungeonBoundsTests.cs` (신규, 8종) + `.meta` | W-MV 테스트 |
| `docs/SIM_SPEC_HACKSLASH.md:1115` / `:1345-` | 부록 A 원장 #15 행 + AMENDMENT #15 (§19) 전문 |

### 6-B.8 테스트 (8종, 요구: OFF 불변성 ≥1 + ON 동작 ≥3)

| # | 테스트 | 검증 |
|---|---|---|
| 1 | `Bounds_Off_IsLockstepWithTheFrozenConstructor` | **OFF 불변성.** 던전 5400틱, 8방위 강제 이동으로 클램프를 전 방향 실제로 때리며 플레이어·전체 적 좌표 락스텝. `All` 이 bounds 를 켜지 않음도 확인 |
| 2 | `Bounds_ArenaAndPrologue_IgnoreTheExpandedBounds` | 아레나/프롤로그는 `Everything` 을 넘겨도 확장 안 함, 각 2400틱 |
| 3 | `Resolve_FallsBackToFrozenAndNeverShrinks` | 비활성 / 반쪽 설정 / 축소 요청 전부 동결값 |
| 4 | `ExpandedBounds_StayInsideThePlateAndInsideEveryGimmickSpan` | 링 4변 ⊂ 플레이트, 플레이어 x ⊂ 기믹 span, 스폰 8점 ⊂ 확장 타원 |
| 5 | `ExpandedBounds_On_ClampHoldsAndTheExtraSpaceIsReachable` | 7200틱 동안 플레이어·전체 적이 확장 타원 내부 + **동결 타원 밖까지 실제 도달**(미도달이면 명시 실패) |
| 6 | `ExpandedBounds_On_SameInputsProduceIdenticalRuns` | **결정론** 5400틱, 좌표·적 좌표까지 |
| 7 | `StopE_MatchesTheRingDerivation` | 동결 입력 시 `EnvironmentBuilder` 상수 정확 재현 + 확장 시 플레이어 정지선이 항상 적 정지선 안쪽 |
| 8 | `Bounds_AreResolvedOnceAndSurviveRestart` | 런 중 불변, Restart 후 동일, 리스타트 런 == 신규 런 락스텝 |

### 6-B.9 결정론 근거

RNG 0건. 반축은 생성자에서 1회 해석되는 상수 2개이고, `ClampToArena` 는 그
2개만 읽는 순수 산술이다. 게이트 OFF 경로는 필드 이니셜라이저가 동결 상수를 넣으므로
**코드 경로가 분기하지 않는다** — 개정 전과 같은 식에 같은 값이 들어간다.
4레인 pre/post 다이제스트 **224행 무이동**을 W-MV 반영 후 재실행해 재확인했다.

### 6-B.10 View 연동 필요 항목 (W-MV) — 코드 수정 안 함

| # | 항목 | 필요한 것 |
|---|---|---|
| MV-1 | **게이트 켜기** | `GameDirector` 던전 시작 시 `new CinderSim(config, new DungeonProgressionConfig { AdaptiveWaves = …, GradedLoot = …, Bounds = DungeonBoundsSpec.Expanded })`. `DungeonProgressionConfig.Everything` 이 셋 다 켜는 프리셋 |
| MV-2 | **벽 링 동기 (필수 — 이것 없이 MV-1 만 켜면 플레이어가 벽을 통과한다)** | `Assets/Scripts/View/EnvironmentBuilder.cs:536-537, 542-543` 의 `HalfW` / `HalfH` / `StopE` 를 상수에서 **심 게시값**으로 전환: `HalfW ← IDungeonProgressionSnapshot.BoundsHalfWidth`, `HalfH ← BoundsHalfHeight`, `StopE ← DungeonBoundsSpec.EnemyStopE(BoundsHalfWidth)`. 동결값 입력 시 현재 상수를 정확히 재현함이 테스트 W-MV-7 로 보장됨 |
| MV-3 | `EnvironmentBuilder.EnemyStopE` / `PlayerStopE` 공개 프로퍼티 (`:41-48`) | 같은 전환. 현재 `SimConfig` 상수에서 직접 파생 |
| MV-4 | 카메라 프레이밍 | 플레이필드 높이가 506 → 802 px(×1.59). `CameraRig` 던전 거리 20 / 24.5 가 확장된 세로를 담는지 재확인 필요 — 미측정 |
| MV-5 | Zone A 바닥 패널 | `EnvironmentBuilder.cs:848` 이 `EllipseE(x,y) > StopE` 로 Zone A 내접을 거른다. 링이 커지면 패널 배치 표(`FloorMidRowX`/`FloorOuterRowX`, `:908-910`)가 새 영역을 덮지 않아 확장부가 민무늬가 될 수 있음 |
| MV-6 | 미니맵/HUD | 미니맵이 있다면 반축을 하드코딩하지 말고 `BoundsHalfWidth/Height` 를 읽을 것 |

### 6-B.11 W-MV 사람 판단 필요

| # | 항목 |
|---|---|
| MV-D1 | **x 축을 기믹 span 밖으로 더 넓힐 것인가.** 554 는 재의 벽·조류가 플레이필드를 완전히 덮는 최댓값이다. 더 넓히면 두 기믹이 회피 가능해진다. 넓히려면 `CampaignSpec.WallEdgeX/WallEdgeRightX/CurrentHalfW`(FROZEN `CampaignTypes.cs`) 를 함께 개정하는 **별도 증보**가 필요 — 이번 범위 밖으로 뒀다 |
| MV-D2 | Zone C 테라스(x −1740..3260)까지 넓히는 안은 **기각**. 테라스는 프러스텀 충전용으로 링 밖에 의도적으로 깔린 장식이며(`EnvironmentBuilder.cs:1296-1306` 주석), 걸을 수 있는 평면이 아니다(gallery +0.8 / bridge +1.1 높이) |
| MV-D3 | `ViewWorld.Scale` 이 이번 사이클에 0.01 → **0.0125** 로 바뀌었다(`ViewWorld.cs:17`, 다른 레인). 시드 문서 §3 이 "건드리면 안 됨"으로 못박은 값이라 충돌 여부 확인 필요. 심 수치와는 무관 |

## 7. Git 상태

커밋·스테이징·push **없음**. 변경은 워킹트리에만 존재.
파괴적 작업 없음. 다른 세션 소유 파일(`SceneBuilder.cs`,
`EnvironmentBuilder.cs`, `graphify-out/*`) 무접촉.
