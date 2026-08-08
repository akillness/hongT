# Triage

- Problem: Cinder Court 로비가 플레이어에게 **"지금 어디 / 다음 무엇 / 왜 잠김"**을
  세 질문 모두 답하지 못한다. SORTIE 패널은 1058u 콘텐츠를 434u 뷰포트에 담아
  **가시율 41.0%**인데 `ScrollRect`에 Scrollbar가 미할당이라 위치를 알려주는 픽셀이
  **0**이다(→ "지금 어디" 무응답). 카드 상태 문자열은 `"정화 완료" / "강하 가능" /
  "잠김"` 3개뿐이고 `StageEntry.PrereqId`가 데이터에 존재하는데도 화면에 없다
  (→ "왜 잠김" 무응답). SANCTUM 4탭에는 배지가 없어 지금 살 수 있는 것이 어느 탭에
  있는지 탭을 눌러야만 안다(→ "다음 무엇" 무응답). 게다가 9스테이지가 평평한 한 줄이라
  worldview가 정의한 3부작(기록/증언/집행) 서사 분절이 UI에 존재하지 않는다.
  본 서베이는 구현 전에 **로그라이트/액션RPG 장르가 이 세 질문에 쓰는 UI 문법**을
  실측 빈도로 확정한다. 조사 전용이며 코드는 건드리지 않는다.

- Audience:
  - **1차 — Stage 1b 설계자/구현자**: 이 문서의 11축 빈도 교차표가 무엇을 채택하고
    무엇을 기각할지의 유일한 근거다. 특히 G8(참신성) 판정은 빈도 ≤2/N 축에서만 나온다.
  - **2차 — PM(`PMNavRevenueMap`)**: 배지 축(N5/N11)이 각인 5종의 도달률
    스프레드(100% ~ 11%, 9배)와 직접 충돌한다. 빈도표가 "탭 배지 단독 채택"의
    기각 근거를 제공한다.
  - **3차 — QA(`QANavBenchmark`)**: 계측축 (a)조작수↔N2, (c)잠금사유 확인 조작수↔N3,
    (d)동시가시비율↔N1로 매핑 합의됨. 표본 6타이틀 공유(Hades / Hades II / StS /
    Dead Cells / Rogue Legacy 2 / Vampire Survivors).
  - **4차 — 신규 플레이어**: 실패 사례 6건이 전부 "외부 위키 없이는 진행 못 함"으로
    수렴한다. 이 문서가 막으려는 최종 피해자.

- Why now:
  - **사용자가 명시 요청**: "현재 캠페인 & 강화에서 어떻게 게임이 진행되는지 네비게이션
    기능이 필요해. survey로 우리 게임 장르에 맞게 조사해서 우리 게임 색깔에 맞게
    네비게이션 구현해줘" — 즉 조사가 구현의 선행 조건으로 지정됐다.
  - **콘텐츠가 임계를 넘었다**: cycle-2 던전 확장으로 스테이지가 6→9로 늘면서 스크롤
    콘텐츠가 1058u가 됐다. 6스테이지 시절(=(6+1+5)×70+8=848u, 가시율 51.2%)에는
    절반이 보였지만 지금은 41.0%다. 다음 확장이 오면 더 나빠진다. 네비게이션 부채는
    **콘텐츠 증가와 함께 복리로 커진다**.
  - **하드 제약이 지금 결정을 강제한다**: 터치 하한 래칫(`LobbyLayoutTests.cs`)이
    44 CSS px 미만 컨트롤의 정확한 집합을 동결하고, 폰트는 서브셋(실측 498 글리프)이며,
    심 수치는 FROZEN이다. 제약을 모르고 장르 관례를 그대로 베끼면 래칫 위반이나
    글리프 탈락으로 되돌아온다. 실제로 이 조사 중 **`·`(U+00B7)가 View 소스 6곳 이상에서
    쓰이는데 배포 폰트 cmap에 없음**을 발견했다(§context.md Adjacent Problems).
  - **선행 서베이 3건이 이미 이 화면 위에 쌓여 있다**: `dungeon-gimmick-trends`,
    `meta-upgrade-gimmick-interaction`, `roguelike-training-and-surge`.
    시련 5종·각인 5종·등급선택이 전부 SORTIE 스크롤에 얹히면서 1058u가 됐다.
    네 번째 기능을 얹기 전에 **담는 그릇을 먼저 조사**해야 한다.

---

## 조사 질문 (하나로 고정)

> **로그라이트/액션RPG의 메타 진행 화면은 플레이어에게 '지금 어디, 다음 무엇, 왜 잠김'을
> 어떤 UI 문법으로 알려주는가?**

## survey_run

```yaml
survey_run:
  primary_mode: market-landscape
  scope: medium
  evidence_floor: indexed-snippets-allowed
  output_language: user-language(한국어)
  needs_platform_map: false
  reuse_existing: false      # .survey/progression-navigation/ 신규
  run_id: 20260807-progression-navigation
```

## 표본과 축

- **표본 18 타이틀**(로그라이트 메타 진행 화면 보유) + 장르 밖 참고 1(Diablo IV
  Season Journey). 요구 하한 11 타이틀을 상회.
- **축 11개**: 지정 10축(N1~N10) + PM 입력으로 추가된 **N11 컨텍스트 반영 추천**.
  N11은 "배지가 '살 수 있나'가 아니라 '다음 출정에 효과가 있나'를 반영하는가"를 묻는다.
  각인이 기믹 바인딩이라 우리 게임에만 컨텍스트가 실재하기 때문에 추가했다.

## 증거 규칙

- 검색은 영어, 산출은 한국어.
- 확인 못 한 셀은 `?`로 남기고 **N에서 제외**한다. 추측으로 채우지 않는다.
- 표기: `[OBSERVED]` 리포지토리 실측 / `[INFERENCE]` 추론 / `[TARGET]` 목표치.
- 출처 강도 라벨: `indexed snippet`(검색 인덱스 경유) / `direct page retrieval`(원문 직접).
  본 런은 전 항목 `indexed snippet`이며, 리포지토리 사실만 `direct page retrieval`이다.
