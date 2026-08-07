# Task Manifest — run-id 20260806-training-and-upgrade

next_public_beat = "gh-pages 라이브 — 훈련이 반복 가치를 갖고, 강화가 돌발까지 다룬다"

전 사이클(20260805-dungeon-gimmicks)은 마감됨. 이 매니페스트가 현행이다.

| task | owner | stage.phase | artifact | gate | status | beat |
|---|---|---|---|---|---|---|
| 인테이크 브리프 | director | 0 | intake/production-brief.md | — | done | 위 beat |
| 훈련/강화 트렌드 서베이 | designer | 1.a | design/trend-survey/training-and-upgrade.md (.survey 미러) | G8 | done | 위 beat |
| 돌발 이벤트 벤치마크 서베이 + 테스트 플랜 | qa | 1.a | qa/benchmark-notes.md, qa/test-plan.md | — | done | 위 beat |
| 수익 포인트 초안 | pm | 1.a | pm/revenue-map.md | — | done | 위 beat |
| 훈련 리워크 + 강화 축 스펙 | designer | 1.b | design/training-and-surge-spec.md | G7,G8 | done | 위 beat |
| 참신성 스코어카드 갱신 | designer | 1.b | design/novelty-scorecard.md | G8 | done | 위 beat |
| 협상 1R (신규 보상 커플링) | designer+pm | 1.c | pm/negotiation-record.md entry 7-9 | G5 | done | 위 beat |
| AMENDMENT #10 수치 계약 | director | 1.d | docs/SIM_SPEC_HACKSLASH.md §14/§15 | G1 | done | 위 beat |
| 훈련 리워크 심 구현 | programmer | 1.d | Sim/CampaignTypes.cs TrainingTrials, CinderSim.UpdateTraining | G7 | done | 위 beat |
| 돌발 기믹 심 구현 | programmer | 1.d | Sim/CinderSim.cs UpdateSurge + 5 각인 조항 | G7,G8 | done | 위 beat |
| 뷰·세이브 배선 | programmer | 1.d | View/{LobbyView,GameDirector,HudView,CampaignStore}.cs | G7 | done | 위 beat |
| AMENDMENT #10 EditMode 커버리지 | qa | 1.d | Assets/Tests/EditMode/TrainingSurgeTests.cs | G7,G8 | in-progress | 위 beat |
| 게이트 측정 | qa | 1.d | qa/gate-measurements.md §v1.6 | G7,G1,G6 | done | 위 beat |
| 게이트 판정 G7/G1/G6 draft | director | 1.gate | production/gate-reviews/stage1-gates-v16.md | G7,G1,G6 | done | 위 beat |
| 사이클 회고 + 룰 파일 재도출 + 아카이브 | director | close | retrospectives/cycle-3-retrospective.md | — | in-progress | 위 beat |

## 교차 세션 주의 (이 사이클 시작 시점 실측)

워킹트리에 **다른 세션의 미커밋 작업 15파일**이 있다 — 클립 리타이밍
(`Assets/Art/Motion/CinderActor.controller`, `Assets/Editor/CharacterImportPipeline.cs`,
`ClipLengthProbe.cs`, `ClipMotionProbe.cs`, `ClipWindowTests.cs`), HudView 필-렌더
계약(`HudView.cs`, `HudLayoutTests.cs`), ActorView roar, equip 머티리얼 4종.

**이 사이클은 위 파일을 건드리지 않는다.** 편집 전 `git status --short`,
스테이징은 명시 pathspec만 (CLAUDE.md §5).

## 이월 (범위 밖)

- gh-pages 배포 — origin 403, 사람 판단
- 로비 44px 터치 하한 — designer+pm 협상 안건
- ash-march 과열 최종 판정 — 사람 플레이테스트
- 각인 가격 서명 — negotiation entry 6
