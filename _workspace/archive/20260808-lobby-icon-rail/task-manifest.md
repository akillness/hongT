# Task Manifest — run-id 20260808-lobby-icon-rail

next_public_beat = "gh-pages 라이브 — 처음 들어온 사람이 외부 문서 없이 플레이한다"

AMENDMENT #11. 전 사이클(20260807-codex-readability)은 cycle-7 회고로 마감.
결함 주도 Stage 2 재진입. 선행: `.omc/specs/deep-interview-lobby-icon-rail.md`
(Round 0 + 4R, 15%) → 5역할 회의 `production/decision-log.md` (D-1~D-11).

**상태: 마감.** 게이트 G1·G4·G6 PASS, G5 재측정 이월. S1 0건, 블로커 0건.

회의가 스펙을 4곳 뒤집었다. 아래 표는 스펙이 아니라 **회의 결과**를 따른다.

| # | 스펙 원안 | 회의 확정 | 근거 |
|---|---|---|---|
| 1 | 아이콘 90.2u | **103.3u** | D-4 (숫자), D-11 (근거 교체) |
| 2 | 레일 라벨 "정보" | **"성소"** | D-5 — 세계관 밖 단어 유일 |
| 3 | AC-13 `E_w < 900` | **`_touchLayout = true` 상수** | D-3 만장일치 기각 |
| 4 | G5 이월 | **G5 재측정** | D-9 — 도달률 0→100은 분모 변경 |

| task | owner | stage.phase | artifact | gate | status |
|---|---|---|---|---|---|
| deep-interview 요구사항 확정 | director | 0 | .omc/specs/deep-interview-lobby-icon-rail.md | — | done |
| 인테이크 브리프 | director | 0 | intake/production-brief.md | — | done |
| 결함 재현 (해석적 좌표) | director | 2.a | 아래 §재현 | G4 | done |
| 5역할 설계 회의 | director | 2.b | production/decision-log.md D-1~D-10 | — | done |
| 터치 플로어 기준 중재 | director | 2.c | production/decision-log.md D-11 | G4 | done |
| 아이콘 3종 생성 | programmer | 2.d | Assets/Resources/Icons/ui-{sanctum,sortie,map}.png | G4 | done |
| 레일 빌드 + 라디오 상태 | programmer | 2.d | View/LobbyView.cs | G4 | done |
| 티어 로직 삭제 + 터치 고정 | programmer | 2.d | View/LobbyView.cs | G6 | done |
| MAP 버튼 개명 | programmer | 2.d | View/LobbyView.cs:330-337 | G1 | done |
| 뷰포트 담김 불변조건 테스트 | qa | 2.d | Tests/EditMode/LobbyContainmentTests.cs | G4 | done |
| 겹침 스윕 + 지도 액션 테스트 갱신 | qa | 2.d | Tests/EditMode/LobbyLayoutTests.cs | G4 | done |
| EditMode 전체 회귀 | qa | 2.d | test-results-145644.xml — 808/808 | G6 | done |
| 변이 스윕 | director | 2.d | qa/mutation-sweep-cycle8.json — 14/14 | G6 | done |
| 무방비 3건 닫기 (D-3·D-7·D-8) | qa | 2.d | Tests/EditMode/LobbyLayoutTests.cs | G6 | done |
| WebGL 빌드 + 브라우저 스모크 (§4c) | director | 2.d | qa/lobby-rail-smoke/ — 콘솔 0 | G4 | done |
| 게이트 판정 | director | 2.gate | production/gate-reviews/stage2-gates-v21.md | G1·G4·G5·G6 | done |
| 회고 | director | close | retrospectives/cycle-8-retrospective.md | — | done |
| 룰 파일 재도출 (§4q·4r·4s) | director | close | CLAUDE.md | — | done |

## 재현 — 배포 빌드 패널 도달 불가 `[OBSERVED]`

`build-webgl/index.html:18-20`이 캔버스를 1280:853에 고정한다. 그 종횡비의
유효 폭은 배율 불변이다 (log-lerp, match=0.5, 기준 1280×720):

