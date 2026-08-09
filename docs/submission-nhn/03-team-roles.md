---
title: "팀원 롤 기술서"
subtitle: "제출물 5 · 팀원 롤 기술서 — Abyssal Lantern: Hold the Cinder Court (Unity 재구현)"
author: "HongT · 정장영 · 이석민 · 정우영"
lang: ko
---

# 1. 팀 구성

팀명은 **HongT**, 3인 팀입니다. 기획과 QA는 세 명이 공동으로 참여했고, 구현
담당은 저장소 레벨에서 분리했습니다. 이 팀에서 **"담당"은 곧 "커밋"**입니다 —
아래 표의 구현 영역은 모두 저장소의 변경 이력에서 직접 확인할 수 있습니다.

| 이름 | 담당 역할 | 저장소상의 구현 영역 |
|---|---|---|
| **정장영** | 기획 · QA(공통) / 리드 개발 · 아트 파이프라인 · AI 활용 | 프레젠테이션 코어, 텍스트 커맨드 에이전트, 자산 생성 파이프라인, WebGL 빌드·배포 |
| **이석민** | 기획 · QA(공통) / 로비·메타 진행 · 던전 확장 개발 | 로비/캠페인 진행, 던전 3종 확장, 시뮬레이션 파생 스탯, EditMode 테스트·QA 증거 |
| **정우영** | 기획 · QA | 난이도·드롭 설계, 게이트 판정 및 릴리스 검증 |

## 1.1 기여의 정량 근거

저장소 전체 브랜치를 기준으로 한 커밋 작성자 집계입니다.

```bash
git shortlog -sne --all
```

| 커밋 작성자 | 커밋 수 | 대응 팀원 |
|---|---|---|
| `akillness <akillness38@gmail.com>` | 288 | 정장영 |
| `lee <seokcmin@…>` | 35 | 이석민 |
| `akillness38 <…@users.noreply.github.com>` | 4 | 정장영 (GitHub 웹 UI 머지 커밋) |
| `supercent <akillness38@gmail.com>` | 2 | 정장영 (동일인 다른 로컬 설정) |

기본 브랜치 `main`은 246개 커밋을 유지하고 있으며, 그중 이석민의 작업은 fork
저장소(`leeseockmin`)에서 올라온 Pull Request로 병합되었습니다.

| PR | 제목 요약 | 상태 |
|---|---|---|
| #1 | 통합 스펙 A/B/C — 사양 결함 6건 수정 | 병합됨 |
| #3 | 훈련장 · 돌발 · 각인 서지 (+ 글로우 셰이더 meta 복구) | 병합됨 |
| #4 | 정보 탭 가독성 + 사이클 5·6 합류 (테스트 738/738) | 병합됨 |
| #5 | 로비 좌측 아이콘 레일 — 배포 빌드에서 도달 못 하던 패널 2개 | 병합됨 |
| #6 | 로비 스모크 재측정 — 폐기된 증거 2행 교체 | 검토 중 |

정우영은 별도의 커밋 작성자 계정을 사용하지 않았습니다. 기획 결정과 QA 판정은
설계 문서와 게이트 리뷰 기록을 통해 반영되었으며, 그 내용을 두 구현 담당이
커밋에 옮겼습니다. 이 문서는 커밋 이력으로 증명되는 것과 그렇지 않은 것을
구분해서 적습니다.

---

# 2. 팀원별 담당 영역

## 2.1 정장영 — 리드 개발 / 아트 파이프라인 / AI 활용

**기획 (공통)**

- 게임 콘셉트와 코어 루프 정의: 웨이브 방어 + 등불 기름 자원 배분
- 전투 수치 설계: 워든의 공격 사거리(160)를 적(76)보다 두 배 넘게 잡아, 거리를
  유지하며 치고 빠지는 실력이 곧 생존이 되도록 이동속도·재사용 대기시간을 조정

**프레젠테이션 코어 구현** — 변경 횟수가 가장 많은 영역입니다.

