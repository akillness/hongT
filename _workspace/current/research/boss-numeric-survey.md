# S8 보스 페이즈 — 현행 수치 계약 전수 조사

**목적**: S8(보스 페이즈 시스템)이 올라타야 할 현행 수치 계약을 근거 기반으로 확정한다.
**범위**: 조사·계산·문서화만. 코드 수정 0건. Unity 미기동.
**표기**: `[OBSERVED]` 직접 확인 / `[INFERENCE]` 추론 / `[TARGET]` 목표.

## 0. 측정 방법 (재현 절차)

`Assets/Scripts/Sim/*.cs` 6개 파일을 `/tmp/boss-survey-harness/` 로 **복사**해 net8.0
콘솔 프로젝트로 링크하고, `Assets/Tests/EditMode/HackSimTests.cs:174-266` 의 결정론적
카이팅 `Pilot`을 그대로 옮겨 구동했다. 저장소 파일은 읽기만 했고 Unity는 열지 않았다.

- `[OBSERVED]` 빌드: 경고 0 / 에러 0 (dotnet 8.0.301).
- `[OBSERVED]` **검증 앵커**: 만스탯 cinder-span 클리어 = **t=2701틱**.
  `_workspace/current/engineering/gjc-hackslash-lane-report.md:155` 의 기록값 `t=2700`과
  1틱 차이(내 카운터는 1-베이스, 보고서는 0-베이스). 하네스가 실주행을 재현함을 확인.

아래 모든 표의 "측정" 열은 이 하네스 출력이고, "식" 열은 인용된 소스 줄의 상수를 그대로 계산한 값이다.

### 0.1 측정 리비전 고정 (중요 — 병행 편집 발생)

**이 문서의 모든 수치는 커밋 `96a55bb` (HEAD) 기준이다.**
`[OBSERVED]` `/tmp` 하네스의 `SimTypes.cs / CampaignTypes.cs / HackTypes.cs / CinderSim.cs`
4개 파일이 `git show HEAD:...` 와 **바이트 단위 동일**함을 확인했다.

`[OBSERVED]` 조사 도중(18:13~18:18) **제3의 세션이 동결 파일을 편집 중**임을 확인했다.
본 레인은 `Assets/` 하위에 쓰기 0건이며(유일한 산출물이 이 파일), 그 편집은 우리 것이 아니다.
작업 트리의 미커밋 변경(`git diff`, +101/−16):

| 파일 | 변경 |
|---|---|
| `CampaignTypes.cs` | `Waves` **5/6/7 → 9/11/13** |
| `HackTypes.cs` | `BossPhase3HealthFraction=0.20`, `BossAttackInterval={1.37,1.16,0.99}`, `BossTelegraph={0.80,0.80,0.80}`, `BossSkillCooldown={5.00,4.00,3.25}`, `BossSpeedMul={1.00,1.25,1.45}`, `BossRangeMul={1.00,1.10,1.20}`, `BossPhaseIndexFor()` 신설 |
| `CinderSim.cs`, `CampaignSimTests.cs` | 연동 수정 |

즉 **S8 구현이 이미 착수된 상태**이며, 그 코멘트는 `boss-phase-metric-definition §6`을 인용한다
(`_workspace/current/design/boss-phase-metric-definition.md`, untracked).
본 문서는 **그 변경이 올라타는 기준선(baseline)** 을 기록한 것으로 읽어야 한다.

**웨이브 증가가 보스 HP에 미치는 영향** (식 `(86 + min(140,(bossWave−1)×11)) × 6` 그대로 적용):

| 스테이지 | 구 Waves | 구 보스웨이브/HP | 신 Waves | 신 보스웨이브/HP | HP 배수 | 140 캡 도달 |
|---|---|---|---|---|---|---|
| cinder-span | 5 | 6 / 846 | 9 | 10 / **1110** | 1.31× | 아니오 |
| abyss-chancel | 6 | 7 / 912 | 11 | 12 / **1242** | 1.36× | 아니오 |
| echo-throne | 7 | 8 / 978 | 13 | 14 / **1356** | 1.39× | **예** |

`[OBSERVED]` **echo-throne의 새 보스웨이브 14는 HP 캡이 정확히 걸리는 지점이다**
(`(14−1)×11 = 143 > 140`, `HackTypes.cs:252`). 웨이브 14 이후로 보스 HP는 웨이브에 따라
더 이상 증가하지 않는다 — **"웨이브를 늘려 보스전을 늘린다"는 레버가 마지막 스테이지에서 소진된다.**

`[INFERENCE]` 이론 TTK는 cinder-span 기준 L1 3.15 → **4.14 s**, 만렉 1.54 → **2.02 s**로 늘지만,
§4.4가 보인 "30 s 보스전에 필요한 HP 7,265~16,523"에는 여전히 크게 못 미친다.
**웨이브 증량만으로는 3페이즈가 읽힐 길이가 확보되지 않는다.**

---

## 1. 보스 현행 상태 — `UpdateBossPhase`의 실제 동작

### 1.1 전환 조건과 전환 시 바뀌는 것 전부

| 항목 | 값 / 동작 | 인용 |
|---|---|---|
| 호출 조건 | `_dungeon && _mode != GameOver` — **아레나 보스는 페이즈 없음** | `CinderSim.cs:652-655` |
| 호출 순서 | `UpdateEnemies` **다음**, `UpdateHazards` 앞 | `CinderSim.cs:651-655` |
| 대상 선택 | 살아있는 `IsBoss` 중 **최저 인덱스 1기**(`break`) | `CinderSim.cs:1193-1200` |
| 보스 부재 시 | `_bossHp=0, _bossMaxHp=0, _bossPhase=0` 후 return | `CinderSim.cs:1202-1208` |
| 전환 조건 | `!_bossPhase2Done && _bossHp <= _bossMaxHp * 0.5` | `CinderSim.cs:1213`, `HackTypes.cs:337` |
| 전환 횟수 | `_bossPhase2Done` 1회 래치 — **런당 단 1회** | `CinderSim.cs:1215`, 리셋은 `CinderSim.cs:2160` |
| 전환 시 ① | `SimEvents.BossPhase2` 발행 | `CinderSim.cs:1216`, `SimTypes.cs:103` |
| 전환 시 ② | `Visual == BossMonarch`면 `_pendingSpawns += 3` | `CinderSim.cs:1217-1221`, `HackTypes.cs:340` |
| 전환 시 ③ | `_bossPhase = 2` (그 외 시점은 1) | `CinderSim.cs:1224` |
| 사후 효과 A | 이동속도 `× 1.25` (보스 0.7 배율 **위에**) | `CinderSim.cs:1731-1733`, `HackTypes.cs:338` |
| 사후 효과 B | 접촉 피해 `× 1.25` (보스 2.0 배율 **위에**) | `CinderSim.cs:1704-1708`, `HackTypes.cs:339` |
| 클리어 후 | `BossPhase`를 0으로 되돌리지 않음(결과 오버레이용) | `HackTypes.cs:211-215` |

