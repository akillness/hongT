# LANE: Deterministic Simulation (owner: gjc)

## Mission
`Assets/Scripts/Sim/CinderSim.cs` 하나와 EditMode 테스트
`Assets/Tests/EditMode/CinderSimTests.cs`를 작성한다. 다른 파일 생성/수정 금지.
`Assets/Scripts/Sim/SimTypes.cs`는 // FROZEN CONTRACT — 절대 수정 금지.

## Binding docs (모두 읽을 것)
- `docs/SIM_SPEC.md` — 유일한 수치 진실. 모든 상수는 `SimConfig` 사용.
- `Assets/Scripts/Sim/SimTypes.cs` — 구현할 인터페이스 `ICinderSim`.

## Requirements
1. `public sealed class CinderSim : ICinderSim` (namespace `CinderCourt.Sim`).
   - 생성자: 파라미터 없음. 초기 상태 = Restart()와 동일.
   - `Tick(in SimInput)`: 정확히 1/60 s 진행. 내부에서 원본 fixedUpdate 순서 준수:
     updatePlayer → updateEnemies → updateSkills → updatePickups → updateWave.
   - `UnityEngine` 사용 금지 (asmdef가 이미 차단). `System.Math`/`MathF`만.
   - RNG 금지 — 스펙의 모듈러 산술만. 같은 입력 시퀀스 → 같은 Digest.
2. 스펙 디테일 강조 (자주 틀리는 부분):
   - 클램프는 L1 다이아몬드 (halfH = 270 − margin*0.5).
   - 공격 활성창: ActionTime ∈ [0.167, 0.333], attackId당 적 1회 피격.
   - 적 접촉타격: attack 시작 0.167 s 후 1회, 반경 (76+14), 아이소 y×1.42.
   - Ward 중 피격: 데미지 0이지만 grace 0.38 s는 소모.
   - wave-clear 중에도 플레이어/스킬/픽업은 계속 tick (원본 isActiveMode 동일).
   - 게임오버 시 enemies 루프 즉시 중단 (원본 break 동작).
   - 보스: 웨이브 5의 배수 시작 시 1기 추가 (SIM_SPEC §Bosses).
   - `SimEvents` 플래그는 tick마다 리셋 후 해당 tick 발생 이벤트만 set.
   - Restart: 원본 restartGame 순서/값 그대로 (플레이어 (768,646), charge 100).
   - visualKind: `(wave + spawnIndexInWave) % 4`, 보스는 BossCommander/BossMonarch.
3. 테스트 (NUnit, `CinderCourt.Tests.EditMode` asmdef 참조가 이미 있음):
   - 결정론: 동일 600-tick 입력 스크립트 2회 → Digest/좌표 완전 일치.
   - 웨이브 산술: wave1 spawn 4기, wave14+ 20 캡, 스폰 포인트 인덱스 수식 검증.
   - 클램프: 다이아몬드 경계 밖으로 밀었을 때 L1 노름 ≤ 1.
   - Nova: 반경 안/밖 경계값, 기름 45 소모, 쿨 6.5.
   - Ward: 지속 3 s 동안 HP 불변 + grace 소모 확인.
   - 픽업: id%3 드롭 종류, 자력 반경, 12 s 만료.
   - 보스: 웨이브 5에서 IsBoss 1기, HP = (58+min(92,4*9))*6.
   - 공격판정: 전방 -18 허용, 후방 명중 불가, 아이소 y 가중.
4. 성능: 힙 할당 최소화 — 리스트 재사용, LINQ 금지, foreach 대신 for.

## Verification (직접 실행할 것)
`csc`나 dotnet이 없어도 된다 — 문법 확인은 다음으로 대신한다:
`mcs`/`csc` 시도 가능하면 `csc -nologo -t:library -langversion:9.0 Assets/Scripts/Sim/SimTypes.cs Assets/Scripts/Sim/CinderSim.cs` (nunit 참조 없는 Sim만).
테스트 파일은 컴파일 시도 없이 API 표면만 SimTypes와 대조- Unity가 최종 게이트.

## Reporting
완료 시 `_workspace/current/engineering/gjc-sim-lane-report.md`에:
구현한 파일, 스펙 해석이 갈렸던 지점과 선택, 실행한 검증 명령과 결과.
git 조작 금지. 포매터/린터 실행 금지.
