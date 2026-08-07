# Cross-session conflicts log

## 2026-08-05 01:26 — CharacterRosterAnimationTests.cs 컴파일 차단

- **관측**: EditMode 게이트가 `CS0234: 'EditorTools' does not exist in 'CinderCourt'`로 전체 차단.
  파일은 다른 세션 산출물(untracked, 01:20경 등장 — 이 세션 전사에 생성 기록 없음).
- **원인**: 테스트가 `using CinderCourt.EditorTools`를 정적 참조하지만, `Assets/Editor/`에는
  asmdef가 없어 기본 `Assembly-CSharp-Editor`로 컴파일됨 — asmdef 어셈블리
  (`CinderCourt.Tests.EditMode`)는 기본 어셈블리를 참조할 수 없다(Unity 규칙).
- **판단**: 컴파일 에러는 `-testFilter`보다 선행하므로 필터 우회 불가. 전체 게이트가
  막혀 있어 대기 불가. **최소 침습 수정** 선택: `using` 1줄 제거 +
  `GetImportRoster()`의 정적 typeof를 AppDomain 어셈블리 순회 리플렉션으로 교체
  (테스트는 이미 필드를 리플렉션으로 읽고 있었음 — 의도 보존, 구조 변경 0).
- **비선택 대안**: `Assets/Editor/CinderCourt.EditorTools.asmdef` 신설은 다른 세션이
  작업 중인 코드의 컴파일 어셈블리를 바꾸는 구조 변경이라 기각.
- **후속**: 해당 세션이 정적 참조를 원하면 asmdef 신설 + 테스트 참조 추가로 대체 가능.
  이 수정은 그 결정을 막지 않는다.

## 2026-08-06 15:30 — HudView.cs: 596e862가 게이지 렌더 수정을 되돌림

