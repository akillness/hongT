# LANE REPORT: Deterministic Simulation (owner: gjc)

## 1. 산출물

| 파일 | 상태 | 내용 |
|---|---|---|
| `Assets/Scripts/Sim/CinderSim.cs` | 신규 | `public sealed class CinderSim : ICinderSim` (namespace `CinderCourt.Sim`), 800 lines |
| `Assets/Tests/EditMode/CinderSimTests.cs` | 신규 | NUnit `CinderCourt.Tests.CinderSimTests`, 919 lines / 20 tests |

그 외 파일은 생성/수정하지 않았다. `Assets/Scripts/Sim/SimTypes.cs`(FROZEN)는 읽기만 했다.
`git status --short` [OBSERVED]: 위 두 파일만 이 레인이 추가한 `??` 항목이다(나머지 변경은 다른 레인 소유).
git 조작·포매터·린터 실행 없음.

## 2. 참조한 진실 소스

- `docs/SIM_SPEC.md` — 모든 수치.
- `Assets/Scripts/Sim/SimTypes.cs` — 인터페이스/`SimConfig`.
- `~/orca/Abyssal-Surge/sprite-2-5d.js` (읽기 전용 원작) — `fixedUpdate`/`updatePlayer`/
  `updateEnemy`/`updateEnemies`/`updateWave`/`updateSkills`/`updatePickups`/`restartGame`/
  `spawnEnemy`/`damageEnemy`/`damagePlayer`/`clampToArena`/`setClip`/`advanceClip` 순서 및 분기 대조.
- `~/orca/Abyssal-Surge/assets/images/sprite-2-5d/{warden,ember-cohort}/manifest.json` [OBSERVED]:
  `attack 5프레임 @12fps (loop=false)`, `idle 4@6`, `walk 6@10`. 공격 활성창 `frame 2..3`
  = `ActionTime ∈ [2/12, 4/12)`이 여기서 확정된다 (SimConfig.AttackActiveFrom/To와 일치).

## 3. 스펙 해석이 갈렸던 지점과 선택

1. **보스 스폰과 상한** — SIM_SPEC §Bosses "보스 1기 추가 스폰(상한 무시하지 않음)".
   선택: `SpawnCountForWave(n) = min(20, 3 + floor(n*1.2) + (보스웨이브 ? 1 : 0))`.
   보스 슬롯을 큐에 더한 뒤 20 상한을 적용하므로 웨이브 15 이상에서도 총 스폰은 20을 넘지 않고,
   동시 상한(`enemies.length < 20`) 게이트도 원본 그대로 유지된다. 보스는 웨이브의 **첫 스폰**
   ("웨이브 시작 시 추가")으로 큐잉된다. [INFERENCE]
2. **`min(20, 3+floor(n*1.2))`의 실제 캡 시점** — 레인 노트는 "wave14+ 20 캡"이라 적었지만
   공식대로면 wave 14 = `3+16 = 19`, 20 캡은 wave 15부터다. SIM_SPEC이 단일 진실이므로 공식을
   따랐고 테스트도 `14 → 19`, `15 → 20`으로 고정했다. [OBSERVED]
3. **스킬 입력 시점** — 원본은 keydown 핸들러에서 즉시 `useSkill`을 호출한다(프레임 사이).
   포트에서는 `Tick` 진입 직후, `updatePlayer` 이전에 Nova/Ward를 해소한다. 그래야 같은 tick의
   `updateEnemies` 접촉이 이미 켜진 Ward의 보호를 받는 원본 타이밍이 재현된다.
   페이즈 순서 자체는 요구대로 player → enemies → skills → pickups → wave다. [INFERENCE]
4. **Restart 입력** — `RestartQueued`인 tick은 `Restart()` 후 즉시 반환한다(원본에서 재시작은
   프레임 사이에 일어나고 다음 프레임이 새 상태를 돌린다). GameOver 모드에서도 동작한다.
5. **`ActorAction` 매핑** — 원본은 스프라이트 클립이 `idle/walk/attack` 3종뿐이라 사망 시
   `idle`을 강제했다. 계약의 액션 셋(11종)에 맞춰 사망은 `Die`, 적 이동은 `Move`,
   **보스 이동만 `Run`**(SIM_SPEC "run은 보스 전용 예약")으로 매핑했다. 수치 영향 없음:
   시뮬레이션의 분기는 "attack 인가 아닌가"만 본다. 죽은 적의 `ActionTime`은 원본처럼 더 이상
   진행하지 않는다(뷰는 `FadeTime` 0.34→0으로 die 클립을 구동).