```
640x426  -> E_w = 1176.7      1920x1280 -> E_w = 1175.8
1280x853 -> E_w = 1176.0      2560x1706 -> E_w = 1176.0
1600x1066-> E_w = 1176.1      3840x2559 -> E_w = 1176.0
```

`SideBySideFloor = 1248` (LobbyView.cs:877) → **1176 < 1248, 항상 stacked.**

stacked 열 1604u vs 유효 높이 783.7u:

```
SORTIE   top  −72   bottom  −692    보임 620.0u  100.0%
SANCTUM  top −708   bottom −1268    보임  75.7u   13.5%
MAP      top −1284  bottom −1604    보임   0.0u    0.0%
                                    초과 820.3u
```

루트 ScrollRect 없음 (`_stageScroll` :1188은 SORTIE 내부 전용).

D-1이 더 나쁜 사실을 확정했다: 보이는 75.7u < `TabContent.offsetMax.y` 116u
이므로 SANCTUM 탭 콘텐츠는 **0% 가시**다. 배포 빌드에 영구 강화 경제가 있고
지출 UI 도달률이 0%다.

기존 테스트가 통과한 이유: `LobbyPanels_NeverOverlap_AtAnyEffectiveWidth`
(:324)는 패널 **상호 겹침**만 검사한다. 뷰포트 담김은 검사하지 않는다 — 그
누락은 실수가 아니라 `:317-321`에 기록된 결정이었고, **두 번째 테스트를 쓰지
않은 것이 결함**이다 (D-10).

## 확정 기하 (D-4 + D-11)

```
아이콘 103.3u 정사각, 간격 12u, 레일 x=[16, 119.3]
  성소  y = [ −72.0, −175.3]
  출정  y = [−187.3, −290.6]
  지도  y = [−302.6, −405.9]
레일 총고 333.9u  (배포 가용 711.7u)
패널 x = 131.3,  y = −72,  네이티브 크기 (400×560 / 392×620 / 424×320)
MAP 우측 끝 555.3 < 1176
SORTIE 선택 시 3D 배경 653u 노출
```

**지원 플로어 = 375×667 CSS (iPhone SE2).** D-11이 명명했다. 320·280은 명시적
범위 밖 — 미달을 부채로 측정해서 적되 통과를 주장하지 않는다.

담김 실측 (전 티어, 선택 패널 + 레일):

| 티어 | E | 가용 h | 최광 우측끝 | 판정 |
|---|---|---|---|---|
| 배포 3:2 | 1176.0×783.7 | 711.7 | 555.3 | OK |
| 에디터 16:9 | 1280.0×720.0 | 648.0 | 555.3 | OK |
| iPhone 12 | 798.7×1728.6 | 1656.6 | 555.3 | OK |
| iPhone SE2 (플로어) | 855.5×1521.6 | 1449.6 | 555.3 | OK |
| 레터박스 501 | 1175.8×783.8 | 711.8 | 555.3 | OK |
| Pixel 7 | 791.5×1757.9 | 1685.9 | 555.3 | OK |

## 보존 대상 (삭제 금지)

- `ApplyLobbyLayoutForTest` 심 — `ProgressionNavigationTests.cs:1138-1140`,
  `LobbyLayoutTests:547`이 호출한다. 티어 분기만 죽이고 패스 강제는 남긴다.
- `LastEffectiveWidth` — 테스트가 스케일러 결합 왕복 검증에 쓴다.
- `MetaScreenView` 전체 — 이번 사이클 무접촉.
- 강하 액션 92×92u — D-3이 유지 확정. 375 플로어에서 40.3px 미달은 D-11이
  이월 부채로 기록, 만료 cycle-9.

## 이월 부채 (cycle-9 만료)

| # | 항목 | 출처 |
|---|---|---|
| 1 | 장비·각인 2중 구현 (`LobbyView` vs `MetaScreenView`) — 생존자 1개 지명 | D-6 |
| 2 | 강하 액션 92u, 지원 플로어 375에서 40.3px (3.7px 미달) | D-11 |
