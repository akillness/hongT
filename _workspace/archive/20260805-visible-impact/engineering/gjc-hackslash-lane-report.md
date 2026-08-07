# LANE REPORT: Hack & Slash Sim Extension (owner: gjc)

`docs/SIM_SPEC_HACKSLASH.md` §0–§7, §12–§13 구현 완료. 3슬라이스 전부 초록.

## 1. 결과 요약

[OBSERVED] 최종 게이트:

```
Passed!  - Failed: 0, Passed: 61, Skipped: 0, Total: 61, Duration: 86 ms
```

| 구분 | 개수 | 파일 |
|---|---|---|
| 아레나 회귀 (기존, 무수정) | 20 | `CinderSimTests.cs` |
| 캠페인 회귀 (기존, 무수정) | 10 | `CampaignSimTests.cs` |
| 신규 핵앤슬래시 | 31 | `HackSimTests.cs` |

기존 30테스트는 **파일을 열지도 않았고** 전부 통과한다(회귀 게이트 충족).

## 2. 수정 파일 (허용 목록 내)

| 파일 | 성격 |
|---|---|
| `Assets/Scripts/Sim/SimTypes.cs` | §12 증분만: `SimInput` 3필드(`DashQueued`/`BoltQueued`/`PulseQueued`) + `SimEvents` 8플래그(`DashUsed`=1<<14 … `ComboFinisher`=1<<21). 그 외 한 줄도 안 건드림 |
| `Assets/Scripts/Sim/HackTypes.cs` | 신규. `GameMode`, `Element`, `MetaStats`, `EquipTiers`, `HackConfig`, `IHackSnapshot`, `HackSpec` |
| `Assets/Scripts/Sim/CinderSim.cs` | `CinderSim(in HackConfig)` 오버로드 + 던전 전투킷. 기본/캠페인 생성자 경로 불변 |
| `Assets/Tests/EditMode/HackSimTests.cs` | 신규 (+ `.meta`) |
| `Assets/Scripts/Sim/HackTypes.cs.meta`, `HackSimTests.cs.meta` | Unity 임포트용 신규 meta (기존 meta 포맷 그대로) |

## 3. 슬라이스 진행 (각 지점에서 dotnet test 초록 확인)

| 슬라이스 | 범위 | 테스트 결과 |
|---|---|---|
| 기준선 | 착수 전 | [OBSERVED] `Passed: 30, Total: 30` |
| S1 | HackTypes + 모드 스캐폴딩 + 프롤로그 | [OBSERVED] `Passed: 39, Total: 39` (30 회귀 + 9 신규) |
| S2 | 콤보/대시/스킬4/원소/XP | [OBSERVED] `Passed: 51, Total: 51` (30 + 21) |
| S3 | 정예/추출/동료/보스페이즈2 | [OBSERVED] `Passed: 61, Total: 61` (30 + 31) |

S2 착수 시점에 S1 테스트도 함께 돌렸고, S3에서 정예 스폰이 들어가며 S2의
"클러스터 웨이브2" 픽스처가 4기 중 1기가 정예(HP×3)로 바뀌었다 —
S2 테스트를 `MaxHealth` 기준으로 일반화해 재초록화한 뒤 S3를 진행했다.
어느 슬라이스도 반쯤 얹은 상태로 넘어가지 않았다.

## 4. 구현 상세 (스펙 §번호)

### §0/§12 모드 스캐폴딩
- `GameMode { Arena, Prologue, Dungeon }`. `CinderSim(in HackConfig)`가
  Arena면 기본 생성자와 **완전 동일 경로**(해저드 없음, `_campaign=false`),
  Dungeon이면 `HackConfig.ToCampaignConfig()`로 기존 캠페인 머신(웨이브 수,
  보스 비주얼, 해저드, 장비 드롭)을 그대로 재사용하고 그 위에 전투킷을 얹는다.
- 던전 전용 분기는 전부 `_dungeon` / `_prologue` 게이트 뒤에 있다.
  아레나/캠페인 경로에서는 `_dashTime`, `_shield`, `_bossPhase2Done`,
  `_comboSwing` 등이 항상 0/false라 기존 코드 경로가 문자 그대로 유지된다.

### §1 프롤로그
- 웨이브 4/6/8, 보스·기믹 없음, 아레나 수치 계약(HP 100, 공격 58, 적 HP 58 곡선).
- `CastSkills`가 프롤로그에서 즉시 return → Nova/Ward/Dash/Bolt/Pulse 전부 무시.
  기름도 안 깎인다.
