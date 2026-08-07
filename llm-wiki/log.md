# Log

- 2026-08-07 — ingest: 외부 핵앤슬래시 디자인 가이드 캡처
  (`raw/sources/2026-08-07-hackslash-design-guide-reference.md` +
  `wiki/sources/` 요약). 용도: 기획-구현 대조 감사 비교축.
- 2026-08-07 — audit 개시: 서브에이전트 5기(주요항목/드롭률/연출/보스·플레이어/
  레벨디자인) 병렬 대조 감사. 결과는 `wiki/reports/`에 파일링 예정.
- 2026-08-07 — audit 완료 + lint: 종합 리포트
  `wiki/reports/2026-08-07-spec-vs-impl-audit.md` 파일링 (High 5·Med 9·의사결정
  D1-D12). 레인별 증거는 `_workspace/current/qa/audit-20260807/`. 볼트 정비:
  AGENTS.md 신설, 부족 디렉토리 4종 생성, 개념 페이지 3종 `wiki/concepts/` 이동,
  raw 위키링크 → 백틱 경로 전환 (lint 깨진 링크 0건).
- 2026-08-07 — PR #3 리뷰·머지 + 감사 후속 착수: stale-base 회귀 2건(비트23
  충돌, A9 HUD 소등)을 머지에서 수정(685605f) → 저자 재머지(f747168) 수렴
  (c14d44e, 골든 9행 Unity 재기록본 채택) → PR #3 MERGED. 후속 4건 구현
  (7ad0d5f): M2 P3 대사 9종, G-2 EquipCosts 단일화(레인 취소 — 머지가 선반영),
  M7 reduced-motion 자동감지, M9 드롭 경제 테스트 40케이스. H4는 타 세션
  57f8afd가 선해소. 상세: `_workspace/current/conflicts.md` 2026-08-07 항목.
- 2026-08-07 — 빌드·배포·모니터링: PR 잔여 0건 확인 → 잔여 변경 커밋(de476cc)
  → /tmp/hongt-build 워크트리(웜 Library)에서 EditMode **502/502** +
  WebGL 빌드(70.5MB, 0 error) → gh-pages `b4ef488` 배포 → 라이브 스모크:
  부팅 0 에러, M7 OS 힌트 시드('al:os-reduced-motion'=0), 로비 각인 탭·Epithet,
  던전 딥링크·목표 칩·전투·드롭(랜턴 랭크+1 = kills%3 경로)·퀵 리트라이 확인.
  모멘텀 HUD는 게이트 검증(14 테스트)·배선 확인, 육안은 미확인(봇이 분출구에
  사망 — 게임 결함 아님).
- 2026-08-07 — merge: 마지막 미병합 브랜치 `origin/docs/ai-native-builder-flow`
  (cb82427, 분기점 2db4942 = main-94) 합류(`af85491`). 경로 충돌 0건이었으나
  내용이 분기 시점에 고정돼 EditMode 166/166·6단계 던전·`_workspace/current/`
  레인 런북 경로를 주장 → 실측(502/502, StageEntry 9건, archive 이동)으로 정정
  하고 html/pdf/png 파생물 재생성(`07711c8`). 세션 런타임 경로 gitignore 편입
  (`e0cb9a4`). origin/main·origin/akillness/main 동기화, gh-pages는 런타임 경로
  무변경(`Assets|Packages|ProjectSettings` diff 0건)이라 재배포 불필요.
  교훈: `wiki/concepts/stale-branch-merges-clean-but-lies.md`.
- 2026-08-07 — feature: 명령 에이전트(텍스트 커맨드 → 순서 있는 시퀀스 → 이벤트
  완료마다 다음 단계). `CommandPlan.cs`(위치순 로컬 스캔 + Gemini JSON 파서),
  `CommandAgent.cs`(Gate/Ack/Settle 러너), `HudView.CommandAgent.cs`(글루),
  `GeminiCommandClient.Plan`(단일 의도 분류기 대체), `Assets/Editor/GeminiDevKey.cs`
  (에디터 전용 `.env.game-audio` 키 로더). 게이트 35/35(신규 2픽스처) —
  Unity 에디터 점유로 배치 러너 불가, msbuild(Roslyn) 컴파일 + net8.0 NUnit
  하네스로 실행. `.env.game-audio` 키는 전 모델 429(크레딧 소진)라 라이브 왕복
  미검증. 지식: `wiki/entities/hongt-command-agent-sequence-console.md`.
- 2026-08-07 — AMENDMENT #12 던전 환경 + 기획문서 최신화: SIM_SPEC_ENVIRONMENT
  신설(제안→frozen, §E8 8게이트 초록), HACKSLASH 부록 A(개정 원장)·B(정오표
  10건), CAMPAIGN supersede, DUNGEON_GUIDE 갱신. EnvironmentBuilder(Zone A/B/C
  링, FNV-1a 결정론, Light 4, 콜라이더 0) + 커버리지 게이트(bare 0.0000 실측,
  핀 0.02). EditMode 571/571 · WebGL 70.8MB 0 error · gh-pages 6c163b5 배포·
  라이브 확인. 결론 개념화: wiki/concepts/hongt-environment-amendment-12.
