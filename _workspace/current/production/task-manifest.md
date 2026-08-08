# Task Manifest — run-id 20260807-codex-readability

next_public_beat = "gh-pages 라이브 — 처음 들어온 사람이 외부 문서 없이 플레이한다"

AMENDMENT #10. 전 사이클(20260807-ingame-guidance)은 cycle-6 회고로 마감.
결함 주도 Stage 2 재진입. 선행: `.omc/specs/deep-interview-codex-readability.md`
(Round 0 + 3R, 13.3%).

| task | owner | stage.phase | artifact | gate | status |
|---|---|---|---|---|---|
| deep-interview 요구사항 확정 | director | 0 | .omc/specs/deep-interview-codex-readability.md | — | done |
| 인테이크 브리프 | director | 0 | intake/production-brief.md | — | done |
| 결함 재현 (해석적 좌표) | director+qa | 2.a | 아래 §재현 | G4 | done |
| 아이콘 2종 생성 | programmer | 2.d | Assets/Resources/Icons/ui-{codex,abandon}.png | G4 | done |
| 탭 이름 + 헤더 주석 | programmer | 2.d | View/HudViewCodex.cs | G1 | done |
| 그룹 페이징 레이아웃 | programmer | 2.d | View/HudViewCodex.cs | G4 | done |
| 버튼 아이콘 배선 | programmer | 2.d | View/HudView.cs | G4 | done |
| 폰트 재생성 (538 → 540) | programmer | 2.d | Assets/Resources/Fonts/HudKorean.otf | G1 | done |
| 글리프 실측 + 변이 증명 테스트 | qa | 2.d | Tests/EditMode/GuidanceTests.cs | G4 | done |
| 게이트 판정 | director | 2.gate | production/gate-reviews/stage2-gates-v20.md | 전체 | done |
| 회고 + 룰 파일 재도출 | director | close | retrospectives/cycle-7-retrospective.md | — | done |
| origin/main 머지 (116커밋) | director | close | 아래 §머지 | — | done |

## 머지 — origin/main 116커밋, 충돌 10파일 24 hunk

개정 번호가 갈라져 있었다. main이 훈련장·돌발을 **#10**으로 재정리했는데
이 레인은 그것을 #7로 부르며 #8·#9·#10을 새로 썼다. 부록 A의 규칙대로
헤딩은 남기고 원장에 canonical 16·17·18로 등록했다 — 코드 주석과 커밋이
도착 헤딩을 인용하므로 문자열을 밀면 그 인용이 끊긴다.

머지가 통합 결함 3건을 드러냈다. 어느 쪽 브랜치도 단독으로는 갖지 않던 상태다:

| # | 결함 | 원인 |
|---|---|---|
| 1 | `FillSprite`·`AsFilled`/`MakeFilled` 이중 정의 | 양쪽이 같은 헬퍼를 다른 이름으로 만듦 (CS0111) |
| 2 | 아코디언 70u 피치 × 터치 106u 카드 = 36u 겹침 | main은 카드를 키우고 이 레인은 피치를 동결 |
| 3 | `Bar()`가 아틀라스 스프라이트를 1×1로 덮어씀 | main이 자기 주석과 반대 순서로 호출 |

3번은 main 단독 결함이다. 다른 세 곳(`:2282`·`:2426`·`:2457`)에 "헬퍼 먼저,
스프라이트 나중"이라고 적어놓고 `Bar()`에서만 뒤집었다. 게이지는 정상 동작하되
그라디언트 대신 단색으로 렌더된다 — §4k의 더 조용한 형태다.

## 재현 — 결함 2 (해석적, 좌표 정확) `[OBSERVED]`

3열 배치 `colW = (620−56−32)/3 = 177u`, 본문 rect 폭 169u, 행 피치 26u:

```
분출구 본문:  rect x[8,177]   RENDERED x[8,306]   y[-47,-33]
이동   본문:  rect x[201,371]                     y[-47,-33]
겹침 = 104u × 14u = 1,459u²
```

원인: `HudView.cs:2407` `Label()`이 `HorizontalWrapMode.Overflow` 기본.
코덱스 본문만 `Wrap` 미지정 (안내 카드 `:1090`·이탈 모달 `:1455`는 지정).

wrap 단독 실패: 분출구 2줄 = 22u > rect 14u, 피치 26u → 다음 제목과 세로 9u 겹침.

예산 (실측 글리프 폭):

| 배치 | 열 폭 | 최심 열 | 300u 대비 |
|---|---|---|---|
| 2열 | 274u | 508u | 208u 초과 |
| 3열 | 177u | 386u | 86u 초과 |
| 4열 | 129u | 380u | 80u 초과 |
| **그룹 페이징 2열** | 274u | 최악 155u (조작) | **여유 47u** |

## 확정 기하

```
칩 행:  5개 × 90.2u 정사각 (44 CSS px ÷ 0.488), 균등 분배 gap 28.25u
본문:   y −98.2..−300 = 201.8u, 2열 colW 274u, 본문 라벨 266u
행:     제목 15u + 실측 wrap 줄수 × 11u + 간격 5u
기본:   위험 그룹 (GroupOrder 주석: "Hazards first — they kill a player who
        does not know them")
```

**칩은 모든 티어에서 90.2u.** 티어별 분기를 만들지 않는다 — §4f가 경고한
"정적 y는 한 구성에서만 맞다"의 반대편 실수를 피한다. 최악 그룹이 155u라
201.8u 예산에 여유 47u가 남으므로 티어 분기의 이득이 없다.

## 폰트

`게임설명`의 `임`·`설`이 배포 서브셋(538 글리프)에 **없음**.
소스 `NanumBarunGothic.otf`(22,836)에는 **있음** → 재생성으로 닫힘.

## 교차 세션 주의

`.vscode/settings.json`, `Data/Plugins/lib_burst_generated.wasm`, `HongT.slnx`,
`Packages/*`, `ProjectSettings/PackageManagerSettings.asset`은 다른 세션 작업.
스테이징은 명시 pathspec만 (§5).