`[OBSERVED]` **전환 시 바뀌는 것은 위 5가지가 전부다.** 공격속도·사거리·패턴·행동트리는
전환 대상이 아니다 — 코드에 존재하지 않는다(§2.3).

### 1.2 1틱 지연 (설계 시 반드시 알아야 할 부작용)

`UpdateEnemies`(접촉 피해 판정, `CinderSim.cs:1704`)가 `UpdateBossPhase`(`_bossPhase` 갱신,
`CinderSim.cs:1224`)보다 **먼저** 돈다. 따라서 보스 HP가 50%를 깬 그 틱의 접촉 피해는
**아직 페이즈 1 배율**로 들어간다. `[OBSERVED]` 호출 순서 `CinderSim.cs:651` vs `654`.
`[INFERENCE]` S8이 페이즈 진입 즉시 패턴을 바꾸려면 이 순서를 인지하거나 뒤집어야 하며,
뒤집으면 기존 Digest가 바뀔 수 있다.

### 1.3 `HackSpec.BossPhase2*` 상수 전량

| 상수 | 값 | 인용 |
|---|---|---|
| `BossPhase2HealthFraction` | `0.5f` | `HackTypes.cs:337` |
| `BossPhase2SpeedMul` | `1.25f` | `HackTypes.cs:338` |
| `BossPhase2DamageMul` | `1.25f` | `HackTypes.cs:339` |
| `MonarchPhase2Escorts` | `3` | `HackTypes.cs:340` |

---

## 2. 보스 기본 스태트

### 2.1 HP 계산식과 실제값

식: `(86 + min(140, (bossWave-1) × 11)) × 6`
(`CinderSim.cs:1966-1968` 던전 몹 커브 → `CinderSim.cs:1981` 보스 배율)
상수: `DungeonEnemyBaseHealth=86`, `PerWave=11`, `Cap=140` (`HackTypes.cs:250-252`),
`BossHealthMul=6` (`SimTypes.cs:203`). 보스 웨이브 = `Waves + 1` (`CinderSim.cs:1901`).

| 스테이지 | Waves | 보스웨이브 | 몹 HP | **보스 HP** | 비주얼 | 호위 | 인용 |
|---|---|---|---|---|---|---|---|
| cinder-span | 5 | 6 | 141 | **846** | BossCommander | 3 | `CampaignTypes.cs:234-236`, 측정 |
| abyss-chancel | 6 | 7 | 152 | **912** | BossCommander | 5 | `CampaignTypes.cs:240-242`, 측정 |
| echo-throne | 7 | 8 | 163 | **978** | BossMonarch | 7 | `CampaignTypes.cs:246-248`, 측정 |
| (아레나 w5 참고) | — | 5 | 94 | 564 | BossCommander | — | `CinderSimTests.cs:776-780` |

`[OBSERVED]` 하네스 측정 `BossMaxHp` = 846 / 912 / 978 — 식과 정확히 일치.
호위 = `min(8, 3 + stageIndex×2)` (`CinderSim.cs:465-468`, `CampaignTypes.cs:138-140`).
`[OBSERVED]` **HP 캡 140은 웨이브 14에서 걸린다** — 캠페인 최대 보스웨이브가 8이므로
현행 3스테이지에서는 **캡에 닿지 않는다**. HP는 웨이브당 66(=11×6)씩 선형 증가 중.

### 2.2 보스 vs 일반 적 — 무엇이 다른가

| 축 | 일반 적 | 정예 | **보스** | 보스 배율 | 인용 |
|---|---|---|---|---|---|
| HP | ×1 | **×3** | **×6** | `BossHealthMul` | `SimTypes.cs:203`, `HackTypes.cs:314` |
| 접촉 피해 | ×1 | **×1.5** | **×2** (P2 **×2.5**) | `BossDamageMul` | `SimTypes.cs:203`, `CinderSim.cs:1695-1708` |
| 이동속도 | ×1 | ×1 | **×0.7** (P2 **×0.875**) | `BossSpeedMul` | `SimTypes.cs:203`, `CinderSim.cs:1728-1734` |
| 스케일 | 1.0 | 1.35 | **1.6** | `BossScale` | `SimTypes.cs:203`, `CinderSim.cs:2005` |
| **공격 주기** | 동일 | 동일 | **동일 — 배율 없음** | — | `CinderSim.cs:1601-1602` |
| **공격 사거리** | 76 | 76 | **76 — 배율 없음** | — | `CinderSim.cs:1598` |
| **접촉 판정 반경** | 90 | 90 | **90 — 배율 없음** | — | `CinderSim.cs:1690` |
| 이동 애니메이션 | `Move` | `Move` | `Run` (보스 전용) | — | `CinderSim.cs:1653-1654` |
| XP | 10 | 25 | **150** | — | `HackTypes.cs:303-305` |
| 드롭 | id%3 로테 | 동일 | **RelicMote 확정** | — | `CinderSim.cs:1813-1817` |
| 처치 효과 | — | 시체 마커 | **랭크 상승 + StageCleared** | — | `CinderSim.cs:1781-1786` |

**`[OBSERVED]` 이 조사의 최대 발견**: 사용자가 언급한 4축 중 **이동속도만 보스 축으로 존재한다.**
공격속도·사거리는 보스/정예/일반이 **완전히 동일한 상수**를 쓰고 보스 분기 자체가 없다
(`CinderSim.cs:1598`, `1601-1602`, `1690` — `IsBoss` 조건문 없음).
쿨타임감소는 **보스가 스킬을 갖고 있지 않으므로 감소시킬 쿨타임이 존재하지 않는다**
(보스의 유일한 쿨타임 = 접촉 공격 쿨 `EnemyAttackCooldown`).

### 2.3 보스 실측 스태트 (스테이지별, 웨이브 종속)

`SpeedFor`: `min(128, 78 + wave×3.2 + (id%3)×2.5) × 0.7 [× 1.25]`
(`CinderSim.cs:1725-1734`, 상수 `CinderSim.cs:26-29`)
접촉: `min(18, 7 + floor(wave×0.8)) × 2 [× 1.25]` (`CinderSim.cs:1694-1708`, 상수 `:30-32`)
공격쿨: `1.22 + min(0.38, wave×0.025)` (`CinderSim.cs:1601-1602`, 상수 `SimTypes.cs:175`, `CinderSim.cs:24-25`)

| 스테이지 | 보스 id | id%3 | 웨이브 | 원속도 | **P1 속도** | **P2 속도** | **P1 접촉** | **P2 접촉** | 공격쿨 | P1 대인DPS | P2 대인DPS |
|---|---|---|---|---|---|---|---|---|---|---|---|
| cinder-span | 32 | 2 | 6 | 102.20 | **71.54** | **89.42** | **22.0** | **27.5** | 1.370 | 16.06 | 20.07 |
| abyss-chancel | 42 | 0 | 7 | 100.40 | **70.28** | **87.85** | **24.0** | **30.0** | 1.395 | 17.20 | 21.51 |
| echo-throne | 53 | 2 | 8 | 108.60 | **76.02** | **95.02** | **26.0** | **32.5** | 1.420 | 18.31 | 22.89 |