6. **플레이어 피격 액션** — 원본은 피격 시 클립을 바꾸지 않는다(hitFlash만). 그대로 두었고,
   HP 0에서만 `Die`로 전환한다.
7. **`SimEvents.WaveStarted`** — 원본 `startWave`는 `waveNumber > 1`에서만 wave 큐를 재생한다.
   오디오 계약 1:1 대응을 위해 웨이브 2부터만 set한다.
8. **`RunDigest.Reason`** — 진행 중에는 `""`, HP 0 종료 시 `"overrun"`.
9. **`SimConfig`에 없는 상수** — 웨이브 스폰 수식, 적 속도/쿨다운/접촉피해 계수, 스폰 간격,
   킬 점수, nova flash 0.42 s, 공격 클립 5프레임/12fps는 `SimConfig`에 없어서 `CinderSim`
   내부 `private const`로 두고 각 줄에 SIM_SPEC 근거를 주석으로 달았다. `SimConfig`가 제공하는
   값은 예외 없이 `SimConfig`를 쓴다.
10. **테스트용 공개 정적 함수 2개** — `CinderSim.SpawnCountForWave(int)`,
    `CinderSim.SpawnPointIndexFor(int, int)`, `CinderSim.IsBossWave(int)`. 시뮬레이션 본체가
    실제로 사용하는 순수 함수라 웨이브 산술을 스폰 대기 없이 직접 검증할 수 있다.
    `ICinderSim` 계약은 변경하지 않았다.

## 4. 결정론·성능 구현 노트

- `UnityEngine` 참조 없음, RNG 없음, LINQ 없음, `foreach` 없음(전부 인덱스 `for`).
- 적/픽업은 `Enemy[]`/`PickupState[]` + count로 보관하고 `ref` 접근으로 복사를 피한다.
  공개 스냅샷(`IReadOnlyList<...>`)은 재사용 `List<T>`에 tick 끝에서 채운다.
- 배열 제거는 원본 `splice`와 동일하게 순서를 보존한다(`Array.Copy`).
- `RunDigest`는 struct라 프로퍼티 접근이 힙을 건드리지 않는다.

## 5. 실행한 검증

### 5.1 문법/컴파일 게이트 (Mono csc, 레인 지정 명령)

```
csc -nologo -t:library -langversion:9.0 -out:/tmp/cinder-sim-check.dll \
    Assets/Scripts/Sim/SimTypes.cs Assets/Scripts/Sim/CinderSim.cs
```

[OBSERVED] exit=0, 경고/에러 출력 없음.

### 5.2 EditMode 테스트 실제 실행 (Unity 없이)

Unity 배치모드는 이 레인에서 열지 않았으므로, 저장소 파일을 그대로 링크한 임시
프로젝트(`/tmp/cinder-tests/tests.csproj`, .NET 8 + NUnit 3.14 + NUnit3TestAdapter)로
**동일한 소스 3개 파일**을 컴파일해 실제로 돌렸다(저장소에는 아무 파일도 추가하지 않음).

```
dotnet test --nologo     # /tmp/cinder-tests
```

[OBSERVED] `Passed! - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 26 ms`

테스트 목록과 커버리지:

