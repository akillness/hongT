# Cycle 2 Spec — 기획·연출 심화 (View-only)

2026-08-04 · 오케스트레이터 직접 작성 (Cycle2Design 레인 실패 대체) · Sim 불가침 동일.
Unity 관례: localScale/CanvasGroup 트윈(레이아웃 리빌드 금지), AnimationCurve/SmoothStep 이즈,
PlayerPrefs 설정, MaterialPropertyBlock, 풀·타이머 필드 무할당.

## A) 연출

### A1. 보스 인트로 시네마틱
- WHAT: `BossSpawned` → 레터박스 바(상하, 0.45s 슬라이드인, 2.0s 유지) + 보스 네임 플레이트
  (StoryCatalog speaker) + CameraRig 보스 포커스 풀(0.8s, 이후 복귀). 입력 비차단(심 계속).
- WHERE: HudView(레터박스+플레이트), CameraRig(신규 `FocusPulse(Vector3, float)` — 이번 사이클
  단독 소유라 PlaceOrbit focus 인자 러프 허용), GameDirector(BossAnchor 전달).
- HOW: 레터박스 = 검정 full-width Image 2장 anchorMin/Max (0,1)/(1,1)·(0,0)/(1,0), height 90,
  anchoredPosition.y 트윈. 플레이트 = 중앙 상단 Text "— {보스명} —" gold, 알파 인아웃.
  FocusPulse: `_focusBlend` 타이머, LateUpdate Dungeon 분기에서 focus를 `Lerp(player, boss, blend)`.
- COST: M / RISK: 레터박스 중 조작감 저하 — 입력 비차단이므로 순수 시각.

### A2. 스테이지 클리어 세리머니
- WHAT: `StageCleared` → 골드 엣지 플래시(0.5s) + "스테이지 클리어" 배너 펀치인 + 점수/유물
  카운트업(0.8s) 후 기존 클리어 패널 표시.
- WHERE: HudView.OnEvents + ShowStageClear 지연 훅(GameDirector 경로 확인).
- HOW: 기존 radial 텍스처 재사용(골드 틴트), 배너는 waveBanner 문법 복제. 카운트업은
  `Mathf.RoundToInt(Mathf.Lerp(0, final, t))` 매 프레임 — 문자열은 값 변화 프레임만 갱신.
- COST: S / RISK: 클리어 패널과 겹침 — 패널 표시를 0.9s 지연(뷰 타이머).

### A3. 콤보 핍 펀치 + 피니셔 골드 (cycle1 컷 회수)
- WHAT: `ComboIndex` 증가 프레임 해당 핍 scale 1.5→1 펀치, `ComboFinisher` 3핍 골드 플래시.
- WHERE: HudView.SyncDungeon 콤보 분기 + OnEvents.
- HOW: `_pipPunch[i]` 타이머 3개, localScale 트윈. 골드 플래시는 색 스냅 후 0.4s 복귀.
- COST: S / RISK: 없음.

### A4. 보스 HP바 등장/페이즈2 연출
- WHAT: 보스바 슬라이드다운 등장(0.4s), 페이즈2 시 바 색 엠버→적색 시프트 + 바 자체 1프레임 셰이크.
- WHERE: HudView.SyncDungeon 보스 분기(_bossBar activeSelf 전환 프레임) + BossPhase2 이벤트.
- HOW: anchoredPosition.y -40→0 트윈; 페이즈2에 `_bossFill.color` 스왑 + rect 펀치.
- COST: S / RISK: 없음.

### A5. 모션 약함 게이팅 (접근성, Unity PlayerPrefs 관례)
- WHAT: ViewPrefs.ReducedMotion(신규, 이미 생성)을 hit-stop/slow-mo(off), shake/플래시(0.4x)에 적용.
- WHERE: GameView(hit-stop 게이트), CameraRig.Punch/Shake(진폭 *MotionScale), HudView(StartCastFlash
  알파 *MotionScale, 비네트 펀치 동일). 토글 UI는 B4.