- 3웨이브 전멸 시 `ClearRun("prologue-clear")` → `StageCleared` + `SimMode.GameOver`.
- 메타스탯/장비는 §5대로 미적용(프롤로그 config에 10/10/10, T5/T5/T5를 꽂아도
  HP 100, 랭크 0).

### §2.1 콤보
- 3타 체인 58/58/87 — 심에서는 `_playerDamage × {1, 1, 1.5}`로 표현한다
  (87/58 = 1.5). 레벨/추출 버프가 곱해져도 스펙의 1:1:1.5 비율이 유지된다. [INFERENCE]
- 스윙 0.30/0.30/0.42 s, 활성창 [0.10,0.22)/[0.10,0.22)/[0.14,0.30),
  링크 윈도 0.9 s, 3타 넉백 120 px / 0.18 s + `ComboFinisher`.
- 사거리 160 / 전방 `dx·facing ≥ −18` / 1스윙 1피격(`lastHitAttack`)은
  아레나 계약 그대로 상속.
- 던전 적 HP `86 + min(140, (wave−1)×11)`.

### §2.2 대시
- 190 px / 0.22 s. 0.22 s는 고정스텝의 정수배가 아니라 **마지막 스텝을
  잔여시간으로 클립**해서 이동거리가 정확히 190.000 px가 된다.
  [OBSERVED] 14틱, `dx = 190.000`.
- 전구간 무적: `DamagePlayer`가 `_dashTime > 0`이면 grace조차 소모하지 않고 반환
  (접촉·해저드 공통). 쿨 1.6 s, 기름 8, 이벤트 `DashUsed`, 액션 `Avoid`.
- 대시는 스윙을 캔슬하되 링크 윈도를 열어둔다(콤보 캔슬 계약).

### §2.3 스킬 4종 + §2.4 원소
표 수치 그대로. `SkillCooldowns[4]` = {bolt, pulse, nova, aegis}.
던전에서는 `NovaCooldown`/`WardCooldown` 스냅샷이 각각 ash-nova(8 s)/
void-aegis(12 s)를 보고한다(뷰 HUD 슬롯 보존).

원소 사이클은 `ember(1) > frost(2) > veil(3) > void(4) > ember`를
`(a % 4) + 1 == b` 한 줄로 표현. 유리 ×1.2 / 불리 ×0.85 / 그 외 ×1.0.
**기본공격·콤보는 무원소** — 스킬만 상성이 붙는다.

[OBSERVED] 던전 웨이브 2(적 HP 97) 실측:

| 스킬 | Possessed(void) | EmberCohort(ember) | Scout(frost) | Shade(veil) |
|---|---|---|---|---|
| grave-pulse 틱(ember, 26) | 22.10 (×0.85) | 26.00 | 31.20 (×1.2) | 26.00 |
| ash-nova(ember, 110) | 93.50 (×0.85) | 110.00 | 132.00 (×1.2) | 110.00 |
| rift-bolt 스플래시(void, 87) | 87.00 | 104.40 (×1.2) | 87.00 | 73.95 (×0.85) |

- `grave-pulse`는 시전 좌표에 고정되는 심 내부 필드(플레이어 추종 아님).
  3 s / 0.5 s 틱 → [OBSERVED] 캐스트 틱 이후 29·59·89·119·149·179틱에 정확히 6회.
- `void-aegis`: 실드 40 흡수 + 8 s 만료 + 시전 무적 0.2 s.
  흡수 중에는 `PlayerDamaged`가 뜨지 않는다(전량 흡수 = 피격 아님).
  [OBSERVED] 실드 40 → 8 데미지 × 5회 흡수 후 0, 그 사이 HP 불변.

### §2.5 XP/레벨
- 곡선 [30,55,85,120,160,205,255,310], 이후 +60/lv, 캡 12
  ([OBSERVED] 9→370, 10→430, 11→490, 12→0).
- 레벨업 즉시 적용: 피해 ×(1+0.04·(lv−1)), 최대 HP +6/lv(+6 회복),
  기름재생 +0.3/s per lv. `LevelUp` 이벤트.
- 레벨 1·버프 0에서 모든 배율이 정확히 1이라 아레나/캠페인 수치는 비트 단위로 동일하다.

