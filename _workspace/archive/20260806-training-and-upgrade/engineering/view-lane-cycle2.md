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

## v1.1 retune pass

작성: view-lane engineer (gimmick readability pass, run-id cycle-2 stage-2
retune). 근거: docs/SIM_SPEC_DUNGEONS.md v1.1 REVISION,
design/gimmick-retune-spec.md, qa/benchmark-notes band 6("contrast is the
failure mode"). 상태: 코드 작성 완료, 미커밋, Unity 미실행(메인 레인이
게이트 실행). 사전 `git status --short`: View 3파일 전부 clean(타 세션
수정 없음 — .vscode/slnx/manifest는 타 세션 소유라 미접촉).

### 1. VfxDirector — AshWall edge-aware (좌/우 벽 2슬롯)

- **빌드**: side는 HazardState.X 앵커로 추론(`fromRight = X > (248+1288)/2`
  — HazardState는 PushX 미발행, CurrentPushSign과 같은 빌드-시 앵커 문법.
  등호 비교 대신 중점 비교: float 안전 + 미래 오버라이드 내성). 루트는
  이미 hazard.X 기준이라 우측 벽 루트는 자동으로 x=1288.
- **커튼 yaw**: 좌 −90(노멀 +x) 유지, 우 +90(노멀 −x). 백페이스 검증:
  쿼드 노멀 −z에 R_y(+90) 적용 → (−1,0,0). Dungeon 카메라는 yaw 0 남쪽
  pitch-down이고 x는 플레이어 추종 — 벽은 플레이어를 항상 자기 홈 에지의
  **아레나 쪽**으로 밀어내므로(WallPush 부호) 카메라 x는 양쪽 커튼의
  front-face 쪽에 유지됨.
- **sync 일반화**: `frontWorld = (FrontX − hazard.X) × Scale` 부호 있는
  오프셋 하나로 양쪽 처리(좌 +성장, 우 −성장). 오버레이 스케일
  `|frontWorld|`, 중심 `frontWorld/2`, 커튼/RM 에지라인 `frontWorld` —
  분기 없음. **live 판정을 `FrontX > WallEdgeX`에서 `hazard.Active`로
  교체**: 구 판정은 우측 벽에서 상시 참(idle FrontX 1288 > 248) →
  오버레이가 휴지기에도 1040px 전체를 덮는 치명 오독. Active는 sim이
  발행하는 depth>0 그대로(§Gimmick 3 v1.1).
- **2슬롯 독립성 검증**: ash-march는 Wall(0)+Wall(11.5,right) 2개 —
  _hazardViews는 인덱스별 struct, 재질은 MakeUnlit이 호출마다 `new
  Material`(ViewWorld.cs 확인, 캐시 없음), GameObject도 빌드마다 신규.
  공유 상태 없음. per-frame 신규 할당 0 유지(color/localPos/localScale
  변이만).

### 2. VfxDirector — 가독성 리튠 (band 6 대비 실패 모드)

- **벽 경계라인**: 회백(0.85,0.80,0.75)→**ember-orange(1,0.55,0.18)**,
  알파 idle 0.22→0.30 / live 0.8→0.9 / blink 바닥 0.25→0.30. 텔레그래프
  블링크는 기존대로 경계라인 담당(RM이면 정적 — 계약 유지).
- **벽 오버레이**: 순수 흑(0.05,0.04,0.04,0.45)→**warm-charcoal
  (0.10,0.06,0.05,0.62)** — ash-grey 바닥 위 "dark-on-grey" 금지 요건.
- **벽 커튼**: 회백 시트→**ember glow(1,0.45,0.15,0.55)** — 전진 전선이
  킬 리드이므로 가장 밝은 요소로 승격.
- **조류 밴드(terrain-read 요건)**: 베드 알파 idle 0.10→0.22 / active
  0.30→0.45, 에지 idle 0.25→0.35(blink 바닥 동반 상승), 셰브론
  (0.62,0.93,1,0.35)→**near-white(0.85,0.97,1)** + 알파 idle 0.35→0.55 /
  active 0.85→1.0. 활성 시 더 밝음+스크롤, RM 정적 — 기존 상태 문법
  그대로, 대비만 상향. 빌드 초기 알파도 idle 값과 일치시켜 첫 프레임 팝
  제거.

