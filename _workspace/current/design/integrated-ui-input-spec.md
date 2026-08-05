# Integrated Spec C — UI·입력·시스템 통합 스펙 (View-only + §S1-S4 AMENDMENT #3 격리)

2026-08-05 · Achilles Visual Overhaul 통합 문서 C · 구현 대상: `Assets/Scripts/View/HudView.cs`·`InputAdapter.cs`·`LobbyView.cs` + `Assets/Tests/EditMode/HudLayoutTests.cs` + `build-webgl/index.html`(BuildScript 후처리).

**Sim 불가침**: 입력은 기존 `SimInput.MoveX/MoveY(-1..1)` 계약 안에서만(심은 이동 벡터 항상 정규화 — CinderSim.cs L1013-1016). 수치·타이밍 변경 금지.

## 통합 매핑 (원본 → 본 문서)

| 원본 문서 | 원본 항목 | 본 문서 |
|---|---|---|
| mobile-layout-spec.md | #1-14 | §1, §2 |
| cycle2-spec.md | B0-B5 | §3 |
| achilles-visual-overhaul-spec.md | §U1·§U2·§U3·§P·§L·§C·§M1·§M2·§G | §4 |
| deep-interview-vfx-terrain-command-hardening.md | Lane K/P | §5 |
| view-vfx-research.md | Gemini 콘솔 | §6 |

## §1. 모바일 레이아웃 (mobile #1-#12)

### 1.1 파생 기준 수치
- CanvasScaler 1280×720·match 0.5. 포트레이트 653×1413u(1u ≈ 0.597 CSS px).
- 조이스틱 catch 260×260·deadzone 0.15(L1241-1263). D-pad 190×190·타격 110×110.
- 스킬 슬롯 72u(phone 76u = 44px 터치 하한). 조이스틱 좌단 ≥ 24u.

### 1.2 항목별 규칙
| # | 항목 | 결정 |
|---|---|---|
| 1 | CanvasScaler match | 0.5 → **0.35**(세로 급 화면 안전) |
| 2 | 세로/가로 전환 | 게이트 `Application.isMobilePlatform \|\| (touchscreen && !mouse)`(L262-265) 유지, 전환 시 스냅 |
| 3 | 터치 영역 | 조이스틱·버튼 터치 하한 44px 유지 |
| 4 | 버튼 배치 | 스킬 카드 4종 하단 — 슬롯 72u 고정 |
| 5 | HUD 밀도 | phone 76u 초과 시 크기 축소(비율 유지) |
| 6 | 폰트 | HudKorean 서브셋 유지, 축소 시 텍스트 생략(점 ···) |
| 7 | 데미지 숫자 | 문서 A §1.5 풀과 동일 문법 |
| 8 | safe-area | `viewport-fit=cover` + safe-area 인셋 반영 |
| 9 | DPR | `maximum-scale=1` 유지, DPR 2 이상 시 품질 티어(§2) |
| 10 | 1280 고정 해제 | 유연 해상도 — 캔버스 레퍼런스 유지, 요소는 앵커 기반 |
| 11 | 터치 스킬 쿨타임 | 스냅샷 쿨타임 표시(심 불변) |
| 12 | 가상 패드 시인성 | 베이스 알파 0.5, 터치 시 0.9 |
| 13 | 레터박스/필 | 모바일 **필(fill)** 유지, 종횡비 고정 레터박스 도입 안 함(390×844·844×390 모두 풀-필, `unity-mobile` 100%/100% 현행 유지) — 코드 변경은 #10 배경색(`#231F20`→`#050812`, style.css L5)만. 근거: 3D 원근 카메라 임의 종횡비 렌더 가능 + HUD 코너 앵커 + 레터박스는 세로에서 44% 사망. RISK: 초광폭 21:9 FOV widen 상한 2.2 초과분 시야 고정 — 수용 |
| 14 | 가로 권장 힌트 | <700 u 포트레이트에서 던전/아레나 출정 시 1회 토스트 '가로 화면을 권장합니다' 2.5 s(강제 회전 없음) — HudView 프롤로그 토스트 문법(L291-310) 재사용, `GameDirector.StartDungeon/StartArena` 트리거, `PlayerPrefs` 키 1회 제한 |

### 1.3 충돌 해소 (기존 6건)
- #1 vs cycle2 B1: match 0.35 채택, B1 레이아웃은 앵커 재계산으로 수용.
- #8 vs viewport: `viewport-fit=cover` + safe-area CSS(BuildScript) 추가.
- #9 vs 품질 티어: DPR 2+는 블룸 강등(문서 A §5.5) — 이중 부담 방지.
- #12 vs reduced-motion: 파티클만 감쇠, 패드 알파는 유지.

## §2. 반응형 품질 티어

| 티어 | 조건 | 적용 |
|---|---|---|
| High | 데스크톱 + DPR ≤ 2 | 블룸 0.35, 파티클 100% |
| Mid | 모바일 + DPR ≤ 2 | 블룸 0.25, 파티클 75% |
| Low | DPR > 2 또는 p95 초과 | 블룸 off, 파티클 50% |

- 티어 전환은 런타임 1회(런 시작 시) + 프로파일 게이트에서 강등만.

## §3. cycle2 B0-B5

