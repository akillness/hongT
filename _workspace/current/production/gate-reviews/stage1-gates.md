# Stage 1 Gate Review — cycle 2 (run-id 20260805-dungeon-gimmicks)

판정자: game-production-director. 규칙: 측정값+방법+증거 경로 3종 필수.

## G7 draft — 코어 루프 (판정: PASS)

- 루프 모델: design/core-loop.md — N1(해류 리듬, 상위 웨이브 루프 25-40s),
  N2(선파괴 우선순위 30-60s), N3(행진 리듬 22.5s 고정). 액션≥3·보상≥1 각 명시.
- 구현 증거: EditMode 183/183 초록 [OBSERVED,
  engineering/unity-logs/test-results-185600.xml] — TideCurrent 푸시/대칭/클램프,
  EmberPylon a-d 계약, AshWall 킨매틱+틱, 결정론 D1/D3 전부 포함
  (qa/test-lane-cycle2.md 게이트 매핑).
- 스모크: 3스테이지 인게임 라이브 [OBSERVED, qa/smoke-*.webp 4장].
- 잔여: repeat-rate ≥70% 프록시는 배포 후 세션 측정(Stage 2 항목).

## G1 draft — 세계관 정합 (판정: PASS)

- 소스: design/worldview.md (3부작 계보·명명 규약·색 언어) 신설.
- 신규 가시 문자열 감사: 스테이지 3종(재의 수문/불씨 요새/재의 행진 — 법정
  기능 물화 규약 준수), 보스 3종(Keeper/Sentinel/Magistrate — 관료제 어휘),
  StoryCatalog 12비트(선고체, roster-spec 표 verbatim). 위반 0 [OBSERVED,
  view-lane-cycle2.md §3].
- 폰트 커버리지: HudKorean 재생성 456글리프 coverage FULL [OBSERVED,
  tools/gen_hud_font.sh 출력] — 신규 15자+잔여 5자 봉합.
- 주의(웨이버 아님): SpeechBubbleView.SpeakerColor가 신규 보스 prefix 미인식
  → watcher 색 폴백(기능 정상, 색만 미분화). 다음 사이클 1줄 수정 항목.

## G6-ops draft — 운영 준비 (판정: PASS)

- telemetry-contract: ops/telemetry-contract.md (localStorage 계약, R8 스키마
  불변) 신설. resource-manifest: engineering/resource-manifest.md — 신규
  임포트 0, 전 자산 재사용.
- 빌드: `result=Succeeded size=56845042 errors=0 warnings=0` [OBSERVED,
  unity-logs/build-185611.log] — 56.8MB ≤ 120MB.
- EditMode: 183/183 [OBSERVED]. 동시 텔레그래프 예산 ≤3 LCM 센서스 테스트로
  기계 고정(Telegraph_CensusUnderBudget).
- 잔여(G6 최종, Stage 3): rollback-runbook 리허설, release-readiness 체크리스트,
  퍼포먼스 소크(p95/메모리/입력) — 이번 사이클 범위 밖.

## G8 사전 판정 — 참신성 (조건부 PASS)

- 빈도: current 1/11 · pylon 1/11 · wall 2/11 — 전부 ≤2/≥5 [design/
  novelty-scorecard.md + trend-survey 빈도표, QA 분모 검증 완료].
- 인상 점수 ≥4/5: 미측정 — 배포 후 구조화 플레이테스트에서 확정(Stage 2).

## R1-R8 회귀 (판정: PASS)

- R1-R3: dotnet pre/post 12행 바이트 동일(추가성) + Unity 골든 15행 고정
  (DungeonGoldenDigestTests, 183/183 내) [qa/golden-digests-cycle2.md 런타임
  주의 포함].
- R4: StageTable_MatchesDungeonAmendment. R5: 166→183 성장.
- R6: 컴패니언 인바리언트 + EmberRest 6/7/8 시드 고정. R7: ReducedMotion
  다이제스트 불변. R8: 마스크 라운드트립 + 레거시 6비트 로드 테스트.
