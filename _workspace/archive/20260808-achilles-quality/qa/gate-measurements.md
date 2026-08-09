# Gate Measurements — live evidence ledger

QA owns this file; the director links these paths in every verdict. Every row
carries measured value + method + evidence. A claim missing any of the three is
FAIL regardless of how good the number looks.

Deploy frame for all rows unless stated: **E = 1176.0 × 783.7 u**, the only
frame `build-webgl/index.html:18-20` can produce (aspect-locked 1280:853, so
E is invariant under window size — verified over 640..3840 buffer widths).

---

## g4 — 이펙트·애니메이션 몰입감 (주 대상)

### g4.1 패널 도달률 `[MEASURED — 해석적, 패널 상수 대조]`

방법: 패널 상수(top, height)를 캔버스 유효 높이 783.7u와 대조. BEFORE 좌표는
구 `ApplyLobbyTier` stacked 분기, AFTER는 `PinPanel`.

| 패널 | BEFORE top | 보임 | % | AFTER | % |
|---|---|---|---|---|---|
| SORTIE | −72 | 620.0u | 100.0% | 620u | 100% |
| SANCTUM | −708 | 75.7u | **13.5%** | 560u | 100% |
| MAP | −1284 | 0.0u | **0.0%** | 320u | 100% |
| **평균** | | | **37.8%** | | **100%** |

### g4.2 성소 지출 UI 도달률 `[MEASURED]`

`TabContent` (`LobbyView.TabContent()`)는 `offsetMax.y = -116`, 즉 패널 상단
116u 아래에서 시작한다. BEFORE 가시량 75.7u < 116u:

```
지출 UI 가시량 = max(0, 75.7 − 116) = 0.0u  →  0%
```

보이던 75.7u는 전부 아이브로 + 탭 스트립 껍데기였다. **배포 빌드에 영구 강화
경제가 있고 구매 버튼 도달률이 0%였다** (D-1). 상단바 유물 카운터는 100%
가시 — 잔고는 늘 보이고 문은 없었다.

AFTER: 성소 선택 시 560u 전체 도달 → 지출 UI 100%.

### g4.3 3D 배경 노출 `[MEASURED — 캔버스 폭 기준]`

BEFORE stacked에서 SORTIE는 `anchorMin.x=0, anchorMax.x=1, sizeDelta.x=-32`
이므로 좌우 16u 여백만 남기고 캔버스 전폭을 덮는다.

| 상태 | 덮인 폭 | 배경 자유 폭 | % |
|---|---|---|---|
| BEFORE (SORTIE 전폭) | 1144u | 32u | 2.7% |
| AFTER 출정 선택 | 523u | **653u** | **55.5%** |
| AFTER 성소 선택 | 531u | 645u | 54.8% |
| AFTER 지도 선택 | 555u | 621u | 52.8% |

**20.4배.**

### g4.4 워든 가시성 `[OBSERVED — 실제 배포 빌드 구동, §4c]`

게이트 초안은 이 항목을 `[NOT MEASURED]`로 열어뒀다. 근거는 옳았다:
`LobbyStaging.cs:12-14`의 스팟은 `ViewWorld.ToWorld(simX, simY)` 3D 월드
좌표이고 화면 x는 배포 종횡비의 카메라 투영에 달렸으므로 캔버스 u와 직접
비교할 수 없다. 인터뷰 스펙(`:128-133`)이 "레일 x는 최소 디오라마 앵커
x=540보다 훨씬 왼쪽이라 아무것도 가리지 않는다"고 쓴 것은 **두 좌표계를
혼동한 것**이었다.

**해석으로 못 얻는 답이라 화면을 열었다.** WebGL 빌드 후 헤드리스 크로뮴:

| 상태 | 워든 | 보스 | 콘솔 |
|---|---|---|---|
| 출정 (기본) | — | 우측 가시 | 0 |
| 성소 | — | 우측 가시 | 0 |
| 지도 | **중앙하단 가시** | 우측 가시 | 0 |
| 지도 3회 클릭 | 중앙하단 가시 | 우측 가시 | 0 |

