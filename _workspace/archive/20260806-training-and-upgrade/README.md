# Archive — run-id 20260806-training-and-upgrade (cycle 3)

읽기 전용. `git mv`로만 들어오고, 편집·삭제하지 않는다.
회고: `_workspace/current/retrospectives/cycle-3-retrospective.md`.

cycle-4 마감(2026-08-07) 시점에 소진된 레인 자료만 옮겼다.

| 파일 | 왜 옮겼나 |
|---|---|
| `production/change-summary-cycle2.md` | cycle-2 변경 요약. 회고로 대체됨 |
| `production/gate-reviews/stage1-gates.md` | cycle-2 판정. v16/v17이 현행 |
| `qa/test-lane-cycle2.md` | cycle-2 테스트 레인 기록 |
| `engineering/view-lane-cycle2.md` | cycle-2 뷰 레인 기록 |

## 남긴 것과 이유

- `design/training-and-surge-spec.md` — **라이브 소스가 참조한다**
  (`CampaignTypes.cs:408`, `CinderSim.cs:2809`, `HackTypes.cs:554`,
  `CampaignStore.cs:33`, `TrainingSurgeTests.cs:2`, `docs/SIM_SPEC_HACKSLASH.md:502`).
  옮기면 여섯 경로가 끊긴다.
- `qa/golden-digests-cycle2.md` — `DungeonGoldenDigestTests.cs:2/7`이 수치 진실로
  인용한다.
- 누적 문서(`novelty-scorecard` `gate-measurements` `test-plan`
  `benchmark-notes` `negotiation-record` `revenue-map` `task-manifest`) —
  사이클마다 절을 덧붙이는 형태라 현행이 곧 전체 이력이다.
- `retrospectives/` 전체 — 스튜디오 기억. 아카이브하지 않는다.

**아카이빙 기준**: 라이브 소스나 현행 문서가 경로로 인용하면 남긴다.
사이클 종료로 소진된 보고서만 옮긴다.
