# HongT — 결정론 심 테스트에서 반복 재현되는 "앵커 오프바이원" 함정

작성 2026-08-07 · 대상 저장소 `~/orca/workspaces/HongT/main` · Amendment #7 (동료 자율성)
검증 중 **독립적으로 두 번** 같은 실수가 재현되어 기록한다.

## [OBSERVED] 증상

`CinderCourt.Sim`의 동료 자율성(앵커 기준 300 px 획득 반경 / 320 px 리시)을 검증하는
테스트가, 심 구현이 정확한데도 실패한다.

- 시도 A(내 계측 하네스): 획득 반경 위반 16건(최악 308.74 > 300), 락 해제 위반 5건.
- 시도 B(다른 세션이 `Assets/Tests/EditMode/HackSimTests.cs`에 작성한
  `CompanionAutonomy_HoldsItsLockUntilDeathLeashExitOrTwoSeconds`):
  `tick 140 slot 0 released a live in-leash lock after 14 ticks` 로 EditMode 316개 중 1개 실패.

## [OBSERVED] 원인 — 틱 내부 순서

`CinderSim.Tick`의 갱신 순서는
`CastSkills → UpdatePlayer → UpdateCompanionBehavior → UpdateCompanion → UpdateEnemies`다.
따라서 틱 T에서 `UpdateCompanionSlot`이 사용하는 값은 **비대칭**이다:

| 값 | 심이 보는 시점 | 테스트가 읽어야 할 것 |
|---|---|---|
| 앵커(플레이어 위치·facing) | 틱 T의 **UpdatePlayer 직후** | `sim.Tick()` **호출 후**의 `sim.Player` |
| 적 위치 | 틱 T-1 종료 시점(UpdateEnemies가 뒤에 있음) | `sim.Tick()` **호출 전**에 복사해 둔 목록 |
| 동료 자기 위치 | 틱 T-1 종료 시점 | `sim.Tick()` 호출 전 값 |

앵커만 "틱 전 플레이어"로 읽으면 한 플레이어 스텝(3.63 px)만큼 어긋나고,
**대시(190 px/0.22 s ≈ 14.4 px/틱)나 facing 반전 시에는 앵커가 한 번에 160 px
(`CompanionFollowOffset` 80 × 2) 점프**한다. 고정 여유값(`- 8f` 같은 슬랙)으로는
절대 흡수되지 않는다. 앵커를 정확히 맞추면 위반 건수가 **정확히 0**이 된다
(계측: 최악 획득 거리 299.98 ≤ 300).

## [OBSERVED] 함께 걸린 두 번째 함정 — 슬롯 순서와 같은 틱 내 사망

락 해제 사유를 검증할 때 "직전 틱 종료 시점에 타깃이 살아 있었는가"만 보면 오탐이 난다.
합법적인 조기 해제가 두 가지 더 있다:

1. **낮은 인덱스 슬롯이 같은 틱에 그 타깃을 죽인다** — 슬롯은 0..n 순서로 갱신된다.
2. **자기가 추격해서 같은 틱에 죽인다** — 추격 스텝이 공격 판정보다 **먼저** 실행되므로,
   틱 시작 시 사거리 밖이던 타깃이 이동 후 사거리 안에 들어와 처치되고
   `CinderSim.cs`가 같은 틱에 락을 해제한다(스냅샷이 시체에 락을 걸어 두지 않기 위함).

그래서 `CompanionEngagedAt(slot) == true` 이면서 `CompanionTargetIdAt(slot) == 0`인
틱이 **정상적으로** 존재한다. "engaged면 락이 있다"는 단언은 틀린 불변식이다.
사망 판정은 **해제 틱의 시작과 끝 양쪽**에서 확인해야 한다.

## [OBSERVED] 세 번째 함정 — 상수를 자기 자신으로 검증하기

`Assert(step <= HackSpec.CompanionPursuitSpeedScale * speed * dt)` 처럼 **검증 대상 상수를
기대값으로도 쓰면** 상수를 1.05 → 1.60으로 바꿔도 테스트가 통과한다(실측 확인: 뮤턴트 생존).
계약 수치는 테스트 안에 **리터럴로 다시 적고**, 별도 테스트 1개에서만 `HackSpec`과 대조한다.
이 구조로 바꾼 뒤 5개 뮤턴트(1.05→1.60, 300→900, 2.0→0, 320→1200, 0.35→0) 전부 사살됐다.

## [INFERENCE] 재사용 규칙

1. 결정론 심 테스트를 쓰기 전에 **틱 내부 갱신 순서를 먼저 읽어라.** 어떤 상태가 "이번 틱"이고
   어떤 상태가 "지난 틱"인지 표로 적고 시작한다.
2. 불변식은 **단언하기 전에 계측하라.** 순수 C# 심(`Assets/Scripts/Sim/**`은 UnityEngine 참조가
   주석에만 있음)은 `dotnet` 콘솔 하네스로 그대로 컴파일된다 — Unity 배치모드(약 2분) 대신
   0.5초 루프로 반증할 수 있다. 위반 건수를 세어 0을 확인한 뒤 테스트로 굳힌다.
3. 계측이 위반을 보고하면 **심을 고치기 전에 계측기를 의심하라.** 위 3건 모두 계측기가 틀렸다.

## 관련 경로

- 구현: `Assets/Scripts/Sim/CinderSim.cs` (`UpdateCompanionSlot`, `ResolveCompanionTarget`),
  상수: `Assets/Scripts/Sim/HackTypes.cs` (`HackSpec.Companion{AcquireRadius,LeashRadius,
  PursuitSpeedScale,TargetLockSeconds,ReturnGraceSeconds}`).
- 게이트: `Assets/Tests/EditMode/CompanionAutonomyTests.cs`(14),
  `Assets/Tests/EditMode/HackSimTests.cs`의 `CompanionAutonomy_*`(12).
- 설계: `_workspace/current/design/companion-autonomy-amendment-proposal.md`.
- [OBSERVED] 2026-08-07 EditMode **316/316 통과, 실패 0** (3.14 s),
  결과 XML `_workspace/current/engineering/unity-logs/test-results-080205.xml`.