### §3 정예/추출
- `spawnOrdinal % 7 == 0` (런 전역 던전 스폰 카운터), 웨이브당 최대 1.
  [OBSERVED] 정예 id = 7, 14, 21 / `ElitesAlive` 최대 1.
- HP ×3, 접촉 ×1.5, 스케일 ×1.35 ([OBSERVED] 웨이브2 접촉 8 → 12.0).
- 정예 사망 → `EliteDown` + 시체 마커 10 s.
- 반경 90 px 아이소 + **정지 상태** 2.0 s 연속 → `ExtractionComplete`.
  이동 1틱 또는 피격 1회로 progress가 0으로 리셋된다([OBSERVED] 둘 다).
- 보상 분기: 신규 비주얼이면 로스터 등록 + 이번 런 피해 +8%
  ([OBSERVED] 58 → 58×1.08), 중복이면 유물 +30.

### §4 동료
- `HackConfig.CompanionId != null`이면 활성. 플레이어 facing 반대쪽 80 px 오프셋 추종.
- 1.1 s마다 자기 위치 기준 200 px 내 최근접 적에게 플레이어 피해 ×0.6
  ([OBSERVED] 34.80 = 58×0.6, 간격 66틱 = 1.1 s).
- 적 배열에 들어가지 않으므로 구조적으로 피격 불가(untargetable).
- 스냅샷 `CompanionX/Y/Attacking`.

### §5/§6 스탯·장비
- 공격 `58 × (1+0.03a) × (1+0.06w)`, HP `100 + 8v + 8c`,
  이속 `218 × (1+0.02s)`, 재생 `7 × (1+0.08l)`. 스탯 캡 10, 티어 캡 5.
- 던전 런 시작 시 1회 적용. 프롤로그/아레나 미적용.

### §7 보스 페이즈 2
- HP ≤ 50%에서 **1회만** `BossPhase2`. 이속 ×1.25(보스 0.7 배율 위에),
  접촉 ×1.25. 스냅샷 `BossHp/BossMaxHp/BossPhase`.
- Monarch(echo-throne)면 전환 시 호위 3기를 라이브 스폰 큐에 추가
  ([OBSERVED] `PendingSpawns` +3). Commander는 +0.

### §13 결정론
RNG 없음. 정예 판정·추출·동료 주기·보스 페이즈 전부 모듈러/카운터 산술.
같은 `HackConfig` + 같은 입력 → 같은 `RunDigest` ([OBSERVED] 2회 실행 전 필드 일치).

## 5. 검증

1. **컴파일** — [OBSERVED] `csc -nologo -t:library -langversion:9.0 Assets/Scripts/Sim/*.cs`
   → 에러 0, **경고 0**.
2. **테스트** — `/tmp/cinder-hack-tests` 임시 net8.0 프로젝트(NUnit 3.14 +
   NUnit3TestAdapter 4.5)에 `Assets/Scripts/Sim/*.cs` + `Assets/Tests/EditMode/*.cs`
   를 전부 링크해 `dotnet test`. [OBSERVED] 61/61 통과.
3. **0 alloc/tick** — `GC.GetAllocatedBytesForCurrentThread()` 기준 [OBSERVED]:
   - 아레나 유휴 600틱: **0 bytes**
   - 프롤로그 3000틱(3웨이브 클리어, 18킬): **0 bytes**
   - 던전 3000틱(echo-throne, 만렙 스탯 + 동료 + 전스킬 난타, 웨이브 8/52킬): **0 bytes**
4. **플레이어블 확인** — 스냅샷만 보는 결정론적 카이팅 파일럿(테스트 내 `Pilot`)으로
   [OBSERVED] 3스테이지 전부 클리어: cinder-span t=2700, abyss-chancel t=3426,
   echo-throne t=3590 (틱). 프롤로그는 t=1289에 무피해 클리어.

## 6. 스펙 대비 판단이 필요했던 지점

1. **`IHackSnapshot.Mode` → `HackMode`로 개명.**
   §12는 스냅샷 멤버 이름을 `Mode`로 적었지만 `ISimSnapshot.Mode`가 이미
   `SimMode` 타입으로 그 이름을 점유한다. 인터페이스 `new` 은닉 + 명시적 구현으로
   글자 그대로 맞출 수는 있으나, 같은 이름의 서로 다른 두 상태는 유지보수 함정이라
   `GameMode HackMode { get; }`로 노출하고 XML 주석에 §12 대응관계를 명기했다.
   기존 `sim.Mode`(SimMode)는 그대로다 — 회귀 없음.

