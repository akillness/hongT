# Production Brief — run-id 20260807-codex-readability

## bmad-gds schema

| 항목 | 값 |
|---|---|
| game_type | 아이소메트릭 핵앤슬래시 (Cinder Court, Unity 6000.5.6f1 / URP / WebGL) |
| team_shape | 5-agent harness (director / designer / pm / programmer / qa) |
| engine | Unity 6000.5.6f1, URP 17.5, WebGL |
| current_stage | **Stage 2 재진입** — 결함 주도. cycle-6 마감 후 사용자 제보 3건 |
| next_public_beat | gh-pages 라이브 — 처음 들어온 사람이 외부 문서 없이 플레이한다 |
| source_packet | 사용자 제보 + `.omc/specs/deep-interview-codex-readability.md` (3R, 13.3%) |
| main_constraint | 순수 뷰 개정 — 심 무변경, 저장 필드 무변경, 골든 무이동 |
| main_question | 정보 패널 안내 탭이 읽히는가 |

## 운영 모드 (이번 사이클 하나만)

**결함 해소 + 가독성.** 신규 기능 없음. 컨셉 작업 없음.

## 사용자 제보 3건

1. 안내 서브탭 이름이 `기록`인데 내용은 게임설명·진행방법·승리/실패 조건
2. 분출구 설명과 이동 설명이 겹쳐 있음
3. `정보`/`포기` 버튼에 쓸 수 있는 아이콘이 필요

## 인터뷰가 확정한 것

- 그룹 페이징 (패널 620×440 불변, 칩 5개, 한 그룹만 펼침, 기본=위험)
- 아이콘 + 텍스트 (아이콘만은 기각 — 오탭 1회에 런 소실)
- 실측 글리프 폭 검사 + 변이 증명 (rect 담김 검사는 이 결함을 통과시켰다)

## 게이트 대상

| 게이트 | 사유 |
|---|---|
| G1 | 새 문자열 `게임설명`이 worldview와 충돌하지 않는지 |
| G4 | 가독성 = 몰입. 겹침은 즉각적 몰입 파괴 |
| G6 | 폰트 재생성·빌드 errors 0·브라우저 스모크 |
| G7/G8 | 무변경 — 코어루프·참신성 건드리지 않음. 이월 |

## 캐리포워드 (건드리지 않음)

`.vscode/settings.json`, `Data/Plugins/lib_burst_generated.wasm`, `HongT.slnx`,
`Packages/*`, `ProjectSettings/PackageManagerSettings.asset` — 다른 세션 작업.
스테이징은 명시 pathspec만 (§5).
