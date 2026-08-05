# View Lane — cycle 2 dungeon expansion (run-id 20260805-dungeon-gimmicks)

작성: view-lane engineer. 근거: docs/SIM_SPEC_DUNGEONS.md (AMENDMENT #5),
design/dungeon-roster-spec.md, pm/negotiation-record.md entries 1-2.
상태: 코드 작성 완료, 미커밋 (git 안전 계약 — CLAUDE.md §5). Unity 미실행
(이 머신에 에디터 없음 — 컴파일/테스트는 다음 에디터 세션에서).

## Git safety

- 사전 `git status --short`: GameView.cs / GameDirector.cs에 타 세션 수정
  **없음** (View 디렉토리 clean). damage-number 충돌 없음.
- 사후: View 6파일만 M (아래 목록). 테스트 파일 M/신규는 TestLane 소유.
- 스테이징/커밋 안 함 — 전부 worktree 미커밋 상태로 남김.

## Per-file summary

### 1. StageCatalog.cs (+82/-4)
- StageEntry 3행 추가 (CatalogIndex 6/7/8): cinder-sluice(재의 수문,
  CINDER SLUICE, skill-dash, anchor cinder-sluice, override null, prereq
  ash-verdict, terrain abyss-chancel, accent #3FA8C8, Commander tint 1.2
  "Sluice Keeper", story cinder-sluice, companion null) · ember-bastion
  (불씨 요새, skill-ward, prereq cinder-sluice, terrain cinder-span,
  #E88A2E, Commander 1.22 "Bastion Sentinel", companion null) · ash-march
  (재의 행진, skill-strike, prereq ember-bastion, terrain echo-throne,
  #B8B0A4, Monarch 1.25 "Ash Magistrate", companion "scout-echo").
- `const ValidClearMask = 0x3F` → `internal static readonly int
  ValidClearMask = (1 << AllEntries.Length) - 1` (= 0x1FF). 파생식이므로
  이 버그 클래스(카탈로그 확장 시 마스크 절단) 재발 불가. 선언 순서 주석
  포함(AllEntries 뒤 — static 초기화는 선언 순). internal + 기존
  InternalsVisibleTo로 EditMode 테스트 접근 가능.
- DressingPlacement 테이블 3종 + DressingFor 케이스 3종. 전 배치가
  (a) 전투 평면(x 248..1288, y 334..874) 밖, (b) 해당 스테이지 **앵커**
  해저드 전부에서 radius+50 이상, (c) 기존 테이블이 이미 참조하는 라이브러리
  child 이름만 재사용(feature/prop only), (d) ash-march는 전부 SimX ≥ 658
  (벽 침식 밴드 x 248..608 시각 간섭 회피). 스크립트로 전 제약 기계 검증
  완료 (9/8/8 배치 모두 PASS).

### 2. CampaignStore.cs (+9/-3)
- Load/Save 양쪽 `& 0x3F` → `& StageCatalog.ValidClearMask` (단일 진실).
- "bits 0-5" 주석 → 0-8.
- 레거시 v0.1 cleared[] 경로: 원래 3개 id(비트 0/2/4)만 매핑 — 유지하고
  "v0.1엔 그 3개만 존재했으므로 확장 금지" 주석 추가.

### 3. StoryCatalog.cs (additive only — 기존 라인 무수정)
- 신규 화자 상수 3종: SLUICE KEEPER / BASTION SENTINEL / ASH MAGISTRATE
  (기존 영문 대문자 문법). **주의**: SpeechBubbleView.SpeakerColor는
  CINDER/VEIL/GATE/DUSK prefix만 인식 → 신규 보스는 당분간 watcher 색으로
  렌더됨. SpeechBubbleView는 이 레인 타깃 외(6파일 제한)라 미수정 — 상수
  주석에 명시. 후속: EmberColor 분기에 SLUICE/BASTION/ASH prefix 추가 1줄.
- 12비트(스테이지당 4) 추가. 대사는 dungeon-roster-spec §비트 표에서
  verbatim — 표 형식 "화자: 대사"에서 화자는 speaker 필드로, 콜론 뒤
  문장만 text로 분리(기존 엔트리와 동일한 분리 규약). stageStart=감시자,
  bossEntry/Phase2=보스 화자, completion=DUSK WARDEN.

### 4. VfxDirector.cs (+376/-2)
- HazardView 확장: Body/Aux/Edge (+각 Material), PushSign, Down. 슬롯은
  kind별 disjoint(기존 Ring이 vent/altar 겸용인 문법 그대로).
- **TideCurrent**: 평면 밴드 쿼드 1040×140 sim(축정렬 — 유일한 사각 판정,
  /IsoY 스쿼시 안 함) + 장변 에지라인 2개(재질 1개 공유) + 셰브론 8개
  (쿼드 2장/개, 재질 1개 공유). 텔레그래프=에지 블링크, 활성=베드 밝아짐+
  셰브론 스크롤(CurrentPush×Scale 속도, ChevronSpacing 모듈러 랩 —
  localPosition만 이동, 재할당 없음). 셰브론 행은 스크롤 전 구간에서 판정
  밴드 **내부** 유지(장식이 판정을 넓혀 읽히지 않게). -x 흐름은 컨테이너
  yaw 180°. ReducedMotion: 정적 셰브론, 스크롤/블링크 없음(마커 상시 유지,
  펄스만 차단 — 계약 준수). 방향(PushX)은 HazardState 미발행 → 빌드 시
  1회 CampaignStages 앵커 테이블 위치 매칭(CurrentPushSign 헬퍼, 순수/
  결정론, per-frame 비용 0).
- **EmberPylon**: 흑요석 원기둥 몸통(필러보다 낮음, r30) + 몸통 자식
  ember-orange 발광 밴드(Hp 240..0 비율로 알파 dim) + 오라 디스크 r220
  (iso 타원, 상시 저알파 — 존 마커라 펄스 없음) + 숨겨둔 스코치 링.
  파괴: OnEvents(SimEvents.PylonDown) 1차 경로 — 이벤트에 파일런 식별자가
  없어 ICampaignSnapshot.Hazards에서 Hp≤0 && !Down 파일런 탐색 →
  TearDownPylon(pooled burst + _novaDebris.Emit + 몸통/오라 off + 스코치
  on). SyncHazards의 Hp-edge가 idempotent 폴백(vent wrap #17과 같은 패턴,
  Down 플래그로 이중발화 방지).
- **AshWall**: x=248 고정 경계라인(텔레그래프 블링크 면) + 어두운 오버레이
  쿼드(x 248..FrontX, 매 프레임 스케일) + FrontX 추종 수직 커튼 시트
  (yaw −90 — 남쪽 카메라 기준 노멀 +x, +90이면 backface-cull). 신규
  파티클 시스템/라이트 0(§V3 예산). ReducedMotion: 경계라인만 —
  라인이 FrontX 추종해 전선 마커 역할(오버레이/커튼 숨김).
- 전 재질 빌드 1회 생성(MakeUnlit 시드 계약), per-frame은 color/
  localPosition/localScale 변이만 — 신규 할당 0. ClearTransient가 루트
  파괴로 런 간 정리(기존 경로 그대로).

### 5. LobbyView.cs (+37/-3)
- 스테이지 리스트 **스크롤** 선택 (피치 압축 기각: 9장을 고정 패널에
  넣으려면 카드 68u 미만 피치 필요 → 겹침 or 강하 버튼 축소로 44 CSS px
  터치 플로어(HudLayoutTests 계약) 침해. 스크롤은 감사된 카드/버튼 치수
  전부 보존).
- 프롤로그 카드 아래 RectMask2D+ScrollRect 뷰포트(패널 −174..−12,
  높이 434u), 콘텐츠 9×70+8=638u, Clamped, 수직 전용. 뷰포트 Image는
  투명+raycastTarget=true — ScrollRect 드래그 면이므로 상호작용 요소
  (HudLayoutTests (c)의 "invisible rects eat taps" 위반 아님; 버튼 클릭은
  드래그 임계 미만이면 정상 통과). 카드 생성 루프는 좌표만 콘텐츠 기준
  (−6 − i·70)으로 변경, 문법/치수 무변경. 배열들은 원래 Entries.Count
  기반이라 9로 자동 확장.

### 6. GameDirector.cs (+21)
- PersistDungeonClear: `if (firstClear) _data.Relics +=
  FirstClearRelicBonus(entry.Id)` — sluice +6 / bastion +8 / march +10,
  기존 6스테이지 0 (negotiation-record entry 1 서명 수치 그대로).
  저장은 기존 CampaignStore.Save 경로 통과(스키마 불변).
- scout-echo 동료는 기존 CompanionReward 경로가 그대로 처리(entry 2) —
  코드 추가 불필요, StageEntry 데이터만으로 동작.

## 신규 한글 글리프 (폰트 서브셋 영향)

렌더 문자열 리터럴 기준(주석 제외), HEAD 대비 View 어셈블리 신규 15자:

    걸 든 떠 뚫 랐 러 멈 멎 벽 새 숨 춘 형 흐 흘

- StageCatalog.cs: `새` (재의 **수문**/불씨 요**새**/재의 행진 중 "새")
- StoryCatalog.cs: 나머지 14자 (신규 12비트 대사)
- LobbyView.cs: 신규 글리프 0 (카탈로그 데이터만 표시)

→ Resources/Fonts/HudKorean 서브셋에 15자 미포함 시 로비 카드 제목(새)과
스피치 버블(나머지)이 빈 글리프로 렌더. **로비 폰트 테스트
(LobbyMotionLabels_UseGlyphsPresentInShippedHudKoreanFont 류) 확장 +
서브셋 재생성 필요 여부를 에디터 세션에서 확인할 것.**

TestLane 교차 검증 (qa/test-lane-cycle2.md): PRE-cycle-2 기준 사이클 전체
신규 글리프는 28자(위 15자의 초집합 — 나머지 13자 꺼락록름막방살역위죽집패허는
HEAD에 이미 들어간 앞선 cycle-2 문자열 소산). 로비 폰트 테스트는 모션
라벨만 게이트하므로 StoryCatalog 전수 글리프 테스트가 올바른 가드.

## Deviations from assignment spec (사유 포함)

1. **PylonDown one-shot 이중 경로**: 과제는 "on PylonDown event (OnEvents)
   spawn burst"만 명시. 이벤트에 파일런 식별자가 없어(비트 1개) OnEvents는
   스냅샷 Hazards에서 Hp≤0 && !Down 탐색으로 구현하고, SyncHazards Hp-edge를
   idempotent 폴백으로 유지(둘 다 TearDownPylon 경유, Down 플래그 1회 보장).
   이벤트 프레임 드랍 시에도 상태 일관.
2. **TideCurrent "scrolling-UV"**: UV 스크롤은 텍스처 없는 MakeUnlit 시드
   재질에서 무의미(단색) → 과제가 병기한 대안인 셰브론 행 + 컨테이너
   localPosition 스크롤로 구현(시각 동등, 할당 0, WebGL 시드 계약 유지).
3. **AshWall "particle-curtain"**: 신규 ParticleSystem 없이(§V3 예산·
   no-new-lights 계약) 반투명 수직 시트 쿼드로 커튼 구현. 기존 4개 풀
   시스템은 재사용 대상이 아님(전부 이벤트 버스트용).
4. **ValidClearMask 접근성**: const → internal static readonly. const는
   파생식 불가(AllEntries.Length는 런타임 값). internal은 CampaignStore
   단일-진실 참조 + 기존 InternalsVisibleTo(EditMode 테스트) 때문.
5. **최종 스테이지 배너 이동(파생 효과)**: GameView는 "마지막 카탈로그
   인덱스 클리어 시 ShowStageClear"를 `Entries.Count - 1`로 판정 —
   카탈로그가 9로 늘며 이 배너가 ash-verdict → ash-march로 자동 이동.
   의도된 체인 연장으로 판단(코드 미수정, 데이터 파생).

## 검증 (에디터 없이 수행한 것)

- 6파일 구분자 균형 검사(문자열/주석/보간 인식 스캐너): 전부 OK.
- 드레싱 제약 기계 검증: 25배치 전부 평면 밖 + 앵커 해저드 clearance +
  기존 child 이름만 + ash-march x≥658 — PASS.
- 글리프 diff: git HEAD 리터럴 vs 워크트리 리터럴 (위 15자).
- 카메라 기하 검증: Dungeon 프로파일 pitch 55/yaw 0(남쪽) 기준 커튼 노멀
  +x 확인(−90 yaw), 에지/베드/셰브론은 +y 노멀(기존 scorch와 동일 문법).
- 컴파일/EditMode 테스트/빌드: **미수행** (계약 — 이 머신에 Unity 없음).
  다음 에디터 세션 체크리스트: ① 컴파일 ② StageCatalog/Dressing/HudLayout
  테스트 ③ HudKorean 글리프 15자 커버리지 ④ WebGL 커튼/밴드 렌더 확인.