### 3. GameView/ActorView — 파일런 실드 시안 틴트 (R2 "−60% must be VISIBLE")

- GameView.SyncViews: 적 루프에서 `CoveredByLivePylon(hazards, x, y)` —
  sim 판정(CinderSim.EnemyDamageTakenMult: Hp>0 && IsoWithin ≤
  PylonAuraRadius) **문자 그대로 미러**(iso-가중 거리², 상수 참조라 280
  자동 추종). Publish가 이미 할당한 IReadOnlyList 순회, 비파일런 kind는
  O(1) skip, 아레나 경로는 hazards 0개 — per-frame 할당 0.
- ActorView: `SetShieldTint(bool)` + MPB 래더 신규 rung — **우선순위
  flash > shield cyan > elite gold > rank glow**. 실드가 elite를 이기는
  근거: −60%는 라이브 전술 사실(지금 때려도 되나), elite는 영구 마커라
  커버리지 종료 프레임에 즉시 복귀. ShieldCyan(0.45,1,1.15) — B>1로
  additive 근사(rim은 신규 재질 필요 → MPB BaseColor 단일 재질 문법
  준수). 정적 틴트라 RM-safe by construction(펄스 없음).
- 마지막 커버 파일런 사망 → 다음 프레임 판정 false → `_shieldApplied`
  falling-edge 래치가 블록 1회 복원(MPB는 덮어쓸 때까지 잔존하므로 래치
  없이는 시안 고착). ResetForPool에 두 플래그 초기화 추가(풀 오염 방지).
- ApplyBossPresentation: `FlashLive || ShieldLive` yield — 카탈로그 보스
  틴트가 SyncEnemy 뒤에 매 프레임 재도장이라 가드 없으면 보스(오라로
  유도할 1순위 타깃)만 실드 리드가 안 보이는 구멍.

### 4. 파일런 오라 반경 sanity check

- 코드 경로는 이미 `CampaignSpec.PylonAuraRadius` 상수 참조(하드코드
  없음) → 280 자동 반영. **주석만 stale**(“radius 220”, “Hp 240..0”) —
  상수명 기준으로 갱신.

### Deviations

1. **ActorView.cs 수정(타깃 2파일 외)**: 과제 명시 tint 문법("elite gold
   pulse 적용 방식과 동일")의 실체가 ActorView MPB 래더. GameView 단독
   구현(보스 프레젠테이션 방식 렌더러 캐시 복제)은 SyncEnemy 내부 플래시
   경로와 같은-프레임 쓰기 순서 경합 + Renderer[] 캐시 중복을 낳음 —
   래더에 rung 추가가 유일하게 경합 없는 지점. ActorView는 non-goal
   목록(sim/StageCatalog/StoryCatalog/LobbyView/GameDirector/tests)에
   없음.
2. **벽 live 판정 교체**(FrontX>WallEdgeX → hazard.Active): 과제 지시
   범위(“edge-aware로”)의 필연 귀결 — 구 판정은 우측 벽에서 의미 자체가
   깨짐(위 §1). sim 발행 필드만 사용, sim 미수정.
3. **실드 "rim" 미구현**: 지시문이 "additive cyan rim/tint, NO new
   materials"— rim 패스는 신규 재질/셰이더 없이는 불가(단일 재질 MPB
   경로). tint로 구현하고 B 채널 >1로 additive 인상 근사.

### 검증 (에디터 없이)

- 3파일 구분자 균형(문자열/주석 인식 스캐너): VfxDirector 119{}/631()/74[]
  · GameView 67/316/30 · ActorView 51/235/23 — 전부 균형.
- **Roslyn 구문 파스 게이트**(dotnet 8 번들 Microsoft.CodeAnalysis.CSharp,
  LanguageVersion.CSharp9): 3파일 전부 SYNTAX OK, 진단 에러 0.
- 커튼 백페이스 기하: R_y(−90)·(0,0,−1)=(+1,0,0) 좌 / R_y(+90)·(0,0,−1)=
  (−1,0,0) 우 — 카메라 x(플레이어 추종, 벽이 아레나 쪽으로 push) 기준
  양쪽 front-face 확인.