- **관측**: `596e862`(AMENDMENT #10)가 `Assets/Scripts/View/HudView.cs`를 +80줄로
  커밋했는데, 그 시점 HEAD 버전에 View 레인의 게이지 수정이 **없다**.
  측정: `git show HEAD:...HudView.cs` → `MakeFilled` 0회 / `FillSprite` 0회.
  작업 트리 → 각각 7회 / 4회. **지금 HEAD에서 빌드하면 체력·기름 게이지가
  다시 안 줄어든다.**
- **원인**: 두 세션이 같은 파일을 동시 편집. 한쪽(#7 훈련장·서지)은 `SyncSurge`를
  추가했고, 다른 쪽(View VFX 레인)은 `MakeFilled`/`FillSprite`를 추가했다.
  커밋된 스냅샷에 후자가 빠졌다. 누구의 실수인지는 판별하지 않았고 중요하지도
  않다 — 동시 편집의 정상적 실패 양상이다.
- **무엇이 없어졌나**: uGUI `Image.OnPopulateMesh`(`Image.cs:883-889`)는
  `activeSprite == null`이면 `Type.Filled` 분기에 도달하기 전에 탈출해 꽉 찬
  사각형을 그린다. `HudView`가 만드는 Filled 이미지에 스프라이트가 없어서
  `fillAmount`는 매 프레임 쓰이지만 화면에 도달할 경로가 없었다. 체력·기름뿐
  아니라 XP·보스·추출·차지 게이지, 스킬 쿨다운 오버레이가 전부 같은 버그였다.
- **판단**: 되돌리거나 강제 덮어쓰지 않았다(§5). 대신 **작업 트리에 양쪽이 모두
  살아 있는 상태로 스테이징**했다. 검증:
  `git show :...HudView.cs` → `MakeFilled` 7 · `SyncSurge` 1.
  **이 스테이징을 그대로 커밋하면 #7 작업이 보존되고 게이지 수정이 복구된다.**
- **현재 스테이징된 것**(17 파일, View 레인 산출물 전부):
  `VfxDirector.cs`(§S1 스킬 실루엣) · `ActorView.cs` · `HudView.cs` ·
  `LobbyStaging.cs` · `CharacterImportPipeline.cs` · `CinderActor.controller` ·
  테스트 4종(`HudLayoutTests` 수정, `ClipWindowTests`/`SkillShapeVocabularyTests`/
  `ViewColliderStripConventionTests` 신규) · `.survey/skill-vfx-intensity/` ·
  `qa/skill-vfx-mode-coverage.md`.
- **게이트**: 저장소 루트 EditMode **319/319 통과**(클론 아님,
  `unity-logs/test-results-152808.xml`). WebGL 빌드 성공(54.8 MB, 0 errors).
  브라우저 실기에서 융기 크라운·크랙 팬 육안 확인, 체력 135→128→37→0 렌더 확인,
  스킬 중첩 시 프레임 델타 0(median 8.3 ms 동일 — 단 M5 Pro 기준, 타깃 기기 아님).
- **후속**: #7 레인이 자기 잔여 변경과 함께 한 번에 커밋하면 된다. View 레인은
  추가 작업 없음. `HudView.cs`를 다시 만질 때는 `MakeFilled`가 살아 있는지
  확인할 것 — 이 회귀는 조용하고(예외 없음·빌드 성공) 기존 테스트
  `ResetRunUi_ReseedsHealthBarForNewRun`은 `fillAmount`만 봐서 버그 내내 초록이었다.
  신규 `HealthMeter_MeshNarrows_WhenTheSimDrainsHealth`는 실제 메시 폭을 재므로
  이제 잡힌다.

---

## 2026-08-06 16:45 — AMENDMENT #10 레인 (훈련장·돌발) ↔ VFX/클립 레인

**공유 파일 2개에서 실제로 부딪혔고, 인덱스 사고 2건이 났다. 둘 다 해소.**

### 무엇을 공유했나

| 파일 | 이 레인(#7) | 상대 레인 |
|---|---|---|
| `HudView.cs` | 서지/시련 배너, `SetTrialMode`, 세리머니·패배 문구 분기 | `MakeFilled` 회귀 수정, 게이지 메시 계약 |
| `VfxDirector.cs` | `CurrentPushSign`에 `TrainingTrials` 스캔 추가 (14줄) | `RemovePrimitiveCollider` 외 +247줄 |

### 사고 1 — 커밋이 인덱스 전체를 삼켰다

`git commit`을 pathspec 없이 실행해 **상대 레인의 스테이징된 작업 전부**
(테스트 3종, `VfxDirector` +281, 컨트롤러, 임포트 파이프라인, `.survey/`)가
내 커밋 `f3fe641`에 들어갔다. `git reset --soft HEAD~1`로 되돌리고
`git commit --only <paths>`로 재커밋(`e1aa041`, 문서 2파일). **워킹트리·인덱스
무손상**, 상대 레인 스테이징 상태 그대로 복원.

### 사고 2 — 인덱스가 내 커밋을 되돌리는 상태로 방치돼 있었다

상대 레인이 스테이징한 `HudView.cs` 스냅샷은 **내 커밋 이전 시점**이라
`index-vs-HEAD`가 `+79/−100`이었다. 그대로 커밋되면 내 100줄이 조용히
사라진다. 워킹트리는 양쪽이 올바르게 합쳐진 상태였으므로 **인덱스를
워킹트리로 갱신**(`git add` 2파일). 상대 내용은 전부 보존됨을 마커로 확인:
`RemovePrimitiveCollider` 21곳, `SyncSurge` 1곳, `TrainingTrials` 2곳.

### 내가 지킨 것 / 상대가 확인할 것

- 내 커밋 3개(`596e862`, `ca025dd`, `e1aa041`)는 **내 헝크만** 담는다.
  공유 파일 2개는 diff를 헝크 단위로 갈라 커밋했다(HudView 20/27,
  VfxDirector 1/32). 헝크 분할이 파일을 두 번 깨뜨렸고 두 번 다
  **임시 워크트리 컴파일 검증**이 잡았다 — 분할 후에는 반드시 파싱을 확인할 것.
- **상대 레인은 지금 그대로 커밋하면 된다.** 인덱스에 양쪽 작업이 다 들어
  있고, 내 몫은 이미 커밋됐으므로 중복 커밋되지 않는다.
- 게이트: 이 상태에서 EditMode **319/319** (`unity-logs/test-results-164112.xml`).

---

## 2026-08-07 11:20 — PR #3 (training-on-main) 머지: stale-base 회귀 2건 수정

**PR #3은 A9(모멘텀)가 main에 착지하기 전 분기점(53a1ed4) 기준으로 작성**됐다.
PR 본문 §5 "A9 심 API는 main에도 없다"는 리뷰 시점에 이미 낡은 전제였다.

### 충돌 4파일 해소

| 파일 | 해소 |
|---|---|
| `SimTypes.cs` | **양측 보존 + 비트 재배정**: main `MomentumTierUp=1<<23` 유지, PR 3종을 `PylonDown=24 / PerilOpened=25 / SurgeOpened=26`으로 시프트. PR 자신의 규칙("main이 낮은 비트를 갖고 이쪽이 올라간다", 비트 22 선례) 연장. 숫자 참조 스윕: `(SimEvents)N`·`1 << 2x` 병합 트리 전수 0건 |
| `ActorView.cs` | PR의 `CastPoseDuration` 상수 + main의 `_castPoseArmed` 프레임 래치 결합 |
| `StageCatalog.cs` | 양 레인이 각각 추가한 필드 **둘 다 유지**: main `RoomObjective` + PR `Epithet` (생성자 16-인자). 엔트리 0-5는 양측 문자열 병기, PR 신규 스테이지 6-8(cinder-sluice/ember-bastion/ash-march)에는 RoomObjective 3문장 신규 저작 (계약: 비공백·트림·전역 유일·제목과 상이 — 데이터 검증 통과) |
| `SIM_SPEC_HACKSLASH.md` | 양측 append 보존 (main A9 + PR 각인·#10 훈련장/돌발) |

### stale-base 회귀 수정 (충돌 아님 — 자동머지가 조용히 통과시킴)

1. `HudView.cs:2836` 주석 처리된 `SyncMomentumGauge(...)` 호출 복원 — A9 심 API가
   main에 실재하므로(HackTypes.cs:363-368) 그대로 두면 A9 HUD가 꺼진 채 출하되고
   MomentumTests는 심만 검증해서 게이트가 못 잡는다.
2. `HudView.cs:1199` 로컬 `const momentumMax=100f` 제거, `HackSpec.MomentumMax` 복원.

### 게이트 (에디터 pid 16568이 프로젝트 점유 — 배치모드 불가)

- [OBSERVED] 심 게이트: `/tmp/pr3-simgate` dotnet test — 순수 심 스위트 8종
  (CinderSim/CompanionAutonomy/CompanionSkill/HackSim/Momentum/Sigil/TrainingSurge/
  WaveTelegraph) **198/198 통과** (병합 트리 소스, A9와 PR 신규 시스템 공존 증명).
- [OBSERVED] View 컴파일: `msbuild CinderCourt.View.csproj` exit=0 (경고만).
  StageCatalog 16-인자 생성자·HudView `HackSpec.MomentumMax` 타입체크 포함.
- [OBSERVED] EditMode 테스트 어셈블리(구 파일 목록): msbuild exit=0.
- [미실행] Unity EditMode 전체 러너 — 에디터 점유로 배치모드 불가. csproj가
  머지 신규 테스트 7파일을 아직 미포함(에디터 재임포트 시 자동 갱신). PR 자체
  게이트는 431/431이나 **pre-A9 베이스 측정치**라 병합 트리 증거로 인용 불가.
- [OBSERVED] RoomObjective 계약(비공백/유일/제목상이): 카탈로그 9엔트리 데이터
  수준 검증 통과 (러너 실행 아님).

### 문서 부채 (머지에서 수정하지 않음 — 저작 판단 필요)

- PR의 각인 증보가 "AMENDMENT #6" 제목을 사용 — main의 Frozen Contract
  Amendment #6(멀티슬롯 동료 DRAFT)과 **번호 충돌**. 내부 §13.x도 main §13
  결정론과 충돌. 코드 동작 무관, 스펙 넘버링 정리는 오퍼레이터/저자 몫.

### 후속 (11:55) — 저자 재머지 수렴, PR #3 MERGED

- 저자가 같은 stale-base 문제를 자기 브랜치에서 독립 해소하고 PR 헤드를
  f747168로 갱신(Unity 재기록 골든 + EditMode 463/463 XML 7종 동봉).
- **내 머지(685605f)의 결함 발견**: DungeonGoldenDigestTests에 pre-A9 골든
  리터럴을 남김 — A9 모멘텀이 던전 스윙 피해를 곱하므로 9행이 이동해야 했다
  (cinder-span 3700→4350 = tier-2 1.18×). 내 심 게이트 8스위트에 이 스위트가
  없어(View 의존) 못 잡았다. 수렴 머지 c14d44e가 저자 버전 채택으로 해소.
- 교훈(정정 2026-08-07 12:05): 골든 스위트는 `using CinderCourt.View`
  (StageCatalog 경유) 때문에 심 소스 복사만으로는 스크래치 컴파일이 안 되지만
  (CS0234 — 첫 시도에서 관측), **UnityEngine 직접 의존은 없고** 깨진 행
  `cinder-span|3700→4350`은 **정수(score) 열**이라 dotnet에서도 판정 가능했다
  (파일 헤더: 15행 전부 정수 열은 Unity와 일치, X/Y 부동소수점만 ~4 ULP 표류).
  올바른 게이트 개선: 심 동작을 바꾸는 머지는 **골든 스위트 + CampaignSimTests
  + StageCatalog(+Color 스텁)를 dotnet 게이트에 편입하고 정수 열만 신뢰**,
  부동소수점 열은 Unity 러너 몫으로 남긴다. "Unity 없이는 불가"가 아니라
  "게이트에 안 넣어서 못 잡은 것"이 정확한 원인이다.
- 최종 상태: HEAD의 Sim 폴더+골든 테스트 2파일이 f747168과 **바이트 동일**
  (git diff 0줄) → 저자의 463/463은 이 바이트에 대한 유효한 이월 증거.
  4레인 후속 작업은 충돌 영역 밖 생존 검증 완료. PR #3 GitHub MERGED
  (2026-08-07T04:16:25Z).

## 2026-08-07 16:12 — AMENDMENT #11 레인이 라이브라서 푸시 보류

- **관측**: "미커밋 전부 푸시" 정리 작업 중, 작업 트리에 다른 세션(jeo)의
  진행 중 레인이 섞여 있었다. 판정 시점 기준 갱신 시각:
  `Assets/Scripts/View/GameView.cs`·`ImpactBudget.cs` 1분 전,
  `docs/SIM_SPEC_HACKSLASH.md` 3분 전, `DifficultyGroupAiTests.cs` 3분 전,
  `CinderSim.cs` 9분 전, `HackTypes.cs` 19분 전.
  `.jeo/artifacts/tool-results/` 최신 기록 66초 전 — 세션이 살아 있다.
- **게이트 실측**: Unity 에디터가 다른 세션에 점유돼 있어(pid 16568) 배치
  게이트를 못 돌렸다. Sim은 순수 C#이라 standalone dotnet으로 대체 검증:
  - `Assets/Scripts/Sim/*.cs` 단독 빌드(netstandard2.1) → **0 error 0 warning**.
    즉 `DifficultySpec.cs` 신설과 `HackConfig.Difficulty` 추가는 컴파일 성립.
  - Sim + `DifficultyGroupAiTests.cs`(NUnit) 빌드 → **컴파일 실패 3건**:
    - `DifficultyGroupAiTests.cs(15,28)` / `(48,38)` CS0121 — `TryDungeon`의
      4번째 인자에 bare `null`을 넘겨 `string` 오버로드(HackTypes.cs:250)와
      `string[]` 오버로드(:283)가 모호. `(string[])null` 등 캐스트 필요.
    - `DifficultyGroupAiTests.cs(228,42)` CS8156 — `ref readonly var e =
      ref enemies[i]`. 인덱서 반환값은 참조로 넘길 수 없다.
- **판단**: 컴파일 에러는 어셈블리 전체를 막으므로, 이 상태를 푸시하면 502개
  기존 테스트가 **한 개도 못 도는** 레드 main이 된다. 코드 5파일
  (`DifficultySpec.cs`, `CinderSim.cs`, `HackTypes.cs`, `GameView.cs`,
  `ImpactBudget.cs`, `DifficultyGroupAiTests.cs`, `SIM_SPEC_HACKSLASH.md`)은
  **커밋하지 않는다**. 작업 트리 원본은 손대지 않았다 — 남의 라이브 버퍼를
  고치면 lost-update가 난다.
- **선반영한 것**: 같은 레인의 무해한 기록물은 먼저 밀어 증거를 보존했다 —
  `docs/provenance/video-analysis-wbDv6nawEeY.md`,
  `_workspace/current/design/video-review-analysis-amendment11.md` (86ff932).
- **별건 결함(레인 소유자 확인 필요)**: 신규 `.cs` 3개에 `.meta`가 없다
  (`DifficultySpec.cs`, `ImpactBudget.cs`, `DifficultyGroupAiTests.cs`).
  같은 폴더의 다른 모든 `.cs`는 `.meta`를 추적 중이라, 이대로 커밋되면
  머신마다 GUID가 새로 생겨 참조가 흔들린다. 에디터 임포트 후 함께 커밋할 것.
- **프로버넌스 불일치**: provenance의 `derivedChanges`는
  `Assets/Tests/EditMode/DifficultyTests.cs`를 가리키는데 실제 파일명은
  `DifficultyGroupAiTests.cs`다. 레인 마감 시 정정 대상.