- `Assets/Scripts/View/GameView.cs`, `HudView.cs` (각 28회 수정) — 고정스텝
  시뮬레이션과 화면 사이의 단방향 경계, 화면 정보창(HUD) 전반
- `ActorView.cs`(21회) — 캐릭터 표시·애니메이션 상태 반영
- `VfxDirector.cs`(17회) · `PostFxGate.cs` — 원소 파티클, 스킬 시전 광량, 블룸·
  비네트. 성능 예산 안에서만 켜지도록 상한을 코드에 고정
- `CameraRig.cs`(14회) · `EnvironmentBuilder.cs`(13회) — 아이소메트릭 카메라
  프로파일과 스테이지 배경 배치
- `InputAdapter.cs`(8회) — 키보드·터치 입력을 프레임당 하나의 `SimInput`으로
  합치는 단일 지점. 아레나/프롤로그/던전 프로파일별 키 매핑을 여기서만 소유
- `AudioDirector.cs`(6회) — 이벤트 기반 효과음·배경음. 같은 소리가 겹쳐
  웅웅거리는 문제를 재생 채널 6개 순환 + 음높이 ±6% 흔들기로 해결
- `GameBootstrap.cs`, `ViewWorld.cs`, `WebGLStorage.cs`, `ViewPrefs.cs` —
  부팅 순서, 좌표 변환, 브라우저 저장소 브리지

**텍스트 커맨드 에이전트 구현** — 이 프로젝트의 AI 상호작용 표면 전체입니다.

- `CompanionCommandParser.cs` — 한국어 문장을 닫힌 명령 집합으로 분류하는
  오프라인 해석기. 구체적인 낱말("결계")이 넓은 낱말("방어")보다 먼저 잡히도록
  규칙 순서를 고정
- `CommandPlan.cs` — 한 문장을 **순서 있는 다단계 계획**으로 분해
  ("노바 쓰고 결계 쳐" → 노바 → 결계). 최대 6단계, 대기 0.1~10초로 제한
- `CommandQueue.cs` — 조건부 예약 ("셋 잡으면 노바"). 처치·웨이브 시작·보스
  등장·전리품·피격·추출 6가지 게임 이벤트를 발동 조건으로 지원
- `CommandAgent.cs`, `HudView.CommandAgent.cs` — 계획을 실제 게임 이벤트에 맞춰
  한 단계씩 집행. 모든 단계는 키보드가 세우는 것과 **동일한 입력 래치**로
  들어가므로, 자유 텍스트와 네트워크 지연이 시뮬레이션에 닿지 않습니다
- `GeminiCommandClient.cs` — 원격 해석은 플레이어가 자기 API 키를 등록했을
  때만 켜지는 선택 기능. 기본 동작은 전부 로컬입니다
- `CommandConsoleImeComposition.cs`, `Assets/Plugins/WebGL/hangul_ime.jslib` —
  브라우저 한글 IME 입력이 Unity 프레임 사이에 유실되던 문제 해결

**아트 자산 생성 파이프라인 구현**

- `tools/blender/reskin_character.py`, `reskin_all.sh` — 원작 모션 라이브러리
  메시를 mixamo 표준 휴머노이드 스켈레톤에 자동 웨이트로 재바인딩하고 FBX로
  내보내는 헤드리스 Blender 자동화
- `tools/blender/convert_terrain.py` — 원작 지형을 변환해
  `Assets/Resources/Terrain/`의 스테이지 공용 바닥·소품으로 재구성
- `tools/icons/gen_icons.sh`, `mat_icons.py` — god-tibo-imagen / PerfectPixel로
  UI 아이콘 생성 (`Assets/Resources/Icons/`, 103회 수정)
- `tools/audio/gen_sfx.py` — ElevenLabs 사운드 생성 API로 효과음 제작
  (`Assets/Resources/Audio/`)
- 색상 규칙 수립: 배경 제거 키 색과 충돌하지 않도록 마젠타 계열 전면 금지,
  아군은 시안으로 고정해 난전에서 즉시 구분되게 함
- 생성한 모든 자산의 프롬프트·소스·도구·해시를 `docs/provenance/`에 기록

**빌드 · 배포 · 문서 생산**