- View 전역 grep: WallEdgeX/FrontX/AshWall 잔존 좌측-전제 코드 0건.
- 컴파일/EditMode/플레이 확인: **미수행** — 메인 레인 게이트 소유.
  에디터 체크리스트: ① ash-march 우측 벽 커튼 가시성(23s 주기 중 11.5s
  오프셋 창) ② bastion 오라 진입/이탈 시 시안 on/off + 파일런 파괴 프레임
  드롭 ③ RM 토글 시 벽 경계라인만 + 셰브론 정적 ④ elite(1.35 스케일)가
  오라 안에서 시안으로 전환되는지.

## v1.2 fun pass

작성: view-lane engineer (stage identity data + lobby epithets, run-id
cycle-2 fun-pass). 근거: design/campaign-fun-pass-spec.md +
docs/SIM_SPEC_DUNGEONS.md REVISION v1.2. 상태: 코드 작성 완료, 미커밋,
Unity 미실행(메인 레인이 게이트 실행). 사전 `git status --short`: View
파일 clean(타 세션 M은 .vscode/slnx/Packages/wasm/Sim/docs — 전부 미접촉).

### 1. StageCatalog.cs — v1.2 해저드 테이블 + Epithet

- **테이블 3종 교체 + 1종 신설** (스펙 §세부 배치 verbatim, 기계 대조 PASS):
  - EmberGalleryHazards: vent 4기 시계방향 위상 링(560/980×480/720,
    0/0.6/1.2/1.8s) + 중앙 pillar — "불씨 윤무" vent 숙달.
  - WitnessWellHazards: altar 대각쌍(560,500/980,700) + 중앙 pillar +
    제단 옆 vent 2기(위상 0.3/1.5) — "쌍 제단" altar 도입.
  - **EchoThroneHazards(신설)**: anchor 3기(altar 768,604 + vent 500,700/
    1030,480) + 약류 Current(768,604,+120,0.3) — "왕좌의 조류" current
    예고. entry 4의 HazardOverride null → EchoThroneHazards 연결
    (GameDirector.StartDungeon이 non-null 오버라이드를 이미 적용 — 코드
    무수정, 데이터만).
  - AshVerdictHazards: altar(768,604) + **pylon(960,540)** + vent 2기 —
    "판결의 방벽" pylon 예고.
- **StageEntry.Epithet 신설**(뷰 소유 struct): readonly string, ctor 15번째
  (마지막) 인자. 9개 생성자 호출 전부 갱신 — 별칭은 스펙 §9스테이지 아크
  "정체성(카드 표기)" 열 그대로: 분출구 입문 / 불씨 윤무 / 흑요석 미로 /
  쌍 제단 / 왕좌의 조류 / 판결의 방벽 / 해류 숙달 / 방벽 숙달 / 집행 수렴.
- 드레싱 블록 주석의 anchor 내용 서술이 pre-v1.1 stale(구 ash-march
  altar 1100,604 등) → v1.2 실제 테이블로 갱신(주석만, 배치 무변경).

### 2. LobbyView.cs — 카드 별칭 라인

- **선택: 신규 행 대신 보상 라인 병합** — `"{Epithet} • 보상: {reward}"`
  (기존 34,-44 220×16 10pt Gold 라벨, • 구분자는 프롤로그 서브라인에서
  기존 사용). 사유: 68u 카드에서 보상 밴드(-44..-60)와 강하 버튼(하단
  6..34) 사이 수직 여유 0 — 신규 행은 피치 증가(스크롤 콘텐츠 재계산) 또는
  44px 터치 플로어 침해. 스펙 문구 자체가 "기존 보상 라인 문법에 기믹 별칭
  추가"라 병합이 스펙 직역. (12,-44)의 해저드 글리프 아이콘이 라벨 바로
  왼쪽 — 별칭의 기믹 마커 겸용(과제의 "기믹 글리프 재사용").
- 최장 문자열 "분출구 입문 • 보상: 불씨 초계병" ≈ 19자@10pt ≈ 180px
  < 220px rect (버튼 좌단 x272보다 안쪽). 카드 높이/피치/스크롤 콘텐츠
  높이/터치 타깃 전부 무변경.

### 3. worldview.md — 기믹 계보 (G1)

- '## 기믹 계보 (v1.2)' 신설: 9행 표(스테이지 → 별칭 → 법정 기능 의미).
  별칭 전부 법정 기능의 물화로 서술(수문=말소, 방벽주=위증 보호, 장벽=집행
  — 기존 명명 규약 조항 어휘). 카드 문자열의 유일 출처 선언 + 추적성 조항.

