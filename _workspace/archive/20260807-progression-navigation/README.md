# Archive — run-id 20260807-progression-navigation (cycle 4)

읽기 전용. `git mv`로만 들어오고, 편집·삭제하지 않는다.
회고: `_workspace/current/retrospectives/cycle-4-retrospective.md`.

cycle-5 마감(2026-08-07) 시점에 소진된 자료만 옮겼다.

| 파일 | 왜 옮겼나 |
|---|---|
| `production/gate-reviews/stage1-gates-v16.md` | cycle-3 판정. v18이 현행 |
| `production/gate-reviews/stage1-gates-v17.md` | cycle-4 판정. v18이 현행 |
| `qa/nav-smoke/` | cycle-4 로비 네비게이션 스모크 6장. 그 사이클 전용 증거 |

## 남긴 것과 이유

- `design/progression-navigation-spec.md` — **라이브 소스가 참조한다**
  (`ProgressionGuide.cs:1`, `LobbyView.cs:144`, `ProgressionNavigationTests.cs:1`).
  옮기면 세 경로가 끊긴다.
- `design/training-and-surge-spec.md` — 여섯 경로가 참조 (cycle-4 아카이브 참조).
- `qa/golden-digests-cycle2.md` — `DungeonGoldenDigestTests.cs:2/7`이 인용.
- 누적 문서(`gate-measurements` `test-plan` `benchmark-notes`
  `negotiation-record` `revenue-map` `novelty-scorecard` `task-manifest`) —
  사이클마다 절을 덧붙이는 형태라 현행이 곧 전체 이력이다.
- `retrospectives/` 전체 — 스튜디오 기억. 아카이브하지 않는다.

**아카이빙 기준**: 라이브 소스나 현행 문서가 경로로 인용하면 남긴다.
사이클 종료로 소진된 보고서·증거만 옮긴다.
