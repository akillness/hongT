# Mobile Layout Spec — 모바일 해상도 최적화 (View + Web template only)

2026-08-04 · ResearchSpec 레인 · 구현 대상: `Assets/Scripts/View/HudView.cs`, `LobbyView.cs`, `CameraRig.cs`, `InputAdapter.cs`, `Assets/Editor/BuildScript.cs`(PolishIndexHtml), `web/` 오버레이.
**Sim 불가침**: 입력은 기존 `SimInput.MoveX/MoveY(-1..1)` 계약 안에서만 (심은 이동 벡터를 항상 정규화 — CinderSim.cs L1013-1016).

## 기준 사실 (코드 검증 완료 — 인용은 원문)

| 항목 | 값 | 위치 |
|---|---|---|
| HUD CanvasScaler | `ScaleWithScreenSize`, `referenceResolution = new Vector2(1280, 720)`, `matchWidthOrHeight = 0.5f` | HudView.Build L65-68 |
| Lobby CanvasScaler | 동일 3값 (별도 캔버스, sortingOrder 5) | LobbyView.Build L112-115 |
| 터치 게이트 | `var touchscreen = UnityEngine.InputSystem.Touchscreen.current != null; var mouse = UnityEngine.InputSystem.Mouse.current != null; if (Application.isMobilePlatform \|\| (touchscreen && !mouse)) BuildTouchControls(root);` | HudView.Build L134-137 |
| 터치 컨트롤 구성 | 좌하 D-pad 190×190(버튼 58×58 4개) + 우하 "타격" 110×110 — **이동+타격만**; 대시/스킬 전용 터치 버튼 없음(스킬 카드 자체가 Button이라 탭은 가능) | HudView.BuildTouchControls L681-699 |
| 캔버스 HTML | `<canvas id="unity-canvas" width=1280 height=853>`; 데스크톱 CSS `1280px/853px` 고정; 모바일 UA 검출 시 `unity-mobile` 클래스 → `width:100%; height:100%` + `position:fixed` | build-webgl/index.html L13·L91-110, style.css L4·L6 |
| 모바일 viewport meta | UA 검출 시 JS 주입: `width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes` — **`viewport-fit=cover` 없음**, 데스크톱은 meta 자체가 없음 | index.html L91-97 |
| devicePixelRatio | 무제한 (기본 — `config.devicePixelRatio = 1` 주석 처리 상태) | index.html L101-103 |
| 캔버스 배경 | `#unity-canvas { background: #231F20 }` — 브랜드 `#050812` 아님 | style.css L5 |
| campaign.html | 즉시 `location.replace("index.html")` 리다이렉트 (meta refresh 백업) — 자체 UI 없음, 조치 불요 | web/campaign.html L9-11 |
| index.html 생성 경로 | Unity 기본 템플릿 → `BuildScript.PolishIndexHtml()` 문자열 후처리 (title·touch-icon) — **템플릿 수정은 이 함수 확장이 정본**; `web/`은 배포 시 rsync 오버레이(gh-pages 전용, 로컬 빌드 미반영) | BuildScript.cs L61-72, tools/deploy/deploy_pages.sh L30 |
| 카메라 종횡비 보정 | `ApplyAspect`가 **Arena 프로파일 전용** (`if (_camera == null \|\| _profile != Profile.Arena) return;`) — Dungeon(FOV 42 고정)·Prologue(ortho 3.105 고정)는 세로 화면에서 수평 시야 붕괴 | CameraRig.ApplyAspect L102-110 |

### 파생 수치 (390×844 포트레이트, match 0.5)

scaleFactor = 2^(0.5·log₂(390/1280) + 0.5·log₂(844/720)) ≈ 0.597 → **캔버스 유효폭 ≈ 653 u, 유효높이 ≈ 1413 u**.
1 캔버스 u ≈ 0.597 CSS px (3× DPR 기기: ≈ 1.79 물리 px).