### 드레싱 클리어런스 재검증 (기계, Euclidean vs radius+50 — 테스트 동일 메트릭)

변경 스테이지 3 + anchor 스테이지 3 전 배치 × 신 해저드 전수. **위반 0 —
배치 이동 불필요.** 스테이지별 worst-case 마진(가장 타이트한 배치↔해저드 쌍):

| 스테이지 | worst 배치 | vs 해저드 | dist | 문턱 | 마진 |
|---|---|---|---|---|---|
| ember-gallery | prop-003(620,950) | Vent(560,720) | 237.7 | 140 | **+97.7** |
| witness-well | prop-012(1040,250) | Vent(980,500) | 257.1 | 140 | **+117.1** |
| ash-verdict | prop-021(990,940) | Vent(980,720) | 220.2 | 140 | **+80.2** |
| cinder-sluice | feature-011(190,700) | Vent(500,604) | 324.5 | 140 | +184.5 |
| ember-bastion | feature-021(770,215) | Pylon(768,430) | 215.0 | 80 | +135.0 |
| ash-march | feature-002(900,215) | Vent(980,450) | 248.2 | 140 | +108.2 |

- ash-march 신규 pylon(768,520): 최근접 배치 feature-001(700,230) dist
  ≈298 ≫ 80 — v1.2 anchor 추가분도 클리어.
- echo-throne: DressingFor 스위치에 케이스 없음 → null 확인(드레싱 없음,
  T-b split 대기 주석 그대로) — 오버라이드 신설과 무관하게 클리어런스
  검증 대상 아님.
- StageDressingTests.HazardsFor는 오버라이드 우선이라 테스트도 자동으로
  신 테이블 기준 검증(테스트 수정 불요 — 이 계약 확인함).

### 신규 한글 글리프 (폰트 재생성 필요 — 메인 레인)

View 어셈블리 렌더 문자열 리터럴 diff(HEAD 대비, 주석 제외 스캐너):

    렴 숙 쌍 윤 흑  (5자)

shipped Assets/Resources/Fonts/HudKorean.otf cmap(456 codepoints) 대조:
**5자 전부 미포함** — tools/gen_hud_font.sh 재생성 없이는 로비 카드
별칭이 tofu. (별칭의 나머지 문자는 전부 기존 리터럴/폰트에 존재.)

### TestLane 인계 (IRC 통지함)

- StageEntry ctor 15인자(epithet 추가) — 직접 생성하는 테스트는 인자 추가.
- echo-throne: HazardOverride null → non-null. StageCatalogTests
  §NewStageAnchors(6/7/8만 순회)는 무영향, §전수 루프의 "override null이면
  Id==SimAnchorId" 단언도 무영향(역방향 함의 아님).
- **AssertCompositeHazards 페어와이즈 주의**: echo-throne v2는 Altar(768,604)
  r70과 Current(768,604) r0가 동일 좌표 — dist 0 > 70 단언은 실패.
  밴드 해저드(current/wall)는 §NewStageAnchors처럼 radial 페어에서 제외
  필요(스펙 의도: 제단이 조류 안 = 타이밍 퍼즐).
- 골든: 1/3/4/5 행 이동 예상(+8 ash-march는 심 앵커發), 0/2/6/7 불변 필수.

### 검증 (에디터 없이)

- Roslyn 구문 파스(SDK 8.0.129 bincore, LanguageVersion.CSharp9):
  StageCatalog.cs / LobbyView.cs 모두 SYNTAX OK, 에러 0.
- ctor arity 기계 검사: StageEntry 15 파라미터, 9개 호출 전부 15인자.
- 테이블 내용 기계 대조: 4테이블 × 스펙 §세부 배치 — 전부 MATCH
  (팩토리/좌표/위상/푸시 verbatim), echo-throne 연결 확인, 별칭 9종
  마지막 인자 확인.
- 컴파일/EditMode/플레이: **미수행** — 메인 레인 게이트 소유. 에디터
  체크리스트: ① 컴파일 ② StageCatalog/StageDressing/골든 테스트
  ③ gen_hud_font.sh 재생성 후 별칭 5글리프 렌더 ④ 로비 카드 보상 라인
  길이 육안(최장 cinder-span 행).

## v1.3 meta pass