- `ProjectSettings/`, `Assets/Plugins/NuGet/`, `index.html`, `Build/` — Unity
  WebGL 빌드 구성과 GitHub Pages 산출물
- `tools/deploy/deploy_pages.sh` — 저장소에 커밋된 내용만으로 배포본이
  만들어지도록 하는 배포 스크립트
- `tools/video/capture-unity-play.mjs`, `tools/video/brand/` — 플레이 캡처와
  브랜드 범퍼(Remotion)
- `tools/docs/build-submission-pdf.mjs` — 제출 문서 PDF 변환 파이프라인

## 2.2 이석민 — 로비 · 메타 진행 / 던전 확장

**기획 (공통)**

- 온보딩 설계: 튜토리얼 없이 첫 화면에서 목표·조작·상태가 동시에 읽히도록
  정보 구조 정의
- 메타 진행 설계: 각인(사길) A/B 양면 선택, 훈련장 5종 시련, 돌발 서지
- 세계관 텍스트 — 진행에 따라 열리는 로어 비트(`StoryCatalog.cs`)

**로비 · 캠페인 진행 구현**

- `LobbyView.cs`(11회, 최다 수정 파일) — 출정·군단·장비·각인 탭, 좌측 아이콘
  레일. PR #5는 배포 빌드에서 실제로는 도달할 수 없던 패널 2개를 열었고, PR #4는
  지도 패널이 출정 패널을 덮던 결함을 배포 상태 전체에서 수정했습니다
- `CampaignStore.cs`(5회) — 세이브 스키마 v2→v6 확장(각인·훈련·안내 비트).
  모든 신규 키를 가산적으로 설계해, 이전 버전 세이브가 그대로 로드되도록 유지
- `StageCatalog.cs`, `LobbyStaging.cs` — 스테이지 카탈로그와 로비 연출 배치
- `ProgressionGuide.cs`, `GuidanceCatalog.cs`, `HudViewCodex.cs` (신규 작성) —
  진행 네비게이션, 인게임 코덱스, 안내 탭. "다음에 뭘 해야 하는지"를 화면 안에서
  답하는 계층
- `GameDirector.cs`(6회), `HudView.cs`(6회), `GameView.cs`(5회) —
  로비 ↔ 던전 라우팅 접합부

**던전 확장 구현**

- 스테이지 3종 신규 추가: 재의 수문(cinder-sluice) · 불씨 요새(ember-bastion) ·
  재의 행진(ash-march). 기믹 3종 동반