| 테스트 | 검증 내용 |
|---|---|
| `Restart_ProducesSpecInitialState` | (768,646), facing +1, HP 100, charge 100, wave 1, pending 4 |
| `Restart_AfterGameOver_RewindsRunToWaveOne` | `overrun` digest → R 입력 후 완전 초기화 |
| `Tick_AfterGameOver_FreezesTheRun` | 게임오버 후 tick은 상태/이벤트 불변 |
| `Tick_SameInputScript_ProducesIdenticalRun` | 600-tick 스크립트 2회 → digest·플레이어·적·픽업 전 필드 일치 (kills>0로 비어있지 않음 보장) |
| `SpawnCountForWave_FollowsSpecFormula` | 1→4, 5→10(보스 포함), 10→16, 14→19, 15→20, 30→20 |
| `SpawnPointIndex_FollowsWaveSeedFormula` | `(wave*3)%8 + id*3` mod 8 = 6,1,4,7 / wave5 id23 → 4 |
| `WaveOne_SpawnsFourEnemiesOnTheFormulaSpawnPoints` | 0.18 s 이전 스폰 없음, 11번째 tick에 (848,840) facing −1, 4기 전부 id·visual `(wave+idx)%4` |
| `WaveClear_KeepsPlayerSkillsAndPickupsTicking` | wave-clear 중 이동·기름 재생 지속, 2.15 s 후 다음 웨이브·pending 수식 |
| `Clamp_KeepsPlayerInsideTheL1Diamond` | 6방향 600 tick 동안 L1 노름 ≤ 1, 종료 시 ≥ 0.99(클램프가 실제로 걸렸음) |
| `Clamp_IsDiamondNotAxisAlignedBox` | 대각 밀기에서 \|x\|<halfW, \|y\|<halfH 이면서 L1=1 → AABB 아님 |
| `Nova_OutsideRadius_SpendsOilAndLeavesEnemyUntouched` | 반경 밖 무피해, 기름 −45, 쿨 6.5, flash>0, NovaX/Y |
| `Nova_DamagesExactlyTheEnemiesInsideTheIsoRadius` | 4기 중 3기(iso ≤250)만 96 피해·사망, 294 iso 1기 무사, 점수 `100*wave*킬` |
| `Ward_RefusesDamageForThreeSecondsButStillBurnsGrace` | 180 tick 동안 HP 100 고정 + grace 소모 tick 존재, 만료 후 다시 피해(비공허 검증) |
| `Pickup_KindRotatesOnEnemyIdAndMagnetCollectsInstantly` | 드롭 종류 `id%3`, 사망 좌표, 자력 78 안쪽은 같은 tick 회수·바깥은 잔존 |
| `Pickup_RelicMoteAppliesScoreAndRelicWhenWalkedOver` | relic-mote 회수 시 relics +1, score +250 |
| `Pickup_ExpiresAfterTwelveSeconds` | 카이팅 중 정확히 720 tick(12 s)에 소멸, 회수 아님 |
| `Attack_HitsEnemyInFrontOncePerAttackId` | 58 피해 1회, 활성창 10 tick 중 피해 tick 정확히 1 (`lastHitAttack`), 피해 시점 `ActionTime ∈ [2/12, 4/12)` |
| `Attack_MissesEnemiesBehindTheFacingArc` | 활성창 내내 사거리 안 + `dx*facing < −18` → 무피해 |
| `Attack_RangeUsesIsoWeightedDistance` | 활성창 내내 원거리 ≤160 이지만 iso >160 → 무피해 (y×1.42 가중이 유일한 기각 사유) |
| `Boss_SpawnsOnWaveFiveWithSixTimesHealth` | 웨이브 5 보스 1기, HP `(58+min(92,4*9))*6 = 564`, scale 1.6, `BossCommander`, pending = 10−1, 스폰 포인트 수식 |

보스/wave-clear 테스트는 스냅샷만 보고 조종하는 결정론적 그리디 파일럿
(가까운 적 추격 + 상시 공격 + 쿨 되는 즉시 Nova/Ward)으로 웨이브 5까지 진행한다.
[OBSERVED] 보스 스폰 tick 1804, 그 시점 kills 22, HP 100.

### 5.3 할당 측정

`GC.GetAllocatedBytesForCurrentThread()` 기준 [OBSERVED]:
- 유휴 600 tick: **0 bytes**
- 전투 3000 tick(웨이브 5 도달, kills 31): **0 bytes**

## 6. 미해결/후속

- **Unity 최종 게이트 미수행** — 이 레인은 Unity 에디터를 열지 않았다.
  `Unity -batchmode -runTests -testPlatform EditMode`는 Unity 소유 레인에서 한 번 돌려야
  asmdef 참조(`nunit.framework.dll`, `UNITY_INCLUDE_TESTS`) 경로까지 확인된다.
  API 표면은 `SimTypes.cs`와 대조 완료, 테스트는 UnityEngine 심볼을 전혀 쓰지 않는다.
- `MathF`는 .NET Standard 2.1(Unity 2021+)에서 제공된다. Mono csc(langversion 9.0)와
  .NET 8 양쪽에서 컴파일 확인.
- 부동소수 잔차: Ward 3 s는 180 tick 감산 후 `2.19e-06`가 남아 한 tick 더 살아있다.
  원본(double)도 같은 성질이며 스펙 위반이 아니라 판단해 별도 보정을 넣지 않았다. [INFERENCE]