작성: view-lane engineer (meta tabs + verdict pact, run-id cycle-2 stage-2c
meta-fun-pass). 근거: design/meta-fun-pass-spec.md M1-M4 +
pm/negotiation-record.md entry 5(서명) + worldview.md '메타 서사 (v1.3)'
(이 패스에서 신설). 상태: 코드 작성 완료, 미커밋, Unity 미실행(메인 레인
게이트). 사전 `git status --short`: View 3파일 clean(타 세션 M은
.vscode/slnx/Packages/wasm/negotiation-record — 전부 미접촉).

### 1. StageCatalog.cs — M3a 서약 테이블 9종 + PactFor

- `PactFor(stageId)` 공개: 9개 카탈로그 id 전부 non-null. 계약(MetaTest와
  IRC 합의): pact[0..base.Length-1] == 유효 베이스(HazardOverride ??
  프로즌 앵커, `AnchorHazards`가 CampaignStages.TryGet(0,0,0)로 조회) 원소
  동일·동순서, 추가분은 꼬리에 엄격 append(`Pact(base, extras)` 헬퍼 —
  Array.Copy 2회, 정적 초기화 1회 실행이라 할당 예산 무관).
- 추가 배치(스테이지 정체성 기믹, 신규 종류 0): span Vent(768,604,0.6) ·
  gallery Pillar(768,468)+(768,740) · chancel Pillar(900,500) · well
  Vent(560,500,0.9) · throne Current(768,740,−120,3.3) · verdict
  Pylon(576,668) · sluice Vent(768,604,1.7) · bastion Pylon(620,720) ·
  march Vent(768,796,1.2).
- **텔레그래프 예산 산술 (전 테이블 코드 주석 병기 + 기계 스윕 검증)**.
  창 공식: vent tel (t+ph)mod2.4∈[1.6,2.4) · current tel mod6∈[0,0.8) ·
  wall tel mod23∈[4.5,6.0). 스테이지별 최대치(0.05s 스윕, LCM 전구간):
  | 스테이지 | LCM | max 동시 | max 동종 | 근거 요약 |
  |---|---|---|---|---|
  | span | 2.4s | 2 | 2v | 추가 0.6 창 [1.0,1.8)이 베이스 두 창과 각각 겹치나 셋이 동시엔 불가(베이스 쌍 서로소) |
  | gallery | 2.4s | 2 | 2v | 추가분 pillar(무텔레그래프) — 베이스 링 그대로 |
  | chancel | 2.4s | 1 | 1 | vent 1기 |
  | well | 2.4s | 2 | 2v | 0.9 창 [0.7,1.5): 0.3창과 [1.3,1.5), 1.5창과 [0.7,0.9) — 베이스 쌍 서로소라 3중 불가 |
  | throne | 12s | 2 | 1 | vent 쌍 서로소, current 쌍 서로소([5.7,6)∪[0,0.5) vs [2.7,3.5)) → 이종 2가 상한 |
  | verdict | 2.4s | 1 | 1 | pylon 무텔레그래프 |
  | sluice | 12s | 3 | 2v | vent 3기 중 (2.1,1.7)만 겹침 [2.3,2.4)∪[0,0.3), 0.9는 1.7과 서로소 → 2v 상한; +current 1 → 3 (t∈[0,0.3),[9.5,9.8)) |
  | bastion | 2.4s | 1 | 1 | vent 1기 |
  | march | 276s | 3 | 2v | vent 쌍존 [1.0,1.2)/[0.4,0.6), (0.6,1.8) 서로소 → 2v; wall 쌍 서로소 → +1w = 3 |
  전부 ≤3 동시 / ≤2 동종 — PASS (MetaTest 독립 미러 센서스와 최대치 일치
  확인, IRC).
- **gallery 스펙 이탈(스케치 "+2 ring vents 0.3/1.5" 기각) — 산술 증명**:
  베이스 4기 링의 텔레그래프 창(0.6s 간격 4개, 각 0.8s)은 2.4s 주기 전체를
  0.6s마다 폭 0.2s의 2중첩 구간([0.4,0.6) [1.0,1.2) [1.6,1.8) [2.2,2.4))
  으로 타일링 — 어떤 위상의 추가 vent 창(0.8s = mod 0.6 완전 잔여계)도
  2중첩 구간과 반드시 교차 → 동종 3 위반이 **위상 무관 필연**. 과제의
  "위반 시 위상 조정" 조항으로 kind 자체를 스테이지 2차 기믹(pillar)로
  전환: 중앙 기둥과 3×3 격자를 완성해 윤무 회랑을 좁힌다(정체성 유지).