- COST: S / RISK: 없음 — 기본 off.

## B) 기획

### B0. 던전 HP바 분모 수정 (REQUIRED — 실측 버그)
- WHAT: 던전 최대체력(100+vit*8+cloak)은 100 초과인데 HudView.Sync가 `/SimConfig.PlayerMaxHealth`
  고정 — 바가 최대에서 209%로 클램프되어 실체력 오독.
- WHERE: HudView — `_maxHealthSeen` 필드. Begin 직후 첫 Sync에서 `max(100, health)` 시드,
  이후 `max(_maxHealthSeen, health)` 갱신, 분모로 사용. `ResetRunUi()`(신규, GameView.Begin에서 호출)
  에서 100으로 리셋.
- COST: S / RISK: 회복 픽업으로 초과 시 자동 상향 — max-seen 의미상 정확.

### B1. 로비 스테이지 카드 강화
- WHAT: 스테이지 카드에 보스명(이미 있음) + 위험 요소 글리프(스킬 아이콘 재사용: cinder-span=
  skill-nova(벤트), abyss-chancel=skill-aegis(기둥), echo-throne=skill-pulse(메아리)) + 보상 라인 강조.
- WHERE: LobbyView.BuildSortiePanel 카드 루프 — RowIcon 문법 재사용(24px, 보스 라인 좌측).
- COST: S / RISK: 카드 높이 여유 확인(포트레이트 스택).

### B2. 퀵 리트라이
- WHAT: 게임오버/클리어 패널에 "다시 강하 (R)" — 로비 경유 없이 같은 스테이지 즉시 재시작.
  (사망 페널티 없음 = 스펙 §11 리스크 계약 유지, 뷰 편의만.)
- WHERE: HudView 게임오버 패널(던전 모드일 때 버튼 라벨/콜백 스왑) + GameDirector.RetryStage()
  신규(현재 _runStageId로 StartDungeon 재호출; 프롤로그/아레나는 기존 R 경로 유지).
- COST: S / RISK: 클리어 후 재도전은 보상 재획득 — 심 규칙 그대로(정상).

### B3. 첫 실행 가이드
- WHAT: PrologueDone=false 동안 로비 프롤로그 카드 테두리 엠버 펄스(1.2s 주기).
- WHERE: LobbyView — 프롤로그 카드 Border Image 참조 보관, Update에서 PingPong 알파.
- COST: S / RISK: 없음.

### B4. 설정 행: 모션 약함 토글
- WHAT: 로비 SANCTUM에 "모션 약함" 토글 버튼(성장 탭 하단 힌트 아래) — ViewPrefs.ReducedMotion.
- WHERE: LobbyView.BuildGrowthTab 하단 + TextButton 재사용, 라벨 "모션: 보통/약함".
- COST: S / RISK: 없음.

### B5. 사망 리캡 컨텍스트
- WHAT: 게임오버 패널에 사망 맥락 1줄: 최근 이벤트 캐시(view) — 보스 존재 시 "보스전 패배",
  HazardPulse 최근 2s 내 "위험 지대에 잠식", 그 외 "군단에 함락" + "웨이브 N 도달".
- WHERE: HudView — `_recentHazardTime`/`_bossAliveAtDeath` 캐시, OnEvents GameOver 분기에서 조립.
- COST: S / RISK: 휴리스틱 오판 가능 — 문구를 단정 대신 분위기 서술로.

## 컷 라인
시간 압박 시: A4 → B3 → B5 → A3 순으로 드랍. A1·A2·B0·B2는 사수.

## 검증 계약
- EditMode 66/66 유지 (HudLayoutTests 포함 — 신규 표면 전부 raycastTarget=false 준수).
- 데스크톱 스모크: 보스 스폰(웨이브5 도달 또는 시드 세이브) 레터박스+플레이트 스크린샷,
  클리어 세리머니 스크린샷, 게임오버 리캡 문구 확인.
- 포트레이트 390x844: 로비 카드 글리프/토글 겹침 없음.