`[OBSERVED]` 보스 id는 하네스로 실측(스폰 순번 누적값). 나머지는 인용 상수로 계산.

**추격 성립 여부**: 플레이어 이속 218 (`SimTypes.cs:161`), 민첩 10에서 261.6
(`HackTypes.cs:168-169`, `:334`). 보스 최고속도는 P2 echo-throne의 **95.02**.
`[OBSERVED]` **보스는 어떤 페이즈에서도 플레이어를 따라잡지 못한다** (95.02 ≪ 218).
`[INFERENCE]` 이것이 §1의 측정에서 보스 접촉 피해가 거의 들어가지 않은 이유이고,
"이동속도 ×1.25"가 체감 난도에 사실상 기여하지 못하는 구조적 원인이다.

**생존 여유**: 만스탯 L6 플레이어 최대 HP 250 기준, 보스 접촉만으로 죽이려면
P1 10~12회 / P2 8~10회 필요(위 표). 공격쿨 1.37~1.42s이므로 **P2에서도 순수 보스 접촉으로는
11~14초 연속 피격**이 필요하다.

---

## 3. 플레이어 대미지 예산 — 계산 과정 전개

### 3.1 배율 체인 (곱연산 순서)

```
base      = 58 × (1 + 0.03·attack) × (1 + 0.06·weapon)      HackTypes.cs:157-160
effective = base × (1 + 0.04·(level-1)) × (1 + 0.08·extractStacks)   CinderSim.cs:2109-2112
```
상수: `PlayerDamage=58` `SimTypes.cs:162` / `AttackPerPoint=0.03` `HackTypes.cs:332` /
`WeaponDamagePerRank=0.06` `CampaignTypes.cs:129` / `LevelDamageBonus=0.04` `HackTypes.cs:308` /
`ExtractionDamageBonus=0.08` `HackTypes.cs:320` / 캡: 스탯 10 `HackTypes.cs:331`, 랭크 5 `CampaignTypes.cs:128`, 레벨 12 `HackTypes.cs:302`.

| 프로필 | base 계산 | base | effective 계산 | **effective** | **콤보 3타** |
|---|---|---|---|---|---|
| L1 신규 (a0/w0) | 58×1.00×1.00 | 58.00 | ×1.00×1.00 | **58.00** | **87.00** |
| L1 만스탯 (a10/w5) | 58×1.30×1.30 | 98.02 | ×1.00×1.00 | **98.02** | **147.03** |
| L12 만렉+만스탯 | 98.02 | 98.02 | ×(1+0.04×11)=1.44 | **141.15** | **211.72** |
| L12 + 추출 3스택 | 98.02 | 98.02 | ×1.44×1.24 | **175.02** | **262.54** |

`[OBSERVED]` 하네스 출력과 일치 (58.000 / 98.020 / 141.149 / 175.025).

### 3.2 기본공격 vs 콤보 — 던전은 콤보만 쓴다

던전은 `UpdateCombo`를 타고 아레나 기본공격(`PlayerAttackCooldown=0.48`, `SimTypes.cs:164`)은
쓰이지 않는다. 콤보 사이클:

| 타 | 스윙시간 | 틱(=ceil(s×60)) | 피해 배율 | 인용 |
|---|---|---|---|---|
| 1타 | 0.30 s | 18 | ×1.0 | `HackTypes.cs:242-243` |
| 2타 | 0.30 s | 18 | ×1.0 | 〃 |
| 3타 | 0.42 s | 26 | ×1.5 | 〃 |
| **합계** | **1.02 s** | **62틱 = 1.0333 s** | **×3.5** | — |

`[OBSERVED]` 스윙 종료는 `ActionTime >= ComboSwing[i]`로 판정되므로(`CinderSim.cs:1419-1427`)
실제 사이클은 62틱이다. 하네스 실측: **125회 스윙 / 2578틱 = 20.624 틱/스윙**,
이론값 20.667 틱/스윙 — 일치.

```
콤보 DPS = effective × 3.5 / 1.0333 s = effective × 3.3871
```

### 3.3 스킬 4종 — 피해·쿨타임·기름비용

| 스킬 | 피해 | 쿨타임 | 기름 | 원소 | 성장 배율 적용? | 인용 |
|---|---|---|---|---|---|---|
| Q 리프트볼트 | 145 (+주변 60% / 115px) | 6.5 s | 25 | Void | **아니오 — 상수** | `HackTypes.cs:267-273`, `CinderSim.cs:825-831` |
| E 그레이브펄스 | 26 × **6틱** = 156 | 4.0 s | 30 | Ember | **아니오** | `HackTypes.cs:275-281`, `CinderSim.cs:891-904` |
| R 애쉬노바 | 110 (+넉백 120) | 8.0 s | 45 | Ember | **아니오** | `HackTypes.cs:283-288`, `CinderSim.cs:856-867` |
| F 보이드에이기스 | 0 (실드 40 / 8 s) | 12.0 s | 30 | Frost | — | `HackTypes.cs:290-295` |
| (대시) | 0 | 1.6 s | 8 | — | — | `HackTypes.cs:255-258` |

`[OBSERVED]` 펄스 틱 수는 하네스로 실측: `duration 3.0s / interval 0.5s` → **6틱**, 총 156.
`[OBSERVED]` **스킬 피해는 `_boltDamage` 등 생성자 고정 필드로, 레벨·스탯·장비·추출 배율을
전혀 받지 않는다** (`CinderSim.cs:206-211`, `279-281`). Ember Rest 룬만 ±10~20% 조정
(`CinderSim.cs:340-346`).

**원소 상성** (`HackTypes.cs:382-398`, 하네스로 계산 검증):

| 보스 | 원소 | 볼트(Void) | 펄스(Ember) | 노바(Ember) | 유효 스킬 DPS |
|---|---|---|---|---|---|
| BossCommander | Veil | **0.85** → 123.25 | 1.00 → 156 | 1.00 → 110 | **71.71** |
| BossMonarch | Void | 1.00 → 145 | **0.85** → 132.6 | **0.85** → 93.5 | **67.15** |

무상성 기준 스킬 DPS = 145/6.5 + 156/4 + 110/8 = 22.31 + 39.00 + 13.75 = **75.06**

### 3.4 초당 기대 DPS — 레벨 1과 만렉

```
총 DPS = effective × 3.3871  +  스킬 DPS(상성 반영)
```