- **v1.2 클리어런스 계약 기계 재검증** (MetaTest 교차 확인 4건 포함):
  gallery pact pillar 간격 — 중앙 기둥과 136/136 ≥132(보행성 규칙), 쌍
  272; 초안 (768,480)/(768,720)은 124/116 위반이라 이동. march vent —
  초안 (768,880)이 prop-010(760,940) d=60.5 <140 드레싱 침해 → (768,796)
  이동: prop-010 d=144.2 ≥140, altar d=192 ≥160, 평면 내(y706..886 남측
  보행대 거부 밴드). **서약 예외 2건(문서화)**: well vent↔altar d=0,
  sluice vent↔pillar d=0 — 의도적 동좌표(제단 채널이 리듬을 직접 타게 /
  기둥 커버 뒤 40..90 환형이 물리는 것 자체가 서약의 bite). 예외 범위는
  PACT-EXTRA vent↔altar/pillar 한정, 베이스 테이블 불변.
- throne pact current(768,740): 좌표를 sluice 앵커 current와 일치시켜
  VfxDirector.CurrentPushSign(빌드 시 앵커 조회)이 −1을 해석 — −120
  역류가 뷰에서 올바른 방향으로 렌더(뷰 코드 무수정으로 방향 해결).

### 2. LobbyView.cs — M1/M2/M3b/M4

- **M1 성장 탭**: 파생 실수치는 **심의 자체 프로퍼티**로 표시 — 스택
  HackConfig 프로브(`Probe(in data, +a,+v,+s)`: MetaStats.Of/EquipTiers.Of
  채운 inert 구조체)에서 PlayerDamage/PlayerMaxHealth/PlayerSpeed 읽기.
  뷰는 공식을 재조립하지 않음(0.03/0.06 등 조합 상수 미보유) → 미러
  드리프트 구조적 불가(Main 레인 지시 정정 반영; MetaTest 미러 가드
  테스트의 필수 4프로퍼티/금지 5리터럴 스캔 자체 검증 PASS). 다음 포인트
  델타 = probe(x+1)−probe(x) (프로퍼티가 내부 클램프하므로 캡에서 0 —
  UI는 캡에서 델타 대신 "숙련" 표기). 행 문구: "공격력 75.4 (+2.2)" /
  "최대 체력 180 (+8)" / "이동 262 (+4.4)"; 캡 "… • 숙련". 정적
  "+3%/pt" 행이 이 라이브 행으로 대체(효과 요약은 하단 힌트에 존치).
  장비 기여 자동 합성(프로브가 EquipTiers 동반) — 성장+장비가 한 수치로
  보임(스펙 M1). 하단 요약(-300, Gold): 3수치 나열만, 총점 지표 없음.
- **M2 장비 탭**: `EquipTierNames`(internal string[3][6], 법정 어휘 —
  worldview '메타 서사' 표가 유일 출처): 잿날→담금날→벼림날→선고날→
  심판날→판결인 / 잿등→밀랍등→서약등→기록등→증언등→진실등 / 잿천→무명포→
  증인포→기록포→선고포→집행포. 과제 스케치의 판독불가 자리(쟿/릷)는
  글리프 경제로 조정: 잿- 계열 통일(기존 글리프), 랜턴 T0 香유→잿등
  (한자 배제 — 폰트/G1), 망토 T1 재릷전→무명포(무명=이름 없는 자 —
  신분 상승 서사 시작점). 랭크 행: "판결인 • 공격 +30%" — 퍼센트는 단일
  프로즌 상수 × 랭크(CampaignSpec.WeaponDamagePerRank 등 직독, 조합
  아님; 유일한 뷰측 상수 접촉이며 금지 리터럴 스캔과 무충돌... 스캔은
  리터럴 재철자만 금지, 상수 참조는 허용). 구매 버튼: "유물 7 → 공격
  +4.5" — 프로브 프로퍼티 차분(실효 델타: 무기 델타는 할당된 공격
  스탯과 승산 합성된 실제 증가분).