**충돌 실측 (653 u 폭 기준, 전부 재현 확정)**:
- 던전 스킬 행: dash 카드 좌단 x=39.5 u < D-pad 우단 x=214 u → **175 u 겹침** (HudView L392-395 vs L684-685).
- 아레나 2카드: nova 카드 좌단 x=156 u < 214 u → **58 u 겹침**.
- 캠페인 장비 스트립(좌하 16,16 / 240×34, L180-181) — D-pad 박스(24,24 / 190×190) **내부에 완전 매몰**.
- "타격" 버튼 좌단 x=519 u < F 카드 우단 x=563 u → **44 u 겹침**.
- 로비: SORTIE(392 u, 우) + SANCTUM(400 u, 좌) + 여백 48 u = 840 u > 653 u → **187 u 겹침**.
- 터치 D-pad 버튼 58 u ≈ **34.6 CSS px < 44 pt 최소 타깃 미달**. "타격" 110 u ≈ 65.7 px OK. 던전 스킬 카드 108 u ≈ 64.5 px OK.
- Prologue ortho: 세로 aspect 0.462에서 가시폭 2×3.105×0.462 ≈ 2.87 u — 아레나 폭 10.4 u의 **28%만 보임** (사실상 플레이 불가).
- Dungeon FOV 42 고정: 수평 FOV ≈ 20° → 거리 17에서 가시폭 ≈ 6.1 u (59%) — 스폰 포인트 시야 밖.

---

## 항목

