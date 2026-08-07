# 영상 리뷰 분석 리포트 — Achilles: Legends Untold 리뷰 대응 (AMENDMENT #11)

- **분석 대상**: 카사노박TV (2024-05-03, 8분 35초), 'Achilles: Legends Untold' (쿼터뷰 액션 RPG) 리뷰
- **영상 URL**: https://youtu.be/wbDv6nawEeY
- **자막 경로**: `/tmp/ytana/transcript.txt`
- **관련 구현**: `Assets/Scripts/Sim/DifficultySpec.cs`, `CinderSim.PlanEnemyGroup`, `HackConfig.Difficulty`

---

## 1. 개요 및 관측 사실 [OBSERVED]

분석 대상 영상은 'Achilles: Legends Untold'의 정식 출시/스토브 한글화 리뷰 영상으로, 리뷰어가 지적한 핵심 축과 발언은 다음과 같다.

1. **난이도 및 적 그룹 AI 지적**
   - `[OBSERVED]` **[02:56]**: "어 이 게임에서의 보통 난이도는 전투가 너무 쉽고 또 몹들도 너무 멍청하다는 생각이 들었기 때문에... 차라리 난이도를 어려움으로 설정하는게 좋지 않을까 하는 그런 생각도 들었습니다."
   - `[OBSERVED]` **[03:18]**: "이게 어려움 난이도를 통해 이 더 개선이 된다라고 하면 적들이 게임 유저의 전투 방식을 바로바로 학습하면서... 다른 적들과 함께 유저를 공격해 온다 이 말이에요."
   - `[OBSERVED]` **[02:43-02:52]**: 각 난이도별 차이점으로 "받는 피해량", "적의 공격성", "가이아(GAIA)라 불리우는 적들의 그룹 AI"의 3가지 요소가 영향을 미침을 언급함.

2. **타격감 및 모션 한계 지적**
   - `[OBSERVED]` **[01:16]**: "지금은 그때보다 조금 더 나아졌다 뿐이지 그렇다고 지금도 좋은 편은 아니었습니다. 뭔가 전투와 관련된 모든 모션들이 여전히 굉장히 엉성함이 많이 묻어나 있길래..."
   - `[OBSERVED]` **[02:08]**: "이 모션이나 타격감에 대한 아쉬움이 해결되기 전까지는 저도 70점 이상 주긴 어려울 것 같기 때문에..."

3. **장비/템파밍 깊이 아쉬움**
   - `[OBSERVED]` **[01:41-01:46 / 04:28-04:34]**: 무기와 갑옷 정도로 장비 종류가 제한되어 템 파밍의 재미가 떨어진다고 지적.

---

## 2. 우리 프로젝트 매핑 및 설계 결정 [INFERENCE]

### 2.1 이번에 구현한 것 (AMENDMENT #11)

리뷰어의 핵심 발언("보통 난이도는 전투가 너무 쉽고 몹이 멍청함", "어려움 난이도에서 그룹 AI가 개선됨")에 정확히 대응하여 `Difficulty` 시스템과 cooperative 적 그룹 AI를 구현하였다.

1. **난이도 축 매핑 (`DifficultyProfile`)**
   - 리뷰어가 언급한 3대 축(피해량, 공격성, 그룹 AI)을 정확히 `IncomingDamageMul`, `AttackCooldownMul`, `GroupAi` (+ `AttackTokens`, `RingRadiusMul`, `FlankBias`) 프로필 수치로 정의.
   - `Normal=0`을 기본값으로 유지하여 레거시 결정론 골든 다이지스트 파괴 없이 기존 동작을 100% 보존.

2. **그룹 AI의 Hard/Nightmare 한정 활성화 (`GroupAi = true`)**
   - `[INFERENCE]`: 리뷰어가 "보통 난이도에서는 몹이 멍청하고, 어려움 난이도에서 그룹 AI가 작동/개선된다"고 지적한 점에 부합하도록, `GroupAi` 매스터 스위치를 `Normal`(0)과 `Story`(1)에서는 `false`(기존 개별 추격거동), `Hard`(2)와 `Nightmare`(3)에서만 `true`로 설정함.
   - `Hard` 이상에서는 적들이 플레이어 주변 포위 링(`RingSlots=8`)의 자기 슬롯으로 오비팅하며 대기하고, `PlanEnemyGroup()`을 통해 유효 공격 토큰(`AttackTokens`: Hard 3개, Nightmare 4개)을 부여받은 적만 순서대로 공격을 개시함.
   - `FlankBias = 0.75`를 적용하여 정면(`ForwardThreshold = -18`)에 서 있지 않은 측/후방 적이 공격 토큰을 우선 획득하도록 유도함으로써 유저 후방 협공을 연출함.

### 2.2 타격감(hit feel) — 같이 처리한 것 (View 전용)

리뷰어가 이 게임에 70점 이상을 주지 못한 **유일한 이유**가 타격감이었으므로
(01:16, 02:08), 시뮬레이션 개정과 별개로 View 층의 임팩트 피드백을 함께 손봤다.

- `[OBSERVED]` 개정 전 감사 결과: 일반 근접 적중(`SimEvents.EnemyHit`)에는
  히트스톱도 카메라 펀치도 **전혀 없었다**. 시간/카메라 채널은 처치(0.04 s)와
  콤보 피니셔(0.07 s)에만 붙어 있었고, 두 채널의 병합 규칙이 서로 다른 if/else-if
  체인으로 흩어져 있었다.
