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

- **관측**: `596e862`(AMENDMENT #7)가 `Assets/Scripts/View/HudView.cs`를 +80줄로
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

## 2026-08-06 16:45 — AMENDMENT #7 레인 (훈련장·돌발) ↔ VFX/클립 레인

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