### 1. 오리엔테이션 인지 CanvasScaler — 세로에서 match 0 전환
- **WHAT**: 두 캔버스의 `matchWidthOrHeight`를 화면 종횡비에 따라 동적 전환: landscape 0.5(현행) / portrait 0.35.
- **WHERE**: `HudView`·`LobbyView` — scaler 참조 필드로 보관(HudView L65-68, LobbyView L112-115), 신규 공용 헬퍼 `ViewWorld.MatchFor(float aspect)` 또는 각자 Update 내 해상도 dirty-check.
- **HOW**: `scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0.35f : 0.5f;` — 완전 width-match(0)는 터치 타깃이 17 CSS px로 붕괴하므로 금지. 0.35는 653→739 u 유효폭으로 겹침 완화 + 타깃 30.9 px(하한 #6에서 보전). 해상도 변경 프레임에만 세팅(매 프레임 대입 금지 — 캔버스 리빌드 유발).
- **COST**: S
- **RISK**: 0.35는 절충값 — 실기기에서 0.3-0.4 재보정 여지; 회전 순간 1프레임 레이아웃 점프(허용).

### 2. HUD 컴팩션 티어 — 유효폭 3단계
- **WHAT**: 캔버스 유효폭(`Screen.width / scaler.scaleFactor`) 기준 3티어: ≥980 full / 700-980 compact / <700 phone.
- **WHERE**: `HudView` — 신규 `ApplyLayoutTier(int tier)`; `Build()` 말미 + 해상도 dirty-check에서 호출. 각 패널 RectTransform 참조는 Build에서 필드로 보관.
- **HOW**: compact: 상단 meters 300→240 u(바 폭 축소), stats 240→200 u, 폰트 18→15. phone: stats 패널을 meters **아래** 세로 스택(x=16, y=-98)으로 이동, 라벨 4→2행(웨이브·점수만, 유물·적 수는 웨이브 텍스트에 병합 "웨이브 3 · 적 7"), 뮤트 버튼 우상단 아이콘화(34×34).
- **COST**: M
- **RISK**: 병합 라벨은 문자열 조립 발생 — 기존 dirty-check(값 변화 시만) 안이므로 무할당 원칙 유지; 한국어 라벨 폭 실측 필요.

### 3. 던전 스킬 행 리플로우 — phone 티어 2단 배치
- **WHAT**: <700 u에서 5카드 1행(589 u)을 2단(위: Q/E/R/F 4×86 u, 아래 중앙: SHIFT 질주 1장)으로 재배치, 카드 108→86 u 축소.
- **WHERE**: `HudView.EnableDungeonUi` L392-410 — 카드 RectTransform 배열 필드로 보관 후 `ApplyLayoutTier`에서 anchoredPosition/sizeDelta 재설정. 콤보 핍(L379-387)은 스킬 행 좌측→행 위 중앙으로.
- **HOW**: phone: 4카드 x = -129,-43,43,129 (합 344+갭 = 356 u), y=64; dash 카드 (0,18) 96×64. 86 u 카드 ≈ 51 CSS px ≥44 유지. full/compact는 현행 유지.
- **COST**: M
- **RISK**: 2단 행이 XP 바(y=4)·부활 패널과 근접 — XP 바를 y=0 풀폭 유지, 시각 검증 필수.

### 4. 터치 컨트롤 충돌 해소 — D-pad·스킬 행·장비 스트립 재배치
- **WHAT**: 터치 활성 시 좌하 D-pad와 겹치는 세 표면(#기준사실 충돌 4건)을 전부 비겹침 좌표로 이동.
- **WHERE**: `HudView.BuildTouchControls` L681-699(신규 좌표), `EnableCampaignUi` L180-181(장비 스트립), `ApplyLayoutTier`(#2·#3과 동일 훅).
- **HOW**: 터치 시: 스킬 행 전체를 y+210(D-pad 상단 214 u 위로) — 또는 #3 phone 리플로우와 병행 시 행 자체가 위이므로 y+120로 충분. 장비 스트립 → 좌**상**단 meters 아래(16, -98; #2 phone에서는 stats 아래 -160). "타격" 버튼 (-24,36)→(-24,150) 상향해 F 카드와 44 u 겹침 해소.
- **COST**: S
- **RISK**: 좌표 상수 다발 — 티어 함수 한 곳에 집중해 산재 방지(리뷰 게이트).

### 5. 대시 전용 터치 버튼
- **WHAT**: 던전 터치 시 "타격" 위에 "질주" 버튼(96×96) 추가 — 현재 대시는 화면 하단 SHIFT 카드 탭뿐이라 엄지 동선 밖.
- **WHERE**: `HudView.BuildTouchControls` — 던전 모드에서만 활성(필드로 만들고 `SetCampaignSurfacesVisible`/`EnableDungeonUi`에서 토글). 콜백 `Input.QueueDash()` (InputAdapter L69 기존).
- **HOW**: `TouchButton` 문법 재사용, 우하 (-24, 260), 시안 톤 배경. 96 u ≈ 57 CSS px.
- **COST**: S
- **RISK**: 우측 세로 버튼 2개(타격·질주)로 오탭 가능 — 갭 24 u 확보.

### 6. D-pad 버튼 44 pt 하한 확보
- **WHAT**: D-pad 개별 버튼 58→84 u, 패드 박스 190→260 u — 34.6→50 CSS px(portrait #1 적용 후에도 ≥44 유지).
- **WHERE**: `HudView.BuildTouchControls` L684-689 — 상수 교체(버튼 84×84, 배치 (88,176)/(88,8)/(4,92)/(172,92), 패드 260×260).
- **HOW**: 좌표만 재계산 — 로직 무변경. #4의 스킬 행 상향과 함께 적용(패드가 커지므로 y+210 필수).
- **COST**: S
- **RISK**: 큰 패드가 화면 하단 25%를 점유 — 게임 뷰 가림은 카메라가 중앙 고정이라 허용 범위.

### 7. 가상 조이스틱 권고 — D-pad 대체 (권고: 채택)
- **WHAT**: 좌하 사분면 터치-드래그 플로팅 조이스틱으로 D-pad 교체 — 8방향→**임의각 이동**.
- **WHERE**: `HudView.BuildTouchControls` 대체 구현 + `InputAdapter`에 `public float TouchMoveX, TouchMoveY;` 신설, `Sample()` L83-88에서 bool D-pad 합산 대신 float 합산(`moveX += TouchMoveX` — D-pad 병존 시 클램프가 이미 처리).
- **HOW**: 판정 근거: 심은 이동 벡터를 **정규화**(CinderSim L1013-1016)하므로 조이스틱은 속도가 아닌 **방향 해상도**만 개선 — 하지만 사거리 160 vs 76의 스탠드오프 카이팅이 코어 스킬이므로 임의각 이탈은 실질 전투력. 구현: IDrag 핸들러 패널(좌하 300×300 u), 드래그 벡터/60 u 정규화 클램프, 손 떼면 0. 데드존 12 u. D-pad는 접근성 폴백으로 옵션 유지 대신 **완전 교체 권고**(코드 단순화).
- **COST**: M
- **RISK**: SimInput은 float 계약이라 심 무변경·결정론 무해(같은 float 열=같은 Digest). 미세 드리프트 방지로 데드존 필수. QA: 기존 D-pad 스모크가 조이스틱 시퀀스로 교체 필요.

### 8. 로비 세로 스택 — 2패널 → 1열
- **WHAT**: <700 u에서 SORTIE(우 392)·SANCTUM(좌 400) 고정폭 패널을 상하 스택 풀폭(-32 u)으로 재배치.
- **WHERE**: `LobbyView` — `BuildSortiePanel` L246-248·`BuildSanctumPanel` L295-296의 RectTransform을 필드 보관, 신규 `ApplyLobbyTier()`에서 재앵커. 호출은 Build 말미 + 해상도 dirty-check.
- **HOW**: portrait: SORTIE anchor (0,1)-(1,1), offset (16,-72)~(-16,-72-336) 카드 2열 그리드(프롤로그+3스테이지 = 190 u 폭 카드 2×2); SANCTUM 그 아래 (16,-424)~(-16,-1000) — 탭 3개 유지, 내용 세로 스크롤 불요(스택 총높이 1413 u 유효높이 내). landscape phone(844×390, 유효 1104×~590 u): 두 패널 폭 340으로 축소 + 높이 500 클램프로 병존 유지.
- **COST**: M
- **RISK**: 가장 큰 구조 변경 — 카드/버튼 자식 앵커가 부모 사이즈에 의존하지 않는 절대 배치라 부모만 옮기면 대부분 따라옴(카드 y 오프셋만 그리드 재계산 필요). 실기기 세로 스크린샷 게이트 필수.

### 9. 카메라 종횡비 보정 확장 — Dungeon·Prologue
- **WHAT**: Arena 전용 `ApplyAspect`를 Dungeon(거리 보정)·Prologue(ortho 폭 보정)로 확장 — 세로에서 아레나 가시폭 59%/28% 붕괴 해소.
- **WHERE**: `CameraRig` — `ApplyAspect` L102-110 프로파일 게이트 제거·분기화; Dungeon은 `PlaceOrbit` 거리 인자에 배율, Prologue는 `orthographicSize` 재설정, `SetProfile` L65-71에도 반영.
- **HOW**: `widen = clamp(1.5 / aspect, 1, 2.2)`(기존 공식 상한만 2→2.2). Dungeon: `PlaceOrbit(55°, _dungeonDistance * widen, …)` — FOV는 42 유지(원작 검증 수치 존중, 거리로만 보정). Prologue: `orthographicSize = 3.105f * widen`. Lobby는 연출 프레이밍이라 제외.
- **COST**: S
- **RISK**: 세로에서 카메라가 멀어져 캐릭터가 작아짐 — 3D 판독성 실기기 확인; 거리 2.2×는 클리핑/포그 무관(현 씬 무포그).

### 10. viewport meta 정적화 + safe-area (노치 대응)
- **WHAT**: `viewport-fit=cover` 포함 viewport meta를 **정적으로 head에 상주**시키고, safe-area 인셋을 CSS로 컨테이너에 패딩.
- **WHERE**: `BuildScript.PolishIndexHtml` L61-72 — 문자열 치환 확장(정본); `web/`은 배포 오버레이일 뿐이므로 손대지 않음.
- **HOW**: favicon 태그 치환에 `<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">` 추가(기존 JS 주입 L94-97은 UA 검출 시 교체하므로 병존 무해 — 단 JS 주입 문자열에도 `viewport-fit=cover` 추가 치환).
  스타일 주입: `.unity-mobile #unity-canvas { padding: env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left); box-sizing: border-box; background:#050812; }` — Unity `Screen.safeArea`는 WebGL에서 신뢰 불가라 CSS 층에서 해결.
- **COST**: S
- **RISK**: canvas padding 방식은 WebGL 뷰포트와 CSS 픽셀 매핑을 검증해야 함 — 실패 시 container padding + canvas 100%로 폴백 [INFERENCE — iOS 실기기 필수].

### 11. devicePixelRatio 상한 2
- **WHAT**: 3× DPR 폰(390×844 → 1170×2532 네이티브 렌더)을 2×(780×1688)로 캡 — GPU 프레임 예산 방어.
- **WHERE**: `BuildScript.PolishIndexHtml` — config 블록 문자열에 `config.devicePixelRatio = Math.min(window.devicePixelRatio, 2);` 주입(index.html L101-103 주석 위치).
- **HOW**: `showBanner: unityShowBanner,` 라인 뒤 삽입 치환 1건.
- **COST**: S
- **RISK**: 2× 캡은 텍스트 미세 소프트닝 — HUD 폰트가 충분히 커서(스케일드) 허용; 1× 강제는 과도.

### 12. 데스크톱 협폭 대응 — 고정 1280px 캔버스 유연화
- **WHAT**: 데스크톱 CSS를 `max-width: min(1280px, 100vw); max-height: min(853px, 100vh); aspect-ratio: 1280/853`으로 — 1280 미만 창·iPadOS 데스크톱 모드 Safari에서 캔버스 잘림 해소.
- **WHERE**: `BuildScript.PolishIndexHtml` — L108-109 대응 문자열(`canvas.style.width = "1280px"`) 치환 + 스타일 주입.
- **HOW**: JS 고정값 대신 클래스 기반: `canvas.className = "unity-desktop-canvas"` + 주입 CSS. 종횡비 유지 레터박스(배경 `#050812`).
- **COST**: S
- **RISK**: Unity 로더가 canvas 스타일을 다시 만질 가능성 — `config.matchWebGLToCanvasSize` 기본(true)이 DOM 크기 추종이므로 안전.

### 13. 캔버스 레터박스/필 전략 — 판정: 모바일 필(fill), 데스크톱 레터박스
- **WHAT**: 390×844 포트레이트·844×390 랜드스케이프 모두 **풀-필 유지**(현행 `unity-mobile` 100%/100%), 종횡비 고정 레터박스는 도입하지 않음.
- **WHERE**: 결정 기록 항목 — 코드 변경은 #10(배경색 `#231F20`→`#050812`, style.css L5 대응 치환)만.
- **HOW**: 근거: (a) 3D 원근 카메라는 임의 종횡비 렌더 가능, #9가 가시폭 보전, (b) HUD는 코너 앵커라 필에서 자연 적응(#1-#6이 겹침 해소), (c) 레터박스는 세로에서 화면 44%를 죽임(844→390·1.5 기준). 아레나 모드 FOV widen(기존 L108)이 이미 필 전제.
- **COST**: S
- **RISK**: 초광폭(21:9 폰 랜드스케이프)에서 FOV widen 상한 2.2 초과분은 시야 고정 — 수용.

### 14. 포트레이트 던전 1회성 가로 권장 힌트
- **WHAT**: <700 u 포트레이트에서 던전/아레나 출정 시 1회 토스트 "가로 화면을 권장합니다" 2.5 s (강제 회전 없음).
- **WHERE**: `HudView` — 프롤로그 토스트 패널 문법(L291-310) 재사용; 트리거는 `GameDirector.StartDungeon`/`StartArena`에서 화면 비 검사. `PlayerPrefs` 키로 1회 제한.
- **HOW**: `if (Screen.width < Screen.height && !PlayerPrefs.HasKey("al:rotate-hint")) { toast; PlayerPrefs.SetInt(...); }`
- **COST**: S
- **RISK**: 없음 — 순수 안내. 세로 플레이가 #1-#9로 성립하므로 강제 금지.

---

## 컷 라인 (시간 압박 시 아래부터 순서대로 드랍)

**드랍 순서**: #14 가로 힌트 → #12 데스크톱 협폭 → #5 대시 터치 버튼 → #7 조이스틱(D-pad 유지 + #6 확대만) → #2 phone 티어의 stats 재배치(compact까지만) → #8 로비 세로 스택(임시로 portrait 시 SANCTUM 접힘 처리).

**절대 사수 (플레이 성립 코어)**: #9 카메라 종횡비(세로 시야 28%는 **플레이 불가 버그**에 준함), #4 터치 충돌 해소(장비 스트립 매몰·스킬 행 겹침), #6 44 pt 하한, #1 오리엔테이션 match, #10 safe-area+viewport(노치 기기 조작 불능 방지), #11 DPR 캡(성능 게이트), #13의 배경색 치환(1줄).

**검증 계약**: 390×844·844×390 실뷰포트에서 (a) 겹침 0, (b) 터치 타깃 전수 ≥44 CSS px, (c) 던전 아레나 가시폭 ≥90%, (d) 노치 기기 하단 D-pad 도달 가능 — 기존 브라우저 하니스(`tests/sprite-2-5d-browser.cjs`의 390×844 무오버플로 검사 전례)를 Unity 빌드용으로 이식해 스크린샷 게이트.
