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