캔버스 실측 **CSS 1280×853, 1.0884 px/u** — 템플릿 잠금이 예측대로 동작한다
(브라우저 창은 1920이었다). 증거: `qa/lobby-rail-smoke/` 4장.

열린 653u가 실제로 디오라마를 드러낸다. 653u는 캔버스 폭 측정이었고,
이것이 그 폭 안에 무엇이 있는지에 대한 답이다.

### g4.5 에디터 프레임 역행 `[MEASURED — 출하 대상 외]`

사용자 스크린샷 1868×998 → E=1313.4 (≥1248이므로 side-by-side, 즉 에디터 Game
뷰). 그 프레임에서 캐릭터는 px[570,790] = u[401,555]에 있었고 SANCTUM(끝 416)과
MAP(시작 448.7) 사이 틈으로 보였다.

레일 이후 SANCTUM은 531.3에서 끝난다 → **그 프레임에서는 가림이 늘어난다.**

D-2가 에디터 티어를 출하 대상에서 제외했으므로 게이트 판정에 반영하지 않는다.
다음 사이클이 재발견하지 않도록 기록한다.

---

## g1 — 세계관 내 일관적 서사

### g1.1 신규 문자열 감사 `[OBSERVED — 브라우저 4상태 전수]`

| 문자열 | 위치 | worldview 근거 | 화면 확인 |
|---|---|---|---|
| 성소 | 레일 아이콘 0 | `Eyebrow("SANCTUM","성소 정비")` — 패널 제목과 일치 | 01·02 |
| 출정 | 레일 아이콘 1 | `Eyebrow("SORTIE","출정")` — 완전 일치 | 01 |
| 지도 | 레일 아이콘 2 | `Eyebrow("CAMPAIGN","심연 지도")` — 부분 일치 | 03·04 |
| 전체 지도 | MAP 내부 (개명) | 신규 조합. "지도"는 세계관 단어 | 03·04 |

D-5가 원안 "정보"를 기각했다: 세 라벨 중 유일하게 세계관 밖 단어였고, 그것이
여는 패널 제목이 "성소 정비"였다. 라벨 불일치를 새로 만들 뻔했다.

**두부 없음.** WebGL은 OS 폴백이 없어 서브셋에 없는 글자가 두부로 렌더된다
(§4b). 신규 글자는 전부 기존 서브셋에 있었고 — 성소·출정·지도·전체는 이미
패널 아이브로가 쓰는 단어다 — 스크린샷 4장에서 육안 확인했다. 폰트 재생성
불요.

---

## g5 — 매출·밸런스 시너지 `[재측정 사유 발생 — D-9]`

밴드 숫자는 무변경. **분모가 바뀌었다.**

`reward-bands.md`의 `steady.parity_sessions_band: [10, 20]`은 T5 도달까지의
세션 수다. 지출 UI 도달률이 0%면 실측값은 ∞ — 밴드 위반이 아니라 **밴드 정의
붕괴**다. 도달률 0%→100%는 이 게이트의 분모 변경이므로 이월 취소.

측정 필요: 레일 이후 실제 T5 도달 세션 수가 [10,20] 안에 드는지. 시뮬레이션
필요, 이번 사이클 범위 밖 → **cycle-9 첫 항목.**

---

## g6 — 게임운영 계획

### g6.1 코드 경로 감소 `[MEASURED — 컴파일 게이트]`

삭제: `SideBySideFloor` 상수, `_stacked` 필드, `StackedForTest` 심,
stacked/side-by-side 2분기(`ApplyLobbyTier` 내), 맵 패널 2분기.

`ApplySortieTouchLayout(stack)` → `ApplySortieTouchLayout(true)`. 구 인자의
false 아암은 84×28u 강하를 만드는데, 이는 **어떤 출하 구성에서도** 44px 플로어를
못 넘는다(배포 30.5px, 레터박스 11.9px). 도달 불가가 아니라 사용 불가 아암이었다.

### g6.2 테스트·빌드 `[OBSERVED]`

