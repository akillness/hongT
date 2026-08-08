# Sim 레인 2 — W6 보스 다양화 (AMENDMENT #16) 리포트

- 작성: 2026-08-08, sim-lane2
- 스펙: `docs/SIM_SPEC_HACKSLASH.md` "Frozen Contract Amendment #16" (로컬 절 **§20**)
- 커밋 없음 (지시대로). Unity 미실행 (지시대로). 검증은 스탠드얼론 dotnet 8 하네스.

---

## 1. 증보 번호 배정

부록 A canonical 원장의 마지막 배정이 **#15**(던전 이동 한계)라 다음 가용 번호인
**#16** 을 썼다. 로컬 절 번호 **§20**. 시드 문서가 "#16 후보"로 지칭한 것과 결과가
일치했다 (#13/#14 때처럼 밀 필요가 없었다). 부록 A 표에 DRAFT 행 1개 추가.

---

## 2. 착수 전 실측 [OBSERVED]

| 항목 | 관측 | 근거 |
|---|---|---|
| 보스 visual | 2종 (`BossCommander`/`BossMonarch`) | `SimTypes.cs:15` |
| 페이즈 테이블 | **1개, 전 보스 공유** (0.50/0.20 임계) | `HackTypes.cs:980-997` |
| 두 visual 의 심 차이 | `MonarchPhase2Escorts = 3` **한 줄뿐** | `CinderSim.cs:2205-2214`(수정 전 L2139) |
| 보스 공격 쿨다운 | 일반 적과 **완전 동일** 식 | `CinderSim.cs:2941-2944` |
| 보스 접촉 프레임 | 일반 적과 동일 `EnemyContactFrame = 2` | `CinderSim.cs:50`, `:3069` |
| `BossAttackInterval`/`BossTelegraph`/`BossSkillCooldown` | **선언만, 심 소비 0** | 부록 B 정오표 D1 확인 |
| 심이 받는 스테이지 id | `StageCatalog.SimAnchorId` = `CampaignStages.Ids` **6종** | `GameDirector.cs:402`, `CampaignTypes.cs:263-266` |

→ 스테이지 보스 6종이 체력 총량과 표시명만 다른 같은 적이었다.

---

## 3. 설계 — 아키타입 4종 [TARGET]

명명은 이미 카탈로그에 있는 보스 표시명(`StageCatalog.cs:166/182/198` — "Cinder
Warden" / "Veil Tactician" / "Gate Sovereign")을 그대로 채택했다. 심 아키타입 ·
HUD 이름 · 자산 레인의 GLB(`s1-cinder-warden`/`s2-veil-tactician`/
`s3-gate-sovereign`)가 서로 어긋날 수 없다.

### 3.1 수치 표 (전문은 스펙 §20.2)

| 축 | **None**(동결) | **Warden** | **Tactician** | **Sovereign** | **Monarch** |
|---|---|---|---|---|---|
| 페이즈 수 | 3 | **2** | 3 | 3 | 3 |
| P2 / P3 진입 HP율 | 0.50 / 0.20 | **0.55 / —** | **0.72 / 0.38** | **0.66 / 0.33** | 0.50 / 0.20 |
| 캐던스 배율 (쿨다운 ×) | 1.00/1.00/1.00 | **1.55/1.34/—** | **0.72/0.62/0.54** | **1.12/0.90/0.68** | **1.00/0.85/0.72** |
| 이동 배율 | 1.00/1.25/1.45 | **0.82/0.96/—** | **1.30/1.52/1.74** | **1.00/1.28/1.60** | **1.05/1.32/1.55** |
| 사거리 배율 | 1.00/1.10/1.20 | **1.34/1.48/—** | **0.90/0.95/1.00** | **1.06/1.16/1.26** | 1.00/1.10/**1.22** |
| 피해 배율 | 1.00/1.25/1.45 | **1.34/1.72/—** | **0.84/0.94/1.06** | **1.00/1.22/1.48** | **1.05/1.32/1.58** |
| 텔레그래프 (접촉 프레임) | 2/2/2 | **3/3/—** | **1/1/1** | **3/2/1** | **2/2/1** |
| 텔레그래프 (초) | 0.167 | **0.250** | **0.083** | **0.250→0.083** | 0.167→**0.083** |
| 경계 소환 P1/P2/P3 | 0/0/0 | **0/0/0** | **0/3/2** | **0/1/2** | 0/**3**/0 |
| 체력 배율 | 1.00 | **1.28** | **0.78** | 1.00 | **1.15** |

요구된 "최소 3축"은 **5축(페이즈 구조·캐던스·이동·텔레그래프·몸)** 으로 초과 달성했고,
쌍별 3축 이상 차이를 테스트가 강제한다.

설계 근거 요약:
- **Warden** — 서서 기다렸다 회피하고 후딜을 때리는 상대. 0.25 s 윈드업(표에서 가장 김),
  사거리 1.34/1.48(가장 넓음), 2페이즈(짧고 무거운 싸움).
- **Tactician** — 계속 친다(0.72→0.54)지만 한 대가 싸다(0.84→1.06). 위협은 양 경계
  소환 5기와 1.74배 접근 속도에서 나온다. 체력 0.78 로 가장 무름.
- **Sovereign** — 5축 전부가 모든 경계에서 움직인다. 특히 텔레그래프가 3→2→1 프레임으로
  걸어서 P1 에서 익힌 읽기가 두 번 틀린다.
- **Monarch** — 동결 3페이즈 구조 유지 + 전축 강화. 캐던스 1.00/0.85/0.72 는 동결
  `HackSpec.BossAttackInterval`(1.37/1.16/0.99)의 P1 정규화 비율이다 — 선언만 있고
  소비되지 않던 D1 벡터가 처음으로 형태를 갖는다 (상수 자체는 무개정).

### 3.2 매핑 (결정론 정적 표, RNG·해시 없음)

| 스테이지 id | 앵커 | 아키타입 |
|---|---|---|
| `cinder-span` | 자신 | Warden |
| `ember-gallery` | cinder-span | Warden |
| `abyss-chancel` | 자신 | Tactician |
| `witness-well` | abyss-chancel | Tactician |
| `echo-throne` | 자신 | Sovereign |
| `ash-verdict` | echo-throne | Sovereign |
| `cinder-sluice` | 자신 | Tactician [INFERENCE] |
| `ember-bastion` | 자신 | Warden [INFERENCE] |
| `ash-march` | 자신 | **Monarch** (최종) |

심은 앵커 6종만 받지만 논리 id 3종도 **자기 앵커와 같은 아키타입**으로 표에 실어
두었다 — 어떤 호출부가 논리 id 를 넘겨도 같은 보스에 착지한다.
미매핑·null·빈 문자열·대소문자 불일치 → **`None`(동결 전투)**.

---

## 4. 변경 파일 (경로:라인)

### `Assets/Scripts/Sim/DungeonProgressionSpec.cs` (비동결)

| 라인 | 내용 |
|---|---|
| `:44-50` | `DungeonProgressionConfig.BossVariety` 플래그 |
| `:52-53` | `Any` 에 포함 |
| `:55-73` | `All` **비포함**(사유 주석) / `Everything` **포함** |
| `:501-517` | `enum BossArchetype { None, Warden, Tactician, Sovereign, Monarch }` |
| `:524-620` | `sealed class BossArchetypeProfile` (per-phase 벡터 6종 + PhaseCount + HealthMul + `TelegraphSeconds`) |
| `:604-...` | `static class BossVarietySpec` — 클립 상수, 프로파일 표 5종, `For`, 스테이지 표, `ArchetypeFor`, `ClampPhaseIndex`, `PhaseIndexFor` |
| `:766-778` | `StageTable` 9행 |
| `:884-903` | `IDungeonProgressionSnapshot` 증보 4멤버 |

### `Assets/Scripts/Sim/CinderSim.cs` (비동결)

| 라인 | 내용 |
|---|---|
| `:331-337` | `private readonly BossArchetype _bossArchetype` |
| `:496-502` | 생성자 1회 해석 (`_stageId` 확정 직후, 던전 게이트 뒤) |
| `:707-715` | 스냅샷 4프로퍼티 |
| `:2118-2126` | `BossProfileOrNull()` — 동결 경로는 null 로 분기 유지 |
| `:2128-2154` | `ContactFrameFor(in Enemy)` — 텔레그래프 축, 1..`AttackClipFrames-1` 클램프 |
| `:2190-2224` | `UpdateBossPhase` — 아키타입 임계·페이즈 수·경계 소환 |
| `:2945-2957` | 스윙 커밋 시 캐던스 배율 |
| `:3065-3086` | 접촉 프레임 + 사거리 배율 |
| `:3096-3113` | 피해 배율 (동결 "P2 전엔 배율 없음" 절은 else 로 보존) |
| `:3518-3530` | `SpawnEnemy` 보스 체력 배율 (던전 분기 안) |

### `Assets/Tests/EditMode/BossVarietyTests.cs` (신규, + `.cs.meta`)

FROZEN 파일 `SimTypes.cs`·`HackTypes.cs`·`CampaignTypes.cs` **무수정**.
View/Plugins/Editor **무수정**.

---

## 5. 테스트 — EditMode 13종 (요구 ≥8)

| # | 테스트 | 범주 |
|---|---|---|
| 1 | `GateOff_KeepsTheFrozenBossFight_InLockstep` | **게이트 off 락스텝 불변성** (legacy == default == All, 7200틱, 보스 HP·MaxHP·페이즈·이벤트 비트 포함) |
| 2 | `GateOn_OutsideTheDungeon_StaysOnTheFrozenBoss` | 스코프 |
| 3 | `NoneProfile_RestatesTheFrozenVectors` | 동결 재진술 (+ HP율 101점 페이즈 인덱스 동일) |
| 4 | `StageTable_CoversEveryStage_AndFallsBackToNone` | **매핑 표 무결성** |
| 5 | `EveryProfile_IsMonotoneHarder_AndStructurallyLegal` | 표 구조·단조 불변식 |
| 6 | `EveryArchetypePair_DiffersOnAtLeastThreeAxes` | 차별화 하한 강제 |
| 7 | `Warden_IsATwoPhaseFight_UnlikeTheFrozenBoss` | **차별화 실증(라이브)** |
| 8 | `Tactician_SwingsFasterAndSummonsAtBothBoundaries` | **차별화 실증(라이브)** |
| 9 | `Sovereign_ShiftsItsTelegraphAtEveryBoundary` | **차별화 실증(라이브)** |
| 10 | `Monarch_KeepsTheFrozenThresholds_ButIsReinforced` | **차별화 실증(라이브)** |
| 11 | `CadenceAxis_SeparatesTheThreeStageArchetypes` | 캐던스 축 실측 분리 |
| 12 | `GatedRuns_AreReproducible_OnEveryArchetype` | **결정론** (4스테이지 × 2런 락스텝 + digest) |
| 13 | `Archetype_IsFixedForTheRun_AndSurvivesRestart` | 결정론 (런 중·Restart 불변) |

### 5.1 실행 결과 [OBSERVED]

`Assets/Scripts/Sim/*.cs` + 순수-Sim EditMode 11파일(`BossVarietyTests`,
`CinderSimTests`, `DifficultyGroupAiTests`, `DungeonBoundsTests`, `HackSimTests`,
`LootGradeTests`, `MomentumTests`, `SigilTests`, `TrainingSurgeTests`,
`WaveBudgetDdaTests`, `WaveTelegraphTests`)을 dotnet 8 + NUnit 3.14 로 컴파일·실행:

```
Passed!  - Failed: 0, Passed: 216, Skipped: 0, Total: 216, Duration: 2 s
```

= 기존 순수-Sim 회귀 203종 + 신규 13종 전부 그린.
`dotnet build`(Sim 소스만): **0 error / 0 warning**.

`ClipTableTests` 4종은 하네스에서 제외했다 — `CinderCourt.EditorTools.
CharacterImportPipeline` 을 Editor AppDomain 에서 리플렉션으로 찾는 테스트라
Unity 밖에서는 OneTimeSetUp 이 구조적으로 실패한다 (본 변경과 무관, 오케스트레이터
통합 실행 대상).

### 5.2 pre/post 다이제스트 추가성 증명 [OBSERVED]

HEAD 의 `Assets/Scripts/Sim/*.cs` 와 작업 트리를 **각각** 별도 프로젝트로 빌드해
동일 프로브(아레나 동결 2런 · hack-arena · 프롤로그 · 던전 6스테이지 × legacy 2 ×
`All` 2 × `default` 1 = **총 34행**, score/wave/kills/relics/HP/reason/좌표/기름/
보스HP/보스MaxHP/보스페이즈/적수/대기/XP/레벨 `R` 포맷)를 돌려 diff:

```
34행 완전 동일 (md5 0b24ac3ba1fc5734d84d9390f262f663 양쪽 일치)
```

즉 게이트 OFF 경로는 **부동소수 하위비트까지** 이동이 없다.
(CLAUDE.md §4 대로 이는 스탠드얼론 증명이며, 배포 진실은 Unity 골든
`DungeonGoldenDigestTests` 다 — 통합 실행 대기.)

### 5.3 라이브 실측 (max-stat 파일럿, 동결 런 대조) [OBSERVED]

카이팅 파일럿(스테이지 클리어까지):

| 스테이지 | 게이트 | 보스 MaxHP | 최대 페이즈 | P2/P3 진입 HP율 | 경계 소환 |
|---|---|---|---|---|---|
| cinder-span | OFF | 5076.0 | 3 | 0.456 / 0.198 | 0 |
| cinder-span | **ON (Warden)** | **6497.3** (×1.28) | **2** | **0.523 / —** | 0 |
| abyss-chancel | OFF | 5472.0 | 3 | 0.468 / 0.188 | 0 |
| abyss-chancel | **ON (Tactician)** | **4268.2** (×0.78) | 3 | **0.702 / 0.371** | **5** |
| echo-throne | OFF | 5868.0 | 3 | 0.479 / 0.177 | 3 |
| echo-throne | **ON (Sovereign)** | 5868.0 (×1.00) | 3 | **0.642 / 0.312** | 3 |
| ash-march | OFF | 6660.0 | 3 | 0.485 / 0.199 | 3 |
| ash-march | **ON (Monarch)** | **7659.0** (×1.15) | 3 | 0.490 / 0.188 (동결 유지) | 3 |

캐던스 계측 파일럿(보스 면전 근접, 스윙 시작 간격 평균 틱):

| 스테이지 | 동결 | 게이트 ON | 비율 | 표 P1 값 |
|---|---|---|---|---|
| cinder-span (Warden) | 83.9 | 128.2 | **1.528** | 1.55 |
| abyss-chancel (Tactician) | 85.0 | 56.8 | **0.668** | 0.72 (P2 0.62 혼입) |
| echo-throne (Sovereign) | 86.1 | 91.9 | **1.067** | 1.12 (P2 0.90 혼입) |
| ash-march (Monarch) | 89.0 | 89.0 | **1.000** | 1.00 (P1 만 도달) |

---

## 6. View 연동 필요 항목 (Sim 레인은 View 코드를 수정하지 않았다)

| # | 항목 | 필요한 것 | 접점 |
|---|---|---|---|
| **V6-1** | **게이트 켜기** | `GameDirector` 던전 진입에서 `DungeonProgressionConfig` 에 `BossVariety = true`. **이 한 줄 없이는 증보 전체가 완전 비활성.** #15 와 마찬가지로 `All` 이 아니라 `Everything`(또는 명시 조합)이 필요 | `GameDirector.cs:402` 부근 |
| **V6-2** | **텔레그래프 지속시간** | `IDungeonProgressionSnapshot.BossTelegraphSeconds` 게시 중 (현재 페이즈 윈드업, 0.083~0.250 s). 텔레그래프 링 애니메이션 길이를 이 값으로 구동해야 한다. **하나의 고정 길이로는 Warden 0.250 s 와 Tactician 0.083 s 를 같이 못 그린다** — 이것이 `All` 에 #16 을 안 넣은 이유 | `VfxDirector` 텔레그래프 링 |
| **V6-3** | **아키타입별 연출·자산 선택** | `BossArchetype` enum 게시 중. 보스 GLB(`s1-cinder-warden`/`s2-veil-tactician`/`s3-gate-sovereign`)·텔레그래프 색·SFX 세트를 visual 이 아니라 **아키타입**으로 고르면 자산 레인 임포트와 정합 | `StageCatalog.BossPresentation` / `GameView` |
| **V6-4** | **페이즈 pip 개수** | `BossPhaseCount`(2 또는 3) 게시 중. HUD 가 3페이즈를 하드코딩하고 있으면 Warden 에서 채워지지 않는 pip 이 남는다 | `HudView` (UI 레인 소유) |
| **V6-5** | 소환 예고 | Tactician 은 양 경계에서 3기+2기를 부른다. `SimEvents.BossPhase2`(모든 경계 공통) + `BossArchetype` 조합으로 예고 연출 가능. **신규 SimEvent 비트는 추가하지 않았다** (`SimEvents` 는 FROZEN `SimTypes.cs`) | `VfxDirector` |
| **V6-6** | `BossVarietyActive` 의미 | 게이트 플래그가 아니라 **해석 결과**다. 게이트 ON + 미매핑 스테이지 = false. View 는 이 값으로만 분기할 것 | 전체 |

---

## 7. 미해결 / 사람 판단 필요

| # | 항목 | 상태 |
|---|---|---|
| **D16-A** | **증보 번호 서명** | #16 / §20 배정, 부록 A 에 DRAFT 행 추가. #6(각인) canonical 번호 미결(D13)과 함께 오퍼레이터 서명 필요 |
| **D16-B** | **텔레그래프 해상도가 거칠다** | 공격 클립이 **5프레임 @12fps** 라 접촉 프레임은 1/2/3/4 네 값뿐이다(0.083/0.167/0.250/0.333 s). 아키타형 4종에 4단계는 빠듯하다. 더 세밀한 텔레그래프를 원하면 `AttackClipFrames`/`AttackClipFps` 개정이 필요하고, 그건 동결 애니메이션 계약 변경이라 이 증보 범위 밖 |
| **D16-C** | **`cinder-sluice`/`ember-bastion` 배정은 [INFERENCE]** | 두 cycle-2 앵커의 표시명("Sluice Keeper"/"Bastion Sentinel")은 3종 아키타입 중 어느 것도 직접 지칭하지 않는다. 스테이지 정체성 기믹(dash·조류 → Tactician, ward·방벽 → Warden)으로 추론했다. 디자이너 확정 필요 |
| **D16-D** | **`echo-throne` visual 과 아키타입 불일치** | 이 스테이지의 동결 `BossVisual` 은 `BossMonarch` 인데 아키타입은 Sovereign 이다. 표시명이 "Gate Sovereign" 이므로 아키타입 쪽이 맞고 visual 이 유물이라고 판단했으나, **`BossVisual` 은 FROZEN `CampaignTypes.cs:381`** 이라 손대지 않았다. View 가 아키타입으로 GLB 를 고르면(V6-3) 자연 해소 |
| **D16-E** | **Warden 2페이즈의 P3 슬램 넉백** | `HackSpec.BossSlamKnockbackDistance` 발동 조건은 `_bossPhase >= 3` 이라 Warden 은 **영원히 넉백하지 않는다**. "중장 보스가 안 밀친다"가 어색하면 조건을 "마지막 페이즈"로 바꿔야 하는데, 그건 동결 런의 P3 의미를 건드리므로 별도 판단 |
| **D16-F** | **Tactician 총 위협 상향폭** | P3 DPS 대리값(피해/캐던스) 1.06/0.54 = 1.96 으로 표에서 가장 높다. 체력 0.78·사거리 1.00 으로 상쇄했으나 실전 튜닝은 디자이너·PM 몫 |
| **D16-G** | **Unity 런타임 검증** | 지시대로 Unity 미실행. EditMode 전량(`DungeonGoldenDigestTests` 포함) + WebGL 빌드는 오케스트레이터 통합 실행 대기 |
