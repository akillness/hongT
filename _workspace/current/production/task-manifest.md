# Task Manifest — run-id 20260805-dungeon-gimmicks

next_public_beat = "gh-pages 라이브: 신규 던전 + 던전별 고유 기믹 플레이 가능"

| task | owner | stage.phase | artifact | gate | status |
|---|---|---|---|---|---|
| 워크스페이스 분석 + 게임 기획 전체 분석 | director | 1.a | production/design-analysis.md | — | done |
| 코드 지형 맵핑(스테이지/기믹 시임) | programmer | 1.a | engineering/dungeon-code-map.md | — | done |
| 던전 기믹 트렌드 서베이 | designer | 1.a | design/trend-survey/dungeon-gimmick-trends.md | G8 | in-progress |
| QA 벤치마크 서베이 + 테스트 플랜 | qa | 1.a | qa/benchmark-notes.md, qa/test-plan.md | — | in-progress |
| 던전 로스터 + 기믹 스펙 | designer | 1.b | design/dungeon-roster-spec.md | G7,G8 | pending |
| 밸런스 시트(기믹 수치) | designer | 1.b | design/balance-sheet.md | G2 | pending |
| 참신성 스코어카드 | designer | 1.b | design/novelty-scorecard.md | G8 | pending |
| 협상 1R: 보상 커플링 | designer+pm | 1.c | pm/negotiation-record.md, pm/reward-bands.md | G5 | pending |
| SIM_SPEC_DUNGEONS.md 증분 스펙 | director | 1.d | docs/SIM_SPEC_DUNGEONS.md | G1 | pending |
| 신규 HazardKind 심 구현 + 테스트 | main-lane (FROZEN 목록 전담) | 1.d | Assets/Scripts/Sim/*, Assets/Tests/EditMode/* | G7 | pending |
| StageCatalog 확장 + 뷰 텔레그래프 + 마스크 마이그레이션 | programmer (View 편집 전 `git status --short` — damage-number 병행 세션) | 1.d | Assets/Scripts/View/* | G7 | pending |
| EditMode 게이트 + 배포 스모크 (P2 글로우·C1 트레일 육안 검증 포함 — cycle-1 이월) | qa | 1.d | qa/gate-measurements.md | G6 | pending |
| 게이트 리뷰 G7/G1/G6 draft + G8 사전 판정 | director | 1.gate | production/gate-reviews/ | G7,G1,G6,G8 | pending |
| 사이클 회고 + 룰 파일 재도출 | director | close | retrospectives/cycle-2-retrospective.md | — | pending |

## 이월 백로그 처리 (cycle-1 회고 대비)

- **이번 사이클 채택**: 던전 다변화(6스테이지 전제는 이미 완료 — 719a587로
  확인, 카탈로그 6종 라이브. 이번엔 그 너머 신규 기믹/던전), P2·C1 육안 스모크.
- **의도적 이월(이번 범위 밖 — one operating mode per cycle)**:
  Ember Rest UI 심화(심 시임 완비, View 소비자 확대), 가디언 hold/recall §S
  게이트 검증 패스, nan2026 제출 패키지(HTML/PDF/영상). 다음 사이클 후보.

## 코드 시임 요약 (engineering/dungeon-code-map.md 전문 참조)

- 신규 기믹 종류 = D 경로: AMENDMENT 문서 → CampaignTypes(HazardKind/
  HazardConfig/CampaignSpec/HazardState) → CinderSim.UpdateHazards 분기 →
  VfxDirector.SyncHazards 렌더 분기 → 배치 테이블 → CampaignSimTests.
- 신규 논리 스테이지 = A 경로(동결 무편집): StageEntry 추가 + ValidClearMask
  확장 + StoryCatalog 비트 + StageCatalogTests 6종 하드코딩 갱신.
- 6스테이지 초과 시 파급: EmberRest 룸 인덱스 1..5 검증(CinderSim.cs:532 —
  FROZEN 아님이지만 심 편집), 로비 카드 70px 피치(스크롤 필요), ClearedMask
  "bits 0-5" 계약, 터미널 스테이지 판정.