- **M3b 서약 토글**: 클리어 카드 한정 노출(SetActive(cleared), 빌드 시
  꺼짐). 기하: 강하 버튼 좌측 (-104,6) 84×28 — 감사된 강하 지오메트리
  (84×28, 같은 행, 갭 8) 복제; 카드 터치 계약 그대로. 상호작용 rect끼리
  비중첩(강하와 8u 갭, 상태 라벨과 수직 분리). 클리어 카드는 별칭 라인의
  "• 보상:" 꼬리를 동시 탈락(이미 수령한 보상 — 최장 별칭 89px < 토글
  좌단 180px, 시각 충돌 없음). 상태: `_pactArmed` Dictionary<string,bool>
  세션 한정(스펙 §세이브 스키마 — 미저장), `IsPactArmed(stageId)` public
  읽기 시임. 시각: off "서약"/InkDim/ButtonBack, armed "서약 ✓"/Ember/
  ember 22% 배경(위험=엠버 색 언어; 탭/로스터의 stateful flat-fill 문법,
  plated:false).
- **M4 군단 탭**: `CompanionEpithets` 5종 — 첫 서약의 증인 / 성당의
  메아리 / 왕좌의 메아리 / 행진의 메아리 / 정예의 잿불 (worldview 표가
  유일 출처, 전부 기존 글리프). 이름 라벨 offsetMin.y=14로 상단 밴드
  확보, 별칭은 하단 4..18 정적 라벨(빌드 1회, Refresh 미접촉,
  raycastTarget=false). 슬롯 0(없음)은 별칭 없음.
- Refresh 문법 유지: 텍스트/상태만, 재인스턴스화 0. 프로브는 스택 구조체
  (할당 0); 문자열 보간은 기존 Refresh 문법 그대로(데이터 변경 시에만
  호출되는 경로, per-frame 아님).

### 3. GameDirector.cs — M3c 라우팅 + 지급

- OnSortie 계약: **콜백 시그니처 불변** (string target 유지). 선택지 중
  "director가 LobbyView.IsPactArmed(stageId) 읽기"를 채택 — 사유:
  LobbyCallbacks 4필드 전부 기존 시그니처 유지(테스트/기존 배선 무접촉),
  서약은 세션 뷰 상태라 소유자(LobbyView)에게 두고 라우터가 조회하는
  쪽이 상태 이중화가 없음. StartDungeon에서 `_runWasPact` 래치(러닝 중
  토글 변경이 현행 런에 영향 없음; 리트라이는 재래치 — 토글 유지 시
  서약 유지).
- 해저드 선택: 서약 시 `config.Hazards = StageCatalog.PactFor(entry.Id)`
  가 override/anchor **대신** — else-if 체인이라 기존 경로 문자 그대로
  보존(서약 미선택 골든 불변, 스펙 §검증).
- HUD 마커: `_game.Begin(config, displayName + " — 서약", …)` — 기존
  stageName 파라미터 경유(캠페인 HUD 타이틀로 흐름), 신규 UI 0.
- 지급: `PactRelicMultiplier = 2` (internal const — 경제 테스트 핀 고정,
  entry 5 서명 수치). PersistDungeonClear에서 `sim.Relics × 2`는 서약
  런만; **첫클리어 보너스 비배수**(별도 라인 유지 — entry 5 비중복 조항.
  서약 토글이 클리어 카드에만 존재하므로 실전에서 서약 런의 firstClear는
  false — QA 딥링크가 경합해도 보너스 라인은 독립이라 계약 유지, 코드
  주석 명기). 패배 뱅킹 경로(GameOver)는 비배수 — 서약 보상은 "살아서
  다시 판결받은 자"에게만(worldview 서사와 일치).

### 4. worldview.md — '메타 서사 (v1.3)' 신설

