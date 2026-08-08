# Production Brief — run-id 20260808-lobby-icon-rail

## bmad-gds schema

| 항목 | 값 |
|---|---|
| game_type | 아이소메트릭 핵앤슬래시 (Cinder Court, Unity 6000.5.6f1 / URP / WebGL) |
| team_shape | 5-agent harness (director / designer / pm / programmer / qa) |
| engine | Unity 6000.5.6f1, URP 17.5, WebGL |
| current_stage | **Stage 2 재진입** — 결함 주도. cycle-7 마감 후 사용자 제보 1건 |
| next_public_beat | gh-pages 라이브 — 처음 들어온 사람이 외부 문서 없이 플레이한다 |
| source_packet | 사용자 제보 + `.omc/specs/deep-interview-lobby-icon-rail.md` (R0+4R, 15%) |
| main_constraint | 순수 뷰 개정 — 심 무변경, 저장 필드 무변경, 밸런스 수치 무변경 |
| main_question | 로비 세 패널 중 두 개가 배포 빌드에서 도달 불가한 것을 어떻게 고치는가 |

## 운영 모드 (이번 사이클 하나만)

**결함 해소 + 내비게이션.** 신규 기능 없음. 컨셉 작업 없음.

## 사용자 제보

> "로비 화면 UI/UX 보면 들어가자마자 다 활성화되어 있어. 왼쪽에 맞춰서
> 정보·출정·지도 순으로 아이콘 3개를 선택해, 각각 눌렀을 때 활성화/비활성화
> 형식으로 수정하고 싶어."

## 인테이크가 재분류한 것

제보는 UI 선호로 도착했으나 **배포 결함**이다.
`[OBSERVED — 로컬 build-webgl 실제 구동, 헤드리스 크로뮴 실측]`

`build-webgl/index.html:18-20`이 캔버스를 CSS 1280×853에 고정한다. 창 크기와
무관하게 Unity가 보는 값은 동일하다:

| 브라우저 창 | 캔버스 CSS | E_w | 배치 |
|---|---|---|---|
| 3440×1440 | 1280×853 | 1176.0 | stacked |
| 1920×1080 | 1280×853 | 1176.0 | stacked |
| 1440×900 | 1280×853 | 1176.0 | stacked |
| 1024×768 | 1024×682 | 1176.3 | stacked |

`SideBySideFloor = 1248` (`LobbyView.cs:853`) → 항상 미달 → 항상 stacked.
1604u 열이 783.7u 캔버스에 들어간다. 최대 스크롤(여유 21px)에서 실측:

| 패널 | 보이는 양 | 시안선 실측 vs 예측 |
|---|---|---|
| SORTIE | 100% | 하단 690.9u vs 692.0u |
| SANCTUM | **13.5%** (75.7u) | 상단 708.4u vs 708.0u |
| MAP | **0%** | — |

MAP 상단 1284u > 캔버스 783.7u → 화면 밖이 아니라 **캔버스 바깥**. 루트
ScrollRect도 없어 도달 경로가 존재하지 않는다.

**정정**: 초안은 side-by-side를 넓은 창의 정상 경로로 봤으나, 브라우저는
어떤 창 크기에서도 그 배치를 만들지 못한다. 사용자가 본 "다 활성화"는 유니티
에디터 Game 뷰다 — 에디터는 `Screen.width/Height`를 Game 뷰 해상도에서 직접
읽어 HTML 템플릿을 우회하므로 16:9에서 E=1280이 되어 1248을 넘긴다.
플레이어는 예외 없이 stacked를 받는다.

라디오 레일은 1604u 열 자체를 없애므로 UI 정리가 아니라 이 결함의 수리다.

## 인터뷰가 확정한 것 (4R, 15%)

- 정보 = 기존 SANCTUM 그대로 (4탭 유지), 레일 라벨만 "정보"
- 라디오 — 항상 정확히 1개 active, 재클릭해도 안 닫힘, 기본 = 출정
- 티어 로직 삭제, 겹침 스윕 테스트를 **뷰포트 포함 불변조건**으로 교체
- MAP 내부 "지도" 버튼 → "전체 지도" 개명 (이름 충돌만 제거)
- 아이콘 90.2u 정사각 = 44 CSS px, 모든 티어 동일 (티어 분기 없음)

## 게이트 대상

| 게이트 | 사유 |
|---|---|
| G4 | 주 대상. 도달 불가 패널 2개 → 도달 가능, 3D 배경 658u 노출 |
| G1 | 신규 라벨 3종 + 개명 1종이 worldview 용어와 일치하는지 |
| G6 | 티어 분기 삭제로 코드 경로 감소. perf 예산 무변, 회귀 스윕 필요 |
| G2/G3/G5/G7/G8 | 무변경 — 밸런스·보상·코어루프·참신성 무접촉. 이월 |

## 미해결 위험 (승인 전 확정 필요)

**AC-13**: `_touchLayout` 입력이 `stack`이었다 (`LobbyView.cs:185-187`).
현재 배포(E=1176)는 stacked=true → 터치 피치 112/카드 106. 제안된
`E_w < 900` 규칙에서는 false → 70/68로 축소된다. 데스크톱에선 옳지만
`PrimarySortieActions_ClearThe44CssPxTouchFloor`가 어느 쪽으로 움직이는지
실측 전까지 미지. 승인 시 첫 번째 측정 대상.

## 캐리포워드 (건드리지 않음)

`.vscode/settings.json`, `Data/Plugins/lib_burst_generated.wasm`, `HongT.slnx`,
`Packages/*`, `ProjectSettings/PackageManagerSettings.asset` — 다른 세션 작업.
스테이징은 명시 pathspec만.