```
EditMode   808 / 808 passed, 0 failed   (test-results-162755.xml)
변이 스윕  14 / 14 검출                  (qa/mutation-sweep-cycle8.json)
WebGL      result=Succeeded errors=0 warnings=1  88.1MB
           (build-165822.log)
브라우저   5상태, 콘솔 에러 0, pageerror 0  (qa/lobby-rail-smoke/)
           D-12 이후 재측정. 초안 4장 중 2행(기본값=출정, 3회 클릭 후
           열림)은 D-12가 뒤집은 동작이라 폐기하고 다시 찍었다.
```

변이 첫 스윕은 **11/14**였다. 못 잡힌 셋(D-3 터치 기하, D-7 배지, D-8 레일
3개 동결)이 **전부 만장일치 결정**이었고 사이클 내에 테스트 4종을 추가해
닫았다. 논쟁이 있었던 결정은 무방비가 0건이다 — 상세는
`production/gate-reviews/stage2-gates-v21.md#변이-증명`.

### g6.3 perf 예산 `[UNCHANGED — 재측정 불요]`

패널 3개 중 2개가 비활성이 되므로 캔버스 배치·드로 대상이 줄어든다. 예산
방향이 개선이고 신규 per-frame 작업이 없다(`SelectRail`은 클릭 시 1회,
할당 없음). p95 프레임·입력 지연 재측정 불요.

---

## g2 · g3 · g7 · g8

이 변경이 밸런스 수치·보상 밴드·코어루프 모델·참신성 요소를 건드리지 않는다.
전 사이클 값 이월. 단 D-9의 약이의 인용: G7 반복률 기준선은 레일 이후 로비
동선이 바뀌므로 cycle-9에서 재설정한다.

---

## 이월 부채 (측정됨, 이번 사이클 미해소)

| # | 항목 | 측정값 | 만료 |
|---|---|---|---|
| 1 | 강하 액션 92u — 지원 플로어 375에서 미달 | 40.3px / 44px (−3.7) | cycle-9 |
| 2 | 레일 아이콘 103.3u — 범위 밖 320 CSS에서 미달 | 38.6px / 44px (−5.4) | 범위 밖, 무기한 |
| 3 | 장비·각인 2중 구현 (LobbyView vs MetaScreenView) | 2곳 | cycle-9 |
| 4 | G5 parity 밴드 재측정 | 미측정 | cycle-9 |

---

## g8 — cycle 9 entry novelty and impression

### g8.1 frequency `[UNVERIFIED]`

Measured value: `tide-current = 1/11`, but the sole positive source cell is
`ETG(t)` thin evidence.

Method: read the 11-title frequency table and provenance labels in
`design/trend-survey/dungeon-gimmick-trends.md`; compare the
`Conveyor / current / push field` row with the `≤2 of ≥5` gate.

Evidence:
`design/trend-survey/dungeon-gimmick-trends.md#frequency-table-g8-input` and
`design/novelty-scorecard.md`. The current run has not revalidated the thin
source cell or denominator, so this leg cannot PASS yet.

### g8.2 synthetic blind panel `[MEASURED — directional, not human closure]`

Measured value: five independent reviewer-subagent scores
`[3, 3, 4, 3, 4]`; median **3/5**. Unprompted first recall grouped as
current/lane `3/5` and orange telegraph `2/5`.

Method: Playwriter recorded live deployed combat. Four frames were cropped to
remove the stage title and objective banner because those strings named the
mechanic. Five independent raters received randomized frame orders without the
candidate or benchmark label, named one recalled element first, then applied
the 1–5 memorability anchors.

Evidence: `qa/cycle9-g8-ballots.md`,
`qa/cycle9-g8-entry-raw.mp4`, and
`qa/cycle9-g8-anon-{a,b,c,d}.png`.

Interpretation: the panel consistently recognizes the lane/hazard event, but
counter timing and outcome are not clear enough. This is a synthetic LLM panel
over real-combat captures, not a human playtest; it may direct presentation
work but cannot replace the required human/real-combat session.

### g8 verdict

**FAIL / human-blocked.** Impression median `3/5 < 4/5`; the current/lane
first-recall pair is `3/5 > 2/5`; and the thin frequency source remains
unverified. Cycle 9 does not advance by carry-over.