| 프로필 | 콤보 DPS | 스킬 DPS(Cmd) | **총 DPS(Cmd)** | 총 DPS(Mon) | 콤보 비중 |
|---|---|---|---|---|---|
| **L1 신규** (a0/w0) | 58.00×3.3871 = **196.45** | 71.71 | **268.2** | 263.6 | 72.4% |
| L1 만스탯 (a10/w5) | 98.02×3.3871 = **332.00** | 71.71 | **403.7** | 399.1 | 81.6% |
| **L12 만렉+만스탯** | 141.15×3.3871 = **478.08** | 71.71 | **549.8** | 545.2 | 86.4% |
| L12 + 추출 3스택 | 175.02×3.3871 = **592.82** | 71.71 | **664.5** | 660.0 | 88.8% |

**`[OBSERVED]` L1 → 만렉 성장폭은 2.05배 (268.2 → 549.8)** 이고, 그중 스킬 기여는 고정이라
성장할수록 콤보 비중이 72% → 86%로 올라간다.

### 3.5 기름 게이트 — 지속전에서는 위 DPS가 유지되지 않는다

기름 재생 = `7 × (1 + 0.08·lantern) + 0.3 × (level-1)`
(`HackTypes.cs:171-173`, `CampaignTypes.cs:130`, `HackTypes.cs:310`, `CinderSim.cs:2114`), 상한 100 (`SimTypes.cs:184`),
처치당 +6 (`SimTypes.cs:186`, `CinderSim.cs:1777`).

공격 3종 풀로테 소모 = 25/6.5 + 30/4 + 45/8 = **16.97 기름/s** (에이기스 포함 시 19.47/s)

| 프로필 | 재생 | 풀로테 지속 가능 시간 | 이후 유지율 | 지속 스킬 DPS |
|---|---|---|---|---|
| L1 lantern0 | 7.00/s | **10.03 s** (4종 8.02 s) | 41.2% | 75.06 → **31.0** |
| L12 lantern5 | 13.10/s | **25.83 s** (4종 15.70 s) | 77.2% | 75.06 → **57.9** |

`[OBSERVED]` **현재 측정된 보스전 길이(2.60~8.30 s)는 전부 버스트 창 안쪽이므로
기름 게이트가 한 번도 작동하지 않는다.** `[INFERENCE]` S8이 보스전을 30 s 이상으로 늘리면
기름이 실질 제약이 되고, 그때 "쿨타임감소"는 기름 소모율을 함께 올리므로
**쿨감 단독으로는 DPS가 선형 증가하지 않는다** — S8 설계 시 반드시 반영해야 할 결합.

---

## 4. 시간 예산 — 현행 보스전 길이 vs 3-5분 목표

### 4.1 이론 TTK (보스 HP ÷ 총 DPS)

| 프로필 | cinder-span 846 | abyss-chancel 912 | echo-throne 978 |
|---|---|---|---|
| L1 신규 | 846/268.2 = **3.15 s** | 912/268.2 = **3.40 s** | 978/263.6 = **3.71 s** |
| L1 만스탯 | **2.10 s** | **2.26 s** | **2.45 s** |
| L12 만렉 | **1.54 s** | **1.66 s** | **1.79 s** |
| L12+추출3 | **1.27 s** | **1.37 s** | **1.48 s** |

### 4.2 실측 보스전 길이 (카이팅 파일럿, 하네스)

| 런 | 보스스폰(틱) | P2(틱) | 클리어(틱) | **보스전** | P1 | P2 | 총 스테이지 | **보스전 비중** |
|---|---|---|---|---|---|---|---|---|
| cinder-span L1 무스탯 | 2536 | 2913 | 3034 | **8.30 s** | 6.28 s | 2.02 s | 50.6 s | **16.4 %** |
| cinder-span 만스탯 | 2371 | 2612 | 2701 | **5.50 s** | 4.02 s | 1.48 s | 45.0 s | **12.2 %** |
| abyss-chancel 만스탯 | 3002 | 3341 | 3427 | **7.08 s** | 5.65 s | 1.43 s | 57.1 s | **12.4 %** |
| echo-throne 만스탯 | 3435 | 3520 | 3591 | **2.60 s** | 1.42 s | 1.18 s | 59.9 s | **4.3 %** |

실측이 이론 TTK의 2~4배인 이유는 파일럿이 카이팅에 시간을 쓰기 때문이다
(`[OBSERVED]` 보스 피격 틱 수: P1 4~8틱, P2 3~5틱뿐).
`[OBSERVED]` **페이즈 2 구간은 전 스테이지에서 1.18~2.02초** — 페이즈 전환 연출
(말풍선 홀드 2200~5200 ms, `SIM_SPEC_HACKSLASH.md:140`; 슬로모 0.5 s, `GameView.cs:329-332`)
보다 **짧거나 비슷하다.**

### 4.3 3-5분 목표와의 격차

