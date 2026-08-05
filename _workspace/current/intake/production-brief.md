# Production Brief — run-id 20260805-dungeon-gimmicks

- game_type: 탑다운 핵앤슬래시 (Unity WebGL, 결정론 60Hz 심)
- team_shape: 솔로 오케스트레이터(하니스 역할 순차/병렬 수행, 서브에이전트 위임)
- engine: Unity 6000.5.6f1 + URP 17.5, WebGL
- current_stage: Stage 1 재진입 (컨셉 확장 — cycle-1 회고 결정)
- next_public_beat: gh-pages 라이브 갱신 — 신규 던전 N종이 로비 카드·해금 체인·기믹과 함께 플레이 가능
- source_packet: cycle-1 회고 이월 백로그 + 사용자 지시(2026-08-05: "던전 여러 개 추가, 던전마다 기믹 부여, survey로 유사 장르 조사, 회의 후 개발") + deep-interview-cinder-court-dungeon-revival.md(존속 세계관 레퍼런스)
- main_constraint: Sim FROZEN CONTRACT(SimTypes/CampaignTypes/HackTypes + SIM_SPEC 3종) 무편집 — 증분은 추가형 AMENDMENT 문서+비동결 시임으로만. 결정론·무RNG·무할당·≤120MB·EditMode 166/166 유지. 위임 에이전트는 FROZEN 파일 편집 금지(AGENTS.md) — 동결 해제 목록 편집은 메인 레인 전담.
- main_question: 신규 던전이 "해저드 조합 재배치"를 넘어 **기믹 종류 자체**를 늘리는가 — 기존 6스테이지는 Vent/Pillar/Altar 3종의 배치 조합일 뿐(G8 참신성 리스크).

## Operating mode: Stage 1 컨셉 확장 — 던전 다변화 사이클

1. Phase 1a (병렬): 디자이너 트렌드 서베이(.survey/dungeon-gimmick-trends) ∥ QA 벤치마크 서베이+테스트 플랜 ∥ 코드 지형 맵핑
2. Phase 1b: 던전 로스터 스펙(신규 스테이지 id/서사/보스/기믹) + 밸런스 시트 + 참신성 스코어카드
3. Phase 1c: 디자이너↔PM 협상 1라운드(보상 커플링 — 클리어 포인트/유물/동료 해금)
4. Phase 1d: 빌드 — SIM_SPEC_DUNGEONS.md 증분 스펙 → 신규 HazardKind 심 구현(+테스트) → StageCatalog 확장 → 로비/HUD/뷰 텔레그래프 → EditMode 게이트
5. 게이트: G7 draft, G1 draft, G6-ops draft, G8 사전 판정(서베이 빈도표 대비)

## 워크스페이스 결정 기록

- cycle-1 산물 아카이브: `_workspace/archive/20260805-visible-impact/` (intake/design/qa/engineering 증거 — cycle2-spec.md는 A/B 항목이 커밋으로 구현 확인되어 완료 스펙으로 아카이브).
- `engineering/icons/`·`engineering/reskin/`은 라이브 도구 참조(tools/blender/reskin_all.sh, tools/icons/*, docs/nan2026)라 current에 존치 — 의도적 분할.
- `design/deep-interview-cinder-court-dungeon-revival.md`는 세계관/캠페인 존속 레퍼런스로 current 유지 (G1 소스).

## 이월 리스크 (cycle-1 회고)

- P2 글로우·C1 트레일 라이브 육안 검증 미수행 → 이번 사이클 QA 스모크에 포함.
- 타 세션이 damage-number 경로 병행 확장 — GameView 편집 시 충돌 주의(§5 git 안전 수칙).
- CharacterRosterAnimationTests.cs는 타 세션 소유(conflicts.md) — 건드리지 않음.