- **구현**: `Assets/Scripts/View/ImpactBudget.cs` — 세 티어(Light 0.028 s /
  Kill 0.045 s / Finisher 0.075 s)를 하나의 표로 통일하고, 같은 틱에 여러
  이벤트가 겹치면 가장 무거운 티어 하나로 해소한다. 짧은 요청이 진행 중인 긴
  히트스톱을 깎지 못하도록 Max 병합한다.
- **Light refractory 0.14 s**: 이것이 이 변경의 핵심 안전장치다. 일반 적중마다
  히트스톱을 걸면 다수의 적을 연타할 때 화면이 슬로우모션으로 눌어붙어 "타격감
  추가"가 오히려 퇴보가 된다. 0.14 s 안의 두 번째 Light 는 시간도 카메라도 받지
  못한다.
- **접근성**: 시간 채널은 기존 `ViewPrefs.TimeEffectsAllowed`(모션 약함) 게이트를
  그대로 존중하고, 카메라 채널은 `CameraRig.Punch` 안의 기존 `ReducedMotion`
  게이트가 단독으로 소유한다 — 병행 관례를 만들지 않았다.
- **한계 [INFERENCE]**: 리뷰어가 말한 "모션이 엉성하다"의 절반은 애니메이션
  클립 자체의 품질이며, 그건 리소스 파이프라인(§docs/RUNTIME_ANIMATION_CONTRACT.md)
  작업이다. 이번 변경은 "모션이 접촉하는 순간의 반응"만 다룬다.

### 2.3 이번에 구현하지 않은 것과 그 이유

1. **실시간 기계학습 AI / 절차적 상호작용 (동료 방패 밟고 점프, 불화살 등)**
   - `[INFERENCE]`: 리뷰어가 03:28에서 언급한 '방패 밟고 점프', '불붙이기' 등은
     고비용 절차적 애니메이션과 정밀 3D 풋플레이스먼트를 요구한다. 본 프로젝트는
     60 Hz 고정스텝 순수 C# 시뮬레이션 + WebGL 제약(compute/threads 금지) 위에서
     돌아가므로, 비결정론적 학습 AI 대신 **결정론적 포위 링 + 공격 토큰 교대**로
     "협공당하는 감각"만 취했다.

2. **장비 파밍 가짓수 확장**
   - `[INFERENCE]`: 01:41 / 04:28 의 "무기와 갑옷이 전부라 파밍 재미가 떨어진다"
     지적은 메타 루프 콘텐츠 영역이다. 현재 우리 축은 3슬롯 선형 강화(T0–T5) +
     각인 2슬롯이며, 확장은 별도 개정으로 다룬다.

3. **인게임 중 난이도 변경**
   - 영상 03:47 은 "게임 중 언제든 난이도 변경 가능"을 장점으로 든다. 우리는
     티어를 **런 생성 시점에 고정**했다 — 런이 `(config, 입력 시퀀스)` 만으로
     재현되어야 골든 다이제스트와 결정론 계약이 성립하기 때문이다. 로비에서
     언제든 바꿀 수 있고 다음 강하부터 적용된다.

---

## 3. 검증 결과 [OBSERVED]

- `[OBSERVED]` **Normal 무변경 증명**: 개정 전(git HEAD) 심과 개정 후 심을 동일
  입력으로 arena 5400틱 / prologue 3600틱 / dungeon(cinder-span) 5400틱 돌려
  97틱마다 플레이어 좌표·HP·점수·웨이브와 전체 적 좌표/액션/HP 를 덤프한 153행이
  **완전히 동일**했다. 골든 다이제스트 재핀이 필요 없다.
- `[OBSERVED]` **티어 게이트**: `Assets/Tests/EditMode/DifficultyGroupAiTests.cs`
  8건, `ImpactBudgetTests.cs` 8건이 통과한다. 관측된 최소 공격 간격은 Story 93틱 /
  Normal 75틱 / Hard 64틱 / Nightmare 54틱으로, 각 티어의 `AttackCooldownMul` 이
  실제 공격 주기에 나타난다.
- `[OBSERVED]` **토큰 상한**: 동일 시나리오에서 동시 스윙 최대치가 Story 2 /
  Normal 3 / Hard 3 / Nightmare 4 로 각 티어의 `AttackTokens` 를 넘지 않았다.
  Normal(무제한)이 Story(2)를 넘어선다는 점이 토큰이 실제로 작동한다는 증거다.
- `[OBSERVED]` **포위 거동**: 1800틱 관측에서 공격 사거리 안으로 동시에 밀려드는
  적 최대치가 Hard 3 vs Normal 4 이고, 적-플레이어 평균 거리가 Hard 106.4 vs
  Normal 93.8 이다. 팩이 실제로 물러서서 교대한다.
- `[BLOCKED]` Unity 배치모드 EditMode 전체 스위트는 이 세션에서 실행하지 못했다 —
  다른 세션의 Unity 에디터(PID 16568)가 프로젝트를 점유 중이라 배치모드가
  "another Unity instance is running" 으로 거부된다. 대신 순수 C# 심 테스트를
  dotnet 으로 격리 실행했고, `dotnet build CinderCourt.Tests.EditMode.csproj` 로
  EditMode 어셈블리 전체가 0 에러로 컴파일됨을 확인했다.

## 4. 남은 작업 [TARGET]

- `[TARGET]` 에디터 점유가 풀리면 `bash tools/unity_batch.sh tests` 로 EditMode
  전체 스위트(기존 195건 + 신규 25건)를 돌려 골든 다이제스트 통과를 재확인한다.
- `[TARGET]` 난이도별 실플레이 밸런스(어려움이 "재밌게 어려운가")는 측정된 바
  없다. 플레이테스트 후 `_workspace/current/design/balance-sheet.md` 에 기록한다.