목표 근거: `_workspace/current/design/combat-feel-boss-phase-spec.md:24, 66-68` (§B, #16),
전체 25-30분/6스테이지 체인은 `_workspace/current/design/integrated-campaign-level-spec.md:57-59`.

| 런 | 실측 스테이지 | 3분 대비 | 5분 대비 |
|---|---|---|---|
| cinder-span L1 | 50.6 s | **0.28×** | **0.17×** |
| cinder-span 만스탯 | 45.0 s | 0.25× | 0.15× |
| abyss-chancel 만스탯 | 57.1 s | 0.32× | 0.19× |
| echo-throne 만스탯 | 59.9 s | 0.33× | 0.20× |

`[OBSERVED]` **단일 스테이지는 목표의 1/3~1/6에 불과하다.** 다만 §3.3
(`integrated-campaign-level-spec.md:57-59`)이 "플레이타임 조정은 6스테이지 체인으로만,
개별 전투 수치·타이밍 변경 금지"로 못박았으므로, **3-5분은 체인 단위로 달성해야 하고
보스전 자체를 늘려서 메우는 것은 계약 위반이다.**

### 4.4 보스전을 늘린다면 필요한 HP 풀 (기름 게이트 반영)

| 목표 보스전 | L1 평균 DPS | 필요 HP | 현행 846 대비 | L12 평균 DPS | 필요 HP | 현행 대비 |
|---|---|---|---|---|---|---|
| 30 s | 242.2 | 7,265 | **8.6×** | 550.8 | 16,523 | **19.5×** |
| 60 s | 234.8 | 14,087 | **16.6×** | 543.4 | 32,603 | **38.5×** |
| 180 s | 229.9 | 41,376 | 48.9× | 538.5 | 96,926 | 114.6× |

`[INFERENCE]` 단순 HP 증량으로 보스전을 늘리는 것은 스펀지화 위험이 크다. S8이 시간을
벌어야 한다면 **HP가 아니라 "무적/전환 구간, 패턴 회피 요구, 접근 차단"** 같은
비-HP 시간 소비 장치가 필요하다. 이는 §3.5의 기름 게이트와도 맞물린다.

> **§7.3 참조 — 이 표를 목표 혼동 없이 읽을 것.** 위 배수(8.6×~19.5×)는 **"보스전을 30~60 s로
> 늘린다"** 는 강한 목표의 값이다. **"3페이즈가 각각 읽히게 한다"** 는 별개의 약한 목표이고,
> 그쪽은 §7.3이 보이듯 **HP 변경 없이 임계값 재배분(0.20→0.24)만으로 L1에서 달성된다.**
> 두 목표를 섞으면 불필요한 HP 스펀지화로 간다.

### 4.5 레벨 도달 현실 — "만렉"은 단일 런에서 불가능

`[OBSERVED]` 레벨 1→12 누적 XP = **2,510**(곡선 `HackTypes.cs:306-307`, `:352-364`).
일반 처치 10 XP 기준 251킬 상당. 실측 스테이지 총 처치는 35 / 47 / 53킬,
보스 150 XP를 포함해도 **한 스테이지 종료 시 레벨 6~7**(하네스 실측).
따라서 §3.4의 "L12 만렉" 열은 **6스테이지 체인 후반부에서만 성립**한다. `[INFERENCE]`
S8 페이즈 수치는 레벨 6~7 구간(스테이지 보스 조우 시점의 실제 레벨)을 기준선으로 잡아야 한다.

---

## 5. 결정론 제약 — 패턴 선택에 무엇이 가능한가

### 5.1 RNG 부재의 근거

`[OBSERVED]` `Assets/Scripts/Sim/` 전체에서 `Random` / `Guid` / `DateTime` /
`Environment.Tick` **일치 0건** (grep). 계약 문서도 명시:
`docs/SIM_SPEC_HACKSLASH.md:194` "전 모드 RNG 금지. 정예 판정·추출·동료 공격 주기 전부
모듈러/카운터 산술."

### 5.2 현행 결정론 달성 수단 전량

| 대상 | 결정론 식 | 종류 | 인용 |
|---|---|---|---|
| 스폰 위치 | `((wave×3 % 8) + id×3) % 8` | 모듈러(id) | `CinderSim.cs:458-462` |
| 적 비주얼 | `(wave + spawnIndexInWave) % 4` | 모듈러(카운터) | `CinderSim.cs:1994`, `:51` |
| 아레나 보스 비주얼 | `wave % 10 == 0 ? Monarch : Commander` | 모듈러(웨이브) | `CinderSim.cs:1993`, `:52` |
| 캠페인 보스 비주얼 | 스테이지 테이블 상수 | 테이블 | `CampaignTypes.cs:235,241,247` |
| **정예 판정** | `_spawnOrdinal % 7 == 0` (웨이브당 1회) | 모듈러(카운터) | `CinderSim.cs:1976`, `HackTypes.cs:313` |
| 파편 드롭 | `enemyId % 7 == 3` | 모듈러(id) | `CinderSim.cs:1815`, `CampaignTypes.cs:134-135` |
| 픽업 종류 | `enemyId % 3` | 모듈러(id) | `CinderSim.cs:1817` |
| 적 첫 공격 지연 | `(id % 3) × 0.18 s` | 모듈러(id) | `CinderSim.cs:2006`, `:33` |
| 적 속도 편차 | `(id % 3) × 2.5` | 모듈러(id) | `CinderSim.cs:1727`, `:28` |
| 벤트 위상 | `HazardConfig.Phase` 상수 | 테이블 | `CampaignTypes.cs:165-181` |
| **Ember Rest 보상** | **정수 해시**(xorshift-multiply) of (seed, roomIndex, slot) | **해시** | `CinderSim.cs:597-620` |
| **보스 페이즈** | HP 임계 + 1회 래치 bool | 상태 임계 | `CinderSim.cs:1213-1224` |

### 5.3 S8 패턴 선택에 걸리는 제약

`[OBSERVED]` 결정론 소스는 세 종류뿐이다: **① 모듈러(적 id / 스폰 카운터)**,
**② 상태 임계값(HP)**, **③ 정수 해시(순수 함수)**.

`[INFERENCE]` 따라서 S8 패턴 선택기는 다음 중 하나여야 한다:
- **카운터 모듈러** — `bossAttackOrdinal % patternCount`. 가장 단순하고 기존 문법과 동형
  (`_spawnOrdinal % 7`과 같은 계열). 단점: 패턴 순서가 완전히 예측 가능해 반복 체감.
- **틱/페이즈 결합 모듈러** — `(phaseIndex × k + ordinal) % patternCount`. 페이즈마다
  다른 순열을 만들되 여전히 순수.
- **정수 해시** — `PreparationHash(CinderSim.cs:608-620)` 문법을 그대로 재사용해
  `hash(bossId, phaseIndex, ordinal) % patternCount`. **무작위처럼 보이면서 완전 재현 가능**하고,
  저장소에 이미 승인된 선례가 있다는 점이 가장 강력한 근거다.

**금지되는 것**: 실시간 시각, 프레임 지터, 부동소수 누적 오차 의존, 플레이어 입력 히스토리
해싱(입력이 결정론 입력이므로 기술적으로는 가능하나 Digest 재현 계약을 취약하게 만듦 `[INFERENCE]`).

**추가 제약**: 0-alloc/tick 계약(`gjc-hackslash-lane-report.md:150-153`, [OBSERVED] 3케이스 0 bytes).
패턴 테이블은 `static readonly` 배열이어야 하고 틱 중 할당이 없어야 한다.

---

## 6. 스냅샷 표면 — 현행 리스 필드와 S8 추가 제안

### 6.1 `IHackSnapshot`이 보스에 대해 노출하는 것 전부

| 필드 | 타입 | 의미 | 인용 |
|---|---|---|---|
| `BossHp` | `float` | 생존 보스 HP, 없으면 0 | `HackTypes.cs:208-209` |
| `BossMaxHp` | `float` | 보스 최대 HP, 없으면 0 | `HackTypes.cs:210` |
| `BossPhase` | `int` | 0(미등장) / 1 / 2, 클리어 후 유지 | `HackTypes.cs:211-215` |
| `BossAlive` | `bool` | `ICampaignSnapshot` 소유 (`_livingBosses > 0`) | `CampaignTypes.cs:102`, `CinderSim.cs:403` |

보스 개체 자체는 `EnemyState`로도 나가지만(`ISimSnapshot.Enemies`), 그 구조체에는
**패턴/텔레그래프/페이즈 필드가 없다**: `Id, Visual, X, Y, Facing, Health, MaxHealth, Dead,
FadeTime, Action, ActionTime, IsBoss, Scale`가 전부 (`SimTypes.cs:51-65`).

이벤트 측: `BossSpawned = 1<<9` (`SimTypes.cs:90`), `BossPhase2 = 1<<20` (`SimTypes.cs:103`).

### 6.2 현행 View 소비처 (변경 시 영향 범위)

| 소비처 | 읽는 것 | 인용 |
|---|---|---|
| `GameView.SyncDungeon` 전달 | `BossHp/BossMaxHp/BossPhase` | `GameView.cs:486-491` |
| `HudView` 보스바 + 페이즈 핍 | 같은 3필드 → 채움/색/"PHASE I·II" | `HudView.cs:1510-1538` |
| `GameDirector` 카메라 거리 티어 | `BossHp > 0f` (빅웨이브 판정) | `GameDirector.cs:574-577` |
| `GameDirector` 말풍선 | `SimEvents.BossPhase2` → `StoryCatalog.BossPhase2` | `GameDirector.cs:541-545`, `StoryCatalog.cs:12` |
| `GameView` 슬로모 | `BossPhase2` 이벤트 → 0.5 s / 0.35배 | `GameView.cs:328-332` |
| `CameraRig` 셰이크 | `BossPhase2` → 0.3/0.09 | `CameraRig.cs:113-114` |
| `AudioDirector` | `BossPhase2` → 저음 큐 | `AudioDirector.cs:106` |
| `VfxDirector` | `BossPhase2` → 살아있는 보스 탐색 후 버스트 | `VfxDirector.cs:241-244` |

`[OBSERVED]` **`BossPhase`를 3·4로 확장해도 HUD는 깨지지 않는다** — `bossPhase >= 2`
분기라서 3 이상은 자동으로 "PHASE II" 취급된다 (`HudView.cs:1532-1535`).
`[INFERENCE]` 즉 N페이즈 확장은 HUD 라벨만 손보면 되고, 이벤트는 `BossPhase2`가
"페이즈가 올라갔다" 신호로 재사용 가능하나 **어느 페이즈로 갔는지는 이벤트만으로 알 수 없다**
(현재는 항상 2였으므로 문제가 없었다).

### 6.3 S8이 패턴을 View에 알리려면 — 제안만 (구현 금지)

**아키텍처 선례**: `IRunPreparationSnapshot` (`RunPreparationSnapshot.cs:22-36`) 이
> "Additive read seam ... **It deliberately does not amend the frozen IHackSnapshot contract.**"

라고 명시하며 **동결 인터페이스를 건드리지 않고 별도 인터페이스를 추가하는 선례**를 만들었다.
`CinderSim`은 이미 4개 인터페이스를 구현한다 (`CinderSim.cs:19`).

**제안: `IBossPatternSnapshot` 신설 (IHackSnapshot 무수정)**

| 제안 필드 | 타입 | 필요 이유 | 근거 |
|---|---|---|---|
| `BossPhaseIndex` | `int` | `BossPhase`가 `>=2`로 뭉개지는 문제 회피, N페이즈 정확 노출 | `HudView.cs:1532` |
| `BossPhaseCount` | `int` | HUD 페이즈 핍 개수 (스펙 "이름 + 페이즈 핍") | `SIM_SPEC_HACKSLASH.md:128` |
| `BossPatternId` | `int` | 현재 패턴 6종 중 무엇인가 (0=없음) | `combat-feel-boss-phase-spec.md:20` |
| `BossPatternStage` | `int`/enum | 텔레그래프/시전/판정/회복 4구간 중 현재 | `combat-feel-boss-phase-spec.md:90` |
| `BossPatternProgress` | `float` | 0..1 — 예고 링 채움에 필요 | `§K` 예고 0.25 s (`:38`) |
| `BossPatternX/Y` | `float` | 판정 형상의 원점(지면/폭격 패턴은 보스 위치와 다름) | `[INFERENCE]` |
| `BossPatternRadius` | `float` | 예고 링 크기 — View가 상수 테이블을 복제하지 않게 | `[INFERENCE]` |

**이벤트 측 제안**: `SimEvents`는 `1<<21`까지 사용 중 (`SimTypes.cs:104`).
`1<<22` 이상이 비어 있으므로 `BossPatternStarted` / `BossPhaseChanged` 추가 여지가 있다.
`[INFERENCE]` 단 `SimEvents`는 `SimTypes.cs`(FROZEN) 소속이므로
§12형 "동결 해제 목록" 개정이 선행되어야 한다 (`SIM_SPEC_HACKSLASH.md:179-190`이 그 선례).

**주의**: `EnemyState`에 필드를 추가하면 `Enemies` 리스트 전체가 커지고
0-alloc 계약과 아레나 Digest 회귀에 영향을 준다 `[INFERENCE]`.
보스는 스테이지당 1기이므로 **스냅샷 루트 단일 필드 방식이 비용이 낮다**.

---

## 7. 사용자 문장 "수치합이 점점 작아지는" 에 대한 수치적 사실

이 조사는 해석을 확정하지 않는다(오케스트레이터 몫). 다만 **판정에 필요한 사실**은 다음과 같다.

| 사용자가 언급한 축 | 보스에 현재 존재하는가 | P1 → P2 방향 | 인용 |
|---|---|---|---|
| **이동속도** | **존재** (보스 전용 ×0.7) | **증가** ×1.25 (71.5 → 89.4) | `CinderSim.cs:1728-1734` |
| **공격속도** | **없음** — 보스 분기 자체가 없음 | — | `CinderSim.cs:1601-1602` |
| **범위** | **없음** — 76/90 고정, 보스 분기 없음 | — | `CinderSim.cs:1598`, `:1690` |
| **쿨타임감소** | **없음** — 보스에 스킬 없음 | — | 스킬 캐스팅은 플레이어 전용 `CinderSim.cs:676-763` |
| (참고) 접촉 피해 | 존재 (×2) | **증가** ×1.25 (22 → 27.5) | `CinderSim.cs:1695-1708` |

`[OBSERVED]` **현행 페이즈 2는 두 축 모두 "증가"한다. 어떤 정의로도 현행 수치의 합은 작아지지 않는다.**
따라서 사용자 문장은 현행 동작 서술이 아니라 **신규 요구**이며, 3개 축(공속·범위·쿨감)은
**측정 대상이 아니라 신설 대상**이다.

`[OBSERVED]` 저장소 내 유일한 기존 해석 시도:
`_workspace/current/design/combat-feel-boss-phase-spec.md:89`
> "각 페이즈의 (이속+공속+범위+쿨감) 합이 **증가**하되 개별 항목은 트레이드오프(예: P2 이속↑ 범위↓).
> 사용자 문구 '합이 작아지는'은 쿨타임 **감소**를 의미하는 것으로 해석 — **승인 시 확인 필요**."

즉 4턴 전에 이미 "확인 필요"로 표시된 채 미해결이다. `[INFERENCE]` 수치적으로 성립 가능한
해석은 최소 3가지이며(쿨타임 값 자체가 작아진다 / 4축 정규화 합이 페이즈마다 감소해
보스가 약해지는 대신 패턴 위협이 커진다 / "합"이 플레이어 여유 시간을 뜻한다),
**어느 쪽이든 공속·범위·쿨감 3축을 심에 신설해야 한다는 결론은 동일하다.**

### 7.1 진행 중인 해석 (작업 트리, 미커밋)

`[OBSERVED]` §0.1의 병행 편집이 이 질문에 **답을 확정해 코드에 넣었다.**
`HackTypes.cs` 미커밋 주석 원문:

> "The stat vector is stored as **TIME values in seconds**, so 'the numeric sum gets smaller'
> IS the strengthening. P1 0.80+1.37+5.00 = 7.17 / P2 0.80+1.16+4.00 = 5.96 /
> P3 0.80+0.99+3.25 = 5.04. Monotonically decreasing."

즉 **합산 대상을 배율이 아니라 "초 단위 시간값"으로 바꿔** 사용자 문장을 문자 그대로 성립시켰다
(텔레그래프 + 공격 간격 + 스킬 쿨타임). 배율축(`BossSpeedMul`, `BossRangeMul`)은
**증가**시키되 합산에서 제외한다.

`[OBSERVED]` 이 해석은 본 조사의 §2.2 발견과 정합적이다 — 공속·범위·쿨감이 심에 **없었으므로**
신설이 불가피했고, 신설하는 김에 단위를 "시간"으로 잡으면 사용자 문장과 강화 방향이 일치한다.
`[OBSERVED]` 또한 새 `BossAttackInterval[0] = 1.37`은 본 조사 §2.3이 측정한
cinder-span 보스의 현행 공격 쿨 **1.370 s와 정확히 같은 값**이다 — 즉 P1은 현행 동작을
보존하도록 잡혀 있다(회귀 안전).

`[INFERENCE]` 남는 검증 과제 두 가지:
1. `BossSpeedMul={1.00,1.25,1.45}`의 P3는 보스 기저 ×0.7과 곱해 **실효 ×1.015**가 되어
   일반 적 수준이다(주석도 동일하게 서술). 본 조사 §2.3 기준 echo-throne P3 속도는
   약 `108.60 × 1.015 ≈ 110`으로, 여전히 플레이어 218/261.6에 **한참 못 미친다** —
   "따라잡지 못하는 보스" 구조는 해소되지 않는다.
2. 시간값 축소는 **DPS를 올리므로** §3.5의 기름 게이트가 아니라 **플레이어 생존**에 압력을 준다.
   현행 P2 대인 DPS 20~23(§2.3)에서 공격 간격 1.37→0.99는 대인 DPS를 약 1.38배로 올린다.

### 7.2 `BossAttackInterval` 절대값 저장의 웨이브 드리프트 (실측 확인된 결함)

`[OBSERVED]` §7.1에서 확인했듯 `BossAttackInterval[0] = 1.37`은 **웨이브 6의 값**이다
(`1.22 + min(0.38, 6×0.025) = 1.370`, `CinderSim.cs:1601-1602` + `:24-25`).
그런데 §0.1의 같은 변경이 보스 웨이브를 **10 / 12 / 14**로 옮겼다. 웨이브별 기저 공격 쿨:

| 보스 웨이브 | 스테이지 | 기저 공격 쿨 | 저장된 P1 값 | 괴리 |
|---|---|---|---|---|
| 6 | (구 cinder-span) | 1.370 | 1.37 | 없음 |
| **10** | cinder-span (신) | **1.470** | 1.37 | **−0.100 (보스가 원래보다 빨라짐)** |
| **12** | abyss-chancel (신) | **1.520** | 1.37 | **−0.150** |
| **14** | echo-throne (신) | **1.570** | 1.37 | **−0.200** |

`[OBSERVED]` 즉 절대 초값으로 저장하면 **P1이 "현행 보존"이 아니게 되고**, 스테이지가 뒤로 갈수록
보스가 기저 대비 더 빨라진다(스테이지 1 전용 상수가 2·3에 잘못 적용됨).
`[INFERENCE]` **배율 저장이 안전하다**: 저장값 `{1.37, 1.16, 0.99} ÷ 1.370 = {1.0000, 0.8467, 0.7226}`
(제안 반올림 `{1.00, 0.85, 0.72}`), 실행 시 `기저(웨이브) × 배율`로 유도. 그러면 P1은 어떤 웨이브에서도
정의상 현행과 동일해지고 회귀 증명이 자명해진다.

`[OBSERVED]` **두 번째 천장**: 공격 쿨 가산분의 캡 `0.38`은 **웨이브 16**에서 걸린다
(`0.025 × w ≥ 0.38 → w ≥ 15.2`, `CinderSim.cs:25`). HP 캡(웨이브 14, §0.1)에 이어
**두 천장이 모두 마지막 스테이지 부근에 있다** — 웨이브 증량 레버는 여기서 두 번 소진된다.

### 7.3 교차 확인: 페이즈가 "읽히는가" (BossMetricResearch 레인 결과, 실측 TTK 기준으로 정정)

`[교차-OBSERVED]` 병행 레인이 **PET(Phase Expression Time) = TTK × HP지분** 을 정의하고,
"페이즈가 읽히려면 PET ≥ 텔레그래프→공격 1사이클"을 요구했다. 보스 웨이브 10에서
사이클은 P1 2.27 / P2 2.05 / P3 1.86 s.

**정정 사유**: 최초 교환에서는 §0.1의 *이론* TTK(L1 4.14 / 만렉 2.02 s)를 넣어
"세 페이즈 전부 1사이클 미달, 필요 HP 2.24×"라는 결론이 나왔다. 그러나 §4.2에서 보였듯
**실주행 보스전은 이론 TTK의 2~4배**다(파일럿이 카이팅에 시간을 쓰고 보스 피격은 P1 4~8틱뿐).
본 조사의 **실측값 L1 8.30 s / 만렉 5.50 s**를 넣어 재계산한 것이 아래이며, 결론이 뒤집힌다.

| 분할 | TTK | P1 | P2 | P3 | 전 페이즈 ≥1사이클 |
|---|---|---|---|---|---|
| 50/30/20 (현 제안) | L1 8.30 | 1.83 | 1.21 | **0.89** | 아니오 — **P3만 미달** |
| 50/30/20 | 만렉 5.50 | 1.21 | 0.80 | 0.59 | 아니오 |
| **50/26/24 (재배분)** | **L1 8.30** | **1.83** | **1.05** | **1.07** | **예 — HP 변경 0** |
| 50/26/24 | 만렉 5.50 | 1.21 | 0.70 | 0.71 | 아니오 |

`[OBSERVED]` 위 표는 본 레인이 독립 재계산해 병행 레인 값과 일치 확인했다.

**핵심 정정**: 병목은 "세 페이즈 전부"가 아니라 **P3 하나**였고, 이는 이론 TTK를 쓴 데서 온 착시였다.
필요 HP 배수는 **2.24× → 1.12×** 로 떨어진다.

`[OBSERVED]` **더 나은 결론 — 임계값 재배분은 공짜다.** 50% 경계는 말풍선 계약
(`SIM_SPEC_HACKSLASH.md:138`)에 묶여 있으나 P2/P3 경계는 자유롭다. L1 실측 8.30 s 기준
P3 지분의 가용 대역은 **22.41 % ~ 25.30 %** 이고(P3가 1사이클을 채우려면 하한,
P2가 1사이클을 지키려면 상한), **24 %(=50/26/24)가 이 대역 한가운데** 에 든다.
즉 `BossPhase3HealthFraction`을 `0.20 → 0.24`로 바꾸는 것만으로
**HP를 전혀 건드리지 않고 L1에서 3페이즈가 모두 표현된다.**

`[INFERENCE]` 만렉 구간은 여전히 부족해 `BossHealthMul` **약 1.43×** 가 필요하다
(§4.4의 8.6×가 아니다 — 그 수치는 30 s 보스전이라는 훨씬 강한 목표 기준이었다).
따라서 권고 순서는 **① 임계값 재배분(무비용·HP가정 무관) → ② 실측 → ③ 그래도 만렉이
미달일 때만 HP 결정**이다. §4.4의 "HP 레버만 남았다"는 판단은 *3페이즈 표현* 목표에 한해
이 재배분으로 대체 가능하며, *3-5분 플레이타임* 목표에는 여전히 유효하다(두 목표는 별개다).

---

## 부록 A. 조사 중 확인된 부수 사실

- `[OBSERVED]` 아레나 보스(웨이브 5의 배수)는 `UpdateBossPhase`를 타지 않아 **페이즈가 영원히 0**이다 (`CinderSim.cs:652`).
- `[OBSERVED]` `_bossPhase2Done`은 런 단위 리셋(`CinderSim.cs:2160`)이라 보스 2기 이상이 등장하면 두 번째 보스는 페이즈 전환을 못 한다. 현행 캠페인은 스테이지당 1기라 노출되지 않는다.
- `[OBSERVED]` Monarch 호위 3기는 `_pendingSpawns`에 더해질 뿐 즉시 소환이 아니다 — 스폰 간격 `max(0.28, 0.62 - wave×0.018)`을 따른다 (`CinderSim.cs:1938`).
- `[OBSERVED]` 스킬을 쓰지 않는 L1 파일럿은 cinder-span을 클리어하지 못하고 t=4138에 `overrun`으로 사망(웨이브 5 도달). 즉 **현행 밸런스에서 스킬은 필수**다.
- `[OBSERVED]` 에이기스(Frost)는 Commander(Veil) 상대로 상성 1.2가 계산되지만 피해가 0이라 무의미하다.

## 부록 B. 하네스 원본 출력

```
=== A. stage runs (deterministic kiting pilot) ===
stage,label,bossSpawnTick,phase2Tick,clearTick,deathTick,bossMaxHp,bossHpAtP2,lvAtBoss,lvEnd,wave,kills,hpAtBoss,hpEnd,totalTicks,reason
cinder-span,L1-nostat-skills,2536,2913,3034,-1,846.00,364.32,5,6,6,35,124.00,130.00,3034,stage-clear
cinder-span,L1-nostat-noskill,-1,-1,-1,4138,0.00,0.00,0,4,5,23,0.00,0.00,4138,overrun
cinder-span,maxstat-skills,2371,2612,2701,-1,846.00,422.04,5,6,6,35,244.00,250.00,2701,stage-clear
abyss-chancel,maxstat-skills,3002,3341,3427,-1,912.00,415.50,6,7,7,47,250.00,256.00,3427,stage-clear
echo-throne,maxstat-skills,3435,3520,3591,-1,978.00,426.59,6,7,8,53,250.00,256.00,3591,stage-clear

=== boss dps probe ===
stage,label,spawnT,p2T,clearT,fightT,p1T,p2DurT,fightS,p1S,p2S,bossMaxHp,dmgAccounted,grossDps,dmgP1,dmgP2,dpsP1,dpsP2,hitTicksP1,hitTicksP2
cinder-span,L1-nostat-skills,2536,2913,3034,498,377,121,8.30,6.28,2.02,846.0,784.4,101.9,481.7,302.8,76.7,150.1,8,4
cinder-span,maxstat-skills,2371,2612,2701,330,241,89,5.50,4.02,1.48,846.0,729.4,153.8,424.0,305.4,105.6,205.9,4,5
abyss-chancel,maxstat-skills,3002,3341,3427,425,339,86,7.08,5.65,1.43,912.0,776.1,128.8,496.5,279.6,87.9,195.1,4,4
echo-throne,maxstat-skills,3435,3520,3591,156,85,71,2.60,1.42,1.18,978.0,963.1,376.2,551.4,411.7,389.2,347.9,5,3

=== combo cadence ===
presses=125 elapsedTicks=2578 ticksPerPress=20.624 swingsPerSec=2.909
theoretical: cycle ticks = 62, ticksPerPress=20.667, swingsPerSec=2.903

=== boss id / stats ===
stage,bossId,idMod3,wave,rawSpeed,p1Speed,p2Speed,contactP1,contactP2,playerMaxHp,hitsToKillP1,hitsToKillP2,atkCd,dpsToPlayerP1,dpsToPlayerP2
cinder-span,32,2,6,102.20,71.54,89.42,22.0,27.5,250.0,12,10,1.370,16.06,20.07
abyss-chancel,42,0,7,100.40,70.28,87.85,24.0,30.0,250.0,11,9,1.395,17.20,21.51
echo-throne,53,2,8,108.60,76.02,95.02,26.0,32.5,250.0,10,8,1.420,18.31,22.89
```

## 부록 C. 조사 범위 밖 / 미검증

- `[미검증]` Unity EditMode 테스트는 실행하지 않았다(다른 세션 점유). 하네스는 dotnet 8 링크 컴파일이며 asmdef 경로는 확인하지 않았다.
- `[미검증]` 실제 사람 플레이의 보스전 길이. 위 실측은 전부 결정론 카이팅 봇 기준이며, 봇은 회피 반경 95 px에 민감하다(`gjc-hackslash-lane-report.md:208-210`).
- `[범위 밖]` 페이즈 수치의 최종 결정(합 증가/감소 해석) — 오케스트레이터 판단 사항. §7.1이 진행 중인 코드 측 답을 기록했으나, 이 레인이 승인한 것은 아니다.
- `[미검증]` §0.1의 미커밋 변경(Waves 9/11/13, 3페이즈 상수)은 **실주행 측정을 하지 않았다.** 해당 표의 신규 HP·TTK는 인용 상수로 계산한 값이며, 하네스 재측정이 필요하다(스테이지가 길어져 파일럿 생존·레벨 도달이 달라질 수 있다 — §4.5 참조).
- `[미검증]` 본 조사는 저장소 파일에 쓰기를 하지 않았으므로 동결 계약 위반 여부 판정은 이 레인의 권한 밖이다. 사실만 §0.1에 기록했다.