| ID | 항목 | 내용 |
|---|---|---|
| B0 | HP바 분모 버그 | `_maxHealthSeen` 갱신 + `ResetRunUi()` — 최대 체력 변경 후 분모 오류 수정 |
| B1 | 모바일 HUD | §1.2 기반 재배치 |
| B2 | 페이즈 배지 | 문서 A §3.4와 공유 |
| B3 | 콤보 카운터 | 문서 A §3.3과 공유 |
| B4 | 설정 행: 모션 약함 토글 | 로비 SANCTUM 성장 탭 하단 힌트 아래 토글 — ViewPrefs.ReducedMotion, TextButton '모션: 보통/약함' 재사용 (LobbyView.BuildGrowthTab) |
| B5 | 컷 라인 | A4→B3→B5→A3 순 검증(컷 라인 유지) |

- **컷 라인**: A1·A2·B0·B2 사수(삭제 금지). 구현은 컷 라인 순서로 병행.

## §4. 로비·스킬 슬롯·실루엣 (achilles §U1/§U2/§U3/§P/§L/§C/§M1/§M2/§G)

### 4.1 §U1 스킬 슬롯 바
- 4스킬 슬롯 바 72u — LobbyView·HudView 공유 컴포넌트.
- **HudLayoutTests 갱신 계약**: `InteractiveRects` 갱신 + 슬롯 rect 검증 추가. 사각지대(기존 슬롯 미포함) 해소.

### 4.2 §U2 실루엣 패널
- 캐릭터 선택 실루엣 패널 + Q/E 탭 순환. 로스터 8 ID 표시.
- 실루엣은 `assets/mesh/character/*/glb/base_basic_pbr.glb` 썸네일 캡처(빌드 타임).

### 4.3 §P 파츠 분할 연출
- "콜리더 분할" 요구는 **렌더러/파츠 분할 연출**로 번역(물리 콜라이더 추가 금지 — 심 순수 2D 수학 판정 유지).
- 사망 파츠 분리·타격 파츠 흔들림은 View 메시 그룹 조작만.

### 4.4 §L·§C·§M1·§M2·§G
- §L: 로비 라이브 3D 배경(워든·동료·보스 대치) 유지 — 문서 A §7 배경과 통합.
- §C(전환 컷신)·§M1(모션)·§G(Gemini): 문서 A §4·§6 참조.
- **§M2 가상 조이스틱 데스크톱 노출**: 조이스틱이 D-pad를 대체했으나 게이트 L262-265(`Application.isMobilePlatform || (touchscreen && !mouse)`)가 마우스 보유 데스크톱에서 터치 컨트롤 전체를 숨김 → 터치 이벤트 실제 발생 시 지연 생성으로 게이트 완화. WHERE: HudView — Build의 정적 게이트 + Update에서 최초 `Touchscreen.current?.primaryTouch.press.wasPressedThisFrame` 감시(프레임당 1회 null 체크, 무할당) 시 1회 `BuildTouchControls` 지연 호출, 생성 후 감시 중단. COST S. RISK: 마우스+터치 동시 기기에서 조이스틱이 스킬 카드와 겹침 — §1.2 재배치 규칙 선행 조건.
- §C(전환 컷신)·§M1(모션)·§G(Gemini): 문서 A §4·§6 참조.
### 4.5 §U3 소환수 인벤토리 (Phase 2 게이트)
- **WHAT**: 소환수에 아이템/스킬을 장착하는 설정 UI. Phase 2 게이트 — 1차 릴리스에서 제외, §S3(소환수 룬 슬롯 계약) 승인 후 연결. Phase 1 범위: U2 실루엣 패널의 소환수 정보 표시까지만.
- **COST**: M(Phase 2 시). Phase 1: U2에 흡수.

## §5. 레인 K/P (vfx-terrain)

| 레인 | 내용 |
|---|---|
| K | 키아트·로딩 스크린(문서 A §7 자산과 공유) |
| P | 파티클 프리셋 공유 계약 — 문서 A §5.3 풀 문법 |

## §6. Gemini 콘솔 (문서 A §6.2 상세)

- `#gemini=<명령>` 프래그먼트 파싱(BuildScript 폴리필 포함) → `GeminiCommandClient.cs`로 전달.
- 콘솔 UI: HUD 우측 슬라이드 패널(모바일 시 자동 접힘). 입력 창 1개.
- 명령: 시전(`cast <skill>`)·리셋(`reset`)·카메라(`cam dump`)·상태(`state`).
- 실패 시 오버레이 에러 + 재시도 1회. 키는 PlayerPrefs 로컬 전용.

## §S. 심 변경 격리 (AMENDMENT #3)

| ID | 항목 | 심 변경 | 게이트 |
|---|---|---|---|
| S1 | 스킬 슬롯 확장(룬 슬롯) | 스냅샷/입력 확장 | AMENDMENT #3 + 테스트 |
| S2 | 룬 슬롯 사용 계약 | RunPreparation 통합 | AMENDMENT #3 + 테스트 |
| S3 | 준비 선택 UI 연동 | PreparationOfferKind 노출 | AMENDMENT #3 + 테스트 |
| S4 | 조합 보스 | 캠페인 보스 조합 규칙 | AMENDMENT #3 + 테스트 |

- 각각 `// FROZEN CONTRACT AMENDMENT #3` + `docs/SIM_SPEC_HACKSLASH.md` 개정 + 결정론 EditMode 테스트 선행. View 구현과 병행 금지.

## §7. 구현 순서·검증

1. §1 모바일 레이아웃(#1-12) + HudLayoutTests 갱신.
2. §3 B0 수정(B0 → B4 → B1 → B2/B3) — 컷 라인 검증.
3. §4 로비(U1/U2) — InteractiveRects 사각지대 해소.
4. §5·§6 K/P/Gemini.
5. §S1-S4 — 승인 후.

**완료 조건**: EditMode 66/66 + HudLayoutTests 신규 검증 통과 + 데스크톱/모바일 스모크 + 심 diff 0(S1-S4 미승인 시).