- 티어명 2표(장비 3슬롯 × T0-T5) + 서사 규약(도구의 재→판결 복권,
  T5 판결인=도구의 인격화), 서약 의미("판결이 끝난 법정에 다시 서는
  자는 더 무거운 집행을 서약한다"), 동료 별칭 5행 표(기원 명기).
  EquipTierNames/CompanionEpithets의 유일 출처 + G1 추적성 조항.

### 신규 글리프 (폰트 재생성 필요 — 메인 레인)

View 어셈블리 렌더 문자열 리터럴 diff(HEAD 대비, 주석 제외 스캐너 —
v1.2와 동일 도구), 한글 7자 + 심볼 2자:

    금 날 담 랍 벼 생 천   (한글, 전부 LobbyView.cs: 티어명·재생 라벨)
    → (U+2192, 구매 델타 화살표) ✓ (U+2713, 서약 armed 마커)

shipped HudKorean.otf cmap(466 codepoints) 대조: **9자 전부 미포함**.
cmap에 §×—•…▲▶▼◀ 등 심볼 선례 있음 — 서브셋 재생성 시 U+2192/U+2713
추가 가능. 참고: HEAD 리터럴의 · (U+00B7)와 − (U+2212)도 현행 cmap
미포함(기존 이슈, v1.2 재생성 대기열과 합산 — v1.2의 렴숙쌍윤흑 5자
+ 이번 9자 = 재생성 1회로 14+2자 해소).

### Deviations from assignment spec (사유 포함)

1. **M1/M2 파생 수치 구현 경로**: 과제 원문 "상수 조합 미러" → Main 레인
   IRC 정정 지시대로 HackConfig 파생 프로퍼티 직독(프로브 패턴)으로 구현.
   뷰가 조합 상수를 아예 보유하지 않아 미러 드리프트가 타입 수준에서
   불가능 — MetaTest 미러 가드도 이 계약으로 단순화.
2. **gallery 서약 추가분 kind 전환** (vent→pillar): 위 §1 산술 증명 —
   베이스 4기 링이 모든 위상의 추가 vent를 동종 3 위반으로 만듦. 과제의
   "예산 위반 시 조정" 권한 행사, 정체성은 2차 기믹(기둥)으로 유지.
3. **march 서약 vent 이동** (스케치 768,880 → 768,796): 880은 드레싱
   클리어런스(prop-010 d=60.5 <140) 위반 + 평면 경계(y874) 6px 초과.
   796은 동일한 남측 밴드 거부 의도 유지(y706..886)하며 전 계약 준수.
4. **티어명 표기 조정**: 과제 스케치의 손상 문자(쟿/릷)와 한자(香)를
   글리프 경제·G1 어휘로 정규화(§2). worldview 표가 정본.
5. **서약 토글 28u 높이** (44u 아님): 강하 버튼과 동일한 카드 내 감사
   지오메트리 복제 — 68u 카드에서 44u 버튼은 v1.2 스크롤 결정(피치
   보존)과 충돌. 과제의 "카드 공간이 좁으면 강하 좌측" 배치 조항 적용,
   강하와 동일 취급(같은 행·같은 치수·갭 8).

### 검증 (에디터 없이)

- **Roslyn 구문 파스**(SDK 8.0.129 bincore, LanguageVersion.CSharp9):
  StageCatalog.cs / LobbyView.cs / GameDirector.cs 전부 SYNTAX OK, 에러 0
  (전 편집 완료 후 최종 상태 기준).
- 서약 센서스 기계 스윕: 9테이블 × 각 LCM(2.4/12/276s) × 0.05s 해상도 —
  최대치 표(§1)와 일치, 전부 ≤3/≤2. MetaTest 독립 미러와 교차 일치.
- 기하 기계 검증: 추가분 전부 평면 내(248..1288/334..874), 원형 해저드
  radial 비중첩(문서화 예외 2건 제외), gallery 기둥 간격 132 규칙,
  march 드레싱 radius+50 전 배치 클리어.
- 미러 가드 자체 스캔: 필수 4프로퍼티 존재 + 금지 5리터럴(58f/218f/
  0.06f/0.02f/0.08f) 부재 — PASS.
- 글리프 diff + cmap 대조: 위 §신규 글리프.
- 컴파일/EditMode/플레이: **미수행** — 메인 레인 게이트 소유. 에디터
  체크리스트: ① 컴파일 ② MetaTest v1.3 스위트(카탈로그 계약·센서스·
  미러 가드·경제 핀) ③ gen_hud_font.sh 재생성(9자, v1.2 5자와 합산) 후
  장비 탭 티어명/구매 화살표/서약 체크 렌더 ④ 서약 토글 on→강하→클리어
  →유물 2배 육안 + HUD "— 서약" 타이틀 ⑤ 성장 탭 수치가 강하 결과와
  일치하는지(프로브==런 배율) 육안.