2. **`HackConfig.RosterMask` 추가 (§12 목록 외 1필드).**
   §3의 보상 분기("신규면 로스터 등록, 중복이면 유물 +30")는 심이
   "이 비주얼을 이미 보유했는가"를 알아야 판정할 수 있는데 §12 필드 목록
   (mode/stage/metaStats/equipTiers/companionId/hazards)에는 그 입력이 없다.
   문자열 리스트 대신 `EnemyVisual` 비트마스크(`int`)로 받아 심의 0-alloc 계약을
   지켰고, 스냅샷에도 `RosterMask`를 되돌려 뷰가 영속화할 수 있게 했다.
   **다른 대안(런 내 최초 추출 여부로 판정)은 로비 로스터와 어긋나므로 배제.**

3. **콤보 피해를 절대값이 아닌 비율로.**
   표의 58/58/87은 기본 공격력 58 기준이다. 메타스탯·장비·레벨·추출 버프가
   공격력을 올리는 v0.2.0에서 3타만 절대 87로 두면 성장이 3타를 역전한다.
   `_playerDamage × {1, 1, 1.5}`로 구현했다 (a=w=0, lv=1에서 정확히 58/58/87).

4. **대시 y축.** "190 px"는 축을 명시하지 않는다. 심의 모든 플레이어 이동이
   y축에 0.68을 곱하는 계약이므로 대시도 동일하게 적용했다
   (정면 대시 = 정확히 190 px). [INFERENCE]

5. **넉백은 이동을 대체하지 않고 얹는다.** 스펙이 스턴을 명시하지 않아
   적은 밀리는 동안에도 추격 로직을 계속 돌린다. 결과적으로 관측되는 순 이격은
   120 px에서 적의 자체 이동분만큼 작다(테스트는 "한 틱에 9 px 이상 밀림"으로
   임펄스를 식별한다 — 추격 적의 1틱 최대 이동은 128/60 = 2.13 px).

6. **`BossPhase`는 클리어 후에도 마지막 페이즈를 유지.** 보스 처치 →
   `ClearStage()` → `GameOver`라 이후 틱이 돌지 않는다. 결과 오버레이가
   "페이즈 2에서 잡았다"를 읽을 수 있도록 0으로 되돌리지 않고 인터페이스 주석에 명기했다.

7. **실드 전량 흡수는 `PlayerDamaged`를 발생시키지 않는다.**
   §3의 "피격 시 채널 리셋"과 직결되는 판단이다. 실드가 다 먹은 피해로
   추출 채널이 끊기면 aegis의 의미가 없다.

## 7. 미해결/후속

- **Unity 에디터 게이트 미수행.** 이 레인은 Unity를 열지 않았다.
  `Unity -batchmode -runTests -testPlatform EditMode`는 Unity 소유 레인에서
  한 번 돌려야 asmdef 참조 경로까지 확인된다. 테스트/심 모두 `UnityEngine`
  심볼을 전혀 쓰지 않고 `MathF`는 .NET Standard 2.1(Unity 2021+)에 있으며,
  Mono csc(langversion 9.0)와 .NET 8 양쪽에서 컴파일 확인했다.
  신규 `.cs` 2개에는 기존 포맷과 동일한 `.meta`(fileFormatVersion 2 + guid)를 넣었다.
- **뷰/입력 미구현은 의도.** §2.3의 키→불리언 리맵(던전 Q=Bolt/E=Pulse/R=Nova/
  F=Ward), 말풍선(§8), 로비(§9), 연출(§10), 지속성(§11), 릴리즈(§14)는
  이 레인 범위 밖이다. 심이 뷰에 넘기는 계약은 `IHackSnapshot` + 8개 신규 이벤트가 전부다.
- **테스트 파일럿의 민감도.** `Pilot`의 회피 반경 95 px는 "적 공격 사거리 76 /
  접촉 90" 사이에서 고른 값이고, 여기서 5 px만 키워도 파일럿이 공격을 못 하고
  무한 카이팅에 빠진다(프롤로그 실측). 전투 수치를 손대면 이 상수를 같이 봐야 한다.
- git/포매터 명령은 하나도 실행하지 않았다.