- 훈련장 · 돌발 · 각인 서지(PR #3) — 로비에서 바로 들어가는 반복 훈련 루프
- `VfxDirector.cs`(5회) — 스킬 실루엣 VFX 5종과 게이지 메시 계약

**시뮬레이션 구현**

- `CinderSim.cs`(12회) — 던전 확장에 필요한 결정론적 전투 규칙
- `CampaignTypes.cs`(6회), `HackTypes.cs`(3회) — 스테이지별 웨이브/보스 구성,
  동료 슬롯 정규화
- `DerivedStatSnapshot.cs` (신규 작성) — 화면이 시뮬레이션 상태를 되쓰지 않고
  **읽기만** 할 수 있도록 만든 파생 스탯 읽기 표면

**테스트 · QA 증거**

- EditMode 테스트 신규·확장: `LobbyLayoutTests.cs`(6회),
  `DungeonGoldenDigestTests.cs`(5회), `CampaignSimTests.cs`(5회),
  `StageCatalogTests.cs`, `TrainingSurgeTests.cs`, `SigilTests.cs`,
  `ProgressionNavigationTests.cs`, `SkillShapeVocabularyTests.cs`,
  `LobbyContainmentTests.cs`, `ViewColliderStripConventionTests.cs`
- QA 증거 56개 파일(`_workspace/current/qa/`) — 로비 레일 스모크, 안내 스모크,
  코덱스 가독성 스모크. PR #4는 테스트 738/738 통과와 변이 테스트 14/14를 함께
  제시했고, PR #6은 이미 폐기된 측정 2행을 재측정 결과로 교체하는 작업입니다
- 사양 문서 갱신: `docs/SIM_SPEC_HACKSLASH.md`, `docs/SIM_SPEC_DUNGEONS.md`,
  `docs/DUNGEON_GUIDE.md`

## 2.3 정우영 — 기획 / QA

**기획**

- 난이도 곡선 설계: 웨이브별 증원 규모·스폰 간격, 웨이브 간 정비 구간.
  스테이지별 웨이브 수(5→9)와 보스 웨이브 배치가 여기서 나왔습니다
- 드롭 설계: Ember shard(+18 체력) / Oil flask(+35 기름) / Relic mote(+250 점수)
  3종 순환, 수명 12초, 자력 반경 78

**QA (품질 검증)**

- 자동 테스트 게이트 판정 — `Assets/Tests/EditMode/`의 결정론 검증
  (`CinderSimTests.cs`, `CampaignSimTests.cs`, `HackSimTests.cs`), 명령 해석
  정확도(`CompanionCommandParserTests.cs`), 캐릭터 동작(`ClipTableTests.cs`,
  `PoseResolveTests.cs`), 화면 배치(`HudLayoutTests.cs`), 웹 빌드 텍스처 용량
  상한(`WebGlTextureCapTests.cs`)
- 경계 규칙 준수 검증: 화면·소리·효과 계층이 시뮬레이션 상태를 되쓰지 않는지
  확인 (같은 입력이면 항상 같은 결과가 나오게 하는 안전장치)
- 플레이 검증 — 일반 공격 · 스킬 단축키 · 텍스트 명령 세 가지 입력 경로가 모두
  실제로 동작하는지 배포본에서 확인
- 배포 점검: GitHub Pages 산출물이 저장소에 커밋된 내용만으로 재현되는지 확인

---

# 3. 협업 · 분업 방식

## 3.1 단일 운영 계약

세 명이 각자 다른 도구·세션에서 작업하되, 저장소 루트의 `CLAUDE.md`(및 이를
가리키는 `AGENTS.md`)를 **단일 운영 계약**으로 공유했습니다. 수치 계약, 엔진
제약, 자산별 고정 도구, Git 안전 규칙이 한 파일에 모여 있으므로 담당자별로 다른
관행이 생기지 않습니다.

## 3.2 Pull Request 기반 병렬 브랜치

이 저장소는 **fork + PR** 방식으로 굴러갔습니다.

- 정장영은 `akillness/main` 브랜치에서 작업한 뒤 `main`으로 올립니다.
- 이석민은 자신의 fork(`leeseockmin`)에서 `dungen` · `training-on-main` 브랜치를
  만들어 작업하고 PR로 제출합니다 (#1, #3, #4, #5 병합, #6 검토 중).
- 두 사람이 동시에 같은 파일을 건드리는 일이 실제로 자주 발생했고, 그때마다
  **되돌리는 대신 재정렬**했습니다. PR #3은 "main 위에 재정렬"이 제목에 들어가
  있고, PR #4는 충돌 24 hunk를 해소한 뒤 738/738 테스트로 결과를 증명했습니다.
- 충돌 사고 자체도 기록으로 남깁니다 — 공유 파일 충돌 2건과 그 해소 과정을
  별도 문서로 커밋했습니다.

## 3.3 레인 분리

작업 산출물은 `_workspace/current/` 아래 담당 레인에 배치합니다.

| 레인 | 소유 | 담는 것 |
|---|---|---|
| `_workspace/current/design/` | 3인 공동 | 기획·수치·서사 결정 |
| `_workspace/current/engineering/` | 정장영 | 구현·자산 파이프라인·빌드 로그 |
| `_workspace/current/qa/` | 이석민 · 정우영 | 검증 결과 |
| `_workspace/current/pm/` | 이석민 · 정우영 | 협상 기록 |
| `_workspace/current/production/` | 정우영 | 태스크 매니페스트 · 게이트 상태 |

**잘못된 레인에 놓인 파일은 사소한 실수가 아니라 결함으로 취급**합니다. 증거의
소유자가 불분명해지면 검증이 무너지기 때문입니다. 사이클이 끝나면 레인 전체를
`_workspace/archive/<사이클-id>/`로 옮기고 읽기 전용으로 둡니다.

## 3.4 증거 규약

모든 주장에 "직접 확인함 / 추론 / 목표치"를 구분해 표시합니다. 목표치를 이미
달성한 측정값처럼 적는 것을 금지하며, "파일이 있다"는 사실만으로는 근거로
인정하지 않습니다. 실제 측정값·실행한 명령·테스트 결과를 함께 제시해야 합니다.
PR #6은 이 규약을 스스로에게 적용한 사례입니다 — 이미 폐기된 증거 2행을 발견해
재측정으로 교체하는 것이 PR의 전체 내용입니다.

## 3.5 동시 작업 안전장치

여러 세션이 같은 저장소를 동시에 편집하므로 다음을 강제했습니다.

- 편집 전과 커밋 직전 `git status --short`를 확인하고, 예상치 못한 변경은 다른
  담당자의 작업으로 간주한다
- 명시적 경로만 스테이징한다. `git add -A` / `git add .` 금지
- 다른 담당자의 변경을 되돌리거나 덮어쓰지 않는다. 충돌 시 중단하고 기록한다
- 푸시 전 upstream을 명시적으로 페치하고 `@{upstream}..HEAD` 범위를 전부
  확인한다. force-push 금지
- 파괴적 자산 작업 전에는 `git tag -f pre-<작업>-<날짜>`로 복귀점을 남긴다
  (`backup/pre-expansion-20260805` 브랜치가 그 사례입니다)

## 3.6 실제 협업 사례 — 동료 명령 콘솔

게임 중 `Enter`로 열리는 텍스트 명령 콘솔은 세 담당의 작업이 한 지점에서
맞물리는 대표 사례입니다.

| 단계 | 담당 | 내용 |
|---|---|---|
| 명령 해석기 | 정장영 | `CompanionCommandParser.Parse`가 한국어 문장을 닫힌 명령 집합으로 분류. 구체적 낱말이 넓은 낱말보다 먼저 잡히도록 규칙 순서를 고정 |
| 계획·예약 | 정장영 | `CommandPlanParser.ParseLocal`이 문장을 순서 있는 다단계로 분해하고, `CommandTriggerParser`가 "셋 잡으면 …" 같은 조건을 게임 이벤트에 건다 |
| 입력 연결 | 정장영 | `HudView.SubmitCommand` → `InputAdapter` 래치로 이어져, 콘솔 명령이 키보드 입력과 **똑같은 경로**로 시뮬레이션에 전달된다 |
| 화면 피드백 | 이석민 | 안내 문구를 실제 동작에 맞춰 정직하게 표기 — 동료에게 내리는 명령과 플레이어 본인의 스킬 시전을 문구에서 구분. 대기열 패널이 남은 조건과 진행도를 함께 표시 |
| QA 검증 | 정우영 | `CompanionCommandParserTests.cs`로 문장이 의도한 명령으로 분류되는지, 알 수 없는 문장은 실행 없이 안내만 뜨는지 반복 검증 |

기본 동작은 인터넷 연결 없이 게임 안에서 처리되는 로컬 해석기입니다. 원격
Gemini는 플레이어가 자기 API 키를 직접 등록했을 때만 켜지는 선택 기능이므로,
**게임의 실제 진행은 어떤 경우에도 원격 AI 응답에 의존하지 않습니다.** 이
원칙을 QA가 검증합니다.

---

# 4. 기여 확인 방법

저장소의 변경 이력으로 담당별 기여를 직접 확인할 수 있습니다.

```bash
git shortlog -sne --all                       # 작성자별 커밋 수
git log --author=akillness --name-only        # 정장영이 건드린 파일
git log --author=seokcmin  --name-only        # 이석민이 건드린 파일
gh pr list --state merged                     # 병합된 Pull Request 목록
```

- 저장소: <https://github.com/akillness/hongT> (공개)
- 배포: <https://akillness.github.io/hongT/>
